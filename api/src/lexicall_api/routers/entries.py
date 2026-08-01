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
    # Captured BEFORE the query runs: any write landing between this capture
    # and the query's execution will have an UpdatedAt >= this timestamp, so
    # it's simply picked up on the NEXT pull instead of being missed by an
    # over-optimistic checkpoint.
    response.headers["X-Sync-Timestamp"] = timestamps.now_iso()
    return entries_repo.list_entries(updated_since=updated_since)


@router.put("/{entry_id}", response_model=VocabularyEntrySummary)
def upsert_entry(entry_id: str, payload: VocabularyEntryWrite) -> dict:
    # True upsert (see entries_repo.put_entry) — the client always PUTs,
    # whether entry_id is brand new or already exists. This is the only
    # write path for entries; there's no separate POST/create-only endpoint
    # (see put_entry's docstring for why PUT alone is enough).
    _validate_category_ids(payload.category_ids)
    image_bytes = _decode_image(payload.image_base64)
    data = payload.model_dump(by_alias=True, exclude={"image_base64"})
    entry, applied = entries_repo.put_entry(entry_id, data)

    # Only touch the image if THIS push actually took effect — either a
    # fresh insert or a genuine update, both count (see put_entry). A push
    # that lost the CAS comparison against an existing, more recent entry
    # must not overwrite its image either, even though its own metadata was
    # just rejected by that same mechanism.
    if applied:
        if image_bytes is not None:
            entry_images_repo.upsert_image(entry_id, image_bytes, "image/jpeg")
        else:
            entry_images_repo.delete_image(entry_id)
    return entry


@router.delete("/{entry_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_entry(entry_id: str, deleted_at: datetime | None = None) -> None:
    entry, applied = entries_repo.delete_entry(entry_id, deleted_at=deleted_at)
    if entry is None:
        raise HTTPException(status_code=404, detail="Entry not found.")
    # Same CAS gating as upsert_entry above — a delete that lost the race
    # (the entry was actually edited more recently elsewhere) must not
    # cascade to the image either.
    if applied:
        entry_images_repo.delete_image(entry_id)


@router.get("/{entry_id}", response_model=VocabularyEntrySummary)
def get_entry(entry_id: str) -> dict:
    # Never called by the desktop client (it only does bulk pulls via
    # list_entries above) — kept for direct API inspection/debugging and as
    # a complete, conventional REST resource.
    entry = entries_repo.get_entry(entry_id)
    if entry is None:
        raise HTTPException(status_code=404, detail="Entry not found.")
    return entry
