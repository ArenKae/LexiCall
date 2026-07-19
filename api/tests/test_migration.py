# The migration script must sanitize inconsistent data rather than
# abandoning the import, and be idempotent (upsert by application Id).
from pathlib import Path

import pytest

from lexicall_api.migration.migrate_from_json import ForceRequiredError, run
from lexicall_api.repositories import entries_repo

FIXTURE_PATH = Path(__file__).parent / "fixtures" / "sample_vocabulary.json"


def test_migration_dry_run_does_not_write():
    summary = run(str(FIXTURE_PATH), dry_run=True)
    assert summary.entries_inserted == 0
    assert entries_repo.list_ids() == set()


def test_migration_sanitizes_and_reports_summary():
    summary = run(str(FIXTURE_PATH))

    assert summary.entries_inserted == 2
    assert summary.entries_rejected_invalid == 1
    assert summary.entries_sanitized_category_ids == 1
    assert summary.categories_inserted == 4
    assert summary.categories_duplicate_id_skipped == 1
    # Breaking a single link of the cccccccc <-> dddddddd cycle is enough to
    # resolve it: cccccccc loses its parent, dddddddd keeps its own (cccccccc,
    # now a root) — the resulting tree is valid without a second fix.
    assert summary.categories_broken_parent_fixed == 1


def test_migration_is_idempotent():
    run(str(FIXTURE_PATH))
    second_summary = run(str(FIXTURE_PATH))

    assert second_summary.entries_inserted == 0
    assert second_summary.entries_unchanged == 2
    assert second_summary.categories_inserted == 0
    assert second_summary.categories_unchanged == 4


def test_migration_requires_force_when_data_diverges():
    run(str(FIXTURE_PATH))
    entries_repo.create_entry(
        {
            "Word": "Étrangère",
            "Definition": "Créée hors migration",
            "Synonyms": [],
            "ExampleSentences": [],
            "Notes": "",
            "Source": "",
            "CategoryIds": [],
            "Tags": [],
            "ImageBase64": "",
        }
    )

    with pytest.raises(ForceRequiredError):
        run(str(FIXTURE_PATH))

    summary = run(str(FIXTURE_PATH), force=True)
    assert summary.entries_unchanged == 2


def test_ids_are_strings_round_trip():
    run(str(FIXTURE_PATH))
    entry = entries_repo.get_entry("11111111-1111-1111-1111-111111111111")
    assert entry is not None
    assert entry["Id"] == "11111111-1111-1111-1111-111111111111"
    assert isinstance(entry["Id"], str)
