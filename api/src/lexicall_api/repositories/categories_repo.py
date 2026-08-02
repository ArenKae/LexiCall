# Data access for the `categories` collection, keyed by the application Id
# field rather than Mongo's native _id.
from datetime import datetime, timezone

from pymongo import ReturnDocument
from pymongo.errors import DuplicateKeyError

from lexicall_api import timestamps
from lexicall_api.database import get_categories_collection, strip_mongo_id


def list_categories(updated_since: datetime | None = None) -> list[dict]:
    # No updated_since: live view, tombstones excluded. With it: delta pull
    # that includes tombstones too, since that's how a deletion reaches
    # another client.
    query = (
        {"UpdatedAt": {"$gt": timestamps.to_iso_utc(updated_since)}}
        if updated_since is not None
        else {"IsDeleted": {"$ne": True}}
    )
    docs = get_categories_collection().find(query).sort("UpdatedAt", 1)
    return [strip_mongo_id(doc) for doc in docs]


def list_ids() -> set[str]:
    return set(get_categories_collection().distinct("Id"))


def get_category(category_id: str) -> dict | None:
    doc = get_categories_collection().find_one({"Id": category_id, "IsDeleted": {"$ne": True}})
    return strip_mongo_id(doc) if doc else None


def _get_category_raw(category_id: str) -> dict | None:
    # Includes tombstoned categories — for internal use where a
    # soft-deleted record still needs to be found by Id.
    doc = get_categories_collection().find_one({"Id": category_id})
    return strip_mongo_id(doc) if doc else None


def category_exists(category_id: str) -> bool:
    # Live only: a tombstoned category must no longer be a valid target for
    # a new ParentId or CategoryIds.
    return get_categories_collection().count_documents(
        {"Id": category_id, "IsDeleted": {"$ne": True}}, limit=1
    ) > 0


def creates_cycle(category_id: str, parent_id: str | None) -> bool:
    """True if assigning parent_id as the parent of category_id would create
    a cycle (parent_id == category_id, or category_id is an ancestor of
    parent_id). Pure structure traversal, no IsDeleted filter — a candidate
    parent's liveness is checked separately by category_exists."""
    visited: set[str] = set()
    current = parent_id
    while current is not None:
        if current == category_id:
            return True
        if current in visited:
            return False  # pre-existing cycle unrelated to category_id
        visited.add(current)
        doc = get_categories_collection().find_one({"Id": current}, {"ParentId": 1})
        current = doc.get("ParentId") if doc else None
    return False


def has_children(category_id: str) -> bool:
    # Live only: a child that's already tombstoned must no longer block its
    # parent's deletion.
    return get_categories_collection().count_documents(
        {"ParentId": category_id, "IsDeleted": {"$ne": True}}, limit=1
    ) > 0


def put_category(category_id: str, data: dict) -> dict:
    """True upsert: creates the category if unknown, otherwise updates it
    only if the incoming UpdatedAt is newer (Last-Write-Wins). A losing
    write still attempts an insert, which collides with the unique index on
    Id and raises DuplicateKeyError — the signal that this was a stale push
    against an existing category, not a genuine creation.

    Unlike entries, this returns just the document: there's no image to
    gate on whether the push actually won."""
    incoming = timestamps.to_iso_utc(data.get("UpdatedAt")) or timestamps.now_iso()
    # Routed through $setOnInsert below so an edit can never overwrite it.
    created_at = timestamps.to_iso_utc(data.pop("CreatedAt", None)) or incoming
    try:
        result = get_categories_collection().find_one_and_update(
            {"Id": category_id, "UpdatedAt": {"$lt": incoming}},
            {
                "$set": {**data, "UpdatedAt": incoming},
                "$setOnInsert": {"Id": category_id, "CreatedAt": created_at, "IsDeleted": False},
            },
            upsert=True,
            return_document=ReturnDocument.AFTER,
        )
        return strip_mongo_id(result)
    except DuplicateKeyError:
        return _get_category_raw(category_id)


def delete_category(category_id: str, deleted_at: datetime | None = None) -> dict | None:
    """Soft-delete: sets IsDeleted instead of removing the document. Never
    an upsert — an unknown Id must stay a 404. TombstonedAt is a real BSON
    Date so MongoDB's TTL index can auto-expire old tombstones."""
    incoming = timestamps.to_iso_utc(deleted_at) or timestamps.now_iso()
    result = get_categories_collection().find_one_and_update(
        {"Id": category_id, "UpdatedAt": {"$lt": incoming}},
        {"$set": {"IsDeleted": True, "UpdatedAt": incoming, "TombstonedAt": datetime.now(timezone.utc)}},
        return_document=ReturnDocument.AFTER,
    )
    return strip_mongo_id(result) if result is not None else _get_category_raw(category_id)


def upsert_category(doc: dict) -> str:
    """Idempotent upsert by Id for the one-shot JSON migration: keeps the
    document's original CreatedAt/UpdatedAt as-is and matches on Id
    regardless of IsDeleted, so a tombstone gets updated in place instead of
    colliding with the unique index."""
    result = get_categories_collection().update_one({"Id": doc["Id"]}, {"$set": doc}, upsert=True)
    if result.upserted_id is not None:
        return "inserted"
    return "updated" if result.modified_count > 0 else "unchanged"
