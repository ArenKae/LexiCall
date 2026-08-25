# GET /wiktionary/{word}: exact match, near-match title fallback, and the
# clean "nothing found" case — httpx.get mocked, no real network call.
import httpx

from lexicall_api import wiktionary_client


def _response(url, json_body):
    return httpx.Response(200, json=json_body, request=httpx.Request("GET", url))


def test_lookup_exact_match(client, auth_headers, monkeypatch):
    def fake_get(url, *, params, **kwargs):
        assert params["page"] == "chien"
        return _response(url, {"parse": {"title": "chien", "pageid": 1, "wikitext": "Un chien est un animal."}})

    monkeypatch.setattr(wiktionary_client.httpx, "get", fake_get)

    response = client.get("/wiktionary/chien", headers=auth_headers)

    assert response.status_code == 200
    assert response.json() == {"word": "chien", "wikitext": "Un chien est un animal."}


def test_lookup_falls_back_to_nearmatch(client, auth_headers, monkeypatch):
    calls = []

    def fake_get(url, *, params, **kwargs):
        calls.append(params)
        if params["action"] == "parse" and params["page"] == "Chien":
            return _response(url, {"error": {"code": "missingtitle", "info": "not found"}})
        if params["action"] == "query":
            return _response(url, {"query": {"search": [{"title": "chien"}]}})
        if params["action"] == "parse" and params["page"] == "chien":
            return _response(url, {"parse": {"title": "chien", "pageid": 1, "wikitext": "Un chien est un animal."}})
        raise AssertionError(f"unexpected call: {params}")

    monkeypatch.setattr(wiktionary_client.httpx, "get", fake_get)

    response = client.get("/wiktionary/Chien", headers=auth_headers)

    assert response.status_code == 200
    assert response.json() == {"word": "Chien", "wikitext": "Un chien est un animal."}
    assert len(calls) == 3


def test_lookup_nothing_found(client, auth_headers, monkeypatch):
    def fake_get(url, *, params, **kwargs):
        if params["action"] == "parse":
            return _response(url, {"error": {"code": "missingtitle", "info": "not found"}})
        return _response(url, {"query": {"search": []}})

    monkeypatch.setattr(wiktionary_client.httpx, "get", fake_get)

    response = client.get("/wiktionary/zzzzznotarealword9999", headers=auth_headers)

    assert response.status_code == 200
    assert response.json() == {"word": "zzzzznotarealword9999", "wikitext": None}
