// ViewModel for the add/edit entry form. The window hands a validated
// SavedEntry back to MainWindowViewModel, which decides whether to add it or
// replace the existing entry.
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Commands;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed class EntryEditorWindowViewModel : INotifyPropertyChanged
{
    public const int MaxImages = 3;

    private readonly VocabularyEntry? _existingEntry;
    private string _word = string.Empty;
    private string _definition = string.Empty;
    private string _synonymsText = string.Empty;
    private string _exampleSentencesText = string.Empty;
    private string _notes = string.Empty;
    private string _source = string.Empty;
    private string _tagsText = string.Empty;
    private VocabularyEntryType _type = VocabularyEntryType.Undefined;
    private bool _isArchived;
    private string _errorMessage = string.Empty;

    public EntryEditorWindowViewModel(
        VocabularyEntry? existingEntry = null,
        IEnumerable<VocabularyCategory>? availableCategories = null,
        Guid? initialCategoryId = null)
    {
        _existingEntry = existingEntry;
        SaveEntryCommand = new RelayCommand(SaveEntry);

        // Categories are optional (CategoryIds may stay empty). On creation,
        // initialCategoryId pre-checks the category selected in the tree.
        CategorySelections = new ObservableCollection<CategorySelectionViewModel>(
            CategoryHierarchy.Flatten((availableCategories ?? []).ToList())
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
            TagsText = TextListParser.FormatCommaSeparatedText(existingEntry.Tags);
            _type = existingEntry.Type;
            _isArchived = existingEntry.IsArchived;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? EntrySaved;

    public RelayCommand SaveEntryCommand { get; }

    public ObservableCollection<CategorySelectionViewModel> CategorySelections { get; }

    public ObservableCollection<EntryImageEditorViewModel> Images { get; }

    public bool CanAddMoreImages => Images.Count < MaxImages;

    public IReadOnlyList<(VocabularyEntryType Value, string Label)> AvailableTypes =>
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

    public string TagsText
    {
        get => _tagsText;
        set => SetProperty(ref _tagsText, value);
    }

    public VocabularyEntryType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
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
            Tags = TextListParser.ParseCommaSeparatedText(TagsText),
            Type = Type,
            IsArchived = IsArchived,
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
