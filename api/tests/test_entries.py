# Entry CRUD: CategoryIds validation, projection without image on list.
ENTRY_PAYLOAD = {
    "Word": "Ubac",
    "Definition": "Versant exposé au nord",
    "Synonyms": [],
    "ExampleSentences": [],
    "Notes": "",
    "Source": "",
    "CategoryIds": [],
    "Tags": [],
    "ImageBase64": "",
}


def test_create_and_get_entry(client, auth_headers):
    create_response = client.post("/entries", json=ENTRY_PAYLOAD, headers=auth_headers)
    assert create_response.status_code == 201
    created = create_response.json()
    assert created["Word"] == "Ubac"

    get_response = client.get(f"/entries/{created['Id']}", headers=auth_headers)
    assert get_response.status_code == 200
    assert get_response.json()["Id"] == created["Id"]


def test_list_entries_excludes_image(client, auth_headers):
    payload = {**ENTRY_PAYLOAD, "ImageBase64": "abc123"}
    client.post("/entries", json=payload, headers=auth_headers)

    list_response = client.get("/entries", headers=auth_headers)
    assert list_response.status_code == 200
    assert "ImageBase64" not in list_response.json()[0]


def test_get_entry_includes_image(client, auth_headers):
    payload = {**ENTRY_PAYLOAD, "ImageBase64": "abc123"}
    created = client.post("/entries", json=payload, headers=auth_headers).json()

    get_response = client.get(f"/entries/{created['Id']}", headers=auth_headers)
    assert get_response.json()["ImageBase64"] == "abc123"


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


def test_delete_entry(client, auth_headers):
    created = client.post("/entries", json=ENTRY_PAYLOAD, headers=auth_headers).json()

    delete_response = client.delete(f"/entries/{created['Id']}", headers=auth_headers)
    assert delete_response.status_code == 204

    get_response = client.get(f"/entries/{created['Id']}", headers=auth_headers)
    assert get_response.status_code == 404


def test_get_unknown_entry_returns_404(client, auth_headers):
    response = client.get("/entries/does-not-exist", headers=auth_headers)
    assert response.status_code == 404
