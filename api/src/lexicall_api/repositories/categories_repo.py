# Data access for the `categories` collection. Replicates server-side the
# guards already present client-side in MainWindowViewModel.DeleteCategory
# (apps/windows/src/ViewModels/MainWindowViewModel.cs) — necessary as soon as
# there's a second writer, the desktop app is no longer the sole source of truth.
import uuid
from datetime import datetime, timezone

from pymongo import ReturnDocument

from lexicall_api.database import get_categories_collection, strip_mongo_id


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def list_categories() -> list[dict]:
    return [strip_mongo_id(doc) for doc in get_categories_collection().find({})]


def list_ids() -> set[str]:
    return set(get_categories_collection().distinct("Id"))


def get_category(category_id: str) -> dict | None:
    doc = get_categories_collection().find_one({"Id": category_id})
    return strip_mongo_id(doc) if doc else None


def category_exists(category_id: str) -> bool:
    return get_categories_collection().count_documents({"Id": category_id}, limit=1) > 0


def creates_cycle(category_id: str, parent_id: str | None) -> bool:
    """True if assigning parent_id as the parent of category_id would create a
    cycle (parent_id == category_id, or category_id is an ancestor of parent_id)."""
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
    return get_categories_collection().count_documents({"ParentId": category_id}, limit=1) > 0


def create_category(data: dict) -> dict:
    # data may carry a client-supplied Id (e.g. a category created offline by
    # the desktop app, synced later) — preserve it so it doesn't diverge from
    # the client's own copy; generate one only if none was supplied.
    category_id = data.get("Id") or str(uuid.uuid4())
    doc = {**data, "Id": category_id, "CreatedAt": _now_iso(), "UpdatedAt": _now_iso()}
    get_categories_collection().insert_one(doc)
    return strip_mongo_id(doc)


def update_category(category_id: str, data: dict) -> dict | None:
    result = get_categories_collection().find_one_and_update(
        {"Id": category_id},
        {"$set": {**data, "UpdatedAt": _now_iso()}},
        return_document=ReturnDocument.AFTER,
    )
    return strip_mongo_id(result) if result else None


def delete_category(category_id: str) -> bool:
    result = get_categories_collection().delete_one({"Id": category_id})
    return result.deleted_count > 0


def upsert_category(doc: dict) -> str:
    """Used by the migration: idempotent upsert by Id, preserves the
    document's original CreatedAt/UpdatedAt (no regeneration).
    $set rather than replace_one: only $set does a field-by-field comparison
    and reports modified_count=0 for content that's genuinely unchanged —
    replace_one reports modified_count>0 even when writing identical content."""
    result = get_categories_collection().update_one({"Id": doc["Id"]}, {"$set": doc}, upsert=True)
    if result.upserted_id is not None:
        return "inserted"
    return "updated" if result.modified_count > 0 else "unchanged"
