# Response model for GET /wiktionary/{word}.
from pydantic import BaseModel


class WiktionaryLookupResult(BaseModel):
    word: str
    wikitext: str | None
