# HELP-CENTER-01 — `/app/help` uygulama içi yardım merkezi

Durum: **AUTHORED_UNVERIFIED** (22.11.2026, dal `feature/help-center-20261122`, taban `origin/main` 36d5d183). Yerel hedefli typecheck/lint/vitest çalıştı; CI ve bağımsız QA bekliyor. Türetilmiş iş (USER-GUIDE-01 UG-4'ün eksik `/app/help` kısmı); canonical iş/SP değildir.

Kaynak: `docs/tasks/USER-GUIDE-01.md` §3 B, D2, D3, D6, D7; 65.03 p50 (gizli menü adı/sayısı sızmaz), p57–59 (yardım yan etkisiz).

## Teslim edilen davranış
- **Rota:** `/app/help` (`frontend/src/App.tsx`) → `frontend/src/help/HelpCenterWorkspace.tsx`. Oturum `auth/session`, sayfa listesi mevcut sunucu navigasyonu (`useEnterpriseNavigation`). Sunucu navigasyon sözleşmesi değişmedi: yardım merkezi bir yetki hedefi değildir, hiçbir şey vermez.
- **Menü girişi:** kenar çubuğunda sabit "Yardım merkezi" bağlantısı (`EnterpriseChrome.tsx`), yetkili hedeflerden sonra; çekmece odak döngüsünün son durağı.
- **Yetki süzme (D3):** yalnız kullanıcının navigasyonundaki sayfaların kılavuzu listelenir; gizli sayfa ad, sayı veya yer tutucu üretmez. Navigasyon bilinmiyorken (yükleniyor, hata, oturum yok, çevrimdışı) hiç kılavuz listelenmez. `?page=<gizli-anahtar>` bilinmeyen adresle aynı metni verir.
- **Gruplama ve arama:** kenar çubuğu bölümleri (`enterprise-sections.ts`, kılavuz üreticisiyle aynı sıra). Arama başlık + kılavuz gövdesinde, menü filtresinin yerel katlamasıyla (`foldMenuText`, TR I/İ ve aksan) çalışır; Escape temizler.
- **Kılavuz açma:** panelin aynı renderer'ı (`HelpGuideContent`, `HelpPanel.tsx`'ten dışa açıldı; davranış değişmedi). URL `?page=<anahtar>` (geri/ileri çalışır), "Bu sayfayı aç" derin bağlantısı sayfanın kendi `href`'i, "İlgili sayfalar" yine yalnız yetkili sayfalar.
- **Taslak bildirimi:** her girişte `reviewed:false` için "Taslak" rozeti; açık kılavuzda panelin "Taslak kılavuz — uzman onayı bekliyor" notu; TR/EN dışı dillerde sayfa başında "makine çevirisi, gözden geçirilmedi" notu. Hiçbir yerde "onaylı" denmez.
- **Kullanım kılavuzu (TR/EN HTML+PDF):** build çıktısında erişilebilir değil (`.runtime/user-manual/`, commit edilmez). Sayfa bunu dürüstçe söyler (uygulama dışında üretilir, bu sürümde yok, yöneticiden istenir, uzman incelemesine kadar taslak); sahte indirme bağlantısı yoktur.
- **i18n:** yeni `copies.HelpCenter_tsx_copy` (19 anahtar) 17 katalogda; `py scripts/i18n_untranslated.py --prefix copies.HelpCenter_tsx_copy` tüm dillerde 0 çevrilmemiş / 0 eksik / 0 fazla.
- **Çevrimdışı önbellek:** yardım merkezi ana parçaya statik import edilir, yeni parça eklemez. Yerel `vite build` envanteri 36 giriş (sınır 128).

## Doğrulama (yerel, hedefli; Docker/PostgreSQL kullanılmadı)
- `npm run typecheck` — exit 0.
- `npm run lint` (`--max-warnings 0`) — exit 0.
- `npx vitest run src/help/HelpCenterWorkspace.test.tsx` — 9/9 geçti (arama, gruplama, reviewed:false + taslak dil notu, yetki süzme, derin bağlantı, `?page=` gizli anahtar, bilinmeyen navigasyon durumları, kenar çubuğu girişi).
- Etkilenen dosyalar: `src/help` (HelpPanel, help-coverage, HelpMarkdown), `src/i18n` (messages, locales, labels, code-option-labels, index), `src/App.test.tsx` ve EnterpriseChrome kullanan 8 ürün testi tek koşuda: 18 dosya, 1011/1011 geçti. **Düzeltme (QA, d3645d9d):** bu koşu `src/product/Workspace.test.tsx` dosyasını içermiyordu; o dosyadaki çekmece odak testi yeni son durak yüzünden kırmızıydı. QA düzeltmesi ve tam suite sonucu aşağıda. `EnterpriseChrome.test.tsx` çekmece odak döngüsü beklentisi yeni son durağa (Yardım merkezi bağlantısı) güncellendi.
- `scripts/offline-shell-build.test.ts` bu makinede 9 testte `SubtleCrypto.digest ... not instance of ArrayBuffer` ile kırmızı: bu paket o betiğe dokunmuyor; yerel Node v22.15.0 `package.json` engines alt sınırının (^22.22.2) altında. CI'da doğrulanacak — **DEFERRED**.

## QA düzeltmesi (d3645d9d REJECT sonrası)
- Engelleyici: `src/product/Workspace.test.tsx` çekmece odak testi yeni son durağı (`Yardım merkezi` bağlantısı) bekliyor; aynı beklenti Playwright `specs/product.spec.ts` mobil çekmece adımına da eklendi (bağlantı varsa o, yoksa Çalışma görünümü). Bağlantı artık yalnız oturum açmış okuyucuya gösterilir. Başka "son odak durağı" iddiası grep ile bulunmadı.
- a11y: liste görünümüne görünür h2 "Sayfa kılavuzları" (h1→h2→h3); kılavuz açılınca odak kılavuz başlığına, "Tüm kılavuzlar" ile liste başlığına gider; `aria-live` tüm listeden kaldırıldı, yalnız sonuç sayısı `role=status` ile duyurulur.
- Oturum okunamazsa ve okuma yetkisi yoksa ayrı, dürüst metinler (`sessionUnavailable`, `noAccess`); "Oturum açın" yalnız gerçekten oturum yokken.
- Çevrimiçi/çevrimdışı durumu `online`/`offline` olaylarıyla güncellenir.
- Yetkili sayfa var ama kılavuz yoksa `noPages` gösterilir.
- Testler güçlendirildi: ilgili sayfalar tam olarak `[Today]` olmalı ve gizli başlıklar yok; gizli `?page=` anahtarı bilinmeyen anahtarla aynı DOM'u üretmeli; sayaç, odak, oturum durumları, online olay testi, oturumsuz bağlantı yokluğu.
- en-GB başlığı "Help centre". 3 yeni anahtar 17 katalogda; `i18n_untranslated.py` 0/0/0.
- Sonuç: `npm run typecheck` 0, `npm run lint` 0, `npx vitest run src/help/HelpCenterWorkspace.test.tsx` 13/13; **tam `npx vitest run`: 233 dosya, 3080 geçti / 9 kırmızı** — 9'un tamamı bilinen ortam sorunu `scripts/offline-shell-build.test.ts` (QA, origin/main'de aynı 9'u doğruladı). Playwright spec değişikliği yerelde koşulmadı — **DEFERRED**.

## Ertelenen / açık
- UG-DOC-04 (çevrimdışı `/app/help`): çevrimdışıyken yetki bilinemediği için liste bilinçli olarak gösterilmez; çevrimdışı tam liste için önbelleğe alınmış navigasyon tasarımı gerekir — **DEFERRED**.
- Playwright/axe ve mobil 320 px görsel kontrolü — **DEFERRED** (bundle kapısı).
- Kullanım kılavuzunun uygulama içinden indirilebilir yayını (ör. imzalı statik varlık) — ayrı karar.
- UG-REV-01 insan SME/UX onayı — insan kapısı, AI kapatamaz.
