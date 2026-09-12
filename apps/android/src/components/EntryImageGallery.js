import { useState } from 'react';
import {
  ActivityIndicator,
  Dimensions,
  Image,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';

const THUMBNAIL_SIZE = 120;
const PREVIEW_RATIO = 0.8;

// An entry's images: thumbnails that open a full-screen viewer, mirroring the
// desktop client's gallery. A thumbnail that failed to download says so and
// offers a retry, since unlike on desktop the bytes always come over the network.
export function EntryImageGallery({ images, states, onRetry }) {
  const colors = useTheme();
  const [openedIndex, setOpenedIndex] = useState(null);
  const screen = Dimensions.get('window');
  const opened = openedIndex === null ? null : images[openedIndex];
  const openedUri = opened ? states[opened.Id]?.uri : null;

  return (
    <>
      <View style={styles.thumbnails}>
        {images.map((image, index) => {
          const state = states[image.Id] ?? { status: 'loading' };

          return (
            <Pressable
              key={image.Id}
              style={[styles.thumbnail, { backgroundColor: colors.surfaceHover }]}
              onPress={() =>
                state.status === 'ready' ? setOpenedIndex(index) : onRetry(image.Id)
              }
            >
              {state.status === 'ready' && (
                <Image
                  source={{ uri: state.uri }}
                  style={styles.thumbnailImage}
                  resizeMode="contain"
                />
              )}
              {state.status === 'loading' && <ActivityIndicator color={colors.textMuted} />}
              {state.status === 'failed' && (
                <View style={styles.failure}>
                  <CategoryIcon iconKey="Phosphor.image" color={colors.textMuted} size={22} />
                  <Text style={[styles.failureText, { color: colors.textMuted }]}>
                    Image indisponible
                  </Text>
                  <Text style={[styles.retry, { color: colors.accent }]}>Réessayer</Text>
                </View>
              )}
            </Pressable>
          );
        })}
      </View>

      <Modal
        visible={openedUri !== null && openedUri !== undefined}
        transparent
        animationType="fade"
        onRequestClose={() => setOpenedIndex(null)}
      >
        <Pressable style={styles.backdrop} onPress={() => setOpenedIndex(null)}>
          {openedUri && (
            <>
              <Image
                source={{ uri: openedUri }}
                style={{
                  width: screen.width * PREVIEW_RATIO,
                  height: screen.height * PREVIEW_RATIO,
                }}
                resizeMode="contain"
              />
              {opened.Caption.length > 0 && <Text style={styles.caption}>{opened.Caption}</Text>}

              <View style={styles.navigation}>
                {openedIndex > 0 ? (
                  <Pressable onPress={() => setOpenedIndex(openedIndex - 1)} hitSlop={14}>
                    <CategoryIcon iconKey="Phosphor.caret-left" color="#FFFFFF" size={30} />
                  </Pressable>
                ) : (
                  <View />
                )}
                {openedIndex < images.length - 1 && (
                  <Pressable onPress={() => setOpenedIndex(openedIndex + 1)} hitSlop={14}>
                    <CategoryIcon iconKey="Phosphor.caret-right" color="#FFFFFF" size={30} />
                  </Pressable>
                )}
              </View>
            </>
          )}
        </Pressable>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  thumbnails: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  thumbnail: {
    width: THUMBNAIL_SIZE,
    height: THUMBNAIL_SIZE,
    borderRadius: 8,
    overflow: 'hidden',
    alignItems: 'center',
    justifyContent: 'center',
  },
  thumbnailImage: { width: '100%', height: '100%' },
  failure: { alignItems: 'center', gap: 6, paddingHorizontal: 8 },
  failureText: { fontSize: 11, textAlign: 'center' },
  retry: { fontSize: 12, fontWeight: '600' },
  backdrop: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(0, 0, 0, 0.92)',
  },
  caption: { color: '#FFFFFF', fontSize: 14, textAlign: 'center', paddingHorizontal: 24 },
  navigation: {
    position: 'absolute',
    left: 18,
    right: 18,
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
});
