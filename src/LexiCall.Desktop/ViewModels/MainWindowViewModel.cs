using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;

namespace LexiCall.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly VocabularyRepository _repository;
    private VocabularyEntry? _selectedEntry;

    public MainWindowViewModel(VocabularyRepository? repository = null)
    {
        _repository = repository ?? new VocabularyRepository();

        var entries = _repository.DataFileExists
            ? _repository.LoadEntries()
            : CreateSampleEntries();

        Entries = new ObservableCollection<VocabularyEntry>(entries);
        SelectedEntry = Entries.FirstOrDefault();

        if (!_repository.DataFileExists)
        {
            SaveEntries();
        }
    }

    public ObservableCollection<VocabularyEntry> Entries { get; }

    public string DataFilePath => _repository.FilePath;

    public VocabularyEntry? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AddEntry(VocabularyEntry entry)
    {
        Entries.Insert(0, entry);
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
        SelectedEntry = updatedEntry;
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
        SelectedEntry = Entries.Count == 0
            ? null
            : Entries[Math.Min(index, Entries.Count - 1)];
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
