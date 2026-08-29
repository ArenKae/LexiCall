# Request/response models for the AI enrichment routes (routers/enrichment.py).
from pydantic import BaseModel, ConfigDict, Field

from lexicall_api.models.entry import VocabularyEntryType


# Unlike VocabularyEntryWrite, Definition has no min_length — this is meant to
# cover a brand-new, still-empty draft (see routers/enrichment.py), not just
# an already-saved entry.
class EntryEnrichmentRequest(BaseModel):
    word: str = Field(alias="Word", min_length=1)
    definition: str = Field(default="", alias="Definition")
    type: VocabularyEntryType = Field(default=VocabularyEntryType.UNDEFINED, alias="Type")
    synonyms: list[str] = Field(default_factory=list, alias="Synonyms")
    example_sentences: list[str] = Field(default_factory=list, alias="ExampleSentences")
    locked_fields: list[str] = Field(default_factory=list, alias="LockedFields")

    model_config = ConfigDict(populate_by_name=True)


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
    # False when the LLM couldn't confirm Word is a real, existing French
    # word/expression — every other field is then absent, no suggestion
    # content is sent even if the model produced some.
    word_recognized: bool = True
    definition: TextFieldSuggestion | None = None
    type: TypeFieldSuggestion | None = None
    synonyms: ListFieldSuggestion | None = None
    example_sentences: ListFieldSuggestion | None = None


class RephraseDefinitionRequest(BaseModel):
    word: str = Field(alias="Word", min_length=1)
    definition: str = Field(alias="Definition", min_length=1)

    model_config = ConfigDict(populate_by_name=True)


class RephraseDefinitionResult(BaseModel):
    definition: str
