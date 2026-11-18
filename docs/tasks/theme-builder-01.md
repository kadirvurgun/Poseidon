# THEME-BUILDER-01 — Uygulama içi tema oluşturucu

Durum: **AUTHORED_LOCALLY_CHECKED_NOT_ACCEPTED**, 18.11.2026. Kullanıcı, yalnız sabit paletler yerine `https://ui.shadcn.com/create` benzeri uygulama içi bir tema oluşturucu istedi. THEME-FOUNDATION-01'in ([görev](theme-foundation-01.md)) türetilmiş devamıdır; yeni canonical FR/TC veya accepted SP değildir.

Kaynaklar: kullanıcı isteği (18.11.2026); [preset b3Zheoix4U exact registry yanıtı](../sources/ui/shadcn-b3Zheoix4U.json); shadcn/ui tema belgeleri (Tailwind v4 oklch palet değerleri, MIT); THEME-FOUNDATION-01 kaynak locator'ları (13.04 Design System paragraf114/129, 65.03 paragraf257–260/572 kontrast). Bu oturumda web erişimi kullanılmadı; renk değerleri Tailwind v4 paletinden alındı ve kod içi oklch→sRGB dönüşümüyle bilinen hex karşılıklarına karşı sınandı (ör. emerald-700 → `#007a55`).

Owning role: frontend/UI. Dal: `feature/theme-builder-20261118`. Push/PR yok.

| İz | Kabul kriteri | Kaynak / kanıt |
| --- | --- | --- |
| THEME-BUILDER-A11Y | Görünüm panelindeki "Özelleştir… / Customize…" düğmesi modal yan paneli açar (`role=dialog`, `aria-modal`, başlık odaklı); Tab panel içinde döner; Escape/Vazgeç/arka plan kapatır, odak düğmeye döner; 44px hedefler, native radio/select | `ThemeBuilder.tsx`; `ThemeBuilder.test.tsx` A11Y |
| THEME-BUILDER-CONTROLS | Taban (stone/zinc/neutral/slate/gray), vurgu (emerald/teal/cyan/sky/blue/indigo/violet/rose/orange/amber), grafik (vurguyu izle veya renk), radius (0/0.3/0.5/0.625/0.75/1 rem), mod (sistem/açık/koyu) | `theme-tokens.ts` kapalı listeler; LIVE/PRESET testleri |
| THEME-BUILDER-LIVE | Seçim, kaydedilmeden tüm uygulama kromunda canlı önizlenir (root `data-*` öznitelikleri + üretilmiş CSS); Vazgeç/Escape önizlemeyi geri alır; Uygula kalıcılaştırır | `previewAppearance`; LIVE testi |
| THEME-BUILDER-PRESET | Zümrüt/Okyanus/Grafit ve b3Zheoix4U hazır temaları; "Varsayılana dön" renk/radius seçimini varsayılan Zümrüt'e döndürür (mod korunur) | PRESET testi |
| THEME-BUILDER-PERSIST | `poseidon.appearance.v2` kapalı JSON `{v:2, mode, palette, custom:{base,accent,chart,radius}}`; v1 bir kez taşınır ve silinir; bozuk/gelecek sürüm/bilinmeyen değer alan bazında güvenli varsayılana düşer; sekmeler arası eşitleme; PII/credential yok | `appearance-store.ts`; PERSIST testleri + mevcut THEME-FOUNDATION testleri |
| THEME-BUILDER-PAINT | `appearance-init.js` React öncesi v2 (yoksa v1) okur, yalnız allow-list adlarını `data-*` olarak yazar; ağ yok, senkron | PAINT testleri; offline shell allowlist değişmedi |
| THEME-BUILDER-CSS | Token değerleri tek kaynaktan (`theme-tokens.ts`) üretilen `theme-builder.css` içinde ve yalnız `data-palette="custom"` altında; dosya üretici çıktısıyla birebir aynı | CSS eşitlik testi |
| THEME-BUILDER-EXPORT | Üretilen CSS değişkenleri (`:root` / `.dark`) ve tema JSON'u gösterilir, kopyalanabilir | EXPORT testleri |
| THEME-BUILDER-IMPORT | Yapıştırılan JSON yalnız tam anahtar kümesi, `version:1`, kapalı değerler ve ≤1024 karakterle kabul edilir; diğer her şey reddedilir, önizleme değişmez | 11 red + kabul testi, UI red testi |
| THEME-BUILDER-CONTRAST | oklch→sRGB→WCAG hesabı; primary-foreground/primary, foreground/background ve diğer 7 metin çifti iki modda ≥4.5:1; vurgu ön planı gerekirse otomatik seçilir ve kullanıcıya gösterilir; başarısız/hesaplanamayan çift Uygula'yı engeller | `contrastReport`; tüm 50 kombinasyon + 3 hazır tema testi; UI engel testi |
| THEME-BUILDER-SAFETY | Builder warning/danger/info/destructive/focus tokenlarını asla üretmez; durum renkleri sabit kalır; rose/orange/amber için "durum rengine benzer" notu; tema değişimi formu/draftı veya REVIEW_REQUIRED durumunu değiştirmez | SAFETY testi; LIVE testinde draft/status korunur |

Kararlar ve sapmalar:
- Tokenlar inline style yerine öznitelik kapsamlı üretilmiş CSS ile uygulanır; böylece ilk boyama betiği küçük kalır ve depodan DOM'a yalnız allow-list adlar geçer (değer enjeksiyonu yok).
- Kontrast koruması varsayılan Zümrüt'te açık mod `--sidebar-primary` (emerald-600 / emerald-50 = 3.48:1) değerini emerald-700'e yükseltti; preset kaynağından bilinçli sapmadır.
- Mavi/menekşe/gül/indigo koyu modda `--sidebar-primary-foreground` otomatik siyah/beyaza geçer (gösterilir).
- Yeni bağımlılık yok; dialog native element/Radix olmadan erişilebilir özel modal olarak yazıldı. Yalnız TR/EN metin; diğer diller İngilizceye düşer (mevcut AppearanceControls kuralı).
- Tercih hâlâ yalnız tarayıcı profiline ait kozmetik ayardır; sunucuya yazılmaz, audit/tenant kapsamı yoktur.

Açık: gerçek tarayıcıda görsel/AT/320px ve ilk boyama flash ölçümü, domain CSS sabit renkleri ve ECharts serilerinin chart tokenlarını tüketmesi (THEME-FOUNDATION-01'den devralınan), bağımsız QA ve CI.

[Faz raporu](../phases/THEME-BUILDER-01.md) · [Tema temeli](theme-foundation-01.md)
