# Entry CRUD: CategoryIds validation. Image handling has its own dedicated
# tests, see test_entry_images.py — images are a separate resource entirely
# (entry_images collection), not a field on the entry itself.
from datetime import datetime

ENTRY_PAYLOAD = {
    "Word": "Ubac",
    "Definition": "Versant exposé au nord",
    "Synonyms": [],
    "ExampleSentences": [],
    "Notes": "",
    "Source": "",
    "CategoryIds": [],
    "Tags": [],
}


def test_create_and_get_entry(client, auth_headers):
    create_response = client.post("/entries", json=ENTRY_PAYLOAD, headers=auth_headers)
    assert create_response.status_code == 201
    created = create_response.json()
    assert created["Word"] == "Ubac"

    get_response = client.get(f"/entries/{created['Id']}", headers=auth_headers)
    assert get_response.status_code == 200
    assert get_response.json()["Id"] == created["Id"]


def test_create_entry_rejects_unknown_category(client, auth_headers):
    payload = {**ENTRY_PAYLOAD, "CategoryIds": ["inconnu"]}
    response = client.post("/entries", json=payload, headers=auth_headers)
    assert response.status_code == 400


def test_create_entry_rejects_empty_word(client, auth_headers):
    payload = {**ENTRY_PAYLOAD, "Word": ""}
    response = client.post("/entries", json=payload, headers=auth_headers)
    assert response.status_code == 422


def test_create_entry_rejects_empty_definition(client, auth_headers):
    payload = {**ENTRY_PAYLOAD, "Definition": ""}
    response = client.post("/entries", json=payload, headers=auth_headers)
    assert response.status_code == 422


def test_create_entry_without_id_generates_one(client, auth_headers):
    created = client.post("/entries", json=ENTRY_PAYLOAD, headers=auth_headers).json()
    assert created["Id"]


def test_create_entry_preserves_client_supplied_id(client, auth_headers):
    client_id = "11111111-1111-1111-1111-111111111111"
    payload = {**ENTRY_PAYLOAD, "Id": client_id}
    created = client.post("/entries", json=payload, headers=auth_headers).json()
    assert created["Id"] == client_id

    get_response = client.get(f"/entries/{client_id}", headers=auth_headers)
    assert get_response.status_code == 200


def test_create_entry_rejects_duplicate_id(client, auth_headers):
    client_id = "22222222-2222-2222-2222-222222222222"
    payload = {**ENTRY_PAYLOAD, "Id": client_id}
    first_response = client.post("/entries", json=payload, headers=auth_headers)
    assert first_response.status_code == 201

    second_response = client.post("/entries", json=payload, headers=auth_headers)
    assert second_response.status_code == 409


def test_update_entry_ignores_id_in_body(client, auth_headers):
    created = client.post("/entries", json=ENTRY_PAYLOAD, headers=auth_headers).json()
    other_id = "33333333-3333-3333-3333-333333333333"

    update_payload = {**ENTRY_PAYLOAD, "Word": "Renomme", "Id": other_id}
    update_response = client.put(f"/entries/{created['Id']}", json=update_payload, headers=auth_headers)
    assert update_response.status_code == 200
    assert update_response.json()["Id"] == created["Id"]

    get_response = client.get(f"/entries/{created['Id']}", headers=auth_headers)
    assert get_response.status_code == 200
    assert get_response.json()["Word"] == "Renomme"


def test_delete_entry(client, auth_headers):
    created = client.post("/entries", json=ENTRY_PAYLOAD, headers=auth_headers).json()

    delete_response = client.delete(f"/entries/{created['Id']}", headers=auth_headers)
    assert delete_response.status_code == 204

    get_response = client.get(f"/entries/{created['Id']}", headers=auth_headers)
    assert get_response.status_code == 404


def test_get_unknown_entry_returns_404(client, auth_headers):
    response = client.get("/entries/does-not-exist", headers=auth_headers)
    assert response.status_code == 404


def test_delete_entry_is_idempotent(client, auth_headers):
    created = client.post("/entries", json=ENTRY_PAYLOAD, headers=auth_headers).json()

    first_delete = client.delete(f"/entries/{created['Id']}", headers=auth_headers)
    assert first_delete.status_code == 204

    second_delete = client.delete(f"/entries/{created['Id']}", headers=auth_headers)
    assert second_delete.status_code == 204


def test_list_entries_returns_sync_timestamp_header(client, auth_headers):
    response = client.get("/entries", headers=auth_headers)
    assert response.status_code == 200
    assert "x-sync-timestamp" in response.headers

    # Rejouer immédiatement avec ce même timestamp comme updated_since ne
    # doit plus rien renvoyer (convergence en régime stable).
    checkpoint = response.headers["x-sync-timestamp"]
    follow_up = client.get("/entries", params={"updated_since": checkpoint}, headers=auth_headers)
    assert follow_up.status_code == 200
    assert follow_up.json() == []


def test_update_entry_with_stale_updated_at_is_noop(client, auth_headers):
    created = client.post("/entries", json=ENTRY_PAYLOAD, headers=auth_headers).json()

    stale_payload = {
        **ENTRY_PAYLOAD,
        "Word": "Ne-doit-pas-s'appliquer",
        "UpdatedAt": "2000-01-01T00:00:00+00:00",
    }
    response = client.put(f"/entries/{created['Id']}", json=stale_payload, headers=auth_headers)
    assert response.status_code == 200
    assert response.json()["Word"] == "Ubac"
    assert response.json()["UpdatedAt"] == created["UpdatedAt"]


def test_update_entry_trusts_client_supplied_updated_at(client, auth_headers):
    created = client.post("/entries", json=ENTRY_PAYLOAD, headers=auth_headers).json()

    future_timestamp = "2999-01-01T00:00:00+00:00"
    payload = {**ENTRY_PAYLOAD, "Word": "Renomme", "UpdatedAt": future_timestamp}
    response = client.put(f"/entries/{created['Id']}", json=payload, headers=auth_headers)
    assert response.status_code == 200
    # Comparaison en datetime plutôt qu'en chaîne exacte : Pydantic peut
    # reformater la précision (microsecondes) sans changer l'instant réel.
    assert datetime.fromisoformat(response.json()["UpdatedAt"]) == datetime.fromisoformat(future_timestamp)


def test_create_entry_preserves_client_supplied_created_at(client, auth_headers):
    past_timestamp = "2020-06-15T12:00:00+00:00"
    payload = {**ENTRY_PAYLOAD, "CreatedAt": past_timestamp}
    created = client.post("/entries", json=payload, headers=auth_headers).json()
    assert datetime.fromisoformat(created["CreatedAt"]) == datetime.fromisoformat(past_timestamp)


def test_delete_entry_tombstones_and_appears_in_delta_pull(client, auth_headers):
    created = client.post("/entries", json=ENTRY_PAYLOAD, headers=auth_headers).json()

    checkpoint = client.get("/entries", headers=auth_headers).headers["x-sync-timestamp"]

    delete_response = client.delete(f"/entries/{created['Id']}", headers=auth_headers)
    assert delete_response.status_code == 204

    plain_list = client.get("/entries", headers=auth_headers).json()
    assert all(entry["Id"] != created["Id"] for entry in plain_list)

    delta = client.get("/entries", params={"updated_since": checkpoint}, headers=auth_headers).json()
    matching = [entry for entry in delta if entry["Id"] == created["Id"]]
    assert len(matching) == 1
    assert matching[0]["IsDeleted"] is True


def test_new_entry_appears_in_delta_pull(client, auth_headers):
    checkpoint = client.get("/entries", headers=auth_headers).headers["x-sync-timestamp"]

    created = client.post("/entries", json=ENTRY_PAYLOAD, headers=auth_headers).json()

    delta = client.get("/entries", params={"updated_since": checkpoint}, headers=auth_headers).json()
    assert any(entry["Id"] == created["Id"] for entry in delta)
