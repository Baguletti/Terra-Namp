using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Terra_Namp.Core.Services;

public static class SongCacheService
{
    public record SongMetadata(string Title, string Author, string Hash, string Folder);

    /// <summary>
    /// Reads all songs from cache, returns filtered and sorted list plus all available folders.
    /// </summary>
    public static (List<(string Title, string Uuid)> Songs, List<string> Folders) GetSongsAndFolders(
        string folderFilter = null, string searchFilter = null)
    {
        var songs = new List<(string Title, string Uuid)>();
        var folders = new HashSet<string>();

        if (!Directory.Exists(Terra_Namp.CachePath))
            return (songs, new List<string>());

        foreach (string file in Directory.GetFiles(Terra_Namp.CachePath, "*.txt"))
        {
            string uuid = Path.GetFileNameWithoutExtension(file);
            string[] lines = File.ReadAllLines(file);
            if (lines.Length == 0) continue;

            string title = lines[0];
            string folder = lines.Length > 3 ? lines[3].Trim() : "";

            if (!string.IsNullOrEmpty(folder))
                folders.Add(folder);

            if (!string.IsNullOrEmpty(folderFilter) && folder != folderFilter)
                continue;

            songs.Add((title, uuid));
        }

        songs.Sort((a, b) => a.Title.CompareTo(b.Title));

        if (!string.IsNullOrEmpty(searchFilter))
            songs = songs.Where(s => s.Title.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        return (songs, folders.OrderBy(f => f).ToList());
    }

    public static SongMetadata GetSongMetadata(string uuid)
    {
        string path = Path.Combine(Terra_Namp.CachePath, $"{uuid}.txt");
        if (!File.Exists(path)) return null;

        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0) return null;

        return new SongMetadata(
            lines[0],
            lines.Length > 1 ? lines[1] : "",
            lines.Length > 2 ? lines[2] : "",
            lines.Length > 3 ? lines[3].Trim() : ""
        );
    }

    public static void DeleteSongFiles(string uuid)
    {
        string songFile = Path.Combine(Terra_Namp.CachePath, $"{uuid}.mp3");
        string titleFile = Path.Combine(Terra_Namp.CachePath, $"{uuid}.txt");

        TryDeleteFile(songFile);
        TryDeleteFile(titleFile);
    }

    private static void TryDeleteFile(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        for (int i = 0; i < 3; i++)
        {
            try
            {
                File.Delete(filePath);
                return;
            }
            catch (IOException)
            {
                if (i < 2)
                {
                    System.Threading.Thread.Sleep(100);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }
    }

    /// <summary>
    /// Cycles through folder filter: "" -> folder1 -> folder2 -> ... -> ""
    /// Returns the new folder filter value.
    /// </summary>
    public static string CycleFolder(string currentFilter, List<string> availableFolders)
    {
        if (availableFolders.Count == 0)
            return "";

        if (string.IsNullOrEmpty(currentFilter))
            return availableFolders[0];

        int idx = availableFolders.IndexOf(currentFilter);
        if (idx < 0 || idx >= availableFolders.Count - 1)
            return "";

        return availableFolders[idx + 1];
    }
}
