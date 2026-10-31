# FOUNDATION-APP-01 — ürün temeli / yerel doğrulama tamamlandı

Kaynak: kullanıcının 31.10.2026 ürüne geçme ve testleri temel paket sonunda
çalıştırma talebi. DEV-013/T-S1-001 (18.01 Backlog row14),
DEV-014/T-S1-002 (row15), 14.01 ERD paragraph7, mimari ve güvenlik sözleşmeleri.
DEV-013/014 bağımlılıkları ve dış IdP seçimi/kurulumu tamamlanmış sayılmaz.

## Paket kapsamı

Esra profilinde ayrı worktree'de geliştirme tamamlandı. OIDC authorization-code/PKCE
ve cookie session, sunucunun doğruladığı tenant/rol/aktör bağlamı, PostgreSQL
şirket/filo liste-detay API'leri, ayrı HTTP/security audit ve React uygulama
kabuğu aynı ürün temeli paketinde hazırlandı. Sahte üretim login'i veya
hardcoded credential yok. IdP yapılandırılmadığında dürüst setup-required
durumu ve kapalı business erişimi korunuyor. Teknik sağlık ve dashboard açık kalır.

Unit, gerçek PostgreSQL/API integration, React component ve Playwright senaryoları
uygulama ile birlikte yazıldı. Kullanıcı talimatıyla temel paket bitmeden
çalıştırılmadı; paket tamamlandıktan sonra entegre doğrulama kapısına geçildi.
Başarısız kontroller üretim kodu düzeltilerek tekrarlandı; assertion/skip gevşetilmedi.
Eksik test/QA ile merge yapılmaz.

## Mevcut durum

- Geliştirme: tamamlandı; temel uygulama Esra Pro, GPT-6.1 Sol medium;
  odaklı contract/test işleri Esra GPT-6 Luna medium.
- Release build: PASS, 0 warning / 0 error. Lint/typecheck/frontend build: PASS.
- .NET: 16 domain + 23 persistence + 29 API = **68 PASS**, 0 failed/skip/not-run.
- React component/transport: **46 PASS**.
- Playwright desktop/mobile Chromium: **14 PASS**, 0 failed/skip.
  Kapsam: 4 gerçek API smoke + 6 sentetik ops UI + 4 gerçek ürün setup/klavye.
- EF model/migration parity: PASS, açık `--context PoseidonDbContext` ile.
- Coordinator EF komutu regresyonu: **16 Python PASS**; tam Python toplu koşusu
  sürüyor, henüz PASS sayılmaz.
- PR/push: bağımsız exact-commit QA ve dört uzak CI kontrolü merge ön koşulu.
- Kabul: canonical 8/970 SP (%0,82), 2/121 görev değişmedi.
- Sonraki adım: bağımsız exact-commit QA, PR ve gereken CI.

## Doğrulama ayrıntısı ve sınırlar — 01.11.2026

İlk kapıda host config tamamlanmadan servis kaydı ve OIDC varsayılan claim
action'ın doğrulanmış `iss` alanını silmesi bulundu. Startup kaydı ve yalnız
issuer silme action'ı düzeltildi. İmza/issuer/audience/expiry/nonce/state/correlation
kontrolleri korunarak bütün 68 backend testi tekrar geçti. İkinci DbContext
sonrası EF gate/dev komutlarına açık context seçimi eklendi.

Microsoft.Testing.Platform için eski solution-level `--logger trx` denemesi
0 test/exit5 verdi; başarı sayılmadı. Ardından Release assembly'leri doğru xUnit
executable runner ile gerçekten çalıştırıldı. Yerel kanıtlar:
`.runtime/foundation-final-{Domain,Persistence,Api}.log`,
`.runtime/foundation-final-build.log`, `.runtime/foundation-final-browser.log`.
Playwright HTML/trace: `tests/Poseidon.Browser.Tests/playwright-report/`.

Gerçek Development OpenAPI dışa aktarıldı ve consumer tipleri üretildi.
Bilinmeyen vessel kind için sessiz Ship varsayımı yerine hata üretiliyor.
Security-event migration'ı gerçek PostgreSQL testlerinde uygulandı; ayrı writer
denial olaylarını business rollback'ten bağımsız koruyor. Olaylar sahte seed ile
üretilmiyor; mevcut Company/Vessel sentetik importer'ı korunuyor.

Bu yerel doğrulanmış hazırlık alt paketidir; tam S0/S1 veya release kabulü değildir.
Sentetik imzalı OIDC protokol testleri gerçek dış IdP/HTTPS tarayıcı kabulü değildir.
IdP/client, callback origin, üyelik provisioning, logout interoperability ve kaynak
bağımlılıkları açık. Company/vessel mutation ve formları sonraki pakettir:
yazma rolü, antiforgery, validation ve xmin conflict ile birlikte uygulanacak.
Privacy/retention, legal hold ve dış immutable export kapıları açık;
tam HTTP audit veya GDPR/KVKK uygunluğu iddia edilmez.
