import { useRouter } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { createApiClient } from '../../src/services/apiClient';
import { useTheme } from '../../src/theme/useTheme';
import { useVocabularyStore } from '../../src/store/useVocabularyStore';
import { SYNC_STATUS_LABELS } from '../../src/utils/syncStatusLabels';

function formatLastSynced(iso) {
  const date = iso ? new Date(iso) : null;

  return date && !Number.isNaN(date.getTime())
    ? `Dernière synchronisation le ${date.toLocaleString('fr-FR')}`
    : 'Pas encore synchronisé';
}

// Secondary destinations and a quick read on what the device holds locally.
export default function More() {
  const colors = useTheme();
  const router = useRouter();
  const entries = useVocabularyStore((state) => state.entries);
  const categories = useVocabularyStore((state) => state.categories);
  const status = useVocabularyStore((state) => state.globalSyncStatus);
  const lastSyncedAt = useVocabularyStore((state) => state.lastSyncedAt);
  const apiBaseUrl = useVocabularyStore((state) => state.apiBaseUrl);
  const apiKey = useVocabularyStore((state) => state.apiKey);
  const [isReindexing, setIsReindexing] = useState(false);

  async function handleReindexCategories() {
    setIsReindexing(true);
    const { status: reindexStatus, result, errorDetail } = await createApiClient(
      apiBaseUrl,
      apiKey
    ).reindexCategoryEmbeddings();
    setIsReindexing(false);

    const message =
      reindexStatus === 'Ok'
        ? `${result.embedded} recalculée(s), ${result.unchanged} déjà à jour, ${result.orphans_removed} orpheline(s) supprimée(s).`
        : reindexStatus === 'NotConfigured'
          ? 'Cette action nécessite une synchronisation API configurée (voir Options).'
          : errorDetail
            ? `Impossible de mettre à jour la catégorisation automatique : ${errorDetail}`
            : 'Impossible de mettre à jour la catégorisation automatique pour le moment. Réessaie plus tard.';

    Alert.alert('Mise à jour de la catégorisation automatique', message);
  }

  const statusColor =
    status === 'Ok'
      ? colors.success
      : status === 'Syncing'
        ? colors.warning
        : status === 'Problem'
          ? colors.danger
          : colors.textMuted;

  const rowStyle = [
    styles.row,
    { backgroundColor: colors.surface, borderColor: colors.borderSubtle },
  ];

  return (
    <View style={styles.container}>
      <View style={styles.rows}>
        <Pressable style={rowStyle} onPress={() => router.push('/sync-history')}>
          <View style={[styles.dot, { backgroundColor: statusColor }]} />
          <View style={styles.rowText}>
            <Text style={[styles.label, { color: colors.textPrimary }]}>
              {SYNC_STATUS_LABELS[status] ?? status}
            </Text>
            <Text style={[styles.info, { color: colors.textMuted }]}>
              {formatLastSynced(lastSyncedAt)}
            </Text>
          </View>
        </Pressable>

        <Pressable style={rowStyle} onPress={handleReindexCategories} disabled={isReindexing}>
          {isReindexing ? (
            <ActivityIndicator size="small" color={colors.textSecondary} />
          ) : (
            <CategoryIcon
              iconKey="Phosphor.arrows-counter-clockwise"
              color={colors.textSecondary}
              size={20}
            />
          )}
          <View style={styles.rowText}>
            <Text style={[styles.label, { color: colors.textPrimary }]}>
              {isReindexing ? 'Actualisation…' : 'Actualiser la catégorisation'}
            </Text>
            <Text style={[styles.info, { color: colors.textMuted }]}>
              Recalcule les vecteurs des catégories dont le contenu a changé
            </Text>
          </View>
        </Pressable>

        <Pressable style={rowStyle} onPress={() => router.push('/options')}>
          <CategoryIcon iconKey="Phosphor.gear" color={colors.textSecondary} size={20} />
          <Text style={[styles.label, { color: colors.textPrimary }]}>Paramètres</Text>
        </Pressable>

        <Text style={[styles.info, { color: colors.textMuted, paddingHorizontal: 4 }]}>
          {entries.length} entrée(s) · {categories.length} catégorie(s)
        </Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, justifyContent: 'flex-end', padding: 14, paddingBottom: 24 },
  rows: { gap: 8 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    borderWidth: 1,
    borderRadius: 12,
    paddingHorizontal: 12,
    paddingVertical: 14,
  },
  dot: { width: 13, height: 13, borderRadius: 999, marginHorizontal: 3 },
  rowText: { flex: 1 },
  label: { flex: 1, fontSize: 15 },
  info: { fontSize: 12, marginTop: 2 },
});
