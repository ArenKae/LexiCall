# One-off migration: for a database where backfill_server_updated_at.py has
# already run (every document carries ServerUpdatedAt). Swaps ServerUpdatedAt
# into the UpdatedAt slot and relocates the prior client-stamped UpdatedAt to
# ClientLastWrite — the API code must switch to these same names in the same
# deploy, so run this with the API stopped, not against a live server still
# using the old ones. Idempotent: a document already in the target shape is
# left untouched, and reruns tolerate having already finished.
import argparse

from database import get_categories_collection, get_entries_collection

# A single $rename that both takes and gives 'UpdatedAt' fails outright
# (MongoDB error 40, "would create a conflict") rather than silently picking
# an order — verified empirically against this project's own dev instance.
# Three passes through a scratch name sidestep the collision instead of
# relying on same-operation ordering.
SCRATCH_FIELD = "__rename_scratch"

STALE_INDEX = "ServerUpdatedAt_1"
NEW_INDEX_FIELD = "UpdatedAt"


def _migrate(collection, dry_run: bool) -> dict[str, int]:
    counts = {
        "pending": collection.count_documents(
            {"ServerUpdatedAt": {"$exists": True}, "ClientLastWrite": {"$exists": False}}
        ),
        "already_done": collection.count_documents(
            {"ServerUpdatedAt": {"$exists": False}, "ClientLastWrite": {"$exists": True}}
        ),
        "anomalous": collection.count_documents(
            {"ServerUpdatedAt": {"$exists": True}, "ClientLastWrite": {"$exists": True}}
        ),
        # Neither field: this database was never backfilled — the prod
        # script (which derives instead of swapping) is the right one here.
        "unbackfilled": collection.count_documents(
            {"ServerUpdatedAt": {"$exists": False}, "ClientLastWrite": {"$exists": False}}
        ),
    }

    if dry_run:
        return counts

    if counts["anomalous"]:
        raise RuntimeError(
            f"{counts['anomalous']} document(s) carry both ServerUpdatedAt and "
            "ClientLastWrite — inspect before rerunning."
        )
    if counts["unbackfilled"]:
        raise RuntimeError(
            f"{counts['unbackfilled']} document(s) have neither field — this "
            "database was never backfilled; use rename_updated_at_prod.py instead."
        )

    if counts["pending"]:
        pending_filter = {"ServerUpdatedAt": {"$exists": True}, "ClientLastWrite": {"$exists": False}}
        collection.update_many(pending_filter, {"$rename": {"UpdatedAt": SCRATCH_FIELD}})
        collection.update_many({SCRATCH_FIELD: {"$exists": True}}, {"$rename": {"ServerUpdatedAt": "UpdatedAt"}})
        collection.update_many({SCRATCH_FIELD: {"$exists": True}}, {"$rename": {SCRATCH_FIELD: "ClientLastWrite"}})

    return counts


def _migrate_indexes(collection, dry_run: bool) -> None:
    # Unlike the prod script, ServerUpdatedAt and UpdatedAt are two distinct
    # field names here, so the old index is genuinely dead (indexing a name
    # no document carries any more) and a new one is genuinely missing —
    # re-checked after the drop rather than off a stale pre-drop snapshot.
    if dry_run:
        return
    if STALE_INDEX in collection.index_information():
        collection.drop_index(STALE_INDEX)
    if f"{NEW_INDEX_FIELD}_1" not in collection.index_information():
        collection.create_index(NEW_INDEX_FIELD)


def run(dry_run: bool = False) -> dict[str, dict[str, int]]:
    result = {
        "entries": _migrate(get_entries_collection(), dry_run),
        "categories": _migrate(get_categories_collection(), dry_run),
    }
    _migrate_indexes(get_entries_collection(), dry_run)
    _migrate_indexes(get_categories_collection(), dry_run)
    return result


def main() -> None:
    parser = argparse.ArgumentParser(
        description=(
            "Dev-only: swaps ServerUpdatedAt into UpdatedAt and relocates the "
            "prior UpdatedAt to ClientLastWrite. Requires backfill_server_"
            "updated_at.py to have already run."
        )
    )
    parser.add_argument("--dry-run", action="store_true", help="Counts without writing anything.")
    args = parser.parse_args()

    result = run(dry_run=args.dry_run)
    suffix = " (dry run, nothing written)" if args.dry_run else ""
    for name, counts in result.items():
        print(
            f"{name}: {counts['pending']} swapped, {counts['already_done']} already done, "
            f"{counts['anomalous']} anomalous, {counts['unbackfilled']} unbackfilled{suffix}"
        )


if __name__ == "__main__":
    main()
