# FastAPI entry point: assembles the routers, exposes / and /health,
# initializes Mongo indexes on startup.
from contextlib import asynccontextmanager

from fastapi import FastAPI

from lexicall_api.database import ensure_indexes, ping
from lexicall_api.routers import categories, entries


@asynccontextmanager
async def lifespan(app: FastAPI):
    ensure_indexes()
    yield


app = FastAPI(title="LexiCall API", lifespan=lifespan)

app.include_router(entries.router)
app.include_router(categories.router)


@app.get("/", tags=["health"])
def root() -> dict:
    # Identifies the service (useful to quickly check which API is answering
    # a given URL/port) without requiring an API key.
    return {"service": "LexiCall API", "docs": "/docs"}


@app.get("/health", tags=["health"])
def health() -> dict:
    mongo_ok = ping()
    return {"status": "ok" if mongo_ok else "degraded", "mongo": "ok" if mongo_ok else "ko"}
