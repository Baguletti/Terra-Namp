using System.Diagnostics;
using System.IO;
using Terraria.ModLoader;
using System.Runtime.InteropServices;
using System.IO.Compression;


namespace Terra_Namp;

public static class FFmpeg
{
    private static string platformBinary;

    public static void Initialise(Mod mod)
    {
        platformBinary = GetPlatformBinary();

        string path = Terra_Namp.CachePath;

        string binaryPath = Path.Combine(path, platformBinary);

        if (!File.Exists(binaryPath))
        {
            using Stream ffmpegStream = mod.GetFileStream($"FFmpeg/{platformBinary}.gz");
            ExtractGZipFile(ffmpegStream, binaryPath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                mod.Logger.Info($"[Linux Compatibility] Making ffmpeg executable. Location: {binaryPath}.");
                File.SetUnixFileMode(binaryPath, UnixFileMode.UserExecute);
            }
        }

        EnsureStandardNameExists();
    }

    /// <summary>
    /// Creates a standard-named ffmpeg binary (ffmpeg / ffmpeg.exe) so that
    /// yt-dlp can find it via --ffmpeg-location pointing to the cache directory.
    /// </summary>
    public static void EnsureStandardNameExists()
    {
        string path = Terra_Namp.CachePath;
        string standardName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        string standardPath = Path.Combine(path, standardName);

        if (File.Exists(standardPath))
            return;

        string sourcePath = Path.Combine(path, GetPlatformBinary());
        if (!File.Exists(sourcePath))
            return;

        try
        {
            File.CreateSymbolicLink(standardPath, sourcePath);
        }
        catch
        {
            File.Copy(sourcePath, standardPath);
        }
    }

    private static string GetPlatformBinary()
    {
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return $"ffmpeg64W.exe";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return $"ffmpeg64L";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "ffmpeg64M";
        }

        return "How are you using tModLoader on FreeBSD?";
    }

    private static void ExtractGZipFile(Stream stream, string path)
    {
        using (FileStream decompressedFileStream = File.Create(path))
        {
            using (GZipStream decompressionStream = new(stream, CompressionMode.Decompress))
            {
                decompressionStream.CopyTo(decompressedFileStream);
            }
        }

        stream.Close();
    }

    public static Process Run(string path, string args)
    {
        ProcessStartInfo info = new()
        {
            FileName = Path.Combine(path, platformBinary),
            Arguments = args,
            CreateNoWindow = true
        };

        return Process.Start(info);
    }
}
