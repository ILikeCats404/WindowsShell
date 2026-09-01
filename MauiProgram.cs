using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.LifecycleEvents;

#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Media.Control;
using WinRT.Interop;
using WinUIWindow = Microsoft.UI.Xaml.Window;
#endif

namespace DesktopWallpaper
{
    //not timber. made with codex
    public static class MauiProgram
    {
#if WINDOWS
        /*
         * Assistant-authored Windows shell support.
         *
         * Notes for future maintainers:
         * - Timber's original app idea and UI direction are still the heart of this thing.
         * - The native Windows glue below -- click-through mode, hotkeys, media keys,
         *   volume keys, Win-key behavior, screen snip handling, and custom Alt-Tab
         *   events -- was substantially written and organized by Codex.
         * - So if this section looks suspiciously like someone spent too much time
         *   arguing with Win32 interop on Timber's behalf: yes. That was me (codex). Credit taken. :D
         *   
         *   
         *   Timber note here, yeah i have no clue how the heck this works lol
         */
        
        // Win32 window/style constants
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int GWLP_WNDPROC = -4;

        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_BORDER = 0x00800000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private const int SW_SHOW = 5;
        private const int SW_SHOWNOACTIVATE = 4;

        // Window messages and keyboard hook constants
        private const int WM_HOTKEY = 0x0312;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        // Registered hotkey IDs
        private const int TOGGLE_CLICK_THROUGH_HOTKEY_ID = 9000;
        private const int MEDIA_PLAY_PAUSE_HOTKEY_ID = 9001;
        private const int MEDIA_NEXT_TRACK_HOTKEY_ID = 9002;
        private const int MEDIA_PREV_TRACK_HOTKEY_ID = 9003;
        private const int MEDIA_STOP_HOTKEY_ID = 9004;
        private const int VOLUME_MUTE_HOTKEY_ID = 9005;
        private const int VOLUME_DOWN_HOTKEY_ID = 9006;
        private const int VOLUME_UP_HOTKEY_ID = 9007;

        // Virtual keys
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        private const uint VK_CAPITAL = 0x14;
        private const int VK_TAB = 0x09;
        private const int VK_SHIFT = 0x10;
        private const int VK_S = 0x53;
        private const int VK_V = 0x56;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_MENU = 0x12;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;
        private const int VK_VOLUME_MUTE = 0xAD;
        private const int VK_VOLUME_DOWN = 0xAE;
        private const int VK_VOLUME_UP = 0xAF;
        private const int VK_MEDIA_NEXT_TRACK = 0xB0;
        private const int VK_MEDIA_PREV_TRACK = 0xB1;
        private const int VK_MEDIA_STOP = 0xB2;
        private const int VK_MEDIA_PLAY_PAUSE = 0xB3;

        // Appbar/taskbar reservation constants.
        private const uint ABM_NEW = 0x00000000;
        private const uint ABM_QUERYPOS = 0x00000002;
        private const uint ABM_SETPOS = 0x00000003;
        private const uint ABE_BOTTOM = 3;

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SM_CMONITORS = 80;

        private const int TASKBAR_HEIGHT = 54;
        private const uint MONITORINFOF_PRIMARY = 0x1;

        private static readonly IntPtr HWND_TOPMOST = new(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new(-2);
        private static readonly IntPtr HWND_TOP = new(0);

        private static IntPtr _hwnd;
        private static IntPtr _startMenuHwnd;
        private static IntPtr _oldWndProc;
        private static IntPtr _keyboardHook;
        private static OverlappedPresenter? _presenter;
        private static Window? _startMenuWindow;
        private static Window? _altTabWindow;
        private static readonly List<Window> _secondaryTaskbarWindows = new();
        private static readonly List<IntPtr> _secondaryTaskbarHwnds = new();
        private static Services.AltTabStateService? _altTabStateService;
        private static System.Threading.Timer? _environmentRefreshTimer;
        private static readonly object _environmentLock = new();
        private static Dictionary<string, string> _freshEnvironment = SnapshotCurrentProcessEnvironment();

        private static readonly WndProc _wndProc = WndProcHandler;
        private static readonly LowLevelKeyboardProc _keyboardProc = KeyboardHookHandler;

        private static bool _clickThrough = true;
        private static bool _winKeyDown;
        private static bool _shiftKeyDown;
        private static bool _altKeyDown;
        private static bool _startKeyAltChord;
        private static bool _screenSnipHandled;
        private static readonly HashSet<IntPtr> _reservedTaskbarHwnds = new();

        public static event Action? StartKeyPressed;
        public static event Action? ClipboardHistoryPressed;
        public static event Action<string>? ScreenshotCaptured;
        public static event Action<int>? VolumeChanged;

        private delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("shell32.dll")]
        private static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll")]
        private static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        private enum EDataFlow
        {
            eRender,
            eCapture,
            eAll,
        }

        private enum ERole
        {
            eConsole,
            eMultimedia,
            eCommunications,
        }

        private enum CLSCTX : uint
        {
            INPROC_SERVER = 0x1,
            INPROC_HANDLER = 0x2,
            LOCAL_SERVER = 0x4,
            REMOTE_SERVER = 0x10,
            ALL = INPROC_SERVER | INPROC_HANDLER | LOCAL_SERVER | REMOTE_SERVER,
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator
        {
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, CLSCTX dwClsCtx, IntPtr pActivationParams, out IAudioEndpointVolume ppInterface);
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            int GetChannelCount(out uint pnChannelCount);
            int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
            int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
            int GetMasterVolumeLevel(out float pfLevelDB);
            int GetMasterVolumeLevelScalar(out float pfLevel);
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            int SetMute(bool bMute, Guid pguidEventContext);
            int GetMute(out bool pbMute);
            int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
            int VolumeStepUp(Guid pguidEventContext);
            int VolumeStepDown(Guid pguidEventContext);
            int QueryHardwareSupport(out uint pdwHardwareSupportMask);
            int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
        }
#endif

        public static MauiApp CreateMauiApp()
        {
#if WINDOWS
            KillExplorerShell();
            RefreshEnvironmentFromRegistry();
            _environmentRefreshTimer ??= new System.Threading.Timer(
                _ => RefreshEnvironmentFromRegistry(),
                null,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10));
#endif

            Config.Load();

            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            builder.Services.AddScoped(_ => new HttpClient());
            builder.Services.AddSingleton<DesktopWallpaper.Services.TaskbarShortcutsService>();
            builder.Services.AddSingleton<DesktopWallpaper.Services.AppSearchService>();
            builder.Services.AddSingleton<DesktopWallpaper.Services.OpenWindowsService>();
            builder.Services.AddSingleton<DesktopWallpaper.Services.ClipboardHistoryService>();
            builder.Services.AddSingleton<DesktopWallpaper.Services.AltTabStateService>();

            builder.ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(windows =>
                {
                    windows.OnWindowCreated(ConfigureWindowsShellWindow);
                });
#endif
            });

            var app = builder.Build();
            app.Services.GetRequiredService<DesktopWallpaper.Services.AppSearchService>().WarmCache();
            _altTabStateService = app.Services.GetRequiredService<DesktopWallpaper.Services.AltTabStateService>();

            return app;
        }

        public static void DisableInteractiveMode()
        {
#if WINDOWS
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            _clickThrough = true;
            DevStuff.IsClickThrough = true;
            ApplyClickThrough(_hwnd, true);
            SetWindowPos(_hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
#endif
        }

        public static void OpenStartMenuWindow()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_startMenuWindow is not null)
                {
                    CloseStartMenuWindow();
                    return;
                }

                _startMenuWindow = new Window(new StartMenuPage())
                {
                    Title = "DesktopWallpaper Start",
                    Width = 460,
                    Height = 560,
                };

                _startMenuWindow.Destroying += (_, _) =>
                {
                    _startMenuWindow = null;
                    _startMenuHwnd = IntPtr.Zero;
                };

                Application.Current?.OpenWindow(_startMenuWindow);
            });
        }

        public static void CloseStartMenuWindow()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_startMenuWindow is null)
                {
                    return;
                }

                Application.Current?.CloseWindow(_startMenuWindow);
                _startMenuWindow = null;
                _startMenuHwnd = IntPtr.Zero;
            });
        }

        public static void OpenOrCycleAltTabWindow()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_altTabStateService is null)
                {
                    return;
                }

                if (_altTabWindow is not null)
                {
                    _altTabStateService.Cycle();
                    return;
                }

                _altTabStateService.Start();
                if (_altTabStateService.Windows.Count == 0)
                {
                    return;
                }

                _altTabWindow = new Window(new AltTabPage())
                {
                    Title = "DesktopWallpaper AltTab",
                    Width = 820,
                    Height = 620,
                };

                _altTabWindow.Destroying += (_, _) =>
                {
                    _altTabWindow = null;
                };

                Application.Current?.OpenWindow(_altTabWindow);
            });
        }

        public static void CloseAltTabWindow(bool activateSelected)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (activateSelected)
                {
                    _altTabStateService?.ActivateSelected();
                }

                if (_altTabWindow is null)
                {
                    return;
                }

                Application.Current?.CloseWindow(_altTabWindow);
                _altTabWindow = null;
            });
        }

        public static void ToggleClickableMode()
        {
#if WINDOWS
            ToggleClickThrough();
#else
            DevStuff.IsClickThrough = !DevStuff.IsClickThrough;
#endif
        }

        public static void FocusInteractiveMode()
        {
#if WINDOWS
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            _clickThrough = false;
            DevStuff.IsClickThrough = false;
            ApplyClickThrough(_hwnd, false);

            ShowWindow(_hwnd, SW_SHOW);
            _presenter?.Maximize();
            FocusAndBringToFront(_hwnd);
#endif
        }

        public static void FocusAndBringToFront(IntPtr hwnd)
        {
#if WINDOWS
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            ShowWindow(hwnd, IsIconic(hwnd) ? 9 : SW_SHOW);
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            BringWindowToTop(hwnd);
            SetActiveWindow(hwnd);
            SetForegroundWindow(hwnd);
            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
#endif
        }

        public static Process? StartWithFreshEnvironment(string fileName, string? arguments = null, string? workingDirectory = null)
        {
#if WINDOWS
            RefreshEnvironmentFromRegistry();

            var environment = GetFreshEnvironmentSnapshot();
            if (ShouldLaunchThroughExplorer(fileName))
            {
                arguments = string.IsNullOrWhiteSpace(arguments)
                    ? QuoteArgument(fileName)
                    : $"{QuoteArgument(fileName)} {arguments}";
                fileName = "explorer.exe";
                workingDirectory = null;
            }

            var resolvedFileName = ResolveExecutablePath(fileName, environment);

            var psi = new ProcessStartInfo
            {
                FileName = resolvedFileName,
                Arguments = arguments ?? "",
                UseShellExecute = false,
                WorkingDirectory = workingDirectory ?? GetWorkingDirectory(resolvedFileName),
            };

            ApplyEnvironment(psi, environment);

            return Process.Start(psi);
#else
            return Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? "",
                UseShellExecute = true,
                WorkingDirectory = workingDirectory ?? "",
            });
#endif
        }

        public static void ApplyFreshEnvironment(ProcessStartInfo psi)
        {
#if WINDOWS
            RefreshEnvironmentFromRegistry();
            ApplyEnvironment(psi, GetFreshEnvironmentSnapshot());
#endif
        }

#if WINDOWS
        public static void RefreshEnvironmentFromRegistry()
        {
            // Each step is independently fault-tolerant: a single registry read failing
            // (e.g. access denied under a locked-down shell token) must never wipe out
            // the snapshot, since ApplyEnvironment() replaces a child process's entire
            // environment with whatever is in here.
            var environment = SnapshotCurrentProcessEnvironment();

            TryMergeRegistryEnvironment(environment, () => Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"));
            TryMergeRegistryEnvironment(environment, () => Registry.CurrentUser.OpenSubKey("Environment"));

            try
            {
                ExpandEnvironmentValues(environment);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            if (environment.Count == 0)
            {
                return;
            }

            lock (_environmentLock)
            {
                _freshEnvironment = environment;
            }
        }

        private static Dictionary<string, string> SnapshotCurrentProcessEnvironment()
        {
            return Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .ToDictionary(
                    entry => entry.Key.ToString() ?? "",
                    entry => entry.Value?.ToString() ?? "",
                    StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> GetFreshEnvironmentSnapshot()
        {
            lock (_environmentLock)
            {
                return new Dictionary<string, string>(_freshEnvironment, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void ApplyEnvironment(ProcessStartInfo psi, Dictionary<string, string> environment)
        {
            if (environment.Count == 0)
            {
                // Leave psi.Environment untouched (it already inherits the current
                // process's environment) rather than clearing it down to nothing.
                return;
            }

            psi.Environment.Clear();
            foreach (var item in environment)
            {
                psi.Environment[item.Key] = item.Value;
            }
        }

        private static void TryMergeRegistryEnvironment(Dictionary<string, string> environment, Func<RegistryKey?> openKey)
        {
            try
            {
                using var key = openKey();
                if (key is null)
                {
                    return;
                }

                foreach (var name in key.GetValueNames())
                {
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    var value = key.GetValue(name, "", RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? "";
                    environment[name] = value;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static void ExpandEnvironmentValues(Dictionary<string, string> environment)
        {
            foreach (var key in environment.Keys.ToList())
            {
                environment[key] = ExpandEnvironmentValue(environment[key], environment);
            }
        }

        private static string ExpandEnvironmentValue(string value, Dictionary<string, string> environment)
        {
            foreach (var item in environment)
            {
                value = value.Replace($"%{item.Key}%", item.Value, StringComparison.OrdinalIgnoreCase);
            }

            return Environment.ExpandEnvironmentVariables(value);
        }

        private static string ResolveExecutablePath(string fileName, Dictionary<string, string> environment)
        {
            if (Path.IsPathRooted(fileName) || fileName.Contains('\\') || fileName.Contains('/'))
            {
                return fileName;
            }

            var pathExt = environment.TryGetValue("PATHEXT", out var extValue)
                ? extValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [".COM", ".EXE", ".BAT", ".CMD"];

            var candidates = Path.HasExtension(fileName)
                ? [fileName]
                : pathExt.Select(extension => fileName + extension);

            if (!environment.TryGetValue("PATH", out var pathValue))
            {
                return fileName;
            }

            foreach (var folder in pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                foreach (var candidate in candidates)
                {
                    var fullPath = Path.Combine(folder.Trim('"'), candidate);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            return fileName;
        }

        private static bool ShouldLaunchThroughExplorer(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".appref-ms", StringComparison.OrdinalIgnoreCase);
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string GetWorkingDirectory(string fileName)
        {
            try
            {
                if (File.Exists(fileName))
                {
                    return Path.GetDirectoryName(fileName) ?? "";
                }
            }
            catch
            {
            }

            return "";
        }

        private static void KillExplorerShell()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/f /im explorer.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static void ConfigureWindowsShellWindow(WinUIWindow window)
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            if (_hwnd == IntPtr.Zero)
            {
                ConfigureMainShellWindow(window, hwnd);
                return;
            }

            if (window.Title?.Contains("AltTab", StringComparison.OrdinalIgnoreCase) == true)
            {
                ConfigureAltTabWindow(window, hwnd);
                return;
            }

            if (window.Title?.Contains("Taskbar", StringComparison.OrdinalIgnoreCase) == true)
            {
                ConfigureTaskbarWindow(window, hwnd);
                return;
            }

            ConfigureStartMenuWindow(window, hwnd);
        }

        private static void ConfigureMainShellWindow(WinUIWindow window, IntPtr hwnd)
        {
            _hwnd = hwnd;

            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

            _presenter = appWindow.Presenter as OverlappedPresenter;
            if (_presenter is null)
            {
                return;
            }

            _presenter.IsMaximizable = false;
            _presenter.IsMinimizable = false;
            _presenter.IsResizable = false;
            _presenter.SetBorderAndTitleBar(false, false);
            _presenter.PreferredMinimumHeight = 1080;
            _presenter.PreferredMinimumWidth = 1920;
            _presenter.Maximize();

            ApplyClickThrough(_hwnd, false);
            RegisterShellHotkeys();
            InstallKeyboardHook();
            InstallWindowProcHook();

            ReserveBottomTaskbarSpace(_hwnd, GetPrimaryMonitorRect(), false);
            OpenSecondaryTaskbarWindows();
            BringVisibleAppWindowsInFrontOfShell();
        }

        private static void BringVisibleAppWindowsInFrontOfShell()
        {
            var currentProcessId = (uint)Environment.ProcessId;
            var windows = new List<IntPtr>();

            EnumWindows((hwnd, _) =>
            {
                if (hwnd == IntPtr.Zero
                    || hwnd == _hwnd
                    || hwnd == _startMenuHwnd
                    || _secondaryTaskbarHwnds.Contains(hwnd)
                    || !IsWindowVisible(hwnd)
                    || IsIconic(hwnd))
                {
                    return true;
                }

                GetWindowThreadProcessId(hwnd, out var processId);
                if (processId == currentProcessId)
                {
                    return true;
                }

                var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                {
                    return true;
                }

                windows.Add(hwnd);
                return true;
            }, IntPtr.Zero);

            for (var i = windows.Count - 1; i >= 0; i--)
            {
                SetWindowPos(
                    windows[i],
                    HWND_TOP,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        private static readonly Dictionary<string, RECT> _pendingTaskbarRects = new();

        private static List<RECT> GetSecondaryMonitorRects()
        {
            var rects = new List<RECT>();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMonitor, ref info) && (info.dwFlags & MONITORINFOF_PRIMARY) == 0)
                {
                    rects.Add(info.rcMonitor);
                }

                return true;
            }, IntPtr.Zero);

            return rects;
        }

        private static RECT GetPrimaryMonitorRect()
        {
            var primaryRect = new RECT
            {
                left = 0,
                top = 0,
                right = GetSystemMetrics(SM_CXSCREEN),
                bottom = GetSystemMetrics(SM_CYSCREEN),
            };

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMonitor, ref info) && (info.dwFlags & MONITORINFOF_PRIMARY) != 0)
                {
                    primaryRect = info.rcMonitor;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return primaryRect;
        }

        // Opens one extra borderless window per non-primary monitor, each hosting
        // its own copy of the taskbar pinned to that monitor's bottom edge -- a
        // duplicate of the main taskbar rather than stretching the original across
        // monitor boundaries.
        private static void OpenSecondaryTaskbarWindows()
        {
            if (GetSystemMetrics(SM_CMONITORS) <= 1)
            {
                return;
            }

            var rects = GetSecondaryMonitorRects();
            if (rects.Count == 0)
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                for (var i = 0; i < rects.Count; i++)
                {
                    var title = $"DesktopWallpaper Taskbar {i}";
                    _pendingTaskbarRects[title] = rects[i];

                    var window = new Window(new TaskbarPage())
                    {
                        Title = title,
                    };

                    _secondaryTaskbarWindows.Add(window);
                    Application.Current?.OpenWindow(window);
                }
            });
        }

        private static void ConfigureTaskbarWindow(WinUIWindow window, IntPtr hwnd)
        {
            _secondaryTaskbarHwnds.Add(hwnd);

            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }

            var title = window.Title ?? "";
            if (!_pendingTaskbarRects.TryGetValue(title, out var monitorRect))
            {
                return;
            }

            _pendingTaskbarRects.Remove(title);

            var x = monitorRect.left;
            var width = monitorRect.right - monitorRect.left;
            var y = monitorRect.bottom - TASKBAR_HEIGHT;

            appWindow.Resize(new Windows.Graphics.SizeInt32(width, TASKBAR_HEIGHT));
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));

            // Never let this window take keyboard focus/activation -- it only exists
            // to mirror the taskbar onto a second monitor and must not interfere with
            // the global low-level keyboard hook or steal focus from whatever the
            // user is typing into.
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            var style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~WS_CAPTION;
            style &= ~WS_THICKFRAME;
            style &= ~WS_BORDER;
            SetWindowLong(hwnd, GWL_STYLE, style);

            ReserveBottomTaskbarSpace(hwnd, monitorRect, true);
            SetWindowPos(hwnd, HWND_TOPMOST, x, y, width, TASKBAR_HEIGHT, SWP_SHOWWINDOW | SWP_NOACTIVATE);
            ShowWindow(hwnd, SW_SHOWNOACTIVATE);
        }

        private static void ConfigureStartMenuWindow(WinUIWindow window, IntPtr hwnd)
        {
            _startMenuHwnd = hwnd;

            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }

            var width = 460;
            var height = 560;
            var x = 18;
            var y = Math.Max(18, GetSystemMetrics(SM_CYSCREEN) - height - 72);
            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));

            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_LAYERED;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            SetWindowPos(hwnd, HWND_TOPMOST, x, y, width, height, SWP_SHOWWINDOW);
            FocusAndBringToFront(hwnd);
        }

        private static void ConfigureAltTabWindow(WinUIWindow window, IntPtr hwnd)
        {
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }

            var width = Math.Min(820, GetSystemMetrics(SM_CXSCREEN) - 80);
            var height = Math.Min(620, GetSystemMetrics(SM_CYSCREEN) - 80);
            var x = Math.Max(20, (GetSystemMetrics(SM_CXSCREEN) - width) / 2);
            var y = Math.Max(20, (GetSystemMetrics(SM_CYSCREEN) - height) / 2);
            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));

            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_LAYERED;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            SetWindowPos(hwnd, HWND_TOPMOST, x, y, width, height, SWP_SHOWWINDOW);
            FocusAndBringToFront(hwnd);
        }

        private static void RegisterShellHotkeys()
        {
            RegisterHotKey(_hwnd, TOGGLE_CLICK_THROUGH_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_CAPITAL);
            RegisterHotKey(_hwnd, MEDIA_PLAY_PAUSE_HOTKEY_ID, 0, VK_MEDIA_PLAY_PAUSE);
            RegisterHotKey(_hwnd, MEDIA_NEXT_TRACK_HOTKEY_ID, 0, VK_MEDIA_NEXT_TRACK);
            RegisterHotKey(_hwnd, MEDIA_PREV_TRACK_HOTKEY_ID, 0, VK_MEDIA_PREV_TRACK);
            RegisterHotKey(_hwnd, MEDIA_STOP_HOTKEY_ID, 0, VK_MEDIA_STOP);
            RegisterHotKey(_hwnd, VOLUME_MUTE_HOTKEY_ID, 0, VK_VOLUME_MUTE);
            RegisterHotKey(_hwnd, VOLUME_DOWN_HOTKEY_ID, 0, VK_VOLUME_DOWN);
            RegisterHotKey(_hwnd, VOLUME_UP_HOTKEY_ID, 0, VK_VOLUME_UP);
        }

        private static void InstallKeyboardHook()
        {
            if (_keyboardHook != IntPtr.Zero)
            {
                return;
            }

            using var currentProcess = Process.GetCurrentProcess();
            using var currentModule = currentProcess.MainModule;
            var moduleHandle = GetModuleHandle(currentModule?.ModuleName);
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
        }

        private static void InstallWindowProcHook()
        {
            if (_oldWndProc != IntPtr.Zero)
            {
                return;
            }

            _oldWndProc = SetWindowLongPtr(
                _hwnd,
                GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(_wndProc));
        }

        private static void ApplyClickThrough(IntPtr hwnd, bool enable)
        {
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (enable)
            {
                exStyle |= WS_EX_TRANSPARENT;
                exStyle |= WS_EX_NOACTIVATE;
                exStyle |= WS_EX_LAYERED;
                exStyle |= WS_EX_TOOLWINDOW;
            }
            else
            {
                exStyle &= ~WS_EX_TRANSPARENT;
                exStyle &= ~WS_EX_NOACTIVATE;
                exStyle &= ~WS_EX_TOOLWINDOW;
            }

            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }

        private static void ToggleClickThrough()
        {
            _clickThrough = !_clickThrough;
            DevStuff.IsClickThrough = _clickThrough;
            ApplyClickThrough(_hwnd, _clickThrough);
        }

        public static void OpenInteractiveMode()
        {
            _clickThrough = false;
            DevStuff.IsClickThrough = false;
            ApplyClickThrough(_hwnd, false);

            ShowWindow(_hwnd, SW_SHOW);
            _presenter?.Maximize();
            FocusAndBringToFront(_hwnd);
        }

        private static void ReleaseForegroundAppFocus()
        {
            var taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarHwnd == IntPtr.Zero)
            {
                return;
            }

            SetForegroundWindow(taskbarHwnd);
        }

        private static IntPtr KeyboardHookHandler(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
            {
                return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }

            var message = wParam.ToInt32();
            var vkCode = Marshal.ReadInt32(lParam);

            if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
            {
                return HandleKeyDown(nCode, wParam, lParam, vkCode);
            }

            if (message == WM_KEYUP || message == WM_SYSKEYUP)
            {
                return HandleKeyUp(nCode, wParam, lParam, vkCode);
            }

            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private static IntPtr HandleKeyDown(int nCode, IntPtr wParam, IntPtr lParam, int vkCode)
        {
            if (IsShiftKey(vkCode))
            {
                _shiftKeyDown = true;
                return _winKeyDown ? 1 : CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }

            if (IsWinKey(vkCode))
            {
                ReleaseForegroundAppFocus();
                _winKeyDown = true;
                _startKeyAltChord = _altKeyDown;
                _screenSnipHandled = false;
                return 1;
            }

            if (IsAltKey(vkCode))
            {
                _altKeyDown = true;
                if (_winKeyDown)
                {
                    _startKeyAltChord = true;
                }

                return 1;
            }

            if (_winKeyDown && _shiftKeyDown && vkCode == VK_S)
            {
                _screenSnipHandled = true;
                DisableInteractiveMode();
                CaptureFullScreenScreenshot();
                return 1;
            }

            if (_winKeyDown && vkCode == VK_V)
            {
                ReleaseForegroundAppFocus();
                _screenSnipHandled = true;
                OpenInteractiveMode();
                ClipboardHistoryPressed?.Invoke();
                return 1;
            }

            if (_altKeyDown && vkCode == VK_TAB)
            {
                ReleaseForegroundAppFocus();
                OpenOrCycleAltTabWindow();
                return 1;
            }

            if (IsPlaybackMediaKey(vkCode))
            {
                _ = HandlePlaybackMediaKeyAsync(vkCode);
                return 1;
            }

            if (IsVolumeKey(vkCode))
            {
                HandleVolumeKey(vkCode);
                return 1;
            }

            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private static IntPtr HandleKeyUp(int nCode, IntPtr wParam, IntPtr lParam, int vkCode)
        {
            if (IsShiftKey(vkCode))
            {
                _shiftKeyDown = false;
                return _winKeyDown ? 1 : CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
            }

            if (IsWinKey(vkCode))
            {
                if (!_screenSnipHandled)
                {
                    if (_startKeyAltChord)
                    {
                        OpenInteractiveMode();
                        StartKeyPressed?.Invoke();
                    }
                    else
                    {
                        OpenStartMenuWindow();
                    }
                }

                _winKeyDown = false;
                _startKeyAltChord = false;
                _screenSnipHandled = false;
                return 1;
            }

            if (IsAltKey(vkCode))
            {
                _altKeyDown = false;
                CloseAltTabWindow(true);
                return 1;
            }

            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private static bool IsWinKey(int vkCode)
        {
            return vkCode is VK_LWIN or VK_RWIN;
        }

        private static bool IsShiftKey(int vkCode)
        {
            return vkCode is VK_SHIFT or VK_LSHIFT or VK_RSHIFT;
        }

        private static bool IsAltKey(int vkCode)
        {
            return vkCode is VK_MENU or VK_LMENU or VK_RMENU;
        }

        private static bool IsPlaybackMediaKey(int vkCode)
        {
            return vkCode is VK_MEDIA_NEXT_TRACK
                or VK_MEDIA_PREV_TRACK
                or VK_MEDIA_STOP
                or VK_MEDIA_PLAY_PAUSE;
        }

        private static bool IsVolumeKey(int vkCode)
        {
            return vkCode is VK_VOLUME_MUTE
                or VK_VOLUME_DOWN
                or VK_VOLUME_UP;
        }

        private static async Task HandlePlaybackMediaKeyAsync(int vkCode)
        {
            try
            {
                var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                var session = manager.GetCurrentSession();

                if (session is null)
                {
                    return;
                }

                _ = vkCode switch
                {
                    VK_MEDIA_NEXT_TRACK => await session.TrySkipNextAsync(),
                    VK_MEDIA_PREV_TRACK => await session.TrySkipPreviousAsync(),
                    VK_MEDIA_STOP => await session.TryStopAsync(),
                    VK_MEDIA_PLAY_PAUSE => await session.TryTogglePlayPauseAsync(),
                    _ => false,
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static void HandleVolumeKey(int vkCode)
        {
            IAudioEndpointVolume? endpointVolume = null;
            IMMDevice? device = null;

            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out device);

                var endpointVolumeId = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref endpointVolumeId, CLSCTX.ALL, IntPtr.Zero, out endpointVolume);

                var eventContext = Guid.Empty;

                switch (vkCode)
                {
                    case VK_VOLUME_MUTE:
                        endpointVolume.GetMute(out var muted);
                        endpointVolume.SetMute(!muted, eventContext);
                        break;
                    case VK_VOLUME_DOWN:
                        endpointVolume.VolumeStepDown(eventContext);
                        break;
                    case VK_VOLUME_UP:
                        endpointVolume.VolumeStepUp(eventContext);
                        break;
                }

                endpointVolume.GetMasterVolumeLevelScalar(out var volumeLevel);
                var volumePercent = Math.Clamp((int)Math.Round(volumeLevel * 100), 0, 100);
                VolumeChanged?.Invoke(volumePercent);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                if (endpointVolume is not null)
                {
                    Marshal.ReleaseComObject(endpointVolume);
                }

                if (device is not null)
                {
                    Marshal.ReleaseComObject(device);
                }
            }
        }

        private static void CaptureFullScreenScreenshot()
        {
            try
            {
                var width = GetSystemMetrics(SM_CXSCREEN);
                var height = GetSystemMetrics(SM_CYSCREEN);
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "DesktopWallpaper Screenshots");

                Directory.CreateDirectory(folder);

                var path = Path.Combine(folder, $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png");

                using var bitmap = new System.Drawing.Bitmap(width, height);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);

                ScreenshotCaptured?.Invoke(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static IntPtr WndProcHandler(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY)
            {
                HandleRegisteredHotkey(wParam.ToInt32());
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        private static void HandleRegisteredHotkey(int hotkeyId)
        {
            switch (hotkeyId)
            {
                case TOGGLE_CLICK_THROUGH_HOTKEY_ID:
                    ToggleClickThrough();
                    break;
                case MEDIA_PLAY_PAUSE_HOTKEY_ID:
                    _ = HandlePlaybackMediaKeyAsync(VK_MEDIA_PLAY_PAUSE);
                    break;
                case MEDIA_NEXT_TRACK_HOTKEY_ID:
                    _ = HandlePlaybackMediaKeyAsync(VK_MEDIA_NEXT_TRACK);
                    break;
                case MEDIA_PREV_TRACK_HOTKEY_ID:
                    _ = HandlePlaybackMediaKeyAsync(VK_MEDIA_PREV_TRACK);
                    break;
                case MEDIA_STOP_HOTKEY_ID:
                    _ = HandlePlaybackMediaKeyAsync(VK_MEDIA_STOP);
                    break;
                case VOLUME_MUTE_HOTKEY_ID:
                    HandleVolumeKey(VK_VOLUME_MUTE);
                    break;
                case VOLUME_DOWN_HOTKEY_ID:
                    HandleVolumeKey(VK_VOLUME_DOWN);
                    break;
                case VOLUME_UP_HOTKEY_ID:
                    HandleVolumeKey(VK_VOLUME_UP);
                    break;
            }
        }

        private static void ReserveBottomTaskbarSpace(IntPtr hwnd, RECT monitorRect, bool moveTaskbarWindow)
        {
            if (hwnd == IntPtr.Zero || _reservedTaskbarHwnds.Contains(hwnd))
            {
                return;
            }

            var taskbarRect = new RECT
            {
                left = monitorRect.left,
                right = monitorRect.right,
                top = monitorRect.bottom - TASKBAR_HEIGHT,
                bottom = monitorRect.bottom,
            };

            APPBARDATA data = new()
            {
                cbSize = Marshal.SizeOf<APPBARDATA>(),
                hWnd = hwnd,
                uEdge = ABE_BOTTOM,
                rc = taskbarRect,
            };

            SHAppBarMessage(ABM_NEW, ref data);
            SHAppBarMessage(ABM_QUERYPOS, ref data);

            data.rc.left = taskbarRect.left;
            data.rc.right = taskbarRect.right;
            data.rc.top = data.rc.bottom - TASKBAR_HEIGHT;
            SHAppBarMessage(ABM_SETPOS, ref data);

            if (moveTaskbarWindow)
            {
                SetWindowPos(
                    hwnd,
                    IntPtr.Zero,
                    data.rc.left,
                    data.rc.top,
                    data.rc.right - data.rc.left,
                    data.rc.bottom - data.rc.top,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }

            _reservedTaskbarHwnds.Add(hwnd);
        }
#endif
    }
}
