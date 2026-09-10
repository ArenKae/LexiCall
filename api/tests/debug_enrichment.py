# Manual debug tool for the entry-enrichment pipeline (grounding + the
# specialized LLM calls). Not a pytest test — lives here because it exercises
# the API's own modules directly and isn't part of the installable package.
#
# Runs the real suggest_entry_enrichment and reports every HTTP call it made,
# by intercepting the OpenAI and Wiktionary clients rather than rebuilding the
# pipeline here, so it cannot drift from the code it debugs.
#
# Usage:
#   PYTHONPATH=src .venv/bin/python tests/debug_enrichment.py <mot> [--locked <champs>]
#
# <mot>     : any word — simulates a brand-new, all-empty entry for it,
#             never written to the database.
# <champs>  : comma-separated subset of Definition, Type, Synonyms,
#             ExampleSentences (e.g. "Synonyms,Type") to simulate as locked.
import argparse
import json
import sys
import threading
import time

from _debug_console import (
    BOLD,
    CYAN,
    DIM,
    GREEN,
    RED,
    YELLOW,
    c,
    estimate_cost,
    kv,
    kv_wrapped,
    request_line,
    response_line,
    step,
    step_done,
)

import enrichment
import llm_client
import wiktionary_client

_lock = threading.Lock()
_llm_calls: list[dict] = []
_wiktionary_titles: list[tuple[str, bool]] = []


class _RecordingResponses:
    def __init__(self, inner):
        self._inner = inner

    def create(self, **payload):
        started = time.perf_counter()
        response = self._inner.create(**payload)
        with _lock:
            _llm_calls.append(
                {
                    "started": started,
                    "elapsed": time.perf_counter() - started,
                    "payload": payload,
                    "response": response,
                }
            )
        return response


class _RecordingClient:
    def __init__(self, inner):
        self.responses = _RecordingResponses(inner.responses)


def install_recorders() -> None:
    real_openai = llm_client.OpenAI
    llm_client.OpenAI = lambda **kwargs: _RecordingClient(real_openai(**kwargs))

    real_parse = wiktionary_client._parse_wikitext

    def recording_parse(title: str):
        wikitext = real_parse(title)
        _wiktionary_titles.append((title, wikitext is not None))
        return wikitext

    wiktionary_client._parse_wikitext = recording_parse


def build_synthetic_entry(word: str, locked_fields: list[str]) -> dict:
    return {
        "Word": word,
        "Definition": [],
        "Type": ["Undefined"],
        "Synonyms": [],
        "ExampleSentences": [],
        "LockedFields": locked_fields,
    }


def report_wiktionary() -> None:
    t0 = step("Wiktionnaire — titres candidats essayés")
    request_line("GET", wiktionary_client.WIKTIONARY_API_URL)
    if not _wiktionary_titles:
        response_line("aucune requête", ok=False)
    for title, found in _wiktionary_titles:
        response_line(f"{title!r} — {'section française trouvée' if found else 'rien'}", ok=found)
    step_done(t0)


def report_llm_call(call: dict) -> None:
    payload = call["payload"]
    name = payload["text"]["format"]["name"]
    tools = payload.get("tools") or []
    title = f"Appel LLM — {name}" + (" (web_search forcé)" if tools else "")
    step(title)
    request_line("POST", "https://api.openai.com/v1/responses")
    kv("reasoning", payload["reasoning"]["effort"])
    kv("tools", "web_search (tool_choice=required)" if tools else "aucun")
    kv("instructions", f"{len(payload['instructions'])} caractères")
    kv_wrapped("instructions", payload["instructions"])
    kv_wrapped("input", payload["input"])
    kv_wrapped("schéma JSON", json.dumps(payload["text"]["format"]["schema"], ensure_ascii=False))

    response = call["response"]
    result = json.loads(response.output_text)
    response_line("200 OK")
    kv("items", ", ".join(item.type for item in response.output))
    for key, value in result.items():
        if value is None:
            kv(key, c(DIM, "aucune suggestion"))
        elif isinstance(value, dict):
            kv_wrapped(key, f"{value['value']!r} — {value['justification'] or 'sans justification'}")
        else:
            kv_wrapped(key, str(value))
    print(c(DIM, f"  ⏱ {call['elapsed']:.2f}s"))


def main() -> None:
    parser = argparse.ArgumentParser(description="Debug complet du pipeline d'enrichissement d'entrée.")
    parser.add_argument("word", help="Mot à tester (simule une entrée vide pour ce mot, jamais écrite en base)")
    parser.add_argument(
        "--locked",
        default="",
        help=f"Champs à simuler verrouillés, séparés par des virgules, parmi : {', '.join(enrichment.ENRICHABLE_FIELDS)}",
    )
    args = parser.parse_args()
    locked_fields = [f.strip() for f in args.locked.split(",") if f.strip()]

    install_recorders()
    print(c(BOLD, f"Pipeline d'enrichissement — mot : {c(CYAN, args.word)}"))
    if locked_fields:
        print(c(YELLOW, f"Champs verrouillés simulés : {', '.join(locked_fields)}"))

    entry = build_synthetic_entry(args.word, locked_fields)
    pipeline_start = time.perf_counter()
    result = enrichment.suggest_entry_enrichment(entry)
    total_elapsed = time.perf_counter() - pipeline_start

    report_wiktionary()
    for call in sorted(_llm_calls, key=lambda call: call["started"]):
        report_llm_call(call)

    step("Résumé")
    kv("mot", args.word)
    if not _llm_calls:
        kv("résultat", c(YELLOW, "aucun appel LLM (tous les champs verrouillés)"))
    elif not result.get("word_recognized", True):
        kv("résultat", c(RED, "word_recognized=false") + " — mot non reconnu, aucune suggestion")
    else:
        for field in enrichment.ENRICHABLE_FIELDS:
            key = enrichment._FIELD_SCHEMA_KEYS[field]
            suggestion = result.get(key)
            if key not in result:
                kv(key, c(DIM, "verrouillé ou appel échoué"))
            elif suggestion is None:
                kv(key, c(DIM, "aucune suggestion"))
            else:
                kv_wrapped(key, str(suggestion["value"]), color=GREEN)
                if suggestion["justification"]:
                    kv_wrapped(f"{key} (justif.)", suggestion["justification"])

    web_calls = sum(
        1 for call in _llm_calls for item in call["response"].output if item.type == "web_search_call"
    )
    cost = sum(
        estimate_cost(
            call["response"].usage,
            sum(1 for item in call["response"].output if item.type == "web_search_call"),
        )
        for call in _llm_calls
    )
    kv("appels LLM", str(len(_llm_calls)))
    kv("tokens entrée", str(sum(call["response"].usage.input_tokens for call in _llm_calls)))
    kv("tokens sortie", str(sum(call["response"].usage.output_tokens for call in _llm_calls)))
    kv("web_search", f"{web_calls} appel(s)")
    kv("coût estimé", c(BOLD, f"${cost:.6f}") + " (tarif standard gpt-5.6-luna, cache et web_search inclus)")
    kv("durée totale", f"{total_elapsed:.2f}s")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(c(RED, f"\n✗ Erreur : {exc}"))
        sys.exit(1)
