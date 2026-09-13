// Local persistence: the vocabulary database and the app settings, each in its
// own JSON file under the app's document directory.
import { File, Paths } from 'expo-file-system';

const DATABASE_FILE = 'vocabulary.json';
const SETTINGS_FILE = 'settings.json';

const DEFAULT_SETTINGS = {
  apiBaseUrl: '',
  apiKey: '',
  lastPulledAt: null,
  // Server the sync state above (and every record's SyncedAt) was built
  // against. Both are meaningless against a different server, so a resync
  // finding a mismatch here wipes them and starts over.
  syncedAgainstBaseUrl: null,
};

function fileFor(name) {
  return new File(Paths.document, name);
}

async function readJson(name) {
  const file = fileFor(name);

  if (!file.exists) {
    return null;
  }

  try {
    const parsed = JSON.parse(await file.text());
    return parsed !== null && typeof parsed === 'object' ? parsed : null;
  } catch {
    return null;
  }
}

function writeJson(name, value) {
  const file = fileFor(name);

  if (!file.exists) {
    file.create({ intermediates: true });
  }
  file.write(JSON.stringify(value, null, 2));
}

export async function loadDatabase() {
  const stored = await readJson(DATABASE_FILE);

  return {
    Entries: Array.isArray(stored?.Entries) ? stored.Entries : [],
    Categories: Array.isArray(stored?.Categories) ? stored.Categories : [],
    // Deletions attempted but not yet confirmed by the API. Persisted with
    // the data itself: the record is already gone locally, so this queue is
    // the only remaining trace that the server still has to be told.
    PendingEntryDeletions: Array.isArray(stored?.PendingEntryDeletions)
      ? stored.PendingEntryDeletions
      : [],
    PendingCategoryDeletions: Array.isArray(stored?.PendingCategoryDeletions)
      ? stored.PendingCategoryDeletions
      : [],
  };
}

export function saveDatabase(database) {
  writeJson(DATABASE_FILE, database);
}

// Wipes the vocabulary database and the pull checkpoint, keeping the API
// credentials so the next sync can refill everything from the server.
export async function resetLocalData() {
  const database = fileFor(DATABASE_FILE);

  if (database.exists) {
    database.delete();
  }
  await saveSettings({ lastPulledAt: null });
}

export async function loadSettings() {
  return { ...DEFAULT_SETTINGS, ...(await readJson(SETTINGS_FILE)) };
}

// Merge-then-write rather than a plain overwrite: callers only ever hold the
// one field they are changing.
export async function saveSettings(patch) {
  const merged = { ...(await loadSettings()), ...patch };
  writeJson(SETTINGS_FILE, merged);
  return merged;
}

export function databaseFileUri() {
  return fileFor(DATABASE_FILE).uri;
}

function timestampSuffix() {
  return new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-');
}

// Writes the database to a freshly named cache file the share sheet can hand
// to another app. Cache rather than documents: this copy is disposable, the
// real database stays where it is.
export function stageDatabaseExport(database) {
  const file = new File(Paths.cache, `lexicall-${timestampSuffix()}.json`);

  if (file.exists) {
    file.delete();
  }
  file.create({ intermediates: true });
  file.write(JSON.stringify(database, null, 2));
  return file.uri;
}

// Parsed and shape-checked before anything is overwritten, so a wrong file
// picked by mistake fails with the database still intact.
function parseImportedDatabase(text) {
  let parsed;

  try {
    parsed = JSON.parse(text);
  } catch {
    throw new Error('Fichier illisible : ce n’est pas du JSON valide.');
  }

  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error('Fichier invalide : une base LexiCall est attendue.');
  }
  if (!Array.isArray(parsed.Entries) && !Array.isArray(parsed.Categories)) {
    throw new Error('Fichier invalide : ni entrées ni catégories trouvées.');
  }

  return {
    Entries: (Array.isArray(parsed.Entries) ? parsed.Entries : []).map(withClientLastWrite),
    Categories: (Array.isArray(parsed.Categories) ? parsed.Categories : []).map(withClientLastWrite),
    PendingEntryDeletions: Array.isArray(parsed.PendingEntryDeletions)
      ? parsed.PendingEntryDeletions
      : [],
    PendingCategoryDeletions: Array.isArray(parsed.PendingCategoryDeletions)
      ? parsed.PendingCategoryDeletions
      : [],
  };
}

// A file exported before the rename carries the edit time under the old key
// "UpdatedAt". Copied across on the way in, so an imported record keeps a real
// Last-Write-Wins arbiter instead of an undefined one that loses every race.
function withClientLastWrite(record) {
  return record?.ClientLastWrite === undefined && record?.UpdatedAt !== undefined
    ? { ...record, ClientLastWrite: record.UpdatedAt }
    : record;
}

// Replaces the local database with a picked file, keeping a timestamped copy
// of what was there next to it. The source is parsed and checked first, so a
// wrong file picked by mistake leaves the database untouched.
export async function importDatabaseFrom(sourceFile) {
  const database = parseImportedDatabase(await sourceFile.text());
  const current = fileFor(DATABASE_FILE);

  if (current.exists) {
    const backup = new File(Paths.document, `vocabulary.backup-${timestampSuffix()}.json`);

    if (!backup.exists) {
      backup.create({ intermediates: true });
    }
    backup.write(await current.text());
  }

  writeJson(DATABASE_FILE, database);
  return database;
}

// Opens the system picker and returns the chosen file, or null if dismissed.
export async function pickDatabaseFile() {
  const picked = await File.pickFileAsync({ mimeTypes: ['application/json'] });
  return picked.canceled ? null : picked.result;
}
