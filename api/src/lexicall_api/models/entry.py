# Pydantic models for VocabularyEntry (mirrors
# apps/windows/src/Models/VocabularyEntry.cs). CategoryIds references
# categories by application Id (never by name nor by Mongo _id).
from datetime import datetime

from pydantic import BaseModel, ConfigDict, Field


class VocabularyEntryWrite(BaseModel):
    # min_length=1: reject an empty string, not just a missing field
    word: str = Field(alias="Word", min_length=1)
    definition: str = Field(alias="Definition", min_length=1)
    synonyms: list[str] = Field(default_factory=list, alias="Synonyms")
    example_sentences: list[str] = Field(default_factory=list, alias="ExampleSentences")
    notes: str = Field(default="", alias="Notes")
    source: str = Field(default="", alias="Source")
    category_ids: list[str] = Field(default_factory=list, alias="CategoryIds")
    tags: list[str] = Field(default_factory=list, alias="Tags")

    model_config = ConfigDict(populate_by_name=True)


class VocabularyEntryCreate(VocabularyEntryWrite):
    # Optional client-supplied Id, POST-only (never PUT — an update's target
    # id is the URL path parameter, not the body). Lets an offline-created
    # entry keep the same Id once it reaches the server, instead of getting
    # a second, different server-generated one.
    id: str | None = Field(default=None, alias="Id", min_length=1)


class VocabularyEntrySummary(BaseModel):
    # Response model for both GET /entries (list) and GET /entries/{id}:
    # images live in the separate entry_images collection (see
    # entry_images_repo.py), never inline on the entry document, so there's
    # no field distinction left between a list item and a single entry.
    id: str = Field(alias="Id")
    word: str = Field(alias="Word")
    definition: str = Field(alias="Definition")
    synonyms: list[str] = Field(default_factory=list, alias="Synonyms")
    example_sentences: list[str] = Field(default_factory=list, alias="ExampleSentences")
    notes: str = Field(default="", alias="Notes")
    source: str = Field(default="", alias="Source")
    category_ids: list[str] = Field(default_factory=list, alias="CategoryIds")
    tags: list[str] = Field(default_factory=list, alias="Tags")
    created_at: datetime = Field(alias="CreatedAt")
    updated_at: datetime = Field(alias="UpdatedAt")

    model_config = ConfigDict(populate_by_name=True)
