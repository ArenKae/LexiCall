# Static API key authentication (X-API-Key header) — single-user usage,
# no accounts/sessions.
import secrets

from fastapi import Header, HTTPException

from lexicall_api.config import settings


def require_api_key(x_api_key: str = Header(...)) -> None:
    if not secrets.compare_digest(x_api_key, settings.api_key):
        raise HTTPException(status_code=401, detail="Invalid API key.")
