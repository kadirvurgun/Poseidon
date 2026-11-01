# IMPORT-COLUMN-MAPPING-01

AUTHORED / UNVERIFIED support for DEV-069 / T-S11-001, active18.01 Backlog
sheet:Backlog/row70, SHA25609890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674.
Source requires mapping, validation, reject reporting/provenance, duplicate/key
rules and no bypass of domain validation. It does not select a file format here.

The actual format-independent domain validator consumes a server-loaded immutable
template descriptor and user column bindings. It permits only exact whitelisted
writable target keys, checks real provider column bounds, rejects ambiguous duplicate
targets and missing required input mappings. Source-column reuse is governed by an
explicit trusted template flag. System/readonly fields cannot be made client input.
Unknown target strings and cell/header values never appear in returned diagnostics;
only fixed issue codes and authorized request/template ordinals are returned.

Valid output is an immutable canonical target-order mapping with copied target keys,
template version and SHA256 fingerprint of the exact field/write/required/reuse schema.
Later mutation of the input descriptor/bindings cannot retarget the saved plan.
The actual import service must compare current template fingerprint/version at commit,
reauthorize and revalidate all values against the real domain. A client-supplied plan,
template or actor is never authoritative. Incomplete mapping drafts remain savable
under POL-DEFAULT-01; this validated plan is a processing/commit prerequisite, not a
reason to block initial draft capture.

Nineteen meaningful cases authored (16 facts and3 theory rows): target/source bounds,
unknown/case-alias/readonly fields, duplicates, explicit reuse, required mappings,
template/schema failure, non-disclosing diagnostics, immutable plan and descriptor,
schema fingerprints and bounded allocation independent of provider column count.
Execution, build and typecheck are **DEFERRED**, with no PASS/acceptance claim.

This policy writes no domain entities and therefore introduces no operational seed.
The following IMPORT-FOUNDATION-01 must integrate it into the real authorized template,
upload/provider/parser/map/preview/commit/reject UI/API flow for vessel/crew/certificate/
equipment. Actual row-level key/duplicate/validation/provenance, transactions, audit,
idempotency, migrations/seeds and real PostgreSQL/browser cases remain in that package.
This is not a complete importer, spreadsheet/CSV format acceptance or pilot/owner sign-off.
