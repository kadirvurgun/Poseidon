# HELP-MANUAL-DOWNLOAD-01 — CI'da yeniden üretilen yardım görüntüleri + uygulama içinden indirilebilir kullanım kılavuzu

Durum: **AUTHORED_UNVERIFIED** (23.11.2026, dal `feature/manual-download-20261123`, taban entegrasyon başı 4f7d87ca). Türetilmiş iş (USER-GUIDE-01 UG-2/UG-4 devamı, HELP-CENTER-01 açık maddesi "kılavuzun uygulama içinden indirilebilir yayını"); canonical iş/SP değildir (114/121 iş, 932/970 SP değişmez). Bağımsız QA bekliyor. İçerik `reviewed:false`; UG-6 / UG-REV-01 insan SME/UX onayı yerine geçmez.

Kaynak: `docs/tasks/USER-GUIDE-01.md` §3 B, §5 D4/D5/D7, §6 UG-DOC-01/02/03, §7 UG-4/UG-6; `docs/phases/HELP-CENTER-01.md` "Ertelenen / açık"; `docs/phases/USER-GUIDE-01-UG2.md` açık konular (kalkış görüntüleri sunucu tarihine bağlı).

## A — Görüntülerin CI'da yeniden üretimi

- **Rozet yerleşimi (7f196f8c):** `capture.ts` içindeki iyileştirme geri uygulandı: önceden yerleştirilen rozetler ve diğer hedeflerin halkaları engel sayılır; son çare konum görünüm alanı içinde ve commit damgasının üstünde sıkıştırılır.
- **Sabit saat:** `POSEIDON_SCREENSHOTS_FIXED_CLOCK` (yalnız `YYYY-MM-DDT00:00:00.000Z`, doğrulanır; yoksa önceki sabit `2026-11-22T00:00Z`). Kalkış değerlendirmesi sunucu tarihine bağlı olduğundan (sayfa tarayıcı tarihini önerir, sunucu geçmiş aralığı reddeder) iş akışı çalıştığı günün UTC gece yarısını verir. Değer çalıştırma boyunca tek bir sabit an olarak kalır ve her köken JSON'una yazılır.
- **Linux lab yolu:** lab şimdiye kadar yalnız Windows'ta çalışıyordu. Bağımsız incelenmiş `oidc_lab.py` değiştirilmedi. Değişenler:
  - `lab_profile_acl.py`: POSIX'te aynı sahiplik denetimi, ardından sahip-yalnız `0700` ve geri okuma (`prepare_posix`, `validate_posix`). Windows yolu aynı. Birim testleri: 0700 + geri okuma, yanlış sahip işaretinde mod değişmez, grup/dünya/yabancı uid reddi.
  - `lab_profile_ca.py`: aynı NSS API'si aynı sabit Playwright Firefox dizininden yüklenir. Linux Firefox NSS'i katlamadığı için semboller `libnss3.so` + `libsmime3.so` içinde aranır (`CERT_DecodeCertFromPackage` smime'dadır). Sistem NSS'i veya certutil kullanılmaz.
- **İş akışı** `.github/workflows/help-screenshots.yml` (**zorunlu kontrol değildir**): yalnız `screenshots/**` dallarına push ile çalışır, çünkü workflow_dispatch varsayılan olmayan bir dalı hedefleyemez. `contents: read` izniyle çalışır, depo gizli değeri kullanmaz ve hiçbir şey push etmez. Belgelenmiş yerel sırayı izler: build (frontend + Release API/DbMigrator) → üreteç temiz mi (`git diff --exit-code`) → `oidc_lab.py init` → `start` → `help_screenshots_lab.py prepare_screenshots` → API'yi pid ile durdur (`owned_process` Windows'a özgü Win32_Process kullanır) → `start` → sıkı Firefox profil ön denetimi → `screenshots` `update` → ikinci bağımsız `compare` → `steps/provenance/png-palette` testleri → `manual:pdf` + `manual.test.ts`. Keycloak kapsayıcısı (uid 1000) salt-okunur bağlanan realm dosyasını okuyabilsin diye geçici koşucuda tek bir adlandırılmış okuma ACL'i verilir. Yükleme yalnız PNG + köken JSON, sınırlı rapor (ad/sonuç/oran/kod) ve kılavuz çıktılarını içerir; özel lab dizini yüklenmez.

## B — Uygulama içinden indirilebilir kılavuz

- **Varlıklar:** `frontend/src/help/manual/poseidon-user-manual-{tr,en}.pdf` + `provenance.json` (şema `poseidon.help.user-manual-provenance.v1`, sha256, bayt, üreteç commit'i, workflow çalıştırması, tarih, `reviewed:false`). Vite bunları `?url` importuyla içerik özetli `assets/*.pdf` olarak yayar.
- **Çevrimdışı önbellek:** `offline-shell-build.ts` yalnız `assets/*.{js,css,woff2}` (+ index.html, appearance-init.js) kabul eder. PDF'ler envantere girmez, 128 giriş sınırı etkilenmez. Çevrimdışıyken bağlantı yerine "çevrimiçi olunca indirilebilir" notu gösterilir.
- **`/app/help`:** "bu sürümde yok" notunun yerine TR ve EN indirme bağlantıları geldi (`download` dosya adı, `type="application/pdf"`, `hreflang`, yerel biçimde MB boyutu). Okurun dili önce gelir; TR/EN dışındaki dillerde "yalnız Türkçe ve İngilizce" notu gösterilir. `reviewed:false` olduğu sürece taslak notu ve her dosyada "Taslak" rozeti bulunur; uygulama hiçbir yerde onay iddia etmez. Bölüm yalnız oturum açmış okuyucuya (`access === 'allowed'`) gösterilir; oturumsuz, yetkisiz, oturumu okunamayan veya beklemedeki okuyucu bağlantı görmez. Not: PDF, JS paketi ve kılavuz metni gibi kimlik doğrulamasız statik bir varlıktır; yalnız sentetik lab verisi içerir. Gizleme yalnız arayüz düzeyindedir.
- **i18n:** `HelpCenter_tsx_copy` içinde `manualBody` yeniden yazıldı; `manualDraft`, `manualOtherLocales`, `manualTr`, `manualEn`, `manualOffline` 17 katalogda. `py scripts/i18n_untranslated.py --prefix copies.HelpCenter_tsx_copy`: tüm dillerde 0 çevrilmemiş / 0 eksik / 0 fazla.
- **Sunucu:** API ön ucu yalnız Development lab'ında (`DevelopmentLab:WebRoot`, `UseStaticFiles`) sunar. Varsayılan `FileExtensionContentTypeProvider` `.pdf` → `application/pdf` eşler. Genel başlıklar (`X-Content-Type-Options: nosniff`, `Cache-Control: no-store`, `Referrer-Policy: no-referrer`) indirmeyi engellemez. CSP başlığı yoktur ve satır içi yürütme gerekmez. Backend değişikliği gerekmedi. Depoda ayrı bir üretim statik barındırma tanımı yoktur.

## Doğrulama (gerçek sonuçlar)

| Kontrol | Sonuç |
|---|---|
| Help screenshots run 45075400512 (fe00c0a9) | KIRMIZI: lab başlatma, yetkiler ve demo fixture geçti; sıkı profil ön denetimi `undefined symbol: CERT_DecodeCertFromPackage` (libnss3.so) ile düştü → libsmime3 düzeltmesi |
| Help screenshots run **45075963418** (5e7e2e0e) | **success**: `update` 196 written; ikinci `compare` 196/196 match, en büyük oran 0; CI'da steps/provenance/png-palette ve manual testleri geçti; PDF TR 9,41 MB, EN 9,46 MB |
| Yerel `node --test` steps + provenance + png-palette + manual.test (Node 26.13.0, indirilen görüntülerle) | 29/29 geçti |
| `py -3.13 -m unittest test_lab_profile_acl` (Windows) | 20 test, 2 POSIX testi atlandı (bunlar Linux'ta çalışır) |
| Mobil görüntü göz kontrolü (tr/today/1, tr/user-admin/7, en/departure-signoff/3, en/tug-job-board/2, tr/audit/2, en/companies/1-desktop) | Üst çubuk opak ve içerik arkasından görünmüyor; rozetler halkaların dışında; sentetik adlar dışında kişisel veri yok; zaman/UUID maskeli |
| `npm run typecheck`, `npm run lint` (frontend) | exit 0 |
| `npx vitest run` (tam, Node 26) | 234 dosya, 3107/3107 geçti |
| `npm run build` | geçti; `dist/assets` içinde 2 PDF (özetli ad) ve 196 PNG; `offline-shell.js` 36 giriş, `.pdf`/`.png` girişi 0 |
| `py scripts/secret_scan.py --mode client --bundle frontend/dist` | 235 dosya, 0 bulgu |
| `py scripts/i18n_untranslated.py --prefix copies.HelpCenter_tsx_copy` | 0/0/0 (tüm diller) |

Test düzeltmesi: `HelpCenterWorkspace.test.tsx` içindeki DE testi, değişiklikten önce de (HEAD 4f7d87ca) bu makinede kırmızıydı. Neden, DE kılavuzlarının 1 sn içinde yüklenememesiydi. Dosyadaki diğer çağrılar gibi `guideLoad` zaman aşımı eklendi; doğrulama koşulu değişmedi.

## Ertelenen / açık

- Bağımsız QA; UG-6 / UG-REV-01 insan SME/UX onayı (AI kapatamaz). Kılavuz ve PDF'ler `reviewed:false` olarak kalır.
- Görüntülerde görülen ürün kusurları (önceden de vardı, bu pakette düzeltilmedi): dil seçicideki küre simgesi metnin üstüne biniyor; mobilde uzun tanılama kodları (kalkış "Neden") ve uzun iş numaraları satır taşırıyor; commit damgası 390 px'de iki satıra kırılıyor.
- Depo boyutu: iki PDF yaklaşık 18,9 MB ekler. Kılavuz yeniden üretildiğinde PDF'ler de değişir; daha küçük bir PDF (görüntü yeniden sıkıştırma) ayrı bir karar konusudur.
- PDF bayt düzeyinde belirlenimci değildir (Chromium oluşturma tarihi yazar). Doğrulama `provenance.json` sha256 değerine dayanır.
- Linux artık referans görüntüleme platformudur; Windows'ta yerel `compare` yazı tipi farkı verebilir.
- UG-DOC-04 çevrimdışı PDF: bilinçli olarak yok, çünkü PDF çevrimdışı envantere alınmaz.
