# Fetches wikitext from the French Wiktionary (action=parse), with a
# near-match title search fallback when the exact word has no page. Returned
# wikitext is trimmed to the French-language section, stripped of subsections
# that carry no defining content (etymology, translations, related/derived
# word lists, idioms, emoji abbreviations, references...), and stripped of
# the literary citation supporting each numbered sense (#*) — the sense
# itself (#) already states the meaning, the quote is corroborating evidence
# a definition prompt doesn't need. Raw pages can otherwise run to tens of
# thousands of characters, the vast majority of it irrelevant to writing a
# definition; this was found by inspecting real entries (e.g. "chien"), not
# guessed — extend _NOISE_SECTION_NAMES if another word surfaces more.
import re

import httpx

WIKTIONARY_API_URL = "https://fr.wiktionary.org/w/api.php"
USER_AGENT = "LexiCall/1.0 (https://github.com/ArenKae/LexiCall)"

_FR_SECTION_RE = re.compile(r"==\s*\{\{langue\|fr\}\}\s*==.*?(?=\n==\s*\{\{langue\||\Z)", re.DOTALL)

_NOISE_SECTION_NAMES = (
    "étymologie", "prononciation", "abréviations", "apparentés", "dérivés", "composés",
    "synonymes", "quasi-synonymes", "vocabulaire", "phrases", "traductions", "hyperonymes",
    "anagrammes", "voir aussi", "références",
)

_NOISE_BLOCK_RES = [
    re.compile(r"\{\{trad-début.*?\{\{trad-fin\}\}", re.DOTALL),
    re.compile(r"^\*?\s*\{\{écouter.*\}\}\s*$", re.MULTILINE),
    re.compile(
        r"={2,4}\s*\{\{S\|(" + "|".join(_NOISE_SECTION_NAMES) + r")[^}]*\}\}\s*={2,4}.*?(?=\n={2,4}|\Z)",
        re.DOTALL,
    ),
    # Literary citation/example bullet under a numbered sense, e.g.
    # "#* {{exemple | lang=fr | ... | source=...}}" — can span several lines.
    re.compile(r"^#\*.*?(?=\n#|\n\n|\Z)", re.DOTALL | re.MULTILINE),
    # Image embed, e.g. "[[Fichier:Chien de race Barbet.jpg|vignette|180px|Un
    # '''chien''' (1)]]" — filename/thumbnail params are pure noise, and the
    # caption (when present) only restates what the sense text next to it
    # already says. Allows one level of nested [[link]] inside the caption
    # (seen in real entries), otherwise a naive .*? would stop at the inner
    # link's closing ]] and leave unbalanced brackets behind.
    re.compile(r"\[\[Fichier:(?:[^\[\]]|\[\[[^\[\]]*\]\])*\]\]"),
]
# Collapses the blank-line runs the removals above leave behind — applied
# separately since it replaces with "\n\n", not "" like the removals do.
_BLANK_RUN_RE = re.compile(r"\n{3,}")


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
        # No French section on this page (e.g. a loanword documented only in
        # its language of origin) — treated the same as a missing page, not
        # worth sending irrelevant foreign-language content as context.
        return None
    cleaned = match.group(0)
    for noise_re in _NOISE_BLOCK_RES:
        cleaned = noise_re.sub("", cleaned)
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
