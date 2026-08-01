# Category CRUD: parent validation (exists, no cycle), PUT as a true upsert
# (see test_put_category_creates_when_id_does_not_exist), and deletion
# guards replicating client-side MainWindowViewModel.DeleteCategory.
import uuid
from datetime import datetime


def _put_category(client, auth_headers, category_id=None, name="Cat", parent_id=None, **overrides):
    category_id = category_id or str(uuid.uuid4())
    payload = {"Name": name, "ParentId": parent_id, "Description": "", "IconGlyph": "", **overrides}
    response = client.put(f"/categories/{category_id}", json=payload, headers=auth_headers)
    return category_id, response


def _create_category(client, auth_headers, name="Cat", parent_id=None):
    _, response = _put_category(client, auth_headers, name=name, parent_id=parent_id)
    assert response.status_code == 200
    return response.json()


def _put_entry(client, auth_headers, **overrides):
    entry_id = str(uuid.uuid4())
    payload = {
        "Word": "Mot",
        "Definition": "Def",
        "Synonyms": [],
        "ExampleSentences": [],
        "Notes": "",
        "Source": "",
        "CategoryIds": [],
        "Tags": [],
        **overrides,
    }
    return client.put(f"/entries/{entry_id}", json=payload, headers=auth_headers)


def test_put_category_creates_and_get_category(client, auth_headers):
    created = _create_category(client, auth_headers, "Nature")
    response = client.get(f"/categories/{created['Id']}", headers=auth_headers)
    assert response.status_code == 200
    assert response.json()["Name"] == "Nature"


def test_put_category_rejects_unknown_parent(client, auth_headers):
    _, response = _put_category(client, auth_headers, name="Enfant", parent_id="inconnu")
    assert response.status_code == 400


def test_put_category_rejects_empty_name(client, auth_headers):
    _, response = _put_category(client, auth_headers, name="")
    assert response.status_code == 422


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
    _put_entry(client, auth_headers, CategoryIds=[category["Id"]])

    response = client.delete(f"/categories/{category['Id']}", headers=auth_headers)
    assert response.status_code == 409


def test_delete_unused_leaf_category_returns_204(client, auth_headers):
    category = _create_category(client, auth_headers, "Inutilisée")
    response = client.delete(f"/categories/{category['Id']}", headers=auth_headers)
    assert response.status_code == 204


def test_delete_category_succeeds_once_child_is_tombstoned(client, auth_headers):
    parent = _create_category(client, auth_headers, "Parent")
    child = _create_category(client, auth_headers, "Enfant", parent_id=parent["Id"])

    child_delete = client.delete(f"/categories/{child['Id']}", headers=auth_headers)
    assert child_delete.status_code == 204

    # The has_children guard must no longer count an already-tombstoned child.
    parent_delete = client.delete(f"/categories/{parent['Id']}", headers=auth_headers)
    assert parent_delete.status_code == 204


def test_delete_category_succeeds_once_using_entry_is_tombstoned(client, auth_headers):
    category = _create_category(client, auth_headers, "Utilisée")
    entry = _put_entry(client, auth_headers, CategoryIds=[category["Id"]]).json()

    entry_delete = client.delete(f"/entries/{entry['Id']}", headers=auth_headers)
    assert entry_delete.status_code == 204

    # The count_entries_using_category guard must no longer count an
    # already-tombstoned entry.
    category_delete = client.delete(f"/categories/{category['Id']}", headers=auth_headers)
    assert category_delete.status_code == 204


def test_delete_category_is_idempotent(client, auth_headers):
    category = _create_category(client, auth_headers, "Solo")

    first_delete = client.delete(f"/categories/{category['Id']}", headers=auth_headers)
    assert first_delete.status_code == 204

    # Second call: the category is already tombstoned, so it's not found by
    # the live view the router's pre-check uses — 404, not 204. Documents
    # the accepted asymmetry with entries.delete_entry (idempotent as
    # 204/204): the client already treats 404 and success as equivalent
    # (TryDeleteAsync).
    second_delete = client.delete(f"/categories/{category['Id']}", headers=auth_headers)
    assert second_delete.status_code == 404


def test_put_category_creates_when_id_does_not_exist(client, auth_headers):
    new_id = str(uuid.uuid4())
    _, response = _put_category(client, auth_headers, category_id=new_id, name="Nouvelle")
    assert response.status_code == 200
    assert response.json()["Id"] == new_id
    assert response.json()["Name"] == "Nouvelle"

    get_response = client.get(f"/categories/{new_id}", headers=auth_headers)
    assert get_response.status_code == 200


def test_put_category_update_does_not_change_created_at(client, auth_headers):
    created = _create_category(client, auth_headers, "Original")

    update_payload = {
        "Name": "Renommée",
        "ParentId": None,
        "Description": "",
        "IconGlyph": "",
        "CreatedAt": "1999-01-01T00:00:00+00:00",
    }
    response = client.put(f"/categories/{created['Id']}", json=update_payload, headers=auth_headers)
    assert response.status_code == 200
    assert datetime.fromisoformat(response.json()["CreatedAt"]) == datetime.fromisoformat(created["CreatedAt"])
