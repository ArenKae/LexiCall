# Pydantic models for VocabularyCategory (mirrors
# apps/windows/src/Models/VocabularyCategory.cs). ParentId references another
# category by application Id; null = root category.
from datetime import datetime

from pydantic import BaseModel, ConfigDict, Field


class VocabularyCategoryWrite(BaseModel):
    # min_length=1: reject an empty string, not just a missing field.
    name: str = Field(alias="Name", min_length=1)
    parent_id: str | None = Field(default=None, alias="ParentId")
    description: str = Field(default="", alias="Description")
    icon_glyph: str = Field(default="", alias="IconGlyph")
    # Real client-side edit timestamp — see VocabularyEntryWrite and
    # categories_repo.put_category (conditional LWW upsert).
    updated_at: datetime | None = Field(default=None, alias="UpdatedAt")
    # Real creation timestamp — see VocabularyEntryWrite.created_at for why
    # this lives on Write (PUT is a true upsert) and why $setOnInsert, not
    # field placement, is what actually protects it from an edit.
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
    # Tombstone — see VocabularyEntrySummary.is_deleted.
    is_deleted: bool = Field(default=False, alias="IsDeleted")

    model_config = ConfigDict(populate_by_name=True)
