# POSEIDON web — APP-FOUNDATION-01

Technical connection screen only. This package does not implement login, tenant selection, business readiness, user-action auditing or safety authorization.

Requirement: Node >=26.13.0, npm. Dependencies are exact-pinned with package-lock.json. Registry versions/engine metadata checked 2026-10-30. Node 26.13.0 is the current LTS used by CI; latest jsdom requires a newer runtime than the originally installed Node 22.15.0.

Compatibility decision: TypeScript 7.0.2 is the registry's latest release, but current typescript-eslint 8.72.1 requires TypeScript `>=4.8.4 <6.1.0`. This package uses the latest compatible stable TypeScript 6.0.3 without disabling peer checks. Reevaluate the TypeScript 7 update once the lint toolchain supports it.

```powershell
cd frontend
npm ci
npm run dev
```

Run the API at `http://127.0.0.1:5080` and open `http://127.0.0.1:5173`. Dev and preview proxy `/api` to that API. The endpoint is `GET /api/v1/system/status`, expected `{ "service": "poseidon", "status": "available" }`. Fetch bypasses cache and times out after ten seconds. A malformed response is a recoverable connection failure. No endpoint result claims operational readiness.

```powershell
npm run lint
npm run typecheck
npm test
npm run build
npm run preview
```

Preview uses `http://127.0.0.1:4173`. Component tests mock fetch and do not prove a real API journey. Independent Playwright QA must cover the running backend and preview together.

Stable QA selectors: `[data-testid="connection-card"]` has `data-state="loading|available|error"`; `[data-testid="connection-retry"]` triggers a new request. Accessible Turkish headings: `API bağlantısı kuruldu`, `API bağlantısı kurulamadı`. Language buttons have accessible names `TR`, `EN`, and `aria-pressed`. `role=status` announces changes. Responsive shell, skip link, keyboard focus and reduced motion are included. No remote fonts/assets are requested.

Traceability: APP-FOUNDATION-01; 18.01 Backlog rows 2–3 (DEV-001 / DEV-002 preparation only); 13.04 paragraphs 10, 19–25 and layout standard. React/TypeScript contract is handwritten for this single heartbeat; API schema contract checks are owned by the backend package.
