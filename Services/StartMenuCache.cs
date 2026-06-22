using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace DesktopWallpaper.Services
{
    public static class StartMenuCache
    {
        private static readonly SemaphoreSlim CacheLock = new(1, 1);
        private static readonly object SnapshotLock = new();
        private static List<SearchResult> AppCache = new();
        private static Task? InitializeCacheTask;
        private static bool CacheInitialized;

        public static bool IsReady => CacheInitialized;

        public static void Warm()
        {
            _ = EnsureReadyAsync();
        }

        public static async Task EnsureReadyAsync()
        {
            if (CacheInitialized)
            {
                return;
            }

            await CacheLock.WaitAsync();
            try
            {
                InitializeCacheTask ??= InitializeCacheAsync();
            }
            finally
            {
                CacheLock.Release();
            }

            await InitializeCacheTask;
        }

        public static async Task<List<SearchResult>> GetAllAppsAsync()
        {
            await EnsureReadyAsync();
            return GetAllAppsSnapshot();
        }

        public static List<SearchResult> GetAllAppsSnapshot()
        {
            lock (SnapshotLock)
            {
                return AppCache.ToList();
            }
        }

        public static async Task<List<SearchResult>> SearchAsync(string query)
        {
            await EnsureReadyAsync();
            return SearchSnapshot(query);
        }

        public static List<SearchResult> SearchSnapshot(string query)
        {
            var apps = GetAllAppsSnapshot();
            if (string.IsNullOrWhiteSpace(query))
            {
                return apps;
            }

            var lowerQuery = query.ToLowerInvariant();
            return apps
                .Where(a => a.Name.ToLowerInvariant().Contains(lowerQuery))
                .OrderBy(a => a.Name)
                .ToList();
        }

        private static async Task InitializeCacheAsync()
        {
            var appCache = new List<SearchResult>();

            await Task.Run(() =>
            {
                SearchStartMenu(appCache);
                SearchRegistry(appCache);
                SearchStoreApps(appCache);

                var dedupedApps = appCache
                    .GroupBy(a => a.Name.ToLowerInvariant())
                    .Select(g => g.First())
                    .OrderBy(a => a.Name)
                    .ToList();

                lock (SnapshotLock)
                {
                    AppCache = dedupedApps;
                }
            });

            CacheInitialized = true;
        }

        private static void SearchStartMenu(List<SearchResult> appCache)
        {
            var folders = new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                    "Programs")
            };

            foreach (var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                SearchStartMenuFolder(folder, appCache);
            }
        }

        private static void SearchStartMenuFolder(string folder, List<SearchResult> appCache)
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    return;
                }

                var files = Directory
                    .EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                    .Where(file =>
                        file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".appref-ms", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".url", StringComparison.OrdinalIgnoreCase));

                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    appCache.Add(new SearchResult
                    {
                        Name = name,
                        Path = file,
                        IconPath = file
                    });
                }
            }
            catch
            {
            }
        }

        private static void SearchRegistry(List<SearchResult> appCache)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths");
                if (key is null)
                {
                    return;
                }

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var defaultValue = subKey?.GetValue("") as string;

                        if (string.IsNullOrWhiteSpace(defaultValue) || !File.Exists(defaultValue))
                        {
                            continue;
                        }

                        var appName = subKeyName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                        if (appCache.Any(a => a.Name.Equals(appName, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        appCache.Add(new SearchResult
                        {
                            Name = appName,
                            Path = defaultValue,
                            IconPath = defaultValue
                        });
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static void SearchStoreApps(List<SearchResult> appCache)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-StartApps | Select-Object Name,AppID | ConvertTo-Json -Compress\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process is null)
                {
                    return;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                if (string.IsNullOrWhiteSpace(output))
                {
                    return;
                }

                var apps = JsonSerializer.Deserialize<List<StartApp>>(output);
                if (apps is null)
                {
                    return;
                }

                foreach (var app in apps)
                {
                    if (string.IsNullOrWhiteSpace(app.Name) || string.IsNullOrWhiteSpace(app.AppID))
                    {
                        continue;
                    }

                    appCache.Add(new SearchResult
                    {
                        Name = app.Name,
                        Path = app.AppID,
                        IconPath = app.AppID,
                        IsStoreApp = true
                    });
                }
            }
            catch
            {
            }
        }

        private sealed class StartApp
        {
            public string Name { get; set; } = "";
            public string AppID { get; set; } = "";
        }
    }
}
