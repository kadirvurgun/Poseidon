# THEME-BUILDER-01 — Uygulama içi tema oluşturucu

18.11.2026. **AUTHORED + yerel otomatik kontroller çalıştırıldı; ACCEPTED DEĞİL, yeni kabul puanı 0.** Kullanıcı isteğiyle THEME-FOUNDATION-01 üzerine eklendi. Tam sprint/release kapısı, bağımsız QA veya CI değildir. [Görev ve kabul kriterleri](../tasks/theme-builder-01.md).

## Yazılan davranış

- Görünüm panelinde "Özelleştir… / Customize…" düğmesi; sağ yan panel modal tema oluşturucu (TR/EN, diğer diller İngilizce): hazır temalar (Zümrüt, Okyanus, Grafit, b3Zheoix4U), mod, 5 taban, 10 vurgu, grafik rengi (vurguyu izle/seç), 6 radius, önizleme (birincil/ikincil düğme, kart, sabit uyarı/tehlike/bilgi rozetleri, grafik çubukları), kontrast raporu, CSS/JSON dışa aktarma + kopyalama, sıkı JSON içe aktarma, Varsayılana dön / Vazgeç / Uygula.
- Seçim tüm uygulama kromunda canlı önizlenir; Vazgeç/Escape/arka plan geri alır, Uygula kaydeder. Odak başlığa gider, Tab panelde döner, kapatınca "Özelleştir…" düğmesine döner.
- `frontend/src/theme/theme-tokens.ts` tek kaynak: Tailwind v4 oklch değerleri, oklch→sRGB→WCAG kontrast, otomatik ön plan seçimi, kontrast raporu (iki modda 9 metin çifti, başarısız/hesaplanamayan çift Uygula'yı engeller), export/import, üretilmiş `theme-builder.css` (yalnız `data-palette="custom"` kapsamı; semantik durum tokenları hiç üretilmez).
- Saklama `poseidon.appearance.v2` (`{v:2, mode, palette, custom}`); v1 bir kez taşınıp silinir; bozuk/gelecek sürüm güvenli varsayılan; sekmeler arası eşitleme; `appearance-init.js` React öncesi yalnız allow-list adları `data-*` olarak uygular (ağ yok).
- Kontrast sapması: varsayılan açık mod `--sidebar-primary` 3.48:1 ölçüldü, emerald-700'e yükseltildi (`design-system.css`).

## Gerçek doğrulama (bu worktree, commit öncesi çalışma ağacı)

| Komut | Sonuç |
| --- | --- |
| `npx vitest run src/theme scripts/offline-shell-build.test.ts` | exit 0 — 5 dosya, 52 test geçti |
| `npm run lint --prefix frontend` | exit 0 |
| `npm run typecheck --prefix frontend` | exit 0 |
| `npm test --prefix frontend` (tam birim suite) | exit 0 — 160 dosya, 1705 test geçti (344.5 s) |
| `npm run build --prefix frontend` | exit 0 (mevcut >500 kB chunk ve i18n INEFFECTIVE_DYNAMIC_IMPORT uyarıları önceden vardı) |

Yeni testler: `theme-tokens.test.ts` (CSS eşitliği, renk matematiği, 50 kombinasyon + 3 hazır tema kontrastı, otomatik ön plan, fail-closed engel, semantik token koruması, export round-trip, 11 import reddi, radius kabulü), `ThemeBuilder.test.tsx` (klavye/odak, canlı önizleme + geri alma + kalıcılaştırma + draft korunması, hazır temalar/sıfırlama, kopyalama/import red/kabul, kontrast gösterimi, v1→v2 göçü, bozuk değer, cross-tab, ilk boyama betiği), `ThemeBuilder.contrast.test.tsx` (zorlanmış başarısız raporda Uygula engeli). Mevcut THEME-FOUNDATION testleri v2 şemaya uyarlandı. Offline shell testi sentetik bayt kullandığından değişmedi; gerçek `appearance-init.js` baytı build envanterinde otomatik yer alır.

**Çalıştırılmadı / DEFERRED:** gerçek tarayıcı görsel/ilk boyama flash ölçümü, Playwright, ekran okuyucu/AT, 320px, domain CSS sabit renk ve ECharts token tüketimi, bağımsız QA, CI. Push/PR/merge yapılmadı.

Mevcut aşama: tema oluşturucu yazıldı ve yerel birim/lint/typecheck/build kontrolleri geçti. Sonraki adım: entegrasyon dalına alma, gerçek tarayıcı/kontrast/AT kapısı ve bağımsız QA.
