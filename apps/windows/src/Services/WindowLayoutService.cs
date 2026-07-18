// Persiste taille/position de la fenêtre principale et largeur des deux colonnes
// redimensionnables (catégories, liste des mots) dans settings.json. La 3e
// colonne (détail) reste élastique et n'est volontairement pas sauvegardée.
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

        // Avant la création du HWND, WPF ne peut pas résoudre le DPI cible et se
        // rabat sur le moniteur sous le curseur (position instable au lancement).
        // On attend donc SourceInitialized puis on positionne en pixels physiques
        // via Win32, plutôt que Window.Left/Top/Width/Height.
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

        // NormalPosition donne la géométrie "restaurée" en pixels physiques même
        // si la fenêtre est maximisée/réduite, contrairement à Window.Left/Top/
        // Width/Height qui restent en unités logiques dépendantes du DPI courant.
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

    // Ignore la position sauvegardée si l'écran a été déconnecté ou la résolution
    // changée, pour éviter que la fenêtre apparaisse hors de vue.
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
