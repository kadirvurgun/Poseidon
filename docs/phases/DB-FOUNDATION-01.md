# DB-FOUNDATION-01 — Persistence, seed ve audit doğrulaması

Tarih: 30 Ekim 2026. Durum: **yerel teknik doğrulama + bağımsız QA + remote CI geçti; main'e teslim edildi**.

[PR #1](https://github.com/kadirvurgun/Poseidon/pull/1) merged. İncelenen kod commit'i: 56b284c7923c3e6a031a37fcc956d37f89468c86. Main merge commit: a7d5eafcf7ba952c03ff9d41b072c38b4d07304a.

Bu paket S0 hazırlığı ve ilerideki S1 foundation işleri için altyapıdır. Bütün S0/S1 fazının tamamlandığı anlamına gelmez. Ana backlog tamamlanması %0 kalır; hiçbir tam acceptance criteria paketi bu alt teslimat nedeniyle otomatik kapatılmaz.

## Teslim edilen davranış

- .NET 10.0.403, EF Core 10.0.14 ve Npgsql 10.0.3; sürümler/lock dosyaları sabit.
- Tenant/Company/Vessel/VesselProfile/TugProfile için PostgreSQL modeli ve migration.
- İki tenant'lı sentetik manifest; parent/child ve profile doğrulaması; eksik kayıtları ekleyen, mevcut düzenlemeleri koruyan seed.
- Senkron/asenkron EF seed hook'ları ve migration kilidi; eşzamanlı initializer'larda mükerrer kayıt yok.
- Tenant query/write sınırları, compound FK ve xmin concurrency kontrolü.
- Yazma ile aynı transaction'da aktörlü audit; EF ve PostgreSQL trigger'ı ile audit mutasyon engeli.
- Identity PasswordHasher kullanan ilk hash bileşeni; henüz login/session servisi yok.
- Push/PR için backend foundation workflow'u; iki remote koşu başarılı.

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
| Bağımsız statik QA | 56b284c hedef commit'i onaylandı; 55 kaynak hash'i de bağımsız doğrulandı |
| Remote push CI | [PASS](https://github.com/kadirvurgun/Poseidon/actions/runs/41075632700) |
| Remote PR CI | [PASS](https://github.com/kadirvurgun/Poseidon/actions/runs/41075709919) |

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

Docker ve GitHub erişim engelleri kalktı. Poseidon için kadirvurgun repo-local Git/GitHub oturumu ayrıldı; iş hesabı avurgun1 korunuyor. Workflow OAuth kapsamı onaylandı. Token/parola sohbetten istenmedi.

Değişiklikler main'e teslim edildi. Sonraki geliştirme: Pro/Plus worker oturumları, S0 karar/ortam/trace paketinin kalanları ve seçilen kimlik sağlayıcısına uygun HTTP auth/audit altyapısı. React/Playwright ve sürekli ajan servisi henüz çalışır değildir.
