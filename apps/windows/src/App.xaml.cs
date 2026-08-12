// WPF entry point for the application. Loads the saved theme (light by
// default) before the main window declared in App.xaml opens.
using System.Windows;
using LexiCall.Desktop.Services;

namespace LexiCall.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService.Initialize();
    }
}
