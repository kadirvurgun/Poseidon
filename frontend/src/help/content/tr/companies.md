---
key: companies
title: Şirketler
roles: [Tenant Admin, Shore Operations]
permissions: [canReadCompanies, canCreate]
related: [fleet, audit, today]
screenshots: [1, 2, 3]
reviewed: false
---
## Bu sayfa ne işe yarar
Şirketler sayfası, çalışma alanınızda yetkili olduğunuz **şirket kayıtlarını** listeler. Cevapladığı soru şudur: "Bu çalışma alanında hangi şirketler tanımlı ve her birinin kodu ve adı nedir?" Gemi ve römorkör kayıtları bir şirkete bağlıdır; bu yüzden bağlam seçmenin ilk adımı doğru şirkettir.

## Kim kullanır
- **Kiracı yöneticisi (Tenant Admin):** şirket kayıtlarını en az yetki ilkesiyle oluşturmak ve düzenlemek için (13.03 §9).
- **Kıyı operasyonları:** doğru şirket bağlamını bulmak için.

## Başlamadan önce
- Sayfayı görmek için şirketleri okuma yetkisi gerekir. Yetkiniz yoksa "Bu çalışma alanına erişim yetkiniz yok." mesajı görünür.
- Yeni kayıt oluşturmak için oluşturma yetkisi gerekir. Bir kaydı düzenleyebilmeniz o kayıt için sunucunun verdiği yetkiye bağlıdır.

## Adım adım
1. Listede şirketlerin kodunu ve adını görün. Diğer sayfalar için **Önceki** / **Sonraki** düğmelerini kullanın.
2. Aramak için **Kod veya ad ile ara** alanına yazıp **Ara** düğmesine basın.
3. Bir şirketin adına tıklayarak **Ayrıntılar** bölümünü açın.
4. Yeni şirket için **Yeni kayıt** düğmesine basın; kod ve adı girip **Kaydet** düğmesine basın. Tüm alanlar zorunludur.
5. Var olan bir kaydı değiştirmek için ayrıntıda **Düzenle** düğmesini kullanın.
6. Tüzel kişi bilgilerini görmek için **Tüzel kişi kaydını göster** bölümünü açın; bu bölüm salt okunurdur.

## Durumlar ne anlama gelir
- **Kayıt bulunamadı:** arama veya yetki kapsamınızda kayıt dönmedi.
- **Tüzel kişi: Taslak – eksik, aktif değil / Aktif / Pasif:** tüzel kişi kaydının durumu. Taslak, aktivasyon öncesi eksik değerler olduğunu gösterir.
- **Bilinmiyor:** değer kayıtta yok. Bilinmiyor; "Hayır" veya "geçerli" anlamına gelmez.
- **Kaydedildi:** kayıt sunucuya yazıldı. Bu kayıt operasyonel bir onay oluşturmaz.

## Yapamazsınız / dikkat
- Şirket kaydı oluşturmak veya düzenlemek operasyonel, uyum veya emniyet onayı değildir.
- Tüzel kişi değerleri bu sayfadan değiştirilmez; yalnız denetlenen İK komutlarıyla değişir. Sicil tanımlayıcıları maskeli gösterilir.
- Aynı kod iki şirkette kullanılamaz.
- Kaydedilmemiş bir taslakla sayfadan ayrılırsanız taslağı bırakmanız veya düzenlemeye devam etmeniz istenir.

## Sorun olursa
- **Kayıt başka bir işlemle değişti:** taslağınız korunur. Güncel kaydı yüklemek için taslağı bırakın ve değişikliğinizi yeniden yapın.
- **Bu kod zaten kullanılıyor:** başka bir kod girin.
- **İşlem sonucu doğrulanamadı:** alanlar sonuç alınana kadar kilitlidir; aynı komutu güvenle tekrar deneyin.
- **Kaydetme yetkiniz değişmiş olabilir / Erişim yok:** rol ve çalışma alanı bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).
- **Hizmete ulaşılamıyor:** "Tekrar dene" düğmesini kullanın; sorun sürerse destek referansıyla bildirin.

## İlgili sayfalar
