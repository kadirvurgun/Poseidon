# USER-GUIDE-01 / UG-1 — sayfa içi yardım paneli altyapısı

Durum: **AUTHORED_UNVERIFIED** (21.11.2026). Yerel typecheck, lint, tam frontend vitest ve offline-shell inventory testi çalıştırıldı (sonuçlar aşağıda). CI ve bağımsız QA bekliyor. Playwright (axe, 320 px, offline PWA) bu fazda yerelde çalıştırılmadı: **DEFERRED**. İçerik `reviewed:false`; insan SME/UX onayı (UG-REV-01 / UG-6) AI tarafından verilemez ve verilmedi.

Dal: `feature/help-panel-20261121` (taban `origin/main` 4d3b75c4 + `origin/docs/user-guide-analysis-20261121`).
Türetilmiş ek gereksinim; canonical iş/SP sayımı (121 iş / 970 SP) değişmez.

## Kaynaklar

| Locator | Kullanım |
|---|---|
| `docs/tasks/USER-GUIDE-01.md` §3–6 | Kapsam, içerik modeli, D1–D8 kararları, UG-HELP-01…05 |
| `son analizler 15.11.26/65.03_…Tam_Analiz_TR(1).docx` p46–64 | p57–59: Neden ve AI ayrı sekme; panel açılması komut/AI çağrısı başlatmaz; mobilde tam sayfa. p50: gizli menü adı/sayısı sızmaz. p52: sayfa başlığı ana soruyu söyler (bölüm 1). |
| `04_UX_PRODUCT_DESIGN/13.03_…Rol_Bazli_Kullanici_El_Kitabi_TR.docx` §1, §7, §10 (+ §2, §3, §5, §6, §9 rol satırları) | Ortak kullanım modeli, kıyı operasyonları/Tug Master yetki sınırı, hata/yardım akışı tablosu |
| Kod: `TodayWorkspace.tsx`, `TugBoardView.tsx`/`TugWorkspace.tsx`, `ProductApp.tsx` + `DirectoryEditor` kopyaları | Kılavuzdaki düğme/durum adları ürünün gerçek TR/EN etiketlerinden alındı |

## Teslim edilen davranış

- **`?` düğmesi** her enterprise sayfa başlığının yanında (`EnterpriseChrome` breadcrumb, 44 px hedef, `aria-keyshortcuts="Shift+?"`, `aria-haspopup="dialog"`). **Shift+?** paneli açar; odak `input/textarea/select/contenteditable` içindeyken veya başka bir modal açıkken açmaz.
- **Panel** (`frontend/src/help/HelpPanel.tsx`): `role="dialog"`, `aria-modal`, başlık etiketli; arka plan `inert`, Tab/Shift+Tab tuzağı (`components/a11y/modal.ts`), Escape kapatır, odak `?` düğmesine döner. Masaüstünde sağ yan panel (`help-panel--side`), ≤767 px tam sayfa (`help-panel--page`, ayrıca CSS media query). Sekmeler `role="tablist"`, ok/Home/End tuşları.
  - **Kılavuz:** paketlenmiş Markdown. `reviewed:false` → "Taslak kılavuz — uzman onayı bekliyor". İçerik yoksa nötr "Bu sayfanın kılavuzu hazırlanıyor."
  - **Neden:** panel Why yükünü **okumaz** (o okuma sahip ekranın isteğidir). Neden görünümü olan sayfalarda (`today`, `tug-job-board`) kullanıcıyı kaydın Neden bağlantısına yönlendirir; diğerlerinde nötr boş durum.
  - **AI:** kapalı yer tutucu; hiçbir çağrı yok.
- **İlgili sayfalar** (D3): ön bilgideki `related` anahtarları kullanıcının sunucu navigasyonu (`navigation.destinations`) ile kesiştirilir; görünmeyen sayfa adı, sayısı veya yer tutucusu gösterilmez. Gövdedeki `/app/...` bağlantıları da yalnız izinli rotalarda bağlantı olur, aksi halde düz metin.
- **Dil:** TR ve EN içerik; diğer 15 dilde EN gösterilir ve "Bu dilde kılavuz henüz yok; İngilizce sürüm gösteriliyor." notu çıkar (`lang="en"`). Panel arayüz metinleri 17 katalogda `copies.HelpPanel_tsx_copy` (16 anahtar).

## Tasarım kararları

1. **Markdown kütüphanesi eklenmedi.** Bağımlılıklarda Markdown yoktu. `HelpMarkdown.tsx` sabit bir alt kümeyi (paragraf, `-`/`1.` liste, `###`, `**kalın**`, `` `kod` ``, `[metin](href)`) doğrudan React öğelerine çevirir. HTML yolu yoktur: `dangerouslySetInnerHTML` yok, ham HTML metin olarak kaçışlanır, `img/script/iframe` üretilmez; bağlantı yalnız izinli `/app/<rota>` veya `#çapa`. `package.json`/lockfile değişmedi.
2. **İçerik modeli:** `frontend/src/help/content/<dil>/<sayfa-anahtarı>.md`; kapalı ön bilgi (`key, title, roles, permissions, related, screenshots, reviewed`) ve 8 sabit başlık `help-model.ts` içinde katı ayrıştırılır (eksik/fazla alan, `reviewed: true`, kendine ilgili, sıra/dil hatası reddedilir). `reviewed` yalnız `false` veya insan onayı `{ by, date }` olabilir.
3. **Tembel yükleme ve offline:** her dil klasöründe `help-<dil>.ts` o dilin tüm `.md` dosyalarını `?raw` ile tek modüle toplar; `help-content.ts` bunları `import.meta.glob` ile dil başına tembel yükler ve önbelleğe alır (hata önbelleğe alınmaz). Sonuç: dil başına **tek** chunk (`assets/help-tr-*.js`, `assets/help-en-*.js`). Mevcut `offlineShellBuild` envanteri tüm `assets/*.js` dosyalarını hash'leriyle alır; ek araç değişikliği gerekmedi. Sayfa başına chunk 53×17 dosyada 128 girişlik envanter sınırını aşardı; dil başına chunk bunu önler. Chrome açılışında geçerli dilin chunk'ı önceden yüklenir; panel açılması API isteği, komut veya AI çağrısı yapmaz (yalnız aynı kökenli, offline shell tarafından önbelleğe alınmış statik asset).
4. **Kapsam koruması:** `help-pending.ts` 50 anahtarlık açık bekleme listesi (53 − today/tug-job-board/companies), taban `pendingHelpBaseline = 50`. Liste yalnız küçülür: bekleyen bir anahtar için içerik yazılırsa test kırılır ve anahtarın listeden çıkarılmasını ister; liste tabanı aşamaz. **Yeni navigasyon hedefi eklenirse aynı değişiklikte TR+EN kılavuzu yazılmalıdır** (UG-HELP-03).
5. **Navigasyon sözleşmesi:** `enterprise-navigation.ts` sonuna yalnız `enterpriseDestinationKeys` dışa aktarımı eklendi (paralel navigasyon değişiklikleriyle çakışmayı azaltmak için kayıt bloğuna dokunulmadı).

## Dosyalar

- Yeni: `frontend/src/help/{help-model.ts, help-content.ts, help-pending.ts, HelpMarkdown.tsx, HelpPanel.tsx, help-panel.css}`, `frontend/src/help/content/{tr,en}/{help-tr|help-en.ts, today.md, tug-job-board.md, companies.md}`
- Testler: `frontend/src/help/{help-coverage.test.ts, HelpPanel.test.tsx, HelpMarkdown.test.tsx}`, `frontend/scripts/offline-shell-build.test.ts` (+1 vaka)
- Değişen: `frontend/src/product/EnterpriseChrome.tsx`, `frontend/src/product/enterprise-navigation.ts`, `frontend/src/i18n/messages/*.json` (17)

## Doğrulama (yerel, Node 26.13.0)

Sonuçlar teslim raporunda ve aşağıdaki tabloda; CI sonucu ayrıca raporlanır.

| Komut | Sonuç |
|---|---|
| `npm ci --prefix frontend` | 280 paket, hata yok |
| `npm run typecheck` | 0 hata |
| `npm run lint` | 0 hata / 0 uyarı |
| `npx vitest run src/help` | 3 dosya, 79 test geçti |
| `npx vitest run` (tam frontend, offline-shell testi dahil) | bd906386: 229 dosya / 2273 test geçti. QA düzeltmesi sonrası: 229 dosya / 2276 test geçti, 0 başarısız |
| `npx vite build` | geçti; `help-tr` 11,7 kB, `help-en` 10,7 kB chunk; ikisi de `dist/offline-shell.js` envanterinde |
| `py scripts/test_i18n_untranslated.py` | 4 test geçti; `HelpPanel_tsx_copy` için yalnız fr "Guide" aynı (Fransızcada doğru kelime) |
| Playwright / axe / 320 px / offline PWA | **DEFERRED** (yerelde çalıştırılmadı) |

## Bağımsız QA (bd906386: ACCEPT-WITH-NOTES) ve düzeltmeler

- **N1:** Vitest'in disk modül önbelleği `import.meta.glob` sonucunu bayat tutabiliyordu. `frontend/vite.config.ts` içinde `test.fsModuleCache: false` yapıldı. Vitest 5.0.5'te bu seçenek `experimental` altında değil, üst düzeyde; typecheck bunu doğruladı. Önbellek bayrağı olmadan deneme yapıldı: geçen koşu → `content/en/fleet.md` (`<script>`) eklendi → koruma 3 vakada kırmızı → dosya silindi → yeşil.
- **N3:** Kılavuzlardaki durum metinleri UI dizeleriyle birebir aynı yapıldı. EN: "The outcome could not be confirmed", "Every listed source was read and returned no items", "The server response failed validation". TR karşılıkları: "İşlem sonucu doğrulanamadı", "Listelenen tüm kaynaklar okundu ve öğe dönmedi", "Sunucu yanıtı doğrulamadan geçemedi".
- **N4:** UG-REV-01 insan onay akışı gelene kadar her kılavuzun `reviewed: false` olduğunu doğrulayan koruma testi eklendi. İnsan onay akışı bu testi açıkça değiştirecek.
- **N2:** İki test eklendi: Shift+? contentEditable içinde, başka bir modal açıkken ve Ctrl ile birlikte yok sayılır. Panel açılıp sekmeler gezildiğinde XHR veya `sendBeacon` çağrısı olmaz.

## Açık işler

- UG-2 ekran görüntüsü hattı (`screenshots` alanları boş).
- UG-3: kalan 50 sayfanın TR/EN içeriği; UG-5 diğer diller; UG-6 insan SME/UX onayı (AI kapatamaz).
- UG-HELP-01'in 53 sayfada Playwright/axe doğrulaması ve UG-DOC-04 offline PWA testi.
- Kısıtlı kullanıcıyla (lab-restricted) Playwright UG-HELP-04 doğrulaması.
