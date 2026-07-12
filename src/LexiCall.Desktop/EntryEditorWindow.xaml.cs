// Code-behind de la fenêtre d'édition d'entrée.
// La fenêtre expose SavedEntry au parent et se ferme quand le ViewModel signale
// qu'une entrée valide a été construite.
using System.Windows;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop;

public partial class EntryEditorWindow : Window
{
    private readonly EntryEditorWindowViewModel _viewModel;

    public EntryEditorWindow(
        VocabularyEntry? existingEntry = null,
        IEnumerable<VocabularyCategory>? availableCategories = null)
    {
        _viewModel = new EntryEditorWindowViewModel(existingEntry, availableCategories);

        InitializeComponent();
        DataContext = _viewModel;

        // Le ViewModel ne connaît pas WPF. Il émet donc un événement métier simple
        // que la fenêtre traduit en DialogResult.
        _viewModel.EntrySaved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    public VocabularyEntry? SavedEntry => _viewModel.SavedEntry;
}
