# THEME-FOUNDATION-01 — Değiştirilebilir tema temeli

17.11.2026. **AUTHORED_UNVERIFIED_NOT_ACCEPTED; yeni kabul puanı 0.** Kullanıcı isteğiyle 15.11.26 planının dalga 1 UI temeline eklendi. Tam sprint/release veya 196 ekran tema kabulü değildir.

Paylaşılan shadcn preseti `b3Zheoix4U` resmi registry theme yanıtıyla kaynak kaydına alındı: Stone taban, Emerald vurgu, Cyan chart, Sera; font/icon seçimleri Oxanium/Geist/Phosphor. Mevcut bileşen sistemine renk/radius tokenları uyarlandı; component/font/icon aile değişimi ayrı planda tutuldu. [Exact kaynak](../sources/ui/shadcn-b3Zheoix4U.json), [görev ve kabul kriterleri](../tasks/theme-foundation-01.md).

## Yazılan davranış

- Paylaşılan enterprise üst çubukta ve bağlantı ekranında TR/EN “Görünüm / Appearance” seçici; açık, koyu, sistem ve bağımsız Zümrüt/Okyanus/Grafit paletleri.
- Kapalı kozmetik tercih JSON'u tarayıcıda saklanır; storage reddinde sekme içi seçim devam eder. Sekmeler arası değişim/clear ve sistem modu OS tema değişimi izlenir. Account/tenant/credential verisi saklanmaz; sunucu hesabına tercih yazılmadı.
- Synchronous same-origin head bootstrap tercihi React öncesi root'a uygular. Public offline build envanteri yalnız exact `appearance-init.js` yolunu ekler; gerçek byte/hash değişimi public shell sürümüne dahildir. API/auth/özel dosya cache kapsamı genişletilmez.
- Ortak shadcn değişkenleri ve enterprise form/tablo/panel/nav, temel product yüzeyleri ve bağlantı ekranı tokenlara geçirildi. Warning/danger/info ayrı semantik tokenlardır. Mod/palet değişimi formu remount etmez, iş durumu/authority veya navigasyon çağrısı yapmaz.
- 6 component/store/boot davranış senaryosu ve 1 public offline/integrity senaryosu yazıldı: draft/status korunması, restore, OS/system, iki seçicinin eşitlenmesi, storage engeli/bozuk kayıt/cross-tab clear, Escape/focus ve erken bootstrap.

## Gerçek doğrulama ve açık kapsam

Kaynak keşfi, resmi shadcn belgeleri/codec/registry okuması, preset decode ve token/source authoring yapıldı. Python urllib shadcn TLS sertifikasını doğrulayamadığında doğrulama kapatılmadı; aynı registry yanıtı normal sertifika denetimli PowerShell Invoke-WebRequest ile alındı. Repo paket/lock sürümleri değiştirilmedi, dependency kurulmadı.

**Test/lint/typecheck/build/Playwright/gerçek kontrast/AT/browser ve bağımsız QA/CI DEFERRED / NOT RUN.** Yazılan kodun derlendiği veya testlerin geçtiği söylenmez. Önceki S1 kanıtları kendi exact hedefinde korunur; bu değişikliğin kanıtı değildir. Commit/push/PR/merge/deployment yapılmadı.

Açık işler: tam domain CSS sabit renk ve ECharts seri/tooltip/label token geçişi; bütün mod/paletlerde 4.5:1 metin ve 3:1 anlamlı component/focus ölçümü; portal/dialog/drawer/320px davranışı; CSP ve disconnected first-paint gerçek browser kanıtı; Oxanium/Geist/Phosphor/Sera tam stil geçişinin ayrı değerlendirmesi. Paket tam preset/component migration veya tüm ekran temalandırma kabulü değildir.

Mevcut aşama: ortak tema altyapısı ve test kaynakları yazıldı. Sonraki adım: S2 UI ve domain ekranları geliştirilirken aynı tokenları kullanmak, kalan sabit renkleri kapsam bazında taşımak; ertelenmiş birleşik kapıda gerçek doğrulama ve bağımsız hedef kabulü. [Ana plan](../tasks/analysis-20261115-01.md), [proje durumu](../project-status.json).


Kaynak bakım kontrolü: tracked diff için git diff --check exit 0. Graphify AST-only update exit 0: 15.203 node / 27.362 edge / 1.018 community; semantic belge incelemesi yapılmadı. Araç, 730 desteklenmeyen/boş AST kaynak kaydı ve 5.000 node sınırı nedeniyle HTML görselleştirmesinin üretilmediğini bildirdi; bunlar gizlenmedi ve test PASS sayılmadı. Yerel geliştirme operasyonları /agents ayrı sabit koyu çalışma görünümünü korur; root tema değişiminin bu ekrandaki başlık/native-control okunabilirliğini bozmasını önleyen açık stil eklendi.

## 21.11.2026 tamamlama paketi — THEME-TOKEN-SWEEP-01 / THEME-CHART-01 / radius

Durum: **AUTHORED_UNVERIFIED** (yerel komutlar çalıştı; bağımsız QA ACCEPT-WITH-NOTES notları bu pakette düzeltildi; gerçek browser kapıları **DEFERRED**). Dal `feature/theme-complete-20261121`.

- Renk taraması: `design-system.css` dışında 32 CSS dosyası değişti — 17'sinde ~220 sabit renk (hex/rgb/`white`/`var(--x, #fallback)`) semantic tokenlara taşındı, 15'i yalnız radius tokenlaması. Yeni tokenlar: `--shadow-color`, `--selection-surface`, `--selection-border`, `--radius-delta`, `--font-sans/heading/mono`.
- Radius: 121 adet 6–14px `border-radius` → `max(0px, calc(Npx + var(--radius-delta)))`; preset radius'ta piksel değişmez, builder radius'u tüm yüzeylere ulaşır.
- Grafik: `frontend/src/theme/chart-theme.ts` tek yardımcı; ECharts seri/tooltip/kenar renkleri render anında tokenlardan okunur, mod/palet değişiminde yeniden boyanır, çözülemeyen token fail-closed (metin legend kalır).
- Guard: `frontend/src/theme/hardcoded-colors.test.ts` (3 dosyalık allowlist: design-system.css, theme/theme-tokens.ts, theme/theme-builder.css); çok satırlı CSS bildirimlerini ve custom property içindeki named renkleri (`--x: red`) de yakalar.
- QA notları: Today şerit/rozet anlamı hizalandı (Attention ve Review warning, diğerleri neutral); `.ops-ready` rozet kontrastı ≥4.5:1.
- **Kural:** iş/emniyet durumu (READY/UNKNOWN/REVIEW_REQUIRED/blocked vb.) asla `--primary`'ye eşlenmez; primary palet/marka rengidir (emerald'da yeşil). Durumlar yalnız korumalı warning/danger/info/muted tokenlarını kullanır; success/yeşil durum tonu yoktur.
- Preset font/ikon/stil: Oxanium/Geist/Phosphor/Sera yalnız isim olarak kayıtlı; font dosyası/lisans yok, uygulanmadı (açık takip).

Yerel sonuçlar (Node 22.15; CI Node 26):
- `npm run typecheck`: exit 0. `npm run lint`: exit 0.
- `npx vitest run` (8e270264 üzerinde, tam suite): 224 dosya, 2178 geçti / 10 kaldı — 9 `scripts/offline-shell-build.test.ts` (`SubtleCrypto.digest` ArrayBuffer realm hatası, yerel Node 22 ortamı; bu paket offline/public dosyalarına dokunmaz) ve 1 `DataReconciliationWorkspace.test.tsx` tam yük altında 5s timeout (tek başına 7/7 geçti).
- QA düzeltmesi sonrası hedefli: `npx vitest run src/agents src/theme src/product/TodayPriorityTone.test.ts src/product/TodayWorkspace.test.tsx src/product/ScopedDistribution.test.tsx src/product/DirectoryOverview.test.tsx` → 14 dosya, 161/161 geçti.
- CI 44902437779 (8e270264, push): success.

DEFERRED: her mod × palet × builder kombinasyonunda gerçek browser görsel/kontrast ölçümü (grafik serileri dahil), 320px/klavye/AT, 196 ekran tema kabulü, preset font/ikon/stil geçişi.
