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

    model_config = ConfigDict(populate_by_name=True)


class VocabularyCategoryCreate(VocabularyCategoryWrite):
    # Optional client-supplied Id, POST-only — see VocabularyEntryCreate.
    id: str | None = Field(default=None, alias="Id", min_length=1)


class VocabularyCategory(BaseModel):
    id: str = Field(alias="Id")
    name: str = Field(alias="Name")
    parent_id: str | None = Field(default=None, alias="ParentId")
    description: str = Field(default="", alias="Description")
    icon_glyph: str = Field(default="", alias="IconGlyph")
    created_at: datetime = Field(alias="CreatedAt")
    updated_at: datetime = Field(alias="UpdatedAt")

    model_config = ConfigDict(populate_by_name=True)
