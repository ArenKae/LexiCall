// A vocabulary category. Entries reference categories by Id so a category can
// be renamed without touching every entry that uses it.
namespace LexiCall.Desktop.Models;

public sealed class VocabularyCategory
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; set; }

    public Guid? ParentId { get; set; }

    public string Description { get; set; } = string.Empty;

    // Emoji picked via IconPickerWindow. Empty = no icon (a default glyph is
    // shown instead, see CategoryNodeViewModel.DisplayIcon).
    public string IconGlyph { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    // See VocabularyEntry.IsDeleted / SyncedAt — same role, for categories.
    public bool IsDeleted { get; init; }

    public DateTimeOffset? SyncedAt { get; set; }
}
