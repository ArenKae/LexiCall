# Response models for the AI enrichment routes (routers/enrichment.py).
from pydantic import BaseModel

from lexicall_api.models.entry import VocabularyEntryType


class DefinitionSuggestionResult(BaseModel):
    word: str
    definition: str
    type: VocabularyEntryType
