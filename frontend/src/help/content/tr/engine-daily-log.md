---
key: engine-daily-log
title: Makine günlük kaydı
roles: [Chief Engineer, Crew / OOW]
permissions: [daily.engine.read, daily.engine.write, daily.engine.review]
related: [noon-report, deck-daily-log, fuel-bunker-log, operational-downtime, work-order-center, defects, offline-conflict-center]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Makine günlük kaydı, seçili geminin **makine vardiya kayıtlarını** kayıtlı kaynak sürümüyle taslak olarak tutar. Cevapladığı soru şudur: "Makine vardiyası makine durumu ve saatleri, yakıt ve yağ tüketimi, tatlı su, kritik ölçümler, alarmlar, arızalar, ikmal, sintine ve çevre kontrolleri için ne beyan etti ve Başmühendis bu kesin sürüm için inceleme kaydetti mi?" Kaydedilen giriş doğrulanmamış beyandır. Taslak veriler emisyon, yakıt güvencesi veya hazırlık sonucuna dönüştürülmez.

## Kim kullanır
- **Vardiya mühendisi (Crew / OOW / Engineer):** makine vardiya kaydını gerçek zamanlı ve kronolojik tutmak için (13.03 §6).
- **Başmühendis (Chief Engineer):** kayıtlı jurnali incelemek, iş emri ve arızalarla ilişkilendirmek ve incelemeyi insan eylemi olarak kaydetmek için (13.03 §5). Makine kaydı incelemesini yalnız Başmühendis görevli bir görevlendirme kaydedebilir.

## Başlamadan önce
- Üst bağlam çubuğunda **doğru şirket ve gemiyi** seçin.
- Okumak için `daily.engine.read`, kaydetmek için `daily.engine.write` ve bu gemide düzenleme hakkı gerekir. İnceleme kaydetmek için bu gemide düzenleme hakkıyla `daily.engine.write`, `daily.engine.review` ve bu kesin kayıt için geçerli Başmühendis görevlendirmesi gerekir.
- İş emri veya arıza bağlamak için o kayıtları okuma yetkisi de gerekir.

## Adım adım
1. Gemiyi seçin. **Kayıtlı taslaklar** her kaydı tarih ve sürümle, "Taslak · inceleme gerekli" etiketiyle listeler.
2. **Yeni taslak** düğmesine basın veya listedeki kaydı açarak **Kayıtlı taslağı düzenle** formuna geçin.
3. **Makine vardiya bağlamı**, **Makine durumu ve saatleri**, **Tüketim ve üretim**, **Ölçümler, alarmlar ve kusurlar** ve **İş ve çevre bağlamı** gruplarını doldurun. Her alan için **Biliniyor**, **Bilinmiyor** veya **Eksik** seçin.
4. **Taslağı kaydet** düğmesine basın. Her kayıt **Kayıtlı sürüm geçmişi** altına yeni sürüm ekler.
5. Kayıtlı jurnalde **İş emirleri ve arızalar** bölümünü kullanın: **Kayıt türü** ve **Kayıt** seçip **Bağla** düğmesine basın. Bağlantı tek bir kesin revizyonu sabitler; **Bağlantıyı kaldır** onu kaldırır.
6. Gerekirse **Kayıtlı kaynaktan yakıt kaydı aç** veya **Kayıtlı kaynaktan duruş kaydı aç** düğmesini kullanın. Miktar ve birim kopyalanmaz.
7. Başmühendis **İnceleme kaydı** bölümünü açar; **Yetkili insan görevlendirmesi** ve **İnceleme kaydı türü** seçer, **İnceleme gerekçesi** yazar ve **İnceleme kaydını oluştur** düğmesine basar.

## Durumlar ne anlama gelir
- **Doğrulanmamış beyan / Biliniyor / Bilinmiyor / Eksik:** beyan edilen değerler ve bilgi durumları. Bilinmiyor; "Hayır", "Hazır" ya da "Uygulanamaz" anlamına gelmez.
- **Bekliyor · inceleme kaydedilmemiş:** bu sürüm için insan incelemesi yoktur.
- **İnsan üstverisi kaydedilmiş · değerlendirilmemiş:** bu sürüm için Başmühendis inceleme kaydı vardır. Doğrulama veya başmühendis imzası oluşturmaz.
- **Güncel revizyon / Daha yeni revizyon var:** bağlı iş emri veya arıza bağlandıktan sonra değişti mi. Bağlantı yalnızca soy kaydıdır; o kaydı doğrulamaz, kapatmaz veya onaylamaz.
- **arama kullanılamıyor — BİLİNMİYOR:** bağlı kayıt türü erişiminizle okunamıyor.

## Yapamazsınız / dikkat
- Sistem jurnali incelemez veya kesinleştirmez. İnceleme, görevlendirilmiş Başmühendisin insan eylemidir (13.03 §5).
- Kaydı yazan zabit görevlendirmeye sahipse sistem onun inceleme kaydetmesini engellemez; görev ayrımı bir kuruluş kuralıdır.
- Kayıtları gerçek zamanlı ve kronolojik tutun; başkasının incelenmiş kaydını değiştirmeyin (13.03 §6).
- Burada arıza bağlamak kısıtlama kaydetmez veya arızayı kapatmaz; bunu arıza veya iş emri sayfasında yapın (13.03 §5).
- Bağlantılar güncel çevrimiçi bağlantı gerektirir; çevrimdışı bağlantı yapılmaz. Çevrimdışı çakışmalar asla otomatik birleşmez.

## Sorun olursa
- **"Kayıt değişti. Bağlantıları değiştirmeden önce yeniden yükleyin.":** yeniden yükleyip tekrar bağlayın.
- **"Kayıt sonucu bilinmiyor":** yeniden yazmayın, **Kaydedilen aynı komutu tekrar dene** düğmesini kullanın.
- **"İnsan tarafından çakışma çözümü gerekli":** **Çakışmayı incele** bölümünü veya Conflict Center'ı açıp açıkça seçin (13.03 §10).
- **Görevlendirme yok veya erişim reddedildi:** rolünüzü ve gemi bağlamınızı kiracı yöneticinizle kontrol edin (13.03 §10).

## İlgili sayfalar
