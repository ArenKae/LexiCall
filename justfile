set windows-shell := ["powershell.exe", "-NoLogo", "-NoProfile", "-Command"]

# Root justfile for the monorepo: thin dispatcher to each sub-project's
# justfile (apps/windows, api...). Recipes are suffixed per sub-project
# (-app-windows, -api) to avoid name collisions once the other sub-projects
# are in place.

default:
    just --list

# ---------------------------------
# --- apps/windows (.NET / WPF) ---
# ---------------------------------

# Run the desktop app in debug mode.
[group('app : windows')]
run-win:
    just --justfile apps/windows/justfile --working-directory apps/windows run

# Build in debug mode.
[group('app : windows')]
build-win:
    just --justfile apps/windows/justfile --working-directory apps/windows build

# Build in release mode.
[group('app : windows')]
release-win:
	just --justfile apps/windows/justfile --working-directory apps/windows release

# Remove build artifacts (bin/obj).
[group('app : windows')]
clean-win:
    just --justfile apps/windows/justfile --working-directory apps/windows clean

# -------------------------------------------------------------------------
# --- api : dev (docker-compose.yml, local Mongo + uvicorn with reload) ---
# -------------------------------------------------------------------------

# Create the venv and install Python dependencies.
[group('api : dev')]
install:
    just --justfile api/justfile --working-directory api dev-install

# Wrapper: start Mongo, then the API, both in dev mode.
[group('api : dev')]
start:
    just --justfile api/justfile --working-directory api dev-up

# Start Mongo alone (dev container).
[group('api : dev')]
mongo-up:
    just --justfile api/justfile --working-directory api dev-mongo-up

# Run uvicorn with hot reload. HOST defaults to 0.0.0.0 (reachable from
# the Windows host while the API runs inside the Linux VM); positional,
# not NAME=value (`just run 127.0.0.1`).
[group('api : dev')]
run HOST="0.0.0.0":
    just --justfile api/justfile --working-directory api dev-run {{HOST}}

# Stop the dev stack.
[group('api : dev')]
stop:
    just --justfile api/justfile --working-directory api dev-stop

# --------------------------------------------
# --- api : prod (docker-compose.prod.yml) ---
# --------------------------------------------

# Build (if needed) and bring the full stack up.
[group('api : prod')]
deploy:
    just --justfile api/justfile --working-directory api prod-deploy

# Stop the prod stack.
[group('api : prod')]
down:
    just --justfile api/justfile --working-directory api prod-down

# Back up Mongo (deploy/backup.sh script).
[group('api : prod')]
backup:
    just --justfile api/justfile --working-directory api prod-backup

# One-off, idempotent vocabulary.json -> Mongo migration.
[group('api : prod')]
migrate *ARGS:
    just --justfile api/justfile --working-directory api migrate {{ARGS}}

# Interactive mongosh shell on the prod stack.
[group('api : prod')]
mongosh:
    just --justfile api/justfile --working-directory api mongosh
