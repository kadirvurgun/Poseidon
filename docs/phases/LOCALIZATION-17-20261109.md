# 17 dil — 11.11.2026 geliştirme kontrol noktası

Durum: **YAZILDI / ÇEVİRİ DEVAM EDİYOR / TESTLER ERTELENDİ / KABUL EDİLMEDİ**.

## Güncel kaynak — 16fdc90

Ana katalogların **11/17** dili tamamen yazıldı: Türkçe, İngilizce, İngiliz
İngilizcesi, Arapça, Almanca, Fransızca, Hintçe, Çekçe, Rusça, Macarca ve İtalyanca.
Her katalog 3169 metin alanı içerir; kaynakta 2991 benzersiz değer vardır.
Bu oran yalnız yazılan katalog dosyası sayısıdır; ekran kapsamı, çeviri kalitesi
veya kabul yüzdesi değildir. **Altı ana katalog açık:** Fince, Slovakça,
İspanyolca, Brezilya Portekizcesi, Basitleştirilmiş ve Geleneksel Çince.

17 ortak katalog ve ayrıca **17 × 81 sunum etiketi** yazıldı. İtalyanca
`6c5df69` ile tamamlandı; `d8cfde6` mürettebat ve Unicode düzeltmelerini,
`16fdc90` mevzuat/sağlık/çevrimdışı sunum devamını içerir:

- Mürettebatın belge, iletişim, yetkinlik, çalışma/dinlenme, uygunluk ve sağlık
  girdileri; bilinmeyen, gizli ve değerlendirmesi kullanılamayan durumlar.
- Sayfayla sınırlı atama sayacı; locale değişince filtre ve sıfır değerleri korunur.
- Mevzuat kaydındaki 59 eski yardımcı çağrısı, taslak/çatışma/tekrar-deneme ve
  değişmez kaynak seçimi. Kaydedildi bildirimi yeni dilde yeniden gösterilir.
- Sistem sağlığı güncellik kategorileri ve çevrimdışı römorkör çatışma başlıkları.
- Ek dosya modunda ortak Button/EnterpriseActions bileşenleri; mevcut taslak ve
  aktarım kilitleri korunur. Kaynak adları/kodları çevrilmez.
- Bakım planı aramasında seçili dilin harf dönüşümü ve yerelleştirilmiş mevzuat
  sayfa başlıkları. Komut anahtarı, gövdesi veya canonical API değerleri değişmez.

İtalyanca önceki parçalardaki 347 ve Fransızca 2378 metin alanındaki karakter
bozulmaları yalnız tam tersinir UTF-8/Windows-1252 dönüşümüyle düzeltildi.
Geçerli Fransızca `Âge` korunur. Ana İngilizce kaynak SHA256 değeri değişmedi:
`a555c082fcdbecb4836be6d3734eaf7587230ce0a463dd8274bffd59e6123203`.

### Doğrulama ve açık işler

Yeni paket build/typecheck/component/browser/regresyonu **ÇALIŞTIRILMADI**;
**DEFERRED_NEW_BUNDLE_GATE**, PASS değildir. Unicode, katalog değişkenleri,
aynı form düğümleri, filtreler, dosya modu kilitleri, sabit komut içeriği ve
çevrimdışı açık çözüm seçimi için anlamlı testler kodla birlikte yazıldı.

Kaynak yazımına yardımcı dar AST ayrıştırması son değişen 10 TypeScript dosyasında
sözdizimi tanısı veya çözümlenmeyen literal kaynak anahtarı bildirmedi. Sınırlı
dil-koşulu AST sorgusu 0 eşleşme döndürdü; bütün sunumun çevrildiğini, typecheck
veya test başarısını kanıtlamaz. Graphify yalnız türetilmiş keşif grafiğidir.

PR16 ayrı pakettir. `32ff1d0` için push ve PR CI dört zorunlu işi de geçti;
iki API artifact'i ayrı ayrı 826/826 integration, 176/176 unit, hata/skip 0 ve
117/117 gerçek kritik işlem gösterir. Yerelleştirme dalına taşınmaz. Önceki tam
174 signed sonuç 15 PASS / 159 FAIL; güncel tam tekrar ve bağımsız son QA açık,
merge yapılmadı. Yeni yerelleştirme henüz canlı önizlemeye derlenmedi.

Sonraki iş altı ana kataloğu ve değişken/sözlük/format kaynak incelemesini bitirmek;
ardından tek büyük kapıda lint/typecheck/contract/build, tüm component testleri,
gerçek desktop/narrow dil/RTL/taslak yolculukları ve kaynak/client taraması.
Bağımsız hedef QA ve required CI olmadan yayın/kabul yoktur. Canonical kabul
**103/970 SP (%10,62), 20/121 iş, 0/196 ayrı ekran** olarak değişmedi.

## Önceki kontrol noktaları — tarihsel kapsam

Tek i18n dil sahibi, kalıcı tercih, ortak dil seçici, yerel JSON katalogları ve
yükleme başarısızlığında önceki dili koruyan akış yazıldı. Arapça RTL için ortak
yerleşim ve ekipman ağacı klavye yönleri uyarlandı. Dil değişimi taslakları veya
canonical API kodlarını değiştirmemeli.

17 ortak katalog hazır.3169metin alanlı ürün kataloğunun10dili tamamen yazıldı:
Türkçe,İngilizce,İngiliz İngilizcesi,Arapça,Almanca,Fransızca,Hintçe,Çekçe,Rusça,Macarca.
İtalyanca kısmi çalışması korunuyor; kalan diller tamamlanmadı.
Çeviri ajanları gerçek kullanım sınırı bildirdi; hesap/model rotasyonu yapılmadı.

Kaynak AST ayrıştırması kod yazımını destekledi; full build/typecheck/test
çalıştırılmadı. Test kaynakları ve gerçek tarayıcı seçicileri güncellendi;
sonuçları **DEFERRED**, PASS değil. Yeni ürün paketi PR16’dan ayrı tutuluyor.
Canonical103/970SP,%10,62 kabul değişmedi.

[Görev ve kabul koşulları](../tasks/LOCALIZATION-17-01.md).


11.11güncellemesi:950c412 ile günlük görev, tarih sırası, beyan ve durum etiketleri
ayrı JSON namespace’e taşındı;17dil×31etiket. Canonical seçenek değerleri
ve komut gövdeleri korunur. Arapça “beyan edilmedi” erişim reddiyle karıştırılmaz;
sertifika durumu açıkça beyan olarak gösterilir. Kaynak3169metinlik ana katalog
hash’i değişmedi.10tamana katalog ve7eksik ana katalog vardır;17tamüründili
iddiası yoktur. Kalan dictionary/array/template kullanımları da kapatılacak.
Yeni label/RTL/taslak/locale testleri yazıldı; build/typecheck/component/browser
kontrolleri yeni paket kapısına DEFERRED, PASS değil. PR16 recovery ayrı daldadır.
