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
  - Hazırlık eylemleri kapalı şemadır: tıklama/doldurma/Enter; adı kaydet/oluştur/gönder/onayla/imzala/doğrula/sil/iptal/askıya al/çıkış vb. olan bir denetime tıklama **yürütme öncesi reddedilir** (D5). Hedefler rol + erişilebilir ad, etiket veya metin; CSS seçici kabul edilmez.
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

CI: ilk push (`8053a871`, run 44966741552) kırmızıydı: (1) depo gizli tarayıcısı `oidc_lab.py` değişikliğini incelenmiş istisnaların dışına düşürdü → değişiklik geri alındı, eylemler `help_screenshots_lab.py`'ye taşındı; (2) aynı mobil JSON yetimi. Sonraki CI sonucu teslim raporunda.

## Açık konular

- Yazma/yönetim gerektiren 8 adım (tug-job-board 6; companies 4–6; user-admin 3, 5, 6) görüntüsüz. Görüntü için ayrı, yalnız lab'a özgü ve yalnız formu **açan** (kaydetmeyen) bir yazma hesabı gerekir; bu pakette bilinçli olarak eklenmedi.
- Mobil kompozisyon: çok hedefli adımlarda hedeflerin tek karede görünmesi için adım başına görünüm alanına özgü kaydırma gerekir.
- UG-DOC-04 (offline PWA) Playwright testi: DEFERRED (panel bileşen testi offline yer tutucuyu doğrular).
- Bağımsız QA ve CI; UG-REV-01 insan kapısı.
