# RULE-OWNING-ADAPTERS-01 — AUTHORED / NOT RUN

Existing incomplete profile fixtures acquire default Missing propulsion/contract-date/reference fields and null construction file only on fresh add-missing import. No extra entity, qualified profile, clean file, attestation or approval is seeded. Historical/operator JSON and immutable history are never reseeded over. Nullable file-binding and exact-outcome receipt columns remain empty for historical rows; no backfill. Actual commands alone create file relationships/receipts. Existing audit totals stay unchanged; PostgreSQL reseed/operator-preservation/immutability test source is authored, not executed.

# EF Core ve seed import

## TUG-OWNING-CONSUMERS-01 — AUTHORED / NOT RUN

Adds an incomplete fictional Tug capability Evidence draft/head/revision/binding
through the existing attributed add-missing importer. Existing operator revisions
are preserved. Exactly three new inserts are expected for the one synthetic Tug;
existing100/101 audit expectations become103/104, unexecuted. Relational/idempotent/
operator-preservation tests accompany it. No measurement, condition factor,
source-qualified calculation policy, certificate file, independent verification,
human authority/decision, device enrollment/key, replay receipt/outcome or lifecycle
is seeded. Offline tables stay empty until genuine authenticated commands.

## REG-SOURCE-DRAFT-01 — AUTHORED / NOT RUN

The existing synthetic import additionally authors one explicitly FICTIONAL Source draft and one FICTIONAL Requirement draft per tenant, each with an immutable initial revision. Deterministic tenant-derived IDs and add-missing behavior preserve operator edits/history. Requirement fixtures cite that tenant's source revision1; they never follow a later source edit. ObservedAt/declaration remain Unknown; provenance is SyntheticUnverified. There is no law text, signature, credential or approved catalog fixture. Eight actual additional entity imports produce eight attributed audit observations (initial total17→25); no-op reseed adds no audits/history/receipts. New PostgreSQL reseed/lineage/rollback tests and adjusted prior seed expectations are AUTHORED ONLY. Migration and test execution are deferred to CORE-BUNDLE-01.

İlk entity'ler: Tenant, Company, Vessel, VesselProfile, TugProfile. AuditEvent seed JSON'dan yüklenmez; importer'ın gerçek yazma işlemleri tarafından üretilir. Bunlar bütün ürün ERD'sinin tamamı değildir.

## Veri sözleşmesi

`backend/seed/synthetic-demo.json`: iki tenant, iki company, üç vessel, üç vessel profile ve bir tug profile. Veriler sentetiktir; gerçek müşteri/crew/PII, kullanıcı veya parola içermez. GT/BP örnekleri yasal uygunluk ya da READY kararı oluşturmaz.

- Manifest sürümlüdür, sabit GUID kullanır, tüm koleksiyonlar zorunludur. Bilinmeyen alanlar, mükerrer kimlik/kod ve tenant dışına çıkan parent ilişkileri reddedilir.
- Her vessel için profile gerekir; yalnız Tug türünde tug profile olur.
- Parent → child sırasıyla eksik kayıtlar eklenir. Mevcut kayıtlar güncellenmez/silinmez; kullanıcının düzenlemesi korunur. Yeni sürümde değişmesi gereken master-data ayrıca migration/change control ile yönetilir.
- Import tek transaction'dır. FK/index/concurrency kontrolü PostgreSQL'dedir. Tenant-scoped compound FK başka tenant'ın company/vessel'ına referansı engeller.
- EF `UseSeeding` ve `UseAsyncSeeding` birlikte tanımlıdır; migration kilidi eşzamanlı initializer'ları serileştirir. Normal API başlangıcında otomatik schema/seed uygulanmaz; ayrı migrator çalışır.
- Üretimde demo import reddedilir. Production varsayılandır; demo için Development veya Test açıkça seçilmelidir. Migration seed olmadan çalıştırılabilir.
- Her yeni operasyonel entity için ilgili fixture/seed ve doğrulama testleri eklenir. Audit, legal evidence, doğrulanmış mevzuat, imzalı karar ve credential gibi kayıtlar sırf tablo boş kalmasın diye sahte “doğrulanmış” veriyle doldurulmaz.

## Windows üzerinde çalıştırma

Güncel SDK kuruluysa `dotnet`; bu çalışma alanındaki yerel SDK için `./.tools/dotnet/dotnet.exe` kullanılır. Yerel SDK komutlarını çalıştırmadan önce:

```powershell
$env:DOTNET_ROOT = Join-Path (Get-Location) '.tools/dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet tool restore
dotnet restore Poseidon.slnx --locked-mode
dotnet build Poseidon.slnx --no-restore
```

Migration ve seed için ayrı geliştirme PostgreSQL veritabanına ait connection string'i process secret/config aracılığıyla `POSEIDON_DATABASE` değişkenine verin; repoya veya loga yazmayın:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --project backend/Poseidon.DbMigrator -- --seed backend/seed/synthetic-demo.json
```

İkinci çalıştırma eksik yeni kayıt yoksa business ve audit satırı eklemez. Demo olmadan yalnız migration:

```powershell
dotnet run --project backend/Poseidon.DbMigrator
```

Yeni entity/migration ekleme:

```powershell
dotnet ef migrations add ChangeName --context PoseidonDbContext --project backend/Poseidon.Persistence --startup-project backend/Poseidon.DbMigrator
```

## Testler

```powershell
dotnet test --solution Poseidon.slnx --filter-not-trait 'Category=Integration'
dotnet test --solution Poseidon.slnx --filter-trait 'Category=Integration'
```

İkinci komut Docker gerektirir; gerçek PostgreSQL container'ı açar. Docker eksikse integration testleri başarılı/skip sayılmaz, başarısız olur. Kapsam: tekrar import, kullanıcı düzenlemesini koruma, tenant isolation/FK, audit transaction'ı ve mutasyon yasağı; ayrıca senkron migration seed yolu. Henüz HTTP API/React/Playwright senaryosu yoktur.

.NET 10/xUnit v3 için global.json Microsoft.Testing.Platform çalıştırıcısını seçer; eski VSTest `--filter` biçimi kullanılmaz. Backend foundation workflow'u push/PR'da bu iki test grubunu, migration model kontrolünü ve belge aktarım testlerini çalıştıracak şekilde yazıldı. Henüz remote'da çalıştırıldığı doğrulanmadı; frontend/Playwright kapsamını içermez.

Kaynak: [EF Core Data Seeding](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding).

## DIR-WORKSPACE-01 preparation — NOT RUN

Online company/vessel create does not seed regulatory measurements or profiles. A newly created basic vessel has unknown regulatory data; zero GT/BP, verified evidence or approval is not fabricated. The existing complete synthetic profile manifest and idempotent importer are unchanged.

`core.CommandReceipts` records actual executed online commands; it is an observation/retry record, not an operational catalog to prepopulate. No receipt fixture is imported. API and persistence tests author actual synthetic operations and assert empty initial receipts, atomic write/audit/receipt behavior, tenant scope, append-only protection and reseed preservation. These tests are NOT RUN pending the parent integrated gate. Retention and controlled archive policy remain open; no production cleanup bypass is added.

## VESSEL-PROFILE-DRAFT-01

The existing manifest's vessels receive idempotent regulatory draft fixtures during actual synthetic import. These are separate from legacy VesselProfile/TugProfile measurements: all regulatory fields start Missing, declaration/source observation Unknown, reference `synthetic-demo/draft-v1`, provenance Synthetic. Save time describes the import operation only. Each actual draft insertion produces its immutable initial revision and attributed audit; no approval/verification/signature is seeded. Reseed does not update drafts or add history for existing rows. Three drafts and three revisions add six real audit observations (initial manifest total17). Command receipts remain empty until actual API commands. Gate evidence: [phase report](phases/VESSEL-PROFILE-DRAFT-01.md).

## AUDIT-TRACE-01 authoring — execution DEFERRED

No new historic audit/receipt fixtures or credentials. Existing synthetic imports produce metadata only for their actual audited writes; SeedImport source is explicit and old metadata is never backfilled. The directory API test collection owns one ephemeral PostgreSQL container and creates a distinct registered UUID database for each test, each with real current migrations and a fresh synthetic import. It never substitutes a shared mutable schema/InMemory database or imports a fake successful audit. Ownership, independent migration/row/receipt/trigger/audit isolation and rollback tests are authored, not executed; timings and all verification wait for AUDIT-FILES-BUNDLE-01. [Task/fixture safety contract](tasks/audit-trace-01.md).

## SECURE-FILES-01 authoring — NOT RUN

Each of the three existing synthetic vessels now authors one deterministic metadata-only FileStage with state SyntheticUnverified, empty actor assignment, zero length and explicitly synthetic zero digest placeholders. No byte object, actual scan, StoredFile, signed capability, evidence verification or command receipt is seeded. Import is add-missing, tenant-bound, idempotent and preserves intent/operator state. The three actual inserts add three real write audit observations: initial25→28; one later operation26→29. Existing seed negative assertions remain; revised count and fixture regressions are DEFERRED. These placeholders never satisfy the normal upload metadata policy or become downloadable.


## CREW-ROSTER-01 — AUTHORED / execution DEFERRED

Each existing synthetic vessel additionally receives one FICTIONAL/unverified tenant Person, vessel-local Position (rank/capacity unknown), incomplete Draft Assignment and immutable initial safe revision. IDs are deterministic per entity type and tenant/vessel; add-missing import preserves operator edits. No actual employment period, qualification, credential, medical decision, operational appointment or receipt is seeded. Twelve actual inserts for three vessels add twelve attributed writes (existing fixture34→46); related seed expectations and PostgreSQL reseed/tenant/rollback tests are authored, not run. No change to production import prohibition or normal-request context boundaries.

## EQUIPMENT-MAINTENANCE-01 — AUTHORED / execution DEFERRED

Each existing synthetic vessel additionally authors one FICTIONAL/unverified Asset and initial AssetRevision, one immutable synthetic PMS MaintenanceJob (not TugJob), one incomplete Draft MaintenancePlan and immutable initial calculation/input revision. Deterministic type-scoped IDs; add-missing only, preserving all operator changes. No owners, meter readings, condition observations, completed/verified maintenance, approvals or receipts are seeded. Five actual inserts per vessel author fifteen additional attributed writes: current fixture46→61, one later operation47→62. Counts are expectations, not executed observations. Related current model/migration/reseed/tenant/rollback tests are authored and DEFERRED. Normal API startup and production import prohibition remain unchanged. [Task/source/gate contract](tasks/equipment-maintenance-01.md).

## TUG-FOUNDATION-01 — AUTHORED / execution DEFERRED

The existing synthetic Tug receives one deterministic entirely incomplete draft
TugJob plus its immutable initial source-versioned revision, add-missing only.
Ship fixtures receive no TugJob. Operator edits are preserved. No owner, assignment,
measurement, receipt, dispatch approval, signed decision or credential is seeded.
Two additional real insert audit expectations change63/64 total after the prior61/62;
these totals are authored expectations, not observed execution. Migration/designer/
snapshot and independent PostgreSQL reseed/kind/tenant/immutability cases are authored,
all DEFERRED. [Task and acceptance gaps](tasks/tug-foundation-01.md).


## IMPORT-FOUNDATION-01 — AUTHORED / execution DEFERRED

Each existing fictional crew assignment receives one deterministic incomplete private
Credential declaration and one metadata-only initial CredentialRevision, add-missing
only. Composite tenant/vessel/assignment/person relationships are preserved. No
certificate number, issuer, dates, country, controlled lookup, real PII, verification,
signature, receipt, preview or successful import is seeded. Operator edits are never
updated by reseed. Six additional actual insert expectations change63/64→69/70;
these are authored test expectations, not executed counts. Source numeric precision
remains unresolved, and imported declarations cannot grant eligibility/readiness.
Migration/designer/snapshot, real PostgreSQL preservation/isolation/rollback and actual
API consumers are authored; all execution waits for the larger foundation gate.
[Task/source/remaining gates](tasks/import-foundation-01.md).

## DOMAIN-COMPLETION-01 checkpoint A — AUTHORED / execution DEFERRED

Each existing fictional Person receives an add-missing empty PersonalProfile and metadata-only initial revision. No private values, fake ciphertext, actual keys, files, qualification or effective appointment are seeded. Reseed preserves all columns/operator changes. Six additional insert audit expectations change69/70→75/76; these are authored expectations only. See the DOMAIN-COMPLETION-01 phase report for remaining software and separate deployment/source decisions.

## DOMAIN-COMPLETION-01 checkpoint B — AUTHORED / execution DEFERRED

Each existing synthetic Asset receives one add-missing empty unverified Observation, with no reading/value or observation timestamp. It cannot supply valid current PMS input or verified equipment evidence. Reseed preserves actual operator observations/revocations. Three new insert audit expectations change75/76→78/79; none was executed or observed. Production source/freshness/unit/calibration policy remains external; normalized adapter software is authored.


## FOUNDATION checkpoint F — photo_ref selection, AUTHORED / execution DEFERRED

Existing add-missing empty PersonalProfiles and safe PersonalProfileRevisions receive nullable photo columns only. They remain null; IsPhotoSelection is false for synthetic revisions. No new entity or synthetic active photo, object, clean scan, identity verification or binding is seeded. Current audit expectations78/79 are unchanged and unobserved. New actual API/PostgreSQL authored coverage selects/clears an ordinary scanned synthetic photo, reseeds and asserts operator state and two immutable selection revisions/receipts remain unchanged. Migration/designer/snapshot and all execution remain DEFERRED. [Exact report](phases/FOUNDATION-ACCEPTANCE-BUNDLE-01.md).


## CREW-IDENTITY-CLOSURE-01 — AUTHORED / NOT RUN

New add-missing synthetic Persons now contain no name/employee values, protected ciphertext or identity verification. Existing historical/operator Person rows are never reseeded over. Separate controlled current-rights conversion may encrypt historical values atomically; reseed preserves that state and receipt/history. No new seed entity/verified identity/qualification/photo/key or identity revision is fabricated; prior audit expectations78/79 remain unchanged and unobserved. New API/PostgreSQL and DB-guard/reseed test source is authored, not executed. Migration100000/designer/snapshot require later real generation/parity/application. [Scope](tasks/crew-identity-closure-01.md).

## CREW-LEGACY-CLOSURE-01 — AUTHORED / NOT RUN

Normal add-missing synthetic seed and its unobserved78/79 audit expectations are unchanged. Migration110000 adds nullable protection metadata and immutable reference/source guards without rewriting any historical/operator data or manufacturing capture authority. A separate explicit Test-only `--legacy-crew-browser-fixture` command authors one fictional unsupported old-format batch in fixed synthetic scope through ordinary tenant-scoped System/SyntheticFixture SaveChanges; it accepts no caller actor/scope/content and performs no schema/provider/auth setup. This is an opt-in disposable gate fixture, not normal seed/startup/production data. Add-missing retry preserves prior conversion/operator edits. Attribution/idempotency/preservation/environment-denial tests are authored, never run. No verified evidence/credential/consent/receipt/key is seeded. [Contract](tasks/crew-legacy-closure-01.md).


## OFFLINE-APPLICATION-01 — AUTHORED_UNVERIFIED

Normal add-missing seed creates no enrolled device/key/authority, replay receipt, conflict or resolution. Those are real user-context lifecycle facts and cannot be fabricated by seed. Explicit fictional integration fixtures provision policy only inside test host setup and use ordinary authenticated enrollment/key/replay endpoints. A real PostgreSQL test authors reseed twice preserving these actual synthetic-session mutations and tenant isolation. No real evidence/signature/credential is seeded; prior78/79 expectations unchanged and unobserved. Migration120000/model/designer, all fixtures/tests and provider execution DEFERRED. [Source/contract](tasks/offline-application-01.md).


## TUG-PLANNING-01 — AUTHORED_UNVERIFIED / execution DEFERRED

One add-missing empty SyntheticUnverified DraftPlan and immutable initial revision
now belong to the existing synthetic Tug job. No requested time is copied, and no
assigned Tug, measurement, verified capacity/evidence, appointment, receipt, permit,
decision or credential is seeded. Reseed preserves all operator revisions/links.
Two additional insert-audit expectations change78/79→80/81, never observed counts.
Real PostgreSQL seed/preservation/tenant/kind/history tests are authored, not run.
Migration130000/designer/snapshot and all generation/application wait for the larger
gate. [Task/source and actual remaining gaps](tasks/tug-planning-01.md).


## LIFECYCLE-APPLICABILITY-01 — AUTHORED_UNVERIFIED / NOT RUN

Add-missing fixture adds one incomplete tenant-local DRAFT RuleDraft/RuleRevision
per synthetic tenant. The seed actor remains system:seed-import through audited
SaveChanges; the empty ActorId and SystemSyntheticFixture context in the authoring
row explicitly mean no human appointment/signature. No DSL/legal catalog, observed
input, review, authority, evaluation, receipt, credential or evidence is fabricated.
Reseed preserves operator revisions. Two-tenant authored insert-audit expectations
80/81 become84/85; these are not observed counts or passing evidence. Persistence/
API/domain fixture tests and migration application/parity remain DEFERRED to the
combined gate. [Task and limits](tasks/lifecycle-applicability-01.md).


## HUMAN-DECISION-TRACE-01 — AUTHORED / NOT RUN

Human review events/receipts/appointments and retained evaluations are real lifecycle
facts, not seed catalogs. Normal add-missing seed inserts none; no verified/effective
evidence, decision, authority, signature, credential or fake observed audit is seeded.
Existing incomplete control/evidence fixtures remain unchanged. PostgreSQL reseed
coverage authors a fictional pending review via ordinary audited SaveChanges, reseeds
twice and asserts the actual synthetic-session event/rationale and audit count remain
unchanged. The independent-human API branch explicitly inserts a fictional test-only
pinned evaluation/rule fixture in the disposable owned database, then uses real HTTP
review commands and scanner/file transport. It is neither normal seed nor production
source/sign-off evidence. All generation/migration/fixtures/tests remain DEFERRED to
REGULATORY-CHAIN-BUNDLE-01. [Task](tasks/human-decision-trace-01.md).
