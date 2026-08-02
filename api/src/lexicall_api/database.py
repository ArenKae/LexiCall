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
    # Unique on the application Id (not Mongo's _id): fast lookups, and
    # guarantees no two documents share the same Id.
    get_entries_collection().create_index("Id", unique=True)
    get_categories_collection().create_index("Id", unique=True)
    get_entry_images_collection().create_index("Id", unique=True)

    # Speeds up the updated_since delta query used for sync pulls.
    get_entries_collection().create_index("UpdatedAt")
    get_categories_collection().create_index("UpdatedAt")

    # TTL: MongoDB auto-deletes a document once TombstonedAt is older than
    # this many seconds. Only tombstones carry that field, so live records
    # are never touched. 30 days is generous next to the 60s client resync,
    # but a client offline longer than that could miss a deletion.
    get_entries_collection().create_index("TombstonedAt", expireAfterSeconds=30 * 24 * 60 * 60)
    get_categories_collection().create_index("TombstonedAt", expireAfterSeconds=30 * 24 * 60 * 60)


def strip_mongo_id(doc: dict) -> dict:
    doc = dict(doc)
    doc.pop("_id", None)
    return doc
