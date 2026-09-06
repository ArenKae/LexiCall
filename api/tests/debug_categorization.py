# Manual debug tool for the auto-categorization pipeline (query embedding +
# cosine retrieval + LLM decision). Not a pytest test — lives here because it
# exercises the API's own modules directly and isn't part of the installable
# package.
#
# Usage:
#   PYTHONPATH=src .venv/bin/python tests/debug_categorization.py <mot> [--definition <texte>] [-k <n>] [--retrieval-only]
#
# <mot>     : the word to categorize. Nothing is ever written to the database.
# --definition : strongly recommended — a bare rare word is close to noise to
#             the embedding model, the definition is what makes retrieval work.
# -k        : how many candidates to retrieve (default: the API's own).
# --retrieval-only : stop after the ranking, skipping the paid LLM decision.
#
# Reads the stored category embeddings as-is: run the reindex first
# (POST /categories/reindex-embeddings) if the corpus changed.
import argparse
import json
import sys
import time

from _debug_console import (
    BOLD,
    CYAN,
    GREEN,
    RED,
    YELLOW,
    c,
    estimate_cost,
    estimate_embedding_cost,
    kv,
    kv_wrapped,
    request_line,
    response_line,
    step,
    step_done,
)
from openai import OpenAI

from lexicall_api import embeddings, enrichment, llm_client
from lexicall_api.config import settings
from lexicall_api.repositories import categories_repo, category_embeddings_repo

client = OpenAI(api_key=settings.openai_api_key)


def embedding_step(word: str, definition: str) -> tuple[list[float], float]:
    """Vectorizes the query the same way find_category_candidates does, but
    through the SDK directly so the token usage is visible."""
    query_text = f"{word} — {definition}".strip(" —") if definition.strip() else word

    t0 = step("Vectorisation de la requête")
    request_line("POST", "https://api.openai.com/v1/embeddings")
    kv("modèle", embeddings.EMBEDDING_MODEL)
    kv_wrapped("texte vectorisé", query_text, color=CYAN)

    response = client.embeddings.create(model=embeddings.EMBEDDING_MODEL, input=[query_text])
    vector = response.data[0].embedding
    cost = estimate_embedding_cost(response.usage.total_tokens)

    response_line(f"vecteur de {len(vector)} dimensions")
    kv("tokens", str(response.usage.total_tokens))
    kv("coût", f"${cost:.8f}")
    step_done(t0)
    return vector, cost


def retrieval_step(query_vector: list[float], k: int) -> list[dict]:
    t0 = step("Retrieval — similarité cosinus locale (aucun appel réseau)")
    stored = category_embeddings_repo.list_embeddings()
    kv("vecteurs stockés", str(len(stored)))

    if not stored:
        response_line("index vide — lance la réindexation des catégories", ok=False)
        step_done(t0)
        return []

    ranked = embeddings.top_similar(query_vector, [(d["Id"], d["Vector"]) for d in stored], k)
    categories = categories_repo.list_categories()
    by_id = {category["Id"]: category for category in categories}

    candidates = []
    for category_id, score in ranked:
        category = by_id.get(category_id)
        if category is None:
            continue
        candidates.append(
            {
                "id": category_id,
                "name": category["Name"],
                "path": embeddings.build_category_path(category, by_id),
                "score": score,
            }
        )

    response_line(f"top-{len(candidates)}")
    for rank, candidate in enumerate(candidates, start=1):
        kv(f"#{rank}  {candidate['score']:.4f}", candidate["path"])
    step_done(t0)
    return candidates


def roots_step() -> list[dict]:
    """The root categories, always sent alongside the top-K: a word whose
    lexical field is missing from the corpus ranks nothing useful, and
    without these the model could only pick among wrong answers instead of
    proposing a new category under a sensible parent."""
    t0 = step("Catégories racines (toujours jointes au prompt)")
    categories = categories_repo.list_categories()
    by_id = {category["Id"]: category for category in categories}
    roots = [
        {
            "id": category["Id"],
            "name": category["Name"],
            "path": embeddings.build_category_path(category, by_id),
        }
        for category in categories
        if by_id.get(category.get("ParentId")) is None
    ]
    kv("racines", str(len(roots)))
    kv_wrapped("noms", ", ".join(root["name"] for root in roots))
    step_done(t0)
    return roots


def decision_step(word: str, definition: str, candidates: list[dict], roots: list[dict]) -> tuple[dict, object]:
    prompt = enrichment._build_categorization_prompt(word, definition, candidates, roots)
    schema = enrichment._build_categorization_schema(
        [candidate["id"] for candidate in candidates],
        [root["id"] for root in roots],
    )
    payload = {
        "model": llm_client.MODEL,
        "input": prompt,
        "instructions": enrichment.CATEGORIZATION_INSTRUCTIONS,
        "text": {
            "format": {
                "type": "json_schema",
                "name": "categorization",
                "schema": schema,
                "strict": True,
            },
        },
        "reasoning": {"effort": "low"},
        "tools": [],
    }

    t0 = step(f"Décision LLM ({llm_client.MODEL})")
    request_line("POST", "https://api.openai.com/v1/responses")
    kv("reasoning", payload["reasoning"]["effort"])
    kv("ids autorisés", f"{len(candidates)} candidat(s) + {len(roots)} racine(s), enum fermé")
    kv_wrapped("instructions", payload["instructions"])
    kv_wrapped("input", prompt)
    kv_wrapped("schéma JSON", json.dumps(schema, ensure_ascii=False))

    response = client.responses.create(**payload)
    result = json.loads(response.output_text)
    response_line("200 OK")
    kv_wrapped("sortie brute", json.dumps(result, ensure_ascii=False))
    step_done(t0)
    return result, response


def resolution_step(result: dict) -> dict | None:
    """Turns the model's ids back into full categories and enforces the
    coherence the enums can't: "existing" must actually name a category."""
    t0 = step("Résolution des ids et contrôle de cohérence")
    by_id = {category["Id"]: category for category in categories_repo.list_categories()}
    try:
        suggestion = enrichment._resolve_categorization(result, by_id)
    except RuntimeError as exc:
        response_line(f"rejeté (deviendrait un 502) : {exc}", ok=False)
        step_done(t0)
        return None

    if suggestion["decision"] == "existing":
        kv("décision", c(GREEN, "RATTACHER"))
        kv("catégorie", c(GREEN, suggestion["category"]["path"]))
    else:
        parent = suggestion["new_category_parent"]
        kv("décision", c(YELLOW, "CRÉER"))
        kv("nouveau nom", c(YELLOW, suggestion["new_category_name"]))
        kv("parent", parent["path"] if parent else "(racine)")
    kv_wrapped("justification", suggestion["justification"])
    step_done(t0)
    return suggestion


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Debug complet du pipeline de catégorisation (embedding + retrieval + décision LLM)."
    )
    parser.add_argument("word", help="Mot à catégoriser")
    parser.add_argument("--definition", default="", help="Définition du mot (fortement recommandée)")
    parser.add_argument("-k", type=int, default=enrichment.DEFAULT_CATEGORY_CANDIDATES, help="Nombre de candidats")
    parser.add_argument("--retrieval-only", action="store_true", help="S'arrête avant la décision LLM (payante).")
    args = parser.parse_args()

    pipeline_start = time.perf_counter()
    print(c(BOLD, f"Pipeline de catégorisation — mot : {c(CYAN, args.word)}"))
    if not args.definition.strip():
        print(c(YELLOW, "Aucune définition fournie — le retrieval sera nettement moins fiable."))

    query_vector, embedding_cost = embedding_step(args.word, args.definition)
    candidates = retrieval_step(query_vector, args.k)
    roots = roots_step()

    if args.retrieval_only:
        step("Résumé")
        kv("mode", "retrieval seul (aucune décision LLM)")
        kv("coût", f"${embedding_cost:.8f}")
        kv("durée totale", f"{time.perf_counter() - pipeline_start:.2f}s")
        return

    result, response = decision_step(args.word, args.definition, candidates, roots)
    resolution_step(result)

    step("Résumé")
    usage = response.usage
    total_cost = embedding_cost + estimate_cost(usage)
    kv("mot", args.word)
    kv("candidats retenus", str(len(candidates)))
    kv("tokens entrée", f"{usage.input_tokens} (dont {usage.input_tokens_details.cached_tokens} depuis le cache)")
    kv("tokens sortie", f"{usage.output_tokens} (dont {usage.output_tokens_details.reasoning_tokens} de raisonnement)")
    kv("coût estimé", c(BOLD, f"${total_cost:.6f}") + " (embedding + décision)")
    kv("durée totale", f"{time.perf_counter() - pipeline_start:.2f}s")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(c(RED, f"\n✗ Erreur : {exc}"))
        sys.exit(1)
