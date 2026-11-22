---
key: survey-planner
title: Sörvey planlayıcı
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read]
related: [pre-survey-checklist, certificate-command-center, renewal-workflow, drydock-planning]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Sörvey planlayıcı, beyan edilmiş sörvey pencerelerini sefer ve liman uygunluğuyla birlikte filo takviminde gösterir. Cevapladığı soru şudur: "Seçtiğim aralıkta hangi gemilerin hangi sertifikalarında sörvey penceresi açılıyor veya geçti ve hazırlık listesi var mı?" Yalnız planlama yardımıdır: otorite takvimi, randevu veya sörvey sonucu değildir (13.03 §8).

## Kim kullanır
- **Master, Chief Engineer / Technical Superintendent:** yaklaşan sörveyleri görmek ve hazırlık listesini zamanında başlatmak için (13.03 §5).
- **Regulatory / Assurance kullanıcısı:** filo genelinde beyan edilen pencereleri ve eksik beyanları izlemek için (13.03 §8).

## Başlamadan önce
- Sayfayı görmek için sertifika okuma yetkisi gerekir. Yalnız erişebildiğiniz gemiler listelenir.
- Pencereler sertifika kaydındaki beyanlardan gelir. Pencere beyan edilmemişse satır **Pencere beyan edilmemiş** altında görünür; tarihleri [Sertifika merkezi](/app/certificate-command-center) sayfasında kaynak belgeden girin.
- Sayfa canlı okuma gerektirir. Çevrimdışıyken "Çevrimdışı: güncel çevrimiçi okuma gerekir." mesajı görünür.

## Adım adım
1. **Takvim aralığı** bölümünde **Başlangıç** ve **Bitiş** tarihlerini seçip **Göster** düğmesine basın.
2. Özet satırında açık, yaklaşan, gecikmiş ve bilinmiyor sayılarını okuyun.
3. Her satırda **Sertifika**, **Düzenleyen**, **Beyan edilen bayrak**, **Mevzuat dayanağı**, **Beyan edilen bitiş**, **Sörvey penceresi** ve **Sefer/liman uygunluğu** alanlarını inceleyin.
4. Sertifikayı görmek için **Sertifikayı aç** düğmesine basın.
5. Hazırlık listesi varsa **Kontrol listesini aç**, yoksa **Kontrol listesi başlat** düğmesiyle [Sörvey öncesi kontrol listesi](/app/pre-survey-checklist) sayfasına geçin.

## Durumlar ne anlama gelir
- **Yaklaşan / Pencere açık:** beyan edilen pencereye göre konum.
- **Pencere geçti, karşılanma kaydı yok:** pencere bitti ve sörveyin yapıldığına dair kayıt yok. Sörveyin yapılmadığı anlamına da gelmez; kayıtla kontrol edilmelidir.
- **Pencere bilinmiyor:** tarih eksik veya okunamıyor. Bilinmeyen pencere "sorun yok" anlamına gelmez; inceleme gerektirir.
- Sefer/liman uygunluğu: **Koşullu**, **Uygun değil**, **Bilinmiyor**. Sayfa hiçbir zaman "uygun" onayı vermez.
- Kontrol listesi hazırlık durumu: **Engelli**, **Eksik**, **Hazırlandı, insan onayı bekleniyor**.

## Yapamazsınız / dikkat
- Planlayıcı randevu oluşturmaz ve otoriteye başvuru yapmaz. Klas, bayrak ve otorite koordinasyonu dışsaldır ve burada simüle edilmez.
- Takvimdeki görünüm sörvey sonucu veya sertifika geçerliliği değildir. Sefer ve sörvey kararları yetkili kişilerindir.
- Sayfa salt okunurdur; değişiklikler sertifika ve kontrol listesi sayfalarında yapılır.

## Sorun olursa
- **Filonun yalnız bir bölümü gösteriliyor; aralığı daraltın.** Daha kısa bir takvim aralığı seçin.
- **Bu aralıkta sörvey penceresi olan sertifika yok.** Aralığı genişletin veya sertifika beyanlarını kontrol edin.
- **Yüklenemedi. Yeniden deneyin.** **Yeniden dene** düğmesine basın. Sürerse ekran, zaman ve adımlarla destek ekibine bildirin (13.03 §10).
- **Bu görünüme erişiminiz yok.** Rol ve gemi kapsamınızı kontrol edin; gerekirse Tenant Admin ile görüşün (13.03 §10).

## İlgili sayfalar
