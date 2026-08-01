# Data access for the `categories` collection. Replicates server-side the
# guards already present client-side in MainWindowViewModel.DeleteCategory
# (apps/windows/src/ViewModels/MainWindowViewModel.cs) — necessary as soon as
# there's a second writer, the desktop app is no longer the sole source of truth.
import uuid
from datetime import datetime

from pymongo import ReturnDocument

from lexicall_api import timestamps
from lexicall_api.database import get_categories_collection, strip_mongo_id


def list_categories(updated_since: datetime | None = None) -> list[dict]:
    # Voir entries_repo.list_entries : même logique, tombstones exclus sans
    # updated_since, inclus avec (pull différentiel).
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
    # Non filtré (tombstones inclus) — usage interne, voir
    # entries_repo._get_entry_raw pour la justification.
    doc = get_categories_collection().find_one({"Id": category_id})
    return strip_mongo_id(doc) if doc else None


def category_exists(category_id: str) -> bool:
    # Live uniquement : une catégorie tombstonée ne doit plus être une cible
    # valide pour un nouveau ParentId ou CategoryIds.
    return get_categories_collection().count_documents(
        {"Id": category_id, "IsDeleted": {"$ne": True}}, limit=1
    ) > 0


def creates_cycle(category_id: str, parent_id: str | None) -> bool:
    """True if assigning parent_id as the parent of category_id would create a
    cycle (parent_id == category_id, or category_id is an ancestor of parent_id).
    Pure traversée de structure : pas de filtre IsDeleted, la liveness d'un
    parent candidat est déjà tranchée séparément par category_exists."""
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
    # Live uniquement : un enfant déjà tombstoné ne doit plus bloquer la
    # suppression de son parent.
    return get_categories_collection().count_documents(
        {"ParentId": category_id, "IsDeleted": {"$ne": True}}, limit=1
    ) > 0


def create_category(data: dict) -> dict:
    # data may carry a client-supplied Id (e.g. a category created offline by
    # the desktop app, synced later) — preserve it so it doesn't diverge from
    # the client's own copy; generate one only if none was supplied.
    category_id = data.get("Id") or str(uuid.uuid4())
    now = timestamps.now_iso()
    created_at = timestamps.to_iso_utc(data.get("CreatedAt")) or now
    updated_at = timestamps.to_iso_utc(data.get("UpdatedAt")) or now
    doc = {**data, "Id": category_id, "CreatedAt": created_at, "UpdatedAt": updated_at, "IsDeleted": False}
    get_categories_collection().insert_one(doc)
    return strip_mongo_id(doc)


def update_category(category_id: str, data: dict) -> dict | None:
    """Écriture conditionnelle (CAS) — voir entries_repo.update_entry pour la
    justification complète."""
    incoming = timestamps.to_iso_utc(data.get("UpdatedAt")) or timestamps.now_iso()
    result = get_categories_collection().find_one_and_update(
        {"Id": category_id, "UpdatedAt": {"$lt": incoming}},
        {"$set": {**data, "UpdatedAt": incoming}},
        return_document=ReturnDocument.AFTER,
    )
    return strip_mongo_id(result) if result is not None else _get_category_raw(category_id)


def delete_category(category_id: str, deleted_at: datetime | None = None) -> dict | None:
    """Suppression = tombstone — voir entries_repo.delete_entry."""
    incoming = timestamps.to_iso_utc(deleted_at) or timestamps.now_iso()
    result = get_categories_collection().find_one_and_update(
        {"Id": category_id, "UpdatedAt": {"$lt": incoming}},
        {"$set": {"IsDeleted": True, "UpdatedAt": incoming}},
        return_document=ReturnDocument.AFTER,
    )
    return strip_mongo_id(result) if result is not None else _get_category_raw(category_id)


def upsert_category(doc: dict) -> str:
    """Used by the migration: idempotent upsert by Id, preserves the
    document's original CreatedAt/UpdatedAt (no regeneration). Non filtré par
    IsDeleted à dessein — voir entries_repo.upsert_entry.
    $set rather than replace_one: only $set does a field-by-field comparison
    and reports modified_count=0 for content that's genuinely unchanged —
    replace_one reports modified_count>0 even when writing identical content."""
    result = get_categories_collection().update_one({"Id": doc["Id"]}, {"$set": doc}, upsert=True)
    if result.upserted_id is not None:
        return "inserted"
    return "updated" if result.modified_count > 0 else "unchanged"
