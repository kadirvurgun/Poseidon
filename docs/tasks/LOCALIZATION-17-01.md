# LOCALIZATION-17-01 — JSON interface catalogs and persistent locale

State: AUTHORED / IN PROGRESS / NOT ACCEPTED. No canonical story points added.

Source: user's explicit 17-language menu image and AI-authored JSON/i18n request,
09.11.2026. This extends the earlier TR/EN presentation scope. The active source
baseline remains `poseidon-v46.3-20261030`; 15.01 paragraph1626 requires that
localization never changes canonical enum/code values.

## Scope and acceptance

- One locale owner across standalone routes and the enterprise shell. Persist
  only a safelisted locale preference; malformed/unavailable storage is harmless.
- Catalogs for ar,cs,hu,fr,en,fi,en-GB,zh-Hans,hi,sk,tr,ru,pt-BR,it,es,de,zh-Hant.
  English (UK) and both Chinese scripts have separate resources.
- Typed i18next/React integration; bundled JSON resources and lazy locale chunks.
  Load and validate the complete product catalog before committing a selection.
  Keep the previous language on load failure; the latest requested selection wins.
- Arabic sets `html[dir=rtl]`. Use logical layout properties, direction-aware
  hierarchy keyboard controls, and isolated canonical codes/numeric inputs.
- Changing language preserves the same form nodes, unsaved text, command UUID,
  actor/tenant/vessel, request body, receipt, conflict and retry state.
- Preserve Unknown, Missing, Draft, Unverified, ReviewRequired and all negative
  authority statements. Translation cannot manufacture verification or approval.
- All JSON keys, structure, placeholders and nonstring contract values must match
  the frozen source catalog. Never fill an unfinished locale with English copies
  and describe it as translated. Server/user-entered names and source data remain
  source data rather than translated mutations.
- Shared native-name language selector supports keyboard and accessible names.
  Existing genuine browser journeys select the actual shared control.

## Authoring checkpoint

Core: `5a87136`, pinned i18next27.0.1/react-i18next18.0.1,17common catalogs.
Product source SHA256:
`a555c082fcdbecb4836be6d3734eaf7587230ce0a463dd8274bffd59e6123203`.
There are3169string leaves/2991distinct values, including2794paired message keys
and11nested dictionaries. These counts are catalog size, not acceptance coverage.

Complete authored product JSONs at this checkpoint: TR,EN,EN-GB,AR,DE,FR,HI,CS,RU,HU.
IT has preserved partial authoring; SK/FI/ES/PT-BR/ZH-HANS/ZH-HANT remain.
The loader refuses unavailable catalogs; complete17-language product acceptance
has not been claimed. AI translation workers reported a usage limit; partial
authoring is preserved and no account/model rotation was attempted.

## Tests and gate

Meaningful tests are authored for resource completeness/placeholder rejection,
storage, concurrent loading, failure retention, RTL, selected-locale label lookup,
unchanged form/command state and accessible selection. Existing component and
genuine browser selectors have been migrated. Source AST parsing was used only
to support authoring; it is not a build/typecheck/test PASS.

New package execution: DEFERRED until the bundle gate, per user cadence. Run
frontend lint/typecheck/build/generated-contract parity, all component tests,
real desktop/narrow journeys, explicit locale-switch/RTL/dirty-draft scenarios,
repository and built-client scans, then independent exact-target QA and CI.
PR16 verification remains a separate earlier bundle.


11.11 presentation namespace:17×31fully-authored labels for duties, calendar
ordering, daily statuses and certificate declarations. Original option values
remain canonical. Seven main catalogs and remaining nonliteral label branches
are still open; new-bundle execution remains deferred.


## 11.11 continuation — source16fdc90

Complete main catalogs:11/17, now including Italian. The remaining six are
fi,sk,es,pt-BR,zh-Hans,zh-Hant. All seventeen presentation resources contain81keys;
this is distinct from the3169-leaf frozen main catalog. French Unicode corruption
was repaired by a strict reversible conversion; valid `Âge` remains valid text.
Earlier10-catalog/31-label checkpoints above are retained as history.

Additional traceable acceptance cases (authored, execution DEFERRED):

- L17-AC-UNICODE: every authored main catalog retains source structure,
  placeholders and valid Unicode; Italian accents and French `Âge` render intact.
  [Catalog tests](../../frontend/src/i18n/messages.test.ts).
- L17-AC-CREW: changing locale retains the same state filter and visible page,
  does not fetch owning input again and preserves qualified-ineligible, withheld,
  unverified and unknown distinctions.
  [Owning input tests](../../frontend/src/product/CrewOwningInputs.test.tsx),
  [roster tests](../../frontend/src/product/CrewEnterpriseRoster.test.tsx).
- L17-AC-REGULATORY: an uncertain command preserves identical body/headers and
  the existing editor nodes through Italian; the saved notice follows the locale.
  [Register tests](../../frontend/src/product/RegulatoryRegister.test.tsx).
- L17-AC-ATTACHMENT: standard mode controls retain the owning draft node and
  remain disabled while dirty/busy; no automatic mode switch follows translation.
  [Attachment tests](../../frontend/src/product/VesselAttachments.test.tsx).
- L17-AC-OBSERVATION: localized freshness categories preserve absent telemetry;
  source-name filtering follows the selected locale without mutating identifiers.
  [Health tests](../../frontend/src/product/SystemHealthWorkspace.test.tsx),
  [technical tests](../../frontend/src/product/TechnicalReadViews.test.tsx).
- L17-AC-OFFLINE: translated conflict columns retain the same frozen attempt;
  no replay or new capture occurs before an explicit operator choice.
  [Offline tests](../../frontend/src/product/TugOfflinePanel.test.tsx).

These tests have not run. Required new-bundle checks and independent exact-target
QA remain open; canonical accepted story points remain unchanged.
