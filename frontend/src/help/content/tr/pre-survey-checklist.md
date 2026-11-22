---
key: pre-survey-checklist
title: Sörvey öncesi kontrol listesi
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.survey.plan, certificate.survey.review]
related: [survey-planner, certificate-command-center, renewal-workflow]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Sörvey öncesi kontrol listesi, bir sertifikanın sörveyine hazırlık görevlerini sorumlu, son tarih, kanıt ve bağımsız insan doğrulamasıyla izler. Cevapladığı soru şudur: "Bu sörvey için hangi hazırlık maddeleri açık, hangileri kanıtlı ve başka bir kişi tarafından doğrulandı?" Hazırlık durumu tavsiye niteliğindedir, asla sörvey onayı değildir (13.03 §1, §8).

## Kim kullanır
- **Master, Chief Engineer / Technical Superintendent:** kontrol listesi başlatmak, maddeleri yanıtlamak ve kanıt eklemek için.
- **Doğrulayıcı:** başkasının yanıtladığı maddeleri kanıtla karşılaştırıp doğrulamak veya reddetmek için (13.03 §1).
- Yalnız okuma yetkisi olanlar "Rolünüz için salt okunur." mesajını görür.

## Başlamadan önce
- Sayfayı görmek için sertifika okuma yetkisi gerekir. Kontrol listesi başlatmak ve yanıtlamak için o gemide sörvey planlama yetkisi, doğrulamak için sörvey inceleme yetkisi gerekir.
- **Gemi seçin** ile doğru gemiyi seçin (13.03 §1).
- Sertifikanın sörvey penceresi [Sertifika merkezi](/app/certificate-command-center) sayfasında beyan edilmiş olmalıdır.
- Sayfa canlı okuma gerektirir. Çevrimdışıyken "Çevrimdışı: güncel çevrimiçi okuma gerekir." mesajı görünür.

## Adım adım
1. **Kontrol listeleri** bölümünde gemiye ait listeleri görün veya [Sörvey planlayıcı](/app/survey-planner) sayfasında bir satırda **Kontrol listesini aç** ya da **Kontrol listesi başlat** düğmesine basın.
2. Yeni liste için **Sertifika seçin**, bir **Şablon** seçin ve **Kontrol listesi oluştur** düğmesine basın. Şablon bir başlangıç şablonudur, onaylı katalog değildir.
3. **Sörvey planı girdileri** bölümünde **Planlanan başlangıç**, **Planlanan bitiş**, **Planlanan yer (metin)** ve varsa **Liman uğrağı** alanlarını doldurun.
4. **Maddeler** listesinde her madde için **Sorumlu rol**, **Son tarih**, **Yanıt** ve gerekiyorsa **Bulgu** ve **Yorum** girin. **Kanıt ekle** ile kanıt bağlayın, sonra **Kaydet** düğmesine basın. Her kayıt yeni bir revizyon oluşturur.
5. Doğrulayıcı olarak başkasının yanıtladığı maddelerde **Karar** alanından **Doğrula** veya **Reddet** seçin, gerekçeyi yazın ve **Doğrulamayı kaydet** düğmesine basın.
6. **Hazırlık durumu** ve **Engeller** alanlarını izleyin.

## Durumlar ne anlama gelir
- Madde yanıtı: **Açık**, **Hazır**, **Dikkat gerekiyor**, **Engelli**, **Uygulanamaz**. Yanıtı bilinmeyen madde **Açık** kalır; "Uygulanamaz" veya "Hazır" sayılmaz.
- Doğrulama: **Bekliyor**, **Doğrulandı (insan)**, **Reddedildi**. Yanıtlanmış bir madde doğrulanana kadar **Bekliyor** durumundadır.
- Hazırlık durumu: **Engelli**, **Eksik**, **Hazırlandı, insan onayı bekleniyor**. En iyi durum bile insan onayını bekler; sistem hiçbir zaman "sörvey onaylandı" demez.
- **Kritik / Zorunlu / İsteğe bağlı:** maddenin önemi. Çözülmemiş kritik ve zorunlu maddeler hazırlık durumunda sayılır ve **Engeller** alanında görünür.
- **Sefer: bilinmiyor (sefer modülü yok):** sefer uygunluğu henüz değerlendirilemiyor ve bilinmiyor olarak kalır.

## Yapamazsınız / dikkat
- Kendi yanıtladığınız maddeyi doğrulayamazsınız: "Bu maddeyi siz yanıtladınız; başka bir kişi doğrulamalıdır."
- Bir maddeyi **Hazır** yanıtlamak onu doğrulamaz; Tamamlandı ≠ Doğrulandı. Her doğrulama kararı gördüğünüz tam yanıt ve kanıta bağlanır.
- Hazırlık durumu sörvey onayı, randevu veya sertifika geçerliliği değildir. Klas, bayrak ve otorite koordinasyonu dışsaldır ve burada simüle edilmez.
- Şablon maddeleri onaylı bir katalog değildir; gemiye uygunluğunu yetkili kişi değerlendirir.

## Sorun olursa
- **Bu kontrol listesi siz açtıktan sonra değişti. Düzenlemeden önce yeniden yükleyin.** **Güncelini yeniden yükle** düğmesine basın.
- **Sonuç henüz bilinmiyor. Yeniden deneme aynı isteği gönderir.** **Aynı isteği yeniden dene** düğmesini kullanın.
- **Kaydedilmeyen maddeler:** listelenen maddeleri düzeltip yeniden kaydedin.
- **Bu görünüme erişiminiz yok.** Rol, gemi ve nesne kapsamınızı kontrol edin; gerekirse Tenant Admin ile görüşün (13.03 §10).
- Madde doğrulanamıyorsa kanıtı ve kaynağını kontrol edip doğrulayıcıya yönlendirin (13.03 §10).

## İlgili sayfalar
