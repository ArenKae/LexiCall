# Computes and stores the embedding of every live category — the one-shot
# initial indexing pass behind auto-categorization. Run from api/ with:
#   PYTHONPATH=src .venv/bin/python -m lexicall_api.migration.index_category_embeddings [--dry-run]
# Idempotent: a category whose text hasn't changed since its stored
# embedding is skipped, so a re-run costs nothing. Also drops embeddings
# left behind by categories that no longer exist, which would otherwise keep
# coming back as retrieval candidates.
import argparse
from dataclasses import dataclass

from lexicall_api import embeddings
from lexicall_api.repositories import categories_repo, category_embeddings_repo


@dataclass
class IndexSummary:
    embedded: int = 0
    unchanged: int = 0
    orphans_removed: int = 0

    def render(self) -> str:
        return (
            "Index summary:\n"
            f"  Categories embedded: {self.embedded}\n"
            f"  Categories unchanged (skipped): {self.unchanged}\n"
            f"  Orphaned embeddings removed: {self.orphans_removed}"
        )


def run(dry_run: bool = False) -> IndexSummary:
    summary = IndexSummary()
    categories = categories_repo.list_categories()
    by_id = {category["Id"]: category for category in categories}
    stored = {doc["Id"]: doc for doc in category_embeddings_repo.list_embeddings()}

    pending: list[tuple[str, str]] = []
    for category in categories:
        source_text = embeddings.build_category_embedding_text(category, by_id)
        existing = stored.get(category["Id"])
        if existing is not None and existing.get("SourceText") == source_text:
            summary.unchanged += 1
            continue
        pending.append((category["Id"], source_text))
    summary.embedded = len(pending)

    # A tombstoned category is excluded from list_categories above, so its
    # leftover embedding lands here and gets dropped.
    for orphan_id in stored.keys() - by_id.keys():
        summary.orphans_removed += 1
        if not dry_run:
            category_embeddings_repo.delete_embedding(orphan_id)

    # Nothing is sent to the embeddings API on a dry run, so counting stays free.
    if pending and not dry_run:
        vectors = embeddings.embed_texts([text for _id, text in pending])
        for (category_id, source_text), vector in zip(pending, vectors, strict=True):
            category_embeddings_repo.upsert_embedding(category_id, vector, source_text)

    return summary


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Computes and stores the embedding of every category, for auto-categorization retrieval."
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Counts without writing anything or calling the embeddings API.",
    )
    args = parser.parse_args()

    summary = run(dry_run=args.dry_run)
    print(summary.render())


if __name__ == "__main__":
    main()
