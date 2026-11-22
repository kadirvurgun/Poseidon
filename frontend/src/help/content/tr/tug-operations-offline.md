---
key: tug-operations-offline
title: Römorkör çevrimdışı operasyonlar
roles: [Tug Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-risk-briefing, towage-execution, tug-daily-log, offline-conflict-center]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Bu sayfa, bir römorkör işinin **önceden alınmış anlık görüntüsünü** cihazda tutar ve bağlantı yokken yapılan üç tür kaydı **kalıcı, şifreli bir kuyruğa** alır: brifing teyidi, yürütme gözlemi ve günlük defter girişi. Cevapladığı soru şudur: "Bağlantı yokken hangi kayıtları güvenle yapabilirim, kuyrukta ne bekliyor ve sunucu bunları nasıl karşıladı?" Master serbest bırakma, hazırlık GO, yürütme başlatma/geçişleri, doğrulama ve incelemeler çevrimiçi insan kaydı olarak kalır.

## Kim kullanır
- **Tug Master:** bağlantının zayıf olduğu yerde kritik işi sürdürmek ve eşitleme durumunu izlemek için (13.03 §10).
- **Mürettebat:** brifing teyidini ve vardiya kayıtlarını çevrimdışı yapmak, çakışma çıktığında kendi kaydının otomatik kazanacağını varsaymadan çözmek için (13.03 §6).

## Başlamadan önce
- Sayfayı bir römorkör işinden açın; iş seçili değilse sayfa "Anlık görüntüyü almak için bu ekranı bir römorkör işinden açın." gösterir.
- **Çevrimdışı politika** satırında yapılandırılmış bir saklama politikası (`CONFIGURED_UNVERIFIED`) görünmelidir; politika yoksa anlık görüntü alma ve çevrimdışı kayıt kullanılamaz.
- Anlık görüntüyü **çevrimiçiyken** alın. Kayıt yapmadan önce anlık görüntü kirasının dolmadığını kontrol edin.

## Adım adım
1. Çevrimiçiyken **İş anlık görüntüsünü al** düğmesine basın. "Anlık görüntü bu cihazda mühürlendi." mesajı görünür.
2. **İş anlık görüntüsü** bölümünde iş numarasını, durumunu ve **Kira bitişi** zamanını kontrol edin.
3. Yayımlanmış bir brifingin katılımcısıysanız **Brifingi onayla** formunda **Onaylıyorum** veya **İtiraz** (en az 10 karakter gerekçeyle) seçip **Mühürle ve kuyruğa al** düğmesine basın.
4. Süren bir yürütme için **Yürütme gözlemi kaydet** formunda yürütmeyi ve gözlem türünü (Not, Çekme halatı bağlandı, Çekme halatı bırakıldı) seçip notu yazın ve **Mühürle ve kuyruğa al** düğmesine basın.
5. Taslak veya iade edilmiş bir günlük defter için **Günlük defter girişi** formunda defteri seçip **Gün özeti** yazın ve **Mühürle ve kuyruğa al** düğmesine basın.
6. **Kuyruktaki kayıtlar** listesinde her kaydın durumunu izleyin. Bağlantı dönünce kayıtlar sırayla gönderilir; elle göndermek için **Kuyruktakileri gönder** düğmesini kullanın.
7. Bir kayıt **Esaslı çakışma** gösterirse **Sunucuda değişenler** listesini okuyun, **Sunucu kaydını koru** veya **Kaydettiğim değeri güncel kayda uygula** seçeneğinden birini açıkça seçin ve **Çakışmayı çöz** düğmesine basın.

## Durumlar ne anlama gelir
- **Cihazda kuyrukta (gönderilmedi):** kayıt bu cihazda mühürlendi; kuyrukta olmak gönderilmiş olmak değildir.
- **Sunucu kabul etti:** sunucu kaydı aldı. Bu bir onay veya doğrulama değildir.
- **Esaslı çakışma:** kayıt alındıktan sonra sunucudaki kayıt değişti; otomatik birleştirilmez, bir kişinin kararını bekler.
- **Sunucu değeri korundu / Sunucu reddetti:** sunucunun kayda verdiği sonuç.
- **BİLİNMİYOR (çevrimdışı):** çevrimdışıyken politika durumu okunamadı; politika var sayılmaz.

## Yapamazsınız / dikkat
- Yalnızca çevrimiçi: Master serbest bırakma, hazırlık GO, yürütme başlatma ve geçişleri, brifing yayımlama, doğrulama, günlük defter gönderme ve inceleme.
- Esaslı çakışmalar asla otomatik birleşmez; çözüm her zaman açık bir insan seçimidir (13.03 §1).
- Kira süresi dolmuş anlık görüntüyle yeni kayıt yapılamaz; yeni anlık görüntü alın.
- Kayıtlar yeniden gönderimde özgün kimlikleriyle bir kez işlenir; aynı kaydı yeniden oluşturmayın.

## Sorun olursa
- **"Bu kuyruğun cihaz anahtarı kullanılamıyor." mesajı:** kaydedilenler saklanır ancak okunamaz; kayıtları silmeyin ve destek ekibine başvurun.
- **"Sonuç bilinmiyor" mesajı:** aynı kayıt özgün kimliğiyle yeniden denenecek; yeni kayıt oluşturmayın.
- **"Çevrimdışı politika anlık görüntüye izin vermiyor." mesajı:** politika yapılandırması için kiracı yöneticisine başvurun (13.03 §10).
- **Esaslı çakışma:** bu sayfadaki **Çakışmayı çöz** formunu kullanın; çakışmalar **Önemli çakışma merkezi** sayfasında da listelenir (13.03 §10).

## İlgili sayfalar
