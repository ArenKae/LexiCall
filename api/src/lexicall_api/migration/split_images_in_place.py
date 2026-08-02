# Splits inline images (ImageBase64) still left on `entries` out to
# `entry_images`, reading directly from Mongo instead of a vocabulary.json
# export — so it also catches entries that were only ever created through
# live API sync.
import argparse
import base64
from dataclasses import dataclass

from lexicall_api.repositories import entries_repo, entry_images_repo


@dataclass
class SplitSummary:
    images_migrated: int = 0
    images_skipped_invalid_base64: int = 0
    empty_fields_cleared: int = 0

    def render(self) -> str:
        return (
            "Split summary:\n"
            f"  Images migrated to entry_images: {self.images_migrated}\n"
            f"  Images skipped (invalid base64): {self.images_skipped_invalid_base64}\n"
            f"  Empty ImageBase64 fields cleared: {self.empty_fields_cleared}"
        )


def run(dry_run: bool = False) -> SplitSummary:
    summary = SplitSummary()
    for entry in entries_repo.list_entries_with_inline_image_field():
        image_base64 = entry["ImageBase64"]

        if not image_base64:
            summary.empty_fields_cleared += 1
            if not dry_run:
                entries_repo.clear_inline_image(entry["Id"])
            continue

        try:
            image_bytes = base64.b64decode(image_base64, validate=True)
        except (ValueError, TypeError):
            summary.images_skipped_invalid_base64 += 1
            continue

        summary.images_migrated += 1
        if dry_run:
            continue

        entry_images_repo.upsert_image(entry["Id"], image_bytes, "image/jpeg")
        entries_repo.clear_inline_image(entry["Id"])

    return summary


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Migrates inline entry images (entries.ImageBase64) to entry_images, reading Mongo directly."
    )
    parser.add_argument("--dry-run", action="store_true", help="Counts without writing anything.")
    args = parser.parse_args()

    summary = run(dry_run=args.dry_run)
    print(summary.render())


if __name__ == "__main__":
    main()
