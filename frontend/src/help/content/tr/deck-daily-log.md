---
key: deck-daily-log
title: Güverte günlük kaydı
roles: [Crew / OOW, Master]
permissions: [daily.deck.read, daily.deck.write, daily.deck.review]
related: [noon-report, engine-daily-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Güverte günlük kaydı, seçili geminin **güverte vardiya kayıtlarını** kayıtlı kaynak sürümüyle taslak olarak tutar. Cevapladığı soru şudur: "Vardiya bu tarih ve dönem için konum, pruva yönü, hız, hava, görüş, seyir, liman ve yük işleri, balast, güverte işi, emniyet gözlemi ve devir hakkında ne beyan etti ve bu kesin sürüm için inceleme kaydedildi mi?" Kaydedilen giriş doğrulanmamış beyandır, onaylı jurnal değildir.

## Kim kullanır
- **Vardiya zabiti (Crew / OOW):** vardiya kaydını gerçek zamanlı ve kronolojik tutmak için (13.03 §6).
- **Kaptan veya Birinci Zabit:** kayıtlı jurnali incelemek ve bu incelemeyi insan eylemi olarak kaydetmek için (13.03 §2). Güverte kaydı incelemesini yalnız Kaptan veya Birinci Zabit görevli bir görevlendirme kaydedebilir.

## Başlamadan önce
- Üst bağlam çubuğunda **doğru şirket ve gemiyi** seçin. Gemi seçili değilse sayfa "Gemi seçin" listesini gösterir.
- Okumak için `daily.deck.read` gerekir. Oluşturup kaydetmek için `daily.deck.write` ve bu gemide düzenleme hakkı gerekir; yoksa "Salt okunur erişim. Düzenleme ve kayıt yetkisi verilmemiş." yazar.
- İnceleme kaydetmek için bu gemide düzenleme hakkıyla `daily.deck.write`, `daily.deck.review` ve bu kesin kayıt için geçerli Kaptan veya Birinci Zabit görevlendirmesi gerekir.

## Adım adım
1. Gemiyi seçin. **Kayıtlı taslaklar** her kaydı tarih ve sürümle, "Taslak · inceleme gerekli" etiketiyle listeler.
2. **Yeni taslak** ile **Yeni günlük taslak** açın veya listedeki kaydı açarak **Kayıtlı taslağı düzenle** formuna geçin.
3. Önce **Vardiya bağlamı** grubunu doldurun (**Kayıt tarihi**, **Vardiya / dönem**, **Vardiya zabiti referansı**). Zabit listesi yalnız beyan edilen kayıt tarihine uygun kişileri sunar.
4. **Seyir ve hava**, **Liman ve yük faaliyetleri** ve **Güverte işi ve devir** gruplarını doldurun. Her alan için **Biliniyor**, **Bilinmiyor** veya **Eksik** seçin.
5. Vardiya sırasında veya hemen sonunda **Taslağı kaydet** düğmesine basın. Her kayıt **Kayıtlı sürüm geçmişi** altına yeni sürüm ekler; önceki sürümler değişmez.
6. Kaptan veya Birinci Zabit **İnceleme kaydı** bölümünü açar; **Yetkili insan görevlendirmesi** ve **İnceleme kaydı türü** seçer, **İnceleme gerekçesi** yazar ve **İnceleme kaydını oluştur** düğmesine basar.

## Durumlar ne anlama gelir
- **Taslak · beyanlar doğrulanmamış / Doğrulanmamış beyan:** bir kişi beyan etmiştir; doğrulanmamıştır.
- **Biliniyor / Bilinmiyor / Eksik:** alanın bilgi durumu. Bilinmiyor; "Hayır", "Hazır" ya da "Uygulanamaz" anlamına gelmez.
- **Bekliyor · inceleme kaydedilmemiş:** bu sürüm için insan incelemesi yoktur.
- **İnsan üstverisi kaydedilmiş · değerlendirilmemiş:** bu sürüm için inceleme kaydı vardır. Doğrulama, gönderim veya kaptan emniyet onayı oluşturmaz.
- **İncelendi beyanını kaydet (değerlendirilmemiş) / Düzeltme için geri gönderme:** incelemecinin kaydedebileceği iki inceleme türü.
- **Tarihsel sürüm:** inceleme eski bir sürüme aittir, güncel taslağa değil.

## Yapamazsınız / dikkat
- Sistem jurnali incelemez veya kesinleştirmez. İnceleme, görevlendirilmiş Kaptan veya Birinci Zabitin insan eylemidir (13.03 §2).
- Kaydın herhangi bir revizyonunu yazan kişi onu inceleyemez. Yazara görevlendirme sunulmaz ve sayfa "Bu kayıt revizyonunu siz yazdınız; incelemeyi bağımsız bir inceleyici kaydetmelidir." der; sunucu da böyle bir incelemeyi görev ayrılığı nedeniyle reddeder.
- Kayıtları gerçek zamanlı ve kronolojik tutun; eski bir vardiyayı yeniymiş gibi yeniden yazmayın (13.03 §6).
- Başkasının incelenmiş kaydını değiştirmeyin; sonraki kayıt yeni sürümdür ve yeni inceleme gerektirir (13.03 §6).
- Çevrimdışı kayıt yalnız açıkça sınıflandırılmış alanları kapsar. Çevrimdışı çakışma asla otomatik birleşmez; kendi kaydınızın kazanacağını varsaymayın (13.03 §6).

## Sorun olursa
- **"Kayıt değişti. Girdileriniz korunuyor.":** yeni denemeden önce açıkça bırakıp yeniden yükleyin.
- **"Kayıt sonucu bilinmiyor":** alanları yeniden yazmayın, **Kaydedilen aynı komutu tekrar dene** düğmesini kullanın.
- **Zabit referansı kullanılamıyor:** alanı Eksik veya Bilinmiyor bırakın; yazılan isim kaynak seçiminin yerini tutmaz.
- **"İnsan tarafından çakışma çözümü gerekli":** **Çakışmayı incele** bölümünü veya Conflict Center'ı açıp açıkça seçin (13.03 §10).
- **Görevlendirme yok:** rolünüzü ve gemi bağlamınızı kiracı yöneticinizle kontrol edin (13.03 §10).

## İlgili sayfalar
