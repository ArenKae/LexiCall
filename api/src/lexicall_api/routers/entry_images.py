# Read-only: entry images live in their own collection so scanning `entries`
# never touches image bytes. There's no write route here — the client always
# bundles images with the entry PUT instead, which is what keeps independent
# requests from racing to set the same image.
from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import Response

from lexicall_api.repositories import entry_images_repo
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/entries", tags=["entry-images"], dependencies=[Depends(require_api_key)])


@router.get("/{entry_id}/images/{image_id}")
def get_entry_image(entry_id: str, image_id: str) -> Response:
    # entry_id only shapes a RESTful nested-resource path — entry_images is
    # keyed purely by the image's own Id since the multi-image redesign, so
    # it plays no part in the lookup itself.
    # Never called by the desktop client (images arrive bundled in
    # GET/PUT /entries) — kept for direct API inspection/debugging, and as a
    # plausible lazy-fetch route for a future bandwidth-conscious client.
    image = entry_images_repo.get_image(image_id)
    if image is None:
        raise HTTPException(status_code=404, detail="No image for this entry.")
    return Response(content=bytes(image["ImageBytes"]), media_type=image["ContentType"])
