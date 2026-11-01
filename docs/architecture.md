# Mimari başlangıç kararları

Durum: öneri / kısmi doküman incelemesi ve ilk persistence altyapısı. Bütün gereksinimlerin doğrulandığı veya ürünün sertifikalı olduğu iddia edilmez.

## Teknoloji ve sınırlar

23.03 Deployment Runbook başlangıç teknolojileri: .NET 10, React 18, PostgreSQL 17+, nesne depolama; modular-monolith-first. Kullanıcı .NET/React/EF Core ve en güncel kararlı, uyumlu sürümleri istedi. EF Core 10.0.14 uygulanıyor; frontend hedefi React 19.3.1'e güncellendi. Sürüm kanıtları ve güncelleme yöntemi [sürüm politikasında](dependency-policy.md).

Modüller: Core/IAM, Fleet, Crew, Technical, Ship Operations, Tug Operations, Safety, Assurance, Regulatory, Evidence/Decision, Integration/Offline. Modül sınırları domain ve aggregate'lara dayanır; ekran başına tablo veya servis oluşturulmaz. Mikroservise ayrılma ancak somut ölçek/ekip gereksinimiyle değerlendirilir.

Karar: EF Core persistence, migration ve [seed import](seed-data.md). Modül bazlı PostgreSQL şemaları, React + TypeScript, OpenAPI'den türetilen API tipleri, transactional outbox, S3 uyumlu nesne depolama hedefleniyor. IdP, dağıtım sağlayıcısı ve offline istemci depolaması henüz seçilmedi. [Güvenlik/audit/gizlilik sözleşmesi](security-baseline.md) tüm modüllere uygulanır.

## Değiştirilmeyecek iş kuralları

- Tenant, vessel ve job yetkileri backend'de uygulanır.
- Gemi ve römorkör Vessel/Person/Asset/Evidence/Audit köklerini paylaşır.
- Applicability TRUE/FALSE/UNKNOWN; kritik UNKNOWN, REVIEW_REQUIRED üretir.
- Completed, Verified ve Effective ayrı durumlardır.
- Master/Tug Master emniyet kararı kıyı operatörü, platform yöneticisi veya AI tarafından atlanamaz.
- Kanıt, doğrulama, imzalı karar ve audit kayıtları yayımlandıktan sonra değiştirilemez; yeni sürüm veya iptal kaydı üretilir.
- Kritik offline komutlar idempotenttir. Maddi güvenlik/hukuk çatışmalarında sessiz last-write-wins uygulanmaz.
- Tam finans, bordro, satın alma ERP'si ve liman trafik yönetimi kapsam dışıdır.

## Teslim sırası

1. Faz 0: tüm canonical belgelerin incelemesi, register/traceability/backlog aktarımı, ADR'ler, repo, CI, görev ve faz sözleşmeleri.
2. Faz 1: IAM/tenant, Vessel, Audit, Evidence; örnek dikey akış ve tenant izolasyonu testleri.
3. Faz 2: Crew, Technical, Regulatory ve açıklanabilir Readiness.
4. Faz 3: Ship ve Tug kritik yolculukları; ilk desteklenen akıştan itibaren offline testleri.
5. Faz 4: Safety/Assurance ve kalan MVP kapsamı; tam regresyon.
6. Pilot: gerçek gemi + römorkör, Marine/Regulatory uzman onayları, DR ve kontrollü release.

Bu sıra bir başlangıç önerisidir; 18.01 backlog ve 11.01 traceability satırları incelendikten sonra görev bağımlılıkları kesinleşir. Otomasyon uzman ve gerçek pilot imzalarını kendisi üretmez.

## Test sözleşmesi

| Tetik | Zorunlu kontroller |
|---|---|
| Ürün paketi geliştirilirken | Değişen davranışın developer testleri kodla birlikte yazılır; kullanıcı talimatıyla paket bitmeden çalıştırılmaz |
| Her push ve PR | Backend build + unit + gerçek PostgreSQL ile integration; frontend lint/typecheck/build + component; API contract + Playwright smoke |
| Faz bitişi | Bütün önceki kontroller + Ship/Tug Playwright regresyonu, offline/retry/duplicate/conflict, tenant ve rol izolasyonu, regulatory golden cases, migration doğrulaması |
| Release | Faz kontrolleri + güvenlik, restore/rollback kanıtı, UAT/pilot ve yetkili karar |

Önerilen araçlar: xUnit, WebApplicationFactory, Testcontainers/PostgreSQL; Vitest + Testing Library; Playwright. Araç sürümleri uygulama iskeleti kurulurken sabitlenir.

01.11.2026 kullanıcı kararıyla yerel build/test bütün ürün paketi hazır olduğunda
topluca çalışır; daha sonra gereken push/PR ve release kapıları korunur.
Testi silmek, skip etmek veya kabul eşiğini düşürmek başarısız işi başarılı yapmaz.
Eksik test projesi CI'da başarı sayılmaz. Yerel commit test sonucu değildir.
