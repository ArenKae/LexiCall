# Backfills Type/IsArchived and restructures entry_images (own Id per image
# instead of one shared with the owning entry) on every existing `entries`
# document, reading directly from Mongo. Idempotent: an entry already
# carrying all three fields is left untouched, so a crash mid-run can be
# safely rerun.
import argparse
import uuid
from dataclasses import dataclass

from lexicall_api.repositories import entries_repo, entry_images_repo

DEFAULT_TYPE = "Undefined"


@dataclass
class MigrationSummary:
    entries_scanned: int = 0
    types_backfilled: int = 0
    archived_flags_backfilled: int = 0
    images_restructured: int = 0
    images_initialized_empty: int = 0
    entries_already_migrated: int = 0

    def render(self) -> str:
        return (
            "Type/Archive/Images migration summary:\n"
            f"  Entries scanned: {self.entries_scanned}\n"
            f"  Already migrated (skipped): {self.entries_already_migrated}\n"
            f"  Type backfilled to 'Undefined': {self.types_backfilled}\n"
            f"  IsArchived backfilled to False: {self.archived_flags_backfilled}\n"
            f"  Images restructured (own Id): {self.images_restructured}\n"
            f"  Images initialized empty (no prior image): {self.images_initialized_empty}"
        )


def run(dry_run: bool = False) -> MigrationSummary:
    summary = MigrationSummary()

    for entry in entries_repo.list_all_raw():
        summary.entries_scanned += 1
        entry_id = entry["Id"]

        if "Type" in entry and "IsArchived" in entry and "Images" in entry:
            summary.entries_already_migrated += 1
            continue

        type_value = entry.get("Type", DEFAULT_TYPE)
        if "Type" not in entry:
            summary.types_backfilled += 1

        is_archived = entry.get("IsArchived", False)
        if "IsArchived" not in entry:
            summary.archived_flags_backfilled += 1

        if "Images" in entry:
            images = entry["Images"]
        else:
            old_image = entry_images_repo.get_image(entry_id)
            if old_image is None:
                images = []
                summary.images_initialized_empty += 1
            else:
                new_image_id = str(uuid.uuid4())
                images = [{"Id": new_image_id, "Caption": ""}]
                summary.images_restructured += 1
                if not dry_run:
                    entry_images_repo.upsert_image(
                        new_image_id, bytes(old_image["ImageBytes"]), old_image["ContentType"]
                    )
                    entry_images_repo.delete_image(entry_id)

        if not dry_run:
            entries_repo.set_type_archived_images(entry_id, type_value, is_archived, images)

    return summary


def main() -> None:
    parser = argparse.ArgumentParser(
        description=(
            "Backfills Type/IsArchived and restructures entry_images (own Id per "
            "image) on every entries document, reading Mongo directly."
        )
    )
    parser.add_argument("--dry-run", action="store_true", help="Counts without writing anything.")
    args = parser.parse_args()

    summary = run(dry_run=args.dry_run)
    print(summary.render())


if __name__ == "__main__":
    main()
