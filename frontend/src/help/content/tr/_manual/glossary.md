---
key: glossary
title: Durumlar sözlüğü
reviewed: false
---
## Hazırlık ve inceleme durumları
- **READY (Hazır):** gereken her girdi okundu ve kural engel bulmadı. Bu bir kuralın sonucudur, emniyet kararı değildir; kararı yine yetkili kişi verir.
- **CONDITIONAL (Koşullu):** sonuç, Neden görünümünde gösterilen bir koşula bağlıdır.
- **NOT_READY (Hazır değil):** en az bir engel var. Hangisi olduğunu görmek için Neden görünümünü açın.
- **REVIEW_REQUIRED (İnceleme gerekli):** sistem karar veremiyor; örneğin kritik bir girdi bilinmiyor. Yetkili bir kişinin bakması gerekir.
- **UNKNOWN (Bilinmiyor):** değer kaynakta yok veya belirlenemedi. Bilinmiyor hiçbir zaman "Hayır", "Hazır" veya "Uygulanmaz" ile aynı şey değildir. Kritik bir değer bilinmiyorsa sonuç REVIEW_REQUIRED olarak kalır.

## Kayıt yaşam döngüsü
- **Taslak:** kaydedildi ama gönderilmedi. Taslak eksik olabilir; zorunlu veri eksikken gönderim engellenir.
- **Tamamlandı:** işi yapan kişi işi bitirdi.
- **Doğrulandı:** işi kaydeden kişiden farklı, yetkili bir doğrulayıcı onu teyit etti.
- **Etkili:** daha sonraki bir kontrol aksiyonun gerçekten işe yaradığını gösterdi. Tamamlandı ≠ Doğrulandı ≠ Etkili.
- **Yerine geçti:** daha yeni bir kayıt bunun yerine geçer; özgün kayıt değişmeden saklanır ve bağlanır.
- **Görevler ayrılığı:** bazı adımları, önceki bir adımı kaydeden veya doğrulayan kişiden farklı biri atmalıdır. Aksi hâlde sunucu adımı reddeder.

## Kaynaklar ve verinin güncelliği
- **Kullanılabilir (doğrulanmadı):** kaynak okundu; içeriği doğrulanmadı.
- **Bu bağlamda kullanılamıyor:** kaynak bu şirket, araç veya rol için okunamadı. Göremediğiniz bir uyarı her şeyin yolunda olduğu anlamına gelmez.
- **Kısmi görünüm:** bazı kaynaklar okunamadı. Sıfır sayısı "sorun yok" demek değildir.
- **Eski:** gösterilen veri eski bir kopyadır. Yeniden okumak için yenileyin.
- **Okuma / kayıt zamanı:** verinin ne zaman okunduğu veya kaydedildiği. Kaynağın doğru olduğunu kanıtlamaz.

## Çevrimdışı çalışma
- **Çevrimdışı:** bağlantı yok. Ekrandaki veri son kopyadır.
- **Kuyrukta:** değişiklik kalıcı kuyrukta bekler ve bağlantı dönünce gönderilir.
- **Çakışma:** siz çevrimdışıyken kayıt sunucuda değişti. Asla otomatik birleştirilmez; Önemli çakışma merkezi sayfasında çözün.
