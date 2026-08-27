# Manual debug tool for the entry-enrichment pipeline (Wiktionary lookup +
# LLM call). Not a pytest test — lives here because it exercises the API's
# own modules directly and isn't part of the installable package.
#
# Usage:
#   PYTHONPATH=src .venv/bin/python tests/debug_pipeline.py <mot> [--locked <champs>]
#
# <mot>     : any word — simulates a brand-new, all-empty entry for it,
#             never written to the database.
# <champs>  : comma-separated subset of Definition, Type, Synonyms,
#             ExampleSentences (e.g. "Synonyms,Type") to simulate as locked.
import argparse
import itertools
import json
import shutil
import sys
import textwrap
import time
from datetime import datetime

from openai import OpenAI

from lexicall_api import enrichment, llm_client, wiktionary_client
from lexicall_api.config import settings

RESET = "\033[0m"
BOLD = "\033[1m"
DIM = "\033[2m"
RED = "\033[31m"
GREEN = "\033[32m"
YELLOW = "\033[33m"
CYAN = "\033[36m"
USE_COLOR = sys.stdout.isatty()

WRAP_WIDTH = max(60, min(shutil.get_terminal_size().columns - 6, 120))
LABEL_WIDTH = 14

# gpt-5.6-luna, standard tier — ballpark debug estimate, not billing-accurate.
PRICE_PER_MILLION_INPUT = 0.20
PRICE_PER_MILLION_CACHED_INPUT = 0.02
PRICE_PER_MILLION_CACHE_WRITE = 0.25  # 1.25x the input rate
PRICE_PER_MILLION_OUTPUT = 1.20
PRICE_PER_WEB_SEARCH_CALL = 0.01  # $10 / 1k calls, same across the gpt-5.6 family

_step_counter = itertools.count(1)


def c(code: str, text: str) -> str:
    return f"{code}{text}{RESET}" if USE_COLOR else text


def step(title: str) -> float:
    n = next(_step_counter)
    now = datetime.now().strftime("%H:%M:%S.%f")[:-3]
    print(f"\n{c(BOLD + CYAN, f'━━━ [{now}] Étape {n} — {title} ━━━')}")
    return time.perf_counter()


def step_done(start: float) -> float:
    elapsed = time.perf_counter() - start
    print(c(DIM, f"  ⏱ {elapsed:.2f}s"))
    return elapsed


def request_line(method: str, url: str) -> None:
    print(f"  {c(CYAN, f'→ {method} {url}')}")


def response_line(outcome: str, ok: bool = True) -> None:
    print(f"  {c(GREEN if ok else YELLOW, f'← {outcome}')}")


def kv(label: str, value: str) -> None:
    print(f"    {c(DIM, label.ljust(LABEL_WIDTH))} {value}")


def kv_wrapped(label: str, value: str, color: str = DIM) -> None:
    indent = " " * (LABEL_WIDTH + 5)
    wrapped = textwrap.fill(value, width=WRAP_WIDTH, subsequent_indent=indent)
    print(f"    {c(DIM, label.ljust(LABEL_WIDTH))} {c(color, wrapped)}")


def estimate_cost(usage, web_search_calls: int) -> float:
    cached = usage.input_tokens_details.cached_tokens
    cache_write = usage.input_tokens_details.cache_write_tokens
    plain_input = usage.input_tokens - cached - cache_write
    token_cost = (
        plain_input * PRICE_PER_MILLION_INPUT
        + cached * PRICE_PER_MILLION_CACHED_INPUT
        + cache_write * PRICE_PER_MILLION_CACHE_WRITE
        + usage.output_tokens * PRICE_PER_MILLION_OUTPUT
    ) / 1_000_000
    return token_cost + web_search_calls * PRICE_PER_WEB_SEARCH_CALL


def build_synthetic_entry(word: str, locked_fields: list[str]) -> dict:
    return {
        "Word": word,
        "Definition": "",
        "Type": "Undefined",
        "Synonyms": [],
        "ExampleSentences": [],
        "LockedFields": locked_fields,
    }


def wiktionary_parse_step(title: str, page: str) -> str | None:
    t0 = step(title)
    request_line("GET", wiktionary_client.WIKTIONARY_API_URL)
    kv("paramètres", f"action=parse  page={page!r}  prop=wikitext  redirects=1  format=json  formatversion=2")
    wikitext = wiktionary_client._parse_wikitext(page)
    if wikitext is not None:
        response_line(f"trouvé — {len(wikitext)} caractères (après nettoyage)")
        kv_wrapped("aperçu", wikitext[:200] + "...")
    else:
        response_line("introuvable ou sans section française", ok=False)
    step_done(t0)
    return wikitext


def wiktionary_nearmatch_step(word: str) -> str | None:
    t0 = step("Wiktionnaire — recherche de titres proches (action=query, srwhat=nearmatch)")
    request_line("GET", wiktionary_client.WIKTIONARY_API_URL)
    kv("paramètres", f"action=query  list=search  srsearch={word!r}  srwhat=nearmatch  srlimit=1  format=json  formatversion=2")
    near_title = wiktionary_client._search_nearmatch(word)
    if near_title:
        response_line(f"titre proche trouvé : {near_title!r}")
    else:
        response_line("aucun titre proche", ok=False)
    step_done(t0)
    return near_title


def llm_step(entry: dict, context: str | None) -> tuple[dict, object] | None:
    locked = set(entry.get("LockedFields", []))
    unlocked = [f for f in enrichment.ENRICHABLE_FIELDS if f not in locked]

    t0 = step("Analyse des champs verrouillés")
    kv("déverrouillés", ", ".join(unlocked) if unlocked else "aucun")
    kv("verrouillés", ", ".join(locked) if locked else "aucun")
    step_done(t0)

    if not unlocked:
        print(c(YELLOW, "\n  Tous les champs sont verrouillés — aucun appel LLM (comportement réel de suggest_entry_enrichment)."))
        return None

    uses_web_search = context is None
    prompt = enrichment._build_entry_enrichment_prompt(entry, unlocked, context)
    schema = enrichment._build_entry_enrichment_schema(unlocked)
    payload = {
        "model": llm_client.MODEL,
        "input": prompt,
        "instructions": enrichment.ENTRY_ENRICHMENT_INSTRUCTIONS,
        "text": {
            "format": {
                "type": "json_schema",
                "name": "entry_enrichment",
                "schema": schema,
                "strict": True,
            },
        },
        "reasoning": {"effort": "low"},
        "tools": [{"type": "web_search"}] if uses_web_search else [],
    }
    if uses_web_search:
        payload["tool_choice"] = "required"

    title = f"Appel LLM ({llm_client.MODEL})" + (" — web_search forcé" if uses_web_search else "")
    t0 = step(title)
    request_line("POST", "https://api.openai.com/v1/responses")
    kv("reasoning", payload["reasoning"]["effort"])
    kv("tools", "web_search (tool_choice=required)" if uses_web_search else "aucun")
    kv("schéma — champs", ", ".join(schema["properties"].keys()))
    kv_wrapped("instructions", payload["instructions"])
    kv_wrapped("input", prompt)
    kv_wrapped("schéma JSON", json.dumps(schema, ensure_ascii=False))

    client = OpenAI(api_key=settings.openai_api_key)
    response = client.responses.create(**payload)

    result = json.loads(response.output_text)
    response_line("200 OK")
    kv("items", ", ".join(item.type for item in response.output))
    word_recognized = result.get("word_recognized", True)
    kv("word_recognized", c(GREEN, "true") if word_recognized else c(RED, "false"))
    for field_key, suggestion in result.items():
        if field_key == "word_recognized":
            continue
        if suggestion is None:
            kv(field_key, c(DIM, "aucune suggestion"))
        else:
            kv_wrapped(field_key, f"{suggestion['value']!r} — {suggestion['justification'] or 'sans justification'}")
    step_done(t0)

    return result, response


def main() -> None:
    parser = argparse.ArgumentParser(description="Debug complet du pipeline d'enrichissement d'entrée (Wiktionnaire + LLM).")
    parser.add_argument("word", help="Mot à tester (simule une entrée vide pour ce mot, jamais écrite en base)")
    parser.add_argument(
        "--locked",
        default="",
        help=f"Champs à simuler verrouillés, séparés par des virgules, parmi : {', '.join(enrichment.ENRICHABLE_FIELDS)}",
    )
    args = parser.parse_args()
    word = args.word
    locked_fields = [f.strip() for f in args.locked.split(",") if f.strip()]

    pipeline_start = time.perf_counter()
    print(c(BOLD, f"Pipeline d'enrichissement — mot : {c(CYAN, word)}"))
    if locked_fields:
        print(c(YELLOW, f"Champs verrouillés simulés : {', '.join(locked_fields)}"))

    entry = build_synthetic_entry(word, locked_fields)

    wikitext = wiktionary_parse_step("Wiktionnaire — recherche exacte (action=parse)", word)

    if wikitext is None:
        near_title = wiktionary_nearmatch_step(word)
        if near_title:
            wikitext = wiktionary_parse_step(f"Wiktionnaire — nouvelle tentative avec {near_title!r}", near_title)

    context = wikitext
    llm_result = llm_step(entry, context)

    step("Résumé")
    total_elapsed = time.perf_counter() - pipeline_start
    kv("mot", word)
    kv("contexte", "Wiktionnaire" if context else c(YELLOW, "aucun (web_search utilisé)"))

    if llm_result is None:
        kv("résultat", c(YELLOW, "aucun appel LLM (tous les champs verrouillés)"))
        kv("durée totale", f"{total_elapsed:.2f}s")
        return

    result, response = llm_result
    if not result.get("word_recognized", True):
        kv("word_recognized", c(RED, "false") + " — mot non reconnu, aucune suggestion retournée par l'endpoint réel")
    else:
        for field_key in ("definition", "type", "synonyms", "example_sentences"):
            suggestion = result.get(field_key)
            if field_key not in result:
                kv(field_key, c(DIM, "verrouillé"))
            elif suggestion is None:
                kv(field_key, c(DIM, "aucune suggestion"))
            else:
                kv_wrapped(field_key, str(suggestion["value"]), color=GREEN)
                if suggestion["justification"]:
                    kv_wrapped(f"{field_key} (justif.)", suggestion["justification"])

    web_search_calls = sum(1 for item in response.output if item.type == "web_search_call")
    cost = estimate_cost(response.usage, web_search_calls)
    usage = response.usage
    kv("tokens entrée", f"{usage.input_tokens} (dont {usage.input_tokens_details.cached_tokens} lus depuis le cache,"
       f" {usage.input_tokens_details.cache_write_tokens} écrits en cache)")
    kv("tokens sortie", f"{usage.output_tokens} (dont {usage.output_tokens_details.reasoning_tokens} de raisonnement)")
    kv("web_search", str(web_search_calls) + " appel(s)")
    kv("coût estimé", c(BOLD, f"${cost:.6f}") + " (tarif standard gpt-5.6-luna, cache et web_search inclus)")
    kv("durée totale", f"{total_elapsed:.2f}s")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(c(RED, f"\n✗ Erreur : {exc}"))
        sys.exit(1)
