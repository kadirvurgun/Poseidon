---
key: departure-signoff
title: Kalkış hazırlığı ve Kaptan kararı
roles: [Master, Shore Operations, Technical Superintendent]
permissions: [departure.readiness.read, departure.decide]
related: [today, fleet, defects, safety-events, certificate-command-center, data-trust-center]
screenshots: [1, 2, 3, 4, 5, 6, 7, 9]
reviewed: false
---
## Bu sayfa ne işe yarar
Bu sayfa, seçilen gemi ve planlanan kalkış aralığı için **tavsiye niteliğinde bir hazırlık değerlendirmesi** gösterir ve atanmış Kaptanın kararını kaydeder. Cevapladığı soru şudur: "Mürettebat, teknik, yasal, acil durum ve emniyet olayları kaynakları bu aralık için ne söylüyor ve neden?" Kalkışı serbest bırakma, bekletme veya reddetme kararı **Kaptanındır** (13.03 §2). Sistem kalkışa izin vermez; kaydedilen onay bir kalkış izni (clearance) değildir.

## Kim kullanır
- **Master:** değerlendirmeyi ve "Neden" listesini inceleyip Serbest bırak, Beklet veya Reddet kararını gerekçesiyle kaydetmek için (13.03 §2).
- **Shore Operations / Technical Superintendent:** değerlendirmeyi ve kayıtlı Kaptan kararlarını salt okunur izlemek için. Kıyı ekibi Kaptanın yerine karar kaydedemez.

## Başlamadan önce
- Üst bağlam çubuğunda **doğru şirketin** seçili olduğunu doğrulayın. Sayfa yalnız kapsamınızdaki gemileri listeler (13.03 §1).
- Sayfayı görmek için kalkış hazırlığı okuma yetkisi gerekir.
- Karar kaydetmek için karar yetkisi tek başına yetmez: bu gemi için **güncel bir Kaptan atamanız** olmalıdır. Atama yoksa sayfa "Bu gemi için güncel Kaptan atamanız yok. Kararlar salt okunurdur." der.

## Adım adım
1. "Gemi seçin" listesinden gemiyi seçin.
2. **Planlanan kalkış aralığı** bölümünde "Kalkış tarihi" ve "Sefer bitiş tarihi" alanlarını doldurup **Hazırlığı değerlendir** düğmesine basın.
3. **Değerlendirme** satırında durum rozetini, "Güncel" veya "Öngörü" kipini, "Değerlendirme zamanı" ve "Geçerlilik sonu" bilgisini okuyun.
4. Kategori tablosunda Mürettebat, Teknik, Yasal, Acil durum ve Emniyet olayları sonuçlarına bakın.
5. **Neden** listesini okuyun. Her satır geçmeyen bir gözlemi, gerekçe kodunu ve kaynağını gösterir. Beklenmeyen bir satırı kaynağın kendi sayfasında inceleyin.
6. Değerlendirme güncel ve Hazır değilse **Serbest bırakma neden kullanılamıyor** bölümünü okuyun. Bölüm, aralık öngörüyse bunu belirtir, henüz geçmeyen kategorileri sonuçlarıyla listeler ve Acil durum (61.02 tatbikat kaynağı henüz bağlı değil) ile Yasal (gerekli sertifika seti tanımlı değil) kaynak boşluklarını hangi kaydın karşılayacağını açıklar. Bu yalnız sunucunun kategorilerini açıklar; kapıyı değiştirmez.
7. **Kaptan kararları** bölümünde daha önce kaydedilmiş kararları ve geçerliliklerini görün.
8. Kaptan iseniz **Kaptan kararını kaydet** bölümünde Serbest bırak, Beklet veya Reddet seçin, "Atama" listesinden atamanızı seçin, "Gerekçe" yazın (10–2000 karakter) ve **Kaydet** düğmesine basın.
9. Değerlendirmeyi tazelemek için **Yenile** düğmesini kullanın.

## Durumlar ne anlama gelir
- **Hazır:** kayıtlı kaynaklarda geçmeyen gözlem yok. Bu yalnız bilgidir; kalkış kararı yine Kaptanındır.
- **Koşullu:** en az bir kaynak koşul bildiriyor. Bu değerlendirmede Serbest bırak seçeneği açılmaz.
- **Hazır değil:** en az bir kaynak "Kaldı" sonucu veriyor. Diğer kategorilerin geçmesi bunu dengelemez.
- **İnceleme gerekli:** en az bir girdi "Bilinmiyor". Yetkili bir kişinin bakması gerekir.
- **Geçti / Koşul / Kaldı / Bilinmiyor:** kategori tablosundaki kategori sonucu. **Bilinmiyor**, kaynağın okunamadığını veya veri olmadığını gösterir; "Geçti" ya da "Hazır" anlamına gelmez. Acil durum kategorisi için bu sürümde bağlı bir acil durum hazırlık kaynağı yoktur; bu satır Bilinmiyor görünür. Yasal kategorisi, gemi için gerekli sertifika setini tanımlayan bir kaynak olmadığından, kayıtlı sertifikalar geçerli olsa bile Bilinmiyor kalabilir. Yasal kategorisi veri güven sinyallerini de (DATA_TRUST_*) içerir; veri güven okuma yetkisi yoksa Yasal Bilinmiyor görünür.
- **Öngörü:** aralık bugünü kapsamıyor. Bu yalnızca planlama öngörüsüdür ve serbest bırakılamaz.
- **Güncel / Bayat — girdiler değişti:** kayıtlı bir kararın geçerliliği. Girdiler değişince önceki karar bayat olur ve silinmez.

## Yapamazsınız / dikkat
- Sistem kalkışa izin vermez ve hiçbir durumu kalkış izni olarak göstermez. Kaydedilen karar, atanmış Kaptanın kendi insan kaydıdır.
- Serbest bırakma yalnızca güncel bir "Hazır" değerlendirmede kaydedilebilir; her kategori geçmelidir. Acil durum kategorisi bu sürümde Bilinmiyor kaldığından değerlendirme Hazır olamaz ve Serbest bırak sunulmaz; nedeni **Serbest bırakma neden kullanılamıyor** bölümünde yazar. Hazır olsa bile serbest bırakma kararı Kaptanındır; sistem onay vermez. "Beklet ve Reddet her zaman bir gerekçeyle kaydedilebilir."
- Her karar için gerekçe zorunludur. Kayıtlı kararlar değiştirilmez; yeni karar yeni bir kayıt olur.
- Değerlendirme bayatsa veya çevrimdışıysanız karar kaydedilemez; önce yenileyin.

## Sorun olursa
- **Çevrimdışısınız:** değerlendirme güncel olmayabilir ve onay devre dışıdır. Bağlantı gelince yenileyin (13.03 §10).
- **Bu geminin kalkış hazırlığına erişiminiz yok:** rol ve gemi bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun.
- **Beklenmeyen hazırlık durumu:** "Neden" listesini açın; kaynak yanlış görünüyorsa kontrol sahibine ya da alan uzmanına iletin.
- **Sonuç belirsiz:** "Tekrar dene" aynı komutu gönderir. Yeni bir karar yazmayın.
- **Karar geçmişi değişti / Değerlendirme değişti:** "Yeniden yükle" ile güncel değerlendirmeyi inceleyip öyle karar verin.
- **Komut kabul edilmedi:** ekran görüntüsü, zaman ve adımlarla destek ekibine bildirin.

## İlgili sayfalar
