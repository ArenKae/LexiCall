# Response models for the AI enrichment routes (routers/enrichment.py).
from pydantic import BaseModel

from lexicall_api.models.entry import VocabularyEntryType


class TextFieldSuggestion(BaseModel):
    value: str
    justification: str | None = None


class TypeFieldSuggestion(BaseModel):
    value: VocabularyEntryType
    justification: str | None = None


class ListFieldSuggestion(BaseModel):
    value: list[str]
    justification: str | None = None


class EntryEnrichmentSuggestions(BaseModel):
    definition: TextFieldSuggestion | None = None
    type: TypeFieldSuggestion | None = None
    synonyms: ListFieldSuggestion | None = None
    example_sentences: ListFieldSuggestion | None = None
