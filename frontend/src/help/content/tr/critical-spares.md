---
key: critical-spares
title: Kritik yedekler ve talepler
roles: [Chief Engineer, Technical Superintendent]
permissions: [spare.read, spare.write, requisition.propose, requisition.approve]
related: [pms-command-center, work-order-center, defects]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Kritik yedekler ve talepler sayfası, seçili geminin **beyan edilen stokunu, minimum ve maksimum eşiklerini, düşük stok uyarılarını ve teknik talep önerilerini** gösterir. Cevapladığı soru şudur: "Hangi kritik yedek minimumuna ulaştı, kaç açık iş emri ona başvuruyor ve teknik talep önerildi mi, karara bağlandı mı?" Sayımlar insan beyanıdır ve bir depoya karşı doğrulanmamıştır.

## Kim kullanır
- **Chief Engineer / Technical Superintendent:** kritik yedeklerin minimum eşiklerini izlemek ve teknik talep başlatmak için (13.03 §5). Talebi öneren ile onaylayan ayrı kişilerdir.

## Başlamadan önce
- Görmek için `spare.read` gerekir. Yedek tanımlamak ve sayım kaydetmek için `spare.write`, talep önermek için `requisition.propose`, karar vermek için `requisition.approve` gerekir.
- Öneri ve karar oturum açmış bir insan kullanıcı ister.
- **Gemi** listesinden doğru gemiyi seçin.

## Adım adım
1. Gemiyi seçin. **Kritik yedekler** görünümü açılır. Üstteki **Düşük stok** ve **İnceleme gerekli** sayılarını okuyun.
2. Tablodaki her satırda **Yedek kritikliği**, **Ekipman kritikliği**, **Stok durumu**, **Öngörülen**, **Min / maks** ve **Açık iş emirleri** sütunlarını okuyun.
3. Yeni yedek için **Yeni kritik yedek**, mevcut için **Düzenle** düğmesine basın. **Parça numarası**, **Ad**, **Birim**, **Minimum stok** ve **Maksimum stok** alanlarını girin. Karar verilmediyse eşikleri boş bırakın.
4. Sayım girmek için **Yeni stok sayımı kaydet** kutusunu işaretleyip **Sayılan miktar** alanını doldurun ve **Kaydet** düğmesine basın.
5. **Düşük stok** satırında, açık talep yoksa **Talep öner** düğmesine basın. Önerilen miktarı sunucu hesaplar (maksimum eksi öngörülen stok). Maksimum tanımlı değilse miktar önerilmez ve onaylayan kişi **Onaylanan miktar** alanını girmelidir.
6. **Teknik talepler** görünümünde öneriyi kendisi yapmamış yetkili kişi **Onaylanan miktar** alanını kontrol edip **ERP dışa aktarımı için onayla** düğmesine basar ya da **Ret nedeni** seçip **Reddet** düğmesine basar.
7. Onaylanan talebin **ERP dışa aktarım referansı** bölümünü açarak referansı görün.

## Durumlar ne anlama gelir
- **Düşük stok:** öngörülen stok (son beyan edilen sayım eksi o sayımdan sonra gönderilen iş emirlerinde kaydedilen yedekler) minimuma eşit veya altında. Talep önerisi bu satırda açılır.
- **Minimumun üzerinde (beyan):** öngörülen stok minimumun üzerinde; sayım doğrulanmış değildir.
- **Minimum yapılandırılmadı:** eşik girilmedi. Boş eşik sağlıklı değil, inceleme gerekli olarak raporlanır.
- **Stok bilinmiyor / Tüketim çözümlenemedi:** stok hesaplanamadı. Bilinmiyor; "yeterli stok" anlamına gelmez.
- **Önerildi / ERP dışa aktarımı için onaylandı / Reddedildi:** teknik talebin durumu.
- **Kritik (beyan) / Kritik değil (beyan) / Bilinmiyor:** yedek kritikliği bir beyandır.

## Yapamazsınız / dikkat
- Kendi önerdiğiniz talebe karar veremezsiniz. Sayfa "Bunu siz önerdiniz; başka bir kişi karar vermelidir." diye belirtir.
- Öneri ve onay ayrı insan işlemleridir. Onay yalnız ERP dışa aktarım referansı oluşturur; satın alma siparişi, tedarikçi veya fiyat burada yönetilmez.
- Sayfada ayrı bir yeniden sipariş eşiği alanı yoktur; düşük stok minimum stoka göre değerlendirilir.
- Çevrimdışıyken veri yenilenemez ve işlem yapılamaz.

## Sorun olursa
- **Kayıt değişti:** **Yeniden dene** ile sayfayı tazeleyip tekrar deneyin.
- **Sonuç bilinmiyor:** **Aynı komutu yeniden dene** aynı komutu gönderir.
- **İstek bir kural tarafından reddedildi:** stok durumu ve açık talep bilgisini kontrol edin.
- **Yetkiniz yok:** rol ve gemi bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).

## İlgili sayfalar
