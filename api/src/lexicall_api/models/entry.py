# Pydantic models for VocabularyEntry (mirrors
# apps/windows/src/Models/VocabularyEntry.cs). CategoryIds references
# categories by application Id (never by name nor by Mongo _id).
from datetime import datetime

from pydantic import BaseModel, ConfigDict, Field


class VocabularyEntryWrite(BaseModel):
    word: str = Field(alias="Word", min_length=1)
    definition: str = Field(alias="Definition", min_length=1)
    synonyms: list[str] = Field(default_factory=list, alias="Synonyms")
    example_sentences: list[str] = Field(default_factory=list, alias="ExampleSentences")
    notes: str = Field(default="", alias="Notes")
    source: str = Field(default="", alias="Source")
    category_ids: list[str] = Field(default_factory=list, alias="CategoryIds")
    tags: list[str] = Field(default_factory=list, alias="Tags")
    # Client-stamped edit time, used for Last-Write-Wins comparisons.
    updated_at: datetime | None = Field(default=None, alias="UpdatedAt")
    # Trusted from the client so an offline-created entry keeps its real
    # creation date; only actually applied on first insert (see put_entry).
    created_at: datetime | None = Field(default=None, alias="CreatedAt")
    # Base64 image, or None/empty if the entry has none. Never stored on the
    # entry document itself — kept in a separate collection.
    image_base64: str | None = Field(default=None, alias="ImageBase64")

    model_config = ConfigDict(populate_by_name=True)


class VocabularyEntrySummary(BaseModel):
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
    # True once soft-deleted; only ever seen through a delta pull.
    is_deleted: bool = Field(default=False, alias="IsDeleted")

    model_config = ConfigDict(populate_by_name=True)
