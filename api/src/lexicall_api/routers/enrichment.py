# AI enrichment routes — every LLM-backed suggestion feature from
# docs/Roadmap-AI-Enrichment.md lives under this one router as it's built.
from fastapi import APIRouter, Depends

from lexicall_api import enrichment
from lexicall_api.models.enrichment import DefinitionSuggestionResult
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/enrichment", tags=["enrichment"], dependencies=[Depends(require_api_key)])


@router.get("/definition/{word}", response_model=DefinitionSuggestionResult)
def suggest_definition(word: str) -> dict:
    result = enrichment.suggest_definition(word)
    return {"word": word, **result}
