# POL-DEFAULT-01-DOMAIN

Scope: pure .NET 10 required-data draft policy. Local implementation package awaiting independent QA and parent CI; no phase acceptance.

Sources: active baseline `poseidon-v46.3-20261030`, root `analiz/POSEIDON GUNCEL 30.10.2026/`:
- `04_UX_PRODUCT_DESIGN/13.06_POSEIDON_v46.3_Modul_Isleyis_Surec_ve_Veri_Master_Katalogu_TR.xlsx`, sheet `Yönetici Özeti`, row 18; sheet `Karar Onayları`, row 10. SHA256 `3c7eef571e8836fb311e972335997a2ceefcf2404d89f772de7d1b9747e90997`.
- `05_TECHNICAL_ARCHITECTURE/14.02_POSEIDON_v46.3_Product_Wide_Data_Dictionary_TR.xlsx`, sheet `Yönetici Özeti`, row 19; sheet `Karar Onayları`, row 10. SHA256 `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`.
- `05_TECHNICAL_ARCHITECTURE/49.01_POSEIDON_v46.3_Bayrak_Gemi_Tipi_Standardizasyon_Tasarimi_TR.docx`, paragraph 8 (proposal boundary), SHA256 `6d768de9f51efdd22bce59d24898c8046f7b017e9f63e4a1e2e974c2f5efb54c`.

POL-DEFAULT-01 is APPROVED — POLICY ONLY. Missing mandatory information may remain in drafts and must block sending for approval/processing. Field-specific rules remain open. Paragraph 8 does not make flag/type mandatory on every inventory draft; critical missing applicability information cannot imply READY or NOT_APPLICABLE.

Acceptance and test traceability (all tests in `RequiredDataPolicyTests`):

| ID | Acceptance | Test |
|---|---|---|
| POL-D01 | Blank text is missing; draft values remain representable | BlankTextRemainsMissingAndBlocksRequiredGate |
| POL-D02 | Known false/zero/text preserved | KnownFalseZeroAndTextArePreserved |
| POL-D03 | Unknown has no inferred payload and blocks required-data gate | UnknownNeverBecomesKnownOrMissing |
| POL-D04 | Nullable missing never defaults | NullableInputsDoNotDefault |
| POL-D05 | Absent mandatory blocks; optional missing/unknown alone does not | AbsentRequiredBlocksButOptionalMissingAndUnknownDoNot |
| POL-D06 | Reject ambiguous/invalid metadata, duplicate keys, undefined and mistyped values | AmbiguousKeysAreRejected; InvalidMetadataAndDuplicateDefinitionsAreRejected; DuplicateUndefinedMismatchedAndNullValuesAreRejected |
| POL-D07 | Snapshot definitions, stable ordinal blocking keys/reasons, immutable results, repeatable assessment | DefinitionsAreSnapshottedResultsImmutableAndAssessmentRepeatable |

Required field definitions come from trusted domain code. No endpoint accepts definitions. Assessment only reports the required-data gate; a satisfied gate does not authorize submission. No persisted entity, migration or synthetic seed is introduced. API enforcement, tenant/OIDC authorization, permit lifecycle and independent acceptance remain open.

Verification commands are in `docs/development.md`; CI targets domain/persistence unit projects and persistence/API integration projects explicitly, preserving zero-test failures. Local integration is deferred to parent CI because this package changes no persistence/API behavior.

Local verification (2026-10-31, project-local SDK):
- `dotnet restore Poseidon.slnx --locked-mode --source "$env:USERPROFILE/.nuget/packages" -p:NuGetAudit=false`: passed using cached packages; network vulnerability audit not run.
- `dotnet build Poseidon.slnx --configuration Release --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet test --project tests/Poseidon.Domain.Tests/Poseidon.Domain.Tests.csproj --configuration Release --no-build`: 16 passed, 0 failed, 0 skipped.
- `dotnet test --project tests/Poseidon.Persistence.Tests/Poseidon.Persistence.Tests.csproj --configuration Release --no-build --filter-not-trait 'Category=Integration'`: 12 passed, 0 failed, 0 skipped.
- `graphify update .`: passed (AST only); tool warned that six non-code files produced zero nodes.
- `git diff --check`: passed.

Integration tests not executed locally; both integration project commands remain mandatory in CI. No push/PR or independent self-acceptance performed.
