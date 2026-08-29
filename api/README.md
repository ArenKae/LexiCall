# LexiCall — API

FastAPI + MongoDB backend, mirroring the desktop client's data (`vocabulary.json`) through a
bidirectional, Last-Write-Wins sync layer. See the [root README](../README.md) for the
overall architecture and design rationale.

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

## AI enrichment

Foundational plumbing for upcoming AI-assisted features (definition suggestion, field
enrichment, auto-categorization) — this section covers only what's actually implemented so far.
`llm_client.py` wraps the OpenAI Responses API, locked to model `gpt-5.6-luna` (a lightweight
snapshot, fitting for this project's classification/retrieval-style tasks) for the whole
project. It centralizes three things reused by every future AI feature: structured JSON
output (`text.format = {type: "json_schema", strict: true}`), reasoning effort (`reasoning.effort`,
default `"low"`), and the built-in `web_search` tool. The OpenAI key lives only in `api/.env`
(`OPENAI_API_KEY`) — never exposed to the desktop client, which keeps talking exclusively to this
API as usual.

Structured Outputs in strict mode requires `additionalProperties: false` and every property listed
in `required` — the wrapper passes the schema through as-is, so a schema that doesn't follow this
shape is rejected by OpenAI, not caught locally.

`POST /enrichment/fields` judges, per field (Definition/Type/Synonyms/ExampleSentences), whether
the given current value is worth suggesting a replacement for — conservative by default, a
non-empty field is only touched when there's a real gap. Takes the field values in the request
body rather than an entry id: this also has to work for a brand-new, not-yet-saved draft (the
main use case — enriching a word while it's still being typed in), which has no server-side
record to look up. A field listed in the request's `LockedFields` is excluded structurally: it
never appears in the LLM request (prompt or output schema), not just filtered out of the response.

The response always carries `word_recognized`. When the model can't confirm `Word` is a real,
existing French word/expression (random characters, an invented word, an unconfirmed typo), it's
`false` and every other field is absent — the model is explicitly told not to fall back to a
similar-looking real word to avoid leaving the request empty.

`POST /enrichment/rephrase-definition` takes `{Word, Definition}` and returns another phrasing of
the same definition, same meaning — no Wiktionary lookup, no `web_search`, no sufficiency judgment,
so it costs and latencies far less than `/enrichment/fields`. Stateless: the caller is responsible
for always sending the same anchor definition (never the result of a previous rephrase call), so
that repeated calls don't drift from the original meaning over successive reformulations.

## Deployment

`docker-compose.prod.yml` deploys the API only — MongoDB is a shared instance
owned and operated outside this repo. The `api` service reaches it over an external Docker network,
`lexicall-db`, which must already exist before `docker compose up` is run
against this file; nothing in this repo creates, starts, stops, or owns that
network, the Mongo container, or its data volume. `MONGO_URI` in `.env` points
at a scoped user (`readWrite` on this app's own database only, never a root
account — see `.env.example`). `deploy/backup.sh` mirrors that scoping: it
backs up only this app's own database, using the same app credentials, from a
disposable container on `lexicall-db` — not a full-instance backup.
