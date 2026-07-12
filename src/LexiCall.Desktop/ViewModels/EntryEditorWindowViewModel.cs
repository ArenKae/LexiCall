// ViewModel du formulaire d'ajout/modification d'une entrée.
// La fenêtre renvoie une SavedEntry validée au MainWindowViewModel, qui décidera
// ensuite de l'ajouter ou de remplacer l'entrée existante.
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Commands;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed class EntryEditorWindowViewModel : INotifyPropertyChanged
{
    private readonly VocabularyEntry? _existingEntry;
    private string _word = string.Empty;
    private string _definition = string.Empty;
    private string _synonymsText = string.Empty;
    private string _exampleSentencesText = string.Empty;
    private string _notes = string.Empty;
    private string _source = string.Empty;
    private string _tagsText = string.Empty;
    private string _errorMessage = string.Empty;

    public EntryEditorWindowViewModel(
        VocabularyEntry? existingEntry = null,
        IEnumerable<VocabularyCategory>? availableCategories = null)
    {
        _existingEntry = existingEntry;
        SaveEntryCommand = new RelayCommand(SaveEntry);

        // Les catégories sont optionnelles : aucune case cochée produit une entrée
        // valide avec CategoryIds vide.
        CategorySelections = new ObservableCollection<CategorySelectionViewModel>(
            (availableCategories ?? [])
            .OrderBy(category => category.Name)
            .Select(category => new CategorySelectionViewModel(
                category,
                existingEntry?.CategoryIds.Contains(category.Id) == true)));

        if (existingEntry is not null)
        {
            Word = existingEntry.Word;
            Definition = existingEntry.Definition;
            SynonymsText = TextListParser.FormatCommaSeparatedText(existingEntry.Synonyms);
            ExampleSentencesText = TextListParser.FormatLineSeparatedText(existingEntry.ExampleSentences);
            Notes = existingEntry.Notes;
            Source = existingEntry.Source;
            TagsText = TextListParser.FormatCommaSeparatedText(existingEntry.Tags);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? EntrySaved;

    public RelayCommand SaveEntryCommand { get; }

    public ObservableCollection<CategorySelectionViewModel> CategorySelections { get; }

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

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    private void SaveEntry()
    {
        var word = Word.Trim();
        var definition = Definition.Trim();

        // Validation minimale pour rester productif : seules les données
        // nécessaires à l'existence d'une entrée sont obligatoires.
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
            // En édition, on conserve l'identité et la date de création.
            // En ajout, on génère un nouvel Id.
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
