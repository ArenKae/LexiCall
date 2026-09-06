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

`Definition` is a list of senses, one element per distinct meaning — a single string couldn't
represent a word like "ladre" (leper / miser), and every consumer downstream needs the senses
apart rather than fused. `migration/split_definitions.py` converts an existing corpus, splitting
only on newlines the user wrote themselves (never on punctuation, which on real data separates a
rephrasing far more often than a sense) and leaving `UpdatedAt` untouched, since a schema change
is not a user edit.

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

Auto-categorization retrieval runs on embeddings kept in their own `category_embeddings`
collection, one vector per category, keyed by the category Id (same reasoning as entry images:
`categories` is pulled on every client sync, so it stays free of bulky fields).

What gets vectorized for a category is its hierarchical path, the nearest description available
(its own, else the closest ancestor's — child categories rarely carry one), and **the words
actually filed under it**, alphabetically, capped at 100. That last part matters: a category's
label says what it is called, not what it ended up holding, and a category reachable only through
its name misses the words its content would have attracted. Measured on a real 42-category corpus,
adding member words raised similarity scores ~20-25% and fixed the ambiguous cases — a query that
previously ranked an unrelated family first now puts the correct family in the whole top-3. The
word list is only added when the category genuinely has entries attached: categories that exist to
hold sub-categories have none and keep the path-and-description text.

Consequence worth knowing: entry writes are deliberately **not** hooked into re-embedding. Editing
entries or moving them between categories therefore leaves category vectors describing a slightly
older membership, and that drift is absorbed by the reindex pass below rather than by an embeddings
call on the sync-write path (which would fire hundreds of times during a full sync).

A category write does re-embed that category and its descendants — renaming a parent rewrites the
hierarchical path below it — but only when the text actually changed, so an ordinary sync push
costs no embeddings call. That refresh is deliberately best-effort: it never fails the category
write it follows, which means a vector can also end up missing or stale that way.

`POST /categories/reindex-embeddings` reconciles all of it — a full, idempotent pass that re-embeds
whatever drifted (failed refreshes and membership changes alike) and drops embeddings whose
category is gone, returning `{embedded, unchanged, orphans_removed}`. Costs nothing when the corpus
is already current, so it doubles as routine hygiene rather than being purely an error-repair path.
The same pass is available on the server as
`PYTHONPATH=src .venv/bin/python -m lexicall_api.migration.index_category_embeddings [--dry-run]`.

Two manual debug tools sit in `tests/` (not pytest tests — they drive the API's own modules
directly and print every request, intermediate result and cost estimate). No `just` recipe wraps
them; run them from `api/`:

```bash
PYTHONPATH=src .venv/bin/python tests/debug_enrichment.py <mot> [--locked Champ1,Champ2]
PYTHONPATH=src .venv/bin/python tests/debug_categorization.py <mot> [--definition "..."] [-k N] [--retrieval-only]
```

The categorization one shows the query text, the ranked candidates with their scores, the roots
joined to the prompt, the closed id enums, and the raw model output before resolution;
`--retrieval-only` stops before the paid LLM decision.

`POST /enrichment/category-candidates` is the retrieval half on its own: `{Word, Definition?}` plus
an optional `k` (default 6) returns the closest categories as `{candidates: [{id, name, path,
score}]}`. Sending the definition alongside the word is strongly worth it — a bare rare word is
close to noise to the embedding model, and on a real test ("hune", a nautical term) adding the
definition moved the correct category from absent to rank 1 while more than doubling its score.
**Each sense is vectorized and ranked separately**, then the per-sense top-k are merged: one vector
for a word meaning two different things lands between the two and surfaces neither. Measured on
"ladre", the second sense's category sat at rank 9 of 42 with the senses fused, and at rank 5 once
split. `k` is therefore per sense, and the returned list can hold up to `k` per sense — trimming
the excess by global score would push the low-scoring sense straight back out of reach.
Scores are only meaningful relative to each other, never as an absolute threshold: word-to-category
scores sit far below category-to-category ones because the two texts are shaped differently. An
empty `candidates` list means nothing has been indexed yet, not that nothing matched.

`POST /enrichment/categorize` is the decision on top of that retrieval: same `{Word, Definition?}`
body, and it answers either `{"decision": "existing", "category": {...}}` or `{"decision": "new",
"new_category_name": ..., "new_category_parent": {...}}`, always with a `justification`. The LLM
only ever sees the top-K candidates **plus every root category** — the top-K is what keeps the
token cost flat as the corpus grows, and the roots are what let it propose a new category under a
sensible parent even when similarity never surfaced that branch (a word whose whole lexical field
is missing from the corpus ranks nothing useful, so without the roots it could only pick among
wrong answers). Both id fields are constrained to an `enum` of the ids actually shown in the
prompt, so a hallucinated category id is structurally impossible; a self-contradicting answer
("existing" without naming one) is rejected as a 502 rather than returned half-built. About
$0.0004 and ~3.7s per call, dominated by the decision itself — the retrieval half is one embeddings
call plus local math.

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
