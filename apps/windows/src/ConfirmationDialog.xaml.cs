// Themed Yes/No confirmation dialog, used instead of MessageBox for
// deletions (entry, category) — see ConfirmationDialog.xaml.
using System.Windows;
using LexiCall.Desktop.Services;

namespace LexiCall.Desktop;

public partial class ConfirmationDialog : Window
{
    private ConfirmationDialog(Window owner, string message, string title, string confirmText)
    {
        InitializeComponent();
        Owner = owner;
        Title = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        ThemeService.RegisterWindow(this);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    public static bool Show(Window owner, string message, string title, string confirmText = "Supprimer")
    {
        return new ConfirmationDialog(owner, message, title, confirmText).ShowDialog() == true;
    }
}
