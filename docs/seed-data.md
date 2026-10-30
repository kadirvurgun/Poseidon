# EF Core ve seed import

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
dotnet ef migrations add ChangeName --project backend/Poseidon.Persistence --startup-project backend/Poseidon.DbMigrator
```

## Testler

```powershell
dotnet test --solution Poseidon.slnx --filter-not-trait 'Category=Integration'
dotnet test --solution Poseidon.slnx --filter-trait 'Category=Integration'
```

İkinci komut Docker gerektirir; gerçek PostgreSQL container'ı açar. Docker eksikse integration testleri başarılı/skip sayılmaz, başarısız olur. Kapsam: tekrar import, kullanıcı düzenlemesini koruma, tenant isolation/FK, audit transaction'ı ve mutasyon yasağı; ayrıca senkron migration seed yolu. Henüz HTTP API/React/Playwright senaryosu yoktur.

.NET 10/xUnit v3 için global.json Microsoft.Testing.Platform çalıştırıcısını seçer; eski VSTest `--filter` biçimi kullanılmaz. Backend foundation workflow'u push/PR'da bu iki test grubunu, migration model kontrolünü ve belge aktarım testlerini çalıştıracak şekilde yazıldı. Henüz remote'da çalıştırıldığı doğrulanmadı; frontend/Playwright kapsamını içermez.

Kaynak: [EF Core Data Seeding](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding).
