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


def strip_mongo_id(doc: dict) -> dict:
    doc = dict(doc)
    doc.pop("_id", None)
    return doc
