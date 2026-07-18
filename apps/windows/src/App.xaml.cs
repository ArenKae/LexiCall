// Point d'entrée WPF de l'application.
// Charge le thème sauvegardé (clair par défaut) avant l'ouverture de la
// fenêtre principale déclarée dans App.xaml.
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
