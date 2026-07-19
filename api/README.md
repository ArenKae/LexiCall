# LexiCall — API

Phase 2 backend: FastAPI + MongoDB, preparing the progressive migration of
local storage (`vocabulary.json`, desktop app) toward a server source of
truth. See the Roadmap in the [root README](../README.md).

## Quick start (Linux VM/server)

```bash
cp .env.example .env      # fill in API_KEY (see the comment in the file)
just mongo-up-api          # MongoDB via docker compose (from the repo root)
just install-api            # venv + dependencies
just run-api                 # uvicorn --reload on localhost:8000
```

From `api/` directly, the same recipes exist without the `-api` suffix
(`just install`, `just run`, `just test`, `just lint`, `just mongo-up`, ...).

## Authentication

All routes except `/health` require an `X-API-Key` header matching the
`API_KEY` value from `.env`.

## Migrating existing data

```bash
just migrate-api -- --input /path/to/vocabulary.json --dry-run
just migrate-api -- --input /path/to/vocabulary.json
```

Never point directly at the repo's `templates/vocabulary.json`: make a
working copy before any attempt. The script is idempotent (upsert by the
application field `Id`, never by Mongo's native `_id`) — rerunning with the
same file duplicates nothing.

## Deployment

`Dockerfile`, `docker-compose.prod.yml` and `deploy/backup.sh` are ready for
a Docker deployment on the OVH VPS, but the actual rollout (and the choice
of public exposure — direct port + TLS reverse proxy vs VPN/tunnel) is
deferred to a later step, once the VPS is provisioned.
