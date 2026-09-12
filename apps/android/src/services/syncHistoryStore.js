// Read/write for sync_history.json — its own file, kept apart from
// settings.json: append-mostly operational data, not a preference.
import { File, Paths } from 'expo-file-system';

export const MAX_HISTORY_ENTRIES = 200;

const HISTORY_FILE = 'sync_history.json';

function historyFile() {
  return new File(Paths.document, HISTORY_FILE);
}

export async function loadSyncHistory() {
  const file = historyFile();

  if (!file.exists) {
    return [];
  }

  try {
    const parsed = JSON.parse(await file.text());
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    // A corrupted history file must never block startup.
    return [];
  }
}

// Entries come in newest-first; trimming here gives every caller the cap.
export function saveSyncHistory(entries) {
  const file = historyFile();

  if (!file.exists) {
    file.create({ intermediates: true });
  }
  file.write(JSON.stringify(entries.slice(0, MAX_HISTORY_ENTRIES)));
}
