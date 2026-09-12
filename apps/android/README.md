# LexiCall — Android

React Native + Expo mobile client (Phase 3), built session by session — see
`docs/Plan-App-Android.md`. Currently: local JSON persistence, API configuration and
delta pulls. No browsing screens yet, and no push (the app never writes to the API).

Stack: JavaScript (no TypeScript), Expo Router (file-based routing, `app/`), Zustand
(`src/store/`) for state, `fetch`-based API client (`src/services/apiClient.js`), local
database and settings as JSON files under the app's document directory
(`src/services/storage.js`).

```bash
npm install
npm run android    # dev server + Expo Go / dev client on a connected device
npm run build:android  # local native build (expo run:android) -> installs on device
```

Or from the repo root: `just start-android` / `just build-android` (see root `justfile`).
