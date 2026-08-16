# Data access for the `entry_images` collection: raw image bytes kept out of
# `entries` so a scan/listing of entries never pages image bytes into the
# WiredTiger cache. Each document has its own Id (client-generated, like
# every other Id in this codebase) — an entry references zero to several of
# them via its own Images: [{Id, Caption}] array, not a shared key.
from lexicall_api.database import get_entry_images_collection, strip_mongo_id


def get_image(image_id: str) -> dict | None:
    doc = get_entry_images_collection().find_one({"Id": image_id})
    return strip_mongo_id(doc) if doc else None


def upsert_image(image_id: str, image_bytes: bytes, content_type: str) -> None:
    get_entry_images_collection().update_one(
        {"Id": image_id},
        {"$set": {"ImageBytes": image_bytes, "ContentType": content_type}},
        upsert=True,
    )


def delete_image(image_id: str) -> bool:
    result = get_entry_images_collection().delete_one({"Id": image_id})
    return result.deleted_count > 0
