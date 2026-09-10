# Data access for the `category_embeddings` collection: vectors kept out of
# `categories` so the client's sync pull never pages them into the WiredTiger
# cache. Keyed by the category's own Id. SourceText is the exact text that
# produced the vector — comparing it is how callers skip re-embedding a
# category whose name, path and description haven't changed.
from database import get_category_embeddings_collection, strip_mongo_id


def get_embedding(category_id: str) -> dict | None:
    doc = get_category_embeddings_collection().find_one({"Id": category_id})
    return strip_mongo_id(doc) if doc else None


def list_embeddings() -> list[dict]:
    docs = get_category_embeddings_collection().find({})
    return [strip_mongo_id(doc) for doc in docs]


def upsert_embedding(category_id: str, vector: list[float], source_text: str) -> None:
    get_category_embeddings_collection().update_one(
        {"Id": category_id},
        {"$set": {"Vector": vector, "SourceText": source_text}},
        upsert=True,
    )


def delete_embedding(category_id: str) -> bool:
    result = get_category_embeddings_collection().delete_one({"Id": category_id})
    return result.deleted_count > 0
