// ViewModel for the category color picker: a fixed palette
// (CategoryColorPalette) plus an "Automatique" (Automatic) option that clears
// the manual override. Same pattern as IconPickerWindowViewModel
// (ColorSelected fires on click).
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

    // colorHex null = "Automatique" (clears the override).
    public void SelectColor(string? colorHex)
    {
        SelectedColorHex = colorHex;
        ColorSelected?.Invoke(this, EventArgs.Empty);
    }
}
