---
key: drydock-planning
title: Havuzlama planlaması
roles: [Technical Superintendent, Chief Engineer]
permissions: [drydock.read, drydock.write, drydock.verify]
related: [work-order-center, defects, certificate-command-center, survey-planner, pre-survey-checklist]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Havuzlama planlaması, seçili geminin **havuzlama paketlerini** (proje, tersane, planlanan tarihler ve kapsam kalemleri) tutar. Cevapladığı soru şudur: "Havuzlama kapsamındaki her kalem hangi iş emri, arıza, sertifika veya sörvey kontrol listesine bağlı ve bu bağlantılar güncel mi?" Kapsam bu kayıtlara bağlanır (13.03 §5). **Bağlantı hazırlığı** yalnız bağlantı bütünlüğüdür; klas veya bayrak sörvey sonucu ya da denize elverişlilik beyanı değildir.

## Kim kullanır
- **Teknik süperintendent:** havuzlama paketini hazırlamak, kapsamı kayıtlara bağlamak, planlamak ve tamamlamak için (13.03 §5).
- **Başmühendis:** kapsamın arıza ve iş emri kayıtlarıyla bağlantısını izlemek ve yetkisi varsa başkasının tamamladığı paketi doğrulamak için (13.03 §5).

## Başlamadan önce
- Doğru gemiyi seçin. Gemi seçili değilse sayfa **Devam etmek için çalışma alanından bir gemi seçin.** der.
- Paketleri görmek için havuzlama okuma yetkisi gerekir. Paket oluşturmak, düzenlemek ve durumu değiştirmek için yazma yetkisi, doğrulamak için doğrulama yetkisi gerekir.
- Bağlayacağınız iş emri, arıza, sertifika ve sörvey kontrol listesi kayıtlarının tanımlayıcılarını ve revizyonlarını hazırlayın.

## Adım adım
1. Listeden bir paketi açın ya da **Havuzlama paketi oluştur** ile boş bir taslak oluşturun.
2. **Havuzlama projesi**, **Tersane**, **Planlanan başlangıç** ve **Planlanan bitiş** alanlarını doldurun.
3. **Kapsam kalemi ekle** ile kalem ekleyin; **Kapsam kalemi** türünü, **Başlık**, **İş paketi**, **Klas gerekliliği**, **Yasal gereklilik** ve **Öncelik** alanlarını girin.
4. Her kalemde **Bağlantı ekle** ile **Kayıt türü**, **Kayıt tanımlayıcısı** ve **Sabitlenmiş revizyon** girin.
5. Tamamlama için **Tamamlama kanıtı** bölümüne **Ekle** ile kanıt referansı ekleyin. Ardından **Kaydet** düğmesine basın.
6. **Planlama engelleri** veya **Tamamlama engelleri** listesini kontrol edin. **Durumu değiştir** bölümünde **İşlem** seçip (**Planla**, **Taslağa geri al**, **Havuzlamayı başlat**, **Havuzlamayı tamamla**, **Paketi iptal et**) onaylayın. İptal için not zorunludur. Yalnız mevcut duruma uygun işlemler sunulur; paket yalnız Taslak veya Planlandı durumundayken iptal edilebilir.
7. Tamamlanmış ve sizin tamamlamadığınız bir pakette en az 10 karakterlik **Gerekçe** yazıp **Kaydı doğrula** ya da **Doğrulamayı reddet** düğmesini kullanın.
8. **Zaman çizelgesi** bölümünde planlanan ve fiili tarihleri ve her revizyonu izleyin.

## Durumlar ne anlama gelir
- **Taslak / Planlandı / Devam ediyor / Tamamlandı / Doğrulandı / İptal edildi:** paketin yaşam döngüsü. Tamamlandı ile Doğrulandı aynı şey değildir.
- **Bağlantılar güncel / Dikkat / İnceleme gerekli / Eksik:** bağlantı hazırlığı. Bağlantılar güncel olsa bile bu bir sörvey sonucu değildir.
- **Bağlı / Bağlanmamış / Süresi geçmiş / İnceleme gerekli:** kapsam kaleminin bağlantı durumu (bağlantılardan biri Güncelliğini yitirmiş veya Kullanılamıyor ise İnceleme gerekli).
- **Güncel / Güncelliğini yitirmiş / Kullanılamıyor:** bağlı kaydın sabitlenen revizyonla karşılaştırması. Güncelliğini yitirmiş bağlantıyı inceleyip **Şuna yeniden sabitle** ile yeni revizyona sabitleyebilirsiniz.
- **Bilinmiyor / ayarlanmadı:** öncelik girilmedi. Bilinmiyor; "Normal" ya da "Düşük" anlamına gelmez.

## Yapamazsınız / dikkat
- Sayfa havuzlamanın güvenli olduğuna veya geminin denize elverişli olduğuna karar vermez.
- Kendi tamamladığınız paketi doğrulayamazsınız. Doğrulamanın reddi paketi yeniden **Devam ediyor** durumuna alır.
- Maliyet, para birimi, satıcı ve bütçe yalnız harici referanslardır; burada toplanmaz, onaylanmaz veya sipariş edilmez.
- Tamamlanmış, doğrulanmış veya iptal edilmiş paket düzenlenemez.
- Çevrimdışıyken komutlar gönderilmez.

## Sorun olursa
- **Sunucu eksik veya engelleyici kalemleri listeledi:** engel listesini tamamlayıp yeniden deneyin.
- **Kayıt değişti veya komut çakışıyor:** **En güncelini yükle** ile yeniden yükleyip inceleyin.
- **Sonuç bilinmiyor:** **Yeniden dene** aynı komutu tekrar gönderir.
- **Bu kayda erişiminiz yok:** rol ve gemi bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).
- **Bu bağlı kayıt türlerini okuyamazsınız:** bağlantının sahibi veya kiracı yöneticisiyle yetkiyi kontrol edin.

## İlgili sayfalar
