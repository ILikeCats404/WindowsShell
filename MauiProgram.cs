using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

#if WINDOWS
using Microsoft.UI.Windowing;
using Microsoft.UI;
using WinRT.Interop;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Windows.Media.Control;
using WinUIWindow = Microsoft.UI.Xaml.Window;
#endif

namespace DesktopWallpaper
{
    public static class MauiProgram
    {
#if WINDOWS
        // =========================
        // Win32 Imports
        // =========================

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetModuleHandle(string? lpModuleName);
        [DllImport("shell32.dll")]
        static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll")]
        static extern int SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
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
        // =========================
        // Constants
        // =========================

        const int GWL_EXSTYLE = -20;
        const int GWLP_WNDPROC = -4;
        const int WM_HOTKEY = 0x0312;
        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x0100;
        const int WM_KEYUP = 0x0101;
        const int WM_SYSKEYDOWN = 0x0104;
        const int WM_SYSKEYUP = 0x0105;

        const int TOGGLE_CLICK_THROUGH_HOTKEY_ID = 9000;
        const int MEDIA_PLAY_PAUSE_HOTKEY_ID = 9001;
        const int MEDIA_NEXT_TRACK_HOTKEY_ID = 9002;
        const int MEDIA_PREV_TRACK_HOTKEY_ID = 9003;
        const int MEDIA_STOP_HOTKEY_ID = 9004;

        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;
        const uint VK_CAPITAL = 0x14;
        const int VK_SHIFT = 0x10;
        const int VK_LSHIFT = 0xA0;
        const int VK_RSHIFT = 0xA1;
        const int VK_S = 0x53;
        const int VK_LWIN = 0x5B;
        const int VK_RWIN = 0x5C;
        const int VK_VOLUME_MUTE = 0xAD;
        const int VK_VOLUME_DOWN = 0xAE;
        const int VK_VOLUME_UP = 0xAF;
        const int VK_MEDIA_NEXT_TRACK = 0xB0;
        const int VK_MEDIA_PREV_TRACK = 0xB1;
        const int VK_MEDIA_STOP = 0xB2;
        const int VK_MEDIA_PLAY_PAUSE = 0xB3;

        const int SW_SHOW = 5;

        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_LAYERED = 0x00080000;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int WS_EX_TOOLWINDOW = 0x00000080;

        // =========================
        // State
        // =========================

        private static IntPtr _hwnd;
        private static IntPtr _oldWndProc;
        private static IntPtr _keyboardHook;
        private static OverlappedPresenter? _presenter;
        private static readonly WndProc _wndProc = WndProcHandler;
        private static readonly LowLevelKeyboardProc _keyboardProc = KeyboardHookHandler;
        private static bool _clickThrough = true;
        private static bool _taskbarReserved;
        private static bool _winKeyDown;
        private static bool _shiftKeyDown;
        private static bool _screenSnipHandled;

        public static event Action? StartKeyPressed;

        // IMPORTANT: delegate type (NOT a method)
        private delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        const uint ABM_NEW = 0x00000000;
        const uint ABM_QUERYPOS = 0x00000002;
        const uint ABM_SETPOS = 0x00000003;

        const uint ABE_BOTTOM = 3;

        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_SHOWWINDOW = 0x0040;

        const int SM_CXSCREEN = 0;
        const int SM_CYSCREEN = 1;

#endif

        public static MauiApp CreateMauiApp()
        {
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
            builder.Services.AddScoped(sp => new HttpClient());
            builder.Services.AddSingleton<DesktopWallpaper.Services.TaskbarShortcutsService>();
            builder.Services.AddSingleton<DesktopWallpaper.Services.AppSearchService>();
            builder.Services.AddSingleton<DesktopWallpaper.Services.OpenWindowsService>();
            

            builder.ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(windows =>
                {
                    windows.OnWindowCreated(window =>
                    {
                        var nativeWindow = window as WinUIWindow;
                        _hwnd = WindowNative.GetWindowHandle(nativeWindow);

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

                        //ReserveBottomTaskbarSpace(_hwnd, 48);

                        // start click-through OFF
                        ApplyClickThrough(_hwnd, false);

                        // register hotkeys
                        RegisterHotKey(_hwnd, TOGGLE_CLICK_THROUGH_HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_CAPITAL);
                        RegisterHotKey(_hwnd, MEDIA_PLAY_PAUSE_HOTKEY_ID, 0, VK_MEDIA_PLAY_PAUSE);
                        RegisterHotKey(_hwnd, MEDIA_NEXT_TRACK_HOTKEY_ID, 0, VK_MEDIA_NEXT_TRACK);
                        RegisterHotKey(_hwnd, MEDIA_PREV_TRACK_HOTKEY_ID, 0, VK_MEDIA_PREV_TRACK);
                        RegisterHotKey(_hwnd, MEDIA_STOP_HOTKEY_ID, 0, VK_MEDIA_STOP);

                        if (_keyboardHook == IntPtr.Zero)
                        {
                            using var currentProcess = Process.GetCurrentProcess();
                            using var currentModule = currentProcess.MainModule;
                            var moduleHandle = GetModuleHandle(currentModule?.ModuleName);
                            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
                        }

                        // hook WndProc (ONLY ONCE)
                        _oldWndProc = SetWindowLongPtr(
                            _hwnd,
                            GWLP_WNDPROC,
                            Marshal.GetFunctionPointerForDelegate(_wndProc)
                        );
                    });
                });
#endif
            });
            
            return builder.Build();
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
#endif
        }

        public static void ToggleClickableMode()
        {
#if WINDOWS
            ToggleClickThrough();
#else
            DevStuff.IsClickThrough = !DevStuff.IsClickThrough;
#endif
        }

#if WINDOWS

        // =========================
        // Click-through toggle
        // =========================

        private static void ApplyClickThrough(IntPtr hwnd, bool enable)
        {
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

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
            }

            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }

        private static void ToggleClickThrough()
        {
            _clickThrough = !_clickThrough;
            DevStuff.IsClickThrough = _clickThrough;
            ApplyClickThrough(_hwnd, _clickThrough);
        }
        private static void ReserveBottomTaskbarSpace(IntPtr hwnd, int height)
{
    if (_taskbarReserved)
    {
        return;
    }

    int screenWidth = GetSystemMetrics(SM_CXSCREEN);
    int screenHeight = GetSystemMetrics(SM_CYSCREEN);

    APPBARDATA data = new()
    {
        cbSize = Marshal.SizeOf<APPBARDATA>(),
        hWnd = hwnd,
        uEdge = ABE_BOTTOM,
        rc = new RECT
        {
            left = 0,
            right = screenWidth,
            top = screenHeight - height,
            bottom = screenHeight
        }
    };

    SHAppBarMessage(ABM_NEW, ref data);
    SHAppBarMessage(ABM_QUERYPOS, ref data);

    data.rc.top = data.rc.bottom - height;

    SHAppBarMessage(ABM_SETPOS, ref data);

    SetWindowPos(
        hwnd,
        IntPtr.Zero,
        data.rc.left,
        data.rc.top,
        data.rc.right - data.rc.left,
        data.rc.bottom - data.rc.top,
        SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW
    );

        _taskbarReserved = true;
    }
        public static void OpenInteractiveMode()
        {
            _clickThrough = false;
            DevStuff.IsClickThrough = false;
            ApplyClickThrough(_hwnd, false);

            ShowWindow(_hwnd, SW_SHOW);
            _presenter?.Maximize();
            SetForegroundWindow(_hwnd);
        }

        private static IntPtr KeyboardHookHandler(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var message = wParam.ToInt32();
                var vkCode = Marshal.ReadInt32(lParam);

                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    if (IsShiftKey(vkCode))
                    {
                        _shiftKeyDown = true;
                        return _winKeyDown ? 1 : CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                    }

                    if (IsWinKey(vkCode))
                    {
                        _winKeyDown = true;
                        _screenSnipHandled = false;
                        return 1;
                    }

                    if (_winKeyDown && _shiftKeyDown && vkCode == VK_S)
                    {
                        _screenSnipHandled = true;
                        DisableInteractiveMode();
                        OpenScreenSnip();
                        return 1;
                    }

                    if (IsPlaybackMediaKey(vkCode))
                    {
                        _ = HandlePlaybackMediaKeyAsync(vkCode);
                        return 1;
                    }
                }
                else if (message == WM_KEYUP || message == WM_SYSKEYUP)
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
                            OpenInteractiveMode();
                            StartKeyPressed?.Invoke();
                        }

                        _winKeyDown = false;
                        _screenSnipHandled = false;
                        return 1;
                    }
                }
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

        private static bool IsPlaybackMediaKey(int vkCode)
        {
            return vkCode is VK_MEDIA_NEXT_TRACK
                or VK_MEDIA_PREV_TRACK
                or VK_MEDIA_STOP
                or VK_MEDIA_PLAY_PAUSE;
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
                    _ => false
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static void OpenScreenSnip()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-screenclip:",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        // =========================
        // REAL WndProc handler
        // =========================

        private static IntPtr WndProcHandler(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_HOTKEY)
            {
                var hotkeyId = wParam.ToInt32();

                if (hotkeyId == TOGGLE_CLICK_THROUGH_HOTKEY_ID)
                {
                    ToggleClickThrough();
                }
                else if (hotkeyId == MEDIA_PLAY_PAUSE_HOTKEY_ID)
                {
                    _ = HandlePlaybackMediaKeyAsync(VK_MEDIA_PLAY_PAUSE);
                }
                else if (hotkeyId == MEDIA_NEXT_TRACK_HOTKEY_ID)
                {
                    _ = HandlePlaybackMediaKeyAsync(VK_MEDIA_NEXT_TRACK);
                }
                else if (hotkeyId == MEDIA_PREV_TRACK_HOTKEY_ID)
                {
                    _ = HandlePlaybackMediaKeyAsync(VK_MEDIA_PREV_TRACK);
                }
                else if (hotkeyId == MEDIA_STOP_HOTKEY_ID)
                {
                    _ = HandlePlaybackMediaKeyAsync(VK_MEDIA_STOP);
                }
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

#endif
    }
}
