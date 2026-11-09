using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NabyScape;

public sealed class DailyResetPlaylistItem
{
    public string Path { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
}

public static class DailyResetPlaylistService
{
    private static readonly object LockObj = new();
    private static List<DailyResetPlaylistItem>? cachedPlaylist;
    private static readonly Dictionary<string, ImageSource> ThumbnailCache = new(StringComparer.OrdinalIgnoreCase);

    public static event Action? PlaylistChanged;

    private static readonly string[] CandidateDirectories = new[]
    {
        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Videos"),
        System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Videos"),
        "Videos",
        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Videos"),
        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "showcase"),
        System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Assets", "showcase"),
        "Assets/showcase",
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
    };

    public static string GetPlaylistFilePath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string desktopiaFile = System.IO.Path.Combine(localAppData, "Desktopia", "daily_reset_playlist.json");
        if (File.Exists(desktopiaFile)) return desktopiaFile;

        string nabyScapeFile = System.IO.Path.Combine(localAppData, "NabyScape", "daily_reset_playlist.json");
        if (File.Exists(nabyScapeFile)) return nabyScapeFile;

        string desktopiaDir = System.IO.Path.Combine(localAppData, "Desktopia");
        if (Directory.Exists(desktopiaDir)) return desktopiaFile;

        string nabyDir = System.IO.Path.Combine(localAppData, "NabyScape");
        if (!Directory.Exists(nabyDir))
        {
            try { Directory.CreateDirectory(nabyDir); } catch {}
        }
        return nabyScapeFile;
    }

    public static List<DailyResetPlaylistItem> GetPlaylist()
    {
        lock (LockObj)
        {
            if (cachedPlaylist != null)
                return new List<DailyResetPlaylistItem>(cachedPlaylist);

            var items = LoadFromFile();
            if (items == null || items.Count == 0)
            {
                items = InitializeDefaultPlaylist();
                SaveToFile(items);
            }

            cachedPlaylist = items;
            return new List<DailyResetPlaylistItem>(cachedPlaylist);
        }
    }

    private static List<DailyResetPlaylistItem>? LoadFromFile()
    {
        try
        {
            string path = GetPlaylistFilePath();
            if (!File.Exists(path)) return null;

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;

            var items = JsonSerializer.Deserialize<List<DailyResetPlaylistItem>>(json);
            if (items == null) return null;

            bool modified = false;
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Path))
                {
                    item.Title = System.IO.Path.GetFileNameWithoutExtension(item.Path);
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(item.Duration) && !string.IsNullOrWhiteSpace(item.Path) && File.Exists(item.Path))
                {
                    item.Duration = FormatDuration(GetVideoDuration(item.Path));
                    modified = true;
                }
            }

            if (modified)
            {
                SaveToFile(items);
            }

            return items;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveToFile(List<DailyResetPlaylistItem> items)
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string primaryPath = GetPlaylistFilePath();
            string? dir = System.IO.Path.GetDirectoryName(primaryPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(primaryPath, json);

            string desktopiaDir = System.IO.Path.Combine(localAppData, "Desktopia");
            if (Directory.Exists(desktopiaDir))
            {
                string desktopiaPath = System.IO.Path.Combine(desktopiaDir, "daily_reset_playlist.json");
                if (!string.Equals(primaryPath, desktopiaPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(desktopiaPath, json);
                }
            }

            string nabyDir = System.IO.Path.Combine(localAppData, "NabyScape");
            if (Directory.Exists(nabyDir))
            {
                string nabyPath = System.IO.Path.Combine(nabyDir, "daily_reset_playlist.json");
                if (!string.Equals(primaryPath, nabyPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(nabyPath, json);
                }
            }
        }
        catch {}
    }

    private static List<DailyResetPlaylistItem> InitializeDefaultPlaylist()
    {
        var items = new List<DailyResetPlaylistItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            string targetDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Videos");
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
            string demoSrc = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Assets", "showcase", "nabyscape-demo.mp4");
            if (!File.Exists(demoSrc))
                demoSrc = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "showcase", "nabyscape-demo.mp4");
            if (File.Exists(demoSrc))
            {
                string dest = System.IO.Path.Combine(targetDir, "reset.mp4");
                if (!File.Exists(dest)) File.Copy(demoSrc, dest, true);
            }
        }
        catch {}

        foreach (var dir in CandidateDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase));

                    foreach (var file in files)
                    {
                        string fullPath = System.IO.Path.GetFullPath(file);
                        if (seen.Add(fullPath))
                        {
                            var duration = GetVideoDuration(fullPath);
                            items.Add(new DailyResetPlaylistItem
                            {
                                Path = fullPath,
                                Title = System.IO.Path.GetFileNameWithoutExtension(fullPath),
                                Duration = FormatDuration(duration)
                            });
                        }
                    }
                }
            }
            catch {}
        }

        return items;
    }

    public static void AddFiles(IEnumerable<string> paths)
    {
        lock (LockObj)
        {
            var current = GetPlaylist();
            var seen = new HashSet<string>(current.Select(i => System.IO.Path.GetFullPath(i.Path)), StringComparer.OrdinalIgnoreCase);
            bool added = false;

            foreach (var path in paths)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
                    string ext = System.IO.Path.GetExtension(path);
                    if (!ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase) &&
                        !ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string fullPath = System.IO.Path.GetFullPath(path);
                    if (seen.Add(fullPath))
                    {
                        var duration = GetVideoDuration(fullPath);
                        current.Add(new DailyResetPlaylistItem
                        {
                            Path = fullPath,
                            Title = System.IO.Path.GetFileNameWithoutExtension(fullPath),
                            Duration = FormatDuration(duration)
                        });
                        added = true;
                    }
                }
                catch {}
            }

            if (added)
            {
                cachedPlaylist = current;
                SaveToFile(current);
                PlaylistChanged?.Invoke();
            }
        }
    }

    public static void AddFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return;

        try
        {
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase));

            AddFiles(files);
        }
        catch {}
    }

    public static void Remove(DailyResetPlaylistItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Path)) return;
        lock (LockObj)
        {
            var current = GetPlaylist();
            int removed = current.RemoveAll(i => string.Equals(System.IO.Path.GetFullPath(i.Path), System.IO.Path.GetFullPath(item.Path), StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                cachedPlaylist = current;
                SaveToFile(current);
                PlaylistChanged?.Invoke();
            }
        }
    }

    public static string? GetRandomVideo()
    {
        var playlist = GetPlaylist();
        var existing = playlist.Where(i => !string.IsNullOrWhiteSpace(i.Path) && File.Exists(i.Path)).ToList();
        if (existing.Count > 0)
        {
            return existing[Random.Shared.Next(existing.Count)].Path;
        }

        var fallbackList = InitializeDefaultPlaylist();
        if (fallbackList.Count > 0)
        {
            lock (LockObj)
            {
                cachedPlaylist = fallbackList;
                SaveToFile(fallbackList);
            }
            return fallbackList[Random.Shared.Next(fallbackList.Count)].Path;
        }

        return null;
    }

    public static TimeSpan GetVideoDuration(string filePath)
    {
        if (!File.Exists(filePath)) return TimeSpan.Zero;

        // 1. Try NAudio MediaFoundationReader
        try
        {
            using var reader = new NAudio.Wave.MediaFoundationReader(filePath);
            if (reader.TotalTime > TimeSpan.Zero)
                return reader.TotalTime;
        }
        catch {}

        // 2. Try Windows Shell COM
        try
        {
            string? dir = System.IO.Path.GetDirectoryName(filePath);
            string? name = System.IO.Path.GetFileName(filePath);
            if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(name))
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    var folder = shell.NameSpace(dir);
                    if (folder != null)
                    {
                        var fileItem = folder.ParseName(name);
                        if (fileItem != null)
                        {
                            string detail = folder.GetDetailsOf(fileItem, 27);
                            if (!string.IsNullOrWhiteSpace(detail) && TimeSpan.TryParse(detail.Trim(), out var ts))
                            {
                                return ts;
                            }
                        }
                    }
                }
            }
        }
        catch {}

        // 3. Try Windows.Storage.StorageFile
        try
        {
            var task = Task.Run(async () =>
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(System.IO.Path.GetFullPath(filePath));
                var props = await file.Properties.GetVideoPropertiesAsync();
                return props.Duration;
            });
            if (task.Wait(1200) && task.Result > TimeSpan.Zero)
                return task.Result;
        }
        catch {}

        return TimeSpan.Zero;
    }

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return "00:00";
        if (duration.TotalHours >= 1)
            return duration.ToString(@"hh\:mm\:ss");
        return duration.ToString(@"mm\:ss");
    }

    public static async Task<ImageSource?> GetVideoThumbnailAsync(string filePath, int pixels = 256)
    {
        if (!File.Exists(filePath)) return null;

        lock (ThumbnailCache)
        {
            if (ThumbnailCache.TryGetValue(filePath, out var cached))
                return cached;
        }

        ImageSource? result = null;

        // 1. Try Windows.Storage.StorageFile
        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(System.IO.Path.GetFullPath(filePath));
            using var thumb = await file.GetThumbnailAsync(
                Windows.Storage.FileProperties.ThumbnailMode.VideosView,
                (uint)pixels,
                Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);

            if (thumb != null && thumb.Type != Windows.Storage.FileProperties.ThumbnailType.Icon)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = thumb.AsStreamForRead();
                bitmap.EndInit();
                bitmap.Freeze();
                result = bitmap;
            }
        }
        catch {}

        // 2. Try AppIconCache (IShellItemImageFactory / SHCreateItemFromParsingName)
        if (result == null)
        {
            try
            {
                result = await AppIconCache.Thumbnail(filePath, pixels);
            }
            catch {}
        }

        if (result != null)
        {
            lock (ThumbnailCache)
            {
                ThumbnailCache[filePath] = result;
            }
        }

        return result;
    }
}
