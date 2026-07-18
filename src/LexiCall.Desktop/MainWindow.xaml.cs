// Code-behind de la fenêtre principale.
// Il reste volontairement mince : il ouvre les fenêtres modales, traduit les
// interactions de l'arbre (menu contextuel, renommage inline) en appels au
// MainWindowViewModel, et affiche les confirmations/erreurs en MessageBox.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleTheme();
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SearchQuery = string.Empty;
        SearchTextBox.Focus();
    }

    // ─── Entrées ───

    private void AddEntryButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EntryEditorWindow(availableCategories: ViewModel.Categories)
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
        // Suppression confirmée : elle modifie ensuite immédiatement le JSON local
        // via le ViewModel.
        var confirmed = ConfirmationDialog.Show(
            this,
            $"Supprimer « {selectedEntry.Word} » ?",
            "Confirmer la suppression");

        if (confirmed)
        {
            ViewModel.DeleteEntry(selectedEntry);
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

    // Reclic sur un nœud déjà sélectionné : referme ce qu'un premier clic vient
    // d'ouvrir (voir CategoryNodeViewModel.IsSelected). Le clic initial est laissé
    // au TreeViewItem lui-même, qui gère la sélection.
    private void CategoryNode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CategoryNodeViewModel { IsEditing: false, Children.Count: > 0 } node } ||
            !node.IsSelected)
        {
            return;
        }

        node.IsExpanded = !node.IsExpanded;
        e.Handled = true;
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

    // Le TextBox reste Collapsed tant que IsEditing est faux ; son Loaded ne se
    // déclenche donc qu'une fois, à la création du conteneur du nœud, bien avant
    // le premier BeginEdit(). On réagit plutôt à chaque passage à Visible, en
    // différant le focus après la passe de layout qui suit ce changement.
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
