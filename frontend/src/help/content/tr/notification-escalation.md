---
key: notification-escalation
title: Bildirimler
roles: [Chief Engineer, Tug Master, Master, Crew]
permissions: [notification.read, maintenance.read, equipment.read, notification.acknowledge, notification.preference.write]
related: [today, offline-conflict-center]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Bildirimler sayfası, seçili gemi veya römorkör için **uygulama içi yapılandırılmış kaynak istisnalarını** (bakım vadesi geldi, bakım incelemesi gerekli, bakım kaynağı değişti veya kullanılamıyor) listeler. Cevapladığı soru şudur: "Benim rolüme hangi bakım bildirimleri geldi, ne kadar önemli, hangisi süresinde alınmadığı için üst role iletildi ve hangi kaynak kaydına dayanıyor?" Alındı bildirimi kanıtı doğrulamaz veya operasyonu onaylamaz.

## Kim kullanır
- **Başmühendis / Teknik sorumlu:** bakım vadelerini ve inceleme isteklerini izleyip kaynak plana geçmek için (13.03 §5).
- **Tug Master / Kaptan / Mürettebat:** kendi rollerine yönlendirilen bildirimleri görmek ve alındı bildirmek için (13.03 §6).

## Başlamadan önce
- Bildirim, bakım ve ekipman okuma yetkileri gerekir. Alındı bildirmek ve tercih kaydetmek ayrı yetkiler ister; yoksa ilgili düğmeler görünmez.
- **Bildirim aracı** listesinden yetkili gemi veya römorkörü seçin.
- Çevrimdışıyken alındı veya tercih komutu gönderilemez.

## Adım adım
1. **Bildirim aracı** listesinden araç seçin.
2. Politika satırını okuyun: "Yapılandırılmış · doğrulanmamış kaynak politikası · yalnız uygulama içi iletim".
3. Her bildirimde başlığı, önem rozetini (**Bilgilendirme**, **Uyarı**, **Kritik**) ve durum rozetini okuyun; **Kaynak / sürüm**, **Son tarih** ve **Oluşturma zamanı** alanlarını kontrol edin. **Yönlendirme kuralı** bölümü kural kodunu gösterir.
4. **Kaynak planı aç** bağlantısıyla ilgili bakım planına geçin.
5. Bildirimi gördüğünüzü kaydetmek için **Alındı bildir** düğmesine basın (yalnız **Açık** veya **Üst role iletildi** durumunda).
6. Tercih yetkiniz varsa **Zorunlu olmayan bilgilendirme bildirimlerini sustur** seçeneğini ve **Sessiz saatler** (başlangıç, bitiş, IANA saat dilimi) alanlarını düzenleyip **Bilgilendirme tercihini kaydet** düğmesine basın.
7. Diğer sayfalar için **Önceki bildirimler** / **Diğer bildirimler**, tazelemek için **Bildirim kutusunu yenile** düğmesini kullanın.

## Durumlar ne anlama gelir
- **Açık:** bildirim alındı bildirilmeyi bekliyor.
- **Alındı bildirildi:** bir kişi bildirimi gördüğünü kaydetti. Bu, bakımın yapıldığı veya kanıtın doğrulandığı anlamına gelmez.
- **Üst role iletildi:** bildirim süresinde alınmadığı için yönlendirme kuralındaki üst role iletildi.
- **Kaynak değişti / Kaynak kullanılamıyor:** dayanak kayıt değişti veya teyit edilemiyor. Kaydedilen dayanak korunur; bildirim kapanmış veya yeniden başlamış sayılmaz.
- **Zorunlu yol · susturulamaz:** bu bildirim tercihlerle susturulamaz.
- **Bilinmiyor (Son tarih):** kaynakta son tarih yok. Bu, vadenin olmadığı anlamına gelmez.

## Yapamazsınız / dikkat
- Alındı bildirimi kanıtı doğrulamaz veya operasyonu onaylamaz.
- Zorunlu ve kritik yollar bilgilendirme tercihleriyle susturulamaz. Sessiz saatler yalnız isteğe bağlı bildirimleri erteler.
- Boş liste her şeyin uygun olduğunu göstermez. Politika veya kaynak kullanılamıyorsa sayfa bunu ayrıca yazar.
- Otomatik tekrar yapılmaz; belirsiz bir komut bu sekmede korunur ve bağlam değişikliği kilitlenir.

## Sorun olursa
- **"Sonuç bilinmiyor" mesajı:** **Aynı bildirim komutunu yinele** düğmesiyle korunan komutu tekrar gönderin.
- **"Bildirim sürümü değişti" mesajı:** **Bildirim kutusunu yenile** ile güncel listeyi yükleyin.
- **"Bildirim kaynakları kullanılamıyor veya erişim reddedildi." mesajı:** rol ve araç bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).
- **"Kayıtlı sessiz saatler uygulanamadı" mesajı:** tüm bildirimler gösterilir; sessiz saat ayarlarınızı kontrol edin.

## İlgili sayfalar
