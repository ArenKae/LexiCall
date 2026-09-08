// Dismissal and placement behaviour for the checkable-dropdown Popups (the
// Type selector). A Popup is its own top-level window: it neither closes by
// itself when the user clicks elsewhere in the owning window, nor follows its
// placement target when the surrounding ScrollViewer scrolls. Both are
// handled here.
//
// Deliberately not done with Mouse.Capture: any interactive control inside
// the popup (a CheckBox) takes capture on click and drops it on release, so
// the popup would silently stop noticing outside clicks after the first tick.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LexiCall.Desktop.Utilities;

public static class ClickAwayPopup
{
    public static void Register(Popup popup)
    {
        var attached = false;

        // Wired on first open, not at registration: the popup's placement
        // target and owning window are only reliably resolved by then.
        // Everything stays attached afterwards, guarded by IsOpen.
        popup.Opened += (_, _) =>
        {
            if (attached)
            {
                return;
            }

            attached = true;

            if (Window.GetWindow(popup) is { } window)
            {
                window.AddHandler(
                    UIElement.PreviewMouseDownEvent,
                    new MouseButtonEventHandler((_, e) => CloseIfOutside(popup, e)),
                    handledEventsToo: true);

                window.Deactivated += (_, _) => popup.IsOpen = false;
            }

            if (FindAncestorScrollViewer(popup.PlacementTarget) is { } scrollViewer)
            {
                scrollViewer.ScrollChanged += (_, e) =>
                {
                    if (popup.IsOpen && (e.VerticalChange != 0 || e.HorizontalChange != 0))
                    {
                        Reposition(popup);
                    }
                };
            }
        };
    }

    // A Popup is placed once when it opens and never tracks its placement
    // target afterwards, so scrolling the form leaves it stranded. Nudging an
    // offset and putting it straight back is what forces WPF to recompute the
    // placement against the target's new position.
    private static void Reposition(Popup popup)
    {
        var offset = popup.HorizontalOffset;
        popup.HorizontalOffset = offset + 1;
        popup.HorizontalOffset = offset;
    }

    private static void CloseIfOutside(Popup popup, MouseButtonEventArgs e)
    {
        if (!popup.IsOpen || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        // A click on the toggle is its own business: its IsChecked binding
        // already closes the popup, and closing it here as well would let the
        // very same click toggle it straight back open.
        if (IsWithin(source, popup.Child) || IsWithin(source, popup.PlacementTarget))
        {
            return;
        }

        popup.IsOpen = false;
    }

    private static bool IsWithin(DependencyObject source, DependencyObject? ancestor)
    {
        if (ancestor is null)
        {
            return false;
        }

        for (DependencyObject? node = source; node is not null; node = GetParent(node))
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static ScrollViewer? FindAncestorScrollViewer(DependencyObject? node)
    {
        for (; node is not null; node = GetParent(node))
        {
            if (node is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }
        }

        return null;
    }

    // Popup content sits in its own visual tree, so the walk has to fall back
    // to the logical parent to cross that boundary.
    private static DependencyObject? GetParent(DependencyObject node) =>
        node is Visual
            ? VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);
}
