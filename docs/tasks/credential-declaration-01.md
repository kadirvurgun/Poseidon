# CREDENTIAL-DECLARATION-01

AUTHORED / UNVERIFIED support for the certificate category of DEV-069/T-S11-001,
18.01 Development_Backlog_TR.xlsx sheet:Backlog/row70,
SHA25609890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674.
Canonical metadata locator14.01 Product_Wide_ERD_Data_Architecture_TR.docx
paragraphs101–102: person, type, number/token, issuer, issue/expiry, limitations,
verification state (SHA256c3f5e612d69e33d2e5e3a1227644cb9a0ec8937bdcf74b20d2b5a60b968344ab).
14.02 Data Dictionary2316 rows1639–1640 are explicit date fields; type,
verification status and references remain controlled/review-required. Current
field-contract SHA2563b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018.

The normalized domain value object captures partial declared certificate metadata,
preserves exact reference/number/issuer/limitation strings and accepts no owner,
tenant, verification/signatory or approval input. System.Text.Json unmapped-member
denial prevents those extra fields from silently entering this input contract.
Actual API/import ownership comes from fresh trusted authorized context; no JSON
attribute or domain object substitutes for role/tenant/person scope enforcement.
The ordinary diagnostic formatter omits private contents; authorized intentional
serialization still contains declared data and must never be treated as safe logs.

The calendar evaluator reports independent issue/expiry comparisons to an explicit
server-selected reference date and their declared order. Missing expiry is unknown,
not unlimited validity; exact expiry day is a distinct boundary, not an invented
legal cutover. Future, reversed and same-day declarations remain visible facts;
there is no inferred issuer authenticity, verified state, qualification, fitness,
operational approval, minimum duration or date correction. Real submission/domain
rules decide how to handle these observations; POL-DEFAULT-01 still allows drafts.

The actual credential entity, EF migration, private persistence/reference lookup,
audit, authorized ordinary edit/import mutation service and synthetic fixtures
remain required in IMPORT-FOUNDATION-01. Reuse this declaration contract where it
matches the resolved source fields; do not omit the remaining controlled source
fields or fake their approved catalogs. In particular14.02 row1638 issuing_country
is a numeric/precision-TBD candidate despite its label: record an explicit source
decision, never silently treat the source as an approved ISO string/number codebook.
This value object is not an operational entity and needs no seed itself.

Meaningful domain cases are authored for partial drafts, forged authority/ownership
properties, exact text preservation, missing dates, boundary/future/reversed/same-day
facts, explicit reference dates, calendar extremes, privacy and immutable draft copies.
Tests/build/integration/browser/QA/CI are **DEFERRED / NOT RUN**. This support does not
complete the full certificate/credential story, accepted import, or canonical SP.
