// Navigating the category hierarchy (ParentId). A dangling parent is treated as
// a root and a parenting cycle is reattached to the root, so hand-edited or
// partially-synced data still renders.
import { colorIndexForRoot } from './categoryColor';
import { compareText } from './collation';

function effectiveParentId(category, knownIds) {
  const parentId = category.ParentId;
  return parentId && parentId !== category.Id && knownIds.has(parentId) ? parentId : null;
}

function compareNames(a, b) {
  return compareText(a.Name, b.Name);
}

function buildChildrenLookup(categories) {
  const knownIds = new Set(categories.map((category) => category.Id));
  const roots = [];
  const childrenByParent = new Map();

  for (const category of categories) {
    const parentId = effectiveParentId(category, knownIds);

    if (parentId === null) {
      roots.push(category);
      continue;
    }

    const siblings = childrenByParent.get(parentId) ?? [];
    siblings.push(category);
    childrenByParent.set(parentId, siblings);
  }

  roots.sort(compareNames);
  for (const siblings of childrenByParent.values()) {
    siblings.sort(compareNames);
  }

  return { roots, childrenByParent };
}

// Depth-first walk returning [{ category, depth }], roots first then children.
export function flattenCategories(categories) {
  const { roots, childrenByParent } = buildChildrenLookup(categories);
  const flattened = [];
  const visited = new Set();

  const visit = (category, depth) => {
    if (visited.has(category.Id)) {
      return;
    }
    visited.add(category.Id);
    flattened.push({ category, depth });

    for (const child of childrenByParent.get(category.Id) ?? []) {
      visit(child, depth + 1);
    }
  };

  for (const root of roots) {
    visit(root, 0);
  }
  // Anything a root never reached sits in a cycle; surface it at root level
  // rather than letting it vanish from the tree.
  for (const category of [...categories].sort(compareNames)) {
    visit(category, 0);
  }

  return flattened;
}

export function getDescendantIds(categories, rootId) {
  const { childrenByParent } = buildChildrenLookup(categories);
  const descendants = new Set();

  const visit = (parentId) => {
    for (const child of childrenByParent.get(parentId) ?? []) {
      if (!descendants.has(child.Id)) {
        descendants.add(child.Id);
        visit(child.Id);
      }
    }
  };

  visit(rootId);
  return descendants;
}

// Each category inherits its root's color index, so a subtree reads as one
// color family. The index comes from the root's own Id rather than its position
// among siblings: reordering categories must never repaint them.
export function computeColorIndexes(categories) {
  const indexes = new Map();
  const lastIndexByDepth = [];

  for (const { category, depth } of flattenCategories(categories)) {
    const colorIndex = depth === 0 ? colorIndexForRoot(category.Id) : lastIndexByDepth[depth - 1];

    lastIndexByDepth[depth] = colorIndex;
    lastIndexByDepth.length = depth + 1;
    indexes.set(category.Id, colorIndex);
  }

  return indexes;
}
