using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace Terra_Namp.Networking;

public class SongRegistry : ModSystem
{
    private readonly Dictionary<string, string> hashToUuid = new();
    private readonly Dictionary<string, string> uuidToHash = new();

    public static SongRegistry Instance { get; private set; }

    public override void Load()
    {
        Instance = this;
    }

    public override void Unload()
    {
        Instance = null;
    }

    public override void PostSetupContent()
    {
        ScanCache();
    }

    public void ScanCache()
    {
        hashToUuid.Clear();
        uuidToHash.Clear();

        string cachePath = Terra_Namp.CachePath;

        if (!Directory.Exists(cachePath))
        {
            NetLogger.Info($"ScanCache: cache directory not found ({cachePath})");
            return;
        }

        int total = 0;
        int legacy = 0;

        foreach (string txtFile in Directory.GetFiles(cachePath, "*.txt"))
        {
            string name = Path.GetFileNameWithoutExtension(txtFile);
            string mp3File = Path.Combine(cachePath, $"{name}.mp3");

            if (!File.Exists(mp3File))
                continue;

            string[] lines = File.ReadAllLines(txtFile);

            string hash;
            if (lines.Length >= 3 && lines[2].Length == 32)
            {
                hash = lines[2];
            }
            else
            {
                byte[] hashBytes = ContentHash.ComputeHash(mp3File);
                hash = ContentHash.HashToHex(hashBytes);

                // Write hash back to the metadata file.
                File.AppendAllText(txtFile, $"\n{hash}");
                legacy++;
            }

            // On dedicated server, file names ARE the hash.
            // On clients, file names are UUIDs.
            hashToUuid[hash] = name;
            uuidToHash[name] = hash;
            total++;
        }

        NetLogger.Info($"ScanCache: found {total} songs in cache ({legacy} legacy files hashed)");
    }

    public string GetUuidByHash(string hash)
        => hashToUuid.TryGetValue(hash, out string uuid) ? uuid : null;

    public string GetHashByUuid(string uuid)
        => uuidToHash.TryGetValue(uuid, out string hash) ? hash : null;

    public bool HasHash(string hash) => hashToUuid.ContainsKey(hash);

    public void RegisterSong(string uuid, string hash)
    {
        NetLogger.Info($"RegisterSong: uuid={uuid[..8]}.. hash={hash[..8]}..");
        hashToUuid[hash] = uuid;
        uuidToHash[uuid] = hash;
    }

    public string GetServerCachePath(string hashHex)
        => Path.Combine(Terra_Namp.CachePath, $"{hashHex}.mp3");

    public string GetServerMetaPath(string hashHex)
        => Path.Combine(Terra_Namp.CachePath, $"{hashHex}.txt");
}
