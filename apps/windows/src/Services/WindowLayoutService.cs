// Persists the main window's size/position and the two resizable columns'
// widths (categories, entry list) to settings.json. The 3rd column (detail)
// stays elastic and is deliberately not saved.
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace LexiCall.Desktop.Services;

public static class WindowLayoutService
{
    public static void Apply(Window window, ColumnDefinition categoryColumn, ColumnDefinition entryListColumn)
    {
        var settings = SettingsStore.Load();

        if (settings.CategoryColumnWidth is > 0)
        {
            categoryColumn.Width = new GridLength(settings.CategoryColumnWidth.Value);
        }

        if (settings.EntryListColumnWidth is > 0)
        {
            entryListColumn.Width = new GridLength(settings.EntryListColumnWidth.Value);
        }

        if (settings.WindowLeft is not double left ||
            settings.WindowTop is not double top ||
            settings.WindowWidth is not > 0 ||
            settings.WindowHeight is not > 0)
        {
            return;
        }

        var width = settings.WindowWidth.Value;
        var height = settings.WindowHeight.Value;

        // Before the HWND exists, WPF can't resolve the target DPI and falls
        // back to the monitor under the cursor (unstable startup position).
        // Wait for SourceInitialized and position in physical pixels via
        // Win32 instead of Window.Left/Top/Width/Height.
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.SourceInitialized += (_, _) =>
        {
            if (!IsOnScreen(left, top, width, height))
            {
                return;
            }

            var handle = new WindowInteropHelper(window).Handle;
            SetWindowPos(handle, IntPtr.Zero, (int)left, (int)top, (int)width, (int)height, SwpNoZOrder | SwpNoActivate);
        };
    }

    public static void Save(Window window, ColumnDefinition categoryColumn, ColumnDefinition entryListColumn)
    {
        var settings = SettingsStore.Load();
        var handle = new WindowInteropHelper(window).Handle;

        // NormalPosition gives the "restored" geometry in physical pixels even
        // while the window is maximized/minimized, unlike Window.Left/Top/
        // Width/Height, which stay in DPI-dependent logical units.
        if (handle != IntPtr.Zero && TryGetNormalBounds(handle, out var bounds))
        {
            settings.WindowLeft = bounds.Left;
            settings.WindowTop = bounds.Top;
            settings.WindowWidth = bounds.Right - bounds.Left;
            settings.WindowHeight = bounds.Bottom - bounds.Top;
        }

        settings.CategoryColumnWidth = categoryColumn.ActualWidth;
        settings.EntryListColumnWidth = entryListColumn.ActualWidth;
        SettingsStore.Save(settings);
    }

    // Ignores the saved position if the monitor was disconnected or the
    // resolution changed, so the window never lands off-screen.
    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        const double margin = 50;

        var virtualLeft = GetSystemMetrics(SmXVirtualScreen);
        var virtualTop = GetSystemMetrics(SmYVirtualScreen);
        var virtualWidth = GetSystemMetrics(SmCxVirtualScreen);
        var virtualHeight = GetSystemMetrics(SmCyVirtualScreen);

        return left + width > virtualLeft + margin &&
               left < virtualLeft + virtualWidth - margin &&
               top + height > virtualTop + margin &&
               top < virtualTop + virtualHeight - margin;
    }

    private static bool TryGetNormalBounds(IntPtr handle, out RECT bounds)
    {
        var placement = new WINDOWPLACEMENT { Length = Marshal.SizeOf<WINDOWPLACEMENT>() };

        if (!GetWindowPlacement(handle, ref placement))
        {
            bounds = default;
            return false;
        }

        bounds = placement.NormalPosition;
        return true;
    }

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int Length;
        public int Flags;
        public int ShowCmd;
        public POINT MinPosition;
        public POINT MaxPosition;
        public RECT NormalPosition;
    }
}
