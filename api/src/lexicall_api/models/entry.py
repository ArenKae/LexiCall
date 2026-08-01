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
    # Real client-side edit timestamp, stamped at the moment of the local
    # action (not of sync) — see entries_repo.put_entry: a conditional
    # write only applies it if it's newer than the value already stored
    # (Last-Write-Wins).
    updated_at: datetime | None = Field(default=None, alias="UpdatedAt")
    # Real creation timestamp (matters for an entry created offline, synced
    # much later) — present on Write, not just Create, because PUT is now a
    # true upsert (see entries_repo.put_entry) and the client always PUTs,
    # even for a brand-new entry. Safe to expose here despite Write also
    # backing plain edits: put_entry routes this through Mongo's
    # $setOnInsert, which only ever applies on a genuine insert — an edit
    # sending its own CreatedAt along (the client always serializes the
    # whole local object) can't touch the stored value, enforced by Mongo
    # itself rather than by keeping the field off this model.
    created_at: datetime | None = Field(default=None, alias="CreatedAt")
    # Base64-encoded image (already JPEG-compressed client-side), or empty/
    # None if the entry has none. Never stored on the entries document: the
    # router extracts it and dispatches to entry_images itself (upsert or
    # delete depending on the case) — see routers/entries.py. Deliberately
    # absent from VocabularyEntrySummary: echoing it back on every GET would
    # reintroduce exactly the WiredTiger cache pressure the two-collection
    # split was meant to eliminate.
    image_base64: str | None = Field(default=None, alias="ImageBase64")

    model_config = ConfigDict(populate_by_name=True)


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
    # Tombstone: True once the entry is deleted (soft-delete, see
    # entries_repo.delete_entry) — only visible through a delta pull
    # (updated_since), so other clients learn about the deletion. Defaults
    # to False: no backfill needed for existing Mongo documents that don't
    # have this field yet.
    is_deleted: bool = Field(default=False, alias="IsDeleted")

    model_config = ConfigDict(populate_by_name=True)
