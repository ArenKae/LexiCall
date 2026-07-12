using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;

namespace LexiCall.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly VocabularyRepository _repository;
    private string _searchQuery = string.Empty;
    private string _searchStatusText = string.Empty;
    private VocabularyEntry? _selectedEntry;

    public MainWindowViewModel(VocabularyRepository? repository = null)
    {
        _repository = repository ?? new VocabularyRepository();

        var entries = _repository.DataFileExists
            ? _repository.LoadEntries()
            : CreateSampleEntries();

        Entries = new ObservableCollection<VocabularyEntry>(entries);
        FilteredEntries = [];
        RefreshFilteredEntries();
        SelectedEntry = FilteredEntries.FirstOrDefault();

        if (!_repository.DataFileExists)
        {
            SaveEntries();
        }
    }

    public ObservableCollection<VocabularyEntry> Entries { get; }

    public ObservableCollection<VocabularyEntry> FilteredEntries { get; }

    public string DataFilePath => _repository.FilePath;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                RefreshFilteredEntries();
            }
        }
    }

    public string SearchStatusText
    {
        get => _searchStatusText;
        private set => SetProperty(ref _searchStatusText, value);
    }

    public bool HasEntries => Entries.Count > 0;

    public bool HasFilteredEntries => FilteredEntries.Count > 0;

    public bool HasSelectedEntry => SelectedEntry is not null;

    public string EmptyListMessage => HasEntries
        ? "Aucun résultat pour cette recherche."
        : "Aucun mot pour l’instant. Clique sur « Ajouter un mot » pour commencer.";

    public string EmptyDetailMessage
    {
        get
        {
            if (!HasEntries)
            {
                return "Ajoute ton premier mot pour commencer à construire ton vocabulaire.";
            }

            return HasFilteredEntries
                ? "Sélectionne un mot dans la liste pour afficher ses détails."
                : "Aucun mot ne correspond à cette recherche.";
        }
    }

    public VocabularyEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                OnPropertyChanged(nameof(HasSelectedEntry));
                OnPropertyChanged(nameof(EmptyDetailMessage));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AddEntry(VocabularyEntry entry)
    {
        Entries.Insert(0, entry);
        OnEntriesChanged();
        SearchQuery = string.Empty;
        RefreshFilteredEntries();
        SelectedEntry = entry;
        SaveEntries();
    }

    public void UpdateEntry(VocabularyEntry updatedEntry)
    {
        var index = FindEntryIndex(updatedEntry.Id);

        if (index < 0)
        {
            return;
        }

        Entries[index] = updatedEntry;
        RefreshFilteredEntries();

        if (FilteredEntries.Any(entry => entry.Id == updatedEntry.Id))
        {
            SelectedEntry = updatedEntry;
        }

        SaveEntries();
    }

    public void DeleteEntry(VocabularyEntry entry)
    {
        var index = FindEntryIndex(entry.Id);

        if (index < 0)
        {
            return;
        }

        Entries.RemoveAt(index);
        OnEntriesChanged();
        RefreshFilteredEntries();
        SelectedEntry = FilteredEntries.Count == 0
            ? null
            : FilteredEntries[Math.Min(index, FilteredEntries.Count - 1)];
        SaveEntries();
    }

    private static IReadOnlyList<VocabularyEntry> CreateSampleEntries()
    {
        return
        [
            new VocabularyEntry
            {
                Word = "Éphémère",
                Definition = "Qui ne dure qu'un temps très court.",
                Synonyms = { "passager", "fugace", "momentané" },
                ExampleSentences =
                {
                    "La beauté éphémère des cerisiers annonce le printemps."
                },
                Notes = "À rapprocher de fugace, qui insiste sur la rapidité.",
                Source = "Le Petit Prince — Antoine de Saint-Exupéry",
                Categories = { "Durée", "Littérature" },
                Tags = { "adjectif" }
            },
            new VocabularyEntry
            {
                Word = "Perspicace",
                Definition = "Qui comprend rapidement et avec justesse.",
                Synonyms = { "clairvoyant", "sagace", "lucide" },
                ExampleSentences =
                {
                    "Son observation perspicace révéla un détail ignoré de tous."
                },
                Source = "Lecture personnelle",
                Categories = { "Caractère" },
                Tags = { "adjectif" }
            },
            new VocabularyEntry
            {
                Word = "Liminal",
                Definition = "Qui se situe à la limite d'un seuil ou entre deux états.",
                Synonyms = { "intermédiaire", "transitoire" },
                ExampleSentences =
                {
                    "Le personnage traverse un espace liminal entre rêve et réalité."
                },
                Notes = "Terme fréquent en anthropologie et en critique littéraire.",
                Source = "Essai sur les espaces de transition",
                Categories = { "Philosophie", "Littérature" },
                Tags = { "adjectif", "concept" }
            }
        ];
    }

    private void SaveEntries()
    {
        _repository.SaveEntries(Entries);
    }

    private void OnEntriesChanged()
    {
        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(EmptyListMessage));
        OnPropertyChanged(nameof(EmptyDetailMessage));
    }

    private void RefreshFilteredEntries()
    {
        var selectedEntryId = SelectedEntry?.Id;
        var matchingEntries = Entries
            .Where(EntryMatchesSearch)
            .ToList();

        FilteredEntries.Clear();

        foreach (var entry in matchingEntries)
        {
            FilteredEntries.Add(entry);
        }

        SearchStatusText = BuildSearchStatusText();
        OnPropertyChanged(nameof(HasFilteredEntries));
        OnPropertyChanged(nameof(EmptyListMessage));
        OnPropertyChanged(nameof(EmptyDetailMessage));

        if (selectedEntryId is not null &&
            FilteredEntries.Any(entry => entry.Id == selectedEntryId))
        {
            return;
        }

        SelectedEntry = FilteredEntries.FirstOrDefault();
    }

    private string BuildSearchStatusText()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return $"{Entries.Count} mot(s)";
        }

        return FilteredEntries.Count == 0
            ? "Aucun résultat"
            : $"{FilteredEntries.Count} résultat(s)";
    }

    private bool EntryMatchesSearch(VocabularyEntry entry)
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return true;
        }

        var normalizedQuery = NormalizeForSearch(SearchQuery);

        return SearchFieldMatches(entry.Word, normalizedQuery) ||
            SearchFieldMatches(entry.Definition, normalizedQuery) ||
            SearchFieldMatches(entry.Notes, normalizedQuery) ||
            SearchFieldMatches(entry.Source, normalizedQuery) ||
            SearchFieldsMatch(entry.Synonyms, normalizedQuery) ||
            SearchFieldsMatch(entry.ExampleSentences, normalizedQuery) ||
            SearchFieldsMatch(entry.Categories, normalizedQuery) ||
            SearchFieldsMatch(entry.Tags, normalizedQuery);
    }

    private static bool SearchFieldsMatch(IEnumerable<string> values, string normalizedQuery)
    {
        return values.Any(value => SearchFieldMatches(value, normalizedQuery));
    }

    private static bool SearchFieldMatches(string value, string normalizedQuery)
    {
        return NormalizeForSearch(value)
            .Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeForSearch(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    private int FindEntryIndex(Guid entryId)
    {
        for (var index = 0; index < Entries.Count; index++)
        {
            if (Entries[index].Id == entryId)
            {
                return index;
            }
        }

        return -1;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
}
