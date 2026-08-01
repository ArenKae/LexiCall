# Read access for entry images: separate resource/collection from entries,
# see entry_images.py / entry_images_repo.py. Writes go through the entry
# PUT (see test_entries.py) — this file only covers the GET route and the
# cascade-on-delete side effect.
import base64
import uuid

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
IMAGE_BASE64 = base64.b64encode(IMAGE_BYTES).decode("ascii")


def _create_entry(client, auth_headers, image_base64=None) -> str:
    entry_id = str(uuid.uuid4())
    payload = {**ENTRY_PAYLOAD, "ImageBase64": image_base64}
    response = client.put(f"/entries/{entry_id}", json=payload, headers=auth_headers)
    return response.json()["Id"]


def test_get_image(client, auth_headers):
    entry_id = _create_entry(client, auth_headers, image_base64=IMAGE_BASE64)

    response = client.get(f"/entries/{entry_id}/image", headers=auth_headers)
    assert response.status_code == 200
    assert response.content == IMAGE_BYTES
    assert response.headers["content-type"] == "image/jpeg"


def test_get_image_missing_returns_404(client, auth_headers):
    entry_id = _create_entry(client, auth_headers)

    response = client.get(f"/entries/{entry_id}/image", headers=auth_headers)
    assert response.status_code == 404


def test_get_image_unknown_entry_returns_404(client, auth_headers):
    response = client.get("/entries/does-not-exist/image", headers=auth_headers)
    assert response.status_code == 404


def test_delete_entry_cascades_to_image(client, auth_headers):
    entry_id = _create_entry(client, auth_headers, image_base64=IMAGE_BASE64)

    delete_response = client.delete(f"/entries/{entry_id}", headers=auth_headers)
    assert delete_response.status_code == 204

    get_response = client.get(f"/entries/{entry_id}/image", headers=auth_headers)
    assert get_response.status_code == 404
