// ViewModel principal de l'application.
// Il expose les données bindées par MainWindow.xaml : l'arbre des catégories
// (navigation principale), la liste filtrée par catégorie + recherche, la
// sélection courante et les opérations CRUD sur les entrées et les catégories.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly VocabularyRepository _repository;
    private string _searchQuery = string.Empty;
    private string _searchStatusText = string.Empty;
    private VocabularyEntry? _selectedEntry;
    private CategoryNodeViewModel? _selectedCategoryNode;
    private HashSet<Guid>? _activeCategoryFilterIds;

    public MainWindowViewModel(VocabularyRepository? repository = null)
    {
        _repository = repository ?? new VocabularyRepository();

        var database = _repository.DataFileExists
            ? _repository.LoadDatabase()
            : CreateSampleDatabase();

        Entries = new ObservableCollection<VocabularyEntry>(database.Entries);
        Categories = new ObservableCollection<VocabularyCategory>(database.Categories);
        FilteredEntries = [];
        CategoryTree = [];
        RebuildCategoryTree();
        RefreshFilteredEntries();
        SelectedEntry = FilteredEntries.FirstOrDefault();

        if (!_repository.DataFileExists)
        {
            SaveDatabase();
        }
    }

    public ObservableCollection<VocabularyEntry> Entries { get; }

    public ObservableCollection<VocabularyCategory> Categories { get; }

    // La liste affichée par l'UI. On garde Entries comme source complète, puis on
    // reconstruit FilteredEntries à chaque recherche ou modification importante.
    public ObservableCollection<VocabularyEntry> FilteredEntries { get; }

    // Arbre latéral : nœuds virtuels ("Toutes", "Sans catégorie") puis les
    // catégories racines avec leurs sous-catégories.
    public ObservableCollection<CategoryNodeViewModel> CategoryTree { get; }

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

    public bool HasCategories => Categories.Count > 0;

    public CategoryNodeViewModel? SelectedCategoryNode => _selectedCategoryNode;

    // Fil d'ariane affiché au-dessus de la liste : nom du nœud virtuel, ou
    // chemin complet "Parent › Enfant" pour une catégorie.
    public string SelectedCategoryLabel
    {
        get
        {
            if (_selectedCategoryNode is null)
            {
                return "Toutes les entrées";
            }

            return _selectedCategoryNode.Category is null
                ? _selectedCategoryNode.DisplayName
                : GetCategoryPath(_selectedCategoryNode.Category);
        }
    }

    public IReadOnlyList<string> SelectedEntryCategoryNames => SelectedEntry is null
        ? []
        : GetCategoryNames(SelectedEntry.CategoryIds).ToList();

    public string EmptyListMessage
    {
        get
        {
            if (!HasEntries)
            {
                return "Aucun mot pour l’instant. Clique sur « Ajouter un mot » pour commencer.";
            }

            return string.IsNullOrWhiteSpace(SearchQuery)
                ? "Aucun mot dans cette catégorie."
                : "Aucun résultat pour cette recherche.";
        }
    }

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
                : "Aucun mot ne correspond à ce filtre.";
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
                OnPropertyChanged(nameof(SelectedEntryCategoryNames));
                OnPropertyChanged(nameof(EmptyDetailMessage));
            }
        }
    }

    public string ThemeToggleText => ThemeService.CurrentTheme == AppTheme.Dark
        ? "☀  Thème clair"
        : "🌙  Thème sombre";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ToggleTheme()
    {
        ThemeService.Toggle();
        OnPropertyChanged(nameof(ThemeToggleText));
    }

    public void AddEntry(VocabularyEntry entry)
    {
        Entries.Insert(0, entry);
        OnEntriesChanged();

        // On réinitialise les filtres pour que le mot ajouté soit toujours visible.
        _searchQuery = string.Empty;
        OnPropertyChanged(nameof(SearchQuery));
        _selectedCategoryNode = null;

        RebuildCategoryTree();
        RefreshFilteredEntries();
        SelectedEntry = entry;
        SaveDatabase();
    }

    public void UpdateEntry(VocabularyEntry updatedEntry)
    {
        var index = FindEntryIndex(updatedEntry.Id);

        if (index < 0)
        {
            return;
        }

        Entries[index] = updatedEntry;
        RebuildCategoryTree();
        RefreshFilteredEntries();

        if (FilteredEntries.Any(entry => entry.Id == updatedEntry.Id))
        {
            SelectedEntry = updatedEntry;
        }

        SaveDatabase();
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
        RebuildCategoryTree();
        RefreshFilteredEntries();
        SelectedEntry = FilteredEntries.Count == 0
            ? null
            : FilteredEntries[Math.Min(index, FilteredEntries.Count - 1)];
        SaveDatabase();
    }

    // Ajout ou remplacement d'une catégorie validée par CategoryEditorWindow.
    // Retourne un message d'erreur, ou null si l'opération a réussi.
    public string? SaveCategory(VocabularyCategory category)
    {
        // Garde-fou anti-cycle : la fenêtre d'édition exclut déjà les descendants
        // du sélecteur de parent, mais une incohérence ne doit jamais être persistée.
        if (category.ParentId is Guid parentId &&
            (parentId == category.Id ||
             CategoryHierarchy.GetDescendantIds(Categories, category.Id).Contains(parentId)))
        {
            return "Le parent choisi créerait un cycle dans la hiérarchie.";
        }

        var index = FindCategoryIndex(category.Id);

        if (index < 0)
        {
            Categories.Add(category);
        }
        else
        {
            Categories[index] = category;
        }

        OnCategoriesChanged();
        return null;
    }

    public string? RenameCategory(Guid categoryId, string newName)
    {
        var index = FindCategoryIndex(categoryId);

        if (index < 0)
        {
            return null;
        }

        var name = newName.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return "Le nom est obligatoire.";
        }

        var category = Categories[index];

        var duplicateExists = Categories.Any(other =>
            other.Id != categoryId &&
            other.ParentId == category.ParentId &&
            string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicateExists)
        {
            return "Une catégorie porte déjà ce nom au même niveau.";
        }

        category.Name = name;
        category.UpdatedAt = DateTimeOffset.Now;
        OnCategoriesChanged();
        return null;
    }

    public string? DeleteCategory(Guid categoryId)
    {
        var index = FindCategoryIndex(categoryId);

        if (index < 0)
        {
            return null;
        }

        if (Categories.Any(category => category.ParentId == categoryId))
        {
            return "Impossible de supprimer une catégorie qui contient des sous-catégories.";
        }

        var usageCount = Entries.Count(entry => entry.CategoryIds.Contains(categoryId));

        if (usageCount > 0)
        {
            return $"Impossible de supprimer : cette catégorie est utilisée par {usageCount} mot(s).";
        }

        Categories.RemoveAt(index);
        OnCategoriesChanged();
        return null;
    }

    private void OnCategoriesChanged()
    {
        RebuildCategoryTree();
        OnPropertyChanged(nameof(SelectedEntryCategoryNames));
        RefreshFilteredEntries();
        SaveDatabase();
    }

    // Appelé par les nœuds quand le TreeView les sélectionne (binding IsSelected).
    private void OnCategoryNodeSelected(CategoryNodeViewModel node)
    {
        if (_selectedCategoryNode == node)
        {
            return;
        }

        // Le TreeView désélectionne déjà l'ancien nœud via le binding, mais le
        // ViewModel doit rester cohérent même sans vue attachée.
        var previousNode = _selectedCategoryNode;
        _selectedCategoryNode = node;
        previousNode?.IsSelected = false;
        OnPropertyChanged(nameof(SelectedCategoryNode));
        OnPropertyChanged(nameof(SelectedCategoryLabel));
        RefreshFilteredEntries();
    }

    private void RebuildCategoryTree()
    {
        // L'arbre est reconstruit à chaque mutation (les compteurs et le tri
        // changent). On préserve l'état d'expansion et la sélection courante.
        var expandedIds = CollectNodes(CategoryTree)
            .Where(node => node.Category is not null && node.IsExpanded)
            .Select(node => node.Category!.Id)
            .ToHashSet();
        var selectedCategoryId = _selectedCategoryNode?.Category?.Id;
        var selectedKind = _selectedCategoryNode?.Kind;

        CategoryTree.Clear();

        var allNode = CategoryNodeViewModel.CreateAllEntries(OnCategoryNodeSelected);
        allNode.EntryCount = Entries.Count;
        CategoryTree.Add(allNode);

        var uncategorizedNode = CategoryNodeViewModel.CreateUncategorized(OnCategoryNodeSelected);
        uncategorizedNode.EntryCount = Entries.Count(entry => entry.CategoryIds.Count == 0);
        CategoryTree.Add(uncategorizedNode);

        // Flatten fournit un parcours en profondeur avec la profondeur de chaque
        // nœud : une pile suffit pour reconstituer l'imbrication.
        var nodeStack = new List<CategoryNodeViewModel>();

        foreach (var (category, depth) in CategoryHierarchy.Flatten(Categories))
        {
            var node = CategoryNodeViewModel.CreateForCategory(category, OnCategoryNodeSelected);
            node.IsExpanded = expandedIds.Contains(category.Id);

            if (depth == 0)
            {
                CategoryTree.Add(node);
            }
            else
            {
                nodeStack[depth - 1].Children.Add(node);
            }

            if (nodeStack.Count > depth)
            {
                nodeStack[depth] = node;
                nodeStack.RemoveRange(depth + 1, nodeStack.Count - depth - 1);
            }
            else
            {
                nodeStack.Add(node);
            }
        }

        foreach (var rootNode in CategoryTree.Skip(2))
        {
            ComputeEntryCounts(rootNode);
        }

        RestoreSelection(selectedKind, selectedCategoryId, allNode);
        OnPropertyChanged(nameof(HasCategories));
    }

    private void RestoreSelection(
        CategoryNodeKind? selectedKind,
        Guid? selectedCategoryId,
        CategoryNodeViewModel allNode)
    {
        var nodeToSelect = selectedKind switch
        {
            CategoryNodeKind.Category => CollectNodes(CategoryTree)
                .FirstOrDefault(node => node.Category?.Id == selectedCategoryId),
            CategoryNodeKind.Uncategorized => CategoryTree[1],
            _ => allNode
        } ?? allNode;

        // Le nœud précédent n'existe plus : le callback mettra le filtre à jour.
        _selectedCategoryNode = null;
        nodeToSelect.IsSelected = true;
    }

    // Compte les entrées du sous-arbre (catégorie + descendantes) et retourne
    // l'ensemble des Ids couverts pour le calcul du parent.
    private HashSet<Guid> ComputeEntryCounts(CategoryNodeViewModel node)
    {
        var subtreeIds = new HashSet<Guid> { node.Category!.Id };

        foreach (var child in node.Children)
        {
            subtreeIds.UnionWith(ComputeEntryCounts(child));
        }

        node.EntryCount = Entries.Count(entry => entry.CategoryIds.Any(subtreeIds.Contains));
        return subtreeIds;
    }

    private static IEnumerable<CategoryNodeViewModel> CollectNodes(
        IEnumerable<CategoryNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var descendant in CollectNodes(node.Children))
            {
                yield return descendant;
            }
        }
    }

    private string GetCategoryPath(VocabularyCategory category)
    {
        var categoriesById = Categories.ToDictionary(item => item.Id);
        var segments = new List<string> { category.Name };
        var visited = new HashSet<Guid> { category.Id };
        var current = category;

        while (current.ParentId is Guid parentId &&
               categoriesById.TryGetValue(parentId, out var parent) &&
               visited.Add(parent.Id))
        {
            segments.Insert(0, parent.Name);
            current = parent;
        }

        return string.Join(" › ", segments);
    }

    private static VocabularyDatabase CreateSampleDatabase()
    {
        var durationCategory = new VocabularyCategory
        {
            Name = "Durée"
        };
        var literatureCategory = new VocabularyCategory
        {
            Name = "Littérature"
        };
        var characterCategory = new VocabularyCategory
        {
            Name = "Caractère"
        };
        var philosophyCategory = new VocabularyCategory
        {
            Name = "Philosophie"
        };

        return new VocabularyDatabase
        {
            Categories =
            [
                durationCategory,
                literatureCategory,
                characterCategory,
                philosophyCategory
            ],
            Entries =
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
                CategoryIds = { durationCategory.Id, literatureCategory.Id },
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
                CategoryIds = { characterCategory.Id },
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
                CategoryIds = { philosophyCategory.Id, literatureCategory.Id },
                Tags = { "adjectif", "concept" }
            }
            ]
        };
    }

    private void SaveDatabase()
    {
        _repository.SaveDatabase(new VocabularyDatabase
        {
            Entries = Entries.ToList(),
            Categories = Categories.ToList()
        });
    }

    private void OnEntriesChanged()
    {
        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(EmptyListMessage));
        OnPropertyChanged(nameof(EmptyDetailMessage));
    }

    private void RefreshFilteredEntries()
    {
        // ObservableCollection notifie WPF des ajouts/retraits. C'est pour cela
        // que la ListBox se met à jour automatiquement après le filtrage.
        var selectedEntryId = SelectedEntry?.Id;
        _activeCategoryFilterIds = BuildCategoryFilterIds();

        var matchingEntries = Entries
            .Where(entry => EntryMatchesCategory(entry) && EntryMatchesSearch(entry))
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

    // Ensemble des Ids couverts par le nœud sélectionné (catégorie + descendantes),
    // ou null quand aucun filtre de catégorie ne s'applique.
    private HashSet<Guid>? BuildCategoryFilterIds()
    {
        if (_selectedCategoryNode?.Category is not VocabularyCategory category)
        {
            return null;
        }

        var ids = CategoryHierarchy.GetDescendantIds(Categories, category.Id);
        ids.Add(category.Id);
        return ids;
    }

    private bool EntryMatchesCategory(VocabularyEntry entry)
    {
        if (_selectedCategoryNode is null || _selectedCategoryNode.Kind == CategoryNodeKind.AllEntries)
        {
            return true;
        }

        if (_selectedCategoryNode.Kind == CategoryNodeKind.Uncategorized)
        {
            return entry.CategoryIds.Count == 0;
        }

        return _activeCategoryFilterIds is not null &&
            entry.CategoryIds.Any(_activeCategoryFilterIds.Contains);
    }

    private string BuildSearchStatusText()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return $"{FilteredEntries.Count} mot(s)";
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

        // La recherche reste volontairement simple : pas d'index, juste un scan en
        // mémoire. C'est suffisant pour la Phase 1 et quelques centaines de mots.
        return SearchFieldMatches(entry.Word, normalizedQuery) ||
            SearchFieldMatches(entry.Definition, normalizedQuery) ||
            SearchFieldMatches(entry.Notes, normalizedQuery) ||
            SearchFieldMatches(entry.Source, normalizedQuery) ||
            SearchFieldsMatch(entry.Synonyms, normalizedQuery) ||
            SearchFieldsMatch(entry.ExampleSentences, normalizedQuery) ||
            SearchFieldsMatch(GetCategoryNames(entry.CategoryIds), normalizedQuery) ||
            SearchFieldsMatch(entry.Tags, normalizedQuery);
    }

    private IEnumerable<string> GetCategoryNames(IEnumerable<Guid> categoryIds)
    {
        var categoriesById = Categories.ToDictionary(category => category.Id);

        foreach (var categoryId in categoryIds)
        {
            if (categoriesById.TryGetValue(categoryId, out var category))
            {
                yield return category.Name;
            }
        }
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
        // Supprime les accents avant comparaison : "ephemere" peut retrouver
        // "Éphémère". Utile pour une application centrée sur le français.
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

    private int FindCategoryIndex(Guid categoryId)
    {
        for (var index = 0; index < Categories.Count; index++)
        {
            if (Categories[index].Id == categoryId)
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
