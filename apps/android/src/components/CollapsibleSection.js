import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { TreeChevron } from './TreeChevron';
import { useTheme } from '../theme/useTheme';

// A labeled, bordered section that starts closed, expanding on tap. The
// border switches to the accent color while open, so the block reads as
// active rather than just another plain row in the form.
export function CollapsibleSection({ title, defaultOpen = false, children }) {
  const colors = useTheme();
  const [open, setOpen] = useState(defaultOpen);

  return (
    <View
      style={[
        styles.container,
        {
          backgroundColor: colors.surface,
          borderColor: open ? colors.accent : colors.borderSubtle,
        },
      ]}
    >
      <Pressable style={styles.header} onPress={() => setOpen((current) => !current)}>
        <Text style={[styles.title, { color: open ? colors.accent : colors.textSecondary }]}>
          {title}
        </Text>
        <TreeChevron expanded={open} color={open ? colors.accent : colors.textSecondary} size={14} />
      </Pressable>
      {open && (
        <View style={[styles.body, { borderTopColor: colors.borderSubtle }]}>{children}</View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { borderWidth: 1.5, borderRadius: 12, paddingHorizontal: 12 },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 12,
  },
  title: { fontSize: 12, fontWeight: '700', textTransform: 'uppercase', letterSpacing: 0.4 },
  body: { borderTopWidth: 1, paddingTop: 10, paddingBottom: 12 },
});
