# Data access for the `entries` collection. Every lookup/update happens via
# the application field Id, never via Mongo's native _id (see database.py).
import uuid
from datetime import datetime, timezone

from pymongo import ReturnDocument

from lexicall_api.database import get_entries_collection, strip_mongo_id


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def list_entries() -> list[dict]:
    # Projection without ImageBase64: avoids loading every image blob on a
    # plain list.
    docs = get_entries_collection().find({}, {"ImageBase64": 0})
    return [strip_mongo_id(doc) for doc in docs]


def list_ids() -> set[str]:
    return set(get_entries_collection().distinct("Id"))


def get_entry(entry_id: str) -> dict | None:
    doc = get_entries_collection().find_one({"Id": entry_id})
    return strip_mongo_id(doc) if doc else None


def create_entry(data: dict) -> dict:
    # data may carry a client-supplied Id (e.g. an entry created offline by
    # the desktop app, synced later) — preserve it so it doesn't diverge from
    # the client's own copy; generate one only if none was supplied.
    entry_id = data.get("Id") or str(uuid.uuid4())
    doc = {**data, "Id": entry_id, "CreatedAt": _now_iso(), "UpdatedAt": _now_iso()}
    get_entries_collection().insert_one(doc)
    return strip_mongo_id(doc)


def update_entry(entry_id: str, data: dict) -> dict | None:
    result = get_entries_collection().find_one_and_update(
        {"Id": entry_id},
        {"$set": {**data, "UpdatedAt": _now_iso()}},
        return_document=ReturnDocument.AFTER,
    )
    return strip_mongo_id(result) if result else None


def delete_entry(entry_id: str) -> bool:
    result = get_entries_collection().delete_one({"Id": entry_id})
    return result.deleted_count > 0


def count_entries_using_category(category_id: str) -> int:
    return get_entries_collection().count_documents({"CategoryIds": category_id})


def upsert_entry(doc: dict) -> str:
    """Used by the migration: idempotent upsert by Id, preserves the
    document's original CreatedAt/UpdatedAt (no regeneration).
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
