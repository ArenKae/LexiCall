# CRUD endpoints for vocabulary entries.
from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException, Response, status
from pymongo.errors import DuplicateKeyError

from lexicall_api import timestamps
from lexicall_api.models.entry import VocabularyEntryCreate, VocabularyEntrySummary, VocabularyEntryWrite
from lexicall_api.repositories import categories_repo, entries_repo, entry_images_repo
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/entries", tags=["entries"], dependencies=[Depends(require_api_key)])


def _validate_category_ids(category_ids: list[str]) -> None:
    unknown = [cid for cid in category_ids if not categories_repo.category_exists(cid)]
    if unknown:
        raise HTTPException(status_code=400, detail=f"Unknown categor(y/ies): {', '.join(unknown)}")


@router.get("", response_model=list[VocabularyEntrySummary])
def list_entries(response: Response, updated_since: datetime | None = None) -> list[dict]:
    # Capturé AVANT la requête : toute écriture atterrissant entre la capture
    # et l'exécution de la requête aura un UpdatedAt >= ce timestamp, donc
    # sera simplement récupérée au PROCHAIN pull plutôt que d'être ratée par
    # un checkpoint trop optimiste.
    response.headers["X-Sync-Timestamp"] = timestamps.now_iso()
    return entries_repo.list_entries(updated_since=updated_since)


@router.get("/{entry_id}", response_model=VocabularyEntrySummary)
def get_entry(entry_id: str) -> dict:
    entry = entries_repo.get_entry(entry_id)
    if entry is None:
        raise HTTPException(status_code=404, detail="Entry not found.")
    return entry


@router.post("", response_model=VocabularyEntrySummary, status_code=status.HTTP_201_CREATED)
def create_entry(payload: VocabularyEntryCreate) -> dict:
    _validate_category_ids(payload.category_ids)
    try:
        return entries_repo.create_entry(payload.model_dump(by_alias=True))
    except DuplicateKeyError as error:
        raise HTTPException(status_code=409, detail="An entry with this Id already exists.") from error


@router.put("/{entry_id}", response_model=VocabularyEntrySummary)
def update_entry(entry_id: str, payload: VocabularyEntryWrite) -> dict:
    _validate_category_ids(payload.category_ids)
    entry = entries_repo.update_entry(entry_id, payload.model_dump(by_alias=True))
    if entry is None:
        raise HTTPException(status_code=404, detail="Entry not found.")
    return entry


@router.delete("/{entry_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_entry(entry_id: str, deleted_at: datetime | None = None) -> None:
    if entries_repo.delete_entry(entry_id, deleted_at=deleted_at) is None:
        raise HTTPException(status_code=404, detail="Entry not found.")
    entry_images_repo.delete_image(entry_id)
