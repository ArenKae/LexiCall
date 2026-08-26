# Raw French Wiktionary lookup for a word — no LLM involved, session 1.2
# builds the definition-suggestion feature on top of fetch_definition_context.
from fastapi import APIRouter, Depends

from lexicall_api import wiktionary_client
from lexicall_api.models.wiktionary import WiktionaryLookupResult
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/wiktionary", tags=["wiktionary"], dependencies=[Depends(require_api_key)])


@router.get("/{word}", response_model=WiktionaryLookupResult)
def lookup_word(word: str) -> dict:
    wikitext = wiktionary_client.fetch_definition_context(word)
    return {"word": word, "wikitext": wikitext}
