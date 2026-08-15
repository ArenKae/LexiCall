# Shared MongoDB client (synchronous PyMongo) and collection access.
import logging
import time

from pymongo import MongoClient
from pymongo.collection import Collection
from pymongo.errors import PyMongoError

from lexicall_api.config import settings

logger = logging.getLogger(__name__)

# Bounded to 5s (PyMongo's own default is 30s): MongoDB is now a separately
# managed shared instance, so a slow/unreachable server should fail fast —
# both for the startup retry loop below and for any live request made while
# it's down — rather than hang for 30s per attempt.
_client: MongoClient = MongoClient(settings.mongo_uri, serverSelectionTimeoutMS=5000)
_db = _client[settings.mongo_db_name]

_INDEX_RETRY_ATTEMPTS = 5
_INDEX_RETRY_DELAY_SECONDS = 2


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
    # Retries a bounded number of times so a shared, separately-managed
    # Mongo instance that happens to start after the API doesn't crash the
    # container on its very first index-create call. Worst case ~5*(5s
    # client timeout)+4*2s =~33s before giving up and letting Docker's
    # `restart: unless-stopped` backoff take over. Catches PyMongoError
    # specifically — a real programming bug should still crash immediately.
    last_error: PyMongoError | None = None
    for attempt in range(1, _INDEX_RETRY_ATTEMPTS + 1):
        try:
            _create_indexes()
            return
        except PyMongoError as exc:
            last_error = exc
            if attempt < _INDEX_RETRY_ATTEMPTS:
                logger.warning(
                    "ensure_indexes: Mongo not ready (attempt %d/%d), retrying in %ds: %s",
                    attempt, _INDEX_RETRY_ATTEMPTS, _INDEX_RETRY_DELAY_SECONDS, exc,
                )
                time.sleep(_INDEX_RETRY_DELAY_SECONDS)
    assert last_error is not None
    raise last_error


def _create_indexes() -> None:
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
