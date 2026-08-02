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
    # Client-stamped edit time, used for Last-Write-Wins comparisons.
    updated_at: datetime | None = Field(default=None, alias="UpdatedAt")
    # Trusted from the client so an offline-created category keeps its real
    # creation date; only actually applied on first insert (see put_category).
    created_at: datetime | None = Field(default=None, alias="CreatedAt")

    model_config = ConfigDict(populate_by_name=True)


class VocabularyCategory(BaseModel):
    id: str = Field(alias="Id")
    name: str = Field(alias="Name")
    parent_id: str | None = Field(default=None, alias="ParentId")
    description: str = Field(default="", alias="Description")
    icon_glyph: str = Field(default="", alias="IconGlyph")
    created_at: datetime = Field(alias="CreatedAt")
    updated_at: datetime = Field(alias="UpdatedAt")
    # True once soft-deleted; only ever seen through a delta pull.
    is_deleted: bool = Field(default=False, alias="IsDeleted")

    model_config = ConfigDict(populate_by_name=True)
