# The static API key (X-API-Key) protects every route except / and /health.
def test_missing_api_key_returns_422(client):
    response = client.get("/entries")
    assert response.status_code == 422


def test_wrong_api_key_returns_401(client):
    response = client.get("/entries", headers={"X-API-Key": "wrong"})
    assert response.status_code == 401


def test_valid_api_key_returns_200(client, auth_headers):
    response = client.get("/entries", headers=auth_headers)
    assert response.status_code == 200
    assert response.json() == []


# /auth: dedicated connectivity check, no Mongo query behind it.
def test_auth_wrong_api_key_returns_401(client):
    response = client.get("/auth", headers={"X-API-Key": "wrong"})
    assert response.status_code == 401


def test_auth_valid_api_key_returns_200(client, auth_headers):
    response = client.get("/auth", headers=auth_headers)
    assert response.status_code == 200
    assert response.json()["status"] == "ok"
