import { randomUUID } from 'expo-crypto';
import * as ImagePicker from 'expo-image-picker';
import { useState } from 'react';
import { Image, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { CategoryIcon } from './CategoryIcon';
import { useTheme } from '../theme/useTheme';
import { inlineImageUri } from '../services/imageCache';
import { processImage } from '../utils/imageProcessor';

export const MAX_IMAGES = 4;

// Picker + caption editor for an entry's images, up to MAX_IMAGES. A newly
// added image always gets a fresh Id — there is no "replace in place":
// editing means remove, then add again.
export function EntryImagePicker({ images, onChange }) {
  const colors = useTheme();
  const [error, setError] = useState('');

  async function addImages() {
    setError('');
    const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();

    if (!permission.granted) {
      setError("L'accès aux photos est nécessaire pour ajouter une image.");
      return;
    }

    const remaining = MAX_IMAGES - images.length;
    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      allowsMultipleSelection: true,
      selectionLimit: remaining,
      quality: 1,
    });

    if (result.canceled) {
      return;
    }

    const picked = result.assets.slice(0, remaining);
    const processed = [];

    for (const asset of picked) {
      try {
        const base64 = await processImage(asset.uri, asset.width, asset.height);
        processed.push({ Id: randomUUID(), Caption: '', ImageBase64: base64 });
      } catch (imageError) {
        console.warn(`[image processing] ${String(imageError?.message ?? imageError)}`);
        setError('Impossible de traiter une des images sélectionnées.');
      }
    }

    onChange([...images, ...processed]);

    if (result.assets.length > remaining) {
      setError(`Une entrée ne peut avoir que ${MAX_IMAGES} images au maximum ; le reste a été ignoré.`);
    }
  }

  const removeImage = (id) => onChange(images.filter((image) => image.Id !== id));
  const setCaption = (id, caption) =>
    onChange(images.map((image) => (image.Id === id ? { ...image, Caption: caption } : image)));

  return (
    <View style={styles.container}>
      <View style={styles.row}>
        {images.map((image) => (
          <View key={image.Id} style={styles.tile}>
            <Image source={{ uri: inlineImageUri(image.ImageBase64) }} style={styles.thumbnail} />
            <Pressable
              onPress={() => removeImage(image.Id)}
              style={[styles.removeBadge, { backgroundColor: colors.danger }]}
              hitSlop={6}
            >
              <CategoryIcon iconKey="Phosphor.x" color={colors.textOnAccent} size={11} />
            </Pressable>
            <TextInput
              style={[styles.caption, { color: colors.textSecondary, borderColor: colors.borderSubtle }]}
              value={image.Caption}
              onChangeText={(text) => setCaption(image.Id, text)}
              placeholder="Légende"
              placeholderTextColor={colors.textMuted}
            />
          </View>
        ))}

        {images.length < MAX_IMAGES && (
          <Pressable
            onPress={addImages}
            style={[styles.addTile, { backgroundColor: colors.surfaceHover, borderColor: colors.borderSubtle }]}
          >
            <CategoryIcon iconKey="Phosphor.plus" color={colors.textMuted} size={22} />
          </Pressable>
        )}
      </View>

      {error.length > 0 && <Text style={[styles.error, { color: colors.danger }]}>{error}</Text>}
    </View>
  );
}

const TILE_SIZE = 84;

const styles = StyleSheet.create({
  container: { gap: 8 },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  tile: { width: TILE_SIZE },
  thumbnail: { width: TILE_SIZE, height: TILE_SIZE, borderRadius: 8 },
  removeBadge: {
    position: 'absolute',
    top: -6,
    right: -6,
    width: 20,
    height: 20,
    borderRadius: 999,
    alignItems: 'center',
    justifyContent: 'center',
  },
  caption: {
    marginTop: 4,
    fontSize: 11,
    borderBottomWidth: 1,
    paddingVertical: 2,
  },
  addTile: {
    width: TILE_SIZE,
    height: TILE_SIZE,
    borderRadius: 8,
    borderWidth: 1,
    borderStyle: 'dashed',
    alignItems: 'center',
    justifyContent: 'center',
  },
  error: { fontSize: 12 },
});
