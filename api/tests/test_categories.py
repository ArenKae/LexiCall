# Category CRUD: parent validation (exists, no cycle), and deletion guards
# replicating client-side MainWindowViewModel.DeleteCategory.
def _create_category(client, auth_headers, name="Cat", parent_id=None):
    payload = {"Name": name, "ParentId": parent_id, "Description": "", "IconGlyph": ""}
    response = client.post("/categories", json=payload, headers=auth_headers)
    assert response.status_code == 201
    return response.json()


def test_create_and_get_category(client, auth_headers):
    created = _create_category(client, auth_headers, "Nature")
    response = client.get(f"/categories/{created['Id']}", headers=auth_headers)
    assert response.status_code == 200
    assert response.json()["Name"] == "Nature"


def test_create_category_rejects_unknown_parent(client, auth_headers):
    payload = {"Name": "Enfant", "ParentId": "inconnu", "Description": "", "IconGlyph": ""}
    response = client.post("/categories", json=payload, headers=auth_headers)
    assert response.status_code == 400


def test_create_category_rejects_empty_name(client, auth_headers):
    payload = {"Name": "", "ParentId": None, "Description": "", "IconGlyph": ""}
    response = client.post("/categories", json=payload, headers=auth_headers)
    assert response.status_code == 422


def test_create_category_preserves_client_supplied_id(client, auth_headers):
    client_id = "44444444-4444-4444-4444-444444444444"
    payload = {"Name": "Avec id", "ParentId": None, "Description": "", "IconGlyph": "", "Id": client_id}
    response = client.post("/categories", json=payload, headers=auth_headers)
    assert response.status_code == 201
    assert response.json()["Id"] == client_id


def test_create_category_rejects_duplicate_id(client, auth_headers):
    client_id = "55555555-5555-5555-5555-555555555555"
    payload = {"Name": "Doublon", "ParentId": None, "Description": "", "IconGlyph": "", "Id": client_id}
    first_response = client.post("/categories", json=payload, headers=auth_headers)
    assert first_response.status_code == 201

    second_response = client.post("/categories", json=payload, headers=auth_headers)
    assert second_response.status_code == 409


def test_update_category_rejects_cycle(client, auth_headers):
    parent = _create_category(client, auth_headers, "Parent")
    child = _create_category(client, auth_headers, "Enfant", parent_id=parent["Id"])

    payload = {"Name": "Parent", "ParentId": child["Id"], "Description": "", "IconGlyph": ""}
    response = client.put(f"/categories/{parent['Id']}", json=payload, headers=auth_headers)
    assert response.status_code == 400


def test_delete_category_with_children_returns_409(client, auth_headers):
    parent = _create_category(client, auth_headers, "Parent")
    _create_category(client, auth_headers, "Enfant", parent_id=parent["Id"])

    response = client.delete(f"/categories/{parent['Id']}", headers=auth_headers)
    assert response.status_code == 409


def test_delete_category_used_by_entry_returns_409(client, auth_headers):
    category = _create_category(client, auth_headers, "Utilisée")
    entry_payload = {
        "Word": "Mot",
        "Definition": "Def",
        "Synonyms": [],
        "ExampleSentences": [],
        "Notes": "",
        "Source": "",
        "CategoryIds": [category["Id"]],
        "Tags": [],
        "ImageBase64": "",
    }
    client.post("/entries", json=entry_payload, headers=auth_headers)

    response = client.delete(f"/categories/{category['Id']}", headers=auth_headers)
    assert response.status_code == 409


def test_delete_unused_leaf_category_returns_204(client, auth_headers):
    category = _create_category(client, auth_headers, "Inutilisée")
    response = client.delete(f"/categories/{category['Id']}", headers=auth_headers)
    assert response.status_code == 204
