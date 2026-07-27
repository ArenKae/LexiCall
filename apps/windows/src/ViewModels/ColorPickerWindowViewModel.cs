// ViewModel du sélecteur de couleur de catégorie : une palette fixe
// (CategoryColorPalette) plus une option "Automatique" qui efface le choix
// manuel. Même principe que IconPickerWindowViewModel (ColorSelected au clic).
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed class ColorPickerWindowViewModel
{
    public ColorPickerWindowViewModel(string? currentColorHex)
    {
        SelectedColorHex = string.IsNullOrEmpty(currentColorHex) ? null : currentColorHex;
    }

    public event EventHandler? ColorSelected;

    public IReadOnlyList<string> Swatches => CategoryColorPalette.Swatches;

    public string? SelectedColorHex { get; private set; }

    // colorHex null = "Automatique" (efface l'override).
    public void SelectColor(string? colorHex)
    {
        SelectedColorHex = colorHex;
        ColorSelected?.Invoke(this, EventArgs.Empty);
    }
}
