using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Terra_Namp;

public static class YtDlp
{
    private static string _binaryPath;
    private static string _qjsPath;
    private static readonly SemaphoreSlim _installLock = new(1, 1);

    private static string BinaryName
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "yt-dlp.exe";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "yt-dlp_linux";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "yt-dlp_macos";
            return "yt-dlp";
        }
    }

    private static string DownloadUrl =>
        $"https://github.com/yt-dlp/yt-dlp/releases/latest/download/{BinaryName}";

    private const string QjsVersion = "2025-09-13";

    private static string QjsZipUrl
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return $"https://bellard.org/quickjs/binary_releases/quickjs-win-x86_64-{QjsVersion}.zip";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return $"https://bellard.org/quickjs/binary_releases/quickjs-cosmo-{QjsVersion}.zip";
            return $"https://bellard.org/quickjs/binary_releases/quickjs-linux-x86_64-{QjsVersion}.zip";
        }
    }

    private static string QjsBinaryName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "qjs.exe" : "qjs";

    public static async Task<string> EnsureInstalledAsync(CancellationToken ct = default)
    {
        string ytdlpPath = Path.Combine(Terra_Namp.CachePath, BinaryName);
        string qjsPath = Path.Combine(Terra_Namp.CachePath, QjsBinaryName);
        string versionFile = Path.Combine(Terra_Namp.CachePath, "quickjs.ver");

        bool qjsUpToDate = File.Exists(qjsPath) &&
                            File.Exists(versionFile) &&
                            File.ReadAllText(versionFile).Trim() == QjsVersion;

        if (File.Exists(ytdlpPath) && qjsUpToDate)
        {
            _binaryPath = ytdlpPath;
            _qjsPath = qjsPath;
            return ytdlpPath;
        }

        await _installLock.WaitAsync(ct);
        try
        {
            if (!File.Exists(ytdlpPath))
                await DownloadFileAsync(DownloadUrl, ytdlpPath, ct);

            qjsUpToDate = File.Exists(qjsPath) &&
                           File.Exists(versionFile) &&
                           File.ReadAllText(versionFile).Trim() == QjsVersion;

            if (!qjsUpToDate)
                await DownloadAndExtractQjsAsync(ct);

            _binaryPath = ytdlpPath;
            _qjsPath = qjsPath;
            return ytdlpPath;
        }
        finally
        {
            _installLock.Release();
        }
    }

    private static async Task DownloadAndExtractQjsAsync(CancellationToken ct)
    {
        string cachePath = Terra_Namp.CachePath;
        string zipPath = Path.Combine(cachePath, "quickjs.zip");

        await DownloadFileAsync(QjsZipUrl, zipPath, ct, setExecutable: false);

        using (var zip = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in zip.Entries)
            {
                string name = entry.Name;
                // Extract qjs binary and its Windows dependency
                if (name == "qjs" || name == "qjs.exe" || name == "libwinpthread-1.dll")
                {
                    string destPath = Path.Combine(cachePath, name);
                    entry.ExtractToFile(destPath, overwrite: true);

                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && name == "qjs")
                    {
                        File.SetUnixFileMode(destPath,
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                    }
                }
            }
        }

        File.Delete(zipPath);
        File.WriteAllText(Path.Combine(cachePath, "quickjs.ver"), QjsVersion);
    }

    private static async Task DownloadFileAsync(string url, string destPath, CancellationToken ct, bool setExecutable = true)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Terra_Namp-tModLoader/2.0");
        http.Timeout = TimeSpan.FromMinutes(5);

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        string tempPath = destPath + ".tmp";
        await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await response.Content.CopyToAsync(fs, ct);
        }

        File.Move(tempPath, destPath, overwrite: true);

        if (setExecutable && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(destPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    public static Process Run(string args)
    {
        if (string.IsNullOrEmpty(_binaryPath))
            throw new InvalidOperationException("yt-dlp is not installed. Call EnsureInstalledAsync first.");

        string fullArgs = !string.IsNullOrEmpty(_qjsPath)
            ? $"--js-runtimes \"quickjs:{_qjsPath}\" {args}"
            : args;

        var info = new ProcessStartInfo
        {
            FileName = _binaryPath,
            Arguments = fullArgs,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // Force Python (yt-dlp runtime) to use UTF-8 for stdout/stderr.
        // Without this, Python on Windows uses the system code page (e.g. cp1251)
        // when stdout is redirected, corrupting Cyrillic and other non-ASCII characters.
        info.Environment["PYTHONIOENCODING"] = "utf-8";
        info.Environment["PYTHONUTF8"] = "1";

        // Force unbuffered stdout/stderr so progress lines reach us immediately
        // instead of being stuck in Python's internal buffer until process exit.
        info.Environment["PYTHONUNBUFFERED"] = "1";

        return Process.Start(info);
    }
}
