# / and /health don't require an API key; /health reflects the state of
# the Mongo connection.
def test_root_identifies_service(client):
    response = client.get("/")
    assert response.status_code == 200
    assert response.json()["service"] == "LexiCall API"


def test_health_ok(client):
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json()["status"] == "ok"
