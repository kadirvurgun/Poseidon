---
key: safety-events
title: Olaylar ve ramak kala
roles: [DPA / HSEQ, Master, Crew / OOW / Engineer]
permissions: [hse.safety_event.read, hse.safety_event.report, hse.safety_event.triage, hse.safety_event.identity.read, hse.safety_event.investigate, hse.safety_event.review, hse.safety_action.write, hse.safety_action.verify, hse.safety_action.effectiveness]
related: [voice-event-draft, risk-assessment, high-risk-work, industry-assurance]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Olaylar ve ramak kala sayfası, bir gemide yaşanan emniyet olayını hızlıca bildirmenizi ve bildirimin sonraki insan adımlarını izlemenizi sağlar: ön değerlendirme, inceleme, bağımsız onay, düzeltici ve önleyici aksiyonlar, doğrulama, etkinlik ve kapanış. Cevapladığı soru şudur: "Ne oldu, kim ele alıyor ve alınan aksiyon gerçekten işe yaradı mı?" Sayfa şiddeti, kök nedeni veya kapanışı otomatik belirlemez; hepsi ayrı insan kayıtlarıdır. Bulgu → Aksiyon → Doğrulama zincirini izler ve **Kapalı, etkili demek değildir** (13.03 §4).

## Kim kullanır
- **Mürettebat / OOW / Mühendis ve Master:** ramak kala, olay, kaza, yorgunluk, çevresel veya siber olay gibi kayıtları bildirmek için (13.03 §2, §6).
- **DPA / HSEQ:** emniyet köprüsü olarak kritik olayları ve gecikmiş aksiyonları izlemek, inceleme ve aksiyon etkinliğini yönetmek için (13.03 §4).
- Ön değerlendirme, inceleme, onay, kimlik okuma, aksiyon yazma, doğrulama ve etkinlik değerlendirme ayrı yetkilerdir; her kullanıcı yalnız yetkisi olan bölümü görür.

## Başlamadan önce
- Doğru gemiyi seçin (**Gemi seçin**). Gemiye erişiminiz yoksa "Bu gemi için olay kayıtlarına erişiminiz yok." mesajı görünür.
- Bildirim için olay bildirme yetkisi, listeyi okumak için olay okuma yetkisi gerekir.
- **Gizli** bildirim ve **Bildirimi gönder** bağlantı gerektirir. Çevrimdışıyken yalnız açık kimlikli taslak kaydedilebilir ve bunun için cihazın önceden hazırlanmış olması gerekir.
- Saat UTC olarak girilir.

## Adım adım
1. **Olaylar** listesinde gemideki olayları görün. Yalnız kendi bildirimlerinizi görmek için **Yalnızca benim bildirimlerim** seçeneğini kullanın.
2. Yeni kayıt için **Olay bildir** düğmesine basın. **Bildirim türü** olarak **Açık kimlikli** veya **Gizli** seçin. Gizli bildirimde takip isterseniz **Bana gizli bir takip kodu ver** seçeneğini işaretleyin.
3. **Olay türü**, **Ne zaman oldu?**, **Operasyon**, **Gemideki yer**, **Özet**, **Ne oldu**, **Acil önlem alındı mı?** ve isterseniz **Şiddet tahmininiz** alanlarını doldurun. Saat bilinmiyorsa **Bilinmiyor** seçip **Saat neden bilinmiyor?** alanını doldurun.
4. Eksik taslakla **Taslağı kaydet** düğmesini kullanabilirsiniz. **Göndermeden önce gerekenler** listesi boşalınca **Bildirimi gönder** düğmesine basın. Gönderilmemiş taslağı **Taslağı geri çek** ile gerekçe yazarak geri çekebilirsiniz.
5. Gizli takip kodu verildiyse **Gizli takip kodunuz** kutusundaki kodu hemen gizli bir yerde saklayın ve **Sakladım** düğmesine basın. Kod yalnız bir kez gösterilir. Daha sonra **Gizli takip** bölümünde **Takip kodu** girip **Aç** düğmesiyle ekip yanıtlarını okuyabilirsiniz.
6. Ön değerlendirme yetkiliniz **Ön değerlendirme** bölümünde şiddeti ve **İnceleme gerekli mi** değerini gerekçeyle girip **Ön değerlendirmeyi kaydet** düğmesine basar ya da **Bildirene iade et** ile iade eder.
7. İnceleyici **İncelemeyi başlat**, ardından **Katkıda bulunan faktörler**, **Kök nedenler**, **Bariyerler** ve **Belirsizlikler** bölümlerini doldurur, **İncelemeyi kaydet** ve **Onaya gönder** düğmelerine basar. **Bağımsız onay** bölümünde başka bir kişi **Onayla**, **İade et** veya **Reddet** kararı verir.
8. **Düzeltici ve önleyici aksiyonlar** bölümünde **Aksiyon ekle** ile sorumlu, termin ve ele aldığı bulguyu girin. Sorumlu **Tamamlandığını bildir**, başka bir kişi **Doğrula**, üçüncü bir kişi **Etkinliği değerlendir** adımını kaydeder.
9. **Kapanışı engelleyenler** listesi boşaldığında, inceleme yetkisi olan ve ne bildiren ne de inceleme yazarı olan bir gözden geçiren **Olayı kapat** düğmesine basar. **Mesajlar** bölümünde **Yalnızca ekip** veya **Bildirenle paylaşılan** görünürlükle mesaj gönderebilirsiniz.

## Durumlar ne anlama gelir
- **Taslak / Gönderildi / İade edildi / Ön değerlendirildi / İnceleniyor / Aksiyon takibi / Kapalı / Geri çekildi:** olayın yaşam döngüsü.
- **Şiddet — Ön değerlendirme yapılmadı / Değerlendirilmedi:** henüz insan sınıflandırması yok. **Bildirenin tahmini** yalnız bir öneridir. **Şiddet politikası: yapılandırılmadı – yalnızca insan sınıflandırması** sistemin şiddet türetmediğini gösterir.
- **İnceleme ihtiyacı BİLİNMİYOR:** karar verilmemiştir; "hayır" anlamına gelmez ve kapanışı engeller.
- **Aksiyon — Açık / Tamamlandı / Doğrulandı:** Tamamlandı, doğrulandı demek değildir; Doğrulandı da etkili demek değildir.
- **Etkinlik — Bekliyor / Etkili / Etkili değil / Sonuçsuz / Aksiyon yok / Görünmüyor:** etkinlik, aksiyon başına insan değerlendirmesidir ve kapanıştan ayrıdır. **Aksiyon yok** etkili anlamına gelmez.
- **Açık emniyet olayları** özeti: **Açık yüksek şiddetli olay var**, **İnceleme gerekli** veya **Açık yüksek şiddetli olay yok**. Bu özet bir uyarıdır; hazır olma kararı vermez.
- **Gizli (korunuyor):** bildirenin kimliği kısıtlı kasadadır ve okuyucuya gösterilmez.
- **Çevrimdışı kaydedildi / Senkronizasyon bekliyor / Bu cihaza kaydedildi. Henüz gönderilmedi.** Kuyrukta olmak gönderildi demek değildir.

## Yapamazsınız / dikkat
- Bildiren kendi bildirimini değerlendiremez. İnceleme yazarı, gönderen veya bildiren bağımsız onay veremez.
- Tamamlandığını yalnız aksiyon sorumlusu bildirir. Sorumlu da tamamlayan da doğrulayamaz; sorumlu, tamamlayan veya doğrulayan kişilerden hiçbiri etkinliği değerlendiremez. Bu adımlar farklı kişilerin ayrı kayıtlarıdır.
- Kök nedenler insan ifadeleridir; sistem asla kök neden önermez.
- Gönderilmiş bir bildirim yazarı tarafından geri çekilemez veya silinemez.
- Gizli bildirimde kimlik yalnız bilmesi gereken (need-to-know) ve kimlik okuma yetkisi olan kişiye, **Amaç** seçilerek açılır; her okuma amacıyla birlikte kaydedilir. Anonim bildirim için genel giriş kanalı gerekir ve bu kanal yapılandırılmamıştır.
- Sonuçsuz değerlendirme tekrarlanabilir; Etkili ve Etkili değil sonuçları kesindir; işe yaramayan aksiyon için yeni bir takip aksiyonu açılmalıdır.

## Sorun olursa
- **Sunucu komutu reddetti:** altında listelenen eksik veya engelleyen öğeleri tamamlayın.
- **Kayıt bu arada değişti. Yenileyip gözden geçirdikten sonra tekrar deneyin.** **Yenile** düğmesine basın.
- **Sonuç bilinmiyor (bağlantı koptu).** **Aynı komutu tekrar gönder** düğmesini kullanın; iki kez uygulanmaz.
- **Bu işlem sizin için izinli değil.** Rol, gemi ve nesne bağlamınızı kontrol edin; gerekiyorsa kiracı yöneticisine başvurun (13.03 §10).
- **Cihaz anahtarı kullanılamıyor** veya **Çevrimiçi yeniden yetkilendirme gerekiyor:** kaydedilen veriler korunur; bağlantı gelince yeniden yetkilendirin. Uygulama hatasında korelasyon kimliği, ekran, zaman ve adımlarla destek ekibine başvurun (13.03 §10).

## İlgili sayfalar
