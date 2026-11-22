---
key: pilot-gate
title: Pilot go/no-go incelemesi
roles: [Product Owner, Tenant Admin]
permissions: [pilot.gate.read, pilot.gate.support.record, pilot.gate.decide]
related: [role-uat, pilot-program, audit-ready]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Pilot go/no-go incelemesi sayfası, pilota geçiş için gereken kapıların durumunu, açık P0 sayısını ve destek hazırlığını tek yerde gösterir ve atanmış Ürün Sahibinin go/no-go kararını kaydeder. Cevapladığı soru şudur: "Pilot için her kapı kanıtla karşılandı mı ve insan kararı kaydedildi mi?" Sayfanın üstündeki uyarı geçerlidir: "Yalnız tavsiye niteliğinde inceleme. Bilinmeyen veya ertelenen kapılar ve bilinmeyen ya da açık P0, NO-GO önerir. Kararı yalnız atanmış Ürün Sahibi kaydeder."

## Kim kullanır
- **Ürün Sahibi (Product Owner):** inceleme sonucunu okur ve go/no-go kararını gerekçesiyle kaydeder. Karar için geçerli bir Ürün Sahibi ataması gerekir.
- **Destek / tenant yöneticisi (Tenant Admin):** destek ve nöbet hazırlığı maddelerini kanıt referansıyla işaretler.
- 13.03 bu sayfa için ayrı bir rol tanımlamaz; tenant yönetimi ilkeleri (en az yetki, kontrollü yayın) geçerlidir (13.03 §9).

## Başlamadan önce
- Sayfayı görmek için tenant geneli okuma ve pilot kapı okuma yetkisi gerekir. Destek maddesi işaretlemek için destek kayıt yetkisi, karar için karar yetkisi ve ayrıca sunucuda tanımlı geçerli Ürün Sahibi ataması gerekir. Yetki tek başına yeterli değildir.
- Kapıların kaynakları başka sayfalardan gelir; örneğin Gemi UAT ve Römorkör UAT kapıları **Rol UAT kayıtları** sayfasındaki teyitli koşulardan hesaplanır.
- Kayıt canlı bağlantı gerektirir; çevrimdışıyken kayıt devre dışıdır.

## Adım adım
1. **Pilot kapı incelemesi** bölümünde öneriyi (**Tavsiye niteliğinde öneri**), **Açık P0** sayısını ve kapı tablosunu inceleyin: **Kapı**, **Durum**, **Kaynak**, **Gerekçeler**.
2. **Destek ve nöbet hazırlığı** bölümünde her madde için **Durum** seçin, **Hazır** için **Kanıt referansı** girin, bir **Not** yazın ve **İşaretle** düğmesine basın.
3. Ürün Sahibi iseniz **Go/no-go kararını kaydet** formunda kararı seçin (**NO-GO**, **Koşullu GO**, **GO**), **Atama** listesinden Ürün Sahibi atamanızı seçin ve **Gerekçe** yazın. **Koşullu GO** için **Koşullar (her satıra bir)** alanını doldurun.
4. **Kaydet** düğmesine basın. Karar **Ürün Sahibi kararları** listesinde sıra numarasıyla görünür.
5. Veriyi tazelemek için **Yenile** düğmesine basın.

## Durumlar ne anlama gelir
- **Kanıt eşiği karşılandı (PASS):** kapının kaynağı kanıt eşiğini karşılıyor. Bu bir onay değildir; kararı insan verir.
- **Başarısız (FAIL)**, **Bilinmiyor (UNKNOWN)**, **Ertelendi (DEFERRED):** kapı karşılanmadı. Bilinmeyen veya ertelenen kapı geçmiş sayılmaz ve NO-GO önerir.
- **NO-GO önerilir:** en az bir kapı PASS değil ya da açık P0 bilinmiyor veya sıfırdan büyük.
- **GO için uygun; insan kararı bekleniyor:** tüm kapılar PASS ve açık P0 sıfır. Sistem GO kararı vermez; yalnız Ürün Sahibi kaydeder.
- **İşaretlenmedi / Hazır / Hazır değil:** destek maddesinin son işaretlenen durumu.
- **Güncel / Eski: inceleme değişti:** karar, verildiği andaki incelemeye bağlıdır. İnceleme sonradan değiştiyse karar eski görünür ve yeniden değerlendirme gerekir.

## Yapamazsınız / dikkat
- "GO yalnız tüm kapılar PASS ve açık P0 sıfır olduğunda mümkündür." Diğer durumlarda GO seçeneği kapalıdır.
- Koşullu GO, başarısız veya bilinmeyen kapı varken ya da açık P0 varken kaydedilemez.
- Sistem, yapay zekâ veya otomasyon pilot kararı vermez ve kapı ya da imza üretmez. Pilot kapıları ve kararlar insan kayıtlarıdır.
- Kayıtlı bir kararı değiştiremezsiniz; yeni karar yeni bir sıra numarasıyla eklenir.

## Sorun olursa
- **Geçerli bir Ürün Sahibi atamanız yok. Kararlar salt okunur.** Atama sunucuda tanımlanır; Tenant Admin'e başvurun (13.03 §10).
- **Bu kayıtlara erişiminiz yok.** Rol ve tenant kapsamınızı kontrol edin.
- **Kayıt değişti veya daha önce kullanıldı. Yeniden yükleyin.** İnceleme siz karar verirken değişti; **Yeniden yükle** ile güncel incelemeyi açın.
- **Sonuç belirsiz. Yeniden deneme aynı komutu gönderir.** **Yeniden dene** ile aynı komutu güvenle gönderin.
- **Kurallar gereği reddedildi.** Seçtiğiniz karar mevcut kapı durumlarıyla uyumlu değil.
- **Hizmet kullanılamıyor. Yeniden deneyin.** Sorun sürerse correlation ID, zaman ve adımlarla destek ekibine bildirin (13.03 §10).

## İlgili sayfalar
