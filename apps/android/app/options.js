import { useState } from 'react';
import { Alert, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useTheme } from '../src/theme/useTheme';
import { useVocabularyStore } from '../src/store/useVocabularyStore';
import { needsPush } from '../src/store/syncCycle';

const CONNECTION_LABELS = {
  Ok: 'Connexion réussie.',
  NotConfigured: 'URL ou clé API manquante.',
  Unreachable: 'Serveur injoignable.',
  InvalidApiKey: 'Clé API refusée.',
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
          style={[styles.button, { borderColor: colors.borderStrong }]}
          onPress={handleTestConnection}
        >
          <Text style={{ color: colors.textPrimary }}>Tester la connexion</Text>
        </Pressable>
        <Pressable
          style={[styles.button, { backgroundColor: colors.accent, borderColor: colors.accent }]}
          onPress={handleSync}
          disabled={store.isSyncing}
        >
          <Text style={{ color: colors.textOnAccent }}>
            {store.isSyncing ? 'Synchronisation…' : 'Synchroniser maintenant'}
          </Text>
        </Pressable>
      </View>

      {connectionMessage.length > 0 && (
        <Text style={[styles.message, { color: colors.textSecondary }]}>{connectionMessage}</Text>
      )}
      {store.statusMessage.length > 0 && (
        <Text style={[styles.message, { color: colors.textSecondary }]}>{store.statusMessage}</Text>
      )}

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
  buttons: { flexDirection: 'row', gap: 10, marginTop: 16, flexWrap: 'wrap' },
  button: { borderWidth: 1, borderRadius: 8, paddingHorizontal: 14, paddingVertical: 10 },
  reset: { marginTop: 20, alignItems: 'center' },
  message: { fontSize: 13, lineHeight: 18 },
});
