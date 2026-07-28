# Data access for the `entry_images` collection: raw image bytes kept out of
# `entries` so a scan/listing of entries never pages image bytes into the
# WiredTiger cache. One document per entry Id, 1:1 with a VocabularyEntry.
from lexicall_api.database import get_entry_images_collection, strip_mongo_id


def get_image(entry_id: str) -> dict | None:
    doc = get_entry_images_collection().find_one({"Id": entry_id})
    return strip_mongo_id(doc) if doc else None


def upsert_image(entry_id: str, image_bytes: bytes, content_type: str) -> None:
    get_entry_images_collection().update_one(
        {"Id": entry_id},
        {"$set": {"ImageBytes": image_bytes, "ContentType": content_type}},
        upsert=True,
    )


def delete_image(entry_id: str) -> bool:
    result = get_entry_images_collection().delete_one({"Id": entry_id})
    return result.deleted_count > 0
