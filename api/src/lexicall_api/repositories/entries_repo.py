# Data access for the `entries` collection. Every lookup/update happens via
# the application field Id, never via Mongo's native _id (see database.py).
from datetime import datetime, timezone

from pymongo import ReturnDocument
from pymongo.errors import DuplicateKeyError

from lexicall_api import timestamps
from lexicall_api.database import get_entries_collection, strip_mongo_id


def list_entries(updated_since: datetime | None = None) -> list[dict]:
    # Without updated_since: classic "live" view, tombstones excluded. With
    # it: delta pull (LWW sync) — includes tombstones, the one channel a
    # deletion has to propagate to another client.
    query = (
        {"UpdatedAt": {"$gt": timestamps.to_iso_utc(updated_since)}}
        if updated_since is not None
        else {"IsDeleted": {"$ne": True}}
    )
    docs = get_entries_collection().find(query).sort("UpdatedAt", 1)
    return [strip_mongo_id(doc) for doc in docs]


def list_ids() -> set[str]:
    return set(get_entries_collection().distinct("Id"))


def get_entry(entry_id: str) -> dict | None:
    doc = get_entries_collection().find_one({"Id": entry_id, "IsDeleted": {"$ne": True}})
    return strip_mongo_id(doc) if doc else None


def _get_entry_raw(entry_id: str) -> dict | None:
    # Unfiltered (tombstones included) — internal use only. delete_entry
    # relies on the None case to distinguish "unknown Id" (404) from "exists
    # but this delete lost the CAS comparison" (no-op). put_entry only ever
    # calls this after a DuplicateKeyError, where the Id is already known to
    # exist, so it never actually sees None there in practice.
    doc = get_entries_collection().find_one({"Id": entry_id})
    return strip_mongo_id(doc) if doc else None


def put_entry(entry_id: str, data: dict) -> tuple[dict, bool]:
    """Backs PUT /entries/{id}: a true upsert, not just an update — creates
    the entry if entry_id is unknown, otherwise applies the same conditional
    write (CAS) used everywhere else in this module (Last-Write-Wins). This
    is what lets the client always PUT, never needing a separate POST
    fallback for a never-before-synced entry.

    $setOnInsert supplies the fields that must exist only on a genuine
    insert (CreatedAt, IsDeleted) — $set alone would also overwrite them on
    every ordinary update, which is exactly the CreatedAt corruption
    VocabularyEntryWrite's field layout is designed to prevent.

    Returns (document, applied): applied is False if entry_id already
    existed but THIS push lost the CAS comparison — a losing push isn't a
    failure for the router (always 200), but side effects tied to it (e.g.
    the image, see routers/entries.py) must only apply when applied is
    True, or a stale push could overwrite a more recent state its own
    metadata never got to touch.

    A losing push is detected indirectly: the CAS filter matches nothing
    (either entry_id is unknown, or it exists with a newer UpdatedAt
    already), so Mongo attempts an insert either way. If entry_id was
    already taken, that insert collides with the unique index on Id and
    raises DuplicateKeyError — the signal that this was actually a stale
    push against an existing entry, not a genuine creation. That's the
    entire distinction this function needs; no separate existence check.

    data.pop("CreatedAt", ...): pulled out of $set and routed through
    $setOnInsert instead — $set and $setOnInsert can't both reference the
    same field in a single Mongo update (a hard error), and $set would
    apply it on every ordinary update too, corrupting it."""
    incoming = timestamps.to_iso_utc(data.get("UpdatedAt")) or timestamps.now_iso()
    created_at = timestamps.to_iso_utc(data.pop("CreatedAt", None)) or incoming
    try:
        result = get_entries_collection().find_one_and_update(
            {"Id": entry_id, "UpdatedAt": {"$lt": incoming}},
            {
                "$set": {**data, "UpdatedAt": incoming},
                "$setOnInsert": {"Id": entry_id, "CreatedAt": created_at, "IsDeleted": False},
            },
            upsert=True,
            return_document=ReturnDocument.AFTER,
        )
        return strip_mongo_id(result), True
    except DuplicateKeyError:
        return _get_entry_raw(entry_id), False


def delete_entry(entry_id: str, deleted_at: datetime | None = None) -> tuple[dict | None, bool]:
    """Deletion = tombstone (same CAS mechanism as put_entry, different
    $set, but no upsert — deleting a truly unknown Id must stay a 404, not
    create a tombstone out of nothing): IsDeleted instead of a delete_one,
    so a delta pull can propagate the deletion to a client that hasn't seen
    it yet. Returns (document, applied) — see put_entry; a DELETE that
    loses the CAS comparison
    (e.g. the entry was actually edited more recently elsewhere) must not
    cascade to the image either.

    TombstonedAt is a genuine BSON Date (unlike UpdatedAt/CreatedAt, which
    stay ISO-8601 strings for lexicographic CAS comparisons) — it exists
    solely to back the TTL index in database.ensure_indexes(), which needs a
    real Date field to auto-expire old tombstones. It's set once, at
    tombstone time, and never touched again."""
    incoming = timestamps.to_iso_utc(deleted_at) or timestamps.now_iso()
    result = get_entries_collection().find_one_and_update(
        {"Id": entry_id, "UpdatedAt": {"$lt": incoming}},
        {"$set": {"IsDeleted": True, "UpdatedAt": incoming, "TombstonedAt": datetime.now(timezone.utc)}},
        return_document=ReturnDocument.AFTER,
    )
    if result is not None:
        return strip_mongo_id(result), True
    return _get_entry_raw(entry_id), False


def count_entries_using_category(category_id: str) -> int:
    return get_entries_collection().count_documents(
        {"CategoryIds": category_id, "IsDeleted": {"$ne": True}}
    )


def list_entries_with_inline_image_field() -> list[dict]:
    # Documents from before entry_images existed still carry ImageBase64,
    # even as an empty string when the entry never had an image — the
    # split-in-place migration must clear the field either way, read
    # straight from Mongo rather than from a JSON export.
    docs = get_entries_collection().find({"ImageBase64": {"$exists": True}}, {"Id": 1, "ImageBase64": 1})
    return [strip_mongo_id(doc) for doc in docs]


def clear_inline_image(entry_id: str) -> None:
    get_entries_collection().update_one({"Id": entry_id}, {"$unset": {"ImageBase64": ""}})


def upsert_entry(doc: dict) -> str:
    """Used by the migration: idempotent upsert by Id, preserves the
    document's original CreatedAt/UpdatedAt (no regeneration). Deliberately
    not filtered by IsDeleted: an existing tombstone must still be findable
    by Id so the upsert updates it in place instead of hitting the unique
    index via an insert.
    $set rather than replace_one: only $set does a field-by-field comparison
    and reports modified_count=0 for content that's genuinely unchanged —
    replace_one reports modified_count>0 even when writing identical content.
    $unset ImageBase64: cleans up the legacy inline-image field left over on
    documents migrated before images moved to entry_images_repo.py; a no-op
    once a document no longer has it."""
    result = get_entries_collection().update_one(
        {"Id": doc["Id"]}, {"$set": doc, "$unset": {"ImageBase64": ""}}, upsert=True
    )
    if result.upserted_id is not None:
        return "inserted"
    return "updated" if result.modified_count > 0 else "unchanged"
