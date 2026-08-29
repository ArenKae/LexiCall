// ViewModel for the add/edit entry form. The window hands a validated
// SavedEntry back to MainWindowViewModel, which decides whether to add it or
// replace the existing entry.
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Commands;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed class EntryEditorWindowViewModel : INotifyPropertyChanged
{
    public const int MaxImages = 4;

    private readonly VocabularyEntry? _existingEntry;
    private readonly VocabularyApiClient? _apiClient;
    private readonly HashSet<string> _lockedFields;
    private string _word = string.Empty;
    private string _definition = string.Empty;
    private string _synonymsText = string.Empty;
    private string _exampleSentencesText = string.Empty;
    private string _notes = string.Empty;
    private string _source = string.Empty;
    private VocabularyEntryType _type = VocabularyEntryType.Undefined;
    private bool _isArchived;
    private string _errorMessage = string.Empty;
    private bool _isEnrichingDraft;
    private string _enrichmentErrorMessage = string.Empty;

    public EntryEditorWindowViewModel(
        VocabularyEntry? existingEntry = null,
        IEnumerable<VocabularyCategory>? availableCategories = null,
        Guid? initialCategoryId = null,
        VocabularyApiClient? apiClient = null)
    {
        _existingEntry = existingEntry;
        _apiClient = apiClient;
        _lockedFields = new HashSet<string>(existingEntry?.LockedFields ?? []);
        SaveEntryCommand = new RelayCommand(SaveEntry);
        EnrichDraftCommand = new RelayCommand(async () => await EnrichDraftAsync());

        // Categories are optional (CategoryIds may stay empty). On creation,
        // initialCategoryId pre-checks the category selected in the tree.
        CategorySelections = new ObservableCollection<CategorySelectionViewModel>(
            CategoryHierarchy.Flatten((availableCategories ?? []).ToList(), CategoryOrderStore.LoadAll())
            .Select(item => new CategorySelectionViewModel(
                item.Category,
                existingEntry is not null
                    ? existingEntry.CategoryIds.Contains(item.Category.Id)
                    : item.Category.Id == initialCategoryId,
                item.Depth)));

        Images = new ObservableCollection<EntryImageEditorViewModel>(
            (existingEntry?.Images ?? [])
            .Select(image => new EntryImageEditorViewModel(image.Id, image.Caption, image.ImageBase64, RemoveImage)));
        Images.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanAddMoreImages));

        if (existingEntry is not null)
        {
            Word = existingEntry.Word;
            Definition = existingEntry.Definition;
            SynonymsText = TextListParser.FormatCommaSeparatedText(existingEntry.Synonyms);
            ExampleSentencesText = TextListParser.FormatLineSeparatedText(existingEntry.ExampleSentences);
            Notes = existingEntry.Notes;
            Source = existingEntry.Source;
            _type = existingEntry.Type;
            _isArchived = existingEntry.IsArchived;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? EntrySaved;

    // The ViewModel never opens EnrichmentReviewWindow itself (it knows
    // nothing about WPF) — it raises this once suggestions arrive, and
    // EntryEditorWindow's code-behind reads PendingEnrichmentSuggestions to
    // show the review dialog, then calls ApplyEnrichmentResult with what
    // came back.
    public event EventHandler? EnrichmentSuggestionsReady;

    public EntryEnrichmentSuggestions? PendingEnrichmentSuggestions { get; private set; }

    // Exposed so the window's code-behind can hand the same client to
    // EnrichmentReviewWindowViewModel (needed for its "Reformuler" action).
    public VocabularyApiClient? ApiClient => _apiClient;

    public RelayCommand SaveEntryCommand { get; }

    public RelayCommand EnrichDraftCommand { get; }

    public ObservableCollection<CategorySelectionViewModel> CategorySelections { get; }

    public ObservableCollection<EntryImageEditorViewModel> Images { get; }

    public bool CanAddMoreImages => Images.Count < MaxImages;

    public IReadOnlyList<VocabularyEntryTypeOption> AvailableTypes =>
        VocabularyEntryTypeCatalog.All;

    public bool HasAvailableCategories => CategorySelections.Count > 0;

    public VocabularyEntry? SavedEntry { get; private set; }

    public string WindowTitle => _existingEntry is null
        ? "Ajouter un mot"
        : "Modifier un mot";

    public string HeaderText => WindowTitle;

    public string DescriptionText => _existingEntry is null
        ? "Renseigne au minimum le mot et sa définition."
        : "Modifie les informations du mot sélectionné.";

    public string SaveButtonText => _existingEntry is null
        ? "Ajouter"
        : "Enregistrer";

    public string Word
    {
        get => _word;
        set
        {
            if (SetProperty(ref _word, value))
            {
                ClearError();
                OnPropertyChanged(nameof(CanEnrichDraft));
            }
        }
    }

    public string Definition
    {
        get => _definition;
        set
        {
            if (SetProperty(ref _definition, value))
            {
                ClearError();
            }
        }
    }

    public string SynonymsText
    {
        get => _synonymsText;
        set => SetProperty(ref _synonymsText, value);
    }

    public string ExampleSentencesText
    {
        get => _exampleSentencesText;
        set => SetProperty(ref _exampleSentencesText, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string Source
    {
        get => _source;
        set => SetProperty(ref _source, value);
    }

    public VocabularyEntryType Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value))
            {
                OnPropertyChanged(nameof(SelectedTypeOption));
            }
        }
    }

    // The ComboBox binds SelectedItem to this instead of SelectedValue: WPF's
    // SelectedValue/SelectedValuePath reflection-based lookup reliably picks
    // up a user's dropdown click but doesn't always repaint the closed box
    // when Type is set programmatically (e.g. ApplyEnrichmentResult) —
    // SelectedItem avoids that lookup path entirely.
    public VocabularyEntryTypeOption? SelectedTypeOption
    {
        get => AvailableTypes.FirstOrDefault(option => option.Value == Type);
        set => Type = value?.Value ?? VocabularyEntryType.Undefined;
    }

    public bool IsArchived
    {
        get => _isArchived;
        set => SetProperty(ref _isArchived, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsEnrichingDraft
    {
        get => _isEnrichingDraft;
        private set
        {
            if (SetProperty(ref _isEnrichingDraft, value))
            {
                OnPropertyChanged(nameof(CanEnrichDraft));
            }
        }
    }

    // Separate from ErrorMessage: that one sits at the very bottom of the
    // scrollable form (below Notes/Images), out of view from the "Enrichir
    // avec l'IA" button up near Mot — an enrichment failure needs its own
    // message right there, not one the user has to scroll down to notice.
    public string EnrichmentErrorMessage
    {
        get => _enrichmentErrorMessage;
        private set
        {
            if (SetProperty(ref _enrichmentErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasEnrichmentError));
            }
        }
    }

    public bool HasEnrichmentError => !string.IsNullOrEmpty(EnrichmentErrorMessage);

    public bool CanEnrichDraft =>
        !IsEnrichingDraft &&
        !string.IsNullOrWhiteSpace(Word) &&
        (_apiClient?.IsConfigured ?? false);

    // One toggle per field the AI enrichment pipeline can touch — checked
    // means excluded from the next enrichment call (see
    // VocabularyApiClient.EntryEnrichmentDraft.LockedFields), never that the
    // field is read-only in the form itself.
    public bool IsDefinitionLocked
    {
        get => _lockedFields.Contains("Definition");
        set => SetLockedField("Definition", value);
    }

    public bool IsTypeLocked
    {
        get => _lockedFields.Contains("Type");
        set => SetLockedField("Type", value);
    }

    public bool IsSynonymsLocked
    {
        get => _lockedFields.Contains("Synonyms");
        set => SetLockedField("Synonyms", value);
    }

    public bool IsExampleSentencesLocked
    {
        get => _lockedFields.Contains("ExampleSentences");
        set => SetLockedField("ExampleSentences", value);
    }

    private void SetLockedField(string field, bool locked)
    {
        if (locked ? !_lockedFields.Add(field) : !_lockedFields.Remove(field))
        {
            return;
        }

        OnPropertyChanged(field switch
        {
            "Definition" => nameof(IsDefinitionLocked),
            "Type" => nameof(IsTypeLocked),
            "Synonyms" => nameof(IsSynonymsLocked),
            _ => nameof(IsExampleSentencesLocked)
        });
    }

    // Resizing/compression delegated to ImageProcessor (no WPF dependency
    // here). Accepts more files than there's room for (e.g. a multi-select
    // dialog) and silently takes only as many as fit under MaxImages.
    public void AddImagesFromFiles(IEnumerable<string> filePaths)
    {
        var truncated = false;

        foreach (var filePath in filePaths)
        {
            if (Images.Count >= MaxImages)
            {
                truncated = true;
                break;
            }

            if (ImageProcessor.TryEncodeImage(filePath, out var base64Image, out var error))
            {
                Images.Add(new EntryImageEditorViewModel(Guid.NewGuid(), string.Empty, base64Image, RemoveImage));
                ClearError();
            }
            else
            {
                ErrorMessage = error;
            }
        }

        if (truncated)
        {
            ErrorMessage = $"Une entrée ne peut avoir que {MaxImages} images au maximum ; le reste a été ignoré.";
        }
    }

    private void RemoveImage(EntryImageEditorViewModel image)
    {
        Images.Remove(image);
    }

    // Plain frontend autocorrect, unrelated to the LLM call itself — just a
    // courtesy fix-up triggered alongside a successful enrichment response.
    private void CapitalizeWordFirstLetter()
    {
        if (Word.Length > 0 && !char.IsUpper(Word[0]))
        {
            Word = char.ToUpperInvariant(Word[0]) + Word[1..];
        }
    }

    // No ConfigureAwait(false): unlike MainWindowViewModel, this ViewModel has
    // no Dispatcher re-marshalling of its own — letting the default WPF
    // SynchronizationContext resume on the UI thread is the simplest option,
    // since the continuation below sets bound properties directly.
    private async Task EnrichDraftAsync()
    {
        var word = Word.Trim();
        if (string.IsNullOrWhiteSpace(word) || _apiClient is null)
        {
            return;
        }

        EnrichmentErrorMessage = string.Empty;
        IsEnrichingDraft = true;

        var draft = new EntryEnrichmentDraft(
            word,
            Definition.Trim(),
            Type,
            TextListParser.ParseCommaSeparatedText(SynonymsText),
            TextListParser.ParseLineSeparatedText(ExampleSentencesText),
            _lockedFields.ToList());

        var (status, suggestions, errorDetail) = await _apiClient.TrySuggestEntryEnrichmentAsync(draft);

        IsEnrichingDraft = false;

        switch (status)
        {
            case EntryEnrichmentStatus.Ok when suggestions is not null:
                if (suggestions.WordRecognized)
                {
                    // Plain frontend autocorrect, not actually part of the
                    // LLM response — applied alongside it just so the
                    // suggestion reads as one cohesive AI-reviewed result.
                    // Skipped when the word itself was rejected: nothing to
                    // "cohere" with in that case.
                    CapitalizeWordFirstLetter();
                }
                PendingEnrichmentSuggestions = suggestions;
                EnrichmentSuggestionsReady?.Invoke(this, EventArgs.Empty);
                break;
            case EntryEnrichmentStatus.NotConfigured:
                EnrichmentErrorMessage = "L'enrichissement IA nécessite une synchronisation API configurée (voir Options).";
                break;
            default:
                EnrichmentErrorMessage = string.IsNullOrWhiteSpace(errorDetail)
                    ? "Impossible d'obtenir des suggestions pour le moment. Réessaie plus tard."
                    : $"Impossible d'obtenir des suggestions : {errorDetail}";
                break;
        }
    }

    // Called by EntryEditorWindow's code-behind once the review dialog it
    // opened (in response to EnrichmentSuggestionsReady) is saved — only the
    // fields the user actually accepted are non-null. Never persists
    // anything itself: the user still has to click Ajouter/Enregistrer.
    public void ApplyEnrichmentResult(EnrichmentReviewResult result)
    {
        if (result.Definition is { } definition)
        {
            Definition = definition;
        }

        if (result.Type is { } type)
        {
            Type = type;
        }

        if (result.Synonyms is { } synonyms)
        {
            SynonymsText = TextListParser.FormatCommaSeparatedText(synonyms);
        }

        if (result.ExampleSentences is { } exampleSentences)
        {
            ExampleSentencesText = TextListParser.FormatLineSeparatedText(exampleSentences);
        }
    }

    private void SaveEntry()
    {
        var word = Word.Trim();
        var definition = Definition.Trim();

        // Only the word and definition are required.
        if (string.IsNullOrWhiteSpace(word))
        {
            ErrorMessage = "Le mot est obligatoire.";
            return;
        }

        if (string.IsNullOrWhiteSpace(definition))
        {
            ErrorMessage = "La définition est obligatoire.";
            return;
        }

        var now = DateTimeOffset.Now;
        SavedEntry = new VocabularyEntry
        {
            // Id/CreatedAt kept as-is when editing, generated when creating.
            Id = _existingEntry?.Id ?? Guid.NewGuid(),
            Word = word,
            Definition = definition,
            Synonyms = TextListParser.ParseCommaSeparatedText(SynonymsText),
            ExampleSentences = TextListParser.ParseLineSeparatedText(ExampleSentencesText),
            Notes = Notes.Trim(),
            Source = Source.Trim(),
            CategoryIds = CategorySelections
                .Where(category => category.IsSelected)
                .Select(category => category.CategoryId)
                .ToList(),
            Type = Type,
            IsArchived = IsArchived,
            LockedFields = _lockedFields.ToList(),
            Images = Images
                .Select(image => new EntryImage { Id = image.Id, Caption = image.Caption.Trim(), ImageBase64 = image.ImageBase64 })
                .ToList(),
            CreatedAt = _existingEntry?.CreatedAt ?? now,
            UpdatedAt = now
        };

        EntrySaved?.Invoke(this, EventArgs.Empty);
    }

    private void ClearError()
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ErrorMessage = string.Empty;
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
