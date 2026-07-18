// Bascule clair/sombre : remplace le dictionnaire de couleurs de l'app et
// persiste le choix dans settings.json. Les styles utilisent DynamicResource,
// donc le changement s'applique à chaud sans recharger les fenêtres.
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

    // La barre de titre Windows ne suit pas les ressources WPF : on demande
    // explicitement le mode sombre à DWM pour chaque fenêtre.
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

        // Le dictionnaire de couleurs occupe toujours l'index 0 (voir App.xaml).
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
