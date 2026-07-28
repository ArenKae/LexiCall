# The split-in-place migration must operate on whatever is actually in
# entries (unlike migrate_from_json.py, no JSON export involved), fully
# clear ImageBase64 even when it's just an empty string (not only when it
# holds real image data), and be idempotent, since it may need to be rerun
# on a live prod database.
import base64

from lexicall_api.migration.split_images_in_place import run
from lexicall_api.repositories import entries_repo, entry_images_repo


def _create_legacy_entry(image_base64: str = "aGVsbG8=") -> str:
    entry = entries_repo.create_entry(
        {
            "Word": "Legacy",
            "Definition": "Entrée avec image inline, avant le split",
            "Synonyms": [],
            "ExampleSentences": [],
            "Notes": "",
            "Source": "",
            "CategoryIds": [],
            "Tags": [],
            "ImageBase64": image_base64,
        }
    )
    return entry["Id"]


def test_dry_run_does_not_write():
    entry_id = _create_legacy_entry()

    summary = run(dry_run=True)

    assert summary.images_migrated == 1
    assert entries_repo.get_entry(entry_id)["ImageBase64"] == "aGVsbG8="
    assert entry_images_repo.get_image(entry_id) is None


def test_splits_image_and_clears_inline_field():
    entry_id = _create_legacy_entry()

    summary = run()

    assert summary.images_migrated == 1
    entry = entries_repo.get_entry(entry_id)
    assert "ImageBase64" not in entry

    image = entry_images_repo.get_image(entry_id)
    assert image is not None
    assert image["ImageBytes"] == b"hello"
    assert image["ContentType"] == "image/jpeg"


def test_clears_empty_inline_field_without_creating_image_doc():
    entry_id = _create_legacy_entry(image_base64="")

    summary = run()

    assert summary.images_migrated == 0
    assert summary.empty_fields_cleared == 1
    entry = entries_repo.get_entry(entry_id)
    assert "ImageBase64" not in entry
    assert entry_images_repo.get_image(entry_id) is None


def test_dry_run_does_not_clear_empty_field():
    entry_id = _create_legacy_entry(image_base64="")

    summary = run(dry_run=True)

    assert summary.empty_fields_cleared == 1
    assert entries_repo.get_entry(entry_id)["ImageBase64"] == ""


def test_ignores_entries_without_inline_image():
    entries_repo.create_entry(
        {
            "Word": "SansImage",
            "Definition": "Aucune image",
            "Synonyms": [],
            "ExampleSentences": [],
            "Notes": "",
            "Source": "",
            "CategoryIds": [],
            "Tags": [],
        }
    )

    summary = run()

    assert summary.images_migrated == 0


def test_skips_invalid_base64():
    entry_id = _create_legacy_entry(image_base64="not-valid-base64!!")

    summary = run()

    assert summary.images_migrated == 0
    assert summary.images_skipped_invalid_base64 == 1
    assert entries_repo.get_entry(entry_id)["ImageBase64"] == "not-valid-base64!!"
    assert entry_images_repo.get_image(entry_id) is None


def test_is_idempotent():
    _create_legacy_entry()

    first_summary = run()
    second_summary = run()

    assert first_summary.images_migrated == 1
    assert second_summary.images_migrated == 0


def test_ids_are_strings_round_trip():
    entry_id = _create_legacy_entry()
    run()

    image = entry_images_repo.get_image(entry_id)
    assert isinstance(image["Id"], str)
    assert image["Id"] == entry_id


def test_base64_encoded_content_round_trips():
    payload = b"un contenu binaire quelconque, pas forcement un vrai jpeg"
    entry_id = _create_legacy_entry(image_base64=base64.b64encode(payload).decode("ascii"))

    run()

    image = entry_images_repo.get_image(entry_id)
    assert image["ImageBytes"] == payload
