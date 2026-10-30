# DB-FOUNDATION-01 — Persistence, seed ve audit doğrulaması

Tarih: 30 Ekim 2026. Durum: **yerel teknik doğrulama geçti; remote teslim/CI bekliyor**.

Bu paket S0 hazırlığı ve ilerideki S1 foundation işleri için altyapıdır. Bütün S0/S1 fazının tamamlandığı anlamına gelmez. Ana backlog tamamlanması %0 kalır; hiçbir tam acceptance criteria paketi bu alt teslimat nedeniyle otomatik kapatılmaz.

## Teslim edilen davranış

- .NET 10.0.403, EF Core 10.0.14 ve Npgsql 10.0.3; sürümler/lock dosyaları sabit.
- Tenant/Company/Vessel/VesselProfile/TugProfile için PostgreSQL modeli ve migration.
- İki tenant'lı sentetik manifest; parent/child ve profile doğrulaması; eksik kayıtları ekleyen, mevcut düzenlemeleri koruyan seed.
- Senkron/asenkron EF seed hook'ları ve migration kilidi; eşzamanlı initializer'larda mükerrer kayıt yok.
- Tenant query/write sınırları, compound FK ve xmin concurrency kontrolü.
- Yazma ile aynı transaction'da aktörlü audit; EF ve PostgreSQL trigger'ı ile audit mutasyon engeli.
- Identity PasswordHasher kullanan ilk hash bileşeni; henüz login/session servisi yok.
- Push/PR için backend foundation workflow'u; remote'da koştuğu henüz doğrulanmadı.

## Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Release build | PASS; 0 warning / 0 error |
| Backend unit/guard | **12 PASS**, 0 fail / skip |
| PostgreSQL 18.6 integration | **10 PASS**, 0 fail / skip; gerçek Docker container'ları |
| Belge aktarım davranış testleri | **4 PASS** |
| EF model/migration tutarlılığı | PASS; pending model change yok |
| Idempotent SQL migration script üretimi | PASS; sadece üretim, canlı DB uygulanmadı |
| Production ortamında demo seed reddi | PASS; DB bağlantısından önce reddedildi |
| Bağımsız statik QA | Sınırlı foundation kapsamında bloklayıcı bulgu yok; son commit'e bağlama ve remote CI ayrıca izlenir |

Integration senaryoları: tekrar seed ve düzenlemeyi koruma; tenant read/write; audit UPDATE/DELETE/TRUNCATE reddi; geçersiz manifestte kısmi yazı olmaması; tenant dışı FK reddi ve audit rollback; senkron migration; üç eşzamanlı initializer; dış transaction rollback; başarısız yazıyı düzelterek tekrar kaydetme; stale xmin güncellemesinin ve başarısız işlem audit'inin reddi.

Komutlar:

```text
dotnet test --solution Poseidon.slnx --configuration Release --filter-not-trait Category=Integration
dotnet test --solution Poseidon.slnx --configuration Release --filter-trait Category=Integration
python -m unittest discover -s scripts -p test_*.py -v
```

## Gereksinim izi ve açık kabul koşulları

Kaynak: [18.01 Backlog](<../sources/text/06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx.md>).

| İş | Bu paketin katkısı | Açık kalan |
|---|---|---|
| DEV-002, row 3 | Backend CI tanımı | Remote CI ve frontend/diğer kabul kontrolleri |
| DEV-005, row 6 | İlk fiziksel model | Ürün geneli normalizasyon planı |
| DEV-014 / T-S1-002, row 15 | DB tenant sınırları | Doğrulanmış kimlik, HTTP 403/404 policy ve uçtan uca yetki |
| DEV-016 / T-S1-004, row 17 | Immutable write-audit çekirdeği | Role/vessel/reason, izinli before/after, audit API ve tam kritik write coverage |
| DEV-019 / T-S1-007, row 20 | İlk tenant negatif testleri | RBAC, vessel/protected object ve remote CI kanıtı |

## Dış engel ve sıradaki adım

Docker engeli kalktı. `origin/main` okunuyor; push dry-run GitHub'da **avurgun1 hesabı için 403** alıyor. Depo sahibi kadirvurgun; Write yetkisi veya doğru yerel Git oturumu gerekiyor. Yerel GitHub CLI oturumu da açık değil. Token/parola sohbetten istenmiyor.

Değişiklikler remote başlangıç commit'ine dayanan bootstrap/foundation dalında korunuyor. Yetki sağlanınca dal push, PR ve CI doğrulaması tamamlanır. Sonraki geliştirme: S0 karar/ortam/trace paketinin kalanları ve seçilen kimlik sağlayıcısına uygun HTTP auth/audit altyapısı. React/Playwright ve sürekli ajan servisi henüz çalışır değildir.
