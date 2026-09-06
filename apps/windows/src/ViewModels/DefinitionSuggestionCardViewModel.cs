// Définition review card in EnrichmentReviewWindow. Unlike the text-shaped
// cards (Synonymes/Exemples) it edits one row per sense, and carries the
// "Reformuler" action unique to this field: POST /enrichment/rephrase-definition
// rephrases a single sense, so an unlocked sense gets its own call rather than
// the whole block being sent at once and fused.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Commands;
using LexiCall.Desktop.Services;

namespace LexiCall.Desktop.ViewModels;

public sealed class DefinitionSuggestionCardViewModel : INotifyPropertyChanged
{
    private readonly string _word;
    private readonly VocabularyApiClient _apiClient;
    private bool _isAccepted = true;
    private bool _isRephrasing;
    private string _rephraseErrorMessage = string.Empty;

    public DefinitionSuggestionCardViewModel(
        string? currentValueDisplay,
        string? justification,
        IEnumerable<string> suggestedSenses,
        string word,
        VocabularyApiClient apiClient)
    {
        CurrentValueDisplay = currentValueDisplay;
        Justification = justification;
        Senses = new DefinitionSenseListViewModel(suggestedSenses);
        _word = word;
        _apiClient = apiClient;
        RephraseCommand = new RelayCommand(async () => await RephraseAsync());

        Senses.SenseChanged += (_, _) => OnPropertyChanged(nameof(CanRephrase));
        Senses.Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanRephrase));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DefinitionSenseListViewModel Senses { get; }

    // Null when the field was empty — nothing to compare against.
    public string? CurrentValueDisplay { get; }

    public bool HasCurrentValue => !string.IsNullOrEmpty(CurrentValueDisplay);

    public string? Justification { get; }

    public bool HasJustification => !string.IsNullOrEmpty(Justification);

    // Pre-checked: unchecking is how the user rejects just this field.
    public bool IsAccepted
    {
        get => _isAccepted;
        set => SetProperty(ref _isAccepted, value);
    }

    public RelayCommand RephraseCommand { get; }

    public bool IsRephrasing
    {
        get => _isRephrasing;
        private set
        {
            if (SetProperty(ref _isRephrasing, value))
            {
                OnPropertyChanged(nameof(CanRephrase));
            }
        }
    }

    public bool CanRephrase => !IsRephrasing && RephraseTargets().Count > 0;

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

    public List<string> ToSenseList() => Senses.ToSenseList();

    private List<DefinitionSenseViewModel> RephraseTargets()
    {
        return Senses.Items
            .Where(sense => !sense.IsRephraseLocked && !string.IsNullOrWhiteSpace(sense.Text))
            .ToList();
    }

    private async Task RephraseAsync()
    {
        var targets = RephraseTargets();

        if (targets.Count == 0)
        {
            return;
        }

        RephraseErrorMessage = string.Empty;
        IsRephrasing = true;

        var results = await Task.WhenAll(targets.Select(async sense =>
        {
            var anchor = sense.RephraseAnchor ?? sense.Text;
            sense.RephraseAnchor = anchor;

            var (status, result, errorDetail) = await _apiClient.TryRephraseDefinitionAsync(
                new RephraseDefinitionRequest(_word, anchor));

            return (Sense: sense, Status: status, Result: result, ErrorDetail: errorDetail);
        }));

        IsRephrasing = false;

        foreach (var (sense, status, result, _) in results)
        {
            if (status == RephraseDefinitionStatus.Ok && result is not null)
            {
                sense.Text = result.Definition;
            }
        }

        // A sense whose call failed simply keeps its text; the message says so.
        var failure = results.FirstOrDefault(item => item.Status != RephraseDefinitionStatus.Ok);

        if (failure.Sense is not null)
        {
            RephraseErrorMessage = failure.Status == RephraseDefinitionStatus.NotConfigured
                ? "La reformulation nécessite une synchronisation API configurée (voir Options)."
                : string.IsNullOrWhiteSpace(failure.ErrorDetail)
                    ? "Impossible de reformuler pour le moment. Réessaie plus tard."
                    : $"Impossible de reformuler : {failure.ErrorDetail}";
        }
    }

    private bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
