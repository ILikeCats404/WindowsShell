using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DesktopWallpaper.Services
{
    //not timber. made with codex
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

        public void WarmCache()
        {
            StartMenuCache.Warm();
        }

        public async Task<List<SearchResult>> SearchAsync(string query)
        {
            return await StartMenuCache.SearchAsync(query);
        }

        public async Task<List<SearchResult>> GetAllAppsAsync()
        {
            return await StartMenuCache.GetAllAppsAsync();
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

                MauiProgram.StartWithFreshEnvironment(
                    app.Path,
                    workingDirectory: GetWorkingDirectory(app.Path));
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
                MauiProgram.StartWithFreshEnvironment("explorer.exe", $"shell:AppsFolder\\{appId}");
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

    }
}
