# FastAPI entry point: assembles the routers, exposes / and /health,
# initializes Mongo indexes on startup.
import json
from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI
from fastapi.responses import Response

from lexicall_api.database import ensure_indexes, ping
from lexicall_api.routers import categories, entries, entry_images
from lexicall_api.security import require_api_key


@asynccontextmanager
async def lifespan(app: FastAPI):
    ensure_indexes()
    yield


app = FastAPI(title="LexiCall API", lifespan=lifespan)

app.include_router(entries.router)
app.include_router(categories.router)
app.include_router(entry_images.router)


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
    # Dédié au test de connectivité authentifié (ex. bouton « Tester la
    # connexion » côté client) : la dépendance suffit à valider la clé,
    # sans exécuter la moindre requête Mongo, contrairement à /entries.
    body = {"status": "ok"}
    return Response(content=json.dumps(body) + "\n", media_type="application/json")
