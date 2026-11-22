---
key: noon-report
title: Öğle raporu
roles: [Master, Crew / OOW]
permissions: [daily.noon.read, daily.noon.write, daily.noon.review]
related: [deck-daily-log, engine-daily-log, fuel-bunker-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Öğle raporu sayfası, seçili geminin **günlük öğle beyanlarını** kayıtlı kaynak sürümüyle birlikte taslak olarak tutar. Cevapladığı soru şudur: "Rapor zamanı, sefer, konum, mesafe, hava, yük, makine saatleri, yakıt ve ROB için ne beyan ettik ve Kaptan bu kesin sürüm için inceleme kaydetti mi?" Kaydedilen taslak doğrulanmamış bir beyandır. Taslak kaydı kaptan veya başmühendis onayı oluşturmaz; sayfa bundan emisyon, yakıt güvencesi veya hazırlık sonucu çıkarmaz.

## Kim kullanır
- **Raporu hazırlayan zabit (Crew / OOW):** doğru gemi ve tarih için öğle taslağını doldurup kaydetmek için (13.03 §6).
- **Kaptan (Master):** kayıtlı raporu incelemek ve bu incelemeyi insan eylemi olarak kaydetmek için (13.03 §2). Öğle raporu incelemesini yalnız Kaptan görevli bir görevlendirme kaydedebilir.

## Başlamadan önce
- Üst bağlam çubuğunda **doğru şirket ve gemiyi** seçin. Gemi seçili değilse sayfa "Gemi seçin" listesini gösterir.
- Okumak için `daily.noon.read` gerekir. Taslak oluşturup kaydetmek için `daily.noon.write` ve bu gemide düzenleme hakkı gerekir; yoksa "Salt okunur erişim. Düzenleme ve kayıt yetkisi verilmemiş." yazar.
- İnceleme kaydetmek için bu gemide düzenleme hakkıyla `daily.noon.write`, `daily.noon.review` ve bu kesin kayıt için geçerli bir Kaptan görevlendirmesi gerekir.

## Adım adım
1. Gemiyi seçin. **Kayıtlı taslaklar** listesi her raporu tarih ve sürümle, "Taslak · inceleme gerekli" etiketiyle gösterir. Sayfalar için **Önceki taslaklar** / **Diğer taslaklar** düğmelerini kullanın.
2. **Yeni taslak** ile **Yeni günlük taslak** formunu açın veya listedeki bir raporu açarak **Kayıtlı taslağı düzenle** formuna geçin.
3. Grupları doldurun (**Rapor ve sefer**, **Konum ve faaliyet**, **Hava ve yük**, **Makine, yakıt ve ROB**). Her alan için bir **Bilgi durumu** seçin: **Biliniyor**, **Bilinmiyor** veya **Eksik**. Eksik taslaklar kaydedilebilir.
4. **Taslağı kaydet** düğmesine basın. Sayfa "Taslak kaydedildi. İnsan incelemesi ve operasyon onayı ayrıdır." der. Her kayıt **Kayıtlı sürüm geçmişi** altına yeni bir sürüm ekler.
5. Kayıtlı raporda Kaptan **İnceleme kaydı** bölümünü açar; **Yetkili insan görevlendirmesi**, **İnceleme kaydı türü** ("İncelendi beyanını kaydet (değerlendirilmemiş)" veya "Düzeltme için geri gönderme") seçer, **İnceleme gerekçesi** yazar ve **İnceleme kaydını oluştur** düğmesine basar.
6. Yakıt veya liman olayı kaydı gerekiyorsa **Kayıtlı operasyon kaynağını kullan** bölümünden **Kayıtlı kaynaktan yakıt kaydı aç** veya **Kayıtlı kaynaktan liman olayı aç** düğmesini kullanın. Miktar, birim ve onay kopyalanmaz.

## Durumlar ne anlama gelir
- **Taslak · beyanlar doğrulanmamış / Doğrulanmamış beyan:** değerler bir kişi tarafından beyan edilmiştir; kimse doğrulamamıştır.
- **Biliniyor / Bilinmiyor / Eksik:** alanın bilgi durumu. Bilinmiyor; "Hayır", "Hazır" ya da "Uygulanamaz" anlamına gelmez. Eksik, sıfır değildir.
- **Bekliyor · inceleme kaydedilmemiş:** bu sürüm için insan incelemesi kaydedilmemiştir.
- **İnsan üstverisi kaydedilmiş · değerlendirilmemiş / Kaydedilmiş · değerlendirilmemiş:** gösterilen sürüm için Kaptan inceleme kaydı vardır. İnceleme kaydı doğrulama, gönderim veya kaptan emniyet onayı oluşturmaz.
- **Tarihsel sürüm:** inceleme kaydı eski bir sürüme aittir; güncel kayıtlı taslağın incelemesi değildir.

## Yapamazsınız / dikkat
- Sistem raporu incelemez veya kesinleştirmez. İnceleme kaydı, görevlendirilmiş Kaptanın insan eylemidir (13.03 §2).
- Kaydı yazan zabit görevlendirmeye sahipse sistem onun inceleme kaydetmesini engellemez; görev ayrımı bir kuruluş kuralıdır.
- Yeni kayıt yeni sürüm oluşturur; önceki inceleme kendi sürümünde kalır. Değişiklikten sonra yeniden inceleme gerekir.
- Serbest metinle yazılan referans yetkili kaynak seçiminin yerini tutmaz; alanı Eksik veya Bilinmiyor bırakın.
- Çevrimdışıyken yalnız açıkça sınıflandırılmış alanlar şifreli kuyruğa alınır. İnceleme, onay ve doğrulama alanları kapsam dışıdır.

## Sorun olursa
- **"Kayıt değişti. Girdileriniz korunuyor.":** başka biri daha yeni sürüm kaydetti. Yeni denemeden önce açıkça bırakıp yeniden yükleyin.
- **"Kayıt sonucu bilinmiyor":** alanlar kilitlidir; yeniden yazmayın, **Kaydedilen aynı komutu tekrar dene** düğmesini kullanın.
- **"Bu kayıt için güncel, kaynakça yetkilendirilmiş insan görevlendirmesi kullanılamıyor.":** rolünüzü ve gemi bağlamınızı kiracı yöneticinizle kontrol edin (13.03 §10).
- **"İnsan tarafından çakışma çözümü gerekli":** çevrimdışı kayıt sunucuyla çakışıyor. **Çakışmayı incele** bölümünü veya Conflict Center'ı açıp açıkça seçin; hiçbir şey otomatik birleşmez (13.03 §10).
- **Günlük kaynakları kullanılamıyor veya erişim reddedildi:** **Kaynakları okumayı tekrar dene** düğmesini kullanın; sürerse zamanı ve adımlarınızı destek ekibine bildirin.

## İlgili sayfalar
