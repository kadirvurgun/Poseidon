---
key: renewal-workflow
title: Sertifika yenileme
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.renewal.write, certificate.renewal.verify, certificate.renewal.complete, certificate.renewal.owner, file.read]
related: [certificate-command-center, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Sertifika yenileme sayfası, bir sertifikanın yenilenme hazırlığını ve iç iş akışını adım adım kaydeder: hazırlık, başvuru, otorite bekleme, düzenlenen yeni sertifika, doğrulama ve tamamlama. Cevapladığı soru şudur: "Bu sertifikanın yenilenmesi hangi aşamada ve sıradaki adımı kim atacak?" Bu yalnız iç iş akışı kaydıdır; hukuki geçerlilik veya otorite onayı oluşturmaz (13.03 §1, §8).

## Kim kullanır
- **Master, Chief Engineer / Technical Superintendent:** yenileme taslağı hazırlamak, gereksinimleri ve başvuruyu kaydetmek için.
- **Atanmış yenileme sorumlusu:** iş akışını ilerletmek için. Yenileme sorumlusu yetkisine sahip üyelerin taşıdığı bir roldür; sorumlu olmadan iş akışı açılamaz veya doğrulanamaz.
- **Doğrulayıcı ve tamamlayıcı:** düzenlenen yeni sertifikayı kaynakla karşılaştırıp doğrulamak ve ayrı bir kişi olarak yenilemeyi tamamlamak için (13.03 §1, §8).

## Başlamadan önce
- Sayfayı görmek için sertifika okuma yetkisi gerekir. Taslak hazırlamak için o gemide sertifika yazma ve yenileme yazma yetkisi, doğrulamak için yenileme doğrulama, tamamlamak için yenileme tamamlama yetkisi gerekir.
- Önce **Araç seçin** listesinden doğru gemiyi seçin (13.03 §1).
- Yenilenecek sertifikanın güncel revizyonu kayıtlı olmalıdır. Yerine geçmiş bir sertifika için yenileme hazırlanamaz.
- İş akışı adımları canlı bağlantı gerektirir. Çevrimdışıyken "Çevrimdışısınız. Bağlantı gelene kadar iş akışı adımları kullanılamaz." mesajı görünür.

## Adım adım
1. **Kayıtlı yenileme taslakları** listesinden bir **Yenileme kaydı** açın. Yeni bir yenileme için **Yenileme için sertifika seç** ile [Sertifika merkezi](/app/certificate-command-center) sayfasına gidin ve kayıtta **Yenileme hazırla** düğmesine basın.
2. **Sertifika yenileme formu** alanlarını doldurun: **Beyan edilen yenileme hedef tarihi**, sörvey penceresi, **Planlanan konum**, **Atanmış yenileme sorumlusu rolü** ve **Sörvey gereklilik beyanı**, **Belge gereklilik beyanı**, **İşlem gereklilik beyanı**. Bilmediğiniz değeri **Bilinmiyor** bırakın.
3. Taslağı **Doğrulanmamış taslağı kaydet** düğmesiyle kaydedin.
4. **Yenileme iş akışı** bölümünde **Kullanılabilir adımlar** listesinden rolünüze açık adımı seçin, örneğin **Gereksinimlerin tamamlandığını onayla**, **Başvuruyu kaydet** veya **Otorite kararını bekle**. Gerekli alanları doldurup **Adımı kaydet** düğmesine basın.
5. Otorite yeni sertifikayı düzenlediğinde **Düzenlenen yeni sertifikayı kaydet** adımıyla yeni sertifikayı seçin. Kanıt olarak onun kendi belgesi kullanılır.
6. Doğrulayıcı **Yeni sertifikayı doğrula** adımında **Düzenleyen eşleşiyor**, **Sertifika numarası eşleşiyor**, **Konu eşleşiyor**, **Tarihler eşleşiyor** ve **Kapsam eşleşiyor** alanlarını kaynakla karşılaştırarak işaretler.
7. Farklı bir kişi **Tamamla ve eskisinin yerine geçir** adımıyla yenilemeyi tamamlar. Otorite reddettiyse **Otorite reddini kaydet**, hazırlıktan vazgeçildiyse **Hazırlığı iptal et** kullanılır.

## Durumlar ne anlama gelir
- **Taslak / Hazırlanıyor:** yenileme hazırlanıyor; form düzenlenebilir.
- **Başvuruya hazır / Başvuru kaydedildi / Otorite bekleniyor:** başvuru aşaması. Otorite kararı sistemin dışındadır.
- **Düzenlendi, doğrulanmadı:** yeni sertifika kaydedildi ama henüz bir insan tarafından kaynakla karşılaştırılmadı.
- **İnceleyen tarafından doğrulandı:** doğrulayıcı karşılaştırmayı kaydetti; yenileme henüz tamamlanmadı.
- **Tamamlandı:** iç iş akışı kapandı ve yeni sertifika eskisinin yerine geçti. Bu, sertifikanın hukuki geçerliliğine dair bir karar değildir.
- **İptal edildi / Reddedildi:** akış durdu.
- Gereksinim durumu **Tamamlandı / Gerekli değil / Bilinmiyor** olabilir. **Bilinmiyor**, "Gerekli değil" anlamına gelmez; sunucu bilinmeyen gereksinimle akışı ilerletmez.

## Yapamazsınız / dikkat
- Düzenlenen yeni sertifikayı kaydeden veya o yeni sertifika kaydını giren kişi onu doğrulayamaz. Tamamlayan kişi; doğrulayıcıdan, düzenlenen yeni sertifikayı kaydedenden ve yeni sertifika kaydının yazarından farklı olmalıdır.
- Doğrulama ve tamamlama yalnız insan kararıdır; Tamamlandı ≠ Doğrulandı.
- Planlama beyanları tamamlanmış sörvey, onaylı başvuru, sefer uygunluğu veya doğrulama oluşturmaz.
- Taslak değiştirme bağlantıları özgün kaydın yerine geçmez. Eski sertifika ancak tamamlama adımıyla ve izlenebilir bir yerine geçme ilişkisiyle kapanır (13.03 §1).
- Her adım değiştirilemez bir revizyon olarak kaydedilir; geçmiş **Kayıtlı sürüm geçmişi** bölümünde kalır.
- Açıkça atanmış sorumlu yoksa sistem yöneticiyi varsayılan sorumlu yapmaz.

## Sorun olursa
- **Bu durumda rolünüz için kullanılabilir adım yok.** Sıradaki adım başka bir role aittir; yetkinizi ve atamanızı kontrol edin, gerekirse Tenant Admin ile görüşün (13.03 §10).
- **Yenileme yüklendikten sonra değişti.** veya **Bu güncel revizyon değil.** **Yeniden yükle** düğmesine basıp güncel durumu inceleyin.
- **Sonuç bilinmiyor. Yeniden deneme aynı isteği tekrar gönderir.** **Aynı isteği yeniden dene** düğmesini kullanın.
- Doğrulama veya tamamlamada **Yenileme yüklendikten sonra değişti.** görünürse yeni sertifikayı kaydeden veya doğrulayıcı siz olabilirsiniz; o adımı başka bir kişi atmalıdır.
- **Adım kaydedilmedi. Ayrıntıları kontrol edip yeniden deneyin.** Zorunlu alanları ve eşleşme seçimlerini kontrol edin.
- Yeni sertifika doğrulanamıyorsa belgeyi ve kaynak bağını kontrol edip doğrulayıcıya yönlendirin (13.03 §10).

## İlgili sayfalar
