# IMPORT-INPUT-BOUNDARY-REGRESSION-01

AUTHORED / UNVERIFIED follow-up for DEV-069/T-S11-001 source18.01 Backlog70:
validation and no bypass of domain validation; certificate numeric type review
remains explicit. Input source contracts are14.02 rows1638/renewal lead numeric
and1639/1640 date fields. No source semantic type or legal range is invented.

Static reading of the still-active Import author snapshot identified two concrete
closure items, not executed failures. CertificateMetadataPolicy's current regex
uses `$`, permitting a match before final LF; exact numeric fields must reject
that whole input, not silently trim or accept a prefix. Use absolute boundaries
or equivalent full lexical consumption and preserve the raw declaration.

The current downloaded XLSX template emits headers without editable-column text
formatting. User-entered dates/long numbers/exact decimal declarations need text
formatting before entry to avoid spreadsheet General coercion. Use a valid linked
styles part/content type and default text format on all editable columns; retain
explicit ISO date/numeric validation. The regression checks the actual emitted
OOXML contract and template parse, not a live Excel interoperability run.
Technical reference: [Microsoft Open XML NumberingFormat](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.spreadsheet.numberingformat)
documents built-in49 as `@`, and General date/number rendering behavior. No SDK
dependency or extra parser service is needed to author the fix.

Six numeric boundary cases and one template contract case are authored. They
depend on the real Import checkpoint; those classes are absent in the older
support worktree base. They are **NOT RUN**; no observed failing/passing suite,
build, provider/browser, QA or CI result is claimed. Two previously authored
support test files also gain explicit `using Xunit` consistent with the project.

DOMAIN-COMPLETION-01 must first inspect the final Import checkpoint; if the author
already repaired these exact conditions preserve its correction, otherwise fix
the actual implementation before the combined gate. Integrate these regressions
after Import's classes exist, do not delete/skip them or replace behavior with a
banner. No operational entity/seed or canonical SP is introduced.
