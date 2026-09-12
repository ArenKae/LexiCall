import { isNewer } from '../utils/timestamps';

// One full catch-up cycle against the API. Returns what happened instead of
// mutating anything, so the caller decides what to keep.

export function needsPush(record) {
  return record.SyncedAt === null || isNewer(record.ClientLastWrite, record.SyncedAt);
}

export async function runSyncCycle({
  client,
  entries,
  categories,
  pendingEntryDeletions,
  pendingCategoryDeletions,
  lastPulledAt,
}) {
  const status = await client.testConnection();

  if (status !== 'Ok') {
    // No point firing hundreds of upserts that would all fail anyway.
    return { status, reachable: false, deletions: [], pushes: [], pull: null };
  }

  const deletions = [];

  // Entries before categories, the reverse of the push order below: the
  // server refuses to delete a category an entry still references, and an
  // entry only stops counting once its own tombstone has landed.
  for (const pending of pendingEntryDeletions) {
    const success = await client.deleteEntry(pending.Id, pending.DeletedAt);
    deletions.push({ entityType: 'Entry', pending, success });
  }
  for (const pending of pendingCategoryDeletions) {
    const success = await client.deleteCategory(pending.Id, pending.DeletedAt);
    deletions.push({ entityType: 'Category', pending, success });
  }

  const pushes = [];

  // Categories before entries: the API validates an entry's CategoryIds
  // against the categories it already knows about.
  for (const category of categories.filter(needsPush)) {
    const success = await client.upsertCategory(category);
    pushes.push({ entityType: 'Category', record: category, success });
  }
  for (const entry of entries.filter(needsPush)) {
    const success = await client.upsertEntry(entry);
    pushes.push({ entityType: 'Entry', record: entry, success });
  }

  try {
    const pulledCategories = await client.pullCategories(lastPulledAt);
    const pulledEntries = await client.pullEntries(lastPulledAt);

    return {
      status,
      reachable: true,
      deletions,
      pushes,
      pull: {
        categories: pulledCategories.records,
        entries: pulledEntries.records,
        // The categories pull ran first, so its token is the older of the
        // two: taking the entries one would skip a category written between
        // the two requests, permanently.
        checkpoint: pulledCategories.syncTimestamp ?? lastPulledAt,
      },
    };
  } catch (error) {
    console.warn(`[sync] pull failed: ${String(error?.message ?? error)}`);
    return { status, reachable: true, deletions, pushes, pull: null };
  }
}
