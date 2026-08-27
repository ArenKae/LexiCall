# AI enrichment routes — every LLM-backed suggestion feature from
# docs/Roadmap-AI-Enrichment.md lives under this one router as it's built.
import httpx
import openai
from fastapi import APIRouter, Depends, HTTPException

from lexicall_api import enrichment
from lexicall_api.models.enrichment import EntryEnrichmentSuggestions
from lexicall_api.repositories import entries_repo
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/enrichment", tags=["enrichment"], dependencies=[Depends(require_api_key)])


@router.get("/fields/{entry_id}", response_model=EntryEnrichmentSuggestions, response_model_exclude_none=True)
def suggest_entry_fields(entry_id: str) -> dict:
    entry = entries_repo.get_entry(entry_id)
    if entry is None:
        raise HTTPException(404, "Entry not found.")
    # Translated into a specific 502 detail instead of letting FastAPI's
    # generic 500 "Internal Server Error" swallow the real cause.
    try:
        return enrichment.suggest_entry_enrichment(entry)
    except openai.AuthenticationError as exc:
        raise HTTPException(502, f"Clé API OpenAI refusée : {exc}") from exc
    except openai.RateLimitError as exc:
        raise HTTPException(502, f"Limite de requêtes OpenAI atteinte : {exc}") from exc
    except openai.APITimeoutError as exc:
        raise HTTPException(502, "Le modèle OpenAI n'a pas répondu à temps.") from exc
    except openai.OpenAIError as exc:
        raise HTTPException(502, f"Erreur OpenAI : {exc}") from exc
    except httpx.HTTPError as exc:
        raise HTTPException(502, f"Erreur Wiktionnaire : {exc}") from exc
    except RuntimeError as exc:
        raise HTTPException(502, str(exc)) from exc
