using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Commands;
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.ViewModels;

public sealed class AddEntryWindowViewModel : INotifyPropertyChanged
{
    private readonly VocabularyEntry? _existingEntry;
    private string _word = string.Empty;
    private string _definition = string.Empty;
    private string _synonymsText = string.Empty;
    private string _exampleSentencesText = string.Empty;
    private string _notes = string.Empty;
    private string _source = string.Empty;
    private string _categoriesText = string.Empty;
    private string _tagsText = string.Empty;
    private string _errorMessage = string.Empty;

    public AddEntryWindowViewModel(VocabularyEntry? existingEntry = null)
    {
        _existingEntry = existingEntry;
        SaveEntryCommand = new RelayCommand(SaveEntry);

        if (existingEntry is not null)
        {
            Word = existingEntry.Word;
            Definition = existingEntry.Definition;
            SynonymsText = string.Join(", ", existingEntry.Synonyms);
            ExampleSentencesText = string.Join(Environment.NewLine, existingEntry.ExampleSentences);
            Notes = existingEntry.Notes;
            Source = existingEntry.Source;
            CategoriesText = string.Join(", ", existingEntry.Categories);
            TagsText = string.Join(", ", existingEntry.Tags);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? EntrySaved;

    public RelayCommand SaveEntryCommand { get; }

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

    public string CategoriesText
    {
        get => _categoriesText;
        set => SetProperty(ref _categoriesText, value);
    }

    public string TagsText
    {
        get => _tagsText;
        set => SetProperty(ref _tagsText, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    private void SaveEntry()
    {
        var word = Word.Trim();
        var definition = Definition.Trim();

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
            Id = _existingEntry?.Id ?? Guid.NewGuid(),
            Word = word,
            Definition = definition,
            Synonyms = ParseCommaSeparatedText(SynonymsText).ToList(),
            ExampleSentences = ParseLineSeparatedText(ExampleSentencesText).ToList(),
            Notes = Notes.Trim(),
            Source = Source.Trim(),
            Categories = ParseCommaSeparatedText(CategoriesText).ToList(),
            Tags = ParseCommaSeparatedText(TagsText).ToList(),
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

    private static IEnumerable<string> ParseCommaSeparatedText(string value)
    {
        return value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0);
    }

    private static IEnumerable<string> ParseLineSeparatedText(string value)
    {
        return value
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0);
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
