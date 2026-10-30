# Responsive ve erişilebilirlik regresyonu — 27 Ekim 2026

Taban: `26c190ac3b86c1e4f3edc30b4553f2f5f6578d47` (origin/main).
Başlangıç çalışma ağacı temiz, HEAD `fc4c9858205a02c8ba3d41b5495d525a73bb4a69` idi.
Salt-okunur fetch ve fast-forward ile beklenen tabana geçildi; kullanıcı değişikliği yoktu.
Patch yalnız frontend ve bu belgeyi değiştirir. Backend/kök README için yeni değişiklik yoktur.
Önceki fetch receiver düzeltmesi ve testi aynen korunur.

## Kusur, tekrar üretim ve çözüm

- **WEB-01:** 390×844 EN gemi listesinde odaklı Open sağ kenarı x=370.17, kapsayıcı sağ kenarı x=369, scrollLeft=0: kesilme yeniden üretildi. Mevcut kısa TR kaydında sağ kenar 354 olduğundan başlangıçta kesilme oluşmadı; önceki raporun TR verisi bu ortamda aynı değildir. Eylemin focus olayında `scrollIntoView(nearest)` ve 6 px `scroll-margin`, düğmeyi 5 px odak çizgisi payıyla görünür kılar. Son EN gemi ölçümü: sağ=363.17, scrollLeft=7; sertifika sağ=363.11, scrollLeft=4. Büyük ekranlarda kaydırma=0.
- **WEB-02:** IMO `123` ile kaydet → EN: Türkçe hata kalıyordu. İstemci doğrulaması son gönderilen değerlerin anlık görüntüsünden seçili dilde render edilir. Kullanıcının güncel girdisi, alan adları ve doğrulama kuralları değişmez; önceki hata gösterme zamanı korunur. Yeni/açılan taslakta anlık görüntü temizlenir. Sunucunun metin tanıları ayrı tutulur ve çevrilmeye çalışılmaz. TR↔EN, gemi IMO, sertifika gemi seçimi/tarih sırası ve veri korunması doğrulandı.
- **WEB-03:** Yerleşik takvim simgesi koyu/siyah, `color-scheme: normal` idi; içeride görüntü incelenerek doğrulandı. Yalnız tarih girdilerine `color-scheme: dark` eklendi. İki tarih simgesi altı görüntüde açık renkte ve belirgin. Ölçülmüş kontrast oranı iddiası yoktur.

## Son sonuçlar

| Kontrol | Durum | Kanıt |
|---|---|---|
| Frontend birim/etkileşim testleri | PASS | 3 dosya, 35 test; fetch receiver, dirty navigation, retry, conflict, scope testleri dahil |
| Typecheck | PASS | `npm run typecheck` |
| Üretim build | PASS | `npm run build` |
| 390×844 TR | PASS | Gemi/sertifika listesi ve formu, klavye, hata dili, veriler, taşma |
| 390×844 EN | PASS | Aynı; taşan eylem focus ile görünür |
| 768×1024 TR | PASS | Aynı |
| 768×1024 EN | PASS | Aynı |
| 1440×900 TR | PASS | Aynı |
| 1440×900 EN | PASS | Aynı |
| Güncel açık regresyon | FAIL: yok | Bu dar kapsam içinde |
| Gerçek mobil donanım, touch/sanal klavye, ekran okuyucu, diğer tarayıcılar, tam WCAG | NOT_RUN | Viewport incelemesi cihaz testi/sertifikasyon değildir |
| Backend testleri / üretim kimlik kabulü | NOT_RUN | Ayrı görev ve kapsam |

Gerçek Chromium 154.0.8037.57, mevcut Playwright 1.57.0, Node 24.19.0 kullanıldı.
Özel yerel Vite 127.0.0.1:5173 ve Development API 127.0.0.1:5080, PostgreSQL readiness başarılı.
İlk matris denemesi sertifika listesinde kayıt bulunmadığından timeout oldu; sahte başarı sayılmadı.
Eksik liste fixture'ı için mevcut sentetik Tenant A oturumundan bir `QA synthetic certificate` taslağı kaydedildi.
Hesap/anahtar veya üretim kaydı oluşturulmadı. Tamamlanan altı matris koşusunda vessel/certificate POST sayısı **0**.

Matris Tab/Enter ile skip-link→main, Aç/Open→kayıt, yeni taslak, kaydet ve ilk hatalı alana odak geçişini denetler.
Düğme dış kenarına 5 px eklenerek kapsayıcı içinde kalması ve `:focus-visible` kontrol edilir.
Gemi adı→IMO Tab sırası, `aria-describedby`, kaydedilmemiş ve yeniden açılan değerlerin dil değişiminde korunması,
form/liste/auth sayfalarında belge yatay taşmaması ve tarih `color-scheme` denetlenir.
Takvim görüntüleri içeride gözle incelendi; Library teslimine eklenmedi.

## Tekrar çalıştırma

Mevcut özel Development servislerini ve Tenant A içinde en az bir gemi ve sertifika taslağını kullanın.
Framework/dependency eklenmedi; browser betiği ortamda zaten kurulu Playwright'ı kullanır:

```sh
cd src/Frontend
npm test
npm run typecheck
npm run build
PLAYWRIGHT_MODULE=/opt/codex/cua_node/lib/node_modules/playwright node tests/responsive.cjs
```

Gerekirse `CHROMIUM_PATH`, `POSEIDON_WEB_URL`, `QA_RESULT_FILE` ayarlanabilir.
`QA_SCREENSHOT_DIR` yalnız yerel iç inceleme içindir; mevcut bir dizin olmalıdır ve görüntü teslim yetkisi vermez.
Test betiği otomatik fixture oluşturmaz ve gerçek müşteri verisiyle kullanılmamalıdır.

Push, commit, merge-to-GitHub, deploy veya public preview yapılmadı. Engellenmiş/izin bekleyen/sonucu belirsiz işlem yoktur.
