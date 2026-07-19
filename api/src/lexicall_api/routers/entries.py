# CRUD endpoints for vocabulary entries.
from fastapi import APIRouter, Depends, HTTPException, status

from lexicall_api.models.entry import VocabularyEntry, VocabularyEntrySummary, VocabularyEntryWrite
from lexicall_api.repositories import categories_repo, entries_repo
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/entries", tags=["entries"], dependencies=[Depends(require_api_key)])


def _validate_category_ids(category_ids: list[str]) -> None:
    unknown = [cid for cid in category_ids if not categories_repo.category_exists(cid)]
    if unknown:
        raise HTTPException(status_code=400, detail=f"Unknown categor(y/ies): {', '.join(unknown)}")


@router.get("", response_model=list[VocabularyEntrySummary])
def list_entries() -> list[dict]:
    return entries_repo.list_entries()


@router.get("/{entry_id}", response_model=VocabularyEntry)
def get_entry(entry_id: str) -> dict:
    entry = entries_repo.get_entry(entry_id)
    if entry is None:
        raise HTTPException(status_code=404, detail="Entry not found.")
    return entry


@router.post("", response_model=VocabularyEntry, status_code=status.HTTP_201_CREATED)
def create_entry(payload: VocabularyEntryWrite) -> dict:
    _validate_category_ids(payload.category_ids)
    return entries_repo.create_entry(payload.model_dump(by_alias=True))


@router.put("/{entry_id}", response_model=VocabularyEntry)
def update_entry(entry_id: str, payload: VocabularyEntryWrite) -> dict:
    _validate_category_ids(payload.category_ids)
    entry = entries_repo.update_entry(entry_id, payload.model_dump(by_alias=True))
    if entry is None:
        raise HTTPException(status_code=404, detail="Entry not found.")
    return entry


@router.delete("/{entry_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_entry(entry_id: str) -> None:
    if not entries_repo.delete_entry(entry_id):
        raise HTTPException(status_code=404, detail="Entry not found.")
