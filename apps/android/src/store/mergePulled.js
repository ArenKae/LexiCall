import { isNewer } from '../utils/timestamps';

// Applies a delta pull onto the local records: drops tombstones, inserts or
// replaces anything genuinely newer, and marks whatever it keeps as already
// synced — a record just received from the server needs no push back, and
// leaving SyncedAt null would queue the entire first pull for re-upload.
// Returns the count it actually changed alongside the records: a full pull
// carries everything, most of which lands on an identical local copy.
export function mergePulled(local, pulled) {
  const byId = new Map(local.map((record) => [record.Id, record]));
  let applied = 0;

  for (const record of pulled) {
    if (record.IsDeleted) {
      if (byId.delete(record.Id)) {
        applied++;
      }
      continue;
    }

    const existing = byId.get(record.Id);

    if (!existing || isNewer(record.ClientLastWrite, existing.ClientLastWrite)) {
      byId.set(record.Id, { ...record, SyncedAt: record.ClientLastWrite });
      applied++;
    }
  }

  return { records: [...byId.values()], applied };
}
