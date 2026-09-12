// Shapes records received from the API into the local database shape, filling
// the defaults the rest of the app relies on.
import { UNDEFINED_TYPE } from '../utils/vocabularyEntryTypes';

function asArray(value) {
  return Array.isArray(value) ? value : [];
}

function asText(value) {
  return typeof value === 'string' ? value : '';
}

export function normalizeEntry(raw) {
  const senses = asArray(raw.Definition).filter((sense) => asText(sense).trim().length > 0);
  const types = asArray(raw.Type).filter((type) => asText(type).length > 0);

  return {
    Id: raw.Id,
    Word: asText(raw.Word),
    Type: types.length > 0 ? types : [UNDEFINED_TYPE],
    // Screens and the editor assume at least one sense row exists.
    Definition: senses.length > 0 ? senses : [''],
    CategoryIds: asArray(raw.CategoryIds),
    Synonyms: asArray(raw.Synonyms),
    ExampleSentences: asArray(raw.ExampleSentences),
    Notes: asText(raw.Notes),
    Source: asText(raw.Source),
    // A pull carries image metadata only, never the bytes.
    Images: asArray(raw.Images).map((image) => ({
      Id: image.Id,
      Caption: asText(image.Caption),
      ImageBase64: asText(image.ImageBase64),
    })),
    // Timestamps are stored exactly as received: re-serializing them through a
    // JS Date would truncate microseconds to milliseconds and make the record
    // look older than the copy the server holds. ClientLastWrite is this
    // device's own edit time, used for Last-Write-Wins — never the server's
    // arrival time, which the API never sends to any client.
    CreatedAt: raw.CreatedAt,
    ClientLastWrite: raw.ClientLastWrite,
    IsArchived: raw.IsArchived === true,
    LockedFields: asArray(raw.LockedFields),
    IsDeleted: raw.IsDeleted === true,
    SyncedAt: raw.SyncedAt ?? null,
  };
}

export function normalizeCategory(raw) {
  return {
    Id: raw.Id,
    Name: asText(raw.Name),
    ParentId: raw.ParentId ?? null,
    Description: asText(raw.Description),
    IconGlyph: asText(raw.IconGlyph),
    // Stored exactly as received, for the same reason as on an entry: a JS
    // Date round-trip would drop precision the server still compares against.
    CreatedAt: raw.CreatedAt,
    ClientLastWrite: raw.ClientLastWrite,
    IsDeleted: raw.IsDeleted === true,
    SyncedAt: raw.SyncedAt ?? null,
  };
}
