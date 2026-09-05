# CRUD endpoints for vocabulary categories.
from datetime import datetime

import openai
from fastapi import APIRouter, Depends, HTTPException, Response, status

from lexicall_api import category_indexing, timestamps
from lexicall_api.models.category import (
    CategoryReindexResult,
    VocabularyCategory,
    VocabularyCategoryWrite,
)
from lexicall_api.repositories import categories_repo, category_embeddings_repo, entries_repo
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/categories", tags=["categories"], dependencies=[Depends(require_api_key)])


def _validate_parent(category_id: str | None, parent_id: str | None) -> None:
    if parent_id is None:
        return
    if not categories_repo.category_exists(parent_id):
        raise HTTPException(status_code=400, detail="Unknown parent category.")
    if category_id is not None and categories_repo.creates_cycle(category_id, parent_id):
        raise HTTPException(status_code=400, detail="This parent would create a category cycle.")


@router.post("/reindex-embeddings", response_model=CategoryReindexResult)
def reindex_embeddings() -> dict:
    # The repair path for embeddings that drifted: the refresh following a
    # category write is best-effort and swallows its own failures, so
    # without this the only fix would be running the indexing script by hand
    # on the server. Explicit user action, so failures surface as a 502
    # rather than being swallowed the way the write-time refresh is.
    try:
        summary = category_indexing.reindex_all()
    except openai.AuthenticationError as exc:
        raise HTTPException(502, f"Clé API OpenAI refusée : {exc}") from exc
    except openai.RateLimitError as exc:
        raise HTTPException(502, f"Limite de requêtes OpenAI atteinte : {exc}") from exc
    except openai.APITimeoutError as exc:
        raise HTTPException(502, "Le modèle OpenAI n'a pas répondu à temps.") from exc
    except openai.OpenAIError as exc:
        raise HTTPException(502, f"Erreur OpenAI : {exc}") from exc
    except RuntimeError as exc:
        raise HTTPException(502, str(exc)) from exc
    return {
        "embedded": summary.embedded,
        "unchanged": summary.unchanged,
        "orphans_removed": summary.orphans_removed,
    }


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
    category = categories_repo.put_category(category_id, payload.model_dump(by_alias=True))
    # Reads the stored state back rather than the incoming payload, so a
    # push that lost the Last-Write-Wins race re-embeds nothing.
    category_indexing.refresh_subtree(category_id)
    return category


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
    # Cascade, same as deleting an entry drops its image: a tombstoned
    # category must stop showing up as a categorization candidate.
    category_embeddings_repo.delete_embedding(category_id)


@router.get("/{category_id}", response_model=VocabularyCategory)
def get_category(category_id: str) -> dict:
    # Never called by the desktop client (it only does bulk pulls via
    # list_categories above) — kept for direct API inspection/debugging and
    # as a complete, conventional REST resource.
    category = categories_repo.get_category(category_id)
    if category is None:
        raise HTTPException(status_code=404, detail="Category not found.")
    return category
