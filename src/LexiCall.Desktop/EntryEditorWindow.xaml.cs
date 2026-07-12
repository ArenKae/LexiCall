using System.Windows;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop;

public partial class EntryEditorWindow : Window
{
    private readonly EntryEditorWindowViewModel _viewModel;

    public EntryEditorWindow(VocabularyEntry? existingEntry = null)
    {
        _viewModel = new EntryEditorWindowViewModel(existingEntry);

        InitializeComponent();
        DataContext = _viewModel;

        _viewModel.EntrySaved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    public VocabularyEntry? SavedEntry => _viewModel.SavedEntry;
}
