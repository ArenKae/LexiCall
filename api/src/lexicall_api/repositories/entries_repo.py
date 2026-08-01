# Data access for the `entries` collection. Every lookup/update happens via
# the application field Id, never via Mongo's native _id (see database.py).
import uuid
from datetime import datetime

from pymongo import ReturnDocument

from lexicall_api import timestamps
from lexicall_api.database import get_entries_collection, strip_mongo_id


def list_entries(updated_since: datetime | None = None) -> list[dict]:
    # Sans updated_since : vue "live" classique, tombstones exclus. Avec :
    # pull différentiel (sync LWW) — inclut les tombstones, c'est le seul
    # canal par lequel une suppression se propage à un autre client.
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
    # Non filtré (tombstones inclus) — usage interne uniquement, par
    # update_entry/delete_entry pour distinguer "Id inconnu" de "l'Id existe
    # mais le push a perdu la comparaison CAS" (voir leurs docstrings).
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


def update_entry(entry_id: str, data: dict) -> dict | None:
    """Écriture conditionnelle (CAS) : le $set ne s'applique que si le
    timestamp entrant est plus récent que celui déjà stocké (Last-Write-Wins).
    Si la comparaison est perdue (ou si l'Id n'existe pas), retourne l'état
    actuel du document — un push perdant n'est pas un échec, la convergence
    se fait au prochain pull ; seul un Id réellement inconnu doit devenir un
    404 côté routeur."""
    incoming = timestamps.to_iso_utc(data.get("UpdatedAt")) or timestamps.now_iso()
    result = get_entries_collection().find_one_and_update(
        {"Id": entry_id, "UpdatedAt": {"$lt": incoming}},
        {"$set": {**data, "UpdatedAt": incoming}},
        return_document=ReturnDocument.AFTER,
    )
    return strip_mongo_id(result) if result is not None else _get_entry_raw(entry_id)


def delete_entry(entry_id: str, deleted_at: datetime | None = None) -> dict | None:
    """Suppression = tombstone (même mécanisme CAS que update_entry, $set
    différent) : IsDeleted plutôt qu'un delete_one, pour qu'un pull
    différentiel puisse propager la suppression à un client qui ne l'a pas
    encore vue."""
    incoming = timestamps.to_iso_utc(deleted_at) or timestamps.now_iso()
    result = get_entries_collection().find_one_and_update(
        {"Id": entry_id, "UpdatedAt": {"$lt": incoming}},
        {"$set": {"IsDeleted": True, "UpdatedAt": incoming}},
        return_document=ReturnDocument.AFTER,
    )
    return strip_mongo_id(result) if result is not None else _get_entry_raw(entry_id)


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
    document's original CreatedAt/UpdatedAt (no regeneration). Filtre non
    filtré par IsDeleted à dessein : un tombstone existant doit rester
    trouvable par Id pour que l'upsert le mette à jour en place plutôt que
    de heurter l'index unique via un insert.
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
