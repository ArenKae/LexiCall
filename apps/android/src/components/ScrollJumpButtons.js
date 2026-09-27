import { useEffect, useRef, useSyncExternalStore } from 'react';
import { Animated, Pressable, StyleSheet, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';

export function ScrollJumpButtons({ edges, onScrollToTop, onScrollToBottom }) {
  const { canScrollUp, canScrollDown } = useSyncExternalStore(edges.subscribe, edges.get);

  return (
    <View style={styles.column} pointerEvents="box-none">
      <JumpButton visible={canScrollUp} iconKey="Phosphor.caret-double-up-bold" onPress={onScrollToTop} />
      <JumpButton visible={canScrollDown} iconKey="Phosphor.caret-double-down-bold" onPress={onScrollToBottom} />
    </View>
  );
}

function JumpButton({ visible, iconKey, onPress }) {
  const colors = useTheme();
  const progress = useRef(new Animated.Value(visible ? 1 : 0)).current;

  useEffect(() => {
    Animated.timing(progress, {
      toValue: visible ? 1 : 0,
      duration: 140,
      useNativeDriver: true,
    }).start();
  }, [visible, progress]);

  const scale = progress.interpolate({ inputRange: [0, 1], outputRange: [0.8, 1] });

  return (
    <Animated.View
      style={{ opacity: progress, transform: [{ scale }] }}
      pointerEvents={visible ? 'auto' : 'none'}
    >
      <Pressable
        onPress={onPress}
        hitSlop={6}
        style={({ pressed }) => [
          styles.button,
          {
            backgroundColor: pressed ? colors.surfaceHover : colors.surface,
            borderColor: colors.borderStrong,
          },
        ]}
      >
        <CategoryIcon iconKey={iconKey} color={colors.accent} size={19} />
      </Pressable>
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  column: { position: 'absolute', right: 16, bottom: 16, gap: 10 },
  button: {
    width: 42,
    height: 42,
    borderRadius: 999,
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
    elevation: 4,
  },
});
