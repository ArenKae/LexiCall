# Entry CRUD: CategoryIds validation, timestamps/CAS, tombstones, PUT as a
# true upsert, and the ImageBase64 write-only field (accepted here, but never
# stored on the entry document — dispatched server-side to the separate
# entry_images collection). Reads of the image resource itself
# (GET /entries/{id}/image) have their own dedicated tests in
# test_entry_images.py.
import base64
import uuid
from datetime import datetime

from lexicall_api.database import get_entries_collection

ENTRY_PAYLOAD = {
    "Word": "Ubac",
    "Definition": "Versant exposé au nord",
    "Synonyms": [],
    "ExampleSentences": [],
    "Notes": "",
    "Source": "",
    "CategoryIds": [],
}

IMAGE_BYTES = b"\xff\xd8\xff\xe0fake-jpeg-bytes"
IMAGE_BASE64 = base64.b64encode(IMAGE_BYTES).decode("ascii")


def _put_entry(client, auth_headers, entry_id=None, **overrides):
    entry_id = entry_id or str(uuid.uuid4())
    payload = {**ENTRY_PAYLOAD, **overrides}
    response = client.put(f"/entries/{entry_id}", json=payload, headers=auth_headers)
    return entry_id, response


def _create_entry(client, auth_headers, **overrides):
    _, response = _put_entry(client, auth_headers, **overrides)
    return response.json()


def test_put_entry_creates_when_id_does_not_exist(client, auth_headers):
    new_id = str(uuid.uuid4())
    _, response = _put_entry(client, auth_headers, entry_id=new_id)
    assert response.status_code == 200
    assert response.json()["Id"] == new_id
    assert response.json()["Word"] == "Ubac"

    get_response = client.get(f"/entries/{new_id}", headers=auth_headers)
    assert get_response.status_code == 200


def test_put_entry_rejects_unknown_category(client, auth_headers):
    _, response = _put_entry(client, auth_headers, CategoryIds=["inconnu"])
    assert response.status_code == 400


def test_put_entry_rejects_empty_word(client, auth_headers):
    _, response = _put_entry(client, auth_headers, Word="")
    assert response.status_code == 422


def test_put_entry_rejects_empty_definition(client, auth_headers):
    _, response = _put_entry(client, auth_headers, Definition="")
    assert response.status_code == 422


def test_update_entry_ignores_id_in_body(client, auth_headers):
    created = _create_entry(client, auth_headers)
    other_id = str(uuid.uuid4())

    update_payload = {**ENTRY_PAYLOAD, "Word": "Renomme", "Id": other_id}
    update_response = client.put(f"/entries/{created['Id']}", json=update_payload, headers=auth_headers)
    assert update_response.status_code == 200
    assert update_response.json()["Id"] == created["Id"]

    get_response = client.get(f"/entries/{created['Id']}", headers=auth_headers)
    assert get_response.status_code == 200
    assert get_response.json()["Word"] == "Renomme"


def test_delete_entry(client, auth_headers):
    created = _create_entry(client, auth_headers)

    delete_response = client.delete(f"/entries/{created['Id']}", headers=auth_headers)
    assert delete_response.status_code == 204

    get_response = client.get(f"/entries/{created['Id']}", headers=auth_headers)
    assert get_response.status_code == 404


def test_delete_entry_sets_tombstoned_at(client, auth_headers):
    # TombstonedAt backs the TTL index (database.ensure_indexes()) that
    # auto-purges old tombstones — must be a real BSON datetime, not the
    # ISO-8601 strings UpdatedAt/CreatedAt use for CAS comparisons.
    created = _create_entry(client, auth_headers)

    delete_response = client.delete(f"/entries/{created['Id']}", headers=auth_headers)
    assert delete_response.status_code == 204

    raw = get_entries_collection().find_one({"Id": created["Id"]})
    assert isinstance(raw["TombstonedAt"], datetime)


def test_get_unknown_entry_returns_404(client, auth_headers):
    response = client.get("/entries/does-not-exist", headers=auth_headers)
    assert response.status_code == 404


def test_delete_entry_is_idempotent(client, auth_headers):
    created = _create_entry(client, auth_headers)

    first_delete = client.delete(f"/entries/{created['Id']}", headers=auth_headers)
    assert first_delete.status_code == 204

    second_delete = client.delete(f"/entries/{created['Id']}", headers=auth_headers)
    assert second_delete.status_code == 204


def test_list_entries_returns_sync_timestamp_header(client, auth_headers):
    response = client.get("/entries", headers=auth_headers)
    assert response.status_code == 200
    assert "x-sync-timestamp" in response.headers

    # Immediately replaying with that same timestamp as updated_since should
    # return nothing (steady-state convergence).
    checkpoint = response.headers["x-sync-timestamp"]
    follow_up = client.get("/entries", params={"updated_since": checkpoint}, headers=auth_headers)
    assert follow_up.status_code == 200
    assert follow_up.json() == []


def test_update_entry_with_stale_updated_at_is_noop(client, auth_headers):
    created = _create_entry(client, auth_headers)

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
    created = _create_entry(client, auth_headers)

    future_timestamp = "2999-01-01T00:00:00+00:00"
    payload = {**ENTRY_PAYLOAD, "Word": "Renomme", "UpdatedAt": future_timestamp}
    response = client.put(f"/entries/{created['Id']}", json=payload, headers=auth_headers)
    assert response.status_code == 200
    # Compared as datetimes rather than exact strings: Pydantic may
    # reformat the precision (microseconds) without changing the actual
    # instant.
    assert datetime.fromisoformat(response.json()["UpdatedAt"]) == datetime.fromisoformat(future_timestamp)


def test_delete_entry_tombstones_and_appears_in_delta_pull(client, auth_headers):
    created = _create_entry(client, auth_headers)

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

    created = _create_entry(client, auth_headers)

    delta = client.get("/entries", params={"updated_since": checkpoint}, headers=auth_headers).json()
    assert any(entry["Id"] == created["Id"] for entry in delta)


def test_put_entry_creates_with_image_uploads_it(client, auth_headers):
    created = _create_entry(client, auth_headers, ImageBase64=IMAGE_BASE64)

    image_response = client.get(f"/entries/{created['Id']}/image", headers=auth_headers)
    assert image_response.status_code == 200
    assert image_response.content == IMAGE_BYTES
    assert image_response.headers["content-type"] == "image/jpeg"


def test_put_entry_creates_without_image_leaves_no_image_resource(client, auth_headers):
    created = _create_entry(client, auth_headers)

    image_response = client.get(f"/entries/{created['Id']}/image", headers=auth_headers)
    assert image_response.status_code == 404


def test_update_entry_sets_image(client, auth_headers):
    created = _create_entry(client, auth_headers)

    payload = {**ENTRY_PAYLOAD, "ImageBase64": IMAGE_BASE64}
    update_response = client.put(f"/entries/{created['Id']}", json=payload, headers=auth_headers)
    assert update_response.status_code == 200

    image_response = client.get(f"/entries/{created['Id']}/image", headers=auth_headers)
    assert image_response.status_code == 200
    assert image_response.content == IMAGE_BYTES


def test_update_entry_clearing_image_deletes_it(client, auth_headers):
    created = _create_entry(client, auth_headers, ImageBase64=IMAGE_BASE64)

    clear_payload = {**ENTRY_PAYLOAD, "ImageBase64": ""}
    update_response = client.put(f"/entries/{created['Id']}", json=clear_payload, headers=auth_headers)
    assert update_response.status_code == 200

    image_response = client.get(f"/entries/{created['Id']}/image", headers=auth_headers)
    assert image_response.status_code == 404


def test_update_entry_with_stale_updated_at_does_not_touch_image(client, auth_headers):
    created = _create_entry(client, auth_headers, ImageBase64=IMAGE_BASE64)

    # This push loses the CAS comparison (UpdatedAt is in the past): neither
    # the metadata nor the image should move, even though this push
    # explicitly asks to clear ImageBase64.
    stale_payload = {
        **ENTRY_PAYLOAD,
        "ImageBase64": "",
        "UpdatedAt": "2000-01-01T00:00:00+00:00",
    }
    response = client.put(f"/entries/{created['Id']}", json=stale_payload, headers=auth_headers)
    assert response.status_code == 200

    image_response = client.get(f"/entries/{created['Id']}/image", headers=auth_headers)
    assert image_response.status_code == 200
    assert image_response.content == IMAGE_BYTES


def test_put_entry_rejects_invalid_image_base64(client, auth_headers):
    _, response = _put_entry(client, auth_headers, ImageBase64="not-valid-base64!!")
    assert response.status_code == 400


def test_put_entry_rejects_image_too_large(client, auth_headers, monkeypatch):
    from lexicall_api.config import settings

    monkeypatch.setattr(settings, "max_image_bytes", 4)
    _, response = _put_entry(client, auth_headers, ImageBase64=IMAGE_BASE64)
    assert response.status_code == 413


def test_put_entry_preserves_client_supplied_created_at(client, auth_headers):
    past_timestamp = "2020-06-15T12:00:00+00:00"
    _, response = _put_entry(client, auth_headers, CreatedAt=past_timestamp)
    assert response.status_code == 200
    assert datetime.fromisoformat(response.json()["CreatedAt"]) == datetime.fromisoformat(past_timestamp)


def test_put_entry_update_does_not_change_created_at(client, auth_headers):
    created = _create_entry(client, auth_headers)

    # A genuine update sends CreatedAt too (the client always serializes the
    # whole local object) — $setOnInsert must keep this from ever reaching
    # an existing document, even with a deliberately different value here.
    update_payload = {**ENTRY_PAYLOAD, "Word": "Renomme", "CreatedAt": "1999-01-01T00:00:00+00:00"}
    response = client.put(f"/entries/{created['Id']}", json=update_payload, headers=auth_headers)
    assert response.status_code == 200
    assert datetime.fromisoformat(response.json()["CreatedAt"]) == datetime.fromisoformat(created["CreatedAt"])
