# IMO_CHECK_DIGIT.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/docs/IMO_CHECK_DIGIT.md
SHA256: affbccc10d7e981c8ed2b3e131d06601d0408212a255fc527d475685a0008090

## lines:1-40

# Gemi IMO kontrol basamağı — yeni teknik doğrulama kararı

29 Ekim 2026. Taban: 12e5a20f421455c5340257b2e9e4e81e014d99ab.
Kullanıcı negatif UAT'de 1234568 değerinin v4 olarak kaydedildiğini gösterdi.
Önceki sözleşme yalnız yedi rakam kontrol ediyordu. Bu paket ship imo_no alanına
kontrol basamağı kontrolü ekler; gerçek kayıt veya kullanıcı runtime'ını değiştirmez.

## Kaynaklar ve kanıt düzeyi

- IMO'nun kendi FAQ'sı, altı sıralı rakam ve ardından kontrol basamağını doğrular:
  https://wwwcdn.imo.org/localresources/en/OurWork/IIIS/Documents/IMO%20Ship%20Number%20%26%20Extension%20FAQs.pdf
- Numarayı atama/doğrulama yetkisinin S&P Global'de olduğu şema yöneticisinin
  kendi açıklamasında belirtilir:
  https://imonumbers.ihs.com/Home/About
- Aritmetik uygulama birincil yazılım kaynağıyla karşılaştırıldı:
  https://github.com/arthurdejong/python-stdnum/blob/master/stdnum/imo.py
  Sürüm 2.1 dokümanı ve yayımlanmış test örnekleri:
  https://arthurdejong.org/python-stdnum/doc/2.1/stdnum.imo

Resmî IMO FAQ'sında ağırlıklar açıkça yazılmıyor. Dolayısıyla burada formülün
ayrıntılarının IMO tarafından ayrıca onaylandığı iddia edilmiyor. Yazılımın
birincil uygulaması 7,6,5,4,3,2 ağırlıkları ve mod10 ile karşılaştırıldı;
9319466/8814275 pozitif, 8814274 negatif bağımsız örnekleri test edildi.
Paket kurulmadı veya kaynak kod kopyalanmadı; basit aritmetik yerel olarak yazıldı.
Bu kaynak sınırı bağımsız inceleme için açık tutulur. Sicil sorgusu yapılmadı.

Kaynak erişim sınırlamaları: UNECE PDF web erişimi HTTP403 döndü; terminalden
https://imonumbers.ihs.com/ salt-okunur HTML isteği proxy CONNECT HTTP403 ile
engellendi. Hiçbiri yeniden denenmedi; erişim kontrolü değiştirilmedi.
Bu istekler atama/hesap açma/kimlik bilgisi işlemi değildi.

## Sözleşme

Gemi numarası ABCDEFG için (7A+6B+5C+4D+3E+2F) mod10 = G olmalıdır.
Yalnız tam 7 ASCII rakam (0–9); prefix, işaret, Unicode rakam, newline ve fazladan
karakter reddedilir. Girişte yanlış basamak otomatik düzeltilmez ve doğru numara
önerilmez. Check digit eşleşmesi atama, gemi kimliği, uygulanabilirlik, sicil
kaydı, operasyonel veya hukuki geçerlilik kanıtı değildir. Tüm yanlış yazımları
tespit etmez; biçim/aritmetik olarak geçen sayı da atanmamış olabilir.
Başlangıç rakamı/katalog/IMO zorunluluğu için yeni kural uydurulmaz.

## lines:41-80

IMO isteğe bağlı kalır. Boş UI alanı mevcut payload politikasıyla gönderilmez.
API'de alanın atlanması geçerlidir; açık boş string mevcut sözleşme uyarınca
reddedilir. Taslak/admin yazılarında yanlış biçim invalid_imo, yanlış kontrol
basamağı invalid_imo_checksum ve HTTP422 verir. Client validation bypass edilse de
sunucu reddeder; rejected mutation aggregate version/audit/idempotency başarı
kaydı oluşturmaz. Admin read completeness.invalidFields içinde imo_no gösterebilir.
Şirket company_imo ayrı şemadır; gemi algoritması bu alana uygulanmaz.

## Mevcut kayıtlar ve yeniden deneme

Migration veya arka planda düzeltme yoktur. Eski hatalı IMO okunur/aranır ve
tarihçesi korunur; RECORDED ise otomatik DRAFT yapılmaz. İdari ekran hatalı IMO
alanını gösterir. Metadata replacement kaydında hatalı değer artık reddedilir:
yetkili kullanıcı kaynağından kontrol ederek düzeltmeli veya optional alanı
açıkça boş bırakmalıdır. IMO zorunlu olmadığından onu kaldırmak için tek başına
reopen gerekmez; mevcut state/permission/If-Match kuralları aynen uygulanır.
Hatalı IMO ile yeni record komutu completeness kontrolünden geçmez.
Yetkili reopen mevcut düzeltme yolunu korur; eski audit değerleri değişmez.
Legacy taslakta hata kaydetme denemesinde görünür; okuma eski veriyi silmez.

Yeni doğrulama, eski invalid payload'ın tekrarını 422 ile reddedebilir. Bu,
önceki sürümde aynı işlem zaten kaydedilmişse onu geri almaz veya başarısızlığını
kanıtlamaz. Bilinen record ID/version yeniden okunur; yeni create anahtarıyla
otomatik tekrar yapılmaz. Mevcut belirsiz işlem bariyerleri değiştirilmedi.

## TR/EN ve sentetik UAT

Ortak frontend fonksiyonu iki formda aynı alan mesajını üretir; dil değişince
hata yeniden çevrilir. Kaydedilmemiş notlar korunur. Test için checksum geçen
sayı gerçek sentetik gemiye atanmaz; olumlu sayılar yalnız aritmetik unit örnekleridir.
Runtime seed'e fake-valid IMO eklenmedi. Kullanıcının v4 kaydı bu görevde
okunmadı, yazılmadı, silinmedi veya düzeltilmedi.

İnceleme/yayın sonrası kullanıcı testi:
1. Mevcut hatalı IMO kaydı hâlâ aynı ID/version ve notlarla okunabilmeli.
2. 123, Unicode rakam ve 1234568 kaydetme girişimi reddedilmeli; sürüm değişmemeli.
3. Kaynağı bilinmeyen test IMO alanını kullanıcı açıkça temizleyip kaydederse
   normal tek sürüm artışıyla alan kaldırılmalı; uydurma yeni IMO girilmemeli.
4. TR/EN hata metni, notların korunması ve isteğe bağlı boş taslak kontrol edilmeli.

## lines:81-89

Gerçek Codespaces retest ve Esra kabulü bu paketin otomatik testleriyle tamamlanmış değildir.

## İnceleme düzeltmeleri (v19)

Tarihsel taslakta `completeness.invalidFields` boş değilse Envantere al düğmesi kapalıdır; sunucunun mevcut 422 kontrolü korunur.

Eski taslak istemcisinin sonucu belirsiz bir create isteği, yeni doğrulama kuralları nedeniyle tekrar denendiğinde 422 alabilir. Bu ret, önceki isteğin commit olmadığını kanıtlamaz. İstemci ilk ağ/5xx belirsizliğinden sonra aynı anahtarı ve gövdeyi tutar; sonraki 4xx bunları temizlemez. Alanlar kilitli kalır, otomatik yeni create yapılmaz. Açık tekrar deneme yeni yerel doğrulamadan geçirilmeden aynı değişmez isteği sunucuya gönderir; sunucu doğrulaması ve güncel yetki denetimi korunur. Oturum kaybında tekrar gönderim kapalıdır. Kullanıcı başka sekmede mevcut kayıtları kontrol edebilir; ancak belirsizliğe ve anahtar kaybına ilişkin açık uyarıyı onaylayarak yerel işlem bilgisini bırakabilir. Bu onay sunucudaki kaydı silmez veya önceki isteği iptal etmiş sayılmaz. Sayfa kapatıldıktan sonra anahtarın kalıcı kurtarılması bu kapsamda uygulanmadı.

Regresyonlar eski doğrulayıcıyla commit/yanıt kaybı → yeni 422 → aynı anahtar/gövde, iptal edilen ayrılma ve açık ayrılma davranışlarını kapsar. Gerçek yerel BFF/PostgreSQL tarayıcı koşusunda geçerli metadata create yanıtı düşürülür, ikinci denemeye sentetik 422 enjekte edilir, üçüncü aynı anahtar sunucunun tek kaydını doğrular. Bu, gerçek Codespaces yeniden doğrulaması veya eski geçersiz IMO'nun yeni sunucuda kabulü iddiası değildir.
