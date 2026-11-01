# IMPORT-MAPPING-UI-01

AUTHORED / UNVERIFIED support for DEV-069 / T-S11-001; active18.01
Development_Backlog_TR.xlsx, sheet:Backlog/row70,
SHA25609890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674.
Acceptance requires template mapping, validation, reject reporting/provenance,
duplicate/key rules and no bypass of domain validation. This editor implements
only explicit column mapping presentation, not the full import story or a new
file-format requirement. Shared design requirements are TR/EN, keyboard labels,
44px targets, responsive layout and distinct unavailable/denied/loading states.

The owning workspace adapts generated authorized API DTOs to the typed presentation
projection. No raw upload defines trusted template fields, requiredness, writable
system fields or column-reuse policy. Physical zero-based column ordinals distinguish
repeated/blank headers; the interface shows one-based numbers, never guesses a
mapping from a similar header. Readonly fields have no editable control. An invalid
or missing required mapping blocks preview while incomplete drafts remain savable
under POL-DEFAULT-01. Preview has no domain-import side effect. All actual preview,
commit, authorization and template fingerprint checks remain server authoritative.

Bindings remain controlled in the workspace; errors and in-flight operations retain
the draft. The editor stores no mappings, cell values or private headers in browser
storage and sends no telemetry. A context mismatch hides old data/actions before
rendering. The adapter must construct context keys from actor/session, tenant,
vessel, upload and schema version/fingerprint, cancel obsolete requests and clear
its controlled state on changes. A React key alone cannot revoke a parent cache.
Unknown mapping keys produce a fixed error without echoing those keys; uploaded
headers render as text. Draft/preview failures use fixed UI messages, not server
error bodies. Actual rejected-row downloads and provenance belong to the importer.

Twenty-two component cases authored: incomplete drafts, explicit mapping/preview,
duplicate and blank headers, trusted source-reuse option, duplicate-target repair,
four invalid column ordinals, unknown/system target denial, clearing, context change,
three load/denial states, retry, in-flight controls, English/error retention, inert
header text, malformed schema and empty source. No tests, typecheck, build, API
generation or browser journeys were executed. These remain **DEFERRED**; no PASS,
accessibility conformance, real API integration or canonical SP acceptance is claimed.

IMPORT-FOUNDATION-01 must connect this component to real authorized templates,
upload/parser, schema-bound drafts, preview/commit/history/report endpoints for
all four source categories and add actual-host journey coverage. Reuse the domain
column validator, current role/scope checks and actual audited mutations. No seeds
are needed for this presentation-only support; operational fixtures stay with
the real importer/entity implementation.
