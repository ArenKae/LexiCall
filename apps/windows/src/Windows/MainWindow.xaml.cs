// Code-behind for the main window: opens modal windows, relays category-tree
// interactions to MainWindowViewModel, and shows confirmations/errors via MessageBox.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.ViewModels;

namespace LexiCall.Desktop.Windows;

public partial class MainWindow : Window
{
    // Sidebar collapse: the panel's own toggle shrinks CategoryColumn down to
    // this width instead of hiding the column outright, so the toggle button
    // itself (hosted inside that column) stays put and clickable. Wide enough
    // for a 32px icon slot plus the card's own padding.
    private const double CollapsedCategoryColumnWidth = 56;
    private const double ExpandedCategoryColumnMinWidth = 220;

    private double _expandedCategoryColumnWidth = 320;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        ThemeService.RegisterWindow(this);
        WindowLayoutService.Apply(this, CategoryColumn, EntryListColumn);
        _expandedCategoryColumnWidth = CategoryColumn.Width.Value;

        if (SettingsStore.Load().CategoryPanelCollapsed)
        {
            ApplyCategoryPanelCollapsedState(isCollapsed: true);
        }

        Closing += MainWindow_Closing;

        // Attached to the TreeView (not the DataTemplate) so the clickable
        // area matches the TreeViewItem's selection highlight.
        CategoryTreeView.AddHandler(
            PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(CategoryNode_MouseLeftButtonDown),
            handledEventsToo: true);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var isCollapsed = CategoryPanelToggleButton.IsChecked == true;

        if (isCollapsed)
        {
            // Only the last expanded width is persisted, never the transient
            // collapsed sliver — CategoryPanelCollapsed below is what remembers
            // the collapsed state itself. UpdateLayout forces ActualWidth to
            // reflect this before Save reads it.
            CategoryColumn.Width = new GridLength(_expandedCategoryColumnWidth);
            UpdateLayout();
        }

        WindowLayoutService.Save(this, CategoryColumn, EntryListColumn);

        var settings = SettingsStore.Load();
        settings.CategoryPanelCollapsed = isCollapsed;
        SettingsStore.Save(settings);
    }

    private void CategoryPanelToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var isCollapsed = CategoryPanelToggleButton.IsChecked == true;

        if (isCollapsed)
        {
            _expandedCategoryColumnWidth = CategoryColumn.ActualWidth;
        }

        ApplyCategoryPanelCollapsedState(isCollapsed);
    }

    private void ApplyCategoryPanelCollapsedState(bool isCollapsed)
    {
        CategoryPanelToggleButton.IsChecked = isCollapsed;

        if (isCollapsed)
        {
            CategoryColumn.MinWidth = CollapsedCategoryColumnWidth;
            CategoryColumn.Width = new GridLength(CollapsedCategoryColumnWidth);
        }
        else
        {
            CategoryColumn.MinWidth = ExpandedCategoryColumnMinWidth;
            CategoryColumn.Width = new GridLength(_expandedCategoryColumnWidth);
        }

        var contentVisibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;
        CategoryPanelTitle.Visibility = contentVisibility;
        AddRootCategoryButton.Visibility = contentVisibility;
        CategoryTreeView.Visibility = contentVisibility;
        CategoryColumnSplitter.Visibility = contentVisibility;
        CollapsedQuickSelectPanel.Visibility = isCollapsed ? Visibility.Visible : Visibility.Collapsed;
        CategoryPanelToggleButton.HorizontalAlignment = isCollapsed
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;
        CategoryPanelToggleButton.Margin = isCollapsed
            ? new Thickness(-2, 0, 0, 0)
            : new Thickness(0);

        FooterExpandedPanel.Visibility = contentVisibility;
        FooterCollapsedPanel.Visibility = isCollapsed ? Visibility.Visible : Visibility.Collapsed;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        new OptionsWindow(ViewModel) { Owner = this }.ShowDialog();
    }

    // The footer's settings row is a Border (matching the tree row/rail tile
    // recipe), not a Button, so it's wired via click instead of Command.
    private void OptionsRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        OptionsButton_Click(sender, e);
    }

    private void SyncStatusRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        new SyncHistoryWindow(ViewModel) { Owner = this }.ShowDialog();
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SearchQuery = string.Empty;
        SearchTextBox.Focus();
    }

    // ─── Entries ───

    private void AddEntryButton_Click(object sender, RoutedEventArgs e)
    {
        // Pre-selects the tree's active category: the new entry is likely
        // meant for the current context.
        var dialog = new EntryEditorWindow(
            availableCategories: ViewModel.Categories,
            initialCategoryId: ViewModel.SelectedCategoryNode?.Category?.Id)
        {
            Owner = this
        };

        // Stops the periodic sync from merging a pull while this window is
        // open (see MainWindowViewModel.TryResyncAsync).
        ViewModel.IsEditorDialogOpen = true;
        try
        {
            if (dialog.ShowDialog() == true &&
                dialog.SavedEntry is not null)
            {
                ViewModel.AddEntry(dialog.SavedEntry);
            }
        }
        finally
        {
            ViewModel.IsEditorDialogOpen = false;
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

        ViewModel.IsEditorDialogOpen = true;
        try
        {
            if (dialog.ShowDialog() == true &&
                dialog.SavedEntry is not null)
            {
                ViewModel.UpdateEntry(dialog.SavedEntry);
            }
        }
        finally
        {
            ViewModel.IsEditorDialogOpen = false;
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

    // Re-decodes the base64 rather than reusing the Image's Source, so this
    // doesn't depend on binding/event ordering.
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

    // The chip carries its category (VocabularyCategory) as DataContext.
    private void CategoryChip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Models.VocabularyCategory category })
        {
            if (CategoryPanelToggleButton.IsChecked == true)
            {
                ApplyCategoryPanelCollapsedState(isCollapsed: false);
            }

            ViewModel.SelectCategory(category.Id);

            if (ViewModel.SelectedCategoryNode is { } selectedNode)
            {
                BringCategoryNodeIntoView(selectedNode);
            }

            e.Handled = true;
        }
    }

    // TreeViewItem containers only materialize for an item once its parent
    // is expanded and a layout pass has run, so each ancestor's container is
    // resolved and laid out before searching the next level down.
    private void BringCategoryNodeIntoView(CategoryNodeViewModel targetNode)
    {
        var path = new List<CategoryNodeViewModel>();
        var current = targetNode;

        while (current is not null)
        {
            path.Insert(0, current);
            current = FindParentNode(ViewModel.CategoryTree, current);
        }

        ItemsControl container = CategoryTreeView;
        TreeViewItem? item = null;

        foreach (var node in path)
        {
            container.UpdateLayout();

            if (container.ItemContainerGenerator.ContainerFromItem(node) is not TreeViewItem nextItem)
            {
                return;
            }

            item = nextItem;
            container = nextItem;
        }

        item?.BringIntoView();
    }

    // ─── Categories ───

    // The two virtual nodes always sit first in CategoryTree (see
    // MainWindowViewModel.RebuildCategoryTree). Setting IsSelected runs the
    // same selection path as clicking them in the tree (CategoryNodeViewModel.
    // IsSelected's setter calls back into OnCategoryNodeSelected).
    private void SelectAllEntriesButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CategoryTree[0].IsSelected = true;
    }

    private void SelectUncategorizedButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CategoryTree[1].IsSelected = true;
    }

    // The swatch carries its node (CategoryNodeViewModel) as DataContext —
    // same selection path as the two virtual-node buttons above.
    private void CollapsedRootCategory_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CategoryNodeViewModel node })
        {
            node.IsSelected = true;
            e.Handled = true;
        }
    }

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

    private void MoveCategoryUpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeFromMenuItem(sender) is { Category: not null } node)
        {
            ViewModel.MoveCategoryUp(node.Category.Id);
        }
    }

    private void MoveCategoryDownMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNodeFromMenuItem(sender) is { Category: not null } node)
        {
            ViewModel.MoveCategoryDown(node.Category.Id);
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

        ViewModel.IsEditorDialogOpen = true;
        try
        {
            if (dialog.ShowDialog() == true &&
                dialog.SavedCategory is not null)
            {
                var error = ViewModel.SaveCategory(dialog.SavedCategory);

                if (error is not null)
                {
                    MessageBox.Show(this, error, "Enregistrement impossible", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    ViewModel.SetCategoryColor(dialog.SavedCategory.Id, dialog.SavedColorHex);
                }
            }
        }
        finally
        {
            ViewModel.IsEditorDialogOpen = false;
        }
    }

    // The MenuItem's DataContext is the node the context menu was opened on
    // (inherited from PlacementTarget).
    private static CategoryNodeViewModel? GetNodeFromMenuItem(object sender)
    {
        return sender is MenuItem { DataContext: CategoryNodeViewModel node }
            ? node
            : null;
    }

    // A node's siblings in the already-displayed tree (roots if Depth == 0,
    // otherwise its parent's Children) — used to enable/disable "Monter"/"Descendre".
    private List<CategoryNodeViewModel> FindSiblingNodes(CategoryNodeViewModel node)
    {
        if (node.Depth == 0)
        {
            return ViewModel.CategoryTree.Skip(2).ToList();
        }

        var parent = FindParentNode(ViewModel.CategoryTree, node);
        return parent is null ? [] : parent.Children.ToList();
    }

    private static CategoryNodeViewModel? FindParentNode(
        IEnumerable<CategoryNodeViewModel> candidates,
        CategoryNodeViewModel target)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.Children.Contains(target))
            {
                return candidate;
            }

            if (FindParentNode(candidate.Children, target) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // Virtual nodes ("Toutes les entrées", "Sans catégorie") aren't
    // categories: no context menu for them. For real ones, CanMoveUp/
    // CanMoveDown are recomputed here from the node's current position among
    // its siblings in the already-displayed tree (already in the right
    // order — no need to re-read CategoryOrderStore here).
    private void CategoryNode_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CategoryNodeViewModel node })
        {
            return;
        }

        if (node.IsVirtual)
        {
            e.Handled = true;
            return;
        }

        var siblings = FindSiblingNodes(node);
        var index = siblings.IndexOf(node);
        node.CanMoveUp = index > 0;
        node.CanMoveDown = index >= 0 && index < siblings.Count - 1;
    }

    // Handles the click (selection + expand) ourselves to avoid conflicting
    // with TreeViewItem's native double-click behavior; walks up to the
    // TreeViewItem from the click point to match its selection highlight,
    // which is wider than the Grid.
    private void CategoryNode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element is not null)
        {
            if (element is ToggleButton)
            {
                // The expand arrow already handles its own open/close.
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

    // Wired to PreviewKeyDown (not KeyDown): the TreeView's native keyboard
    // navigation already consumes Up/Down in its own KeyDown handling before
    // the event would bubble up here, so Ctrl+Up/Ctrl+Down would never reach
    // a bubbling handler. Tunneling intercepts them before that native
    // navigation runs.
    private void CategoryTreeView_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2 &&
            ViewModel.SelectedCategoryNode is { IsVirtual: false } node)
        {
            node.BeginEdit();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control ||
            ViewModel.SelectedCategoryNode is not { IsVirtual: false, Category: not null } selectedNode)
        {
            return;
        }

        if (e.Key == Key.Up)
        {
            ViewModel.MoveCategoryUp(selectedNode.Category.Id);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            ViewModel.MoveCategoryDown(selectedNode.Category.Id);
            e.Handled = true;
        }
    }

    // ─── Inline rename ───

    // Loaded only fires once (when the container is created), well before
    // the first BeginEdit() — so this reacts to Visible on every edit instead.
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
            // On focus loss, try to commit but never block the user with an
            // error — an invalid name is simply discarded.
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

        // IsEditing flips back to false before calling the ViewModel, so the
        // LostFocus triggered by hiding the TextBox doesn't re-commit a
        // second time.
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
