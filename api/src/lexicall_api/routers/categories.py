# CRUD endpoints for vocabulary categories.
from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException, Response, status

from lexicall_api import timestamps
from lexicall_api.models.category import VocabularyCategory, VocabularyCategoryWrite
from lexicall_api.repositories import categories_repo, entries_repo
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/categories", tags=["categories"], dependencies=[Depends(require_api_key)])


def _validate_parent(category_id: str | None, parent_id: str | None) -> None:
    if parent_id is None:
        return
    if not categories_repo.category_exists(parent_id):
        raise HTTPException(status_code=400, detail="Unknown parent category.")
    if category_id is not None and categories_repo.creates_cycle(category_id, parent_id):
        raise HTTPException(status_code=400, detail="This parent would create a category cycle.")


@router.get("", response_model=list[VocabularyCategory])
def list_categories(response: Response, updated_since: datetime | None = None) -> list[dict]:
    # Captured before the query runs, so a write that lands in between gets
    # picked up on the next pull instead of being missed by the checkpoint.
    response.headers["X-Sync-Timestamp"] = timestamps.now_iso()
    return categories_repo.list_categories(updated_since=updated_since)


@router.put("/{category_id}", response_model=VocabularyCategory)
def upsert_category(category_id: str, payload: VocabularyCategoryWrite) -> dict:
    # The only write route for categories — PUT always upserts, so the
    # client never needs to know in advance whether category_id already exists.
    _validate_parent(category_id, payload.parent_id)
    return categories_repo.put_category(category_id, payload.model_dump(by_alias=True))


@router.delete("/{category_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_category(category_id: str, deleted_at: datetime | None = None) -> None:
    if categories_repo.get_category(category_id) is None:
        raise HTTPException(status_code=404, detail="Category not found.")
    if categories_repo.has_children(category_id):
        raise HTTPException(
            status_code=409,
            detail="Cannot delete a category that contains subcategories.",
        )
    usage_count = entries_repo.count_entries_using_category(category_id)
    if usage_count > 0:
        raise HTTPException(
            status_code=409,
            detail=f"Cannot delete: this category is used by {usage_count} word(s).",
        )
    categories_repo.delete_category(category_id, deleted_at=deleted_at)


@router.get("/{category_id}", response_model=VocabularyCategory)
def get_category(category_id: str) -> dict:
    # Never called by the desktop client (it only does bulk pulls via
    # list_categories above) — kept for direct API inspection/debugging and
    # as a complete, conventional REST resource.
    category = categories_repo.get_category(category_id)
    if category is None:
        raise HTTPException(status_code=404, detail="Category not found.")
    return category
