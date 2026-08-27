// ViewModel for the AI enrichment review dialog — the single review
// mechanism in the app, reused from two places: the Détails card
// (MainWindow, an already-saved entry) and the "Enrichir" button in
// EntryEditorWindow (a draft, possibly not saved yet). It knows nothing about
// either caller: it takes the current field values (for the before/after
// comparison) and the suggestions, and only ever exposes a Result — it never
// persists anything itself, that's each caller's own responsibility.
using LexiCall.Desktop.Commands;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed record EnrichmentReviewResult(
    string? Definition,
    VocabularyEntryType? Type,
    List<string>? Synonyms,
    List<string>? ExampleSentences);

public sealed class EnrichmentReviewWindowViewModel
{
    public EnrichmentReviewWindowViewModel(
        string currentDefinition,
        VocabularyEntryType currentType,
        IReadOnlyList<string> currentSynonyms,
        IReadOnlyList<string> currentExampleSentences,
        EntryEnrichmentSuggestions suggestions)
    {
        SaveCommand = new RelayCommand(Save);

        if (suggestions.Definition is { } definitionSuggestion)
        {
            DefinitionCard = new TextFieldSuggestionCardViewModel(
                "Définition",
                string.IsNullOrEmpty(currentDefinition) ? null : currentDefinition,
                definitionSuggestion.Justification,
                definitionSuggestion.Value);
        }

        if (suggestions.Type is { } typeSuggestion)
        {
            TypeCard = new TypeSuggestionCardViewModel(
                currentType == VocabularyEntryType.Undefined ? null : VocabularyEntryTypeCatalog.GetLabel(currentType),
                typeSuggestion.Justification,
                typeSuggestion.Value);
        }

        if (suggestions.Synonyms is { } synonymsSuggestion)
        {
            SynonymsCard = new TextFieldSuggestionCardViewModel(
                "Synonymes",
                currentSynonyms.Count == 0 ? null : TextListParser.FormatCommaSeparatedText(currentSynonyms.ToList()),
                synonymsSuggestion.Justification,
                TextListParser.FormatCommaSeparatedText(synonymsSuggestion.Value));
        }

        if (suggestions.ExampleSentences is { } exampleSentencesSuggestion)
        {
            ExampleSentencesCard = new TextFieldSuggestionCardViewModel(
                "Exemples",
                currentExampleSentences.Count == 0 ? null : TextListParser.FormatLineSeparatedText(currentExampleSentences.ToList()),
                exampleSentencesSuggestion.Justification,
                TextListParser.FormatLineSeparatedText(exampleSentencesSuggestion.Value));
        }

        HasAnySuggestion = DefinitionCard is not null || TypeCard is not null
            || SynonymsCard is not null || ExampleSentencesCard is not null;
    }

    public event EventHandler? Saved;

    public RelayCommand SaveCommand { get; }

    public TextFieldSuggestionCardViewModel? DefinitionCard { get; }

    public TypeSuggestionCardViewModel? TypeCard { get; }

    public TextFieldSuggestionCardViewModel? SynonymsCard { get; }

    public TextFieldSuggestionCardViewModel? ExampleSentencesCard { get; }

    public bool HasAnySuggestion { get; }

    public EnrichmentReviewResult? Result { get; private set; }

    private void Save()
    {
        Result = new EnrichmentReviewResult(
            DefinitionCard is { IsAccepted: true } definitionCard ? definitionCard.EditableText.Trim() : null,
            TypeCard is { IsAccepted: true } typeCard ? typeCard.SelectedType?.Value : null,
            SynonymsCard is { IsAccepted: true } synonymsCard ? TextListParser.ParseCommaSeparatedText(synonymsCard.EditableText) : null,
            ExampleSentencesCard is { IsAccepted: true } exampleSentencesCard ? TextListParser.ParseLineSeparatedText(exampleSentencesCard.EditableText) : null);

        Saved?.Invoke(this, EventArgs.Empty);
    }
}
