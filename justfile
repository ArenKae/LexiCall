set windows-shell := ["powershell.exe", "-NoLogo", "-NoProfile", "-Command"]

default:
    just --list

# --- apps/windows (.NET / WPF) ---

run-app-windows:
    just --justfile apps/windows/justfile --working-directory apps/windows run

build-app-windows:
    just --justfile apps/windows/justfile --working-directory apps/windows build

clean-app-windows:
    just --justfile apps/windows/justfile --working-directory apps/windows clean

test-app-windows:
    just --justfile apps/windows/justfile --working-directory apps/windows test

# --- api (FastAPI / MongoDB) ---

# Local dev tooling: venv, tests, lint, one-off migration script.

install-api:
    just --justfile api/justfile --working-directory api install

# Positional arg, not NAME=value: `just run-api 0.0.0.0` to bind to the LAN.
run-api HOST="127.0.0.1":
    just --justfile api/justfile --working-directory api run {{HOST}}

lint-api:
    just --justfile api/justfile --working-directory api lint

test-api:
    just --justfile api/justfile --working-directory api test

clean-api:
    just --justfile api/justfile --working-directory api clean

migrate-api *ARGS:
    just --justfile api/justfile --working-directory api migrate {{ARGS}}

# Local dev stack (docker-compose.yml: Mongo only — the app itself runs via run-api).

dev-mongo-up-api:
    just --justfile api/justfile --working-directory api dev-mongo-up

dev-mongo-down-api:
    just --justfile api/justfile --working-directory api dev-mongo-down

# Production stack (docker-compose.prod.yml).

deploy-api:
    just --justfile api/justfile --working-directory api deploy

down-api:
    just --justfile api/justfile --working-directory api down

logs-api SERVICE="api":
    just --justfile api/justfile --working-directory api logs {{SERVICE}}

ps-api:
    just --justfile api/justfile --working-directory api ps

health-api:
    just --justfile api/justfile --working-directory api health

backup-api:
    just --justfile api/justfile --working-directory api backup

mongo-shell-api:
    just --justfile api/justfile --working-directory api mongo-shell
