---
key: risk-template-library
title: Risk şablon kütüphanesi
roles: [DPA / HSEQ, Master, Tug Master, Crew / OOW / Engineer]
permissions: [hse.risk_template.read, hse.risk_template.write, hse.risk_template.publish, hse.risk_template.suggest, hse.risk_method.write, hse.risk_method.publish]
related: [risk-assessment, high-risk-work]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Risk şablon kütüphanesi, kiracı genelinde kullanılan risk değerlendirmesi şablonlarını (iş türü, tehlikeler, varsayılan kontroller, zorunlu bariyerler, SMS ve gereksinim kaynakları) ve risk yöntemlerini (matris) revizyonlu olarak yönetir. Cevapladığı soru şudur: "Bu iş türü için geçerli, yayımlanmış şablon hangisi ve hangi revizyonla yürürlükte?" Sayfa **yalnız geliştirme hedefidir**: buradaki yayım kayıtlı bir HSEQ insan kaydıdır; çalışma izni, uzman risk kabulü veya HSE kapısı geçişi değildir (13.03 §4).

## Kim kullanır
- **DPA / HSEQ:** risk yönetişimi kapsamında şablon revizyonlarını, bariyerleri ve risk matrisini hazırlamak, incelemek ve yayımlamak için (13.03 §4).
- **Master, Tug Master ve mürettebat:** yayımlanmış şablonları okumak ve şablon için **Mürettebat önerileri** göndermek için.

## Başlamadan önce
- Okumak için şablon okuma yetkisi gerekir. Şablon yazmak ve yayımlamak için tenant geneli kapsam ile şablon yazma veya yayımlama yetkisi gerekir. Öneri göndermek için şablon öneri yetkisi yeterlidir.
- Risk yöntemi oluşturmak ve yayımlamak ayrı yetkilerdir ve tenant geneli kapsam ister.
- Risk komutları güncel çevrimiçi bağlantı ister; çevrimdışıyken hiçbir şey kuyruğa alınmaz veya onaylanmaz.

## Adım adım
1. Listede şablonları görün. Her satırda durum, **Geçerli** revizyon ve **Öneriler** sayısı yazar. Bir şablonu açın.
2. Yeni şablon için **Yeni şablon** düğmesine basın. **Şablon kodu**, **Başlık**, **İş türü**, **Gemi / römorkör kapsamı**, **Onay sorumlusu**, **Değişiklik gerekçesi**, **Zorunlu bariyerler**, **SMS / gereksinim kaynakları** ve **Tehlikeler** alanlarını doldurup **Taslağı kaydet** düğmesine basın.
3. Taslak revizyonda **İncelemeye gönder** düğmesine basın. Eksikler engelleyen maddeler olarak listelenir.
4. Yayım yetkilisi **Yürürlük başlangıcı** ve en az 10 karakterlik **Gerekçe** girip **Yayımla (HSEQ insan kaydı)** veya **İade et** düğmesine basar.
5. Yayımlanmış bir şablonu değiştirmek için gerekçe yazıp **Yeni revizyon başlat** düğmesine basın; eski revizyon değişmez.
6. Mürettebat **Öneri gönder** alanına önerisini yazıp gönderir. Yetkili kişi gerekçe yazarak öneriyi **Kabul et** veya **Reddet** ile değerlendirir.
7. **Risk yöntemleri (matris)** bölümünde **Yeni yöntem** ile **Yöntem kodu**, **Olasılık kodları**, **Şiddet kodları** ve **Matris hücreleri** girilir; başka bir kişi **Yöntemi yayımla** ile yayımlar.

## Durumlar ne anlama gelir
- **Taslak / İncelemede / İade edildi / Yayımlandı / Yerini aldı:** şablon revizyonunun yaşam döngüsü.
- **Geçerli / Planlandı / Yürürlükte değil:** yürürlük görünümü. Yürürlük tarihi gelmiş en son yayımlanmış revizyon **Geçerli**, tarihi gelecekteki revizyon **Planlandı** olur. Gelecekteki revizyon eski revizyonun yerini erken almaz.
- **Bilinmiyor:** alan doldurulmamış; boş değer varsayılan bir değerle doldurulmaz.
- **Yayımlanmış risk yöntemi yok: derecelendirmeler değerlendirilmedi kalır.** Sistem varsayılan matris veya eşik sunmaz.

## Yapamazsınız / dikkat
- Revizyonun yazarı veya incelemeye gönderen kişi aynı revizyonu yayımlayamaz; yayım ayrı bir insan kaydıdır (görev ayrılığı).
- Yayım, çalışma izni, uzman risk kabulü veya onaylı üretim kataloğu değildir.
- Şablon bir risk değerlendirmesine yalnız **kopya** olarak uygulanır; derecelendirme, imza, katılımcı teyidi, onay veya teyit taşımaz. Bir şablon değişince var olan değerlendirmeler kendiliğinden değişmez.
- Yayımlanmış revizyon düzenlenmez; değişiklik için yeni revizyon başlatılır.

## Sorun olursa
- **Listelenen maddeler tamamlanana kadar engelli.** Engelleyen maddeleri tamamlayıp tekrar gönderin.
- **Bu kayıt başka yerde değişti.** **Yeniden yükle** düğmesiyle güncel revizyonu görün.
- **Bu işlemi yapma yetkiniz yok (görev ayrılığı veya kapsam).** Yayımı revizyonu yazmamış başka bir yetkili kişi yapmalıdır; yetki eksikse kiracı yöneticisine başvurun (13.03 §10).
- **Bu işlem mevcut duruma artık uygun değil. Yeniden yükleyin.** Revizyon durumu değişmiştir.

## İlgili sayfalar
