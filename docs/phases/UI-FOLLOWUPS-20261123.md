# UI-FOLLOWUPS-20261123 — yardım görüntülerinde görülen N6 arayüz kusurları ve şiddet politikası dürüstlüğü

Durum: **AUTHORED_UNVERIFIED → yerel kontroller geçti, CI kanıtı aşağıda** (23.11.2026, dal `fix/ui-followups-20261123`, taban main 494045ee). Türetilmiş takip işi (HELP-MANUAL-DOWNLOAD-01 QA notu N6 ve "Görüntülerde görülen ürün kusurları" maddesi); canonical iş/SP değildir (114/121 iş, 932/970 SP değişmez). Bağımsız QA bekliyor. Kılavuz içeriği `reviewed:false` kalır.

Kaynak: `docs/phases/HELP-MANUAL-DOWNLOAD-01.md` açık konular; QA sonucu N6 (`manual-download-qa.result.md`); AGENTS.md "UNKNOWN must not be silently converted to No, Ready or NotApplicable".

## Düzeltilen kusurlar

1. **Dil seçici küre simgesi metnin üstüne biniyordu** (tüm görünümler). `.enterprise-shell select` / `.tug-workspace select` (aynı 0,1,1 özgüllük, sonra yüklenen) iç başlangıç boşluğunu sıfırlıyordu. `LanguageSelector.css` artık `.language-selector > .language-selector-control > select` (0,2,1) zinciriyle simge için 2rem boşluğu korur.
2. **Mobilde uzun tanılama kodları ve iş numaraları taşıyordu.** `enterprise.css`: `.enterprise-shell main` içindeki `code/kbd/samp` ve bunları taşıyan `li/p` öğeleri `overflow-wrap: anywhere` ile kırılır. Kalkış "Neden" kodları `departure-reason-code` sınıfı ve tam değeri taşıyan `title` alır. `tug-board.css`: iş kartı kimlik sütunu `min-inline-size: 0`, başlık `overflow-wrap: anywhere`; iş numarası bağlantısı tam numarayı `title` olarak taşır. Değer her zaman tam görünür kalır (kırpılmaz).
3. **Commit damgası 390 px'de iki satıra kırılıyordu.** `screenshots/steps.ts` `commitStamp(commit, width)`: 480 px altı görünümde kısa biçim `POSEIDON · <12 karakter commit> · synthetic · UG-2`; masaüstü biçimi değişmedi. `capture.ts` altbilgisi tek satır (`white-space: nowrap`, taşma koruması olarak ellipsis).
4. **Masaüstü safety-events "Yalnızca benim bildirimlerim" onay kutusu geniş bir kutu içinde etiketin üstündeydi.** `tug.css` metin alanı kuralı onay kutusu/radyo düğmelerini artık kapsamaz (`:where(:not([type=checkbox],[type=radio]))`); seçim girdileri 18 px (dokunmatik/dar görünümde 24 px), etiket yatay ve 40/44 px dokunma hedefidir.
5. **Dürüstlük:** sunucu `SeverityPolicyUnconfigured` kapsamı ile `NO_OPEN_HIGH_SEVERITY_EVENT` döndürdüğünde sayfa "Açık yüksek şiddetli olay yok" diyordu. `SafetyEventsWorkspace.blockerDisplayState` bu durumda `POLICY_UNCONFIGURED` (uyarı tonu) gösterir: "Şiddet politikası yapılandırılmadı – inceleme gerekli" ve yüksek şiddetli olayların dışlanamadığını açıklayan not. Engelleyici varken sunucu durumu korunur ve not yine gösterilir; politika yapılandırılınca düz durum döner. Backend yanıtı değişmedi (kapsam kodu zaten mevcuttu). 17 katalogda `b_POLICY_UNCONFIGURED` + `policyUnconfigured`; 16 dildeki safety-events kılavuzuna aynı cümle eklendi (ön madde ve yapı aynı, `reviewed:false`).

## Testler

- `SafetyEventsWorkspace.test.tsx`: politika yapılandırılmamışken "sorun yok" asla görünmez (EN + TR), engelleyici varken not korunur, yapılandırılmışken düz durum.
- `n6-layout-css.test.ts`: dil seçici seçici zinciri, onay kutusu/radyo kuralları (masaüstü + mobil), kod kırma, rozet kırma, iş numarası kırma (jsdom kaskad uygulamaz; tarayıcı görünümü yeniden üretilen görüntülerle denetlenir).
- `DepartureReadinessWorkspace.test.tsx`: neden kodu `title` + sınıf. `TugBoardView.test.tsx`: uzun iş numarası `title`.
- `screenshots/steps.test.ts`: `commitStamp` masaüstü/mobil biçimleri ve geçersiz commit reddi.

## Doğrulama

Bkz. teslim raporundaki sonuç tablosu (aşağıda güncellenir).

## Açık / sonraki adım

- **Bulgu (kapsam dışı, düzeltilmedi):** `DepartureReadinessEngine.SafetyChecks` (`backend/Poseidon.Domain/DepartureReadinessEngine.cs`) `NO_OPEN_HIGH_SEVERITY_EVENT` girdisini `SeverityPolicyUnconfigured` kapsamına bakmadan **PASS** sayar; kalkış görüntüsünde "Emniyet olayları: Geçti" görünür. Aynı dürüstlük sınıfından bir sorundur; kalkış kapısı semantiğini değiştirdiği için ayrı bir pakette (domain + PG entegrasyon testleriyle) ele alınmalıdır.
- Bağımsız QA; UG-6 / UG-REV-01 insan SME/UX onayı.
