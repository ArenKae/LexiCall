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

install-api:
    just --justfile api/justfile --working-directory api install

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

mongo-up-api:
    just --justfile api/justfile --working-directory api mongo-up

mongo-down-api:
    just --justfile api/justfile --working-directory api mongo-down
