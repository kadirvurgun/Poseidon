---
key: certificate-command-center
title: Sertifika merkezi
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.extract, certificate.verify.record, file.read, directory.write]
related: [renewal-workflow, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Sertifika merkezi, seçili geminin sertifika **beyanlarını** ve beyan edilen takvim durumlarını tek listede gösterir. Cevapladığı soru şudur: "Bu gemide hangi sertifikaların bitişi veya sörvey penceresi yaklaşıyor ve hangi kayıt hâlâ insan incelemesi bekliyor?" Buradaki tarihler beyandır. Sayfa bir sertifikanın geçerli ya da doğrulanmış olduğuna karar vermez (13.03 §1, §8).

## Kim kullanır
- **Master, Chief Engineer / Technical Superintendent:** gemideki sertifika takvimini izlemek, yeni sertifika taslağı girmek ve yenileme hazırlığı başlatmak için.
- **Regulatory / Assurance kullanıcısı:** resmî ve sürümlü kaynağa dayanan beyanları kontrol etmek ve atanmış denetçi beyanını kaydetmek için (13.03 §8).
- Yalnız okuma yetkisi olan kullanıcılar listeyi görür, değişiklik yapamaz.

## Başlamadan önce
- Sayfayı görmek için sertifika okuma yetkisi gerekir. Taslak girmek için sertifika yazma yetkisi ve o gemide düzenleme kapsamı, belge önerisi çıkarmak için belge okuma ve çıkarım yetkisi, insan beyanı kaydetmek için dizin yazma yetkisi, doğrulama kaydı yetkisi ve bu gemi ve sertifika için güncel bir **Auditor** ataması gerekir. Korumalı mürettebat beyanları ayrıca personel okuma, personel sertifikası okuma, personel kimliği okuma ve belge okuma yetkisi gerektirir.
- Önce doğru gemiyi seçin: **Araç seçin** listesinden gemiye basın (13.03 §1).
- Sayfa çevrimiçi okuma gerektirir. Çevrimdışıyken "Çevrimdışı veya gizliyken güncel sertifika kaynakları kullanılamıyor." mesajı görünür ve çevrimdışı yetki türetilmez.

## Adım adım
1. **Araç seçin** listesinden gemiyi açın.
2. **Sertifika kapsamı** ile **Araç sertifikaları** ya da yetkiniz varsa **Korunan mürettebat beyanları** görünümünü seçin. **Beyan takvim filtresi** ile listeyi daraltın.
3. Bir satıra basarak kaydı açın. Satırlar **Doğrulanmamış sertifika beyanı** etiketiyle görünür.
4. Yeni kayıt için **Yeni sertifika taslağı** düğmesine basın. **Belge adı**, **Beyan edilen mevzuat dayanağı**, **Beyan edilen bayrak**, sörvey penceresi ve **Beyan edilen kullanılabilirlik** alanlarını kaynak belgeden doldurun, sonra **Doğrulanmamış taslağı kaydet** düğmesine basın.
5. Bağlı belgeden öneri almak için **Belge bilgilerini incele** bölümünde **Bağlı belgeden bilgileri çıkar** düğmesine basın. Her öneriyi kaynakla karşılaştırın, **Bu bilgiyi kaynakla karşılaştırdım** ve **Belgenin bu gemi ve sertifikaya ait olduğunu kontrol ettim** kutularını işaretleyin, ardından **Seçilen bilgileri onayla ve taslağı kaydet** düğmesine basın.
6. Atanmış denetçi olarak **İnsan inceleme kaydı** bölümünde **Gerçek atamayı seç** ve **Kayıtlı beyanı seç** alanlarını doldurup gerekçeyi yazın, sonra **Değerlendirilmemiş beyanı kaydet** düğmesine basın.
7. Yenileme başlatmak için kayıtta **Sertifikaya bağlı işler** altındaki **Yenileme hazırla** düğmesini kullanın. Bu sizi [Sertifika yenileme](/app/renewal-workflow) akışına götürür.

## Durumlar ne anlama gelir
- **Beyan bitişi <90 gün / <60 gün / <30 gün / Beyan bitişi geçmiş:** beyan edilen bitiş tarihine göre takvim gözlemi. Sertifikanın geçerliliği hakkında karar değildir.
- **Beyan sörvey aralığı açık / Hazırlık aralığı dışında:** beyan edilen sörvey penceresine göre konum.
- **Takvim incelemesi gerekli:** tarih eksik, belirsiz veya çelişkili. Bir insan kaynak belgeden kontrol etmeden kayıt hazır sayılmaz.
- **Bilinmiyor:** değer beyan edilmemiş. Bilinmeyen değer "eksik değil" veya "uygun" anlamına gelmez; inceleme gerektirir.
- **Düşük güven · incele / Belirsiz · incele / İnceleme gerekli:** belge önerisi henüz insan tarafından onaylanmadı. Güven skoru doğrulama değildir.
- "Yalnız bu sayfa. Takvim gözlemleri nitelendirmeden ayrıdır; bunlar filo genelinde hazırlık toplamları değildir."

## Yapamazsınız / dikkat
- Taslak kaydetmek sertifikayı doğrulamaz. Kaydı yazmak, belge önerisini onaylamak ve denetçi beyanı ayrı adımlardır; Tamamlandı ≠ Doğrulandı.
- Otomatik çıkarım yalnız öneridir. Eksik değerler bilinmiyor kalır ve yalnız seçip düzelttiğiniz bilgiler taslağı günceller.
- Yönetici rolü tek başına insan beyanı kaydetme izni vermez; güncel atama ve kaynak dosyası gerekir.
- Kayıtlı bir sertifika sessizce değiştirilmez. Her kayıt yeni bir revizyon oluşturur ve eski revizyonlar **Kayıtlı sürüm geçmişi** bölümünde kalır (13.03 §1).
- Sayfa klas, bayrak veya otorite onayı vermez ve sefer uygunluğu hakkında karar vermez. Bu karar yetkili kişilerindir.

## Sorun olursa
- **Sertifika kaynağı veya erişimi kullanılamıyor. Geçerli, boş veya doğrulanmış envanter varsayılmaz.** **Kaynak okumayı tekrar dene** düğmesine basın. Sürerse rol, gemi ve nesne kapsamınızı kontrol edin; gerekirse Tenant Admin ile görüşün (13.03 §10).
- **Sonuç bilinmiyor.** Özgün komutu koruyun ve **Aynı sertifika komutunu tekrar dene** düğmesiyle aynı isteği yeniden gönderin.
- **Güncel kaynak değişti.** Yerel alanlarınız korunur; **Kayıtlara dön** ile güncel kaydı yeniden okuyup değişikliğinizi tekrar yapın.
- **Bu belge için otomatik okuma kullanılamıyor:** değerleri kaynak belgeden elle girin.
- Kayıt doğrulanamıyorsa kanıt ve kaynak bağını kontrol edin ve doğrulayıcıya yönlendirin (13.03 §10).

## İlgili sayfalar
