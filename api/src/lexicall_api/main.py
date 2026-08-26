# FastAPI entry point: assembles the routers, exposes / and /health,
# initializes Mongo indexes on startup.
import json
from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI
from fastapi.responses import Response

from lexicall_api.database import ensure_indexes, ping
from lexicall_api.routers import categories, enrichment, entries, entry_images, wiktionary
from lexicall_api.security import require_api_key


@asynccontextmanager
async def lifespan(app: FastAPI):
    ensure_indexes()
    yield


app = FastAPI(title="LexiCall API", lifespan=lifespan)

app.include_router(entries.router)
app.include_router(categories.router)
app.include_router(entry_images.router)
app.include_router(enrichment.router)
app.include_router(wiktionary.router)


@app.get("/", tags=["health"])
def root() -> Response:
    # Identifies the service (useful to quickly check which API is answering
    # a given URL/port) without requiring an API key.
    body = {"service": "LexiCall API", "docs": "/docs"}
    return Response(content=json.dumps(body) + "\n", media_type="application/json")


@app.get("/health", tags=["health"])
def health() -> Response:
    mongo_ok = ping()
    body = {"status": "ok" if mongo_ok else "degraded", "mongo": "ok" if mongo_ok else "ko"}
    return Response(content=json.dumps(body) + "\n", media_type="application/json")


@app.get("/auth", tags=["health"], dependencies=[Depends(require_api_key)])
def auth_check() -> Response:
    # Dedicated authenticated connectivity check (e.g. the client's "Tester
    # la connexion" button): the dependency alone validates the key, without
    # running any Mongo query, unlike /entries.
    body = {"status": "ok"}
    return Response(content=json.dumps(body) + "\n", media_type="application/json")
