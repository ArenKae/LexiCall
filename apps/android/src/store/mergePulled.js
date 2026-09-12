import { isNewer } from '../utils/timestamps';

// Applies a delta pull onto the local records: drops tombstones, inserts or
// replaces anything genuinely newer, and marks whatever it keeps as already
// synced — a record just received from the server needs no push back, and
// leaving SyncedAt null would queue the entire first pull for re-upload.
export function mergePulled(local, pulled) {
  const byId = new Map(local.map((record) => [record.Id, record]));

  for (const record of pulled) {
    if (record.IsDeleted) {
      byId.delete(record.Id);
      continue;
    }

    const existing = byId.get(record.Id);

    if (!existing || isNewer(record.ClientLastWrite, existing.ClientLastWrite)) {
      byId.set(record.Id, { ...record, SyncedAt: record.ClientLastWrite });
    }
  }

  return [...byId.values()];
}
