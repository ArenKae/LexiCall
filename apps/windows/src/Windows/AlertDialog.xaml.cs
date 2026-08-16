// Themed single-OK-button notice dialog, used instead of MessageBox for
// blocked-action notices (e.g. "category still in use") — see AlertDialog.xaml.
using System.Windows;
using LexiCall.Desktop.Services;

namespace LexiCall.Desktop.Windows;

public partial class AlertDialog : Window
{
    private AlertDialog(Window owner, string message, string title)
    {
        InitializeComponent();
        Owner = owner;
        Title = title;
        MessageText.Text = message;
        ThemeService.RegisterWindow(this);
    }

    public static void Show(Window owner, string message, string title)
    {
        new AlertDialog(owner, message, title).ShowDialog();
    }
}
