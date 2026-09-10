# CLI wrapper around the category embedding reindex, for running the pass by
# hand on a server. Run from api/ with:
#   PYTHONPATH=src .venv/bin/python -m migration.index_category_embeddings [--dry-run]
# The same pass is reachable through POST /categories/reindex-embeddings, so
# the desktop client can repair drifted embeddings without shell access.
import argparse

import category_indexing


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

    summary = category_indexing.reindex_all(dry_run=args.dry_run)
    print(summary.render())


if __name__ == "__main__":
    main()
