# Binary CRUD for entry images: kept as a separate resource/collection from
# `entries` so listing/scanning entries never touches image bytes.
from fastapi import APIRouter, Depends, HTTPException, Request, status
from fastapi.responses import Response

from lexicall_api.config import settings
from lexicall_api.repositories import entries_repo, entry_images_repo
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/entries", tags=["entry-images"], dependencies=[Depends(require_api_key)])


@router.get("/{entry_id}/image")
def get_entry_image(entry_id: str) -> Response:
    image = entry_images_repo.get_image(entry_id)
    if image is None:
        raise HTTPException(status_code=404, detail="No image for this entry.")
    return Response(content=bytes(image["ImageBytes"]), media_type=image["ContentType"])


@router.put("/{entry_id}/image", status_code=status.HTTP_204_NO_CONTENT)
async def upsert_entry_image(entry_id: str, request: Request) -> None:
    if entries_repo.get_entry(entry_id) is None:
        raise HTTPException(status_code=404, detail="Entry not found.")

    body = await request.body()
    if not body:
        raise HTTPException(status_code=400, detail="Empty image body.")
    if len(body) > settings.max_image_bytes:
        raise HTTPException(status_code=413, detail="Image too large.")

    content_type = request.headers.get("content-type") or "image/jpeg"
    entry_images_repo.upsert_image(entry_id, body, content_type)


@router.delete("/{entry_id}/image", status_code=status.HTTP_204_NO_CONTENT)
def delete_entry_image(entry_id: str) -> None:
    if not entry_images_repo.delete_image(entry_id):
        raise HTTPException(status_code=404, detail="No image for this entry.")
