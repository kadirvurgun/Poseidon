# 17 dil — 11.11.2026 geliştirme kontrol noktası

Durum: **YAZILDI / ÇEVİRİ DEVAM EDİYOR / TESTLER ERTELENDİ / KABUL EDİLMEDİ**.

## Güncel kaynak — b172b5d

Ana katalogların **11/17** dili tamamen yazıldı: Türkçe, İngilizce, İngiliz
İngilizcesi, Arapça, Almanca, Fransızca, Hintçe, Çekçe, Rusça, Macarca ve İtalyanca.
Her katalog 3169 metin alanı içerir; kaynakta 2991 benzersiz değer vardır.
Bu oran yalnız yazılan katalog dosyası sayısıdır; ekran kapsamı, çeviri kalitesi
veya kabul yüzdesi değildir. **Altı ana katalog açık:** Fince, Slovakça,
İspanyolca, Brezilya Portekizcesi, Basitleştirilmiş ve Geleneksel Çince.

17 ortak katalog ve ayrıca **17 × 123 sunum etiketi** yazıldı. İtalyanca
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

Kaynak yazımına yardımcı dar AST ayrıştırması son değişen 7 TypeScript dosyasında
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


### Son sözlük devamı — 0a3f292

Arıza, duruş, iş emri ve yakıt/liman seçeneklerindeki dört değişken sözlük de
ortak sunum kaynaklarına taşındı. 17 dil için 36 ek etiketle toplam 117 anahtar
vardır. Kaynak enumları, ROB/VLSFO/HFO/MGO/LNG kısaltmaları ve desteklenmeyen
kaynak değerleri korunur; prototype üyeleri bir seçenek olarak çözümlenmez.
İş emri aşamaları açıkça beyan olarak çevrilir, izin/kapama yetkisi oluşmaz.

Gerçek arıza formunda Arapça dil değişiminde aynı alan düğümünün, seçili `Verify`
değerinin ve JSON gövdesinin korunmasını sınayan test ile dört sözlük için
anlamlı kaynak/enum testleri yazıldı. **Çalıştırılmadı, DEFERRED.** Genişletilmiş
kaynak sorgusunda ürünün bu dört değişken dalı kalmadı; i18n çekirdeğinin açık
TR/EN kaynak-fallback dalı beklenen ayrı davranıştır. Bu sınırlı sorgu bütün
monolingual/format metinlerinin tamamlandığını kanıtlamaz.

Graphify güncellemesi türetilmiş kod grafiğini yeniledi; çok sayıda JSON/config
kaynağının düğüm üretmediği uyarısı vardır. Bu dosyalar ve canonical belgeler
ancak doğrudan kaynaklarıyla değerlendirilir; grafiğin kapsamı test kabulü değildir.


Son kaynak incelemesi, eski başlangıç ve shell ekranlarında yedi sabit başlık/durum
metnini ayrıca açık iş olarak kaydetti: LOCAL OPERATIONS, MARITIME PLATFORM,
WORKSPACE, DEVELOPMENT, available, FOUNDATION ve MARITIME OPERATIONS.
Sahipleri [başlangıç ekranı](../../frontend/src/App.tsx),
[geliştirme görünümü](../../frontend/src/agents/AgentsDashboard.tsx) ve
[ortak shell](../../frontend/src/product/EnterpriseChrome.tsx).
Aynı dar sorgunun bulduğu sürüm işaretleri, açık saatler, API yolu, hash algoritması,
birimler ve canonical DRAFT kodları kaynak anlamlarına göre değerlendirilir.
Bu yedi başlık ve altı ana katalog tamamlanmadan tüm çeviriler hazır denmez.


## Shell devamı — b3a2966 / d2eac62

Önceki envanterdeki altı sabit başlık 17 dilde JSON kaynağına taşındı: yerel
operasyonlar, denizcilik platformu, çalışma alanı, geliştirme, temel altyapı ve
denizcilik operasyonları. Yedinci aday `available`, public heartbeat'in doğrulanan
canonical yanıt değeridir; protokol örneği ve API decoder aynı değeri korur.
Bu ayrım bir çevirinin API sözleşmesini değiştirmesini önler.

Uzun marka başlıkları dar alanın içinde sarılır; Arapça başlıkların harf aralıkları
normaldir. Başlangıç ve geliştirme görünümündeki retry düğmeleri ortak Button'a
geçti. Aktif istek, poll sırası, aynı form/düğme kimliği ve tenant/actor/navigation
sınırları korunur. Eski TR/EN düğmelerini arayan gerçek selector yardımcıları
native17dil seçiciye uyarlandı; ayrı mocked health düğmesi kendi fixture'ıdır.

Sadece boşluk içeren kaynak etiketleri etkinleştirilmez. Önceki dil, yön ve kayıtlı
tercihin tutulması;17ayrı tam ürün kataloğunun bulunması; aynı DOM ve source enum
korunması için ek component testleri yazıldı. Gerçek public API'yi kullanan17dil
shell matrisi masaüstü/dar ekran projeleri için yazıldı: focus, RTL, kalıcı locale,
route/reload, canonical heartbeat ve yatay taşma. Altı eksik ana katalog tamamlanmadan
bu matris geçemez. **Hiçbiri çalıştırılmadı: DEFERRED_NEW_BUNDLE_GATE.**

11.11 saat18:09yerel UTC-saat kontrolü, önceki gerçek18:04kota sıfırlanmasından
sonradır. Kalan altı katalog üç ajanla ve ayrı dosya/dal sahipliğiyle yazılıyor:
L17-ES-PT-01 (İspanyolca/Brezilya Portekizcesi), L17-FI-SK-01 (Fince/Slovakça),
L17-ZH-01 (iki Çince varyantı). Yeni kota hatasında ilerleme korunup durulur;
hesap/model rotasyonu veya sürekli tekrar yoktur. **Tam ana katalog sayısı hâlâ
11/17**; gönderilmiş görev, çeviri veya bağımsız kabul kanıtı değildir.

Yeni kod canlıya derlenmedi, yeni PR açılmadı. PR16'nın32ff1d0CIsonucu kendi
kapsamındadır; tam signed ve bağımsız son QA açık kalır. Canonical kabul değişmedi.


### RTL yerleşim devamı — 3219461 / f4c00a3 / b172b5d

Mürettebat filtre/sayfalama boşlukları ve editör kenarı, mevzuat kaynak-zinciri
çizgisi/indeksi, römorkör arama ikonu ve engel kenarı mantıksal yön özelliklerine
geçti. İç menü girintisi, geliştirme zaman çizgisi, public protokol kutusu ve
ayrıntı aksiyonlarının asimetrik boşlukları da yazı yönünü izler. Canonical kod,
zaman ve miktar içerikleri değişmez.

S2'nin mevcut sentetik scoped API fixture'ı üzerinden Arapça menü girintisini,
açık kirli gemi formunun korunmasını ve sıfır business write davranışını sınayan
L17-AC-RTL tarayıcı testi yazıldı. Bu, gerçek sunucu yetkisi kanıtı değildir ve
**çalıştırılmadı**. Public17dil matrisi de protokol kutusunun desktop/narrow
boşluğunu ve aynı canonical heartbeat içeriğini kontrol edecek.

Son ara kayıt: ES/PT-BR için dil başına1450, FI/SK için1020benzersiz metin
sahipli scratch içinde korunmuştur. Her dil için2991değer gerekir. Kısmi üretim
11tam katalog sayısına eklenmez. ZH üretimi ayrı ajanda devam eder.
