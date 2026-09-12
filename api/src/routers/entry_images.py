# Read-only access to entry images, kept in their own collection so that
# scanning `entries` never pages image bytes into cache. Writing goes through
# the entry PUT instead, so two requests can never race to set the same image.
from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import Response

from repositories import entry_images_repo
from security import require_api_key

router = APIRouter(prefix="/entries", tags=["entry-images"], dependencies=[Depends(require_api_key)])


@router.get("/{entry_id}/images/{image_id}")
def get_entry_image(entry_id: str, image_id: str) -> Response:
    # entry_id shapes the nested path but takes no part in the lookup:
    # entry_images is keyed by the image's own Id alone.
    image = entry_images_repo.get_image(image_id)
    if image is None:
        raise HTTPException(status_code=404, detail="No image for this entry.")
    return Response(content=bytes(image["ImageBytes"]), media_type=image["ContentType"])
