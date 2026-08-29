// Définition-only review card: same text-suggestion display/edit as
// TextFieldSuggestionCardViewModel, plus a "Reformuler" action unique to this
// field (see VocabularyApiClient.TryRephraseDefinitionAsync). Kept as a
// subclass rather than adding this state to the shared base, since
// Synonymes/Exemples have no equivalent and no access to Word/the API client.
using LexiCall.Desktop.Commands;
using LexiCall.Desktop.Services;

namespace LexiCall.Desktop.ViewModels;

public sealed class DefinitionSuggestionCardViewModel : TextFieldSuggestionCardViewModel
{
    private readonly string _word;
    private readonly VocabularyApiClient _apiClient;

    // Captured on the first "Reformuler" click, then reused for every
    // subsequent call — never EditableText directly, which would drift
    // further from the original meaning with each successive rephrase.
    private string? _rephraseAnchor;
    private bool _isRephrasing;
    private string _rephraseErrorMessage = string.Empty;

    public DefinitionSuggestionCardViewModel(
        string? currentValueDisplay,
        string? justification,
        string suggestedValue,
        string word,
        VocabularyApiClient apiClient)
        : base("Définition", currentValueDisplay, justification, suggestedValue)
    {
        _word = word;
        _apiClient = apiClient;
        RephraseCommand = new RelayCommand(async () => await RephraseAsync());
    }

    public RelayCommand RephraseCommand { get; }

    public bool IsRephrasing
    {
        get => _isRephrasing;
        private set => SetProperty(ref _isRephrasing, value);
    }

    public string RephraseErrorMessage
    {
        get => _rephraseErrorMessage;
        private set
        {
            if (SetProperty(ref _rephraseErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasRephraseError));
            }
        }
    }

    public bool HasRephraseError => !string.IsNullOrEmpty(RephraseErrorMessage);

    private async Task RephraseAsync()
    {
        _rephraseAnchor ??= EditableText;

        RephraseErrorMessage = string.Empty;
        IsRephrasing = true;

        var (status, result, errorDetail) = await _apiClient.TryRephraseDefinitionAsync(
            new RephraseDefinitionRequest(_word, _rephraseAnchor));

        IsRephrasing = false;

        switch (status)
        {
            case RephraseDefinitionStatus.Ok when result is not null:
                EditableText = result.Definition;
                break;
            case RephraseDefinitionStatus.NotConfigured:
                RephraseErrorMessage = "La reformulation nécessite une synchronisation API configurée (voir Options).";
                break;
            default:
                RephraseErrorMessage = string.IsNullOrWhiteSpace(errorDetail)
                    ? "Impossible de reformuler pour le moment. Réessaie plus tard."
                    : $"Impossible de reformuler : {errorDetail}";
                break;
        }
    }
}
