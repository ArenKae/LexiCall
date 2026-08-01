# Data access for the `categories` collection. Replicates server-side the
# guards already present client-side in MainWindowViewModel.DeleteCategory
# (apps/windows/src/ViewModels/MainWindowViewModel.cs) — necessary as soon as
# there's a second writer, the desktop app is no longer the sole source of truth.
from datetime import datetime

from pymongo import ReturnDocument
from pymongo.errors import DuplicateKeyError

from lexicall_api import timestamps
from lexicall_api.database import get_categories_collection, strip_mongo_id


def list_categories(updated_since: datetime | None = None) -> list[dict]:
    # See entries_repo.list_entries: same logic, tombstones excluded without
    # updated_since, included with it (delta pull).
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
    # Unfiltered (tombstones included) — internal use, see
    # entries_repo._get_entry_raw for the rationale.
    doc = get_categories_collection().find_one({"Id": category_id})
    return strip_mongo_id(doc) if doc else None


def category_exists(category_id: str) -> bool:
    # Live only: a tombstoned category must no longer be a valid target for
    # a new ParentId or CategoryIds.
    return get_categories_collection().count_documents(
        {"Id": category_id, "IsDeleted": {"$ne": True}}, limit=1
    ) > 0


def creates_cycle(category_id: str, parent_id: str | None) -> bool:
    """True if assigning parent_id as the parent of category_id would create a
    cycle (parent_id == category_id, or category_id is an ancestor of parent_id).
    Pure structure traversal: no IsDeleted filter, a candidate parent's
    liveness is already decided separately by category_exists."""
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
    """Backs PUT /categories/{id}: a true upsert — see entries_repo.put_entry
    for the full rationale (CAS, $setOnInsert, DuplicateKeyError as the
    stale-push signal). No (document, applied) tuple here, unlike put_entry:
    categories have no side-effect resource (no image) that would need to
    know whether this specific push actually won, so a plain document is
    enough — a losing push is already harmless on its own (the router just
    returns the current winning document, no cascade to gate).

    data.pop("CreatedAt", ...): see entries_repo.put_entry for why this
    can't stay in $set alongside $setOnInsert."""
    incoming = timestamps.to_iso_utc(data.get("UpdatedAt")) or timestamps.now_iso()
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
    """Deletion = tombstone, no upsert (an unknown Id must stay a 404) —
    see entries_repo.delete_entry."""
    incoming = timestamps.to_iso_utc(deleted_at) or timestamps.now_iso()
    result = get_categories_collection().find_one_and_update(
        {"Id": category_id, "UpdatedAt": {"$lt": incoming}},
        {"$set": {"IsDeleted": True, "UpdatedAt": incoming}},
        return_document=ReturnDocument.AFTER,
    )
    return strip_mongo_id(result) if result is not None else _get_category_raw(category_id)


def upsert_category(doc: dict) -> str:
    """Used by the migration: idempotent upsert by Id, preserves the
    document's original CreatedAt/UpdatedAt (no regeneration). Deliberately
    not filtered by IsDeleted — see entries_repo.upsert_entry.
    $set rather than replace_one: only $set does a field-by-field comparison
    and reports modified_count=0 for content that's genuinely unchanged —
    replace_one reports modified_count>0 even when writing identical content."""
    result = get_categories_collection().update_one({"Id": doc["Id"]}, {"$set": doc}, upsert=True)
    if result.upserted_id is not None:
        return "inserted"
    return "updated" if result.modified_count > 0 else "unchanged"
