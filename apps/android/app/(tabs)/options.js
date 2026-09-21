import { useRouter } from 'expo-router';
import { useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { CategoryIcon } from '../../src/components/CategoryIcon';
import { createApiClient } from '../../src/services/apiClient';
import { pickDatabaseFile } from '../../src/services/storage';
import { useTheme } from '../../src/theme/useTheme';
import { useVocabularyStore } from '../../src/store/useVocabularyStore';
import { needsPush } from '../../src/store/syncCycle';
import { ARCHIVES, LOCKED } from '../../src/utils/filterEntries';
import { SYNC_STATUS_LABELS } from '../../src/utils/syncStatusLabels';

const CONFIGURABLE_VIRTUAL_CATEGORIES = [
  { kind: LOCKED, label: 'Verrouillées' },
  { kind: ARCHIVES, label: 'Archives' },
];

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

function formatLastSynced(iso) {
  const date = iso ? new Date(iso) : null;

  return date && !Number.isNaN(date.getTime())
    ? date.toLocaleString('fr-FR')
    : 'Jamais';
}

function SectionTitle({ children }) {
  const colors = useTheme();
  return <Text style={[styles.sectionTitle, { color: colors.textSecondary }]}>{children}</Text>;
}

function Card({ children, borderColor }) {
  const colors = useTheme();
  return (
    <View
      style={[
        styles.card,
        { backgroundColor: colors.surface, borderColor: borderColor ?? colors.borderSubtle },
      ]}
    >
      {children}
    </View>
  );
}

function Divider() {
  const colors = useTheme();
  return <View style={[styles.divider, { backgroundColor: colors.borderSubtle }]} />;
}

function Hint({ children }) {
  const colors = useTheme();
  return <Text style={[styles.hint, { color: colors.textMuted }]}>{children}</Text>;
}

function InfoRow({ label, value, valueColor }) {
  const colors = useTheme();
  return (
    <View style={styles.row}>
      <Text style={[styles.rowLabel, { color: colors.textSecondary }]}>{label}</Text>
      <Text style={[styles.rowValue, { color: valueColor ?? colors.textPrimary }]} numberOfLines={1}>
        {value}
      </Text>
    </View>
  );
}

function ActionRow({ iconKey, label, hint, busy, danger, trailing, onPress }) {
  const colors = useTheme();
  const tint = danger ? colors.danger : colors.textSecondary;

  return (
    <Pressable style={styles.row} onPress={onPress} disabled={busy}>
      {busy ? (
        <ActivityIndicator size="small" color={tint} style={styles.rowIcon} />
      ) : (
        <View style={styles.rowIcon}>
          <CategoryIcon iconKey={iconKey} color={tint} size={20} />
        </View>
      )}
      <View style={styles.rowBody}>
        <Text style={[styles.rowTitle, { color: danger ? colors.danger : colors.textPrimary }]}>
          {label}
        </Text>
        {hint !== undefined && (
          <Text style={[styles.rowHint, { color: colors.textMuted }]}>{hint}</Text>
        )}
      </View>
      {trailing}
    </Pressable>
  );
}

function CheckRow({ label, checked, onPress }) {
  const colors = useTheme();

  return (
    <Pressable style={styles.row} onPress={onPress}>
      <View
        style={[
          styles.checkbox,
          {
            borderColor: checked ? colors.accent : colors.borderStrong,
            backgroundColor: checked ? colors.accent : 'transparent',
          },
        ]}
      >
        {checked && <CategoryIcon iconKey="Phosphor.check" color={colors.textOnAccent} size={12} />}
      </View>
      <Text style={[styles.rowTitle, { color: colors.textPrimary }]}>{label}</Text>
    </Pressable>
  );
}

export default function Options() {
  const colors = useTheme();
  const router = useRouter();
  const store = useVocabularyStore();
  const [baseUrl, setBaseUrl] = useState(store.apiBaseUrl);
  const [apiKey, setApiKey] = useState(store.apiKey);
  const [connectionMessage, setConnectionMessage] = useState('');
  const [isReindexing, setIsReindexing] = useState(false);

  const pendingCount = store.pendingEntryDeletions.length + store.pendingCategoryDeletions.length;
  const unsyncedCount = [...store.entries, ...store.categories].filter(needsPush).length;

  const statusColor =
    store.globalSyncStatus === 'Ok'
      ? colors.success
      : store.globalSyncStatus === 'Syncing'
        ? colors.warning
        : store.globalSyncStatus === 'Problem'
          ? colors.danger
          : colors.textMuted;

  const inputStyle = [
    styles.input,
    {
      color: colors.textPrimary,
      borderColor: colors.borderStrong,
      backgroundColor: colors.background,
    },
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

  async function handleReindexCategories() {
    setIsReindexing(true);
    const { status, result, errorDetail } = await createApiClient(
      store.apiBaseUrl,
      store.apiKey
    ).reindexCategoryEmbeddings();
    setIsReindexing(false);

    const message =
      status === 'Ok'
        ? `${result.embedded} recalculée(s), ${result.unchanged} déjà à jour, ${result.orphans_removed} orpheline(s) supprimée(s).`
        : status === 'NotConfigured'
          ? 'Cette action nécessite une synchronisation API configurée.'
          : errorDetail
            ? `Impossible de mettre à jour la catégorisation automatique : ${errorDetail}`
            : 'Impossible de mettre à jour la catégorisation automatique pour le moment. Réessaie plus tard.';

    Alert.alert('Mise à jour de la catégorisation automatique', message);
  }

  async function handleExport() {
    setConnectionMessage('');

    try {
      const fileName = await store.exportDatabase();

      if (fileName !== null) {
        Alert.alert('Exporter la base', `Base enregistrée sous ${fileName}.`);
      }
    } catch (error) {
      console.warn(`[export] ${String(error?.message ?? error)}`);
      Alert.alert('Exporter la base', 'Enregistrement impossible dans ce dossier.');
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
    <KeyboardAvoidingView style={styles.flex} behavior="padding">
      <ScrollView
        style={{ backgroundColor: colors.background }}
        contentContainerStyle={styles.content}
      >
        <SectionTitle>Synchronisation</SectionTitle>
        <Card>
          <InfoRow
            label="État"
            value={SYNC_STATUS_LABELS[store.globalSyncStatus] ?? store.globalSyncStatus}
            valueColor={statusColor}
          />
          <Divider />
          <InfoRow label="Dernière synchronisation" value={formatLastSynced(store.lastSyncedAt)} />
          <Divider />
          <ActionRow
            iconKey="Phosphor.clock"
            label="Historique de synchronisation"
            trailing={<CategoryIcon iconKey="Phosphor.caret-right" color={colors.textMuted} size={16} />}
            onPress={() => router.push('/sync-history')}
          />
        </Card>

        <SectionTitle>Serveur</SectionTitle>
        <Card>
          <View style={styles.field}>
            <Text style={[styles.fieldLabel, { color: colors.textSecondary }]}>
              Adresse du serveur
            </Text>
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
          </View>

          <View style={styles.field}>
            <Text style={[styles.fieldLabel, { color: colors.textSecondary }]}>Clé API</Text>
            <TextInput
              style={inputStyle}
              value={apiKey}
              onChangeText={setApiKey}
              placeholder="X-API-Key"
              placeholderTextColor={colors.textMuted}
              autoCapitalize="none"
              autoCorrect={false}
            />
          </View>

          <View style={styles.buttons}>
            <Pressable
              style={[styles.button, { borderColor: colors.borderStrong }]}
              onPress={handleTestConnection}
            >
              <Text style={[styles.buttonLabel, { color: colors.textPrimary }]}>Tester</Text>
            </Pressable>
            <Pressable
              style={[styles.button, { backgroundColor: colors.accent, borderColor: colors.accent }]}
              onPress={handleSync}
              disabled={store.isSyncing}
            >
              <Text style={[styles.buttonLabel, { color: colors.textOnAccent }]}>
                {store.isSyncing ? 'Synchronisation…' : 'Synchroniser'}
              </Text>
            </Pressable>
          </View>
        </Card>

        {connectionMessage.length > 0 && (
          <Text style={[styles.message, { color: colors.textSecondary }]}>{connectionMessage}</Text>
        )}
        {store.statusMessage.length > 0 && (
          <Text style={[styles.message, { color: colors.textSecondary }]}>
            {store.statusMessage}
          </Text>
        )}

        <SectionTitle>Maintenance</SectionTitle>
        <Card>
          <ActionRow
            iconKey="Phosphor.arrows-counter-clockwise"
            label={store.isFullResyncing ? 'Resynchronisation…' : 'Tout resynchroniser'}
            hint="Renvoie toutes les modifications locales et retélécharge les données divergentes. Après un changement de serveur ou en cas d’écart inexpliqué."
            busy={store.isFullResyncing}
            onPress={handleFullResync}
          />
          <Divider />
          <ActionRow
            iconKey="Phosphor.sparkle"
            label={isReindexing ? 'Actualisation…' : 'Actualiser la catégorisation'}
            hint="Recalcule les vecteurs des catégories dont le contenu a changé."
            busy={isReindexing}
            onPress={handleReindexCategories}
          />
        </Card>

        <SectionTitle>Affichage</SectionTitle>
        <Card>
          <View style={styles.cardHint}>
            <Hint>
              Sélections proposées en haut de l’écran Catégories. « Toutes les entrées » est
              toujours affichée, et « Sans catégorie » n’apparaît que s’il y a des entrées
              concernées.
            </Hint>
          </View>
          {CONFIGURABLE_VIRTUAL_CATEGORIES.map(({ kind, label }, index) => {
            const checked = store.virtualCategories[kind] !== false;

            return (
              <View key={kind}>
                {index > 0 && <Divider />}
                <CheckRow
                  label={label}
                  checked={checked}
                  onPress={() => store.setVirtualCategoryEnabled(kind, !checked)}
                />
              </View>
            );
          })}
        </Card>

        <SectionTitle>Données locales</SectionTitle>
        <Card>
          <InfoRow label="Entrées" value={String(store.entries.length)} />
          <Divider />
          <InfoRow label="Catégories" value={String(store.categories.length)} />
          {pendingCount > 0 && (
            <>
              <Divider />
              <InfoRow
                label="Suppressions en attente"
                value={String(pendingCount)}
                valueColor={colors.warning}
              />
            </>
          )}
          {unsyncedCount > 0 && (
            <>
              <Divider />
              <InfoRow
                label="Modifications non poussées"
                value={String(unsyncedCount)}
                valueColor={colors.warning}
              />
            </>
          )}
          <Divider />
          <ActionRow
            iconKey="Phosphor.floppy-disk"
            label="Exporter la base"
            hint="Pour initialiser un autre appareil sans repasser par le serveur."
            onPress={handleExport}
          />
          <Divider />
          <ActionRow
            iconKey="Phosphor.folder-open"
            label="Importer une base"
            hint="Remplace la base locale ; une sauvegarde de l’ancienne est conservée."
            onPress={handleImport}
          />
        </Card>

        <SectionTitle>Zone sensible</SectionTitle>
        <Card borderColor={colors.danger}>
          <ActionRow
            iconKey="Phosphor.trash"
            label="Effacer les données locales"
            hint="Les entrées non synchronisées seront perdues."
            danger
            onPress={handleReset}
          />
        </Card>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 14, paddingBottom: 28 },
  sectionTitle: {
    fontSize: 12,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginTop: 18,
    marginBottom: 8,
    paddingHorizontal: 4,
  },
  card: { borderWidth: 1, borderRadius: 14, overflow: 'hidden' },
  divider: { height: 1, marginHorizontal: 14 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 14, paddingVertical: 14 },
  rowIcon: { width: 22, alignItems: 'center' },
  rowBody: { flex: 1 },
  rowTitle: { fontSize: 15 },
  rowHint: { fontSize: 12, lineHeight: 16, marginTop: 3 },
  rowLabel: { flex: 1, fontSize: 15 },
  rowValue: { fontSize: 15, fontWeight: '600', maxWidth: '55%' },
  cardHint: { paddingHorizontal: 14, paddingTop: 14 },
  hint: { fontSize: 12, lineHeight: 17 },
  field: { paddingHorizontal: 14, paddingTop: 14 },
  fieldLabel: { fontSize: 12, fontWeight: '700', textTransform: 'uppercase', letterSpacing: 0.4, marginBottom: 6 },
  input: { borderWidth: 1, borderRadius: 10, paddingHorizontal: 12, paddingVertical: 10, fontSize: 15 },
  buttons: { flexDirection: 'row', gap: 10, padding: 14 },
  button: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderRadius: 10,
    paddingVertical: 12,
  },
  buttonLabel: { fontSize: 14, fontWeight: '600' },
  checkbox: {
    width: 20,
    height: 20,
    borderRadius: 6,
    borderWidth: 1.5,
    alignItems: 'center',
    justifyContent: 'center',
  },
  message: { fontSize: 13, marginTop: 10, paddingHorizontal: 4 },
});
