// Petite implémentation réutilisable de ICommand.
// Elle permet de connecter un bouton XAML à une méthode du ViewModel sans écrire
// de logique métier directement dans le code-behind de la fenêtre.
using System.Windows.Input;

namespace LexiCall.Desktop.Commands;

public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return canExecute?.Invoke() ?? true;
    }

    public void Execute(object? parameter)
    {
        execute();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
