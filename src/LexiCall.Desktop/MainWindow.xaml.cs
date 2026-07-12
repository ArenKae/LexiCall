using System.Windows;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private void AddEntryButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EntryEditorWindow
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true &&
            dialog.SavedEntry is not null &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.AddEntry(dialog.SavedEntry);
        }
    }

    private void EditEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            viewModel.SelectedEntry is null)
        {
            return;
        }

        var dialog = new EntryEditorWindow(viewModel.SelectedEntry)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true &&
            dialog.SavedEntry is not null)
        {
            viewModel.UpdateEntry(dialog.SavedEntry);
        }
    }

    private void DeleteEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            viewModel.SelectedEntry is null)
        {
            return;
        }

        var selectedEntry = viewModel.SelectedEntry;
        var result = MessageBox.Show(
            this,
            $"Supprimer « {selectedEntry.Word} » ?",
            "Confirmer la suppression",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            viewModel.DeleteEntry(selectedEntry);
        }
    }
}
