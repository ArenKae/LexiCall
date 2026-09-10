# Turns each entry's Type from a single string into a list, so a word that
# genuinely works as several parts of speech ("rose": noun and adjective) can
# say so. Run from api/ with:
#   PYTHONPATH=src .venv/bin/python -m migration.wrap_entry_types [--dry-run]
# Pure wrapping, no interpretation: a stored "Adjectif" becomes ["Adjectif"]
# and nothing is inferred about a second type — the enrichment pass proposes
# those later. Idempotent: a Type already stored as a list is left alone.
import argparse
from dataclasses import dataclass

from database import get_entries_collection, strip_mongo_id


@dataclass
class WrapSummary:
    converted: int = 0
    already_list: int = 0
    missing: int = 0

    def render(self) -> str:
        return (
            "Wrap summary:\n"
            f"  Types wrapped into a list: {self.converted}\n"
            f"  Already a list (skipped): {self.already_list}\n"
            f"  Absent, defaulted to Undefined: {self.missing}"
        )


def run(dry_run: bool = False) -> WrapSummary:
    summary = WrapSummary()
    collection = get_entries_collection()

    for doc in [strip_mongo_id(doc) for doc in collection.find({})]:
        entry_type = doc.get("Type")
        if isinstance(entry_type, list):
            summary.already_list += 1
            continue

        if entry_type:
            summary.converted += 1
        else:
            summary.missing += 1
            entry_type = "Undefined"

        if not dry_run:
            # UpdatedAt untouched: a schema change is not a user edit, and
            # bumping it would replay every entry down to the client.
            collection.update_one({"Id": doc["Id"]}, {"$set": {"Type": [entry_type]}})

    return summary


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Converts every entry's Type from a string to a list of grammatical types."
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Counts and reports without writing anything.",
    )
    args = parser.parse_args()

    summary = run(dry_run=args.dry_run)
    print(summary.render())


if __name__ == "__main__":
    main()
