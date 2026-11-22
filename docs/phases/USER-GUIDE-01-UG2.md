# USER-GUIDE-01 / UG-2 — ekran görüntüsü üretim hattı

Durum: **AUTHORED_UNVERIFIED** (22.11.2026). Betik yerel OIDC lab'ına karşı **uçtan uca çalıştırıldı**: 36 masaüstü PNG gerçekten üretildi ve ikinci bir çalıştırmada piksel farkıyla birebir doğrulandı (sonuçlar aşağıda). CI ve bağımsız QA bekliyor. İçerik `reviewed:false`; görüntüler insan SME/UX onayı (UG-REV-01) yerine geçmez.

Dal: `feature/help-screenshots-20261122` (taban `origin/main` dde4bb50, UG-1 dahil). Türetilmiş ek gereksinim; canonical sayım (121 iş / 970 SP) değişmez.

## Kaynaklar

| Locator | Kullanım |
|---|---|
| `docs/tasks/USER-GUIDE-01.md` §5 D4, D5; §6 UG-DOC-02, UG-DOC-03 | Deterministik, betikle üretilen, işaretli görüntü; sahte doğrulanmış/imzalı durum yok |
| `docs/seed-data.md`, AGENTS.md seed kuralları | Yalnız sentetik seed + açık opt-in FICTIONAL fixture; üretimde asla |
| `docs/tasks/dev-oidc-lab-01.md` | Lab sınırları: özel dizin, kimlik bilgisi asla çıktıya yazılmaz, test-runner DOM snapshot'ı yok |

## Teslim edilen davranış

- **Demo fixture komutu** (`backend/Poseidon.Persistence/Seeding/DemoFixtureCommand.cs`, `DbMigrator -- --demo-fixtures <seed.json>`): yalnız tam `Development`/`Test` ortamında (bağlantı açılmadan önce denetlenir), seed yoksa reddeder; tek denetimli yönetimsel işlemde `TugDemoFixtures` (çekme planı/brifing/römorkör günlüğü DRAFT) ve `AccountMembershipFixtures` (kiracı başına yetkisiz, `.invalid` issuer'lı KURGUSAL üye) ekler. Karar, onay, doğrulama, imza, yetki, kimlik bilgisi veya receipt yok. Tekrar çalıştırma bir şey eklemez.
- **Lab eylemleri** (`scripts/help_screenshots_lab.py`, `oidc_lab` modülünü kullanır; bağımsız incelenmiş `oidc_lab.py` kaynağı değiştirilmedi çünkü depo gizli tarayıcısının incelenmiş istisnaları satır + blob hash'ine bağlıdır): `prepare_screenshots` lab-manager'a okuma yetkileri (`regulatory.draft.read, maintenance.read, tug.read, membership.read`) ve iki gemi türü görünürlük kapsamı (`mode.tug, mode.cross`) ekler. QA N2: `mode.*` saf okuma değildir; lab-manager'ın görebildiği gemileri (römorkör; `mode.tug` varken yeniden gemi) genişletir, dolayısıyla lab-manager'ın mevcut yazma yetkileri bu özel lab'da o gemilere de uygulanır. İkisi gereklidir: iş panosu `mode.tug + tug.read` ister, `mode.tug` varken `mode.cross` olmadan Today'in gemi bağlamı kaybolur — yalnız `.runtime/oidc-lab/appsettings.json` (gitignore, commit edilmez) — ve demo fixture komutunu çalıştırır. `screenshots` yakalama betiğini çalıştırır. Yazma/yönetim/emniyet yetkisi bilinçli olarak verilmez.
- **Yakalama betiği** (`tests/Poseidon.Browser.Tests/screenshots/capture.ts`): `lab/run.ts` gibi doğrudan Playwright kütüphane koşucusu (trace/video/HAR/test-runner DOM snapshot'ı yok), `strictContext` (yalnız lab CA'sı, Firefox), gerçek Keycloak girişi. Sabit 1440×900 (ve isteğe bağlı 390×844), açık mod + Zümrüt palet (`poseidon.appearance.v2`) ve dil (`poseidon.locale`) yüklemeden önce yerel depoya yazılır; `reducedMotion: reduce`, animasyon kapalı, imleç gizli, `page.clock.setFixedTime(2026-11-22T09:00Z)`. Sunucu zaman damgaları, tarih/saat metinleri ve UUID'ler maskelenir. Her adımın hedef öğelerine CSS token'lı numaralı halka + rozet, altta commit kimliği damgası eklenir.
  - Modlar: `compare` (varsayılan; PNG yazmaz, piksel farkı oranı ≤ `POSEIDON_SCREENSHOTS_THRESHOLD`, varsayılan 0,002; damga şeridi karşılaştırma dışı), `update` (PNG + köken JSON yeniden üretir), `discover` (erişilebilirlik ağacını özel dizine yazar). Commit edilmeyen görünüm alanı (mobil) yalnız `.runtime/oidc-lab/screenshots-uncommitted/` altına yazılır.
  - Hazırlık eylemleri yalnız tıklamadır ve `steps.ts` içindeki **açık izin listesiyle** sınırlıdır (QA N1): tür (düğme / `<details>` özeti) + TR ve EN tam ad (desen yok, büyük/küçük harf katlama yok). Liste dört okuma amaçlı tıklamadır: Neden?/Why?, Neden / sonraki adım/Why / next action, Synthetic Operator A, Hesap/Account add41584. Adsız, yalnız sıra (nth), `re:` desenli, düğme dışı tıklamalar ve doldurma/Enter eylemleri reddedilir; eşleme dosyasını düzenlemek tek başına yeni tıklama ekleyemez. Koşucu tıklamadan hemen önce tarayıcıda öğenin gerçek rolünü ve erişilebilir adını (düğme) ya da kapalı `<details>` özetinin tam metnini (CSS +/− işareti ada eklendiği için) yeniden doğrular.
- **Köken (QA N3):** şema `v2`; her JSON `capture.ts` ve `steps.json` dosyalarının git blob kimliğini ve `dirty` bayrağını taşır. `update` kirli üreteçle çalışmayı reddeder; `provenance.test.ts` kirli kaydı ve depodaki dosyalarla eşleşmeyen blob'u reddeder (üreteç değişirse görüntüler yeniden üretilmelidir). Görüntüler üreteç commit'i `7fdbe061` üzerinden yeniden üretildi; `commit` alanı bu commit'i gösterir.
- **Adım eşlemesi** `frontend/src/help/screenshots/steps.json` (kılavuzların yanında). Okuma yetkili hesabın dürüstçe gösteremeyeceği adımlar açıkça `unavailable: write_permission_required` olarak işaretlenir; yetki veya durum sahnelenmez.
- **Çıktı:** `frontend/src/help/screenshots/<dil>/<anahtar>/<adım>-desktop.png` + aynı adla köken JSON (`generator`, `commit`, veri kümeleri, kiracı, sabit saat, PNG sha256/boyut, yakalanan DOM metni).
- **Panel:** ön bilgi `screenshots` artık adım numarası listesidir (`[1, 2, 4]`; sıralı, tekrarsız, katı ayrıştırma). "Adım adım" bölümünde listelenen her adımın altında görüntü (`loading="lazy"`, `decoding="async"`). Görüntü kaynağı yalnız derleme zamanı `import.meta.glob` URL'sidir; Markdown hâlâ görüntü üretemez. Yeni metinler 17 katalogda (`screenshotAlt`, `screenshotOffline`).

## Kapsam (gerçek görüntüler)

| Kılavuz | Görüntülü adımlar | Görüntüsüz adımlar (neden) |
|---|---|---|
| today | 1–6 | — |
| tug-job-board | 1–5, 7 | 6 "Yeni talep": yazma yetkisi gerekir |
| companies | 1–3 | 4 Yeni kayıt, 5 Düzenle, 6 Tüzel kişi kaydı (düzenleyici içinde): yazma yetkisi gerekir |
| user-admin | 1, 2, 4 (salt-okunur "yetkiyi düzenle" bölümü notuyla) | 3 Üye ekle, 5 Vekalet, 6 Yaşam döngüsü: `membership.manage` gerekir |

Toplam 18 adım × TR/EN = **36 PNG, 3,3 MB** (ortalama ~92 kB) + 36 köken JSON (144 kB).

## Kararlar

1. **Yalnız masaüstü commit edilir.** Mobil (390×844) yakalama çalışır ve doğrulandı (36 PNG, 1,4 MB, özel dizinde) ama commit edilmez: depo büyümesi ve mobilde birden fazla hedefin aynı kareye sığmaması (yalnız ilk hedef görünüme kaydırılır). Panel masaüstü görüntüyü gösterir.
2. **PNG'ler offline envantere girmez.** `offlineShellBuild` yalnız `assets/*.{js,css,woff2}` ve en fazla 128 girişi kabul eder; 36 PNG sınırı zorlar. Panel çevrimdışıyken (`navigator.onLine === false`) veya görüntü yüklenemezse nötr "Ekran görüntüsü çevrimiçiyken görüntülenir." notu gösterir; kılavuz metni offline çalışmaya devam eder (UG-DOC-04 metin kısmı). Build doğrulaması: `dist/assets` altında 36 PNG, `offline-shell.js` içinde 0 `.png` girişi.
3. **CI'da elle tetiklenen yakalama işi eklenmedi.** Lab Linux konteynerlı Docker (Keycloak + PostgreSQL), Windows'a özgü özel dizin ACL'i ve Firefox NSS profil CA'sı ister; GitHub Windows koşucuları Linux konteyner çalıştıramaz, Ubuntu yolu bu lab için hiç doğrulanmadı ve mevcut lab tarayıcı yolculukları da CI'da değil. Bunun yerine saf eşleme/köken testleri (`steps.test.ts`, `provenance.test.ts`) mevcut "Application foundation" işine eklendi; zorunlu kontrol listesi değişmedi.
4. **Görüntü optimizasyonu:** `scale: 'css'` (DPR 1) ve görünüm alanı (tam sayfa değil); ek bağımlılık (pngquant vb.) eklenmedi.

## Nasıl çalıştırılır (yalnız yerel geliştirici lab'ı)

```powershell
py -3.13 scripts/oidc_lab.py init
py -3.13 scripts/oidc_lab.py start
py -3.13 scripts/help_screenshots_lab.py prepare_screenshots   # okuma yetkileri + görünürlük kapsamları (özel config) + demo fixture
py -3.13 scripts/oidc_lab.py start                 # yeni üyelikleri okumak için yeniden başlat
$env:POSEIDON_SCREENSHOTS_MODE='update'; py -3.13 scripts/help_screenshots_lab.py screenshots   # yeniden üret
Remove-Item Env:POSEIDON_SCREENSHOTS_MODE; py -3.13 scripts/help_screenshots_lab.py screenshots # karşılaştır
py -3.13 scripts/oidc_lab.py stop
```

Sonuç `.runtime/oidc-lab/screenshot-report.json` (yalnız ad/sonuç/oran/bayt/sınırlı hata kodu). İsteğe bağlı: `POSEIDON_SCREENSHOTS_GUIDE=<anahtar>`, `POSEIDON_SCREENSHOTS_VIEWPORTS=mobile`.

## Doğrulama (gerçek sonuçlar, 22.11.2026)

| Komut | Sonuç |
|---|---|
| `oidc_lab.py init/start` + `prepare_screenshots` + `oidc_lab.py start` (kendi worktree lab'ı, proje `poseidon-lab-<hash>`) | PASS (eylemler ilk çalıştırmada oidc_lab.py içindeydi; aynı kod ayrı modüle taşındı, birim testleri yeniden geçti) |
| `screenshots` `update`, desktop | `status=passed`, 36/36 written |
| `screenshots` `compare` (ikinci bağımsız çalıştırma) | `status=passed`, 36/36 `match`, en büyük fark oranı 0,00028 (eşik 0,002) |
| `screenshots` `update`, mobile (özel dizin) | `status=passed`, 36/36 written |
| `node --test …/screenshots/steps.test.ts` | 4/4 geçti |
| `node --test …/screenshots/provenance.test.ts` | 3/3 geçti (36 PNG köken + DOM metni taraması) |
| `py -3.13 -m unittest test_help_screenshots_lab test_oidc_lab` (scripts) | 9/9 geçti (yeni 2: okuma kaydı yalnız okuma izni ekler, idempotent, yanlış realm/hesapta kapanır) |
| `dotnet test … --filter-class DemoFixtureCommandTests` | 7/7 geçti |
| `dotnet test … --filter-class DemoFixtureCommandPostgresTests` (Testcontainers PG 18.6) | 1/1 geçti |
| `dotnet test Api.Tests --filter-class *WallClockGuardTests` | 3/3 geçti |
| `npm run typecheck`, `npm run lint` (frontend) | geçti |
| `npx vitest run src/help src/theme/hardcoded-colors.test.ts src/i18n` | 9 dosya, 179 test geçti |
| `npm run build` (frontend) | geçti; 36 PNG asset, offline envanterde PNG yok |

| `npx vitest run` (tam frontend) | 230 dosya / 2296 test; ilk çalıştırmada 1 başarısız (yanlışlıkla depoya yazılmış mobil köken JSON'ları — yakalama betiği hatası düzeltildi, dosyalar kaldırıldı), düzeltme sonrası `help-coverage` yeniden geçti |

CI: ilk push (`8053a871`, run 44966741552) kırmızıydı: (1) depo gizli tarayıcısı `oidc_lab.py` değişikliğini incelenmiş istisnaların dışına düşürdü → değişiklik geri alındı, eylemler `help_screenshots_lab.py`'ye taşındı; (2) aynı mobil JSON yetimi. Düzeltme commit'i `8fd7dc03` → run **44967711805: success** (4/4 iş: persistence-and-security, frontend-and-browser, windows-automation, nightly-installer-construction).

## Bağımsız QA düzeltmeleri (8fd7dc03 → ACCEPT-WITH-NOTES)

| Not | Düzeltme |
|---|---|
| N1 | Yıkıcı kelime kara listesi kaldırıldı; `steps.ts` içinde açık tıklama izin listesi (tür + TR/EN tam ad). Koşucu tıklamadan önce rol + erişilebilir adı (düğme) ya da kapalı `<details>` özetinin tam metnini doğrular. Adsız/yalnız-nth/`re:`/düğme dışı tıklama ve doldurma/Enter reddedilir. Birim testleri: İmzala/İptal/İçe aktar/Çıkış, Grant/Confirm/Assign/Remove/Reject/Add, büyük/küçük harf ve Türkçe İ/ı varyantları. |
| N2 | `mode.tug/mode.cross` ayrı `SCREENSHOT_VISIBILITY_SCOPES` olarak modellendi ve belgelendi (görünürlüğü genişletir, saf okuma değildir). İkisi gerekli olduğu için kaldırılmadı. |
| N3 | Köken `v2`: üreteç blob'ları + `dirty`; `update` kirli üreteci reddeder (doğrulandı: kirli ağaçta `update` FAILED, dosya yazılmadı). 36 görüntü `7fdbe061` üreteç commit'inden yeniden üretildi; JSON blob'ları `git rev-parse HEAD:<yol>` ile birebir. |
| N5 | Rozet halkanın dışında; görünmeyen/başlık altındaki hedef açığa çıkarılır (companies/3 artık Ayrıntılar bölgesinin tamamını işaretler). today/4: hedef bölge başlığın altında tamamen görünür; yarı saydam yapışkan başlığın arkasındaki soluk kaydırılmış içerik (uygulamanın kendi görünümü) kalır. |
| N6 | `SyntheticMembershipGuardTests` (Theory, 4 vaka: fixture `.invalid`, SyntheticUnverified güvenilir issuer, ConfigurationImport güvenilir/`.invalid`): yetki ucu 409 `configuration_managed`, `Editable=false`, yetki revizyonu yok, `ProvisionedIdentityLocator` null, bağlanmış oturum 200 almaz. |

Yeniden doğrulama (22.11.2026, kendi lab'ı yeniden başlatıldı ve sonunda durduruldu):

| Komut | Sonuç |
|---|---|
| `screenshots` `update` (desktop, temiz üreteç `7fdbe061`) | `status=passed`, 36/36 written, 3,16 MB |
| `screenshots` `compare` (ikinci çalıştırma) | 36/36 `match`, en büyük fark 0,000094 |
| `node --test steps.test.ts provenance.test.ts` | 11/11 geçti |
| `py -3.13 -m unittest test_help_screenshots_lab test_oidc_lab` | 9/9 geçti |
| `dotnet test Api.Tests --filter-method *A_membership_not_provisioned_by_an_administrator*` (Testcontainers PG) | 4/4 geçti |
| `npx vitest run src/help` | 3 dosya, 95 test geçti |

Görüntü commit'i `2010e6c3` → CI run **44973811866: success** (4/4 iş).

## Rehber paketi entegrasyonu (`integration/guide-bundle-20261122`, 22.11.2026)

USER-ADMIN-02 davet akışı user-admin kılavuzunu 9 adıma çıkardı; UG-3 50 yeni kılavuz ekledi.

- **user-admin (9 adım):** 1, 2 ve 7 (salt-okunur "Yetkiyi düzenle" bölümü) görüntülü. 3–6 (davet oluşturma, bağlantı, yenileme/iptal, kabul) ve 8–9 (vekalet, yaşam döngüsü) `membership.manage` gerektirir → açıkça `unavailable`. Eski `user-admin/4` görüntüsü (artık başka bir adım) kaldırıldı.
- **Yeni eşlemeler (yalnız okuma):** `fleet` 1–7 (8 yazma gerektirir), `tug-command` 1–7. İzin listesine tek tıklama eklendi: salt-okunur araç ayrıntısını açan `Synthetic Ship A` satır düğmesi. noon-report / certificate-command-center / safety-events adımlarının çoğu yazma veya ek okuma yetkisi istediği için bu turda eşlenmedi.
- **Rozet:** adım başına tek numara (ilk hedefte); bitişik hedef halkaları artık rozetle örtülmez.
- Üreteç commit'leri `1ff000a8` (eşleme) ve `ffabd677` (rozet); görüntüler bu son üreteç commit'inden kendi lab'ımla (bu daldan derlenmiş Release API + frontend/dist) yeniden üretildi: **64 PNG, 5,45 MB** (yalnız masaüstü, TR/EN). Sonuçlar teslim raporunda.

## Mobil görüntüler ve genişletilmiş eşleme (`feature/help-screenshots-mobile-20261122`, 22.11.2026)

Taban `origin/integration/guide-bundle-20261122` (8a9d27f9). Kaynak: `docs/tasks/USER-GUIDE-01.md` §5 D4 (1440×900 **ve** 390×844, TR ve EN), 65.03 p57–59 (mobilde yardım paneli tam sayfa), UG-DOC-02/03.

- **Mobil commit edilir (Karar 1 değişti):** `committedViewports: ["desktop", "mobile"]`; her görüntülü adım için `<adım>-mobile.png` + köken JSON. `help-coverage.test.ts` ve `provenance.test.ts` her iki görünüm alanını zorunlu tutar.
- **Panel:** `help-screenshots.tsx` her adımı `<picture>` ile verir: `(max-width: 767px)` (paneli tam sayfa yapan `help-panel.css` kırılımı) altında 390 px mobil görüntü, üstünde masaüstü görüntü; mobilde görüntü en fazla 390 px genişlikte ortalanır. Yeni bileşen testi `<source media>` ve `srcset`'i doğrular.
- **Mobil kompozisyon:** `reveal()` artık tüm hedeflerin birleşik kutusunu yapışkan başlığın altına getirir (sığıyorsa), yoksa ilk hedefe düşer; ekran dışında kalan ek hedefe halka çizilmez, ilk hedef ekranda değilse adım başarısız olur (`target_off_screen`).
- **Oturum:** uygulama oturumu 30 dk sabit (kaymasız); iki görünüm alanı × iki dil çalıştırması bunu aştığı için her (görünüm alanı, dil) bloğu yeni bağlam + gerçek Keycloak girişiyle çalışır (giriş masaüstü düzeninde, sonra görünüm alanı değişir).
- **Sayfa içi maske:** zaman/tarih/UUID değerleri artık Playwright ekran görüntüsü katmanı yerine sayfada (satır içi stil: gri zemin, saydam metin) maskelenir; böylece yapışkan başlık ve commit damgası şeridi, altlarına kaymış maskelenmiş değerin üstünde kalır (mobilde önceden gri şeritler başlığın ve damganın üzerine biniyordu).
- **Sabit saat `2026-11-22T00:00Z`:** 09:00Z, aynı gün sunucunun verdiği 60 sn geçerlilikli kalkış değerlendirmesini tarayıcıda "eski" gösteriyordu; gün başı hiçbir sunucu zaman damgasından sonra değildir. Tarih/saat metinleri yine maskelenir.
- **Boyut:** `png-palette.ts` (bağımlılıksız, saf) — en sık 256 renk paleti, kalan renkler en yakın girdiye; herhangi bir piksel bir kanalda 24'ten (karşılaştırma eşiği) fazla kayarsa veya saydamlık varsa özgün kayıpsız PNG korunur. Örnek: 88,5 kB → 37,0 kB, en büyük kanal hatası 18. Her modda uygulanır (karşılaştırma aynı biçimi kıyaslar) ve üreteç dosyası olarak köken blob'una bağlıdır (`generatorFiles` 3 dosya). `png-palette.test.ts` CI adımına eklendi.
- **Per-step route:** `steps.json` adımı isteğe bağlı `route` alır (ör. gemi seçim adımı ile geminin sayfası); aynı `/app/...` (+ `?vessel=<uuid>`) deseni, `unavailable` adımda yasak.
- **Yeni görüntüsüz nedenler:** `synthetic_record_unavailable` (sentetik seed/fixture'da kayıt yok; sahnelenmez), `record_selection_not_allowlisted` (kaydı açmak çalıştırmaya bağlı ad/kimlik taşıyan, izin listesinde olmayan bir tıklama ister).
- **İzin listesi:** tek yeni tıklama `Hazırlığı değerlendir`/`Assess readiness` — `GET /api/v1/departure-readiness` (sayfanın önceden doldurduğu tarih aralığıyla okur/değerlendirir; Kaptan kararı ayrı `Record` komutudur, listede yok). Yıkıcı kelime testine Record/Release/Hold/Deny/Record triage/Report an event/Save draft/Release work order/Record verification/Filter/Refresh eklendi.
- **Lab okuma yetkileri** (yalnız özel `.runtime/oidc-lab/appsettings.json`, lab-manager): `daily.noon.read, certificate.read, hse.safety_event.read, departure.readiness.read, workorder.read, equipment.read, audit.read`. Rapor/ön değerlendirme/karar/doğrulama/serbest bırakma/yazma yetkisi yok (`test_help_screenshots_lab` bunları açıkça reddeder).

| Kılavuz | Görüntülü adımlar | Görüntüsüz (neden) |
|---|---|---|
| noon-report | 1 (Kayıtlı taslaklar bölgesi; boş liste metni "tamamlandığına kanıt değildir") | 2–6 yazma/inceleme |
| certificate-command-center | 1 (gemi seçimi), 2 (kapsam + takvim filtresi) | 3, synthetic_record_unavailable; 4–7 yazma |
| safety-events | 1 (Olaylar + "Yalnızca benim bildirimlerim") | 2–9 bildirim/ön değerlendirme/inceleme/kapanış |
| departure-signoff | 1 (gemi seçimi), 2–7, 9 (değerlendirme satırı, kategori tablosu, Neden, "Serbest bırakma neden kullanılamıyor", Kaptan kararları, Yenile) | 8 Kaptan kararı (`departure.decide` + atama) |
| work-order-center | 1 | 2–6 yazma/serbest bırakma/doğrulama/kapanış; 7 synthetic_record_unavailable |
| audit | 1–3 | 4–5 record_selection_not_allowlisted |
| tug-daily-log | 1 (günlük listesi; DemoFixture taslağı) | 2–8 yazma/Kaptan incelemesi |

towage-plan / towage-risk-briefing eşlenmedi: lab'da "gönderilmiş römorkör işi" yok, sayfa yalnız "No submitted tug jobs" gösterir.

Sonuçlar: GENERATION_RESULTS_PLACEHOLDER

## Açık konular

- Yazma/yönetim gerektiren adımlar (yukarıdaki tablolar) görüntüsüz. Görüntü için ayrı, yalnız lab'a özgü ve yalnız formu **açan** (kaydetmeyen) bir yazma hesabı gerekir; bu pakette bilinçli olarak eklenmedi.
- Kalkış değerlendirmesi görüntüleri sunucu tarihine bağlıdır (sayfa tarayıcı tarihini önerir, sunucu geçmiş aralığı reddeder): başka bir günde `compare` bu adımlar için yeniden üretim ister.
- UG-DOC-04 (offline PWA) Playwright testi: DEFERRED (panel bileşen testi offline yer tutucuyu doğrular).
- Bağımsız QA ve CI; UG-REV-01 insan kapısı.
