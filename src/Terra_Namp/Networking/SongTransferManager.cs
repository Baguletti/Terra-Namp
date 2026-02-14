using System;
using System.Collections.Generic;
using System.IO;
using Terra_Namp.Content.UI.TerraUI;
using Terra_Namp.Core.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terra_Namp.Networking;

public struct PendingPlayback
{
    public string HashHex;
    public string Title;
    public bool Forced;
    public float SeekProgress;
    public bool SlowedReverb;
}

public class InboundTransfer
{
    public string HashHex;
    public int TotalSize;
    public string Title;
    public string Author;
    public MemoryStream Buffer = new();
    public int NextExpectedChunkIndex;
}

public class OutboundTransfer
{
    public string HashHex;
    public byte[] Hash;
    public string FilePath;
    public int TotalSize;
    public int NextChunkIndex;
    public int TargetClient; // -1 = server, specific client index otherwise
    public bool Complete;
}

public class ServerTransfer
{
    public string HashHex;
    public int TotalSize;
    public string Title;
    public string Author;
    public int SourceClient;
    public List<int> WaitingClients = new();
    public MemoryStream Buffer = new();
    public int ChunksReceived;
    public bool Complete;
}

public class SongTransferManager : ModSystem
{
    public const int ChunkSize = 8192;
    public const int ChunksPerTick = 4;
    public const int MaxConcurrentPrefetch = 2;

    public static SongTransferManager Instance { get; private set; }

    // Client: pending playback requests (waiting for song transfer)
    private readonly Dictionary<string, PendingPlayback> pendingPlaybacks = new();

    // Client: active inbound transfers
    private readonly Dictionary<string, InboundTransfer> inboundTransfers = new();

    // Client/Server: outbound chunk queues
    private readonly List<OutboundTransfer> outboundTransfers = new();

    // Server: active relay transfers
    private readonly Dictionary<string, ServerTransfer> serverTransfers = new();

    // Client: prefetch queue
    private readonly Queue<(string HashHex, byte[] Hash)> prefetchQueue = new();
    private readonly HashSet<string> prefetchRequested = new();
    private int activePrefetchCount;

    public override void Load()
    {
        Instance = this;
    }

    public override void Unload()
    {
        Instance = null;
    }

    public override void PostUpdateInput()
    {
        ProcessOutboundQueue();

        if (Main.netMode == NetmodeID.MultiplayerClient)
            ProcessPrefetchQueue();
    }

    // --- Client methods ---

    public void SetPendingPlayback(string hashHex, string title, string author, bool forced, float seekProgress = 0f, bool slowedReverb = false)
    {
        NetLogger.Transfer($"SetPendingPlayback: hash={hashHex[..8]}.. title=\"{title}\" forced={forced} seek={seekProgress:F3} slowedReverb={slowedReverb}");
        pendingPlaybacks[hashHex] = new PendingPlayback
        {
            HashHex = hashHex,
            Title = title,
            Forced = forced,
            SeekProgress = seekProgress,
            SlowedReverb = slowedReverb,
        };
    }

    public void OnSongHeaderReceived(string hashHex, int totalSize, string title, string author)
    {
        NetLogger.Transfer($"SongHeader received: hash={hashHex[..8]}.. size={totalSize} title=\"{title}\"");
        inboundTransfers[hashHex] = new InboundTransfer
        {
            HashHex = hashHex,
            TotalSize = totalSize,
            Title = title,
            Author = author,
        };
    }

    public void OnSongChunkReceived(string hashHex, int chunkIndex, byte[] data, int length)
    {
        if (!inboundTransfers.TryGetValue(hashHex, out var transfer))
            return;

        transfer.Buffer.Write(data, 0, length);
        transfer.NextExpectedChunkIndex = chunkIndex + 1;

        if (chunkIndex % 50 == 0)
            NetLogger.Transfer($"Inbound chunk #{chunkIndex}: hash={hashHex[..8]}.. received={transfer.Buffer.Length}/{transfer.TotalSize}");
    }

    public void OnSongTransferComplete(string hashHex)
    {
        if (!inboundTransfers.TryGetValue(hashHex, out var transfer))
            return;

        inboundTransfers.Remove(hashHex);
        NetLogger.Transfer($"Transfer complete: hash={hashHex[..8]}.. totalBytes={transfer.Buffer.Length}");

        byte[] fileData = transfer.Buffer.ToArray();
        transfer.Buffer.Dispose();

        // Verify hash.
        using var md5 = System.Security.Cryptography.MD5.Create();
        byte[] computedHash = md5.ComputeHash(fileData);
        string computedHex = ContentHash.HashToHex(computedHash);

        if (computedHex != hashHex)
        {
            NetLogger.Error($"Hash mismatch! expected={hashHex[..8]}.. got={computedHex[..8]}.. — discarding");
            return;
        }

        NetLogger.Transfer($"Hash verified OK: {hashHex[..8]}..");

        // Save to local cache.
        string uuid = Guid.NewGuid().ToString();
        string mp3Path = Path.Combine(Terra_Namp.CachePath, $"{uuid}.mp3");
        string txtPath = Path.Combine(Terra_Namp.CachePath, $"{uuid}.txt");

        File.WriteAllBytes(mp3Path, fileData);
        File.WriteAllText(txtPath, $"{transfer.Title}{Environment.NewLine}{transfer.Author}{Environment.NewLine}{hashHex}");

        SongRegistry.Instance.RegisterSong(uuid, hashHex);
        NetLogger.Transfer($"Saved to cache: uuid={uuid[..8]}.. file={mp3Path}");

        // Track prefetch completion.
        if (prefetchRequested.Remove(hashHex))
        {
            activePrefetchCount = Math.Max(0, activePrefetchCount - 1);
            NetLogger.Transfer($"Prefetch complete: hash={hashHex[..8]}.. (active={activePrefetchCount}/{MaxConcurrentPrefetch})");
        }

        // If there's a pending playback for this hash, start playing.
        if (pendingPlaybacks.TryGetValue(hashHex, out var pending))
        {
            pendingPlaybacks.Remove(hashHex);
            NetLogger.Transfer($"Pending playback found for hash={hashHex[..8]}.. forced={pending.Forced} seek={pending.SeekProgress:F3} — starting playback");

            Main.QueueMainThreadAction(() =>
            {
                var panel = TerraUILoader.GetUIState<TerraState>()?.MainPanel;
                if (pending.SeekProgress > 0f)
                    panel?.BeginPlayingSongFromNetwork(uuid, pending.Forced, pending.SeekProgress);
                else
                    panel?.BeginPlayingSongFromNetwork(uuid, pending.Forced);

                if (panel != null)
                    panel.SlowedReverbActive = pending.SlowedReverb;
                if (pending.SlowedReverb)
                    panel?.ActiveSong?.ApplySlowedReverbFromNetwork(true);
            });
        }
        else
        {
            NetLogger.Transfer($"No pending playback for hash={hashHex[..8]}.. (cached for future use)");
        }
    }

    // --- Client prefetch methods ---

    public bool QueuePrefetch(string hashHex, byte[] hash)
    {
        if (SongRegistry.Instance.HasHash(hashHex))
            return false;
        if (prefetchRequested.Contains(hashHex))
            return false;
        if (inboundTransfers.ContainsKey(hashHex))
            return false;

        prefetchRequested.Add(hashHex);
        prefetchQueue.Enqueue((hashHex, hash));
        return true;
    }

    private void ProcessPrefetchQueue()
    {
        while (activePrefetchCount < MaxConcurrentPrefetch && prefetchQueue.Count > 0)
        {
            var (hashHex, hash) = prefetchQueue.Dequeue();

            // Skip if already cached (may have arrived via normal transfer).
            if (SongRegistry.Instance.HasHash(hashHex))
            {
                prefetchRequested.Remove(hashHex);
                continue;
            }

            // Skip if already being transferred.
            if (inboundTransfers.ContainsKey(hashHex))
                continue;

            activePrefetchCount++;
            NetLogger.Transfer($"Prefetch: requesting hash={hashHex[..8]}.. (active={activePrefetchCount}/{MaxConcurrentPrefetch}, queued={prefetchQueue.Count})");
            PacketBuilder.RequestSong((byte)Main.myPlayer, hash).Send();
        }
    }

    // --- Server methods ---

    public bool HasServerTransfer(string hashHex) => serverTransfers.ContainsKey(hashHex);

    public void CreateServerTransfer(string hashHex, int sourceClient, List<int> waitingClients)
    {
        NetLogger.Transfer($"CreateServerTransfer: hash={hashHex[..8]}.. source=client{sourceClient} waiting=[{string.Join(",", waitingClients)}]");
        serverTransfers[hashHex] = new ServerTransfer
        {
            HashHex = hashHex,
            SourceClient = sourceClient,
            WaitingClients = new List<int>(waitingClients),
        };
    }

    public void AddWaitingClient(string hashHex, int clientIndex)
    {
        if (serverTransfers.TryGetValue(hashHex, out var transfer))
        {
            if (!transfer.WaitingClients.Contains(clientIndex))
            {
                transfer.WaitingClients.Add(clientIndex);
                NetLogger.Transfer($"AddWaitingClient: client{clientIndex} added to hash={hashHex[..8]}.. (total waiting: {transfer.WaitingClients.Count})");
            }
        }
    }

    public void OnServerSongHeaderReceived(string hashHex, int totalSize, string title, string author)
    {
        if (!serverTransfers.TryGetValue(hashHex, out var transfer))
            return;

        transfer.TotalSize = totalSize;
        transfer.Title = title;
        transfer.Author = author;

        NetLogger.Transfer($"Server relay header: hash={hashHex[..8]}.. size={totalSize} -> forwarding to {transfer.WaitingClients.Count} clients");

        // Forward header to all waiting clients.
        byte[] hash = ContentHash.HexToHash(hashHex);
        foreach (int client in transfer.WaitingClients)
        {
            var packet = PacketBuilder.SongHeader(hash, totalSize, title, author);
            packet.Send(client);
        }
    }

    public void OnServerSongChunkReceived(string hashHex, int chunkIndex, byte[] data, int length)
    {
        if (!serverTransfers.TryGetValue(hashHex, out var transfer))
            return;

        transfer.Buffer.Write(data, 0, length);
        transfer.ChunksReceived = chunkIndex + 1;

        if (chunkIndex % 50 == 0)
            NetLogger.Transfer($"Server relay chunk #{chunkIndex}: hash={hashHex[..8]}.. buffered={transfer.Buffer.Length} -> {transfer.WaitingClients.Count} clients");

        // Forward chunk to all waiting clients.
        byte[] hash = ContentHash.HexToHash(hashHex);
        foreach (int client in transfer.WaitingClients)
        {
            var packet = PacketBuilder.SongChunk(hash, chunkIndex, data, length);
            packet.Send(client);
        }
    }

    public void OnServerSongTransferComplete(string hashHex)
    {
        if (!serverTransfers.TryGetValue(hashHex, out var transfer))
            return;

        NetLogger.Transfer($"Server transfer complete: hash={hashHex[..8]}.. totalBytes={transfer.Buffer.Length} chunks={transfer.ChunksReceived}");

        // Save to server cache.
        byte[] fileData = transfer.Buffer.ToArray();
        transfer.Buffer.Dispose();
        transfer.Complete = true;

        string cachePath = SongRegistry.Instance.GetServerCachePath(hashHex);
        string metaPath = SongRegistry.Instance.GetServerMetaPath(hashHex);

        File.WriteAllBytes(cachePath, fileData);
        File.WriteAllText(metaPath, $"{transfer.Title}{Environment.NewLine}{transfer.Author}{Environment.NewLine}{hashHex}");

        SongRegistry.Instance.RegisterSong(hashHex, hashHex);
        NetLogger.Transfer($"Server cached: hash={hashHex[..8]}.. path={cachePath}");

        // Forward completion to waiting clients.
        byte[] hash = ContentHash.HexToHash(hashHex);
        foreach (int client in transfer.WaitingClients)
        {
            var packet = PacketBuilder.SongTransferComplete(hash);
            packet.Send(client);
        }

        NetLogger.Transfer($"Transfer complete forwarded to {transfer.WaitingClients.Count} clients, cleaning up relay");
        serverTransfers.Remove(hashHex);
    }

    // --- Outbound queue (client sending chunks to server) ---

    public void BeginOutboundTransfer(string hashHex, string filePath, int targetClient = -1)
    {
        byte[] hash = ContentHash.HexToHash(hashHex);
        int fileSize = (int)new FileInfo(filePath).Length;

        int totalChunks = (fileSize + ChunkSize - 1) / ChunkSize;
        NetLogger.Transfer($"BeginOutboundTransfer: hash={hashHex[..8]}.. size={fileSize} chunks={totalChunks} target={(targetClient >= 0 ? $"client{targetClient}" : "server")}");

        outboundTransfers.Add(new OutboundTransfer
        {
            HashHex = hashHex,
            Hash = hash,
            FilePath = filePath,
            TotalSize = fileSize,
            NextChunkIndex = 0,
            TargetClient = targetClient,
        });
    }

    private void ProcessOutboundQueue()
    {
        int chunksThisTick = 0;

        for (int i = outboundTransfers.Count - 1; i >= 0; i--)
        {
            if (chunksThisTick >= ChunksPerTick)
                break;

            var transfer = outboundTransfers[i];

            if (transfer.NextChunkIndex == 0)
            {
                NetLogger.Transfer($"Outbound: sending header for hash={transfer.HashHex[..8]}.. size={transfer.TotalSize}");
                // Send header first.
                string title = "";
                string author = "";

                // Read metadata for this file.
                string uuid = SongRegistry.Instance.GetUuidByHash(transfer.HashHex);
                if (uuid != null)
                {
                    string txtPath = Path.Combine(Terra_Namp.CachePath, $"{uuid}.txt");
                    if (File.Exists(txtPath))
                    {
                        string[] lines = File.ReadAllLines(txtPath);
                        if (lines.Length >= 1) title = lines[0];
                        if (lines.Length >= 2) author = lines[1];
                    }
                }

                var headerPacket = PacketBuilder.SongHeader(transfer.Hash, transfer.TotalSize, title, author);
                if (transfer.TargetClient >= 0)
                    headerPacket.Send(transfer.TargetClient);
                else
                    headerPacket.Send();
            }

            try
            {
                using var fs = new FileStream(transfer.FilePath, FileMode.Open, FileAccess.Read);
                long offset = (long)transfer.NextChunkIndex * ChunkSize;
                fs.Seek(offset, SeekOrigin.Begin);

                byte[] buffer = new byte[ChunkSize];
                int remaining = ChunksPerTick - chunksThisTick;

                for (int c = 0; c < remaining; c++)
                {
                    int bytesRead = fs.Read(buffer, 0, ChunkSize);
                    if (bytesRead <= 0)
                    {
                        // Transfer done.
                        NetLogger.Transfer($"Outbound complete: hash={transfer.HashHex[..8]}.. totalChunks={transfer.NextChunkIndex}");
                        var completePacket = PacketBuilder.SongTransferComplete(transfer.Hash);
                        if (transfer.TargetClient >= 0)
                            completePacket.Send(transfer.TargetClient);
                        else
                            completePacket.Send();

                        transfer.Complete = true;
                        break;
                    }

                    var chunkPacket = PacketBuilder.SongChunk(transfer.Hash, transfer.NextChunkIndex, buffer, bytesRead);
                    if (transfer.TargetClient >= 0)
                        chunkPacket.Send(transfer.TargetClient);
                    else
                        chunkPacket.Send();

                    transfer.NextChunkIndex++;
                    chunksThisTick++;
                }
            }
            catch (Exception ex)
            {
                NetLogger.Error($"Outbound transfer error: hash={transfer.HashHex[..8]}.. {ex.Message}");
                transfer.Complete = true;
            }

            if (transfer.Complete)
                outboundTransfers.RemoveAt(i);
        }
    }

    // --- Server: serve from cache ---

    public void ServeFromServerCache(string hashHex, int targetClient)
    {
        string cachePath = SongRegistry.Instance.GetServerCachePath(hashHex);
        if (!File.Exists(cachePath))
        {
            NetLogger.Error($"ServeFromServerCache: file not found for hash={hashHex[..8]}.. path={cachePath}");
            return;
        }

        NetLogger.Transfer($"ServeFromServerCache: hash={hashHex[..8]}.. -> client{targetClient}");
        BeginOutboundTransfer(hashHex, cachePath, targetClient);
    }
}
