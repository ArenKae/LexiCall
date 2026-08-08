// Code-behind for the Options window: reuses MainWindowViewModel as
// DataContext (no dedicated ViewModel) since ThemeToggleText/DataFilePath
// already live there.
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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

        // Set both fields before wiring TextChanged: attaching it first would
        // have this fire on the ApiBaseUrlTextBox assignment below while
        // ApiKeyTextBox is still empty, saving an empty key over the real one.
        ApiBaseUrlTextBox.Text = viewModel.ApiBaseUrl;
        ApiKeyTextBox.Text = viewModel.ApiKey;

        ApiBaseUrlTextBox.TextChanged += ApiSettingsTextBox_TextChanged;
        ApiKeyTextBox.TextChanged += ApiSettingsTextBox_TextChanged;
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ((MainWindowViewModel)DataContext).ToggleTheme();
    }

    // Saves on every keystroke rather than only when "Tester la connexion" is
    // pressed — leaving the fields edited but unsaved was confusing (looked
    // like a form with no save action of its own).
    private void ApiSettingsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ((MainWindowViewModel)DataContext).UpdateApiSettings(ApiBaseUrlTextBox.Text.Trim(), ApiKeyTextBox.Text.Trim());
    }

    private async void TestApiConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = (MainWindowViewModel)DataContext;

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
        // The folder may not exist yet (no save has happened): create it so
        // Explorer always has something to open.
        var folderPath = Path.GetDirectoryName(((MainWindowViewModel)DataContext).DataFilePath);

        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        Directory.CreateDirectory(folderPath);
        Process.Start(new ProcessStartInfo(folderPath) { UseShellExecute = true });
    }
}
