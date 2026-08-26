# AI enrichment routes — every LLM-backed suggestion feature from
# docs/Roadmap-AI-Enrichment.md lives under this one router as it's built.
import httpx
import openai
from fastapi import APIRouter, Depends, HTTPException

from lexicall_api import enrichment
from lexicall_api.models.enrichment import DefinitionSuggestionResult
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/enrichment", tags=["enrichment"], dependencies=[Depends(require_api_key)])


@router.get("/definition/{word}", response_model=DefinitionSuggestionResult)
def suggest_definition(word: str) -> dict:
    # Translated into a specific 502 detail instead of letting FastAPI's
    # generic 500 "Internal Server Error" swallow the real cause.
    try:
        result = enrichment.suggest_definition(word)
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
    return {"word": word, **result}
