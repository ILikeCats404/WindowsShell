using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace DesktopWallpaper.Services
{
    public class SearchResult
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string? IconPath { get; set; }
        public bool IsStoreApp { get; set; }
    }

    public class AppSearchService
    {
        private const int SW_SHOWNORMAL = 1;
        private const int ShellExecuteSuccessThreshold = 32;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr ShellExecute(
            IntPtr hwnd,
            string? lpOperation,
            string lpFile,
            string? lpParameters,
            string? lpDirectory,
            int nShowCmd);

        private List<SearchResult> _appCache = new();
        private bool _cacheInitialized = false;

        public async Task<List<SearchResult>> SearchAsync(string query)
        {
            // Initialize cache on first search
            if (!_cacheInitialized)
            {
                await InitializeCacheAsync();
            }

            if (string.IsNullOrWhiteSpace(query))
                return _appCache;

            var lowerQuery = query.ToLower();
            return _appCache
                .Where(a => a.Name.ToLower().Contains(lowerQuery))
                .OrderBy(a => a.Name)
                .ToList();
        }

        public async Task<List<SearchResult>> GetAllAppsAsync()
        {
            if (!_cacheInitialized)
            {
                await InitializeCacheAsync();
            }
            return _appCache;
        }

        private async Task InitializeCacheAsync()
        {
            await Task.Run(() =>
            {
                _appCache.Clear();

                // Search in Start Menu
                SearchStartMenu();

                // Search in Registry (App Paths)
                SearchRegistry();

                // Search Microsoft Store / packaged apps
                SearchStoreApps();

                // Remove duplicates
                _appCache = _appCache
                    .GroupBy(a => a.Name.ToLower())
                    .Select(g => g.First())
                    .OrderBy(a => a.Name)
                    .ToList();
            });

            _cacheInitialized = true;
        }

        private void SearchStartMenu()
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
                SearchStartMenuFolder(folder);
            }
        }

        private void SearchStartMenuFolder(string folder)
        {
            try
            {
                if (!Directory.Exists(folder))
                    return;

                var files = Directory
                    .EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                    .Where(file =>
                        file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".appref-ms", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".url", StringComparison.OrdinalIgnoreCase));

                foreach (var file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    _appCache.Add(new SearchResult
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

        private void SearchRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths");
                if (key == null)
                    return;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var defaultValue = subKey?.GetValue("") as string;

                        if (!string.IsNullOrWhiteSpace(defaultValue) && File.Exists(defaultValue))
                        {
                            string appName = subKeyName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

                            // Skip if already have this app
                            if (_appCache.Any(a => a.Name.Equals(appName, StringComparison.OrdinalIgnoreCase)))
                                continue;

                            _appCache.Add(new SearchResult
                            {
                                Name = appName,
                                Path = defaultValue,
                                IconPath = defaultValue
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void SearchStoreApps()
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
                if (process == null)
                    return;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                if (string.IsNullOrWhiteSpace(output))
                    return;

                var apps = JsonSerializer.Deserialize<List<StartApp>>(output);
                if (apps == null)
                    return;

                foreach (var app in apps)
                {
                    if (string.IsNullOrWhiteSpace(app.Name) || string.IsNullOrWhiteSpace(app.AppID))
                        continue;

                    _appCache.Add(new SearchResult
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

        public void LaunchApp(SearchResult app)
        {
            if (string.IsNullOrWhiteSpace(app.Path))
                return;

            try
            {
                if (app.IsStoreApp)
                {
                    LaunchStoreApp(app.Path);
                    return;
                }

                if (OperatingSystem.IsWindows() && LaunchWithShellExecute(app.Path))
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = app.Path,
                    UseShellExecute = true,
                    WorkingDirectory = GetWorkingDirectory(app.Path)
                });
            }
            catch { }
            finally
            {
                MauiProgram.DisableInteractiveMode();
            }
        }

        private static void LaunchStoreApp(string appId)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"shell:AppsFolder\\{appId}",
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        private static bool LaunchWithShellExecute(string path)
        {
            try
            {
                var result = ShellExecute(
                    IntPtr.Zero,
                    "open",
                    path,
                    null,
                    GetWorkingDirectory(path),
                    SW_SHOWNORMAL);

                return result.ToInt64() > ShellExecuteSuccessThreshold;
            }
            catch
            {
                return false;
            }
        }

        private static string? GetWorkingDirectory(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                return Path.GetDirectoryName(path);
            }
            catch
            {
                return null;
            }
        }

        private sealed class StartApp
        {
            public string Name { get; set; } = "";
            public string AppID { get; set; } = "";
        }
    }
}
