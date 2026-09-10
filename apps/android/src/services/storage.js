// Local persistence: the vocabulary database and the app settings, each in its
// own JSON file under the app's document directory.
import { File, Paths } from 'expo-file-system';

const DATABASE_FILE = 'vocabulary.json';
const SETTINGS_FILE = 'settings.json';

const DEFAULT_SETTINGS = {
  apiBaseUrl: '',
  apiKey: '',
  lastPulledAt: null,
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
  };
}

export function saveDatabase(database) {
  writeJson(DATABASE_FILE, database);
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
