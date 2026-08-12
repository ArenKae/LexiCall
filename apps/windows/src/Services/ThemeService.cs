// Light/dark toggle: swaps the app's color ResourceDictionary and persists the
// choice to settings.json. Styles use DynamicResource, so the change applies
// live without reloading any window.
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LexiCall.Desktop.Services;

public enum AppTheme
{
    Light,
    Dark
}

public static class ThemeService
{
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public static void Initialize()
    {
        Apply(LoadSavedTheme(), persist: false);
    }

    public static void Toggle()
    {
        Apply(CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light, persist: true);
    }

    // The Windows title bar doesn't follow WPF resources — explicitly request
    // dark mode from DWM for each window.
    public static void RegisterWindow(Window window)
    {
        if (window.IsLoaded)
        {
            ApplyTitleBar(window);
        }
        else
        {
            window.SourceInitialized += (_, _) => ApplyTitleBar(window);
        }
    }

    private static void Apply(AppTheme theme, bool persist)
    {
        CurrentTheme = theme;

        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"Themes/Colors.{theme}.xaml", UriKind.Relative)
        };

        // The color dictionary always sits at index 0 (see App.xaml).
        Application.Current.Resources.MergedDictionaries[0] = dictionary;

        foreach (Window window in Application.Current.Windows)
        {
            ApplyTitleBar(window);
        }

        if (persist)
        {
            SaveTheme(theme);
        }
    }

    private static AppTheme LoadSavedTheme()
    {
        var settings = SettingsStore.Load();

        return string.Equals(settings.Theme, nameof(AppTheme.Dark), StringComparison.OrdinalIgnoreCase)
            ? AppTheme.Dark
            : AppTheme.Light;
    }

    private static void SaveTheme(AppTheme theme)
    {
        var settings = SettingsStore.Load();
        settings.Theme = theme.ToString();
        SettingsStore.Save(settings);
    }

    private static void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        var useDark = CurrentTheme == AppTheme.Dark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
    }

    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
