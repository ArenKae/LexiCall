# Keeps the category_embeddings collection in step with the categories
# themselves. Two entry points, both idempotent through the stored
# SourceText: refresh_subtree() after a single category write, and
# reindex_all() for a full reconciliation pass — the repair path for
# embeddings that drifted, since refresh_subtree deliberately swallows its
# own failures rather than failing the category write it follows.
import logging
from dataclasses import dataclass

from lexicall_api import embeddings
from lexicall_api.repositories import categories_repo, category_embeddings_repo, entries_repo

logger = logging.getLogger(__name__)


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


def reindex_all(dry_run: bool = False) -> IndexSummary:
    """Re-embeds every live category whose text changed and drops
    embeddings whose category is gone. Safe to run repeatedly: a corpus
    already up to date costs no embeddings call at all."""
    summary = IndexSummary()
    categories = categories_repo.list_categories()
    by_id = {category["Id"]: category for category in categories}
    words = _words_by_category()
    stored = {doc["Id"]: doc for doc in category_embeddings_repo.list_embeddings()}

    pending: list[tuple[str, str]] = []
    for category in categories:
        source_text = embeddings.build_category_embedding_text(
            category, by_id, words.get(category["Id"])
        )
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

    # Nothing reaches the embeddings API on a dry run, so counting stays free.
    if not dry_run:
        _embed_and_store(pending)
    return summary


def refresh_subtree(category_id: str) -> None:
    """Re-embeds a category and its descendants after a write, since
    renaming or moving one rewrites the hierarchical path of everything
    below it. Only the categories whose text actually changed are sent, so
    an ordinary sync push costs no embeddings call at all.

    Best-effort: the category write itself already succeeded, so a failure
    here must not fail the request. Whatever drifts is repaired by
    reindex_all."""
    try:
        categories = categories_repo.list_categories()
        by_id = {category["Id"]: category for category in categories}
        words = _words_by_category()

        pending: list[tuple[str, str]] = []
        for affected_id in _with_descendants(category_id, categories):
            category = by_id.get(affected_id)
            if category is None:
                continue
            source_text = embeddings.build_category_embedding_text(
                category, by_id, words.get(affected_id)
            )
            stored = category_embeddings_repo.get_embedding(affected_id)
            if stored is not None and stored.get("SourceText") == source_text:
                continue
            pending.append((affected_id, source_text))

        _embed_and_store(pending)
    except Exception:
        logger.exception("Embedding refresh failed for category %s", category_id)


def _words_by_category() -> dict[str, list[str]]:
    """The words actually filed under each category. Categories that only
    hold sub-categories end up absent here, which is what leaves their
    embedding text to the path and description alone."""
    words: dict[str, list[str]] = {}
    for entry in entries_repo.list_words_with_categories():
        for category_id in entry.get("CategoryIds", []):
            words.setdefault(category_id, []).append(entry["Word"])
    return words


def _embed_and_store(pending: list[tuple[str, str]]) -> None:
    if not pending:
        return
    vectors = embeddings.embed_texts([text for _id, text in pending])
    for (category_id, source_text), vector in zip(pending, vectors, strict=True):
        category_embeddings_repo.upsert_embedding(category_id, vector, source_text)


def _with_descendants(category_id: str, categories: list[dict]) -> set[str]:
    children: dict[str, list[str]] = {}
    for category in categories:
        parent_id = category.get("ParentId")
        if parent_id is not None:
            children.setdefault(parent_id, []).append(category["Id"])

    affected = {category_id}
    queue = [category_id]
    while queue:
        for child_id in children.get(queue.pop(), []):
            # Doubles as a cycle guard on hand-edited data.
            if child_id not in affected:
                affected.add(child_id)
                queue.append(child_id)
    return affected
