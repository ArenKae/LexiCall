// Modal recapping recent sync operations (push/pull/delete) — see
// SyncHistoryWindow.xaml. Reuses MainWindowViewModel as DataContext (its
// SyncHistory collection) rather than a dedicated ViewModel.
using System.Windows;
using System.Windows.Input;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop.Windows;

public partial class SyncHistoryWindow : Window
{
    public SyncHistoryWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ThemeService.RegisterWindow(this);
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = ConfirmationDialog.Show(
            this,
            "Vider tout l'historique de synchronisation ?",
            "Confirmer la suppression",
            "Vider");

        if (confirmed)
        {
            ((MainWindowViewModel)DataContext).ClearSyncHistory();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
