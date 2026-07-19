// Code-behind de la fenêtre Options : réutilise MainWindowViewModel comme
// DataContext (pas de ViewModel dédié) puisque ThemeToggleText/DataFilePath y
// vivent déjà.
using System.Diagnostics;
using System.IO;
using System.Windows;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop;

public partial class OptionsWindow : Window
{
    public OptionsWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ThemeService.RegisterWindow(this);

        ApiBaseUrlTextBox.Text = viewModel.ApiBaseUrl;
        ApiKeyTextBox.Text = viewModel.ApiKey;
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ((MainWindowViewModel)DataContext).ToggleTheme();
    }

    private async void TestApiConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = (MainWindowViewModel)DataContext;
        viewModel.UpdateApiSettings(ApiBaseUrlTextBox.Text.Trim(), ApiKeyTextBox.Text.Trim());

        ApiConnectionStatusText.Text = "Test en cours…";

        var status = await viewModel.TestApiConnectionAsync();

        ApiConnectionStatusText.Text = status switch
        {
            ApiConnectionStatus.Ok => "Connecté.",
            ApiConnectionStatus.InvalidApiKey => "Clé API invalide.",
            ApiConnectionStatus.Unreachable => "API injoignable.",
            _ => "Synchronisation désactivée (URL vide)."
        };
    }

    private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        // Le dossier n'existe pas forcément encore (aucune sauvegarde effectuée) :
        // on le crée pour que l'Explorateur ait toujours quelque chose à ouvrir.
        var folderPath = Path.GetDirectoryName(((MainWindowViewModel)DataContext).DataFilePath);

        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        Directory.CreateDirectory(folderPath);
        Process.Start(new ProcessStartInfo(folderPath) { UseShellExecute = true });
    }
}
