using System.Windows;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop;

public partial class AddEntryWindow : Window
{
    private readonly AddEntryWindowViewModel _viewModel;

    public AddEntryWindow(VocabularyEntry? existingEntry = null)
    {
        _viewModel = new AddEntryWindowViewModel(existingEntry);

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
