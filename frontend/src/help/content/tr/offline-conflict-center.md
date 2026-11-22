---
key: offline-conflict-center
title: Önemli çakışma merkezi
roles: [Crew, Tug Master, Master, Chief Engineer]
permissions: [directory.read, crew.read, mode.tug, tug.read, daily.noon.read, daily.deck.read, daily.engine.read, workorder.read, certificate.read]
related: [tug-operations-offline, tug-daily-log, today]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Önemli çakışma merkezi, **çevrimdışı değişiklikleriniz ile sunucudaki güncel kayıt arasındaki önemli (material) çakışmaları** seçili gemi veya römorkör için listeler ve gerekçeli bir insan kararıyla çözülmesini sağlar. Cevapladığı soru şudur: "Hangi çevrimdışı değişikliğim sunucudaki daha yeni bir kayıtla çatıştı ve bunu nasıl, kim, hangi gerekçeyle çözecek?" Burada hiçbir şey otomatik olarak üzerine yazılmaz veya çözülmez.

## Kim kullanır
- **Mürettebat / Tug Master / Kaptan / Başmühendis:** çevrimdışı çalıştıktan sonra çakışan kendi kayıtlarını görmek ve çözmek için. Çakışma çıktığında kendi kaydınızın otomatik kazanacağını varsaymayın (13.03 §6).
- Önemli çakışma bir insan çözümü gerektirir; bu sayfa 13.03 §10'daki "Material conflict → Conflict Center" akışının hedefidir.

## Başlamadan önce
- Sayfa yalnız sizin kaydettiğiniz çakışmaları listeler ve en az bir okunabilir kaynak (ör. personel ataması, römorkör operasyonları, günlük kayıt, iş emri kanıtı (ekipman ve bakım okuma yetkisiyle birlikte), sertifika yenileme taslağı) için okuma yetkisi gerektirir. Okunabilir tek bir kaynak sayfayı açmaya yeter; listelenen yetkiler birbirinin alternatifidir.
- Liste ve kararlar güncel bir sunucu bağlantısı ister. Çevrimdışıyken yerel değişiklikleriniz cihazda saklanmaya devam eder.
- **Gemi** listesinden gemiyi veya römorkörü seçin.

## Adım adım
1. **Gemi** listesinden bir gemi seçin. Tablo **Kaynak**, **Kayıt**, **Sürümler**, **Yakalandı**, **Sunucuya ulaştı** ve **Durum** sütunlarıyla açılır.
2. "Dahil edilmeyenler" notu varsa okuma izniniz olmayan kaynakların listede yer almadığını bilin.
3. **Açık — karar gerekli** durumundaki satırda **İncele** düğmesine basın.
4. **Çakışma incelemesi** bölümünde her alan için **Yakalama öncesi**, **Yakalanan** ve **Sunucu** değerlerini karşılaştırın. Hiçbir seçim önceden yapılmaz.
5. Her değer için **Sunucu değerini koru** veya **Yakalanan değeri kullan** seçeneğini açıkça seçin, **Çözüm gerekçesi (zorunlu)** alanını doldurun ve **Çözümü gönder** düğmesine basın.
6. Yalnız bu cihazdaki yerel kopyayı kaldırmak istiyorsanız **Yerel değişikliğimi at** bölümünde onay kutusunu işaretleyip **Yerel değişikliği at** düğmesine basın.
7. **İncelemeyi kapat** ile listeye dönün.

## Durumlar ne anlama gelir
- **Açık — karar gerekli:** çakışma bir insan kararı bekliyor.
- **Daha yeni bir çakışmayla geçersiz kaldı:** aynı kayıt için daha yeni bir çakışma oluştu; yenisini inceleyin.
- **Açık kararla çözüldü:** biri gerekçeli kararıyla çözdü. Bu bir operasyonel onay değildir.
- **Gösterilmiyor / Bildirilmedi / Bilinmiyor:** değer yetki gereği gizli, kaydedilmemiş veya okunamamış. Bilinmeyen değer boş veya "Hayır" kabul edilmez.
- **Açık kararınız için sunucu alındısı kaydedildi:** sunucu kararınızı aldı. Bu alındı operasyonel bir onay değildir.

## Yapamazsınız / dikkat
- Çakışmalar asla otomatik birleşmez veya çözülmez; her değer açık bir insan seçimi ve gerekçe ister (13.03 §1).
- Merkez yalnız personel ataması çakışmalarını doğrudan çözer. Diğer kaynaklar için "Bu çakışmayı ilgili kaydın çevrimdışı panelinden çözün." mesajı görünür; örneğin römorkör kayıtları **Römorkör çevrimdışı operasyonlar** sayfasında çözülür.
- Yerel değişikliği atmak yalnız cihazdaki kopyayı kaldırır; sunucu kaydı değişmez ve çakışma gerekçeli bir karar verilene kadar kayıtlı kalır.
- Başka bir cihazda yakalanan değişiklik o cihazda çözülmelidir; hiçbir şey silinmez.
- Gönderim, yetkinizi ve kayıt sürümlerini yeniden kontrol eder; operasyonel onay vermez.

## Sorun olursa
- **"Gönderimin sonucu bilinmiyor" mesajı:** yeniden göndermeden önce **Gönderim sonucunu sorgula** düğmesini kullanın.
- **"Sunucu kaydı incelemenizden sonra değişti" / "Sunucu kaydı yeniden değişti" mesajı:** kararınız uygulanmadı; çakışmayı yeniden inceleyin.
- **"Önemli çakışmalar yüklenemedi. Hiçbir şey çözülmedi.":** **Yeniden dene** düğmesine basın.
- **"Çakışma merkezine erişiminiz yok." / "Bu gemi için çakışma erişimi kullanılamıyor.":** rol ve gemi bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).

## İlgili sayfalar
