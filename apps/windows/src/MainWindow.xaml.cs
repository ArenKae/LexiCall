// Code-behind de la fenêtre principale : ouvre les fenêtres modales, relaie les
// interactions de l'arbre de catégories au MainWindowViewModel, et affiche les
// confirmations/erreurs en MessageBox.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        ThemeService.RegisterWindow(this);
        WindowLayoutService.Apply(this, CategoryColumn, EntryListColumn);
        Closing += MainWindow_Closing;

        // Attaché au TreeView (pas au DataTemplate) pour que la zone cliquable
        // corresponde au halo de sélection du TreeViewItem.
        CategoryTreeView.AddHandler(
            PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(CategoryNode_MouseLeftButtonDown),
            handledEventsToo: true);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        WindowLayoutService.Save(this, CategoryColumn, EntryListColumn);
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        new OptionsWindow(ViewModel) { Owner = this }.ShowDialog();
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SearchQuery = string.Empty;
        SearchTextBox.Focus();
    }

    // ─── Entrées ───

    private void AddEntryButton_Click(object sender, RoutedEventArgs e)
    {
        // Pré-sélectionne la catégorie active de l'arbre : ajout probable dans le contexte courant.
        var dialog = new EntryEditorWindow(
            availableCategories: ViewModel.Categories,
            initialCategoryId: ViewModel.SelectedCategoryNode?.Category?.Id)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true &&
            dialog.SavedEntry is not null)
        {
            ViewModel.AddEntry(dialog.SavedEntry);
        }
    }

    private void EditEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEntry is null)
        {
            return;
        }

        var dialog = new EntryEditorWindow(ViewModel.SelectedEntry, ViewModel.Categories)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true &&
            dialog.SavedEntry is not null)
        {
            ViewModel.UpdateEntry(dialog.SavedEntry);
        }
    }

    private void DeleteEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEntry is null)
        {
            return;
        }

        var selectedEntry = ViewModel.SelectedEntry;
        var confirmed = ConfirmationDialog.Show(
            this,
            $"Supprimer « {selectedEntry.Word} » ?",
            "Confirmer la suppression");

        if (confirmed)
        {
            ViewModel.DeleteEntry(selectedEntry);
        }
    }

    // Redécode le base64 plutôt que de réutiliser le Source de l'Image, pour ne
    // pas dépendre de l'ordre binding/évènement.
    private void DetailImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: Models.VocabularyEntry entry })
        {
            return;
        }

        var image = Converters.Base64ImageConverter.ToBitmapImage(entry.ImageBase64);

        if (image is null)
        {
            return;
        }

        new ImagePreviewWindow(this, image).ShowDialog();
    }

    // Le chip porte la catégorie (VocabularyCategory) en DataContext.
    private void CategoryChip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Models.VocabularyCategory category })
        {
            ViewModel.SelectCategory(category.Id);
            e.Handled = true;
        }
    }

    // ─── Catégories ───

    private void AddRootCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        OpenCategoryEditor(existingCategory: null, initialParentId: null);
    }

    private void AddSubCategoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeFromMenuItem(sender) is { Category: not null } node)
        {
            node.IsExpanded = true;
            OpenCategoryEditor(existingCategory: null, initialParentId: node.Category.Id);
        }
    }

    private void EditCategoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeFromMenuItem(sender) is { Category: not null } node)
        {
            OpenCategoryEditor(node.Category, initialParentId: null);
        }
    }

    private void RenameCategoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeFromMenuItem(sender) is { IsVirtual: false } node)
        {
            node.BeginEdit();
        }
    }

    private void DeleteCategoryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeFromMenuItem(sender) is not { Category: not null } node)
        {
            return;
        }

        var confirmed = ConfirmationDialog.Show(
            this,
            $"Supprimer la catégorie « {node.Category.Name} » ?",
            "Confirmer la suppression");

        if (!confirmed)
        {
            return;
        }

        var error = ViewModel.DeleteCategory(node.Category.Id);

        if (error is not null)
        {
            MessageBox.Show(this, error, "Suppression impossible", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenCategoryEditor(Models.VocabularyCategory? existingCategory, Guid? initialParentId)
    {
        var dialog = new CategoryEditorWindow(ViewModel.Categories, existingCategory, initialParentId)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true &&
            dialog.SavedCategory is not null)
        {
            var error = ViewModel.SaveCategory(dialog.SavedCategory);

            if (error is not null)
            {
                MessageBox.Show(this, error, "Enregistrement impossible", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    // Le DataContext du MenuItem est le nœud sur lequel le menu contextuel
    // a été ouvert (hérité du PlacementTarget).
    private static CategoryNodeViewModel? GetNodeFromMenuItem(object sender)
    {
        return sender is MenuItem { DataContext: CategoryNodeViewModel node }
            ? node
            : null;
    }

    // Les nœuds virtuels ("Toutes les entrées", "Sans catégorie") ne sont pas
    // des catégories : aucun menu contextuel pour eux.
    private void CategoryNode_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CategoryNodeViewModel { IsVirtual: true } })
        {
            e.Handled = true;
        }
    }

    // Gère nous-mêmes le clic (sélection + dépli) pour éviter un conflit avec le
    // double-clic natif de TreeViewItem ; on remonte au TreeViewItem depuis le
    // point de clic pour matcher le halo de sélection, plus large que la Grid.
    private void CategoryNode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element is not null)
        {
            if (element is ToggleButton)
            {
                // La flèche d'expansion gère déjà elle-même l'ouverture/fermeture.
                return;
            }

            if (element is TreeViewItem treeViewItem)
            {
                if (treeViewItem.DataContext is CategoryNodeViewModel { IsEditing: false } node)
                {
                    if (node.Children.Count > 0)
                    {
                        node.IsExpanded = !node.IsExpanded;
                    }

                    node.IsSelected = true;
                    treeViewItem.Focus();
                    e.Handled = true;
                }

                return;
            }

            element = VisualTreeHelper.GetParent(element);
        }
    }

    private void CategoryTreeView_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2 &&
            ViewModel.SelectedCategoryNode is { IsVirtual: false } node)
        {
            node.BeginEdit();
            e.Handled = true;
        }
    }

    // ─── Renommage inline ───

    // Loaded ne se déclenche qu'une fois (à la création du conteneur), bien avant
    // le premier BeginEdit() : on réagit donc à Visible à chaque édition.
    private void RenameTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox { DataContext: CategoryNodeViewModel { IsEditing: true } } textBox ||
            textBox.Visibility != Visibility.Visible)
        {
            return;
        }

        textBox.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            textBox.Focus();
            textBox.SelectAll();
        }));
    }

    private void RenameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: CategoryNodeViewModel node })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitRename(node, interactive: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            node.IsEditing = false;
            e.Handled = true;
        }
    }

    private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: CategoryNodeViewModel node })
        {
            // Perte de focus : on tente de valider, mais sans bloquer l'utilisateur
            // avec une erreur — un nom invalide est simplement abandonné.
            CommitRename(node, interactive: false);
        }
    }

    private void CommitRename(CategoryNodeViewModel node, bool interactive)
    {
        if (!node.IsEditing || node.Category is null)
        {
            return;
        }

        var newName = node.EditName.Trim();

        // IsEditing repasse à false avant l'appel au ViewModel pour éviter que le
        // LostFocus déclenché par le masquage du TextBox ne revalide une 2e fois.
        node.IsEditing = false;

        if (string.IsNullOrWhiteSpace(newName) || newName == node.Category.Name)
        {
            return;
        }

        var error = ViewModel.RenameCategory(node.Category.Id, newName);

        if (error is not null && interactive)
        {
            MessageBox.Show(this, error, "Renommage impossible", MessageBoxButton.OK, MessageBoxImage.Warning);
            node.EditName = newName;
            node.IsEditing = true;
        }
    }
}
