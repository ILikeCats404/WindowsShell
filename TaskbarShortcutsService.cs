using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopWallpaper.Services
{
    public class TaskbarShortcut
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string? IconBase64 { get; set; }
    }

    public class TaskbarShortcutsService
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_SHOW = 5;

        public List<TaskbarShortcut> GetShortcuts()
        {
            if (!Directory.Exists(Folder))
                return new();

            var list = new List<TaskbarShortcut>();

            foreach (var file in Directory.GetFiles(Folder, "*.lnk"))
            {
                list.Add(new TaskbarShortcut
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Path = file,
                    IconBase64 = GetShortcutIconBase64(file)
                });
            }

            return list;
        }

        public async Task LaunchAsync(TaskbarShortcut shortcut)
        {
            try
            {
                // Try to extract the target executable from the shortcut
                string? targetExe = ExtractShortcutExeName(shortcut.Path);

                if (!string.IsNullOrWhiteSpace(targetExe))
                {
                    // Check if the process is already running using tasklist
                    if (IsProcessRunningViaTasklist(targetExe))
                    {
                        // Process is running - try to activate its window
                        try
                        {
                            var existingProcess = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(targetExe));
                            if (existingProcess.Length > 0)
                            {
                                var proc = existingProcess[0];
                                IntPtr hwnd = proc.MainWindowHandle;
                                if (hwnd != IntPtr.Zero)
                                {
                                    ShowWindow(hwnd, SW_SHOW);
                                    SetForegroundWindow(hwnd);
                                    return;
                                }
                            }
                        }
                        catch { }
                    }
                }

                // Process not running or couldn't determine - launch it
                Process.Start(new ProcessStartInfo
                {
                    FileName = shortcut.Path,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Silently fail
            }
            finally
            {
                MauiProgram.DisableInteractiveMode();
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Checks if a process is running by parsing tasklist command output.
        /// </summary>
        private bool IsProcessRunningViaTasklist(string exeName)
        {
            try
            {
                string exeNameOnly = Path.GetFileNameWithoutExtension(exeName).ToLower();

                var psi = new ProcessStartInfo
                {
                    FileName = "tasklist.exe",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return false;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // tasklist output format: "process.exe    1234"
                // Check if our exe is in the list (case-insensitive)
                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains(exeNameOnly, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Extracts the executable name from a .lnk shortcut file by binary parsing.
        /// </summary>
        private string? ExtractShortcutExeName(string lnkPath)
        {
            try
            {
                if (!File.Exists(lnkPath))
                    return null;

                using var fs = new FileStream(lnkPath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);

                // Read header (4 bytes: 4C 00 00 00)
                byte[] header = br.ReadBytes(4);
                if (header[0] != 0x4C)
                    return null;

                // Skip to offset 0x4C where the data begins
                fs.Seek(0x4C, SeekOrigin.Begin);

                // Read remaining data
                byte[] data = new byte[fs.Length - fs.Position];
                br.Read(data, 0, data.Length);

                // Search for .exe in the data (ASCII)
                string content = Encoding.ASCII.GetString(data);

                // Look for common executable patterns
                int exeIndex = content.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (exeIndex > 0)
                {
                    // Find the start of the filename (work backwards from .exe)
                    int start = exeIndex - 1;
                    while (start >= 0 && content[start] != '\\' && content[start] != ':' && content[start] != '\0')
                    {
                        start--;
                    }
                    start++; // Move to the actual start of filename

                    if (start >= 0 && exeIndex + 4 <= content.Length)
                    {
                        string fileName = content.Substring(start, exeIndex - start + 4);
                        // Validate it looks like an executable path
                        if (!fileName.Contains(" ") || Path.IsPathRooted(fileName) || fileName.Contains("\\"))
                        {
                            return fileName.TrimStart('\\');
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        // ---------- ICON EXTRACTION ----------

        private string? GetShortcutIconBase64(string lnkPath)
        {
            try
            {
                // Try to extract icon from the shortcut file directly
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(lnkPath);
                if (icon == null) return null;

                using var bmp = icon.ToBitmap();
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

                return Convert.ToBase64String(ms.ToArray());
            }
            catch
            {
                return null;
            }
        }
    }
}
