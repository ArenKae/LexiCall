# Pydantic models for VocabularyEntry (mirrors
# apps/windows/src/Models/VocabularyEntry.cs). CategoryIds references
# categories by application Id (never by name nor by Mongo _id).
from datetime import datetime
from enum import Enum

from pydantic import BaseModel, ConfigDict, Field, field_validator

MAX_IMAGES_PER_ENTRY = 3


class VocabularyEntryType(str, Enum):
    NOM_MASCULIN = "Nom masculin"
    NOM_FEMININ = "Nom féminin"
    VERBE = "Verbe"
    ADJECTIF = "Adjectif"
    ADVERBE = "Adverbe"
    EXPRESSION = "Expression"
    UNDEFINED = "Undefined"


class VocabularyEntryImageWrite(BaseModel):
    id: str = Field(alias="Id")
    caption: str = Field(default="", alias="Caption")
    # Never stored on the entry document itself — stripped before the
    # entries $set and dispatched to the entry_images collection instead.
    image_base64: str = Field(default="", alias="ImageBase64")

    model_config = ConfigDict(populate_by_name=True)


class VocabularyEntryImage(BaseModel):
    id: str = Field(alias="Id")
    caption: str = Field(default="", alias="Caption")

    model_config = ConfigDict(populate_by_name=True)


class VocabularyEntryWrite(BaseModel):
    word: str = Field(alias="Word", min_length=1)
    definition: str = Field(alias="Definition", min_length=1)
    synonyms: list[str] = Field(default_factory=list, alias="Synonyms")
    example_sentences: list[str] = Field(default_factory=list, alias="ExampleSentences")
    notes: str = Field(default="", alias="Notes")
    source: str = Field(default="", alias="Source")
    category_ids: list[str] = Field(default_factory=list, alias="CategoryIds")
    tags: list[str] = Field(default_factory=list, alias="Tags")
    # No default: every push must state a type explicitly (Undefined is a
    # real enum member, not a silent fallback at this layer).
    type: VocabularyEntryType = Field(alias="Type")
    is_archived: bool = Field(default=False, alias="IsArchived")
    images: list[VocabularyEntryImageWrite] = Field(default_factory=list, alias="Images")
    # Client-stamped edit time, used for Last-Write-Wins comparisons.
    updated_at: datetime | None = Field(default=None, alias="UpdatedAt")
    # Trusted from the client so an offline-created entry keeps its real
    # creation date; only actually applied on first insert (see put_entry).
    created_at: datetime | None = Field(default=None, alias="CreatedAt")

    model_config = ConfigDict(populate_by_name=True)

    @field_validator("images")
    @classmethod
    def _validate_max_images(cls, value: list[VocabularyEntryImageWrite]) -> list[VocabularyEntryImageWrite]:
        if len(value) > MAX_IMAGES_PER_ENTRY:
            raise ValueError(f"An entry can have at most {MAX_IMAGES_PER_ENTRY} images.")
        return value


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
    # Defaults kept even though the type is conceptually required: a safety
    # net for any document the migration hasn't backfilled yet (belt-and-
    # suspenders alongside the strict deploy-after-migration rollout order).
    type: VocabularyEntryType = Field(default=VocabularyEntryType.UNDEFINED, alias="Type")
    is_archived: bool = Field(default=False, alias="IsArchived")
    images: list[VocabularyEntryImage] = Field(default_factory=list, alias="Images")
    created_at: datetime = Field(alias="CreatedAt")
    updated_at: datetime = Field(alias="UpdatedAt")
    # True once soft-deleted; only ever seen through a delta pull.
    is_deleted: bool = Field(default=False, alias="IsDeleted")

    model_config = ConfigDict(populate_by_name=True)
