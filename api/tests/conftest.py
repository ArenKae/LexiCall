# Shared pytest configuration: isolated test Mongo database (dropped before/after
# each test), FastAPI TestClient, authentication headers.
import os

os.environ.setdefault("API_KEY", "test-api-key")
os.environ.setdefault("MONGO_URI", "mongodb://localhost:27017")
os.environ.setdefault("MONGO_DB_NAME", "lexicall_test")

import pytest
from fastapi.testclient import TestClient

from lexicall_api.database import get_entries_collection
from lexicall_api.main import app


@pytest.fixture(autouse=True)
def clean_database():
    db = get_entries_collection().database
    db.drop_collection("entries")
    db.drop_collection("categories")
    db.drop_collection("entry_images")
    yield
    db.drop_collection("entries")
    db.drop_collection("categories")
    db.drop_collection("entry_images")


@pytest.fixture
def client():
    # Must enter the TestClient as a context manager, not just instantiate it,
    # otherwise FastAPI's lifespan never runs and ensure_indexes() never
    # creates the unique Id index in the test database — duplicate-Id
    # rejection would silently pass without ever being exercised.
    with TestClient(app) as test_client:
        yield test_client


@pytest.fixture
def auth_headers() -> dict:
    return {"X-API-Key": os.environ["API_KEY"]}
