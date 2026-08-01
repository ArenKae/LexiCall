# Shared MongoDB client (synchronous PyMongo) and collection access.
from pymongo import MongoClient
from pymongo.collection import Collection

from lexicall_api.config import settings

_client: MongoClient = MongoClient(settings.mongo_uri)
_db = _client[settings.mongo_db_name]


def get_entries_collection() -> Collection:
    return _db["entries"]


def get_categories_collection() -> Collection:
    return _db["categories"]


def get_entry_images_collection() -> Collection:
    return _db["entry_images"]


def ping() -> bool:
    try:
        _client.admin.command("ping")
        return True
    except Exception:
        return False


def ensure_indexes() -> None:
    # Application Id distinct from Mongo's native _id: unique index so that
    # lookups by Id (used everywhere across the API and migration) stay fast
    # and so two documents can never share the same Id.
    get_entries_collection().create_index("Id", unique=True)
    get_categories_collection().create_index("Id", unique=True)
    get_entry_images_collection().create_index("Id", unique=True)

    # Non-unique: serves the updated_since delta query (LWW sync), so a pull
    # doesn't require a full collection scan.
    get_entries_collection().create_index("UpdatedAt")
    get_categories_collection().create_index("UpdatedAt")

    # TTL: MongoDB's background expiry monitor auto-deletes a document once
    # this field is older than expireAfterSeconds — but only documents that
    # actually carry the field, which only tombstones do (TombstonedAt is set
    # once, at delete time, in entries_repo.delete_entry/categories_repo.
    # delete_category). Live documents are never touched. 30 days is a
    # generous window given the periodic resync client-side (60s while the
    # app is open): a client that hasn't reconnected in over a month would
    # miss the tombstone and could see a stale record reappear, an accepted
    # tradeoff for a personal-scale app. Once purged, the Id becomes free
    # again for a new record (no more DuplicateKeyError on that Id).
    get_entries_collection().create_index("TombstonedAt", expireAfterSeconds=30 * 24 * 60 * 60)
    get_categories_collection().create_index("TombstonedAt", expireAfterSeconds=30 * 24 * 60 * 60)


def strip_mongo_id(doc: dict) -> dict:
    doc = dict(doc)
    doc.pop("_id", None)
    return doc
