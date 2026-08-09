// Code-behind for the entry editor window. Exposes SavedEntry to the caller
// and closes when the ViewModel signals a valid entry was built.
using System.Windows;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop.Windows;

public partial class EntryEditorWindow : Window
{
    private readonly EntryEditorWindowViewModel _viewModel;

    public EntryEditorWindow(
        VocabularyEntry? existingEntry = null,
        IEnumerable<VocabularyCategory>? availableCategories = null,
        Guid? initialCategoryId = null)
    {
        _viewModel = new EntryEditorWindowViewModel(existingEntry, availableCategories, initialCategoryId);

        InitializeComponent();
        DataContext = _viewModel;
        ThemeService.RegisterWindow(this);

        // The ViewModel knows nothing about WPF: it raises a plain business
        // event that the window translates into a DialogResult.
        _viewModel.EntrySaved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    public VocabularyEntry? SavedEntry => _viewModel.SavedEntry;

    // The file picker is a WPF detail: the ViewModel only receives the chosen
    // path and knows nothing about OpenFileDialog.
    private void SelectImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.SetImageFromFile(dialog.FileName);
        }
    }
}
