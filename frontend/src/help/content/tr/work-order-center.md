---
key: work-order-center
title: İş emri merkezi
roles: [Chief Engineer, Technical Superintendent, Crew / OOW / Engineer]
permissions: [workorder.read, equipment.read, maintenance.read, workorder.write, workorder.release, workorder.execute, workorder.verify, workorder.close]
related: [pms-command-center, defects, critical-spares, service-engineer-report, operational-downtime, offline-conflict-center]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
İş emri merkezi, seçili geminin **taslak iş emirlerini ve uygulama beyanlarını** tutar. Cevapladığı soru şudur: "Bu gemide hangi iş emirleri var, hangisi yayımlandı, uygulandı, kanıtı gönderildi ve bir insan tarafından doğrulandı mı?" Her kayıtlı sürüm değişmez geçmişte saklanır. Doğrulama ve kapanış; hizmete dönüş, izin veya risk onayı değildir.

## Kim kullanır
- **Chief Engineer / Technical Superintendent:** planlı ve plansız iş emirlerini açmak, yayımlamak, doğrulamak ve kapatmak için (13.03 §5).
- **Mühendis / mürettebat:** kendisine verilen işi başlatmak, okuma, yedek parça, fotoğraf ve doküman kanıtını kaydedip göndermek için (13.03 §6).

## Başlamadan önce
- Okumak için `workorder.read`, `equipment.read` ve `maintenance.read` gerekir. Taslak için `workorder.write`, yayım için `workorder.release`, uygulama için `workorder.execute`, doğrulama için `workorder.verify`, kapanış için `workorder.close` gerekir.
- Yayım, doğrulama ve kapanış oturum açmış bir insan kullanıcı ister.
- **Gemi veya römorkör seçin** listesinden doğru aracı seçin.

## Adım adım
1. Aracı seçin. **Kayıtlı iş emirleri** listesinde **Dönen iş emri numaralarında ara** alanıyla bu sayfadaki kayıtları süzün ve bir kaydı açın.
2. Yeni iş için **Yeni iş emri taslağı** düğmesine basın, alanları doldurun ve **Değerlendirilmemiş taslağı kaydet** ile kaydedin. Eksik taslak kaydedilebilir.
3. **Uygulama ve kanıt** bölümünde yayım yetkiniz varsa **Zorunlu kanıt politikası** altında **Zorunlu kanıtı belirle** seçip kanıt türlerini işaretleyin ve **İş emrini yayımla** düğmesine basın.
4. Uygulayıcı **İşe başla** ile işi başlatır. **Okumalar**, **Kullanılan yedek parçalar**, **Fotoğraf ve doküman kanıtı**, **Yapılan iş** ve **Bulgular** alanlarını doldurup **Kanıtı kaydet**, sonra **Kanıtı gönder** düğmesine basar.
5. İşi uygulamamış yetkili doğrulayıcı kanıtı inceler. **Doğrulamayı kaydet** ile doğrular veya bir **İade nedeni** seçip **Uygulayıcıya iade et** ile geri gönderir.
6. Doğrulanmış iş emri için yetkili kişi **Kapanışı kaydet** düğmesine basar.
7. Geçmiş sürümleri **Değişmez iş emri geçmişi** bölümünde görün. Duruş kaydı gerekirse **Bağlı duruş taslağı aç** bağlantısını kullanın.

## Durumlar ne anlama gelir
- **Uygulama için yayımlanmadı / Yayımlandı / Devam ediyor:** iş henüz yayımlanmadı, yayımlandı veya uygulanıyor.
- **Kanıt gönderildi:** uygulayıcı işi ve kanıtı beyan etti. Bu, doğrulandı demek değildir.
- **Doğrulandı (insan kaydı):** işi uygulamamış yetkili bir doğrulayıcı kaydetti. **Kapatıldı (insan kaydı)** ayrı bir adımdır.
- **Zorunlu kanıt eksik / Mevcut, insan doğrulaması bekliyor / Bir insan tarafından doğrulandı:** zorunlu kanıt kapısının durumu.
- **İnceleme gerekli: politika yapılandırılmadı:** zorunlu kanıt belirlenmedi. Yapılandırılmamış, hiçbir şey gerekmediği anlamına gelmez; doğrulama engelli kalır.
- **Doğrulayıcı tarafından iade edildi:** iş, yazan nedenle uygulayıcıya döndü.

## Yapamazsınız / dikkat
- İşi siz uyguladıysanız doğrulayamaz veya kapatamazsınız. Sayfa bunu "Bu işi siz uyguladınız; doğrulayamaz veya kapatamazsınız." diye belirtir.
- Kanıt gönderildi, Doğrulandı ve Kapatıldı ayrı insan kayıtlarıdır. Sayfa bir ekipmanı hizmete hazır ilan etmez.
- Okuma değerleri beyan edildiği gibi saklanır; birim dönüşümü veya eşik uygulanmaz.
- Çevrimdışı kanıt kuyruğu yalnız onaylı çevrimdışı politika varsa ve iş devam ederken kullanılır. Çakışmalar otomatik birleştirilmez; **Çakışma merkezini aç** ile insan karar verir.

## Sorun olursa
- **Kayıt değişti veya bu işlem mevcut durumda izinli değil:** **Yeniden yükle** ile güncel kaydı okuyun ve tekrar deneyin.
- **Sonuç bilinmiyor:** **Aynı komutu yeniden dene** aynı komutu gönderir; yeni kayıt oluşturmaz.
- **Yetki veya insan otoritesi eksik:** rol ve gemi bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).
- **Zorunlu kanıt kapısı bu işlemi reddetti:** eksik kanıt türlerini tamamlayın. Kayıt doğrulanamıyorsa kanıtı ve kaynağını doğrulayıcıyla kontrol edin (13.03 §10).

## İlgili sayfalar
