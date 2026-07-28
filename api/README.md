# LexiCall — API

Phase 2 backend: FastAPI + MongoDB, preparing the progressive migration of
local storage (`vocabulary.json`, desktop app) toward a server source of
truth. See the Roadmap in the [root README](../README.md).

## Quick start (Linux VM/server)

```bash
cp .env.example .env      # fill in API_KEY (see the comment in the file)
just dev-mongo-up-api      # MongoDB via docker compose (from the repo root)
just install-api            # venv + dependencies
just run-api                 # uvicorn --reload on localhost:8000
```

From `api/` directly, the same recipes exist without the `-api` suffix (`just install`,
`just run`, `just migrate`, `just dev-mongo-up`, `just dev-mongo-down`, ...). There's no
`test`/`lint` recipe yet — run directly from `api/`:

```bash
PYTHONPATH=src .venv/bin/pytest -v
```

## Authentication

All routes except `/health` require an `X-API-Key` header matching the
`API_KEY` value from `.env`.

## Entry images

Images are not stored inline on an entry: they live in a separate `entry_images` collection,
one document per entry `Id`, accessed through dedicated binary endpoints (raw bytes, not
JSON) — `GET`/`PUT`/`DELETE /entries/{id}/image`, `Content-Type` round-tripped from the `PUT`
request to the `GET` response. This is deliberate: a Mongo projection that excludes a field
only saves bandwidth to the client, not server-side cache pressure — WiredTiger still reads
the whole document (image bytes included) off disk/cache for any scan, so keeping images
inline made browsing the `entries` collection (e.g. in Compass) slower as it grew, regardless
of projections on `list_entries()`. `PUT` is capped at `max_image_bytes` (2 MB by default,
configurable via the `MAX_IMAGE_BYTES` env var); deleting an entry cascades to deleting its
image, if any.

## Migrating existing data

```bash
just migrate-api -- --input /path/to/vocabulary.json --dry-run
just migrate-api -- --input /path/to/vocabulary.json
```

Never point directly at the repo's `templates/vocabulary.json`: make a
working copy before any attempt. The script is idempotent (upsert by the
application field `Id`, never by Mongo's native `_id`) — rerunning with the
same file duplicates nothing.

Entries carrying an inline `ImageBase64` get it split out into the `entry_images` collection
(see "Entry images" above) as they're upserted, and the field is `$unset` from the `entries`
document — `--dry-run` won't show this: it returns before touching Mongo at all, so it only
validates JSON sanitization (duplicate/cyclic categories, empty `Word`/`Definition`, orphaned
`CategoryIds`), never the image split. To actually rehearse the image split before running
against production, run the migration for real against a disposable/dev Mongo first.

### One-off: splitting inline images still in Mongo

`migrate_from_json.py` only ever sees entries present in whichever `vocabulary.json` file it's
given. Entries that only ever existed through live client sync (never captured in a single
`vocabulary.json` snapshot) can still carry an inline `ImageBase64` field even after that
migration has run. `split_images_in_place.py` covers that gap: it reads `entries` straight from
Mongo — no JSON file involved — splits any inline image out to `entry_images`, and clears the
field, including entries where it's just an empty string. Idempotent, safe to rerun. No `just`
recipe wraps it yet (unlike `migrate-api`, it takes no `--input`):

```bash
cd api
PYTHONPATH=src .venv/bin/python -m lexicall_api.migration.split_images_in_place --dry-run
PYTHONPATH=src .venv/bin/python -m lexicall_api.migration.split_images_in_place
```

## Deployment

`Dockerfile`, `docker-compose.prod.yml` and `deploy/backup.sh` are ready for
a Docker deployment on the OVH VPS, but the actual rollout (and the choice
of public exposure — direct port + TLS reverse proxy vs VPN/tunnel) is
deferred to a later step, once the VPS is provisioned.
