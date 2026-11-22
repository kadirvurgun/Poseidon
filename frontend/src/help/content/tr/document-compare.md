---
key: document-compare
title: Doküman revizyon karşılaştırma
roles: [Master, DPA, Regulatory / Assurance, Chief Engineer, Crew / OOW]
permissions: [file.read, document.acknowledge, document.restricted.download, document.isps.read]
related: [data-trust-center, audit, sources, today]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Bu sayfa, seçili gemideki bir kontrollü dokümanın **iki revizyonunu salt okunur olarak yan yana** gösterir. Cevapladığı soru şudur: "Bu iki revizyon arasında dosya ve üst veri düzeyinde ne farklı, her revizyonun yayım, doğrulama ve muhafaza geçmişi ne?" Sayfa farkları gösterir; değişiklikleri onaylamaz, yayımlamaz veya değerlendirmez. Bir revizyonu yürürlüğe sokmaz ve öncekinin yerine geçirmez. Kanıt orijinali sessizce değiştirilmez; yeni revizyon öncekinin yerine geçtiğinde önceki revizyonun yayım durumu Yerini aldı olur ve olay muhafaza geçmişinde görünür (13.03 §1).

## Kim kullanır
- **Master / Başmühendis / Mürettebat:** yürürlükteki revizyonu öncekiyle karşılaştırmak ve okuma onayını kaydetmek için (13.03 §2, §5, §6).
- **DPA / Mevzuat ve güvence kullanıcısı:** revizyon geçmişini ve doğrulama kayıtlarını incelemek için (13.03 §4, §8).

## Başlamadan önce
- Gemi seçili değilse sayfa **Yetkili olduğunuz bir gemi seçin.** der ve gemileri listeler. Doğru gemiyi seçin.
- Sayfayı görmek için dosya okuma yetkisi gerekir. Kısıtlı içerik ve ISPS kısıtlı dokümanlar için ayrıca ilgili yetki gerekir.
- İçerik yükleme ve okuma onayı çevrimiçi çalışır.

## Adım adım
1. **Kontrollü dokümanlar** listesinden bir dokümanı açın. Sınıflandırma ve **ISPS kısıtlı** etiketleri satırda görünür.
2. **İki revizyon seçin** bölümünde **Sol (temel)** ve **Sağ (incelenen)** revizyonları seçin. Sayfa ilk olarak en son revizyonu sağa, bir öncekini sola önerir.
3. **Karşılaştır** düğmesine basın. Tarafları çevirmek için **Tarafları değiştir** kullanın.
4. Bayt ifadesini ve **Üst veri karşılaştırması** tablosunu okuyun: **Dosya türü**, **Boyut (bayt)**, **İçerik SHA-256**, **Kabul**, **Yayım**, **Doğrulama** ve zaman alanları.
5. Her tarafta **Onay ve muhafaza geçmişi** listesini inceleyin.
6. İçeriği görmek için **İçeriği yükle (erişim kaydedilir)** düğmesine basın. İçerik atıf filigranıyla gösterilir.
7. **İçeriği yükle** sonrasında yürürlükteki revizyonu okuduysanız **Bu revizyonu okudum ve içeriğe erişebildim.** kutusunu işaretleyip **Okuma onayını kaydet** düğmesine basın.

## Durumlar ne anlama gelir
- **Yayımlanmadı / Yürürlükte / Yerini aldı:** revizyonun yayım durumu. Yürürlükte olmayan revizyonda **Operasyonel yürürlükte değil** etiketi görünür.
- **Doğrulanmadı / Geçerli doğrulandı / Geçersiz doğrulandı / Doğrulama sonuçsuz:** revizyonun doğrulama kaydı. Doğrulanmadı; "geçerli" anlamına gelmez.
- **Aynı / Değişti / Karşılaştırılamaz:** üst veri satırının sonucu. Karşılaştırılamaz; "Aynı" anlamına gelmez.
- **Baytları aynı dosyalar:** yalnız aynı baytlar demektir; içerik incelemesi değildir.
- **Dosyalar bayt düzeyinde farklı:** hangi kuralın değiştiği ve önemi bir kişinin incelemesini gerektirir.
- **İçerik rolünüz için kısıtlı; yalnızca üst veri.** içerik bu rol için açılmaz.

## Yapamazsınız / dikkat
- Bu sayfadan doküman yayımlanamaz, onaylanamaz, yürürlüğe sokulamaz veya başka bir revizyonun yerine geçirilemez.
- Sayfa içerik farkını otomatik yorumlamaz; önemine yetkili bir kişi karar verir.
- Okuma onayı yalnız yürürlükteki revizyon için, kişi ve revizyon başına bir kez kaydedilir. Bu bir onay veya doğrulama değildir.
- Basılı kopya kontrolsüzdür. İçerik önbelleğe alınmaz ve çevrimdışı saklanmaz.
- Çevrimdışıyken karşılaştırma güncel değildir ve yeni içerik yüklenemez.

## Sorun olursa
- **Bu doküman size açık değil:** gemi bağlamınızı ve yetkinizi kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).
- **Bu işlem için yetkiniz yok:** rol ve gemi bağlamınızı kontrol edin (13.03 §10).
- **Kayıt bu arada değişti:** **Yeniden yükle** ile en güncel hâli açın.
- **Revizyon doğrulanamıyor:** kanıt ve kaynak geçmişini kontrol edin; yetkili doğrulayıcıyla görüşün (13.03 §10).
- **İstek başarısız oldu:** ekranı, zamanı ve adımları destek ekibine iletin (13.03 §10).

## İlgili sayfalar
