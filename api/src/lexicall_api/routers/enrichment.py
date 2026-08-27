# AI enrichment routes — every LLM-backed suggestion feature from
# docs/Roadmap-AI-Enrichment.md lives under this one router as it's built.
import httpx
import openai
from fastapi import APIRouter, Depends, HTTPException

from lexicall_api import enrichment
from lexicall_api.models.enrichment import EntryEnrichmentRequest, EntryEnrichmentSuggestions
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/enrichment", tags=["enrichment"], dependencies=[Depends(require_api_key)])


# Takes the current field values in the request body rather than looking the
# entry up by id: this must also work for a brand-new, not-yet-saved draft
# (the whole point of enriching a word while it's still being typed in),
# which has no server-side record to fetch. suggest_entry_enrichment() only
# ever needed a plain dict, so it's unaffected either way.
@router.post("/fields", response_model=EntryEnrichmentSuggestions, response_model_exclude_none=True)
def suggest_entry_fields(payload: EntryEnrichmentRequest) -> dict:
    # mode="json" so the Type enum serializes to its plain string value
    # ("Verbe", ...), matching the raw-dict shape suggest_entry_enrichment
    # already expects (e.g. from debug_pipeline.py's synthetic entries).
    entry = payload.model_dump(mode="json", by_alias=True)
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
