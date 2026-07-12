// Code-behind de la fenêtre principale.
// Il reste volontairement mince : il ouvre les fenêtres modales WPF, puis délègue
// les changements de données au MainWindowViewModel.
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
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var dialog = new EntryEditorWindow(availableCategories: viewModel.Categories)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true &&
            dialog.SavedEntry is not null)
        {
            viewModel.AddEntry(dialog.SavedEntry);
        }
    }

    private void ManageCategoriesButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var dialog = new CategoriesWindow(viewModel.Categories, viewModel.Entries)
        {
            Owner = this
        };

        // Les catégories ne sont remplacées dans l'application qu'après validation
        // explicite de la fenêtre modale.
        if (dialog.ShowDialog() == true)
        {
            viewModel.ReplaceCategories(dialog.SavedCategories);
        }
    }

    private void EditEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            viewModel.SelectedEntry is null)
        {
            return;
        }

        var dialog = new EntryEditorWindow(viewModel.SelectedEntry, viewModel.Categories)
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
        // Suppression confirmée : elle modifie ensuite immédiatement le JSON local
        // via le ViewModel.
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
