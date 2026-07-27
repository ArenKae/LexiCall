// Aide à la navigation dans la hiérarchie des catégories (ParentId).
// Le JSON pouvant être édité à la main, on tolère parents manquants
// (traités comme racines) et cycles (rattachés à la racine).
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Utilities;

public static class CategoryHierarchy
{
    private static readonly IReadOnlyDictionary<Guid, int> NoOrder = new Dictionary<Guid, int>();

    // Parcours en profondeur : racines puis enfants récursivement, chaque
    // groupe de frères trié via SortSiblings (ordre manuel s'il existe, sinon
    // alphabétique). Le Depth retourné permet aux listes plates d'indenter
    // sans arbre réel. order vient de CategoryOrderStore (menu contextuel
    // "Monter"/"Descendre") ; omis, l'ordre reste purement alphabétique.
    public static IReadOnlyList<(VocabularyCategory Category, int Depth)> Flatten(
        IReadOnlyCollection<VocabularyCategory> categories,
        IReadOnlyDictionary<Guid, int>? order = null)
    {
        var (roots, childrenByParent) = BuildChildrenLookup(categories, order ?? NoOrder);
        var result = new List<(VocabularyCategory, int)>(categories.Count);
        var visited = new HashSet<Guid>();

        void Visit(VocabularyCategory category, int depth)
        {
            if (!visited.Add(category.Id))
            {
                return;
            }

            result.Add((category, depth));

            if (childrenByParent.TryGetValue(category.Id, out var children))
            {
                foreach (var child in children)
                {
                    Visit(child, depth + 1);
                }
            }
        }

        foreach (var root in roots)
        {
            Visit(root, 0);
        }

        // Catégories jamais atteintes depuis une racine : cycle de parenté,
        // on les remonte à la racine pour rester visibles.
        foreach (var category in categories.OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Visit(category, 0);
        }

        return result;
    }

    // Attribue à chaque catégorie l'index de couleur de sa racine ; les
    // descendantes héritent de l'index de leur racine. Sert de repli
    // automatique à CategoryColorResolver quand aucune couleur n'a été
    // choisie manuellement (voir CategoryColorStore). L'index d'une racine
    // est dérivé de son Id (stable), pas de sa position parmi les autres
    // racines : réordonner les catégories (Monter/Descendre) ne doit jamais
    // changer leur couleur automatique.
    public static IReadOnlyDictionary<Guid, int> ComputeColorIndexes(IReadOnlyCollection<VocabularyCategory> categories)
    {
        var colorIndexes = new Dictionary<Guid, int>();
        var lastIndexByDepth = new List<int>();

        foreach (var (category, depth) in Flatten(categories))
        {
            var colorIndex = depth == 0 ? GetStableIndex(category.Id) : lastIndexByDepth[depth - 1];

            if (lastIndexByDepth.Count > depth)
            {
                lastIndexByDepth[depth] = colorIndex;
                lastIndexByDepth.RemoveRange(depth + 1, lastIndexByDepth.Count - depth - 1);
            }
            else
            {
                lastIndexByDepth.Add(colorIndex);
            }

            colorIndexes[category.Id] = colorIndex;
        }

        return colorIndexes;
    }

    // & int.MaxValue plutôt que Math.Abs : évite l'OverflowException sur
    // int.MinValue (Math.Abs(int.MinValue) n'a pas de représentation positive).
    private static int GetStableIndex(Guid categoryId) => categoryId.GetHashCode() & int.MaxValue;

    public static HashSet<Guid> GetDescendantIds(
        IReadOnlyCollection<VocabularyCategory> categories,
        Guid rootId)
    {
        var (_, childrenByParent) = BuildChildrenLookup(categories, NoOrder);
        var descendants = new HashSet<Guid>();

        void Visit(Guid parentId)
        {
            if (!childrenByParent.TryGetValue(parentId, out var children))
            {
                return;
            }

            foreach (var child in children)
            {
                if (descendants.Add(child.Id))
                {
                    Visit(child.Id);
                }
            }
        }

        Visit(rootId);
        return descendants;
    }

    // Frères d'ordre (même parent effectif que category, elle comprise) dans
    // l'ordre d'affichage actuel : utilisé pour déplacer une catégorie vers le
    // haut/bas parmi ses frères (MainWindowViewModel.MoveCategoryUp/Down).
    public static IReadOnlyList<VocabularyCategory> GetSiblingsInOrder(
        IReadOnlyCollection<VocabularyCategory> categories,
        VocabularyCategory category,
        IReadOnlyDictionary<Guid, int>? order = null)
    {
        var knownIds = categories.Select(c => c.Id).ToHashSet();
        var parentId = EffectiveParentId(category, knownIds);
        var siblings = categories.Where(c => EffectiveParentId(c, knownIds) == parentId).ToList();
        SortSiblings(siblings, order ?? NoOrder);
        return siblings;
    }

    private static (List<VocabularyCategory> Roots, Dictionary<Guid, List<VocabularyCategory>> ChildrenByParent)
        BuildChildrenLookup(IReadOnlyCollection<VocabularyCategory> categories, IReadOnlyDictionary<Guid, int> order)
    {
        var knownIds = categories.Select(category => category.Id).ToHashSet();
        var roots = new List<VocabularyCategory>();
        var childrenByParent = new Dictionary<Guid, List<VocabularyCategory>>();

        foreach (var category in categories)
        {
            if (EffectiveParentId(category, knownIds) is Guid parentId)
            {
                if (!childrenByParent.TryGetValue(parentId, out var children))
                {
                    children = [];
                    childrenByParent[parentId] = children;
                }

                children.Add(category);
            }
            else
            {
                roots.Add(category);
            }
        }

        SortSiblings(roots, order);

        foreach (var children in childrenByParent.Values)
        {
            SortSiblings(children, order);
        }

        return (roots, childrenByParent);
    }

    // Rang manuel (CategoryOrderStore) d'abord si au moins une des deux
    // catégories comparées en a un, sinon ordre alphabétique — une catégorie
    // ajoutée après coup dans un groupe déjà réordonné se retrouve donc triée
    // alphabétiquement après les catégories déjà positionnées manuellement,
    // jusqu'à ce qu'on la déplace à son tour.
    private static void SortSiblings(List<VocabularyCategory> siblings, IReadOnlyDictionary<Guid, int> order)
    {
        siblings.Sort((a, b) =>
        {
            var hasOrderA = order.TryGetValue(a.Id, out var rankA);
            var hasOrderB = order.TryGetValue(b.Id, out var rankB);

            if (hasOrderA && hasOrderB)
            {
                return rankA.CompareTo(rankB);
            }

            if (hasOrderA != hasOrderB)
            {
                return hasOrderA ? -1 : 1;
            }

            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        });
    }

    private static Guid? EffectiveParentId(VocabularyCategory category, HashSet<Guid> knownIds)
    {
        return category.ParentId is Guid parentId && knownIds.Contains(parentId) && parentId != category.Id
            ? parentId
            : null;
    }
}
