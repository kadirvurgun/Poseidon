# USER-GUIDE-01 UG-5 — translator brief (machine-drafted page guides)

Source: `docs/tasks/USER-GUIDE-01.md` D6 (15 locales are machine drafts with a "translation not reviewed" label; a missing page falls back to EN and says so), D7 (`reviewed:false`), D8 (the guide never decides). Traceability: UG-5, UG-HELP-03, UG-HELP-05.

## 1. What you produce

One file per page and locale, translated from the **EN** guide:

```
frontend/src/help/content/<locale>/<key>.md        # translate from content/en/<key>.md
frontend/src/help/content/<locale>/help-<locale>.ts # bundle file, once per locale folder
```

Locales: `ar, cs, de, es, fi, fr, hi, hu, it, pt-BR, ru, sk, zh-Hans, zh-Hant` (`en-GB` reads the EN guides; TR is authored, not translated). The bundle file is a verbatim copy of `content/de/help-de.ts` (it has no locale-specific content). A folder with `.md` files and no bundle, or a bundle without `.md` files, fails the coverage guard.

A missing file is allowed: the page shows the EN guide with the existing "no guide in this language" notice. **Do not** add anything to `help-pending.ts`; it only governs TR/EN.

Every translated page shows "Machine translation — not reviewed" (`HelpPanel_tsx_copy.translationUnreviewed`) automatically. You do not write that notice into the guide.

Reference example: `frontend/src/help/content/de/today.md` (from `content/en/today.md`).

## 2. Headings

Use the 8 headings of your locale **exactly** as defined in `helpSectionHeadings` in `frontend/src/help/help-model.ts` (single table for all 16 guide locales), in the same order, each as `## <heading>`. Do not translate them yourself and do not add or remove `##` sections. `###` sub-headings inside a section may be translated freely.

Panel-internal links (`[text](#what-the-states-mean)`) target the stable **EN** heading slug in every locale; keep the `#...` target unchanged.

## 3. Front matter

Copy the EN front matter and change **only** `title` (use the page's name from `frontend/src/i18n/messages/<locale>.json` when it exists there). `key`, `roles`, `permissions`, `related`, `screenshots` stay byte-for-byte identical (roles are canonical codes, not display text). `reviewed: false` always — an AI or translator never records a review; only the human UG-6 / UG-REV-01 flow does.

## 4. Rules for the body

1. Translate prose naturally; keep the meaning, not the word order.
2. **Bold UI labels** (`**Refresh**`, `**Why?**`, state names): use the exact string the app shows in that locale, i.e. the value in `frontend/src/i18n/messages/<locale>.json` whose EN counterpart in `en.json` is the same label (prefer the key of the page's own screen copy, e.g. `TodayWorkspace_tsx_copy` for `today`). If the label has no entry there, keep the EN label unchanged.
3. Keep unchanged: codes and enum values (`REVIEW_REQUIRED`, `UNKNOWN`, `canRead`, permission strings), backticked text, route links `(/app/...)`, citations such as `13.03 §3`, product name POSEIDON, units and numbers.
4. "Step by step" keeps **the same number of numbered steps** as EN, in the same order (screenshots are mapped by step number; drafts reuse the EN screenshots).
5. Never soften safety meaning:
   - UNKNOWN / missing never becomes "No", "Ready", "Not applicable", "OK" or "all clear".
   - "Completed" is not "Verified"; "read" is not "verified"; zero/empty is not a safety clearance.
   - The decision authority stays with the Master / Tug Master (13.03 §3). Never write that the vessel/tug may depart, is safe, or that the guide/system decides.
   - Keep every "does not", "cannot", "is not" sentence as a negation.
6. Safe Markdown only: paragraphs, `-` lists, `1.` lists, `###`, `**bold**`, `` `code` ``, links to `/app/...` or `#anchor`. No HTML, no external URLs, no images.
7. Section bodies (except "Related pages", which stays empty) must not be empty.

## 5. Verify before pushing

```
cd frontend
npm ci                       # once
npx vitest run src/help       # coverage guard + panel tests
npm run typecheck
npm run lint
```

The guard (`src/help/help-coverage.test.ts`, "help translation drafts (UG-5)") checks, for every file present: front matter equals EN except `title`, `reviewed: false`, the locale's 8 headings in order, the same step count as EN, non-empty sections, safe Markdown and the UNKNOWN/decision wording patterns.

Note for the integrator: each new locale folder adds one lazily loaded help chunk to the offline shell inventory (limit 128 entries, `frontend/scripts/offline-shell-build.ts`). After the 14 folders exist, confirm the production build still passes that limit.
