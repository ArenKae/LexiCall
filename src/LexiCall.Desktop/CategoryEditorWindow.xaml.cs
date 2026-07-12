// Code-behind de la fenêtre d'édition de catégorie.
// Même pattern que EntryEditorWindow : SavedCategory est exposée au parent et
// la fenêtre se ferme quand le ViewModel signale une catégorie valide.
using System.Windows;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop;

public partial class CategoryEditorWindow : Window
{
    private readonly CategoryEditorWindowViewModel _viewModel;

    public CategoryEditorWindow(
        IEnumerable<VocabularyCategory> allCategories,
        VocabularyCategory? existingCategory = null,
        Guid? initialParentId = null)
    {
        _viewModel = new CategoryEditorWindowViewModel(allCategories, existingCategory, initialParentId);

        InitializeComponent();
        DataContext = _viewModel;
        ThemeService.RegisterWindow(this);

        _viewModel.CategorySaved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    public VocabularyCategory? SavedCategory => _viewModel.SavedCategory;
}
