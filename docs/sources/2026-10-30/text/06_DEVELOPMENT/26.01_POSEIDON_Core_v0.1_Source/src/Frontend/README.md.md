# README.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/src/Frontend/README.md
SHA256: 3080be0b6cb07a759e5b3259e0f6d60289b39582e078e04887d5f64613604b12

## lines:1-40

# POSEIDON frontend · first product slice

React 19.3 + TypeScript 7 + Tailwind 4, with a locally owned shadcn/ui-style Button built on Radix Slot and CVA. Dark maritime styling derives from controlled v46.3 colors; no prototype DOM or simulated business states are copied.

## Run locally

Requires Node 22.12+ or Node 24. Backend and PostgreSQL setup are separate and require the approved backend environment; this project never starts a fake API.

```sh
npm ci
npm test
npm run build
# Point the server-side Vite proxy at the real backend.
POSEIDON_API_URL=http://localhost:5080 npm run dev
```

Vite listens only on loopback. Browser requests are same-origin `/api/v1/*`. For a future production deployment, configure a same-origin reverse proxy for `/api`; do not publish the development server.

The default authentication screen validates an existing bearer token with `GET /api/v1/session`; it is an integration aid, **not a finished production OIDC sign-in/refresh flow**. Tokens and form state are kept in memory only. Refreshing clears them; there is no localStorage, IndexedDB or browser-storage fallback.

For an explicitly opted-in **local synthetic development** setup only, run `VITE_ENABLE_DEV_SESSION=true npm run dev` and independently enable the backend's Development-only synthetic mode. The UI clearly marks synthetic sessions and offers fixed tenant A / B fixtures, not a user-controlled authorization scope.

## Implemented

- Turkish/English responsive shell, session and company scope display.
- Server-backed vessel and certificate metadata draft list, create, reopen and edit.
- Optional missing vessel/metadata fields; explicit certificate vessel scope; format, date range and length validation.
- POST `{saveMode:"DRAFT",data:{...}}`; idempotency UUID on each operation; quoted `If-Match` on edits.
- Network/5xx uncertainty keeps exact request payload/key and locks fields until retry. No automatic mutation retry.
- Stale version blocks save until explicit server reload, with unsaved-change confirmation. No automatic overwrite.
- No submit/approve/issue, validity or operational-readiness claims. No Yes/No business defaults, attachments, deletion or fake data.
- Labeled controls, keyboard focus, error association, live status, skip link, responsive layouts and reduced-motion support.

## Evidence boundary

Vitest uses jsdom and mocked HTTP **in tests only**. Tests cover transport, validation, authentication errors, save/reopen, language, dirty navigation, uncertain retries, stale edits, scope selection and failed-list clearing. Production build and type checks do not certify real-browser rendering, screen-reader behavior, actual OIDC, .NET/PostgreSQL integration or legal/business acceptance. Those remain explicit follow-on gates.

## Stable-stack refresh — 2026-10-27

Latest user instruction superseded the original React 18 baseline. Stable npm `latest` metadata was checked before pinning: React/React DOM **19.3.1**, TypeScript **7.0.2**, Vite **8.3.5**, React Vite plugin **6.1.2**, Tailwind CSS/Vite integration **4.3.3**, Vitest **5.0.5**. Exact versions and transitive integrity hashes are in `package.json` / `package-lock.json`; no beta, canary or RC selected. Node **24.22.1** is the tested runtime. UI support packages also use current stable releases (Radix Slot 1.5.0, tailwind-merge 3.7.0, Lucide 1.62.0).

## lines:41-62

Sources checked:

- [Official React 19.3 announcement](https://react.dev/blog/2026/09/09/react-19-3), plus `npm view react version` / `npm view react-dom version` for the patched 19.3.1 release.
- [Official TypeScript installation guidance](https://www.typescriptlang.org/download/), plus `npm view typescript version` for 7.0.2.
- [Vite supported releases](https://vite.dev/releases), plus `npm view vite version` for 8.3.5 and the plugin's peer-dependency metadata.
- [Tailwind official Vite integration](https://tailwindcss.com/docs/installation/using-vite), plus `npm view tailwindcss version` / `npm view @tailwindcss/vite version` for 4.3.3.

Migration is deliberately narrow: official Tailwind Vite plugin and CSS import replace legacy PostCSS configuration; application workflow and backend contract remain unchanged. New framework capabilities are not added merely because they exist. This is a stable, supported baseline, not a claim that every new API has been adopted or browser-certified.

## Certificate wire-name alignment

Certificate common metadata uses controlled source 16.01 `CertificateDraftFields` names: `type`, `issuer`, `issue`, `expiry`, plus `vessel_id` for authorization scope. Turkish/English labels remain user-oriented. `certificate_no` and `notes` are explicitly development extensions, not original controlled-source fields. No legacy aliases are accepted: this slice has not been published. Vessel field names are unchanged.

## Optional BFF browser session

`VITE_ENABLE_BFF=true` selects same-origin cookie session UI; default remains disabled.
It hides manual bearer and synthetic controls and requires independently configured
server BFF infrastructure. No access/ID/refresh tokens are exposed to this UI.
Session expiry freezes unsaved edits; confirmed sign-in/logout discards them without
automatic write retries. See [BFF_UI_BOUNDARY.md](../../docs/BFF_UI_BOUNDARY.md).
No HTTP BFF development proxy or real identity provider is configured by this source.
