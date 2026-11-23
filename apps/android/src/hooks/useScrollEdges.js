import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

const EDGE_THRESHOLD = 24;
const GLIDE_VIEWPORTS = 1.5;
const IDLE_DELAY_MS = 250;
const CHASE_TIMEOUT_MS = 4000;
const KEEP_ALL_RENDERED_WINDOW_SIZE = 10000;
const BEYOND_ANY_CONTENT = 1e7;
const VIEWABILITY_CONFIG = { itemVisiblePercentThreshold: 50 };

function createEdgesStore() {
  let value = { canScrollUp: false, canScrollDown: false };
  const listeners = new Set();

  return {
    get: () => value,
    set: (canScrollUp, canScrollDown) => {
      if (value.canScrollUp === canScrollUp && value.canScrollDown === canScrollDown) return;
      value = { canScrollUp, canScrollDown };
      listeners.forEach((listener) => listener());
    },
    subscribe: (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}

export function useScrollEdges(listRef, itemCount) {
  const metrics = useRef({ y: 0, contentHeight: 0, viewportHeight: 0 });
  const lastVisibleIndex = useRef(-1);
  const itemCountRef = useRef(itemCount);
  const pending = useRef(null);
  const idleTimer = useRef(null);
  const [edges] = useState(createEdgesStore);

  itemCountRef.current = itemCount;

  const isAtTop = () => metrics.current.y <= EDGE_THRESHOLD;
  const isAtBottom = () => lastVisibleIndex.current >= itemCountRef.current - 1;

  const refresh = useCallback(() => {
    if (pending.current) return;
    const { contentHeight, viewportHeight } = metrics.current;
    const scrollable = contentHeight > viewportHeight;
    edges.set(scrollable && !isAtTop(), scrollable && !isAtBottom());
  }, [edges]);

  const endPending = useCallback(() => {
    pending.current = null;
    clearTimeout(idleTimer.current);
    refresh();
  }, [refresh]);

  const jumpToEnd = useCallback(
    () => listRef.current?.scrollToOffset({ offset: BEYOND_ANY_CONTENT, animated: false }),
    [listRef]
  );

  const glideTo = useCallback(
    (offset) => {
      const { y, viewportHeight } = metrics.current;
      const glide = viewportHeight * GLIDE_VIEWPORTS;
      if (Math.abs(offset - y) > glide) {
        const runUp = offset > y ? offset - glide : offset + glide;
        listRef.current?.scrollToOffset({ offset: runUp, animated: false });
      }
      listRef.current?.scrollToOffset({ offset, animated: true });
    },
    [listRef]
  );

  const checkPending = useCallback(
    (isIdle) => {
      const target = pending.current;
      if (!target) return false;

      const reached = target.edge === 'top' ? isAtTop() : isAtBottom();
      if (reached || Date.now() > target.deadline) {
        endPending();
      } else if (isIdle && target.edge === 'bottom') {
        jumpToEnd();
      }
      return true;
    },
    [endPending, jumpToEnd]
  );

  const scheduleIdle = useCallback(() => {
    clearTimeout(idleTimer.current);
    idleTimer.current = setTimeout(() => {
      if (checkPending(true)) scheduleIdle();
    }, IDLE_DELAY_MS);
  }, [checkPending]);

  useEffect(() => () => clearTimeout(idleTimer.current), []);

  const onActivity = useCallback(() => {
    if (checkPending(false)) {
      scheduleIdle();
    } else {
      refresh();
    }
  }, [checkPending, scheduleIdle, refresh]);

  const trackScroll = useCallback(
    (event) => {
      const { contentOffset, contentSize, layoutMeasurement } = event.nativeEvent;
      metrics.current = {
        y: contentOffset.y,
        contentHeight: contentSize.height,
        viewportHeight: layoutMeasurement.height,
      };
      onActivity();
    },
    [onActivity]
  );

  const onViewableItemsChanged = useCallback(
    ({ viewableItems }) => {
      lastVisibleIndex.current = viewableItems.reduce(
        (highest, item) => Math.max(highest, item.index ?? -1),
        -1
      );
      onActivity();
    },
    [onActivity]
  );

  const listProps = useMemo(
    () => ({
      onScroll: trackScroll,
      onScrollEndDrag: trackScroll,
      onMomentumScrollEnd: trackScroll,
      onScrollBeginDrag: (event) => {
        if (pending.current) endPending();
        trackScroll(event);
      },
      onContentSizeChange: (_, height) => {
        const grew = height > metrics.current.contentHeight;
        metrics.current.contentHeight = height;
        if (grew && pending.current?.edge === 'bottom') jumpToEnd();
        onActivity();
      },
      onLayout: (event) => {
        metrics.current.viewportHeight = event.nativeEvent.layout.height;
        onActivity();
      },
      onViewableItemsChanged,
      viewabilityConfig: VIEWABILITY_CONFIG,
      scrollEventThrottle: 16,
      windowSize: KEEP_ALL_RENDERED_WINDOW_SIZE,
    }),
    [trackScroll, onActivity, onViewableItemsChanged, endPending, jumpToEnd]
  );

  const scrollToTop = useCallback(() => {
    pending.current = { edge: 'top', deadline: Date.now() + CHASE_TIMEOUT_MS };
    glideTo(0);
    edges.set(false, true);
    scheduleIdle();
  }, [edges, glideTo, scheduleIdle]);

  const scrollToBottom = useCallback(() => {
    pending.current = { edge: 'bottom', deadline: Date.now() + CHASE_TIMEOUT_MS };
    const { contentHeight, viewportHeight } = metrics.current;
    glideTo(Math.max(0, contentHeight - viewportHeight));
    edges.set(true, false);
    scheduleIdle();
  }, [edges, glideTo, scheduleIdle]);

  return { edges, listProps, scrollToTop, scrollToBottom };
}
