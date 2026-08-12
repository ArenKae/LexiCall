// Helpers for navigating the category hierarchy (ParentId). Since the JSON
// can be hand-edited, dangling parents (treated as roots) and cycles
// (reattached to the root) are both tolerated.
using LexiCall.Desktop.Models;

namespace LexiCall.Desktop.Utilities;

public static class CategoryHierarchy
{
    private static readonly IReadOnlyDictionary<Guid, int> NoOrder = new Dictionary<Guid, int>();

    // Depth-first walk: roots then children recursively, each sibling group
    // sorted via SortSiblings (manual order if any, otherwise alphabetical).
    // The returned Depth lets flat lists indent without a real tree. order
    // comes from CategoryOrderStore (context menu "Monter"/"Descendre" —
    // Move up/down); omitted, the order stays purely alphabetical.
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

        // Categories never reached from a root: a parenting cycle — bump
        // them up to the root so they stay visible.
        foreach (var category in categories.OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Visit(category, 0);
        }

        return result;
    }

    // Assigns each category its root's color index; descendants inherit
    // their root's index. Backs CategoryColorResolver's automatic fallback
    // when no color was manually chosen (see CategoryColorStore). A root's
    // index is derived from its Id (stable), not its position among other
    // roots: reordering categories (Move up/down) must never change their
    // automatic color.
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

    // & int.MaxValue rather than Math.Abs: avoids the OverflowException on
    // int.MinValue (Math.Abs(int.MinValue) has no positive representation).
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

    // Siblings (same effective parent as category, itself included) in the
    // current display order — used to move a category up/down among its
    // siblings (MainWindowViewModel.MoveCategoryUp/Down).
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

    // Manual rank (CategoryOrderStore) takes priority if either compared
    // category has one, otherwise alphabetical — a category added later to
    // an already-reordered group sorts alphabetically after the manually
    // positioned ones, until it's moved in turn.
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
