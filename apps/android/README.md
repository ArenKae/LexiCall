# LexiCall — Android

React Native + Expo mobile client (Phase 3). Scaffold only for now — no vocabulary
screens yet, see `../../todo.md`.

Stack: JavaScript (no TypeScript), Expo Router (file-based routing, `app/`), Zustand
(`src/store/`) for state, `fetch`-based API client (`src/services/apiClient.js`)
mirroring `apps/windows`' `VocabularyApiClient.cs`.

```bash
npm install
npm run android    # dev server + Expo Go / dev client on a connected device
npm run build:android  # local native build (expo run:android) -> installs on device
```

Or from the repo root: `just start-android` / `just build-android` (see root `justfile`).
