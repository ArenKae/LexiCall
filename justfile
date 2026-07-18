set shell := ["powershell.exe", "-NoLogo", "-NoProfile", "-Command"]

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
