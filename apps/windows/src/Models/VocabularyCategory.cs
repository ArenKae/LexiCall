// Représente une catégorie de vocabulaire.
// Les entrées référencent les catégories par Id pour permettre de renommer une
// catégorie sans modifier toutes les entrées qui l'utilisent.
namespace LexiCall.Desktop.Models;

public sealed class VocabularyCategory
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; set; }

    public Guid? ParentId { get; set; }

    public string Description { get; set; } = string.Empty;

    // Emoji choisi via IconPickerWindow. Vide = aucune icône (un repère par
    // défaut est affiché à la place, voir CategoryNodeViewModel.DisplayIcon).
    public string IconGlyph { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    // Voir VocabularyEntry.IsDeleted / SyncedAt — même rôle, pour les
    // catégories.
    public bool IsDeleted { get; init; }

    public DateTimeOffset? SyncedAt { get; set; }
}
