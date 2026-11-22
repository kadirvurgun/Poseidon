# Locales + manual bundle 22.11.2026 — UG-4 user manual, UG-5 guide drafts, user-admin i18n

Branch `integration/help-locales-20261122` (base `integration/guide-bundle-20261122`, PR #55). Canonical count unchanged: 114/121 tasks, 932/970 SP. Documentation/i18n delivery; content stays `reviewed: false` until human SME review (UG-6, UG-REV-01).

## Delivered
- **UG-5 infrastructure** (4b3a15e7): per-locale guide bundles, per-page EN fallback with notice (D6), "machine translation — not reviewed" notice on draft locales, heading table `helpSectionHeadings` for 16 locales, coverage guard for draft locales (front matter = EN except title, headings, step count, safe Markdown), EN-slug anchors in every locale.
- **UG-5 drafts**: 14 locales × 54 guides (ar cs de es fi fr hi hu it pt-BR ru sk zh-Hans zh-Hant). Labels without a locale string stay in EN (brief rule 4.2).
- **User administration and invitation UI strings** translated into the 14 locales (d29ba730).
- **UG-4 manual generator** (1718a0e6): TR/EN self-contained HTML (+ PDF with `--pdf`) from the page guides and committed screenshots: cover + DRAFT banner, getting started, 13.03 role chapters, page guides in sidebar order with screenshots under their steps, state glossary, troubleshooting. `npm run manual[:pdf] --prefix tests/Poseidon.Browser.Tests` → `.runtime/user-manual/`. Shared parser `help-markdown-blocks.ts`; sidebar sections in `enterprise-sections.ts` (towage-execution and tug-daily-log now listed under Tug operations; server navigation still gates them by `CanReadTug`).
- Product-fix QA notes N3 (renewal refusal copy covers both separation-of-duties cases, 17 locales + guides) and N4 (unknown SOF test).

## QA
Independent QA ACCEPT-WITH-NOTES (sampled safety-critical guides in de/ru/ar/hi/zh-Hans/es faithful; escaping, links, images and placeholders checked; no menu leak). Notes fixed: N1 brief wording, N2 manual section ids, N4 `dir="ltr"` on EN fallback. Open: N3 mixed EN labels where locales lack strings, N5 no test for the Tug section grouping and `/app/help` page not delivered, N6 one awkward Arabic sentence — for UG-6 review.

## Verification (local)
Full frontend vitest 232 files / 3075 tests passed; typecheck and lint clean; manual + screenshot node tests 25/25; production build offline inventory 36 entries (limit 128). Gate CI pending.

## Open
UG-6 human SME/UX review of all guides and translations; mobile screenshots (separate branch in progress); `/app/help` page.
