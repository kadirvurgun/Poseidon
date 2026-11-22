---
key: defects
title: Arıza ve geçici tedbirler
roles: [Chief Engineer, Technical Superintendent, Master, Crew / OOW / Engineer]
permissions: [defect.read, equipment.read, defect.write, defect.verify, defect.effectiveness, defect.close]
related: [pms-command-center, work-order-center, operational-downtime, service-engineer-report, drydock-planning]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Arıza ve geçici tedbirler sayfası, seçili geminin **kayıtlı arızalarını, kısıtlamalarını ve beyan edilen geçici tedbirlerini** tutar. Cevapladığı soru şudur: "Bu arıza ne kadar kritik, hangi önlem veya operasyonel kısıtlama kayıtlı, düzeltici faaliyet doğrulandı mı ve kapanışın önünde ne var?" Her adım bir insan kaydıdır; sayfa emniyet veya işletme yetkisi vermez.

## Kim kullanır
- **Chief Engineer / Technical Superintendent:** kritik arıza için önlem veya kısıtlamayı ve hazırlık etkisini kaydetmek, düzeltici faaliyeti izlemek için (13.03 §5).
- **Master:** arızanın operasyonel kısıtlama etkisini görmek için (13.03 §2).
- **Mühendis / mürettebat:** gördüğü arızayı beyan etmek için (13.03 §6).

## Başlamadan önce
- Okumak için `defect.read` ve `equipment.read` gerekir. Beyan ve yaşam döngüsü kayıtları için `defect.write` gerekir.
- Doğrulama `defect.verify`, etkinlik `defect.effectiveness`, kapanış `defect.close` yetkisi ve oturum açmış bir insan kullanıcı ister.
- **Gemi veya römorkör seçin** listesinden doğru aracı seçin.

## Adım adım
1. Aracı seçin. **Kayıtlı beyanlar** listesini gerekirse **Dönen son süre gözlemlerini filtrele** ile süzün ve bir beyanı açın.
2. Yeni arıza için **Yeni arıza beyanı** düğmesine basın, alanları doldurun ve **Değerlendirilmemiş beyanı kaydet** ile kaydedin. Eksik taslak kaydedilebilir.
3. Kayıtlı beyanda **Arıza yaşam döngüsü** bölümünde **Yaşam döngüsünü başlat** düğmesine basın.
4. **Değerlendirme kaydet** ile kritikliği seçin ve açıklamayı yazın (en az 10 karakter).
5. **Önlem veya kısıtlama kaydet** ile **Önlem türü** olarak **Önlem** veya **Operasyonel kısıtlama** seçin. Geçici önlemde bitiş zamanını (UTC) girin.
6. **Düzeltici faaliyet kaydet** ile yapılan işi yazın.
7. Düzeltici faaliyet kaydetmemiş yetkili kişi **Doğrulama kaydet**, ardından **Etkinlik kaydet** adımlarını kaydeder.
8. **Kapanış engelleri** kalmadıysa yine düzeltici faaliyet kaydetmemiş yetkili kişi **Kapat ve hizmete döndür** ile kapanışı kaydeder. Kayıtlar **Yaşam döngüsü kayıtları** listesinde görünür.

## Durumlar ne anlama gelir
- **Tespit / Değerlendirme / Önlem / Faaliyet / Doğrulama / Etkinlik:** yaşam döngüsü aşaması.
- **Kapalı (kayıtlı insan kararı):** kapanış bir insan kararıdır; "arıza giderildi" diye yeşil bir sonuç değildir.
- **Kritik kuralı uygulanır:** kritik arıza önlem olmadan kapanamaz.
- **Etkin / Süresi doldu — engelleyici / Kaldırıldı / Kapanışta sona erdi:** önlem veya kısıtlamanın durumu. Süresi dolan geçici önlem yenilenmeli veya kaldırılmalıdır.
- **Etkin önlemi olmayan kritik arıza / Kritiklik bilinmiyor ve önlem yok:** uyarılar. Bu arızalar [PMS komuta merkezi](/app/pms-command-center) sayfasında **Durum uyarıları** altında da görünür.
- **Bilinmiyor:** kritiklik değerlendirilmedi. Bilinmiyor; "Düşük" ya da "Kritik değil" anlamına gelmez.

## Yapamazsınız / dikkat
- Düzeltici faaliyeti siz kaydettiyseniz doğrulama, etkinlik ve hizmete dönüş kaydı yapamazsınız; bunlar ayrı yetki ve ayrı kişi ister.
- Doğrulandı, Etkin ve Kapalı ayrı insan kayıtlarıdır. Beyan edilen aşama veya önlem; hizmete dönüş yetkisi, etkinlik doğrulaması ya da kapanış izni vermez.
- Doğrulamadan sonra beyan değişirse kapanış engellenir ve kayıt yeniden incelemeye düşer.
- Ekipman kritikliği bu arızanın değerlendirmesi olarak kopyalanmaz.
- Çevrimdışıyken güncel arıza kaynakları kullanılamaz; çevrimdışı depolama yoktur.

## Sorun olursa
- **Bu kaydı başka biri değiştirdi:** **Yeniden yükle** ile güncel kaydı okuyun ve tekrar kaydedin.
- **Kayıt sonucu bilinmiyor:** **Aynı komutu yeniden dene** aynı komutu gönderir.
- **Bu işlem veya kayıt için sunucu yetkiniz yok:** rol ve gemi bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).
- **Kapanış engelleri:** listedeki eksik kaydı tamamlayın. Kayıt doğrulanamıyorsa kanıtı doğrulayıcıyla kontrol edin (13.03 §10).

## İlgili sayfalar
