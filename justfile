set shell := ["powershell.exe", "-NoLogo", "-NoProfile", "-Command"]

default:
    just --list

run:
    dotnet run --project src/LexiCall.Desktop

build:
    dotnet build

test:
    dotnet test

clean:
    dotnet clean
