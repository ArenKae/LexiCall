# Read access for entry images: kept as a separate resource/collection from
# `entries` so listing/scanning entries never touches image bytes. There's no
# standalone write route here — the client always bundles the image in the
# entry PUT (see routers/entries.py:upsert_entry), which dispatches to
# entry_images_repo itself, gated on the same CAS check as the entry's
# metadata. A separate write endpoint would reopen the exact race that
# design closed: two independent requests able to set an entry's image with
# no ordering guarantee between them.
from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import Response

from lexicall_api.repositories import entry_images_repo
from lexicall_api.security import require_api_key

router = APIRouter(prefix="/entries", tags=["entry-images"], dependencies=[Depends(require_api_key)])


@router.get("/{entry_id}/image")
def get_entry_image(entry_id: str) -> Response:
    # Never called by the desktop client (images arrive bundled in
    # GET/PUT /entries) — kept for direct API inspection/debugging, and as a
    # plausible lazy-fetch route for a future bandwidth-conscious client.
    image = entry_images_repo.get_image(entry_id)
    if image is None:
        raise HTTPException(status_code=404, detail="No image for this entry.")
    return Response(content=bytes(image["ImageBytes"]), media_type=image["ContentType"])
