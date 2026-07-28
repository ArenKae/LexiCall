# Binary CRUD for entry images: separate resource from entries, see
# entry_images.py / entry_images_repo.py.
from lexicall_api.config import settings

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

IMAGE_BYTES = b"\xff\xd8\xff\xe0fake-jpeg-bytes"


def _create_entry(client, auth_headers) -> str:
    return client.post("/entries", json=ENTRY_PAYLOAD, headers=auth_headers).json()["Id"]


def test_put_then_get_image(client, auth_headers):
    entry_id = _create_entry(client, auth_headers)

    put_response = client.put(
        f"/entries/{entry_id}/image",
        content=IMAGE_BYTES,
        headers={**auth_headers, "Content-Type": "image/jpeg"},
    )
    assert put_response.status_code == 204

    get_response = client.get(f"/entries/{entry_id}/image", headers=auth_headers)
    assert get_response.status_code == 200
    assert get_response.content == IMAGE_BYTES
    assert get_response.headers["content-type"] == "image/jpeg"


def test_put_image_unknown_entry_returns_404(client, auth_headers):
    response = client.put(
        "/entries/does-not-exist/image",
        content=IMAGE_BYTES,
        headers={**auth_headers, "Content-Type": "image/jpeg"},
    )
    assert response.status_code == 404


def test_get_image_missing_returns_404(client, auth_headers):
    entry_id = _create_entry(client, auth_headers)

    response = client.get(f"/entries/{entry_id}/image", headers=auth_headers)
    assert response.status_code == 404


def test_delete_image(client, auth_headers):
    entry_id = _create_entry(client, auth_headers)
    client.put(
        f"/entries/{entry_id}/image",
        content=IMAGE_BYTES,
        headers={**auth_headers, "Content-Type": "image/jpeg"},
    )

    delete_response = client.delete(f"/entries/{entry_id}/image", headers=auth_headers)
    assert delete_response.status_code == 204

    get_response = client.get(f"/entries/{entry_id}/image", headers=auth_headers)
    assert get_response.status_code == 404


def test_delete_missing_image_returns_404(client, auth_headers):
    entry_id = _create_entry(client, auth_headers)

    response = client.delete(f"/entries/{entry_id}/image", headers=auth_headers)
    assert response.status_code == 404


def test_put_image_too_large_returns_413(client, auth_headers, monkeypatch):
    monkeypatch.setattr(settings, "max_image_bytes", 4)
    entry_id = _create_entry(client, auth_headers)

    response = client.put(
        f"/entries/{entry_id}/image",
        content=IMAGE_BYTES,
        headers={**auth_headers, "Content-Type": "image/jpeg"},
    )
    assert response.status_code == 413


def test_delete_entry_cascades_to_image(client, auth_headers):
    entry_id = _create_entry(client, auth_headers)
    client.put(
        f"/entries/{entry_id}/image",
        content=IMAGE_BYTES,
        headers={**auth_headers, "Content-Type": "image/jpeg"},
    )

    delete_response = client.delete(f"/entries/{entry_id}", headers=auth_headers)
    assert delete_response.status_code == 204

    get_response = client.get(f"/entries/{entry_id}/image", headers=auth_headers)
    assert get_response.status_code == 404
