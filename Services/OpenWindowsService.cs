using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopWallpaper.Services
{
    public class OpenWindowInfo
    {
        public nint Handle { get; set; }
        public string Title { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public bool IsMinimized { get; set; }
    }

    public class OpenWindowsService
    {
        private const int GWL_EXSTYLE = -20;
        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(nint hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(nint hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(nint hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(nint hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(nint hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll")]
        private static extern nint GetForegroundWindow();

        public List<OpenWindowInfo> GetOpenWindows()
        {
            if (!OperatingSystem.IsWindows())
                return new();

            var currentProcessId = Environment.ProcessId;
            var windows = new List<OpenWindowInfo>();

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                int titleLength = GetWindowTextLength(hWnd);
                if (titleLength == 0)
                    return true;

                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                    return true;

                GetWindowThreadProcessId(hWnd, out uint processId);
                if (processId == currentProcessId)
                    return true;

                var titleBuilder = new StringBuilder(titleLength + 1);
                GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                string title = titleBuilder.ToString().Trim();
                if (string.IsNullOrWhiteSpace(title))
                    return true;

                string processName = "";
                try
                {
                    processName = Process.GetProcessById((int)processId).ProcessName;
                }
                catch
                {
                }

                windows.Add(new OpenWindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ProcessName = processName,
                    IsMinimized = IsIconic(hWnd)
                });

                return true;
            }, 0);

            return windows
                .GroupBy(window => window.Handle)
                .Select(group => group.First())
                .OrderBy(window => window.ProcessName)
                .ThenBy(window => window.Title)
                .ToList();
        }

        public void ActivateWindow(OpenWindowInfo window)
        {
            if (!OperatingSystem.IsWindows() || window.Handle == 0)
                return;

            ShowWindow(window.Handle, window.IsMinimized ? SW_RESTORE : SW_SHOW);
            SetForegroundWindow(window.Handle);
        }

        public void ToggleWindow(OpenWindowInfo window)
        {
            if (!OperatingSystem.IsWindows() || window.Handle == 0)
                return;

            if (GetForegroundWindow() == window.Handle)
            {
                ShowWindow(window.Handle, SW_MINIMIZE);
                return;
            }

            ActivateWindow(window);
        }
    }
}
