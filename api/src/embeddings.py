# Category embeddings — the retrieval half of auto-categorization. No LLM
# call happens here; this module only turns categories and words into
# vectors and ranks them against each other.
#
# Indexing, ahead of any user action: each live category's text
# (build_category_embedding_text) is vectorized (embed_texts) and stored in
# the category_embeddings collection alongside the exact text it came from,
# so a later pass re-embeds only the categories whose name, path or
# description actually changed.
#
# Lookup, on one user click: the word — with its definition when there is
# one — goes through the same vectorization, gets compared against every
# stored category vector (top_similar), and only the resulting top-K names
# and paths are handed to the LLM, which makes the attach-or-create
# decision. Cutting to a top-K is what keeps that LLM call's token cost flat
# however large the category corpus grows.
import numpy as np
from openai import OpenAI

from config import settings

EMBEDDING_MODEL = "text-embedding-3-small"

# Generous: real categories hold a handful of words, and the cap only exists
# so one oversized category can't drown its own name and description.
MAX_CATEGORY_SAMPLE_WORDS = 100


def embed_texts(texts: list[str]) -> list[list[float]]:
    """Vectorizes each text, in order, in a single API request. Batched
    because renaming a parent category invalidates every descendant's text
    at once — one request instead of one per category."""
    if not settings.openai_api_key:
        # Precise, actionable message instead of the SDK's generic "Missing
        # credentials..." — this is a server misconfiguration, not an
        # OpenAI-side failure.
        raise RuntimeError("OPENAI_API_KEY n'est pas configurée côté serveur (voir api/.env).")

    client = OpenAI(api_key=settings.openai_api_key)
    response = client.embeddings.create(model=EMBEDDING_MODEL, input=texts)
    return [item.embedding for item in response.data]


def build_category_embedding_text(
    category: dict,
    by_id: dict[str, dict],
    words: list[str] | None = None,
) -> str:
    """Builds the text that represents a category for embedding: its full
    hierarchical path, the nearest available description (its own, or the
    closest ancestor's), and the words actually filed under it. Child
    categories carry no description in practice, only a compact compound
    name ("Anatomie et physiologie"), so the ancestors are what give them
    any real signal.

    The member words matter because a category's label describes what it is
    called, not what it ended up holding — a category only reachable through
    its name misses words its content would have attracted. Categories that
    exist to hold sub-categories have no words of their own and simply get
    no such line."""
    ancestors = _ancestor_chain(category, by_id)
    lines = [build_category_path(category, by_id)]

    for node in reversed(ancestors):
        description = (node.get("Description") or "").strip()
        if description:
            lines.append(f"{node['Name']} : {description}")
            break

    if words:
        # Sorted so the text (and therefore the stored SourceText) doesn't
        # change just because entries came back in a different order.
        sample = sorted(words, key=str.casefold)[:MAX_CATEGORY_SAMPLE_WORDS]
        lines.append(f"Mots : {', '.join(sample)}")

    return "\n".join(lines)


def build_category_path(category: dict, by_id: dict[str, dict]) -> str:
    """The category's place in the tree as "Racine › Enfant", which is what
    identifies it for a human (and for the LLM at decision time): a child's
    own name is often too terse to stand alone ("Anatomie et physiologie")."""
    return " › ".join(node["Name"] for node in _ancestor_chain(category, by_id))


def _ancestor_chain(category: dict, by_id: dict[str, dict]) -> list[dict]:
    # Root first, the category itself last. The visited set tolerates cycles
    # and dangling ParentIds coming from hand-edited data.
    chain = [category]
    visited = {category["Id"]}
    current = category.get("ParentId")
    while current is not None and current not in visited:
        parent = by_id.get(current)
        if parent is None:
            break
        chain.append(parent)
        visited.add(current)
        current = parent.get("ParentId")
    chain.reverse()
    return chain


def top_similar(
    query_vector: list[float],
    candidates: list[tuple[str, list[float]]],
    k: int,
) -> list[tuple[str, float]]:
    """Ranks candidates against the query vector and returns the k closest
    as (category_id, cosine score), best first. Cosine similarity measures
    the angle between two vectors — 1.0 means they point the same way (same
    meaning), 0.0 means unrelated — so only a vector's direction affects the
    ranking, never its length. Full cosine rather than a bare dot product:
    one extra division at this corpus size, and no assumption that the API
    returns normalized vectors."""
    if not candidates:
        return []

    # One row per candidate, so the whole corpus becomes a single N x 1536
    # matrix. float32 halves the memory of numpy's float64 default and is
    # already far more precision than a similarity ranking needs.
    matrix = np.array([vector for _id, vector in candidates], dtype=np.float32)
    query = np.array(query_vector, dtype=np.float32)

    # Numerator of the cosine formula (a · b). The matrix-vector product
    # computes all N dot products in one operation — row i against the
    # query — giving one raw score per candidate, with no Python loop.
    dot_products = matrix @ query

    # Denominator (‖a‖ × ‖b‖). axis=1 takes the length of each row
    # separately, so this is N lengths, not one for the whole matrix; the
    # query's own length is a single number multiplied into all of them.
    norms = np.linalg.norm(matrix, axis=1) * np.linalg.norm(query)

    # Elementwise division, except where the denominator is 0: `where`
    # skips those positions and `out` supplies the value they keep instead
    # (0.0). A zero vector should never come back from the API, but
    # dividing by one would silently produce NaN scores rather than fail.
    scores = np.divide(dot_products, norms, out=np.zeros(len(matrix)), where=norms != 0)

    # argsort returns the indices that would sort the scores, not the
    # scores themselves, and always ascending — [::-1] flips it to
    # best-first, [:k] keeps the top K. Those indices still line up with
    # `candidates`, which is how each score finds its category Id again.
    best = np.argsort(scores)[::-1][:k]
    # float() unwraps numpy's own scalar type back into a plain Python float.
    return [(candidates[i][0], float(scores[i])) for i in best]
