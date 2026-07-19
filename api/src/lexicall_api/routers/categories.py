# CRUD endpoints for vocabulary categories.
from fastapi import APIRouter, Depends, HTTPException, status
from pymongo.errors import DuplicateKeyError

from lexicall_api.models.category import VocabularyCategory, VocabularyCategoryCreate, VocabularyCategoryWrite
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
def list_categories() -> list[dict]:
    return categories_repo.list_categories()


@router.get("/{category_id}", response_model=VocabularyCategory)
def get_category(category_id: str) -> dict:
    category = categories_repo.get_category(category_id)
    if category is None:
        raise HTTPException(status_code=404, detail="Category not found.")
    return category


@router.post("", response_model=VocabularyCategory, status_code=status.HTTP_201_CREATED)
def create_category(payload: VocabularyCategoryCreate) -> dict:
    _validate_parent(payload.id, payload.parent_id)
    try:
        return categories_repo.create_category(payload.model_dump(by_alias=True))
    except DuplicateKeyError as error:
        raise HTTPException(status_code=409, detail="A category with this Id already exists.") from error


@router.put("/{category_id}", response_model=VocabularyCategory)
def update_category(category_id: str, payload: VocabularyCategoryWrite) -> dict:
    _validate_parent(category_id, payload.parent_id)
    category = categories_repo.update_category(category_id, payload.model_dump(by_alias=True))
    if category is None:
        raise HTTPException(status_code=404, detail="Category not found.")
    return category


@router.delete("/{category_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_category(category_id: str) -> None:
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
    categories_repo.delete_category(category_id)
