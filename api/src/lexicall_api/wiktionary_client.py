# Fetches raw wikitext from the French Wiktionary (action=parse), with a
# near-match title search fallback when the exact word has no page.
import httpx

WIKTIONARY_API_URL = "https://fr.wiktionary.org/w/api.php"
USER_AGENT = "LexiCall/1.0 (https://github.com/ArenKae/LexiCall)"


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
    return data["parse"]["wikitext"]


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
