# Shared console formatting for the manual debug tools next to this file
# (debug_enrichment.py, debug_categorization.py): numbered steps, aligned
# key/value lines, and the OpenAI pricing used for their cost estimates.
# Everything is printed by these helpers rather than by SDK/httpx debug
# logging, which would dump each request as one unreadable line.
import itertools
import shutil
import sys
import textwrap
import time
from datetime import datetime

RESET = "\033[0m"
BOLD = "\033[1m"
DIM = "\033[2m"
RED = "\033[31m"
GREEN = "\033[32m"
YELLOW = "\033[33m"
CYAN = "\033[36m"
USE_COLOR = sys.stdout.isatty()

WRAP_WIDTH = max(60, min(shutil.get_terminal_size().columns - 6, 120))
LABEL_WIDTH = 18

# gpt-5.6-luna and text-embedding-3-small, standard tier — ballpark debug
# estimates, not billing-accurate.
PRICE_PER_MILLION_INPUT = 0.20
PRICE_PER_MILLION_CACHED_INPUT = 0.02
PRICE_PER_MILLION_CACHE_WRITE = 0.25  # 1.25x the input rate
PRICE_PER_MILLION_OUTPUT = 1.20
PRICE_PER_WEB_SEARCH_CALL = 0.01  # $10 / 1k calls
PRICE_PER_MILLION_EMBEDDING = 0.02

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


def estimate_cost(usage, web_search_calls: int = 0) -> float:
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


def estimate_embedding_cost(tokens: int) -> float:
    return tokens * PRICE_PER_MILLION_EMBEDDING / 1_000_000
