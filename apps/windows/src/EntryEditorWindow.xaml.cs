// Code-behind de la fenêtre d'édition d'entrée.
// La fenêtre expose SavedEntry au parent et se ferme quand le ViewModel signale
// qu'une entrée valide a été construite.
using System.Windows;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop;

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

        // Le ViewModel ne connaît pas WPF. Il émet donc un événement métier simple
        // que la fenêtre traduit en DialogResult.
        _viewModel.EntrySaved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    public VocabularyEntry? SavedEntry => _viewModel.SavedEntry;

    // Le sélecteur de fichiers est un détail WPF : le ViewModel reçoit
    // uniquement le chemin choisi et ne connaît rien d'OpenFileDialog.
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
