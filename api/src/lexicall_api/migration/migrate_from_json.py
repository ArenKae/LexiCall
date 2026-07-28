# Migrates a vocabulary.json (current { Entries, Categories } format only,
# not the old flat format) to MongoDB. Idempotent: upsert by the application
# field Id, never by Mongo's native _id — rerunning with the same file
# duplicates nothing. Tolerant of inconsistent data (duplicate ids, broken/
# cyclic parents, orphaned CategoryIds): neutralizes and counts rather than
# abandoning the whole import, like SanitizeDatabase on the desktop app side.
import argparse
import base64
import json
import sys
from dataclasses import dataclass

from lexicall_api.repositories import categories_repo, entries_repo, entry_images_repo


class ForceRequiredError(RuntimeError):
    pass


@dataclass
class MigrationSummary:
    categories_inserted: int = 0
    categories_updated: int = 0
    categories_unchanged: int = 0
    categories_duplicate_id_skipped: int = 0
    categories_broken_parent_fixed: int = 0
    entries_inserted: int = 0
    entries_updated: int = 0
    entries_unchanged: int = 0
    entries_rejected_invalid: int = 0
    entries_sanitized_category_ids: int = 0
    images_migrated: int = 0

    def render(self) -> str:
        return "\n".join(
            [
                "Migration summary:",
                f"  Categories: {self.categories_inserted} inserted, "
                f"{self.categories_updated} updated, "
                f"{self.categories_unchanged} unchanged, "
                f"{self.categories_duplicate_id_skipped} duplicate id(s) skipped, "
                f"{self.categories_broken_parent_fixed} broken/cyclic parent(s) neutralized",
                f"  Entries: {self.entries_inserted} inserted, "
                f"{self.entries_updated} updated, "
                f"{self.entries_unchanged} unchanged, "
                f"{self.entries_rejected_invalid} rejected (empty Word/Definition), "
                f"{self.entries_sanitized_category_ids} sanitized (orphaned CategoryIds removed)",
                f"  Images: {self.images_migrated} migrated to entry_images",
            ]
        )


def _dedupe_categories(categories: list[dict], summary: MigrationSummary) -> list[dict]:
    seen: set[str] = set()
    result = []
    for category in categories:
        category_id = category["Id"]
        if category_id in seen:
            summary.categories_duplicate_id_skipped += 1
            continue
        seen.add(category_id)
        result.append(category)
    return result


def _resolve_broken_parents(categories: list[dict], summary: MigrationSummary) -> None:
    by_id = {category["Id"]: category for category in categories}

    def is_cyclic_or_broken(category: dict) -> bool:
        visited: set[str] = set()
        current = category.get("ParentId")
        while current is not None:
            if current == category["Id"] or current in visited:
                return True
            if current not in by_id:
                return True
            visited.add(current)
            current = by_id[current].get("ParentId")
        return False

    for category in categories:
        if category.get("ParentId") is not None and is_cyclic_or_broken(category):
            category["ParentId"] = None
            summary.categories_broken_parent_fixed += 1


def _sanitize_entries(entries: list[dict], known_category_ids: set[str], summary: MigrationSummary) -> list[dict]:
    valid = []
    for entry in entries:
        if not entry.get("Word") or not entry.get("Definition"):
            summary.entries_rejected_invalid += 1
            continue
        original_ids = entry.get("CategoryIds", [])
        sanitized_ids = [cid for cid in original_ids if cid in known_category_ids]
        if len(sanitized_ids) != len(original_ids):
            summary.entries_sanitized_category_ids += 1
        entry["CategoryIds"] = sanitized_ids
        valid.append(entry)
    return valid


def _diverging_data_present(categories: list[dict], entries: list[dict]) -> bool:
    incoming_category_ids = {c["Id"] for c in categories}
    incoming_entry_ids = {e["Id"] for e in entries}
    unexpected_categories = categories_repo.list_ids() - incoming_category_ids
    unexpected_entries = entries_repo.list_ids() - incoming_entry_ids
    return bool(unexpected_categories or unexpected_entries)


def run(input_path: str, dry_run: bool = False, force: bool = False) -> MigrationSummary:
    with open(input_path, encoding="utf-8") as input_file:
        document = json.load(input_file)

    summary = MigrationSummary()

    categories = _dedupe_categories(document.get("Categories", []), summary)
    _resolve_broken_parents(categories, summary)

    known_category_ids = {category["Id"] for category in categories}
    entries = _sanitize_entries(document.get("Entries", []), known_category_ids, summary)

    if dry_run:
        return summary

    if not force and _diverging_data_present(categories, entries):
        raise ForceRequiredError(
            "The database already contains data that clearly did not come "
            "from this file. Rerun with --force to confirm the overwrite."
        )

    for category in categories:
        outcome = categories_repo.upsert_category(category)
        if outcome == "inserted":
            summary.categories_inserted += 1
        elif outcome == "updated":
            summary.categories_updated += 1
        else:
            summary.categories_unchanged += 1

    for entry in entries:
        image_b64 = entry.pop("ImageBase64", "")
        outcome = entries_repo.upsert_entry(entry)
        if outcome == "inserted":
            summary.entries_inserted += 1
        elif outcome == "updated":
            summary.entries_updated += 1
        else:
            summary.entries_unchanged += 1
        if image_b64:
            entry_images_repo.upsert_image(entry["Id"], base64.b64decode(image_b64), "image/jpeg")
            summary.images_migrated += 1

    return summary


def main() -> None:
    parser = argparse.ArgumentParser(description="Migrates vocabulary.json to MongoDB.")
    parser.add_argument("--input", required=True, help="Path to the vocabulary.json file to import.")
    parser.add_argument("--dry-run", action="store_true", help="Validates and prints the summary without writing anything.")
    parser.add_argument(
        "--force",
        action="store_true",
        help="Confirms the overwrite if the database already contains diverging data.",
    )
    args = parser.parse_args()

    try:
        summary = run(args.input, dry_run=args.dry_run, force=args.force)
    except ForceRequiredError as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)

    print(summary.render())
    if summary.entries_rejected_invalid > 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
