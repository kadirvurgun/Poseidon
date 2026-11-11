# 17 dil — 11.11.2026 geliştirme kontrol noktası

Durum: **17 KATALOG YAZILDI / PR ÖNCESİ TOPLU DOĞRULAMA BAŞLADI / KABUL EDİLMEDİ**.

## Güncel kaynak — 4a253fb

Ana katalogların **17/17** dili tamamen yazıldı: Arapça, Çekçe, Macarca,
Fransızca, İngilizce, Fince, İngiliz İngilizcesi, Basitleştirilmiş Çince, Hintçe,
Slovakça, Türkçe, Rusça, Brezilya Portekizcesi, İtalyanca, İspanyolca, Almanca ve
Geleneksel Çince. Her katalog3169metin alanı içerir; İngilizce kaynakta2991benzersiz
değer vardır. Bu yalnız kaynak yazımı tamamlanmasıdır; ekran kapsamı, runtime,
çeviri kalite sertifikası veya kabul yüzdesi değildir.

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


### İlk tam katalog entegrasyonu — a843f80

ZH-Hans, ayrı yazarın `d0bc628` commit'inden normal cherry-pick ile `a843f80`
olarak alındı. Tek katalog dosyası,3169metin alanı ve2991benzersiz kaynak değeri
kapsar. Kaynak hash'i sabit kaldı. Üretim sırasında yapı/değişken/Unicode
kontrolleri yazımı destekledi; test/build/typecheck veya bağımsız kabul değildir.
Tam yazılmış ana katalog sayısı12/17; kalan beş dil yazılmaktadır.


`2187e1d`: Bugün/römorkör geri-ileri ve sayfalama ikonları açık inline-navigation
işaretiyle RTL'de yön değiştirir. Hedef adresi, sayfa offset'i veya kayıt içeriği
çeviriden etkilenmez. L17-AC-RTL testi gerçek derlenmiş CSS dönüşümünü ve İngilizceye
dönüşte aynı form verisinin korunmasını da sınayacak. Test yürütmesi yine DEFERRED.
Bu devamın13değişen TypeScript kaynağına uygulanan dar AST ayrıştırması sözdizimi
tanısı bildirmedi; typecheck/build veya test PASS yerine kullanılamaz.


### Foundation kaynak hizalaması — yerel normal merge

Yerelleştirme dalı `52c1181` foundation ucu ile normal merge edildi. Bu kaynak,
`32ff1d0` için iki olayda geçen required CI ile daha sonraki kanıt metadata'sını
içerir. Yeni birleşik hedefin testleri DEFERRED'dır; geçmiş CI yeni hedefe PASS
olarak taşınmaz. Backend, API testleri, OpenAPI ve generated consumer alt ağaçları
foundation ile aynıdır; bu yalnız kaynak karşılaştırmasıdır.

İki metin çatışması çözüldü: durum raporunda foundation'ın güncel kanıt geçmişi
ile yeni locale özeti korundu; genuine login yardımcısında her vaka için gerçek
giriş ve tam güvenli hedef davranışı,17dil native selector ile birleştirildi.
Canonical kabul artmadı. PR16 uzaktan merge edilmedi; güncel tam signed/bağımsız
QA kapıları açıktır. Kalan kataloglar yalnız tam dosya commit'leriyle alınır.


### İki Çince varyantı ve bağımsız kaynak incelemesi

Hans terim düzeltmesi `e578dfa` → `8a3cfd7`; Hant ayrı tam kataloğu `08986b5` →
`981dae4`. Her katalog3169metin alanı kapsar. Hant ayrı sözlük/kelime kullanımı ve
92cümle düzeltmesiyle yazıldı. Son SHA256: Hans
`32b083cf3b1421d11918f87c387645f85942a31661d9290bb113d764120dd637`, Hant
`5d1f0e8cb0c121fe5bc6a6a0f92b81ae1dd49db29acf4bf1e18612dc0babe9c1`.
Üretim denetimi bağımsız test/kalite kabulü değildir. Tam ana katalog13/17.

L17-QA-SOURCE-01, `981dae4` hedefinde Root'un UI/i18n ve genuine login selector
birleşimini ayrı ajanın statik kaynak incelemesine verir. İnceleyici kendi yazdığı
Çince kataloglarını bağımsız kabul etmez. Test/build/typecheck/browser yürütmesi
DEFERRED; kaynak incelemesi final QA veya faz kabulü değildir. Kalan dört ana
katalog ES/PT-BR ve FI/SK sahipli dallarında yazılmaktadır.


### İspanyolca / Brezilya Portekizcesi — 4fa3c74 / 786a0b9

Tam ES `d3762ae` ve PT-BR `365fc2f` katalogları ayrı commit'lerden alındı; her
biri3169metin alanı ve2991benzersiz kaynak değeri kapsar. Repository LF SHA256
ES `a5bd66b17d02cdec08058037421cf1d01e934f5b68f04fac6183792f25790f05`,
PT-BR `47868a1bf54590d0d602c37bc28f6f665d651e5b51ef31422c5fb4139c7366b2`.
Yazarın çalışma kopyası CRLF hash'leri ile repository LF hash'leri farklı
baytları tanımlar; kaynak İngilizce hash'i değişmedi. Tam ana katalog15/17.

L17-QA-COPY-01, başka yazarların kataloglarında ve Root sunum etiketlerinde
Unknown/Missing/beyan/insan-yetkisi olumsuzlukları ve Unicode için sınırlı statik
anlam incelemesi yapar. İnceleyicinin kendi ES/PT ana katalogları bağımsız kabul
kapsamına alınmaz. Bu bir dil kalitesi sertifikası, test yürütmesi veya faz kabulü
değildir. UI kaynak incelemesi başka ajanda ayrı sürer; FI/SK son491'er kaynak
metni yazılmaktadır. Bütün yeni paket testleri DEFERRED kalır.


## 17/17 katalog yazımı tamamlandı — 4a253fb

FI `d4f4c42` → `9e195dd`; SK `32d98c2` → `4a253fb` ile kalan iki tam katalog da
alındı. Her biri3169metin alanı kapsar. Repository SHA256 FI
`136c16793766349e967adad3f5af1eef17b1f575f223fde3e1149a063ca21fde`, SK
`201f279fc4155535af5bc6e58678d0d67397fbd23069c1b0745df23b9d300d7e`.
17dosya,17×38ortak anahtar ve17×123sunum anahtarı aynı geliştirme dalındadır.
İngilizce kaynak hash'i sabit; canonical enum/kimlik/zaman/komut değerleri korunur.

### Bağımsız statik inceleme ve somut düzeltme

L17-QA-SOURCE-01, `981dae4` üzerinde60dosya içeren Root UI/i18n ve genuine
login/selector birleşimini inceledi; somut kaynak hatası bildirmedi. İnceleyicinin
kendi Çince katalogları bu bağımsız kabul kapsamına alınmadı. OpenCC0.1.7/s2tw
geçici yerel yazım aracıdır, uygulama bağımlılığı değildir.

L17-QA-COPY-01, `786a0b9` üzerinde40ana katalog girdisi ve408sunum etiketiyle
sınırlı anlam/Unicode incelemesinde P2 L17-COPY-01 buldu. TR/RU/ikiÇince'de NONE
seçeneği eksik beyanı çağrıştırıyordu. Aktif11.01/2316Alanv46.3/row2204, açık
NONE;ATTENTION;DISPUTED seçeneklerini dış Missing/Unknown durumundan ayrı tanımlar.
`3c30144` tüm17sunum etiketini açık “fark yok (beyan)” anlamına çekti; gönderilen
NONE kodu değişmedi. Gerçek kontrollü yakıt formu ve17dil ayrımı testleri yazıldı.
İnceleyici düzeltmeyi **RESOLVED_SOURCE_ONLY** olarak tekrar değerlendirdi.

FI/SK için `4a253fb` üzerinde16anahtar girdisinin ek statik incelemesi somut hata
bildirmedi. ES/PT'nin başka yazardan16girdilik ek incelemesi de somut hata bildirmeden tamamlandı. Bunlar sınırlı
örneklerdir; tam dil kalitesi sertifikası veya final QA değildir. Önceki bulgu ve
hedef-commit kayıtları silinmedi. Hiçbir yeni test yürütülmedi.

### Açık kapılar ve sonraki adım

Paket, PR hazırlığında tek toplu lint/typecheck/contract/build, component, gerçek
API desktop/narrow dil/RTL/taslak yolculukları ve kaynak/client taramasını bekler.
Ardından bağımsız final hedef QA ve required CI gerekir. Yeni kod önizlemeye
alınmadı, yeni PR açılmadı. PR16 tam signed/bağımsızQA kapısı ayrı açıktır; eski
174koşu15PASS/159FAIL ve32ff1d0CIkanıtı kendi hedeflerinde korunur. Canonical
**103/970SP(%10,62),20/121iş,0/196ayrıekran** kabul değişmedi.


### Bağımsız kaynak inceleme kapanışı — yürütme kabulü değildir

| İnceleme | Hedef / kapsam | Somut kaynak sonucu |
|---|---|---|
| Root UI/i18n/auth selector |981dae4;60dosya; inceleyicinin kendiÇincecatalogları hariç|Somut kaynak bulgusu yok|
| Anlam/Unicode ilk örnek|786a0b9;40ana girdi+408sunumetiketi|P2 L17-COPY-01;3c30144ileRESOLVED_SOURCE_ONLY|
| Fince/Slovakça ek örnek|4a253fb;16girdi; farklı yazar|Somut anlam/Unicode bulgusu yok|
| İspanyolca/Portekizce ek örnek|4a253fb;16girdi; farklı yazar|Somut anlam/Unicode bulgusu yok|

Her kayıt kendi hedefi ve sınırlı örneğiyle geçerlidir. Rapor JSON hash'leri
[durum kaydına](../project-status.json) işlendi. **Hiçbir test/build/typecheck/lint/
browser çalıştırılmadı; acceptance=false.** Bütün dillerin tam dil kalitesi veya
bütün ürünün uzman/pilot kabulü ilan edilmez. Sonraki iş PR hazırlığında toplu
paket yürütmesi ve bağımsız son hedef QA'dır; yeni kaynak önizlemede değildir.


## 11 Kasım — PR hazırlığı toplu yürütme başladı

Paket kaynak yazımı tamamlandıktan sonra kullanıcı teslim ritmine uygun toplu
ön yüz kapısı bağımsız QA dalında başlatıldı. `1ff65b9` hedefinde API consumer
üretimi ve generated-contract parity exit0 ile geçti. İlk lint exit1: tek
`@typescript-eslint/no-unused-vars`, `ComplianceInputEditor.tsx:34:11`.
Kullanılmayan selector yardımcı fonksiyonu `5ae621a` ile kaldırıldı; kullanılan
etiket yardımcı fonksiyonu ve iş davranışı korunur. Özgün başarısız sonuç/hashi
saklandı. Yeni hedefte lint→typecheck→component→productionbuild kapısı tekrar
başlatıldı; sonuçlar henüz yok. İlk hedefte sonraki adımlar failfast nedeniyle
çalışmadı, test sayısı0. Önceki DEFERRED kayıtları tarihsel yazım kapsamıdır.

Güncel durum ve koşullu tarihler [teslim tahmin raporunda](./DELIVERY-FORECAST-20261111.md).
Tam signed, gerçek browser ve bağımsız son hedef kabul kapıları ayrı açıktır.


Yeni hedef `5ae621a`: lint25,108s exit0, hata/uyarı0; typecheck15,722s exit0 geçti.
Tüm component suite bağımsız QA dalında tek koşuyla çalışıyor, sonuç henüz yok.
API üretim/parity kaydı aynı kaynak blob'ları ile önceki hedefe bağlı olarak
korunur. Tam paket/browser/signed/CI kabulü hâlâ açık.


## 11 Kasım — ilk toplu sonuç, düzeltmeler ve yeni hedef

`5ae621a` tam component koşusu: **1435 test,1430 PASS/5 FAIL,0 pending/todo**;
305,849s. Beş neden: insan-beyan metni, izinli locale depolama beklentisi,
birleşik ekipman/iş metni, en/en-GB UTC gösterimi ve gerçek çatışma düğmesi adı.
`152fb0a` test sözleşmelerini düzeltti; dar beş dosya **36/36 PASS**, hata/skip/
unhandled0. Önceki başarısız koşu silinmedi ve başarıya çevrilmedi.

Kaynak incelemesinde katalog dışındaki compliance alanları tespit edildi.
`721cf0b` açık tipli16alan etiketini17dile ekledi: güncel **17×139 sunum anahtarı**.
Gerçek form testi Fransızcaya geçerken aynı DOM/taslak baytlarını, false/Unknown
ayrımını ve offline kilidini kapsar. Eski yardımcı satırın lint hatası12:11,
`23b87d4` ile giderildi. Bağımsız272etiket kaynak incelemesi bu bulguyu
RESOLVED_SOURCE_ONLY kapattı; runtime kabulü değildir, İngilizce ana hash sabit.

`23b87d4` bağımsız ön yüz kapısı: lint17,128s exit0,hata/uyarı0;
typecheck12,525s exit0; ComplianceInputs3/3,hata/pending/todo/unhandled0,
12,464s. Tam component koşusu sürüyor; build/browser/signed/finalQA/CI açık.
API üretim/parity kanıtı değişmeyen kaynak blobları için özgün hedefe bağlıdır.
Yeni teknik owner-recovery paketi ayrı dalda yazılır; onun yeni testleri DEFERRED.
Canonical kabul ve PR16 merge durumu değişmedi.
