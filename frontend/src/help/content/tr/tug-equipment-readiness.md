---
key: tug-equipment-readiness
title: Çekme ekipmanı hazırlığı
roles: [Tug Master, Chief Engineer]
permissions: [mode.tug, tug.read, tug.write, equipment.write]
related: [tug-readiness, tug-crew-readiness, tug-assignment, tug-command]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Bu sayfa, seçili römorkörün **çekme ekipmanı beyanlarını** (çekme vinci, çekme kancası, çekme halatı, acil bırakma, yangınla mücadele FiFi) ve bunların **Başmühendis doğrulamalarını** gösterir. Cevapladığı soru şudur: "Her çekme ekipmanının SWL değeri, test tarihleri, durumu ve kritikliği ne, kim doğruladı ve hazırlık kontrolü ne sonuç veriyor?" Değerlendirme tavsiye niteliğindedir ve hazırlık kapısına girdi olur; GO kararı değildir.

## Kim kullanır
- **Tug Master:** vinç, çekme halatı, çekme kancası / zincir stoperi, acil bırakma, FiFi ve kritik arıza kanıtını kontrol etmek için (13.03 §3).
- **Başmühendis:** beyanları doğrulamak veya doğrulamamak ve kritik arızanın hazırlık etkisini kaydetmek için (13.03 §5).

## Başlamadan önce
- Römorkörü **Römorkör operasyonları** sayfasından açın; bu ekran atama seçimi istemez.
- Beyan eklemek veya düzenlemek için römorkör yazma ve ekipman yazma yetkisi gerekir.
- Doğrulama kaydetmek için o römorkör için sunucuda kayıtlı bir **Başmühendis** görevlendirmesi gerekir.

## Adım adım
1. **Değerlendirme** satırında genel durum rozetini okuyun.
2. Her ekipman kartında sonuç rozetini, gerekçe kodunu, **SWL (t)**, **Sonraki test tarihi**, **Durum** ve **Kritik** bilgisini kontrol edin.
3. Yeni beyan için **Ekipman ekle** düğmesine basın; **Ekipman türü**, **Etiket**, **SWL (t)**, **Son test**, **Sonraki test tarihi**, **Durum**, **Kritik**, **PMS varlık kimliği**, **Sertifika kimliği** ve **Notlar** alanlarını doldurup **Beyanı kaydet** düğmesine basın.
4. Mevcut bir beyanı değiştirmek için kartta **Düzenle** düğmesini kullanın.
5. Başmühendis olarak **Görevlendirme** seçin, gerekçe yazın ve **Doğrulandı** ya da **Doğrulanmadı** düğmesiyle **Doğrulamayı kaydet** işlemini yapın. "Doğrulanmadı" için gerekçe zorunludur.
6. **Başmühendis doğrulamaları** listesinde revizyon ve zamanıyla doğrulama geçmişini, en altta **Kaynak kontrolleri** tablosunu görün.

## Durumlar ne anlama gelir
- **Hazır / Koşullu / Hazır değil / İnceleme gerekli:** tavsiye niteliğindeki değerlendirme. Hazır bir GO kararı değildir.
- **Geçti / Başarısız / Bilinmiyor:** tek bir ekipmanın kontrol sonucu.
- **Kullanılabilir / Kısıtlı / Arızalı / Bilinmiyor:** beyan edilen ekipman durumu. Durum seçilmezse "Bilinmiyor" kalır; kullanılabilir sayılmaz.
- **Değerlendirilmedi:** kritiklik değerlendirilmemiş. Bu "Hayır" anlamına gelmez.
- **Doğrulandı / Doğrulanmadı:** Başmühendisin insan kaydı.
- **sonraki revizyonla geçersiz:** doğrulama, beyanın eski bir revizyonuna aitti; güncel revizyon yeniden doğrulanmalıdır.

## Yapamazsınız / dikkat
- Kaydetmek doğrulanmamış bir beyan oluşturur. Her değişiklik yeni bir Başmühendis doğrulaması gerektirir.
- Kaydı tamamlamak ile doğrulamak aynı işlem değildir; doğrulama yetkili kişiyi bekler (13.03 §1).
- Ekipman türü kaydedildikten sonra değiştirilemez; farklı tür için yeni beyan ekleyin.
- Sayfa çekme ekipmanını emniyetli ilan etmez; karar Tug Master'dadır (13.03 §3).
- Çevrimdışıyken komutlar devre dışıdır.

## Sorun olursa
- **"Kayıt değişti" mesajı:** **Yeniden yükle** ile güncel beyanı inceleyin.
- **"Sonuç belirsiz" mesajı:** **Yeniden dene** aynı komutu gönderir.
- **Kayıt doğrulanamıyor:** kanıtı ve PMS/sertifika kaynağını kontrol edin; doğrulayıcıya başvurun (13.03 §10).
- **Yetki yok:** rol, römorkör ve görevlendirme bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).

## İlgili sayfalar
