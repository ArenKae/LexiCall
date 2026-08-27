# Fetches wikitext from the French Wiktionary (action=parse), with a
# near-match search fallback when the exact word has no page. Trims to the
# French-language section and strips subsections with no defining content
# (etymology, translations, pronunciation, references...); keeps synonyms
# and a capped number of literary citations as context for the Synonyms/
# ExampleSentences enrichment fields.
import re

import httpx

WIKTIONARY_API_URL = "https://fr.wiktionary.org/w/api.php"
USER_AGENT = "LexiCall/1.0 (https://github.com/ArenKae/LexiCall)"

MAX_CITATIONS_PER_ENTRY = 3

_FR_SECTION_RE = re.compile(r"==\s*\{\{langue\|fr\}\}\s*==.*?(?=\n==\s*\{\{langue\||\Z)", re.DOTALL)

_NOISE_SECTION_NAMES = (
    "étymologie", "prononciation", "abréviations", "apparentés", "dérivés", "composés",
    "vocabulaire", "phrases", "traductions", "hyperonymes",
    "anagrammes", "voir aussi", "références",
)

_NOISE_BLOCK_RES = [
    re.compile(r"\{\{trad-début.*?\{\{trad-fin\}\}", re.DOTALL),
    re.compile(r"^\*?\s*\{\{écouter.*\}\}\s*$", re.MULTILINE),
    re.compile(
        r"={2,4}\s*\{\{S\|(" + "|".join(_NOISE_SECTION_NAMES) + r")[^}]*\}\}\s*={2,4}.*?(?=\n={2,4}|\Z)",
        re.DOTALL,
    ),
    # Image embed — filename/caption params are noise. Tolerates one level of
    # nested [[link]] inside the caption, otherwise a naive .*? stops at the
    # inner link's ]] and leaves unbalanced brackets behind.
    re.compile(r"\[\[Fichier:(?:[^\[\]]|\[\[[^\[\]]*\]\])*\]\]"),
]
# Collapses blank-line runs left behind by the removals above.
_BLANK_RUN_RE = re.compile(r"\n{3,}")

# Literary citation under a numbered sense, e.g. "#* {{exemple | lang=fr |
# ... | source=...}}" — a genuine example sentence, kept (capped by
# _cap_citations, not stripped) as context for ExampleSentences.
_CITATION_RE = re.compile(r"^#\*.*?(?=\n#|\n\n|\Z)", re.DOTALL | re.MULTILINE)


def _cap_citations(text: str, max_citations: int = MAX_CITATIONS_PER_ENTRY) -> str:
    matches = list(_CITATION_RE.finditer(text))
    if len(matches) <= max_citations:
        return text
    for match in reversed(matches[max_citations:]):
        text = text[: match.start()] + text[match.end() :]
    return text


def fetch_definition_context(word: str) -> str | None:
    wikitext = _parse_wikitext(word)
    if wikitext is not None:
        return wikitext
    near_title = _search_nearmatch(word)
    if near_title is None:
        return None
    return _parse_wikitext(near_title)


def _parse_wikitext(title: str) -> str | None:
    response = httpx.get(
        WIKTIONARY_API_URL,
        params={
            "action": "parse",
            "page": title,
            "prop": "wikitext",
            "redirects": 1,
            "format": "json",
            "formatversion": 2,
        },
        headers={"User-Agent": USER_AGENT},
        timeout=5.0,
    )
    response.raise_for_status()
    data = response.json()
    if "error" in data:
        if data["error"]["code"] == "missingtitle":
            return None
        raise RuntimeError(f"Wiktionary API error: {data['error']}")
    return _clean_wikitext(data["parse"]["wikitext"])


def _clean_wikitext(wikitext: str) -> str | None:
    match = _FR_SECTION_RE.search(wikitext)
    if match is None:
        # No French section (e.g. a loanword only documented in its origin
        # language) — treated the same as a missing page.
        return None
    cleaned = match.group(0)
    for noise_re in _NOISE_BLOCK_RES:
        cleaned = noise_re.sub("", cleaned)
    cleaned = _cap_citations(cleaned)
    return _BLANK_RUN_RE.sub("\n\n", cleaned).strip()


def _search_nearmatch(word: str) -> str | None:
    response = httpx.get(
        WIKTIONARY_API_URL,
        params={
            "action": "query",
            "list": "search",
            "srsearch": word,
            "srwhat": "nearmatch",
            "srlimit": 1,
            "format": "json",
            "formatversion": 2,
        },
        headers={"User-Agent": USER_AGENT},
        timeout=5.0,
    )
    response.raise_for_status()
    results = response.json()["query"]["search"]
    return results[0]["title"] if results else None
