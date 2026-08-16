# CRUD endpoints for vocabulary entries.
import base64
import binascii
from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException, Response, status

from lexicall_api import timestamps
from lexicall_api.config import settings
from lexicall_api.models.entry import VocabularyEntrySummary, VocabularyEntryWrite
from lexicall_api.repositories import categories_repo, entries_repo, entry_images_repo
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/entries", tags=["entries"], dependencies=[Depends(require_api_key)])


def _validate_category_ids(category_ids: list[str]) -> None:
    unknown = [cid for cid in category_ids if not categories_repo.category_exists(cid)]
    if unknown:
        raise HTTPException(status_code=400, detail=f"Unknown categor(y/ies): {', '.join(unknown)}")


def _decode_image(image_base64: str | None) -> bytes | None:
    # Decoded (and size-checked) BEFORE any Mongo write, so an invalid or
    # oversized image never leaves the entry written without its image, or
    # the other way around.
    if not image_base64:
        return None
    try:
        image_bytes = base64.b64decode(image_base64, validate=True)
    except (binascii.Error, ValueError) as error:
        raise HTTPException(status_code=400, detail="Invalid ImageBase64.") from error
    if len(image_bytes) > settings.max_image_bytes:
        raise HTTPException(status_code=413, detail="Image too large.")
    return image_bytes


@router.get("", response_model=list[VocabularyEntrySummary])
def list_entries(response: Response, updated_since: datetime | None = None) -> list[dict]:
    # Captured before the query runs, so a write that lands in between gets
    # picked up on the next pull instead of being missed by the checkpoint.
    response.headers["X-Sync-Timestamp"] = timestamps.now_iso()
    return entries_repo.list_entries(updated_since=updated_since)


@router.put("/{entry_id}", response_model=VocabularyEntrySummary)
def upsert_entry(entry_id: str, payload: VocabularyEntryWrite) -> dict:
    # The only write route for entries — PUT always upserts, so the client
    # never needs to know in advance whether entry_id already exists.
    _validate_category_ids(payload.category_ids)

    # Decoded (and size-checked) BEFORE any Mongo write, same reasoning as
    # the old single-image case: one invalid image must never leave the
    # entry written without a matching image, or the other way around.
    decoded_images = [(image.id, _decode_image(image.image_base64)) for image in payload.images]

    # The "before" snapshot — put_entry below only ever returns the "after"
    # state, so this is the only way to know which images to drop.
    previous_image_ids = entries_repo.get_current_image_ids(entry_id)

    data = payload.model_dump(by_alias=True, exclude={"images": {"__all__": {"image_base64"}}})
    entry, applied = entries_repo.put_entry(entry_id, data)

    # Skip the image diff if this push lost its Last-Write-Wins race —
    # otherwise a stale push could overwrite a more recent set of images.
    if applied:
        new_image_ids = {image_id for image_id, _bytes in decoded_images}
        for image_id, image_bytes in decoded_images:
            if image_bytes is not None:
                entry_images_repo.upsert_image(image_id, image_bytes, "image/jpeg")
        for stale_id in previous_image_ids - new_image_ids:
            entry_images_repo.delete_image(stale_id)
    return entry


@router.delete("/{entry_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_entry(entry_id: str, deleted_at: datetime | None = None) -> None:
    entry, applied = entries_repo.delete_entry(entry_id, deleted_at=deleted_at)
    if entry is None:
        raise HTTPException(status_code=404, detail="Entry not found.")
    # Skip the image cascade if this deletion lost its Last-Write-Wins race
    # (the entry was edited more recently elsewhere).
    if applied:
        for image in entry.get("Images", []):
            entry_images_repo.delete_image(image["Id"])


@router.get("/{entry_id}", response_model=VocabularyEntrySummary)
def get_entry(entry_id: str) -> dict:
    # Never called by the desktop client (it only does bulk pulls via
    # list_entries above) — kept for direct API inspection/debugging and as
    # a complete, conventional REST resource.
    entry = entries_repo.get_entry(entry_id)
    if entry is None:
        raise HTTPException(status_code=404, detail="Entry not found.")
    return entry
