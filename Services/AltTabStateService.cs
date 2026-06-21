namespace DesktopWallpaper.Services
{
    public class AltTabStateService
    {
        private readonly OpenWindowsService openWindowsService;
        private List<OpenWindowInfo> windows = new();
        private int selectedIndex;

        public event Action? Changed;

        public AltTabStateService(OpenWindowsService openWindowsService)
        {
            this.openWindowsService = openWindowsService;
        }

        public IReadOnlyList<OpenWindowInfo> Windows => windows;

        public int SelectedIndex => selectedIndex;

        public void Start()
        {
            windows = openWindowsService.GetOpenWindows();
            selectedIndex = windows.Count == 0
                ? 0
                : Math.Min(1, windows.Count - 1);
            Changed?.Invoke();
        }

        public void Cycle()
        {
            if (windows.Count == 0)
            {
                Start();
                return;
            }

            selectedIndex = (selectedIndex + 1) % windows.Count;
            Changed?.Invoke();
        }

        public void ActivateSelected()
        {
            if (windows.Count == 0)
            {
                return;
            }

            var selectedWindow = windows[Math.Clamp(selectedIndex, 0, windows.Count - 1)];
            openWindowsService.ActivateWindow(selectedWindow);
        }
    }
}
