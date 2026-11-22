# USER-GUIDE-01 UG-4 — Offline illustrated user manual (generator)

Status: **AUTHORED_UNVERIFIED** (22.11.2026, branch `feature/user-manual-20261122`). Local targeted checks ran (see below); CI and independent QA pending. Derived tracking ID, not a canonical work item or SP.

Source: `docs/tasks/USER-GUIDE-01.md` §3B, §7 UG-4, UG-DOC-01/02/03, D1/D4/D5/D7/D8; 13.03 Rol Bazlı Kullanıcı El Kitabı p18-p125 (common model §1, roles §2-9, help flow §10).

## Delivered
- `tests/Poseidon.Browser.Tests/manual/manual.ts` + `build-manual.ts`: builds `poseidon-user-manual-{tr,en}.html` (self-contained: inline CSS, screenshots as PNG data URIs, no network) and, with `--pdf`, A4 PDFs via the installed Playwright Chromium (page numbers, PDF outline). Output `.runtime/user-manual/` (git-ignored, not committed).
- Structure (UG-DOC-01 order): cover with draft banner → contents → Getting started (how to use the manual, sign-in, context, menu, help panel `?` / Shift+?, appearance/language/offline) → Common working model → 8 role chapters (13.03 §2-9; pages linked from each guide's `roles` front matter) → page guides grouped by sidebar section → Glossary of states (READY/CONDITIONAL/NOT_READY/REVIEW_REQUIRED/UNKNOWN, Completed ≠ Verified ≠ Effective) → If something goes wrong (13.03 §10).
- Single source (D1): the generator parses guides with the panel's own `help-model.ts` and the new shared `frontend/src/help/help-markdown-blocks.ts` (HelpMarkdown.tsx now uses it too). All authored text is HTML-escaped; links only resolve to anchors inside the manual; images come only from committed screenshots at the mapped step; steps marked `write_permission_required` say why they have no picture.
- Draft (D7, UG-REV-01): banner on every printed page plus a per-guide draft badge while any content is `reviewed:false`; `_manual` chapters reject any `reviewed` value other than `false`.
- `frontend/src/product/enterprise-sections.ts`: sidebar section keys extracted as pure data (used by the sidebar and the manual). `towage-execution` and `tug-daily-log` were navigation destinations missing from every sidebar section (so never listed in the menu); they are now in the Tug operations section.

## Not done
- `/app/help` in-app manual page (UG-4 plan item): needs a new server navigation destination; left for a follow-up.
- PDF is not byte-deterministic (Chromium writes a creation date); HTML is.

## Verification (local, Node 26.13.0)
- `node --test tests/Poseidon.Browser.Tests/manual/manual.test.ts`: 14/14 pass.
- `npm run manual:pdf --prefix tests/Poseidon.Browser.Tests`: TR 4.1 MB HTML / 5.5 MB PDF, EN 3.9 MB / 5.4 MB; 54 guides, 32 screenshots per locale.
- Frontend typecheck, lint and targeted vitest: see the delivery report of this branch.
