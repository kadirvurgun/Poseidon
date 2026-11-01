# EF Core ve seed import

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
