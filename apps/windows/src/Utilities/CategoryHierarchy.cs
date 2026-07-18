// Aide à la navigation dans la hiérarchie des catégories (ParentId).
// Le JSON pouvant être édité à la main, on tolère parents manquants
// (traités comme racines) et cycles (rattachés à la racine).
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Utilities;

public static class CategoryHierarchy
{
    // Parcours en profondeur : racines triées par nom, puis enfants récursivement.
    // Le Depth retourné permet aux listes plates d'indenter sans arbre réel.
    public static IReadOnlyList<(VocabularyCategory Category, int Depth)> Flatten(
        IReadOnlyCollection<VocabularyCategory> categories)
    {
        var (roots, childrenByParent) = BuildChildrenLookup(categories);
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

    public static HashSet<Guid> GetDescendantIds(
        IReadOnlyCollection<VocabularyCategory> categories,
        Guid rootId)
    {
        var (_, childrenByParent) = BuildChildrenLookup(categories);
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

    private static (List<VocabularyCategory> Roots, Dictionary<Guid, List<VocabularyCategory>> ChildrenByParent)
        BuildChildrenLookup(IReadOnlyCollection<VocabularyCategory> categories)
    {
        var knownIds = categories.Select(category => category.Id).ToHashSet();
        var roots = new List<VocabularyCategory>();
        var childrenByParent = new Dictionary<Guid, List<VocabularyCategory>>();

        foreach (var category in categories.OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase))
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

        return (roots, childrenByParent);
    }

    private static Guid? EffectiveParentId(VocabularyCategory category, HashSet<Guid> knownIds)
    {
        return category.ParentId is Guid parentId && knownIds.Contains(parentId) && parentId != category.Id
            ? parentId
            : null;
    }
}
