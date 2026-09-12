# One-off migration: for a database that never ran backfill_server_updated_at.py
# (the original pre-fix shape, no ServerUpdatedAt anywhere). Renames the
# client-stamped UpdatedAt to ClientLastWrite, then seeds a fresh UpdatedAt
# from that same value — the best available stand-in for arrival time on
# data that predates the ClientLastWrite/UpdatedAt split. Must run under the
# same deploy as the API code switching to these names, API stopped.
# Idempotent: each step only touches documents it hasn't reached yet, so a
# rerun (including one that resumes after a mid-run crash) is safe.
import argparse
from datetime import datetime, timezone

import timestamps
from database import get_categories_collection, get_entries_collection


def _canonical(value) -> str:
    # Historical documents may carry the legacy "...645328Z" format, which
    # sorts differently from the "+00:00" the API writes for the same
    # instant — parse and re-emit rather than copying the string across.
    if isinstance(value, datetime):
        return timestamps.to_iso_utc(value)
    if isinstance(value, str):
        try:
            return timestamps.to_iso_utc(datetime.fromisoformat(value.replace("Z", "+00:00")))
        except ValueError:
            pass
    return datetime.fromtimestamp(0, timezone.utc).isoformat(timespec="microseconds")


def _migrate(collection, dry_run: bool) -> dict[str, int]:
    counts = {
        "not_renamed": collection.count_documents({"ClientLastWrite": {"$exists": False}}),
        "not_derived": collection.count_documents(
            {"ClientLastWrite": {"$exists": True}, "UpdatedAt": {"$exists": False}}
        ),
        # ServerUpdatedAt here means this is dev-shaped data, already
        # backfilled under the old scheme — the dev script is the right one.
        "dev_shaped": collection.count_documents({"ServerUpdatedAt": {"$exists": True}}),
    }

    if dry_run:
        return counts

    if counts["dev_shaped"]:
        raise RuntimeError(
            f"{counts['dev_shaped']} document(s) carry ServerUpdatedAt — this "
            "database already ran the old backfill; use rename_updated_at_dev.py instead."
        )

    if counts["not_renamed"]:
        collection.update_many({"ClientLastWrite": {"$exists": False}}, {"$rename": {"UpdatedAt": "ClientLastWrite"}})

    for doc in collection.find(
        {"ClientLastWrite": {"$exists": True}, "UpdatedAt": {"$exists": False}}, {"ClientLastWrite": 1}
    ):
        collection.update_one({"_id": doc["_id"]}, {"$set": {"UpdatedAt": _canonical(doc["ClientLastWrite"])}})

    return counts


def run(dry_run: bool = False) -> dict[str, dict[str, int]]:
    # No index work needed: the pre-existing UpdatedAt_1 index (built for the
    # old client-edit meaning) keeps indexing whatever the field holds after
    # the rename-then-derive dance below repopulates it under the same name
    # — verified empirically, an index tracks its field's current value
    # regardless of what was there before.
    return {
        "entries": _migrate(get_entries_collection(), dry_run),
        "categories": _migrate(get_categories_collection(), dry_run),
    }


def main() -> None:
    parser = argparse.ArgumentParser(
        description=(
            "Prod-only: renames UpdatedAt to ClientLastWrite and derives a fresh "
            "UpdatedAt from it. Refuses a database that already carries "
            "ServerUpdatedAt (use rename_updated_at_dev.py there instead)."
        )
    )
    parser.add_argument("--dry-run", action="store_true", help="Counts without writing anything.")
    args = parser.parse_args()

    result = run(dry_run=args.dry_run)
    suffix = " (dry run, nothing written)" if args.dry_run else ""
    for name, counts in result.items():
        print(
            f"{name}: {counts['not_renamed']} to rename, {counts['not_derived']} to derive, "
            f"{counts['dev_shaped']} dev-shaped{suffix}"
        )


if __name__ == "__main__":
    main()
