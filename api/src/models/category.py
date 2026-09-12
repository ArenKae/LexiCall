# Pydantic models for VocabularyCategory (mirrors
# apps/windows/src/Models/VocabularyCategory.cs). ParentId references another
# category by application Id; null = root category.
from datetime import datetime

from pydantic import BaseModel, ConfigDict, Field


class VocabularyCategoryWrite(BaseModel):
    name: str = Field(alias="Name", min_length=1)
    parent_id: str | None = Field(default=None, alias="ParentId")
    description: str = Field(default="", alias="Description")
    icon_glyph: str = Field(default="", alias="IconGlyph")
    # Client-stamped edit time, used for Last-Write-Wins comparisons. Never
    # called UpdatedAt on the wire: that name is reserved for the server's
    # own arrival-time bookkeeping, which no client model ever declares.
    updated_at: datetime | None = Field(default=None, alias="ClientLastWrite")
    # Trusted from the client so an offline-created category keeps its real
    # creation date; only actually applied on first insert (see put_category).
    created_at: datetime | None = Field(default=None, alias="CreatedAt")

    model_config = ConfigDict(populate_by_name=True)


class CategoryReindexResult(BaseModel):
    # Counters from a full embedding reindex, so the desktop client can tell
    # "everything was already current" from "12 categories repaired".
    embedded: int
    unchanged: int
    orphans_removed: int


class VocabularyCategory(BaseModel):
    id: str = Field(alias="Id")
    name: str = Field(alias="Name")
    parent_id: str | None = Field(default=None, alias="ParentId")
    description: str = Field(default="", alias="Description")
    icon_glyph: str = Field(default="", alias="IconGlyph")
    created_at: datetime = Field(alias="CreatedAt")
    # The client's own edit time, for its local Last-Write-Wins merge — not
    # this document's server arrival time, which no client model declares.
    updated_at: datetime = Field(alias="ClientLastWrite")
    # True once soft-deleted; only ever seen through a delta pull.
    is_deleted: bool = Field(default=False, alias="IsDeleted")

    model_config = ConfigDict(populate_by_name=True)
