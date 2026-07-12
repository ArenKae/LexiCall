// Code-behind de la fenêtre de gestion des catégories.
// Il relaie les clics vers le ViewModel et renvoie au parent la liste validée.
using System.Windows;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop;

public partial class CategoriesWindow : Window
{
    private readonly CategoriesWindowViewModel _viewModel;

    public CategoriesWindow(
        IEnumerable<VocabularyCategory> categories,
        IEnumerable<VocabularyEntry> entries)
    {
        _viewModel = new CategoriesWindowViewModel(categories, entries);

        InitializeComponent();
        DataContext = _viewModel;
    }

    public IReadOnlyList<VocabularyCategory> SavedCategories => _viewModel.SavedCategories;

    private void NewCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearForm();
    }

    private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddCategory();
    }

    private void UpdateCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.UpdateSelectedCategory();
    }

    private void DeleteCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeleteSelectedCategory();
    }

    private void AcceptButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.SavePendingChanges())
        {
            return;
        }

        DialogResult = true;
        Close();
    }
}
