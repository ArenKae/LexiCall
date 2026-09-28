// Code-behind for the Options window: reuses MainWindowViewModel as
// DataContext (no dedicated ViewModel) since ThemeToggleText/DataFilePath
// already live there.

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop.Windows;

public partial class OptionsWindow : Window
{
    private readonly DispatcherTimer _apiSettingsSaveTimer;

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

        // Applying a keystroke at a time would rewrite settings.json and
        // rebuild the HTTP client once per typed character.
        _apiSettingsSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _apiSettingsSaveTimer.Tick += (_, _) => ApplyApiSettings();

        // Whatever the last keystroke left pending must still be saved when
        // the window closes before the timer gets a chance to fire.
        Closed += (_, _) =>
        {
            _apiSettingsSaveTimer.Stop();
            ApplyApiSettings();
        };

        ApiBaseUrlTextBox.TextChanged += ApiSettingsTextBox_TextChanged;
        ApiKeyTextBox.TextChanged += ApiSettingsTextBox_TextChanged;
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ((MainWindowViewModel)DataContext).ToggleTheme();
    }

    // Saves as the user types rather than only when "Tester la connexion" is
    // pressed — leaving the fields edited but unsaved was confusing (looked
    // like a form with no save action of its own).
    private void ApiSettingsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _apiSettingsSaveTimer.Stop();
        _apiSettingsSaveTimer.Start();
    }

    private void ApplyApiSettings()
    {
        _apiSettingsSaveTimer.Stop();
        ((MainWindowViewModel)DataContext).UpdateApiSettings(ApiBaseUrlTextBox.Text.Trim(), ApiKeyTextBox.Text.Trim());
    }

    private async void TestApiConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = (MainWindowViewModel)DataContext;

        // The test must use what is in the fields now, not what the debounce
        // timer has not applied yet.
        ApplyApiSettings();

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

    private async void FullResyncButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = (MainWindowViewModel)DataContext;

        // Same as the connection test: resync against what the fields hold now.
        ApplyApiSettings();

        // Progress is shown by the button's own spinner and label; the status
        // line only carries the outcome.
        FullResyncButton.IsEnabled = false;
        ApiConnectionStatusText.Text = string.Empty;

        try
        {
            await viewModel.ForceFullResyncAsync();
        }
        finally
        {
            FullResyncButton.IsEnabled = true;
        }

        ApiConnectionStatusText.Text = viewModel.GlobalSyncStatus switch
        {
            GlobalSyncStatus.Ok => "Resynchronisation terminée.",
            // A cycle was already running: this one was queued behind it.
            GlobalSyncStatus.Syncing => "Resynchronisation en cours en arrière-plan.",
            GlobalSyncStatus.NotConfigured => "Synchronisation désactivée (URL vide).",
            _ => "Resynchronisation incomplète, voir l'historique."
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
