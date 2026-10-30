# EVIDENCE_INTAKE_UAT.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/docs/EVIDENCE_INTAKE_UAT.md
SHA256: 61ef35169cb9c95dc06ceaddbb3b60335d18a2e1d662b71b8fe98e0b67586686

## lines:1-40

# Karantinalı ek alımı — sentetik UAT paketi

Taban: `f20a083b2031f988f63862f0dcd5eb6b758e2e15`. Tam teslim sürümü ve SHA-256 değerleri arşiv manifestindedir. Bu, Controlled Knowledge and Evidence modülünün tamamı veya kontrollü yayın/doğrulama değildir.

**Kullanıcı erişimi kurulmadı; Esra kabulü PENDING.** localhost, görev veya Library bağlantısı çalışan özel uygulama erişimi değildir. Üretim TLS/IdP, tarama sağlayıcısı ve gerçek dosyalar sınanmadı. Kullanıcıya açılacak güvenli özel erişim ayrı kurulum kapısıdır.

## Tekrar üretilebilir teknik kontrol

Mevcut SDK/NuGet/Node bağımlılıklarını ve yalnız izole sentetik PostgreSQL kullanın. Yeni altyapı kurmak veya gerçek dosya yüklemek gerekmez. Varsayılan frontend ve API dosya alımı kapalıdır.

```sh
# Var olan özel test bağlantısı ortamda tanımlı olmalıdır; sırları rapora yazmayın.
dotnet test tests/Backend/Poseidon.Backend.Tests.csproj --no-restore
npm --prefix src/Frontend test
npm --prefix src/Frontend run typecheck
npm --prefix src/Frontend run build
```

BFF tarayıcı fixture'ı için mevcut Chromium/Playwright kullanılır. HTTPS bu fixture'da TestServer–stdio köprüsünde sanaldır; native TLS veya gerçek sağlayıcı değildir.

```sh
VITE_ENABLE_BFF=true VITE_ENABLE_EVIDENCE_INTAKE=true npm --prefix src/Frontend run build -- --outDir /tmp/poseidon-bff-build
POSEIDON_BFF_BROWSER_DIST=/tmp/poseidon-bff-build dotnet test tests/Backend/Poseidon.Backend.Tests.csproj --no-restore --filter FullyQualifiedName~Real_browser_ui
```

Normal yerel Development denemesi ancak operatörün zaten sağlamış olduğu, uygulama/static dizini dışında, mevcut özel Linux x64 kökü ve sentetik DB üzerinde yapılabilir. `EvidenceIntake:Enabled=true`, `EvidenceIntake:Root=<mevcut özel kök>` ve frontend `VITE_ENABLE_EVIDENCE_INTAKE=true` açıkça gerekir. Uygulama dizin oluşturmaz, OS izinlerini değiştirmez. Bir evidence şeması bir nesne köküne aittir; eski/farklı DB ile dosyalar tutarsızsa başlangıç reddedilir.

`node tests/Backend/evidence-uat.cjs`, böyle bir Development servisi önceden varsa yalnız kendi ürettiği sentetik PDF imzalı baytlarla bir gemi ve bir alım oluşturur. Gerçek dosya okumaz; token yazdırmaz. Bu yardımcı betik son paket koşusunda ayrıca yerel serviste çalıştırılmadı; test host ve tarayıcı fixture'ları fiilen çalıştırılan kanıttır.

## Esra için beklenen yol — özel erişim sağlandıktan sonra

1. Sentetik evidence-manager oturumu ve bir gemi/belge açın. Dosya alımı yetkisi bağımsızdır; sıradan taslak yazarı yönetici olmaz.
2. Yalnız sağlanan/üretilen zararsız sentetik PDF/PNG/JPEG seçin. Beklenen: dosya adı metin olarak görünür; yerel önizleme veya çalıştırma yapılmaz.
3. Karantinaya gönderin. Beklenen: `STORED_QUARANTINED`, tarama kullanılamıyor, indirme engelli. Bu “güvenli/geçerli/doğrulanmış” demek değildir.
4. Listeyi yenileyin ve alım geçmişini açın. Beklenen: doğru hedefin kayıtları ve yalnız zaman/sürüm/eylem/izinli alan adları/SELF–OTHER. Dosya içeriği, ham aktör, gerekçe veya eski/yeni değerler geçmişte görünmez.
5. “Yeni dosyayla düzelt” seçin, gerekçe girin ve yeni sentetik dosya yükleyin. Beklenen: ayrı kimlik, eski dosya/hash korunur; başarılı yeni alım eski kayda bağlanır. Bu hukuki iptal veya doğrulama değildir.
6. Tarayıcı otomasyonunun kayıp-yanıt senaryosunu izleyin. Beklenen: eski yükleme kimliği korunur; “yalnız durum kontrolü” commit sonucunu okur, farklı dosya aynı yükleme gibi kabul edilmez.
7. TR/EN ve 390/768/1440 genişliklerinde listeleri, düğmeleri ve uyarıları kontrol edin. Görünüm matrisi gerçek mobil cihaz/ekran okuyucu sertifikasyonu değildir.
8. Bulunan kusurları kaydedin. Teknik PASS kullanıcı kabulü değildir; ancak kullanıcı testi ve açık onaydan sonra USER_ACCEPTED yazılabilir.

## lines:41-50

## Kurtarma ve kullanım sınırları

- RESERVED akış başlamadan 15 dakika sonra yükleme denemesinde EXPIRED olur. Arka planda temizleyici yoktur.
- RECEIVING sırasında kesinti/DB hatası belirsiz alım bırakır; otomatik ikinci akış, commit tahmini veya silme yoktur. Kota korunur. Otomatik devam/kurtarma NOT_IMPLEMENTED.
- REJECTED/EXPIRED eski düzeltme kayıtları korunur; yeni düzeltme rezervasyonuna izin verilir. RECEIVING belirsiz düzeltme çözülmeden paralel düzeltme açılamaz.
- 100 MiB tenant kapasitesi temizlenmemiş/başarısız rezervasyonları da içerir. Politika olmadan kapasite boşaltılmaz. Gerçek verilerin süresiz saklanmasına onay değildir.
- Disk dolması/işletim sistemi çökmesi/elektrik kesintisi fiziksel olarak yaratılmadı; belirli dosya/DB commit sınırlarında kontrollü hata ve iptal enjeksiyonu kullanıldı. Lokal kopya/restore testi üretim DR kanıtı değildir.
- Zararlı yazılım örneği, dış tarama, belge parse/render, OCR, AI tüketimi, download/export, insan doğrulama, controlled DocumentVersion yayını ve operasyonel geçerlilik uygulanmadı.

İnceleme regresyonu: kayıp yanıt sonrasında “Yerel alımı bırak” uyarısını onaylayın. Dosya seçici boşalmalı; aynı dosya tekrar seçilince Gönder etkinleşmeli, fakat kendiliğinden gönderim yapılmamalıdır. Önceki sunucu nesnesi silinmiş sayılmaz. Bu davranış Chromium matrisinde sentetik dosyayla otomatik sınanmıştır.
