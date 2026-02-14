using Terra_Namp.Networking;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Terraria;

namespace Terra_Namp.Core.Services;

/// <summary>
/// Shared service for importing local audio files.
/// Handles FFmpeg conversion, hash computation, and metadata writing.
/// </summary>
public static class FileImportService
{
    /// <summary>
    /// Result of a successful file import (returned on background thread).
    /// </summary>
    public readonly struct ImportResult
    {
        public readonly string Uuid;
        public readonly string DestPath;
        public readonly string HashHex;

        public ImportResult(string uuid, string destPath, string hashHex)
        {
            Uuid = uuid;
            DestPath = destPath;
            HashHex = hashHex;
        }
    }

    /// <summary>
    /// Imports a single audio file: converts via FFmpeg with loudnorm, computes MD5 hash,
    /// and writes metadata .txt. Runs FFmpeg on a background thread.
    ///
    /// Callbacks are invoked on the main thread via Main.QueueMainThreadAction.
    /// </summary>
    /// <param name="sourcePath">Path to the source MP3 file</param>
    /// <param name="destinationDir">Directory to place the converted file</param>
    /// <param name="displayName">Display name for metadata (line 0)</param>
    /// <param name="author">Author for metadata (line 1)</param>
    /// <param name="folderName">Folder tag for metadata (line 3)</param>
    /// <param name="onSuccess">Called on main thread with import result</param>
    /// <param name="onFail">Called on main thread with error message</param>
    public static void ImportFileAsync(
        string sourcePath,
        string destinationDir,
        string displayName,
        string author,
        string folderName,
        Action<ImportResult> onSuccess,
        Action<string> onFail)
    {
        string uuid = Guid.NewGuid().ToString();
        string destPath = Path.Combine(destinationDir, $"{uuid}.mp3");

        Task.Run(() =>
        {
            try
            {
                string args = $"-i \"{sourcePath}\" -vn -af loudnorm -f mp3 -ab 320k \"{destPath}\"";
                using Process process = FFmpeg.Run(Terra_Namp.CachePath, args);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    TryDelete(destPath);
                    Main.QueueMainThreadAction(() => onFail?.Invoke($"FFmpeg failed for {displayName}"));
                    return;
                }

                byte[] hashBytes = ContentHash.ComputeHash(destPath);
                string hashHex = ContentHash.HashToHex(hashBytes);

                // Write metadata file
                string metaFile = Path.Combine(destinationDir, $"{uuid}.txt");
                File.WriteAllText(metaFile,
                    $"{displayName}{Environment.NewLine}" +
                    $"{author}{Environment.NewLine}" +
                    $"{hashHex}{Environment.NewLine}" +
                    $"{folderName ?? ""}");

                var result = new ImportResult(uuid, destPath, hashHex);
                Main.QueueMainThreadAction(() => onSuccess?.Invoke(result));
            }
            catch (Exception ex)
            {
                TryDelete(destPath);
                Main.QueueMainThreadAction(() => onFail?.Invoke(ex.Message));
            }
        });
    }

    /// <summary>
    /// Removes an imported file and its metadata. Safe to call if files don't exist.
    /// </summary>
    public static void CleanupImport(string destinationDir, string uuid)
    {
        TryDelete(Path.Combine(destinationDir, $"{uuid}.mp3"));
        TryDelete(Path.Combine(destinationDir, $"{uuid}.txt"));
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
