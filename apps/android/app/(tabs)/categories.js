import { memo, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Stack, useRouter } from 'expo-router';
import {
  Alert,
  Animated,
  FlatList,
  PanResponder,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { CategoryActionSheet } from '../../src/components/CategoryActionSheet';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { TreeChevron } from '../../src/components/TreeChevron';
import { useCategoryIndex } from '../../src/hooks/useCategoryIndex';
import { useTheme } from '../../src/theme/useTheme';
import { useVocabularyStore } from '../../src/store/useVocabularyStore';
import { flattenCategories, getSiblingsInOrder } from '../../src/utils/categoryHierarchy';
import { ALL_ENTRIES, ARCHIVES, UNCATEGORIZED } from '../../src/utils/filterEntries';

const INDENT = 20;
const DEFAULT_CATEGORY_ICON = 'Solar.tag';
// Rows are forced to one fixed height while reordering so a drag can work in
// index arithmetic instead of measuring every row.
const REORDER_ROW_HEIGHT = 52;
// Band at each end of the list where holding a dragged row scrolls it along.
const AUTO_SCROLL_EDGE = 90;
const AUTO_SCROLL_MAX_STEP = 5;
const AUTO_SCROLL_INTERVAL = 16;

// Hides everything nested under a collapsed node: the depth-first order means a
// subtree is exactly the rows deeper than its root, up to the next shallower one.
function visibleRows(rows, expandedIds) {
  const visible = [];
  let collapsedAtDepth = null;

  for (const row of rows) {
    if (collapsedAtDepth !== null && row.depth > collapsedAtDepth) {
      continue;
    }
    collapsedAtDepth = null;
    visible.push(row);

    if (row.hasChildren && !expandedIds.has(row.key)) {
      collapsedAtDepth = row.depth;
    }
  }

  return visible;
}

// A row plus its whole subtree, which travels with it when dragged.
function blockLength(rows, index) {
  const { depth } = rows[index];
  let length = 1;

  while (index + length < rows.length && rows[index + length].depth > depth) {
    length += 1;
  }

  return length;
}

// The dragged row's siblings as displayed. Depth-first order makes each sibling
// and its descendants one contiguous block, and the group one contiguous span,
// which is what lets the drop slots be computed from heights alone.
function siblingBlocks(rows, categories, category, order) {
  return getSiblingsInOrder(categories, category, order)
    .map((sibling) => {
      const index = rows.findIndex((row) => row.key === sibling.Id);
      return index < 0 ? null : { id: sibling.Id, index, length: blockLength(rows, index) };
    })
    .filter(Boolean);
}

// Slot whose resting position is closest to where the dragged block currently
// sits — the only positions offered are between its own siblings.
function targetSlot(blocks, slot, dy) {
  const dragged = blocks[slot];
  const draggedHeight = dragged.length * REORDER_ROW_HEIGHT;
  const draggedCenter = dragged.index * REORDER_ROW_HEIGHT + dy + draggedHeight / 2;
  const others = blocks.filter((_, index) => index !== slot);

  let top = blocks[0].index * REORDER_ROW_HEIGHT;
  let best = 0;
  let bestDistance = Infinity;

  for (let candidate = 0; candidate <= others.length; candidate += 1) {
    const distance = Math.abs(draggedCenter - (top + draggedHeight / 2));

    if (distance < bestDistance) {
      bestDistance = distance;
      best = candidate;
    }

    if (candidate < others.length) {
      top += others[candidate].length * REORDER_ROW_HEIGHT;
    }
  }

  return best;
}

// How far every other sibling block slides to open the gap at the target slot.
function blockShifts(rows, blocks, slot, target) {
  const shifts = new Map();
  const draggedHeight = blocks[slot].length * REORDER_ROW_HEIGHT;

  blocks.forEach((block, index) => {
    let shift = 0;

    if (target > slot && index > slot && index <= target) {
      shift = -draggedHeight;
    } else if (target < slot && index >= target && index < slot) {
      shift = draggedHeight;
    }

    if (shift !== 0) {
      for (let row = block.index; row < block.index + block.length; row += 1) {
        shifts.set(rows[row].key, shift);
      }
    }
  });

  return shifts;
}

function DragHandle({ color, faded }) {
  return (
    <View style={[styles.handle, { opacity: faded ? 0.25 : 1 }]}>
      <View style={[styles.handleBar, { backgroundColor: color }]} />
      <View style={[styles.handleBar, { backgroundColor: color }]} />
      <View style={[styles.handleBar, { backgroundColor: color }]} />
    </View>
  );
}

const ReorderRow = memo(function ReorderRow({
  row,
  colors,
  expanded,
  shift,
  dragY,
  isDragging,
  dimmed,
  alone,
  onToggle,
  onGrab,
  onMove,
  onRelease,
}) {
  const keyRef = useRef(row.key);
  keyRef.current = row.key;

  const responder = useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => true,
      onMoveShouldSetPanResponder: () => true,
      onPanResponderGrant: () => onGrab(keyRef.current),
      onPanResponderMove: (_event, gesture) => onMove(gesture.dy),
      onPanResponderRelease: () => onRelease(),
      onPanResponderTerminate: () => onRelease(),
    })
  ).current;

  return (
    <Animated.View
      style={[
        styles.reorderRow,
        {
          marginLeft: 14 + row.depth * INDENT,
          transform: [{ translateY: isDragging ? dragY : shift }],
          opacity: dimmed ? 0.3 : 1,
          zIndex: isDragging ? 2 : 1,
          elevation: isDragging ? 6 : 0,
        },
      ]}
    >
      <Pressable
        onPress={() => onToggle(row.key)}
        hitSlop={10}
        style={styles.chevron}
        disabled={!row.hasChildren}
      >
        {row.hasChildren && <TreeChevron expanded={expanded} color={colors.textPrimary} size={20} />}
      </Pressable>

      <View
        style={[
          styles.reorderBody,
          {
            backgroundColor: colors.surface,
            borderColor: isDragging ? colors.accent : colors.borderSubtle,
            borderWidth: isDragging ? 2 : 1,
          },
        ]}
      >
        <CategoryIcon iconKey={row.iconKey} color={row.color} size={20} />
        <Text style={[styles.label, { color: colors.textPrimary }]} numberOfLines={1}>
          {row.label}
        </Text>
        <View {...responder.panHandlers} hitSlop={10}>
          <DragHandle color={colors.textSecondary} faded={alone} />
        </View>
      </View>
    </Animated.View>
  );
});

// Category tree, preceded by the virtual selections. Picking a row sets the
// browsing filter and hands back to the list.
export default function Categories() {
  const colors = useTheme();
  const router = useRouter();
  const entries = useVocabularyStore((state) => state.entries);
  const categories = useVocabularyStore((state) => state.categories);
  const categoryOrder = useVocabularyStore((state) => state.categoryOrder);
  const setCategoryFilter = useVocabularyStore((state) => state.setCategoryFilter);
  const deleteCategory = useVocabularyStore((state) => state.deleteCategory);
  const setCategoryOrder = useVocabularyStore((state) => state.setCategoryOrder);
  const categoryIndex = useCategoryIndex();
  const [expandedIds, setExpandedIds] = useState(() => new Set());
  const [actionsFor, setActionsFor] = useState(null);
  const [reorderMode, setReorderMode] = useState(false);
  const [draftOrder, setDraftOrder] = useState(null);
  const [drag, setDrag] = useState(null);

  const dragY = useRef(new Animated.Value(0)).current;
  const dragRef = useRef(null);
  const listRef = useRef(null);
  const viewportHeightRef = useRef(0);
  const scrollOffsetRef = useRef(0);
  const contentHeightRef = useRef(0);
  const autoScrollRef = useRef(null);
  const applyDragRef = useRef(null);
  const shownRef = useRef([]);
  const categoriesRef = useRef(categories);
  const draftOrderRef = useRef(draftOrder);
  categoriesRef.current = categories;
  draftOrderRef.current = draftOrder;

  const activeOrder = reorderMode && draftOrder ? draftOrder : categoryOrder;

  const rows = useMemo(() => {
    const flat = flattenCategories(categories, activeOrder);

    const uncategorizedCount = entries.filter(
      (entry) => entry.CategoryIds.length === 0 && !entry.IsArchived
    ).length;
    const archivedCount = entries.filter((entry) => entry.IsArchived).length;

    const virtualRows = [
      {
        key: ALL_ENTRIES,
        label: 'Toutes les entrées',
        iconKey: 'Phosphor.stack',
        count: entries.filter((entry) => !entry.IsArchived).length,
        filter: { kind: ALL_ENTRIES, categoryId: null },
      },
      // Both catch-all views disappear when they have nothing to show, rather
      // than offering a selection that lands on an empty list.
      ...(uncategorizedCount > 0
        ? [
            {
              key: UNCATEGORIZED,
              label: 'Sans catégorie',
              iconKey: 'Solar.tag',
              count: uncategorizedCount,
              filter: { kind: UNCATEGORIZED, categoryId: null },
            },
          ]
        : []),
      ...(archivedCount > 0
        ? [
            {
              key: ARCHIVES,
              label: 'Archives',
              iconKey: 'Phosphor.books',
              count: archivedCount,
              filter: { kind: ARCHIVES, categoryId: null },
            },
          ]
        : []),
    ].map((row) => ({ ...row, depth: 0, hasChildren: false, color: colors.iconNeutral }));

    const categoryRows = flat.map(({ category, depth }, index) => ({
      key: category.Id,
      category,
      label: category.Name,
      iconKey: category.IconGlyph || DEFAULT_CATEGORY_ICON,
      depth,
      hasChildren: index + 1 < flat.length && flat[index + 1].depth > depth,
      color: categoryIndex.get(category.Id)?.color ?? colors.iconNeutral,
      filter: { kind: 'category', categoryId: category.Id },
    }));

    // Reordering only ever touches real categories, so the virtual selections
    // are left out of that mode entirely.
    return reorderMode ? categoryRows : [...virtualRows, ...categoryRows];
  }, [categories, activeOrder, entries, categoryIndex, colors.iconNeutral, reorderMode]);

  const actionSheetCategory = actionsFor
    ? categories.find((category) => category.Id === actionsFor)
    : null;

  function handleDelete() {
    const error = deleteCategory(actionsFor);
    setActionsFor(null);

    if (error) {
      Alert.alert('Suppression impossible', error);
    }
  }

  const shown = useMemo(() => visibleRows(rows, expandedIds), [rows, expandedIds]);
  shownRef.current = shown;

  // A category alone in its group has nowhere to go: its handle is shown faded
  // rather than hidden, so rows keep a single layout.
  const siblingCounts = useMemo(() => {
    const counts = new Map();
    for (const category of categories) {
      const key = category.ParentId ?? '';
      counts.set(key, (counts.get(key) ?? 0) + 1);
    }
    return counts;
  }, [categories]);

  const toggle = useCallback(
    (key) =>
      setExpandedIds((current) => {
        const next = new Set(current);
        if (!next.delete(key)) {
          next.add(key);
        }
        return next;
      }),
    []
  );

  const onGrab = useCallback(
    (key) => {
      const rowsNow = shownRef.current;
      const index = rowsNow.findIndex((row) => row.key === key);

      if (index < 0) {
        return;
      }

      const blocks = siblingBlocks(
        rowsNow,
        categoriesRef.current,
        rowsNow[index].category,
        draftOrderRef.current ?? {}
      );
      const slot = blocks.findIndex((block) => block.id === key);

      if (slot < 0 || blocks.length < 2) {
        return;
      }

      dragRef.current = { key, blocks, slot, target: slot, scrollAtGrab: scrollOffsetRef.current, dy: 0 };
      dragY.setValue(0);
      setDrag({ key, slot, target: slot });
    },
    [dragY]
  );

  const stopAutoScroll = useCallback(() => {
    if (autoScrollRef.current !== null) {
      clearInterval(autoScrollRef.current.timer);
      autoScrollRef.current = null;
    }
  }, []);

  const startAutoScroll = useCallback((step) => {
    if (autoScrollRef.current !== null) {
      autoScrollRef.current.step = step;
      return;
    }

    const handle = { step };
    handle.timer = setInterval(() => {
      const maxOffset = Math.max(0, contentHeightRef.current - viewportHeightRef.current);
      const next = Math.min(maxOffset, Math.max(0, scrollOffsetRef.current + handle.step));

      if (next === scrollOffsetRef.current) {
        return;
      }

      scrollOffsetRef.current = next;
      listRef.current?.scrollToOffset({ offset: next, animated: false });
      applyDragRef.current?.();
    }, AUTO_SCROLL_INTERVAL);

    autoScrollRef.current = handle;
  }, []);

  // The list scrolling under the finger moves the row through the content even
  // though the gesture delta hasn't changed, so the slot maths, the row's own
  // offset and the edge detection all work on the delta plus whatever has been
  // scrolled since the grab.
  const applyDrag = useCallback(() => {
    const state = dragRef.current;

    if (!state) {
      return;
    }

    const contentDy = state.dy + (scrollOffsetRef.current - state.scrollAtGrab);
    dragY.setValue(contentDy);
    const target = targetSlot(state.blocks, state.slot, contentDy);

    if (target !== state.target) {
      state.target = target;
      setDrag({ key: state.key, slot: state.slot, target });
    }

    // Where the dragged block currently sits inside the visible window, which
    // only needs the list's height — never its position on screen.
    const block = state.blocks[state.slot];
    const top = block.index * REORDER_ROW_HEIGHT + contentDy - scrollOffsetRef.current;
    const bottom = top + block.length * REORDER_ROW_HEIGHT;
    const fromBottom = viewportHeightRef.current - bottom;

    if (top < AUTO_SCROLL_EDGE) {
      const intensity = Math.min(1, (AUTO_SCROLL_EDGE - top) / AUTO_SCROLL_EDGE);
      startAutoScroll(-Math.max(1, Math.round(intensity * AUTO_SCROLL_MAX_STEP)));
    } else if (fromBottom < AUTO_SCROLL_EDGE) {
      const intensity = Math.min(1, (AUTO_SCROLL_EDGE - fromBottom) / AUTO_SCROLL_EDGE);
      startAutoScroll(Math.max(1, Math.round(intensity * AUTO_SCROLL_MAX_STEP)));
    } else {
      stopAutoScroll();
    }
  }, [dragY, startAutoScroll, stopAutoScroll]);

  applyDragRef.current = applyDrag;

  useEffect(() => stopAutoScroll, [stopAutoScroll]);

  const onMove = useCallback(
    (dy) => {
      if (!dragRef.current) {
        return;
      }

      dragRef.current.dy = dy;
      applyDrag();
    },
    [applyDrag]
  );

  const onRelease = useCallback(() => {
    const state = dragRef.current;
    stopAutoScroll();
    dragRef.current = null;
    dragY.setValue(0);
    setDrag(null);

    if (!state || state.target === state.slot) {
      return;
    }

    const ids = state.blocks.map((block) => block.id);
    const [moved] = ids.splice(state.slot, 1);
    ids.splice(state.target, 0, moved);

    setDraftOrder((current) => {
      const next = { ...current };
      ids.forEach((id, rank) => {
        next[id] = rank;
      });
      return next;
    });
  }, [dragY, stopAutoScroll]);

  // Both transitions remount the list (see its key), so the offset it reports
  // goes back to zero with it.
  function startReorder() {
    setActionsFor(null);
    scrollOffsetRef.current = 0;
    setDraftOrder({ ...categoryOrder });
    setReorderMode(true);
  }

  function cancelReorder() {
    stopAutoScroll();
    setDrag(null);
    dragRef.current = null;
    scrollOffsetRef.current = 0;
    setDraftOrder(null);
    setReorderMode(false);
  }

  function commitReorder() {
    if (draftOrder) {
      setCategoryOrder(draftOrder);
    }
    cancelReorder();
  }

  const dragBlocks = drag ? dragRef.current?.blocks ?? [] : [];
  const shifts = drag ? blockShifts(shown, dragBlocks, drag.slot, drag.target) : null;
  const inGroup = drag
    ? new Set(
        dragBlocks.flatMap((block) =>
          shown.slice(block.index, block.index + block.length).map((row) => row.key)
        )
      )
    : null;

  return (
    <>
      <Stack.Screen
        options={{
          headerRight: reorderMode
            ? undefined
            : () => (
                <Pressable
                  onPress={() => router.push('/category/edit')}
                  hitSlop={10}
                  style={styles.addButton}
                >
                  <CategoryIcon iconKey="Phosphor.plus" color={colors.textPrimary} size={20} />
                </Pressable>
              ),
        }}
      />

      {reorderMode && (
        <View style={[styles.banner, { backgroundColor: colors.accent }]}>
          <CategoryIcon iconKey="Phosphor.list-bullets" color={colors.textOnAccent} size={18} />
          <Text style={[styles.bannerText, { color: colors.textOnAccent }]}>
            Glissez les poignées pour changer l’ordre
          </Text>
          <Pressable onPress={cancelReorder} hitSlop={12}>
            <CategoryIcon iconKey="Phosphor.x" color={colors.textOnAccent} size={18} />
          </Pressable>
        </View>
      )}

      <FlatList
        // Remounts on mode change: the two modes give the list different row
        // geometry, and reusing the native view across the switch left it
        // showing nothing.
        key={reorderMode ? 'reorder' : 'browse'}
        ref={listRef}
        data={shown}
        keyExtractor={(row) => row.key}
        onLayout={(event) => {
          viewportHeightRef.current = event.nativeEvent.layout.height;
        }}
        onContentSizeChange={(_width, height) => {
          contentHeightRef.current = height;
        }}
        onScroll={(event) => {
          scrollOffsetRef.current = event.nativeEvent.contentOffset.y;
        }}
        scrollEventThrottle={16}
        // Explicit flex: with the reorder banner as a sibling, the list can no
        // longer rely on being the only child to end up with a height.
        style={styles.screen}
        contentContainerStyle={reorderMode ? styles.reorderList : styles.list}
        scrollEnabled={!drag}
        extraData={drag}
        // A dragged row travels well outside its own cell, which Android's
        // clipping would cut off mid-gesture.
        removeClippedSubviews={false}
        renderItem={({ item }) =>
          reorderMode ? (
            <ReorderRow
              row={item}
              colors={colors}
              expanded={expandedIds.has(item.key)}
              shift={shifts?.get(item.key) ?? 0}
              dragY={dragY}
              isDragging={drag?.key === item.key}
              dimmed={Boolean(inGroup) && !inGroup.has(item.key)}
              alone={(siblingCounts.get(item.category.ParentId ?? '') ?? 0) < 2}
              onToggle={toggle}
              onGrab={onGrab}
              onMove={onMove}
              onRelease={onRelease}
            />
          ) : (
            <View style={[styles.row, { marginLeft: 14 + item.depth * INDENT }]}>
              <Pressable
                onPress={() => toggle(item.key)}
                hitSlop={10}
                style={styles.chevron}
                disabled={!item.hasChildren}
              >
                {item.hasChildren && (
                  <TreeChevron
                    expanded={expandedIds.has(item.key)}
                    color={colors.textPrimary}
                    size={20}
                  />
                )}
              </Pressable>

              <Pressable
                style={[
                  styles.rowBody,
                  { backgroundColor: colors.surface, borderColor: colors.borderSubtle },
                ]}
                onPress={() => {
                  setCategoryFilter(item.filter);
                  router.push('/');
                }}
                onLongPress={() => item.category && setActionsFor(item.category.Id)}
                delayLongPress={300}
              >
                <CategoryIcon iconKey={item.iconKey} color={item.color} size={20} />
                <Text style={[styles.label, { color: colors.textPrimary }]} numberOfLines={2}>
                  {item.label}
                </Text>
                {item.count !== undefined && (
                  <View style={[styles.badge, { backgroundColor: colors.chipBackground }]}>
                    <Text style={[styles.badgeText, { color: colors.chipForeground }]}>
                      {item.count}
                    </Text>
                  </View>
                )}
              </Pressable>
            </View>
          )
        }
      />

      {reorderMode && (
        <Pressable
          style={[styles.fab, { backgroundColor: colors.accent }]}
          onPress={commitReorder}
        >
          <CategoryIcon iconKey="Solar.check-circle" color={colors.textOnAccent} size={28} />
        </Pressable>
      )}

      {actionSheetCategory && (
        <CategoryActionSheet
          visible
          category={actionSheetCategory}
          onClose={() => setActionsFor(null)}
          onAddSubcategory={() => {
            setActionsFor(null);
            router.push(`/category/edit?parentId=${actionSheetCategory.Id}`);
          }}
          onReorder={startReorder}
          onEdit={() => {
            setActionsFor(null);
            router.push(`/category/edit?id=${actionSheetCategory.Id}`);
          }}
          onDelete={handleDelete}
        />
      )}
    </>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  addButton: { paddingHorizontal: 14, paddingVertical: 8 },
  list: { paddingVertical: 10, paddingRight: 14 },
  row: { flexDirection: 'row', alignItems: 'center', marginBottom: 8 },
  chevron: { width: 32, height: 44, alignItems: 'center', justifyContent: 'center' },
  rowBody: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    borderWidth: 1,
    borderRadius: 12,
    paddingHorizontal: 12,
    paddingVertical: 12,
  },
  label: { flex: 1, fontSize: 15 },
  badge: { minWidth: 28, alignItems: 'center', borderRadius: 999, paddingHorizontal: 7, paddingVertical: 2 },
  badgeText: { fontSize: 12 },
  banner: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
  },
  bannerText: { flex: 1, fontSize: 14, fontWeight: '700' },
  reorderList: { paddingVertical: 10, paddingRight: 14, paddingBottom: 96 },
  reorderRow: { height: REORDER_ROW_HEIGHT, flexDirection: 'row', alignItems: 'center' },
  reorderBody: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    borderRadius: 12,
    paddingLeft: 12,
    paddingRight: 6,
    height: 44,
  },
  handle: { paddingHorizontal: 10, paddingVertical: 8, gap: 3 },
  handleBar: { width: 18, height: 2, borderRadius: 1 },
  fab: {
    position: 'absolute',
    right: 18,
    bottom: 18,
    width: 58,
    height: 58,
    borderRadius: 999,
    alignItems: 'center',
    justifyContent: 'center',
    elevation: 6,
  },
});
