# One-off migration: rewrites every entries document to drop the unused
# Tags field and normalize field order for readability in Mongo tooling
# (Compass, mongosh) — BSON field order has no functional effect, this is
# purely cosmetic. Idempotent: a document already matching the target shape
# is left untouched, so a crash mid-run can be safely rerun.
import argparse

from lexicall_api.database import get_entries_collection

FIELD_ORDER = [
    "Id",
    "Word",
    "Type",
    "Definition",
    "CategoryIds",
    "Synonyms",
    "ExampleSentences",
    "Notes",
    "Source",
    "Images",
    "CreatedAt",
    "UpdatedAt",
    "IsArchived",
    "IsDeleted",
    "TombstonedAt",
]


def _reorder(doc: dict) -> dict:
    ordered = {key: doc[key] for key in FIELD_ORDER if key in doc}
    # Any field this migration doesn't know about is kept, not dropped —
    # only Tags is intentionally removed here, nothing else.
    leftover = {key: value for key, value in doc.items() if key not in FIELD_ORDER and key not in ("_id", "Tags")}
    return {**ordered, **leftover}


def run(dry_run: bool = False) -> tuple[int, int]:
    scanned = 0
    rewritten = 0

    for doc in get_entries_collection().find({}):
        scanned += 1
        original_keys = [key for key in doc if key != "_id"]
        reordered = _reorder(doc)

        if list(reordered.keys()) == original_keys:
            continue

        rewritten += 1
        if not dry_run:
            # Full replace, not $set: $set updates a field in place without
            # moving it, so it can't reorder an already-existing field. No
            # CAS check either — this runs offline with no concurrent writer.
            get_entries_collection().replace_one({"_id": doc["_id"]}, reordered)

    return scanned, rewritten


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Drops the unused Tags field and normalizes field order on every entries document."
    )
    parser.add_argument("--dry-run", action="store_true", help="Counts without writing anything.")
    args = parser.parse_args()

    scanned, rewritten = run(dry_run=args.dry_run)
    suffix = " (dry run, nothing written)" if args.dry_run else ""
    print(f"Entries scanned: {scanned}\nEntries rewritten: {rewritten}{suffix}")


if __name__ == "__main__":
    main()
