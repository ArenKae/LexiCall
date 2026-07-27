// ViewModel principal : expose à MainWindow.xaml l'arbre des catégories,
// la liste d'entrées filtrée (catégorie + recherche) et les opérations CRUD
// sur les entrées et les catégories.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LexiCall.Desktop.Models;
using LexiCall.Desktop.Services;
using LexiCall.Desktop.Utilities;

namespace LexiCall.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly VocabularyRepository _repository;
    private VocabularyApiClient _apiClient;
    private string? _apiBaseUrl;
    private string? _apiKey;
    private string _searchQuery = string.Empty;
    private string _searchStatusText = string.Empty;
    private VocabularyEntry? _selectedEntry;
    private CategoryNodeViewModel? _selectedCategoryNode;
    private HashSet<Guid>? _activeCategoryFilterIds;

    public MainWindowViewModel(VocabularyRepository? repository = null, VocabularyApiClient? apiClient = null)
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

        var settings = SettingsStore.Load();
        _apiBaseUrl = settings.ApiBaseUrl;
        _apiKey = settings.ApiKey;
        _apiClient = apiClient ?? new VocabularyApiClient(_apiBaseUrl, _apiKey);

        // Rattrapage en tâche de fond des mutations faites hors ligne, jamais
        // encore poussées vers l'API — voir ResyncWithApiAsync. Non awaité
        // volontairement : ne doit jamais retarder l'ouverture de la fenêtre.
        _ = ResyncWithApiAsync();
    }

    public ObservableCollection<VocabularyEntry> Entries { get; }

    public ObservableCollection<VocabularyCategory> Categories { get; }

    // Liste affichée par l'UI, reconstruite à partir d'Entries à chaque recherche ou mutation.
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

    // Dernier niveau seulement ; le chemin complet est exposé via SelectedCategoryTooltip.
    public string SelectedCategoryLabel => _selectedCategoryNode?.DisplayName ?? "Toutes les entrées";

    // Null quand le chemin complet n'apporterait rien (nœud virtuel ou racine).
    public string? SelectedCategoryTooltip => _selectedCategoryNode?.Category is VocabularyCategory { ParentId: not null } category
        ? GetCategoryPath(category)
        : null;

    // Objets complets (pas juste le nom) : les chips du détail ont besoin de
    // l'Id pour le clic (cf. SelectCategory).
    public IReadOnlyList<VocabularyCategory> SelectedEntryCategories => SelectedEntry is null
        ? []
        : GetCategories(SelectedEntry.CategoryIds).ToList();

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
                OnPropertyChanged(nameof(SelectedEntryCategories));
                OnPropertyChanged(nameof(EmptyDetailMessage));
            }
        }
    }

    public string ThemeToggleText => ThemeService.CurrentTheme == AppTheme.Dark
        ? "☀  Thème clair"
        : "🌙  Thème sombre";

    public string ApiBaseUrl => _apiBaseUrl ?? string.Empty;

    public string ApiKey => _apiKey ?? string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ToggleTheme()
    {
        ThemeService.Toggle();
        OnPropertyChanged(nameof(ThemeToggleText));
    }

    // Appelé par OptionsWindow : persiste (load-merge-save, comme le reste de
    // settings.json) puis reconstruit le client HTTP avec les nouvelles valeurs.
    public void UpdateApiSettings(string? apiBaseUrl, string? apiKey)
    {
        _apiBaseUrl = apiBaseUrl;
        _apiKey = apiKey;

        var settings = SettingsStore.Load();
        settings.ApiBaseUrl = apiBaseUrl;
        settings.ApiKey = apiKey;
        SettingsStore.Save(settings);

        _apiClient = new VocabularyApiClient(apiBaseUrl, apiKey);
        OnPropertyChanged(nameof(ApiBaseUrl));
        OnPropertyChanged(nameof(ApiKey));
    }

    public Task<ApiConnectionStatus> TestApiConnectionAsync() => _apiClient.TestConnectionAsync();

    // Pousse tout l'état local vers l'API, une seule fois au démarrage — voir
    // le constructeur. Rattrape les mutations faites pendant que l'API était
    // injoignable (chaque mutation tente déjà un push best-effort au moment où
    // elle se produit, cf. AddEntry/UpdateEntry/DeleteEntry/SaveCategory/
    // RenameCategory/DeleteCategory ; ceci couvre les échecs de ces tentatives).
    // Catégories poussées avant les entrées : l'API valide les CategoryIds
    // d'une entrée contre les catégories déjà connues côté serveur.
    private async Task ResyncWithApiAsync()
    {
        if (!_apiClient.IsConfigured)
        {
            return;
        }

        var status = await _apiClient.TestConnectionAsync().ConfigureAwait(false);

        if (status != ApiConnectionStatus.Ok)
        {
            // API injoignable ou mal configurée : pas la peine de tenter des
            // centaines d'upserts qui échoueraient tous à chaque démarrage.
            return;
        }

        // Snapshot pris avant la boucle : Entries/Categories sont des
        // ObservableCollection liées à l'UI, pas sûres à énumérer pendant que
        // l'utilisateur les modifie (ce qui peut arriver, cette boucle tourne
        // en tâche de fond sans bloquer l'UI).
        foreach (var category in Categories.ToList())
        {
            await _apiClient.TryUpsertCategoryAsync(category).ConfigureAwait(false);
        }

        foreach (var entry in Entries.ToList())
        {
            await _apiClient.TryUpsertEntryAsync(entry).ConfigureAwait(false);
        }
    }

    public void AddEntry(VocabularyEntry entry)
    {
        Entries.Insert(0, entry);
        OnEntriesChanged();

        // Recherche réinitialisée pour que le mot ajouté reste visible ; la
        // catégorie sélectionnée, elle, est préservée par RebuildCategoryTree.
        _searchQuery = string.Empty;
        OnPropertyChanged(nameof(SearchQuery));

        RebuildCategoryTree();
        RefreshFilteredEntries();
        SelectedEntry = entry;
        SaveDatabase();

        // Best-effort, jamais awaité : ne doit jamais ralentir l'UI, encore
        // moins quand l'API est injoignable (cas courant pour l'instant).
        _ = _apiClient.TryUpsertEntryAsync(entry);
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
        _ = _apiClient.TryUpsertEntryAsync(updatedEntry);
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
        _ = _apiClient.TryDeleteEntryAsync(entry.Id);
    }

    // Ajout ou remplacement d'une catégorie validée par CategoryEditorWindow.
    // Retourne un message d'erreur, ou null si l'opération a réussi.
    public string? SaveCategory(VocabularyCategory category)
    {
        // Garde-fou anti-cycle : l'éditeur exclut déjà les descendants du sélecteur
        // de parent, mais on revérifie avant de persister.
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
        _ = _apiClient.TryUpsertCategoryAsync(category);
        return null;
    }

    // Couleur choisie manuellement pour une catégorie (ou null pour revenir à
    // l'automatique) : appelé après un SaveCategory réussi depuis
    // CategoryEditorWindow. Préférence locale à l'app Windows (CategoryColorStore),
    // ne touche ni vocabulary.json ni api/.
    public void SetCategoryColor(Guid categoryId, string? colorHex)
    {
        if (string.IsNullOrEmpty(colorHex))
        {
            CategoryColorStore.ClearColor(categoryId);
        }
        else
        {
            CategoryColorStore.SetColor(categoryId, colorHex);
        }

        OnCategoriesChanged();
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
        _ = _apiClient.TryUpsertCategoryAsync(category);
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
        CategoryColorStore.ClearColor(categoryId);
        CategoryOrderStore.ClearOrder(categoryId);
        OnCategoriesChanged();
        _ = _apiClient.TryDeleteCategoryAsync(categoryId);
        return null;
    }

    // Déplace une catégorie parmi ses frères (même parent effectif) et fige
    // l'ordre résultant du groupe entier dans CategoryOrderStore — préférence
    // locale à l'app Windows, ne touche ni vocabulary.json ni api/.
    public void MoveCategoryUp(Guid categoryId) => MoveCategory(categoryId, offset: -1);

    public void MoveCategoryDown(Guid categoryId) => MoveCategory(categoryId, offset: 1);

    private void MoveCategory(Guid categoryId, int offset)
    {
        var category = Categories.FirstOrDefault(c => c.Id == categoryId);

        if (category is null)
        {
            return;
        }

        var siblings = CategoryHierarchy
            .GetSiblingsInOrder(Categories, category, CategoryOrderStore.LoadAll())
            .ToList();
        var index = siblings.FindIndex(sibling => sibling.Id == categoryId);
        var targetIndex = index + offset;

        if (targetIndex < 0 || targetIndex >= siblings.Count)
        {
            return;
        }

        (siblings[index], siblings[targetIndex]) = (siblings[targetIndex], siblings[index]);
        CategoryOrderStore.SetOrder(siblings.Select(sibling => sibling.Id));
        RebuildCategoryTree();
    }

    private void OnCategoriesChanged()
    {
        RebuildCategoryTree();
        OnPropertyChanged(nameof(SelectedEntryCategories));
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

        // Le ViewModel doit rester cohérent même sans vue attachée.
        var previousNode = _selectedCategoryNode;
        _selectedCategoryNode = node;
        previousNode?.IsSelected = false;
        OnPropertyChanged(nameof(SelectedCategoryNode));
        OnPropertyChanged(nameof(SelectedCategoryLabel));
        OnPropertyChanged(nameof(SelectedCategoryTooltip));
        RefreshFilteredEntries();
    }

    // Clic sur un chip de catégorie : sélectionne la même catégorie dans l'arbre de gauche.
    public void SelectCategory(Guid categoryId)
    {
        var node = CollectNodes(CategoryTree)
            .FirstOrDefault(candidate => candidate.Category?.Id == categoryId);

        if (node is null)
        {
            return;
        }

        foreach (var ancestor in GetAncestors(node))
        {
            ancestor.IsExpanded = true;
        }

        node.IsSelected = true;
    }

    // Remonte les parents dans l'arbre affiché (pas juste la hiérarchie de
    // catégories) pour pouvoir déplier jusqu'au nœud ciblé.
    private IEnumerable<CategoryNodeViewModel> GetAncestors(CategoryNodeViewModel node)
    {
        var ancestorIds = new List<Guid>();
        var current = node.Category;
        var categoriesById = Categories.ToDictionary(category => category.Id);

        while (current?.ParentId is Guid parentId && categoriesById.TryGetValue(parentId, out var parent))
        {
            ancestorIds.Add(parent.Id);
            current = parent;
        }

        return CollectNodes(CategoryTree).Where(candidate => candidate.Category is not null && ancestorIds.Contains(candidate.Category.Id));
    }

    private void RebuildCategoryTree()
    {
        // Reconstruit à chaque mutation ; on préserve l'expansion et la sélection courante.
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
        var categoryOrder = CategoryOrderStore.LoadAll();
        var colorIndexes = CategoryHierarchy.ComputeColorIndexes(Categories);
        var colorOverrides = CategoryColorStore.LoadAll();
        var nodeStack = new List<CategoryNodeViewModel>();

        foreach (var (category, depth) in CategoryHierarchy.Flatten(Categories, categoryOrder))
        {
            var node = CategoryNodeViewModel.CreateForCategory(category, OnCategoryNodeSelected);
            node.IsExpanded = expandedIds.Contains(category.Id);
            node.Depth = depth;
            node.ColorBrush = new SolidColorBrush(
                CategoryColorResolver.Resolve(category, Categories, colorIndexes, colorOverrides));

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

    // Compte les entrées du sous-arbre et retourne les Ids couverts, utilisés par le parent.
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

        var stillVisible = selectedEntryId is null
            ? null
            : FilteredEntries.FirstOrDefault(entry => entry.Id == selectedEntryId);

        SelectedEntry = stillVisible ?? FilteredEntries.FirstOrDefault();
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

    private IEnumerable<VocabularyCategory> GetCategories(IEnumerable<Guid> categoryIds)
    {
        var categoriesById = Categories.ToDictionary(category => category.Id);

        foreach (var categoryId in categoryIds)
        {
            if (categoriesById.TryGetValue(categoryId, out var category))
            {
                yield return category;
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
        // Supprime les accents avant comparaison ("ephemere" retrouve "Éphémère").
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
