# THEME-FOUNDATION-01 — Başlangıçtan değiştirilebilir uygulama temaları

Durum: **AUTHORED_UNVERIFIED_NOT_ACCEPTED**, 17.11.2026. Kullanıcı 15.11.26 planına ek olarak `https://ui.shadcn.com/create?preset=b3Zheoix4U` tasarım girdisini ve uygulama içinde değiştirilebilir temaları açıkça istedi. Bu, türetilmiş ek gereksinim kimliğidir; yeni canonical FR/TC veya accepted SP değildir.

Kaynaklar: [preset ve exact registry yanıtı](../sources/ui/shadcn-b3Zheoix4U.json); 13.04 Design System SHA256 `83489a0f0752e22410d7534cdbe0b89726f00012d869e1b868e8127f5bcc573c`, paragraf114/129 klavye/label/contrast; 65.03 paragraf20 hesap tercihi theme, paragraf257–260 üç modda kontrast, paragraf503 gerçek hesap/focus incelemesi ve paragraf572 dark warning metin4.5:1/component3:1. 65.03 exact kaynak SHA/paragraf manifesti [yeni kaynak ekinde](../sources/2026-11-15/planning-manifest.json). Sayısal kontrast gereksinimleri gerçek ölçümle doğrulanacak.

Owning role: frontend/UI. Dalga 1 ortak altyapı; PLAN-1511-6503 / PLAN-1511-5202, S2 DEV-021/022/025 ile ilişkili. Geçerli çalışma dalı/worktree kullanılır; otomatik push/PR yok. Gerekli yayın kabulü: daha sonraki bütünleşik doğrulama, bağımsız exact-target QA ve gereken CI.

| İz | Kabul kriteri | Yazılan kaynak / gereken kanıt |
| --- | --- | --- |
| THEME-MODE-01 | Sistem/açık/koyu seçilebilir; OS değişimi yalnız sistem modunda uygulanır | appearance-store + component davranış testi; gerçek browser deferred |
| THEME-PALETTE-01 | Zümrüt/Okyanus/Grafit seçimi ortak tokenları değiştirir; renk modu bağımsızdır | Root data-palette + shadcn CSS variables; bütün kombinasyonlarda görsel/kontrast deferred |
| THEME-PERSIST-01 | Tercih yenilemede korunur; bozuk kayıt güvenli varsayılana düşer; storage engeli sekmedeki değişimi engellemez | Closed JSON, localStorage exception fallback, restore/cross-tab/corrupt-data testleri |
| THEME-PAINT-01 | İlk boyamada kayıtlı mod/token seçimi uygulanır; tema için ağ çağrısı yapılmaz | HTML head external same-origin bootstrap; VM ilk-boyama sözleşme testi; gerçek flash ölçümü deferred |
| THEME-OFFLINE-01 | Bootstrap exact byte/hash ile public offline envanterindedir; özel veriler cache'e eklenmez | offline-shell-build explicit allowlist; offline response/integrity/version testi |
| THEME-A11Y-01 | TR/EN etiketler, native klavye kontrollü seçim, Escape ile kapatma ve odak dönüşü; 44px target | AppearanceControls ve davranış testi; gerçek keyboard/AT/320px gate deferred |
| THEME-SAFETY-01 | Görünüm değişimi draftı, URL/scope/role/izinleri ve REVIEW_REQUIRED/UNKNOWN durumunu değiştirmez; warning/danger/info renk anlamı korunur | Uygulama remount/reload yok; draft/status davranış testi; badge semantic tokenları; tam negatif/browser/kontrast gate deferred |
| THEME-PRESET-01 | Kullanıcının exact preseti ve uygulanan kapsam kaynak kayıtlıdır | Registry theme/radius snapshot; font/icon/style tam geçişi açık takip |

Tercih şu anda yalnız tarayıcı profiline ait kozmetik seçimdir; tenant/account/person kaydı değildir. Anahtar `poseidon.appearance.v1`, kapalı değerler `{mode: light|dark|system, palette: emerald|ocean|graphite}`; credential/PII/auth/scope içermez. Tema değiştirmek server yetkisi veya audited business mutation üretmez. 52.02 hesabına sunucu tarafı tercih kaydı sonradan bağlanırsa kendi yetki/ETag/audit sözleşmesiyle yapılacak; bu paketle yapılmış sayılmaz.

Preset decode: Sera, Stone, Emerald, Cyan chart, Oxanium body, Geist heading, Phosphor icons, default radius, subtle/translucent menu. Bu pakette **renk/radius tokenları** uygulanır; mevcut Radix/Lucide/system-font bileşenleri korunur. Component style/base, icon ve font ailesini değiştirmek ayrı değerlendirme/migration, yerel font lisansı/barındırma ve kontrast/erişilebilirlik/regression kapısı ister. Yeni npm bağımlılığı veya remote font/registry runtime isteği eklenmez. Ocean/Graphite uygulamanın kendi ek paletleridir.

Mevcut ortak enterprise shell, temel form/tablo/surface ve bağlantı ekranının ana renkleri tokenlara geçirildi. Domain CSS'lerinde ve ECharts çizim seçeneklerinde kalan sabit renkler için tüm ürün taraması/görsel uyum işi açıktır. Grafik palette tokenları tanımlı olmakla bütün ECharts serilerinin bu paleti tükettiği iddia edilmez. Bütün 196 ekranın tema kabulü yoktur.

Yazılan 6 component/store/boot davranış senaryosu ve 1 public offline bütünlük senaryosu **çalıştırılmadı**. Sonraki birleşik kapıda `npm run lint --prefix frontend`, `npm run typecheck --prefix frontend`, `npm test --prefix frontend`, `npm run build --prefix frontend`; desteklenen Ship/Tug/Crew/Technical/Today, portal/dialog, lost-result/offline ve tüm mod/paletlerde gerçek browser/contrast/keyboard kontrolleri gerekir. Backend business davranışı değişmez; gerekli genel CI/regression kapısı korunur.

[Faz raporu](../phases/THEME-FOUNDATION-01.md) · [Ana plana ek](analysis-20261115-01.md).
