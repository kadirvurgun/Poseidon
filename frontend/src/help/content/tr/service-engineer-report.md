---
key: service-engineer-report
title: Servis ve mühendis raporları
roles: [Chief Engineer, Technical Superintendent, Crew / OOW]
permissions: [service.report.read, equipment.read, service.report.write, service.report.verify, file.read]
related: [work-order-center, defects, pms-command-center, today]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Bu sayfa, seçili gemide yapılan servis ve mühendis ziyaretlerinin **raporlarını** kaydeder ve listeler. Cevapladığı soru şudur: "Hangi ekipmanda, hangi iş emri için kim ne iş yaptı, hangi parçalar ve ölçümler kaydedildi, rapor doğrulandı mı?" Rapor ekipman kimliğine, iş emrine ve arızalara bağlanır; böylece iş emri ve ekipman geçmişiyle ilişkilendirilir (13.03 §5). Doğrulanmış rapor bir insan kaydıdır; arızayı veya iş emrini kapatmaz.

## Kim kullanır
- **Başmühendis / Teknik süperintendent:** raporu ekipman ve iş emri geçmişine bağlamak, eksikleri izlemek ve yazarı olmadığı raporu doğrulamak için (13.03 §5).
- **Mühendis:** servis sonrası raporu taslak olarak girmek ve doğrulamaya göndermek için (13.03 §6).

## Başlamadan önce
- Gemi seçili değilse sayfa **Bir gemi seçin.** der ve kapsamınızdaki gemileri listeler. Doğru gemiyi seçin.
- Raporları görmek için servis raporu ve ekipman okuma yetkisi gerekir. **Yeni rapor** düğmesi yalnız yazma yetkiniz varsa ve çevrimiçiyseniz görünür.
- Bağlayacağınız ekipman, iş emri, arıza ve kanıt dosyası kimliklerini önceden hazırlayın. İş emri, arıza ve kanıt alanları yalnız bu kayıtları okuma yetkiniz varsa görünür.

## Adım adım
1. **Raporlar** listesinden bir raporu açın ya da **Yeni rapor** ile yeni taslak başlatın.
2. **Rapor no.**, **Servis sağlayıcı**, **Mühendis**, **Servis tarihi**, **Neden ve kapsam** ve **Yapılan iş** alanlarını doldurun.
3. Gerekirse **Parça ekle** ve **Ölçüm ekle** ile kullanılan parçaları ve ölçümleri girin. Ölçüm sonucunu bilmiyorsanız **Bilinmiyor** olarak bırakın.
4. **Bağlantılar** bölümünde **Ekipman kimliği**, **İş emri kimliği**, bağlı arıza ve kanıt dosyası kimliklerini (satır başına bir) yazın.
5. **Taslağı kaydet** ile kaydedin. Raporda **Göndermeden önce eksik olanlar** listesi varsa önce bunları tamamlayın.
6. **Doğrulamaya gönder** ile raporu gönderin.
7. Yazarı olmadığınız gönderilmiş bir raporda en az 10 karakterlik **Gerekçe** yazıp **Doğrulamayı kaydet** ya da **Yazara iade et** düğmesini kullanın.
8. Doğrulanmış raporu değiştirmek gerekiyorsa gerekçe yazıp **Düzelt (yeni revizyon)** kullanın. **Değişmez geçmiş** bölümünde her revizyon kalır.

## Durumlar ne anlama gelir
- **Taslak / Gönderildi / İade edildi:** raporun yaşam döngüsü. Gönderim doğrulama demek değildir.
- **Doğrulandı (kayıtlı insan kararı) / Yazar olmayan kişi doğruladı:** raporu yazmamış ve göndermemiş yetkili bir kişi doğrulama kaydı oluşturdu. Tamamlandı ile Doğrulandı aynı şey değildir.
- **Doğrulama bekleniyor:** rapor gönderildi, henüz doğrulanmadı.
- **Önceki bir revizyon doğrulandı; bu revizyon doğrulanmadı:** düzeltmeden sonra yeni revizyon yeniden doğrulama ister.
- **güncel revizyon / bağlandıktan sonra değişti / kullanılamıyor:** bağlı ekipman, iş emri veya arızanın bağlandığı revizyona göre durumu.
- **Kabul edilmiş aktarım, doğrulanmış kanıt değil:** ekli dosya yalnız kabul edilmiş bir aktarımdır.
- **Bilinmiyor** ölçüm sonucu "Limitler içinde" anlamına gelmez.

## Yapamazsınız / dikkat
- Kendi yazdığınız veya gönderdiğiniz raporu doğrulayamazsınız; sunucu bu isteği reddeder.
- Doğrulanmış rapor arızayı veya iş emrini kapatmaz, ekipmanın emniyetli olduğunu da göstermez.
- Kanıt orijinali değiştirilmez; düzeltme yeni revizyon açar, eski revizyon geçmişte kalır (13.03 §1).
- Çevrimdışıyken hiçbir şey kaydedilmez. Bağlantı dönünce **Aynı komutu yeniden dene** ile aynı komutu gönderebilirsiniz.

## Sorun olursa
- **Zorunlu veriler veya kayıtlar eksik:** eksikler listesini tamamlayıp yeniden kaydedin.
- **Bu kaydı başka biri değiştirdi:** **Yeniden yükle** ile en güncel hâli açın, sonra yeniden kaydedin.
- **Bu işlem veya kayıt için sunucu yetkiniz yok:** rol ve gemi bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).
- **Rapor doğrulanamıyor:** bağlı kayıtları ve kanıt dosyalarını kontrol edin; yetkili doğrulayıcıyla görüşün (13.03 §10).
- **Kayıt yüklenemedi:** hata ekranını, zamanı ve adımları destek ekibine iletin (13.03 §10).

## İlgili sayfalar
