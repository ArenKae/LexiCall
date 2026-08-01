# Data access for the `entries` collection. Every lookup/update happens via
# the application field Id, never via Mongo's native _id (see database.py).
import uuid
from datetime import datetime

from pymongo import ReturnDocument

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
    # Unfiltered (tombstones included) — internal use only, by
    # update_entry/delete_entry to distinguish "unknown Id" from "the Id
    # exists but the push lost the CAS comparison" (see their docstrings).
    doc = get_entries_collection().find_one({"Id": entry_id})
    return strip_mongo_id(doc) if doc else None


def create_entry(data: dict) -> dict:
    # data may carry a client-supplied Id (e.g. an entry created offline by
    # the desktop app, synced later) — preserve it so it doesn't diverge from
    # the client's own copy; generate one only if none was supplied.
    entry_id = data.get("Id") or str(uuid.uuid4())
    now = timestamps.now_iso()
    created_at = timestamps.to_iso_utc(data.get("CreatedAt")) or now
    updated_at = timestamps.to_iso_utc(data.get("UpdatedAt")) or now
    doc = {**data, "Id": entry_id, "CreatedAt": created_at, "UpdatedAt": updated_at, "IsDeleted": False}
    get_entries_collection().insert_one(doc)
    return strip_mongo_id(doc)


def update_entry(entry_id: str, data: dict) -> tuple[dict | None, bool]:
    """Conditional write (CAS): the $set only applies if the incoming
    timestamp is newer than what's already stored (Last-Write-Wins).
    Returns (document, applied): applied is False if the Id exists but THIS
    push lost the CAS comparison (the returned document is then the current
    winning version, not the result of this push) — a losing push isn't a
    failure for the router (always 200), but side effects tied to it (e.g.
    the image, see routers/entries.py) must only apply when applied is True,
    or a stale push could overwrite a more recent state its own metadata
    never got to touch. document is None only when the Id doesn't exist at
    all — the one case that should become a 404 in the router."""
    incoming = timestamps.to_iso_utc(data.get("UpdatedAt")) or timestamps.now_iso()
    result = get_entries_collection().find_one_and_update(
        {"Id": entry_id, "UpdatedAt": {"$lt": incoming}},
        {"$set": {**data, "UpdatedAt": incoming}},
        return_document=ReturnDocument.AFTER,
    )
    if result is not None:
        return strip_mongo_id(result), True
    return _get_entry_raw(entry_id), False


def delete_entry(entry_id: str, deleted_at: datetime | None = None) -> tuple[dict | None, bool]:
    """Deletion = tombstone (same CAS mechanism as update_entry, different
    $set): IsDeleted instead of a delete_one, so a delta pull can propagate
    the deletion to a client that hasn't seen it yet. Returns (document,
    applied) — see update_entry; a DELETE that loses the CAS comparison
    (e.g. the entry was actually edited more recently elsewhere) must not
    cascade to the image either."""
    incoming = timestamps.to_iso_utc(deleted_at) or timestamps.now_iso()
    result = get_entries_collection().find_one_and_update(
        {"Id": entry_id, "UpdatedAt": {"$lt": incoming}},
        {"$set": {"IsDeleted": True, "UpdatedAt": incoming}},
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
