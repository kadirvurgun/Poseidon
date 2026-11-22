---
key: towage-execution
title: Römorkaj operasyon yürütme
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.execution.verify]
related: [tug-readiness, towage-risk-briefing, towage-plan, tug-daily-log, tug-operation-history]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Yürütme konsolu, gönderilmiş bir römorkör işinin **gerçek icrasını** zaman çizelgesiyle kaydeder: başlatma, gözlemler, **Askıya al / Devam ettir / İptal et / Tamamla** işlemleri ve sonrasındaki doğrulama. Cevapladığı soru şudur: "Operasyon ne zaman başladı, halat ne zaman bağlandı ve bırakıldı, hangi gözlemler kaydedildi, operasyon hangi durumda ve kayıt doğrulandı mı?" Başlatma ve geçişler icra eden römorkördeki Römorkör Kaptanı veya Kaptan'a ait insan kayıtlarıdır; konsol asla GO vermez.

## Kim kullanır
- **Tug Master / Kaptan:** operasyonu başlatmak, Start/Suspend/Resume/Abort/Complete zaman çizelgesini ve kanıtlarını kaydetmek için (13.03 §3).
- **Doğrulayıcı (yürütme doğrulama yetkisi olan kişi):** tamamlanan veya iptal edilen kaydı doğrulamak ya da doğrulamayı reddetmek için (13.03 §1).
- **Römorkör operasyon koordinatörü / Kıyı operasyonları:** tamamlanan işlerde kanıt bütünlüğünü ve geçmişi izlemek için; operasyonu başlatamaz veya iptal edemez (13.03 §7).

## Başlamadan önce
- Römorkörü **Römorkör operasyonları** sayfasından açın ve **Gönderilmiş bir römorkör işi seçin** listesinden işi seçin.
- Başlatma için şunların hepsi güncel olmalıdır: römorkörün aktif ataması, onaylı **Römorkaj operasyon planı** (römorkör planda yer almalı), yayımlanmış ve tüm katılımcılarca teyit edilmiş **Römorkaj risk ve brifing**, ve **Operasyon hazırlık kapısı** üzerinde güncel değerlendirmeye bağlı bir **GO** kaydı.
- Başlatma ve geçişler için o römorkör için sunucuda kayıtlı bir **Atama** (Römorkör Kaptanı veya Kaptan) gerekir.

## Adım adım
1. **Başlatma kontrol noktası** bölümünde her römorkör için **Hazırlık kararı** ve **Engeller** listesini okuyun.
2. Engel yoksa gemideki atanmış kişi olarak onay metnini okuyun, **Atama** seçin, isterseniz **Başlangıç mevkii** girin ve **Operasyonu başlat** düğmesine basın.
3. Yürütme açıldığında **Durum**, **Doğrulama** ve defter alanlarını (halat bağlandı/bırakıldı, azami çekme yükü, haberleşme, vinç, acil bırakma testi, aktif dakika) izleyin.
4. **Gözlem kaydet** formunda gözlem türünü (ör. çekme halatı bağlandı, çekme yükü, haberleşme kontrolü, hava durumu, vinç durumu, acil bırakma testi, mevki, kanıt, not) seçip değerleri girin ve **Gözlem kaydet** düğmesine basın.
5. **Römorkör Kaptanı işlemleri** bölümünde **İşlem** (Askıya al, Devam ettir, İptal et, Tamamla) seçin, **Atama** ve **Gerekçe** girin. İptal ve tamamlama için en az 20 karakterlik gerekçe gerekir ve **Bitiş mevkii** alanı açılır; tamamlama bitiş mevkii olmadan kabul edilmez.
6. Tamamlama engelleri varsa **Tamamlama engelleri** listesini giderin; örneğin halat hâlâ bağlıysa önce bırakılmasını kaydedin.
7. Doğrulayıcı olarak gerekçe yazıp **Kaydı doğrula** veya **Doğrulamayı reddet** düğmesine basın.
8. **Zaman çizelgesi** listesinde tüm olayları sırayla görün.

## Durumlar ne anlama gelir
- **Aktif / Askıda / Tamamlandı / İptal edildi:** operasyonun durumu. Tamamlandı, kaydın doğrulandığı anlamına gelmez.
- **Beklemede / Doğrulandı / Reddedildi:** doğrulama durumu. Tamamlandı ≠ Doğrulandı; doğrulama ayrı bir kişinin kaydıdır.
- **İyi / Kesildi / Geçti / Kaldı / Koşullu / Bilinmiyor:** haberleşme, vinç ve acil bırakma gözlemlerinin sonucu. Bilinmiyor, kayıt yapılmadığını gösterir; "İyi" veya "Geçti" yerine geçmez.
- **Emniyet olayları — Bilinmiyor:** bu konsol emniyet olaylarını saymaz; olay yoksa bile "olay yok" sonucu çıkarılmaz.
- **GO (insan kaydı) / NO GO (insan kaydı):** hazırlık kapısında Tug Master veya Kaptan tarafından kaydedilen karar.

## Yapamazsınız / dikkat
- Kıyı kullanıcıları, yapay zekâ ve sistem kimlikleri bir operasyonu başlatamaz veya iptal edemez. Kıyı ataması Tug Master'ın gemideki emniyet kararını ortadan kaldırmaz (13.03 §7).
- Yalnızca atanmış Römorkör Kaptanı veya Kaptan askıya alabilir, devam ettirebilir, iptal edebilir veya tamamlayabilir. Operasyon sürerken iptal her zaman kullanılabilir.
- Operasyonu başlatan veya bitiren kişi aynı kaydı doğrulayamaz; doğrulama operasyon bittikten sonra yapılır.
- Defter yalnız eklemelidir; kaydedilen olaylar silinmez veya değiştirilmez.
- Çevrimdışıyken komutlar gönderilmez. Gözlemler **Römorkör çevrimdışı operasyonlar** sayfasında kuyruğa alınabilir; başlatma, geçişler ve doğrulama çevrimiçi kalır.

## Sorun olursa
- **"Sunucu eksik veya engelleyici kalemleri listeledi." mesajı:** **Engeller** veya **Tamamlama engelleri** listesindeki kodları giderin (ör. plan onaylı değil, brifing teyidi bekliyor, GO kaydı yok veya güncel değil).
- **"Kayıt değişti veya komut çakışıyor." mesajı:** **En güncelini yükle** ile kaydı inceleyin.
- **"Sonuç bilinmiyor." mesajı:** **Yeniden dene** aynı komutu tekrar gönderir.
- **Yetki yok:** rol, römorkör ve atama bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).

## İlgili sayfalar
