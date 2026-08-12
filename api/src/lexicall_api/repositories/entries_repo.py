# Data access for the `entries` collection, keyed by the application Id
# field rather than Mongo's native _id.
from datetime import datetime, timezone

from pymongo import ReturnDocument
from pymongo.errors import DuplicateKeyError

from lexicall_api import timestamps
from lexicall_api.database import get_entries_collection, strip_mongo_id


def list_entries(updated_since: datetime | None = None) -> list[dict]:
    # No updated_since: live view, tombstones excluded. With it: delta pull
    # that includes tombstones too, since that's how a deletion reaches
    # another client.
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
    # Includes tombstoned entries — for internal use where a soft-deleted
    # record still needs to be found by Id.
    doc = get_entries_collection().find_one({"Id": entry_id})
    return strip_mongo_id(doc) if doc else None


def put_entry(entry_id: str, data: dict) -> tuple[dict, bool]:
    """True upsert: creates the entry if entry_id is unknown, otherwise
    updates it only if the incoming UpdatedAt is newer (Last-Write-Wins).
    Returns (document, applied); applied is False when the stored UpdatedAt
    was already newer, in which case the current document is returned
    unchanged.

    A losing write still attempts an insert, since the filter above matched
    nothing — that collides with the unique index on Id and raises
    DuplicateKeyError, which is how a stale push is told apart from a
    genuine creation."""
    incoming = timestamps.to_iso_utc(data.get("UpdatedAt")) or timestamps.now_iso()
    # Routed through $setOnInsert below so an edit can never overwrite it.
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
    """Soft-delete: sets IsDeleted instead of removing the document, so a
    delta pull can tell other clients about the deletion. Same CAS-gated
    write as put_entry, but never an upsert — an unknown Id must stay a 404,
    not create a tombstone out of nothing. Returns (document, applied);
    applied is False if the entry was edited more recently elsewhere and the
    deletion lost that race.

    TombstonedAt is a real BSON Date (unlike the ISO-string UpdatedAt/
    CreatedAt) so MongoDB's TTL index can auto-expire old tombstones."""
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
    # Legacy entries may still carry ImageBase64 (even as an empty string)
    # from before images moved to their own collection; used by the
    # split-in-place migration to find and clear them.
    docs = get_entries_collection().find({"ImageBase64": {"$exists": True}}, {"Id": 1, "ImageBase64": 1})
    return [strip_mongo_id(doc) for doc in docs]


def clear_inline_image(entry_id: str) -> None:
    get_entries_collection().update_one({"Id": entry_id}, {"$unset": {"ImageBase64": ""}})


def upsert_entry(doc: dict) -> str:
    """Idempotent upsert by Id for the one-shot JSON migration: keeps the
    document's original CreatedAt/UpdatedAt as-is, matches on Id regardless
    of IsDeleted so a tombstone gets updated in place instead of colliding
    with the unique index, and clears any legacy inline ImageBase64 field."""
    result = get_entries_collection().update_one(
        {"Id": doc["Id"]}, {"$set": doc, "$unset": {"ImageBase64": ""}}, upsert=True
    )
    if result.upserted_id is not None:
        return "inserted"
    return "updated" if result.modified_count > 0 else "unchanged"
