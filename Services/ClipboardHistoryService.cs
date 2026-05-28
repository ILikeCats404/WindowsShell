using System.Runtime.InteropServices;

namespace DesktopWallpaper.Services
{
    public class ClipboardHistoryItem
    {
        public string Text { get; set; } = "";
        public DateTime CapturedAt { get; set; } = DateTime.Now;
    }

    public class ClipboardHistoryService
    {
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        private readonly List<ClipboardHistoryItem> _items = new();
        private readonly object _lock = new();
        private string _lastText = "";

        public event Action? HistoryChanged;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        public IReadOnlyList<ClipboardHistoryItem> Items
        {
            get
            {
                lock (_lock)
                {
                    return _items.ToList();
                }
            }
        }

        public void Poll()
        {
            var text = TryGetClipboardText();
            if (string.IsNullOrWhiteSpace(text) || text == _lastText)
            {
                return;
            }

            lock (_lock)
            {
                _lastText = text;
                _items.RemoveAll(item => item.Text == text);
                _items.Insert(0, new ClipboardHistoryItem
                {
                    Text = text,
                    CapturedAt = DateTime.Now,
                });

                if (_items.Count > 30)
                {
                    _items.RemoveRange(30, _items.Count - 30);
                }
            }

            HistoryChanged?.Invoke();
        }

        public void CopyToClipboard(ClipboardHistoryItem item)
        {
            SetClipboardText(item.Text);
            _lastText = item.Text;
        }

        public void Clear()
        {
            lock (_lock)
            {
                _items.Clear();
                _lastText = "";
            }

            HistoryChanged?.Invoke();
        }

        private static string? TryGetClipboardText()
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            if (!OpenClipboard(IntPtr.Zero))
            {
                return null;
            }

            try
            {
                if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
                {
                    return null;
                }

                var handle = GetClipboardData(CF_UNICODETEXT);
                if (handle == IntPtr.Zero)
                {
                    return null;
                }

                var pointer = GlobalLock(handle);
                if (pointer == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    return Marshal.PtrToStringUni(pointer);
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        private static void SetClipboardText(string text)
        {
            if (!OperatingSystem.IsWindows() || !OpenClipboard(IntPtr.Zero))
            {
                return;
            }

            try
            {
                EmptyClipboard();

                var bytes = (text.Length + 1) * 2;
                var handle = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
                if (handle == IntPtr.Zero)
                {
                    return;
                }

                var pointer = GlobalLock(handle);
                if (pointer == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
                    Marshal.WriteInt16(pointer, text.Length * 2, 0);
                }
                finally
                {
                    GlobalUnlock(handle);
                }

                SetClipboardData(CF_UNICODETEXT, handle);
            }
            finally
            {
                CloseClipboard();
            }
        }
    }
}
