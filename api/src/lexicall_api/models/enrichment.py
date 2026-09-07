# Request/response models for the AI enrichment routes (routers/enrichment.py).
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field

from lexicall_api.models.entry import VocabularyEntryType


# Unlike VocabularyEntryWrite, Definition has no min_length — this is meant to
# cover a brand-new, still-empty draft (see routers/enrichment.py), not just
# an already-saved entry.
class EntryEnrichmentRequest(BaseModel):
    word: str = Field(alias="Word", min_length=1)
    definition: list[str] = Field(default_factory=list, alias="Definition")
    type: VocabularyEntryType = Field(default=VocabularyEntryType.UNDEFINED, alias="Type")
    synonyms: list[str] = Field(default_factory=list, alias="Synonyms")
    example_sentences: list[str] = Field(default_factory=list, alias="ExampleSentences")
    locked_fields: list[str] = Field(default_factory=list, alias="LockedFields")

    model_config = ConfigDict(populate_by_name=True)


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
    definition: ListFieldSuggestion | None = None
    type: TypeFieldSuggestion | None = None
    synonyms: ListFieldSuggestion | None = None
    example_sentences: ListFieldSuggestion | None = None


class CategorizationRequest(BaseModel):
    word: str = Field(alias="Word", min_length=1)
    # Optional but worth sending: a bare word is thin signal next to the
    # word plus what it means. One element per sense — each is retrieved
    # separately, so a two-sense word reaches both its lexical fields.
    definition: list[str] = Field(default_factory=list, alias="Definition")

    model_config = ConfigDict(populate_by_name=True)


class CategoryRef(BaseModel):
    id: str
    name: str
    path: str


class CategoryCandidate(CategoryRef):
    score: float


class CategoryCandidatesResult(BaseModel):
    candidates: list[CategoryCandidate]


class CategorySuggestion(BaseModel):
    # "existing" fills category; "new" fills new_category_name, and
    # new_category_parent stays null when the suggestion is a new root.
    decision: Literal["existing", "new"]
    category: CategoryRef | None = None
    new_category_name: str | None = None
    new_category_parent: CategoryRef | None = None
    justification: str


class CategorizationSuggestions(BaseModel):
    # False when the LLM couldn't confirm Word is a real, existing French
    # word/expression — suggestions is then empty, no category proposed and
    # none invented to house it.
    word_recognized: bool = True
    # Usually one. Several only when the word carries genuinely distinct
    # senses across different lexical fields ("ladre": leper / miser), each
    # saying which sense it covers.
    suggestions: list[CategorySuggestion]


class RephraseDefinitionRequest(BaseModel):
    word: str = Field(alias="Word", min_length=1)
    definition: str = Field(alias="Definition", min_length=1)

    model_config = ConfigDict(populate_by_name=True)


class RephraseDefinitionResult(BaseModel):
    definition: str
