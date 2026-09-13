import * as Sharing from 'expo-sharing';
import { useState } from 'react';
import { Alert, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { CategoryIcon } from '../src/components/CategoryIcon';
import { pickDatabaseFile } from '../src/services/storage';
import { useTheme } from '../src/theme/useTheme';
import { useVocabularyStore } from '../src/store/useVocabularyStore';
import { needsPush } from '../src/store/syncCycle';

const CONNECTION_LABELS = {
  Ok: 'Connexion réussie.',
  NotConfigured: 'URL ou clé API manquante.',
  Unreachable: 'Serveur injoignable.',
  InvalidApiKey: 'Clé API refusée.',
};

const FULL_RESYNC_RESULTS = {
  Ok: 'Resynchronisation terminée.',
  Syncing: 'Resynchronisation en cours en arrière-plan.',
  NotConfigured: 'Synchronisation désactivée (URL vide).',
  Problem: 'Resynchronisation incomplète, voir l’historique.',
};

// API configuration and manual sync trigger.
export default function Options() {
  const colors = useTheme();
  const store = useVocabularyStore();
  const [baseUrl, setBaseUrl] = useState(store.apiBaseUrl);
  const [apiKey, setApiKey] = useState(store.apiKey);
  const [connectionMessage, setConnectionMessage] = useState('');

  const pendingCount = store.pendingEntryDeletions.length + store.pendingCategoryDeletions.length;
  const unsyncedCount = [...store.entries, ...store.categories].filter(needsPush).length;

  const inputStyle = [
    styles.input,
    { color: colors.textPrimary, borderColor: colors.borderStrong, backgroundColor: colors.surface },
  ];

  async function handleTestConnection() {
    setConnectionMessage('Test en cours…');
    await store.saveApiConfig(baseUrl, apiKey);
    const status = await store.testConnection();
    setConnectionMessage(CONNECTION_LABELS[status] ?? status);
  }

  async function handleSync() {
    setConnectionMessage('');
    await store.saveApiConfig(baseUrl, apiKey);
    await store.resync();
  }

  function handleFullResync() {
    Alert.alert(
      'Tout resynchroniser',
      'Toutes les données locales sont renvoyées au serveur et toute sa vue est retéléchargée. Chaque enregistrement reste arbitré par date de modification : le plus récent des deux côtés gagne.',
      [
        { text: 'Annuler', style: 'cancel' },
        {
          text: 'Resynchroniser',
          onPress: async () => {
            setConnectionMessage('');
            await store.saveApiConfig(baseUrl, apiKey);
            await store.forceFullResync();
            setConnectionMessage(
              FULL_RESYNC_RESULTS[useVocabularyStore.getState().globalSyncStatus] ?? ''
            );
          },
        },
      ]
    );
  }

  async function handleExport() {
    setConnectionMessage('');

    try {
      const uri = store.exportDatabase();

      if (!(await Sharing.isAvailableAsync())) {
        setConnectionMessage('Partage indisponible sur cet appareil.');
        return;
      }

      await Sharing.shareAsync(uri, {
        mimeType: 'application/json',
        dialogTitle: 'Exporter la base LexiCall',
      });
    } catch (error) {
      console.warn(`[export] ${String(error?.message ?? error)}`);
      setConnectionMessage('Export impossible.');
    }
  }

  async function handleImport() {
    setConnectionMessage('');

    const picked = await pickDatabaseFile();

    if (picked === null) {
      return;
    }

    Alert.alert(
      'Importer une base',
      'Cette action remplace la base de vocabulaire locale par le fichier choisi. Une copie de sauvegarde de la base actuelle sera conservée à côté.',
      [
        { text: 'Annuler', style: 'cancel' },
        {
          text: 'Importer',
          style: 'destructive',
          onPress: async () => {
            try {
              const database = await store.importDatabase(picked);
              setConnectionMessage(
                `${database.Entries.length} entrée(s) et ${database.Categories.length} catégorie(s) importées.`
              );
            } catch (error) {
              setConnectionMessage(`Import impossible : ${String(error?.message ?? error)}`);
            }
          },
        },
      ]
    );
  }

  function handleReset() {
    Alert.alert(
      'Effacer les données locales',
      'Toutes les entrées et catégories stockées sur ce téléphone seront supprimées, puis rechargées depuis le serveur à la prochaine synchronisation. Les modifications non encore synchronisées seront perdues.',
      [
        { text: 'Annuler', style: 'cancel' },
        {
          text: 'Effacer',
          style: 'destructive',
          onPress: async () => {
            await store.resetLocalData();
            await store.resync();
          },
        },
      ]
    );
  }

  return (
    <ScrollView
      style={{ backgroundColor: colors.background }}
      contentContainerStyle={styles.content}
    >
      <Text style={[styles.section, { color: colors.textPrimary }]}>Synchronisation API</Text>

      <Text style={[styles.label, { color: colors.textSecondary }]}>Adresse du serveur</Text>
      <TextInput
        style={inputStyle}
        value={baseUrl}
        onChangeText={setBaseUrl}
        placeholder="http://192.168.0.119:8000"
        placeholderTextColor={colors.textMuted}
        autoCapitalize="none"
        autoCorrect={false}
        keyboardType="url"
      />

      <Text style={[styles.label, { color: colors.textSecondary }]}>Clé API</Text>
      <TextInput
        style={inputStyle}
        value={apiKey}
        onChangeText={setApiKey}
        placeholder="X-API-Key"
        placeholderTextColor={colors.textMuted}
        autoCapitalize="none"
        autoCorrect={false}
      />

      <View style={styles.buttons}>
        <Pressable
          style={[styles.button, styles.rowButton, { borderColor: colors.borderStrong }]}
          onPress={handleTestConnection}
        >
          <Text style={[styles.buttonLabel, { color: colors.textPrimary }]}>
            Tester la connexion
          </Text>
        </Pressable>

        <Pressable
          style={[
            styles.button,
            styles.rowButton,
            { backgroundColor: colors.accent, borderColor: colors.accent },
          ]}
          onPress={handleSync}
          disabled={store.isSyncing}
        >
          <Text style={[styles.buttonLabel, { color: colors.textOnAccent }]}>
            {store.isSyncing ? 'Synchronisation…' : 'Connexion'}
          </Text>
        </Pressable>
      </View>

      <Pressable
        style={[styles.wideButton, { borderColor: colors.borderStrong }]}
        onPress={handleFullResync}
        disabled={store.isFullResyncing}
      >
        <CategoryIcon
          iconKey="Phosphor.arrows-counter-clockwise"
          color={colors.textSecondary}
          size={17}
        />
        <Text style={{ color: colors.textPrimary }}>
          {store.isFullResyncing ? 'Resynchronisation…' : 'Tout resynchroniser'}
        </Text>
      </Pressable>
      <Text style={[styles.hint, { color: colors.textMuted }]}>
        Renvoie toutes les modifications locales et retélécharge les données divergentes depuis le
        serveur. À utiliser après un changement de serveur ou en cas d’écart inexpliqué.
      </Text>

      {connectionMessage.length > 0 && (
        <Text style={[styles.message, { color: colors.textSecondary }]}>{connectionMessage}</Text>
      )}
      {store.statusMessage.length > 0 && (
        <Text style={[styles.message, { color: colors.textSecondary }]}>{store.statusMessage}</Text>
      )}

      <View style={[styles.separator, { backgroundColor: colors.borderSubtle }]} />

      <Text style={[styles.section, { color: colors.textPrimary }]}>État local</Text>
      <Text style={[styles.message, { color: colors.textSecondary }]}>
        {store.entries.length} entrée(s) · {store.categories.length} catégorie(s)
      </Text>
      <Text style={[styles.message, { color: colors.textMuted }]}>
        Dernier pull : {store.lastPulledAt ?? 'jamais'}
      </Text>
      {pendingCount > 0 && (
        <Text style={[styles.message, { color: colors.warning }]}>
          {pendingCount} suppression(s) en attente de confirmation par le serveur.
        </Text>
      )}
      {unsyncedCount > 0 && (
        <Text style={[styles.message, { color: colors.warning }]}>
          {unsyncedCount} modification(s) pas encore poussée(s).
        </Text>
      )}

      <View style={[styles.separator, { backgroundColor: colors.borderSubtle }]} />

      <Text style={[styles.section, { color: colors.textPrimary }]}>Transfert</Text>
      <Text style={[styles.hint, { color: colors.textMuted }]}>
        Un export sert à initialiser un autre appareil sans repasser par le serveur. Un import
        remplace la base locale et conserve une copie de sauvegarde de l’ancienne.
      </Text>

      <Pressable
        style={[styles.wideButton, { borderColor: colors.borderStrong }]}
        onPress={handleExport}
      >
        <CategoryIcon iconKey="Phosphor.floppy-disk" color={colors.textSecondary} size={17} />
        <Text style={{ color: colors.textPrimary }}>Exporter la base</Text>
      </Pressable>

      <Pressable
        style={[styles.wideButton, { borderColor: colors.borderStrong }]}
        onPress={handleImport}
      >
        <CategoryIcon iconKey="Phosphor.folder-open" color={colors.textSecondary} size={17} />
        <Text style={{ color: colors.textPrimary }}>Importer une base</Text>
      </Pressable>

      <View style={[styles.separator, { backgroundColor: colors.borderSubtle }]} />

      <Pressable style={[styles.button, styles.reset, { borderColor: colors.danger }]} onPress={handleReset}>
        <Text style={{ color: colors.danger }}>Effacer les données locales</Text>
      </Pressable>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  content: { padding: 16, gap: 8 },
  section: { fontSize: 16, fontWeight: '700', marginTop: 12 },
  label: { fontSize: 13, marginTop: 8 },
  input: {
    borderWidth: 1,
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 15,
  },
  // No wrapping: the two buttons share the row at any width, each shrinking
  // and wrapping its own label rather than dropping onto a second line.
  buttons: { flexDirection: 'row', gap: 10, marginTop: 16 },
  wideButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    borderWidth: 1,
    borderRadius: 8,
    paddingVertical: 12,
    marginTop: 10,
  },
  hint: { fontSize: 12, lineHeight: 17 },
  button: { borderWidth: 1, borderRadius: 8, paddingHorizontal: 14, paddingVertical: 10 },
  // Only inside the row above: flex:1 on a standalone child of the scrolling
  // column would stretch it to fill the leftover height. minWidth 0 lets a
  // button shrink below its label's own width instead of widening the row.
  rowButton: {
    flex: 1,
    flexBasis: 0,
    minWidth: 0,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 8,
  },
  buttonLabel: { textAlign: 'center' },
  reset: { marginTop: 20, alignItems: 'center' },
  separator: { height: 1, marginTop: 14, marginBottom: 2 },
  message: { fontSize: 13, lineHeight: 18 },
});
