---
key: tug-readiness
title: Operasyon hazırlık kapısı
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-assignment, tug-crew-readiness, tug-equipment-readiness, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Hazırlık kapısı, bir römorkör atamasının operasyona hazırlığını kaynak kontrollerinden **tavsiye niteliğinde** değerlendirir ve atanmış Tug Master'ın veya Kaptan'ın **GO/NO-GO kararını** gerekçesiyle kaydetmesini sağlar. Cevapladığı soru şudur: "Mürettebat, ekipman, PMS, sertifika, iletişim, hava, çekiş gücü payı, atama ve brifing kontrolleri ne diyor ve hangi engeller var?" Değerlendirme bir karar değildir: "Sistem asla GO vermez; GO/NO-GO kararını gerekçesiyle yalnızca atanmış Kaptan veya Römorkör Kaptanı kaydeder."

## Kim kullanır
- **Tug Master / Kaptan:** değerlendirmeyi ve engelleri inceleyip GO veya NO-GO kararını kaydetmek için (13.03 §3).
- **Römorkör operasyon koordinatörü / Kıyı operasyonları:** hazırlık durumunu ve engelleri izlemek için; karar vermezler (13.03 §7).

## Başlamadan önce
- Sayfayı bir atamadan açın: **Atama ve çakışmalar** sayfasında öneri altındaki **Operasyon hazırlık kapısı** bağlantısını kullanın. Atama seçili değilse sayfa "İşin atama listesinden bir atama seçin." gösterir.
- Karar kaydetmek için römorkör yazma yetkisi ve o römorkör için sunucuda kayıtlı bir **Görevlendirme** (Römorkör Kaptanı veya Kaptan) gerekir.
- Brifing tamamlanmadan READY olmaz: **Römorkaj risk ve brifing** yayımlanmış ve tüm katılımcılar tarafından teyit edilmiş olmalıdır (13.03 §3).

## Adım adım
1. **Değerlendirme** satırında durum rozetini okuyun. Yanındaki not "Tavsiye niteliğinde, GO kararı değildir." der.
2. **Gerekli çeki kuvveti (t)**, **Doğrulanmış mevcut çeki kuvveti (t)** ve **Çeki kuvveti payı (t)** değerlerini kontrol edin. Değer yoksa "Bilinmiyor" görünür.
3. **Kritik engeller**, **İnceleme gerekçeleri** ve **Koşullar** listelerindeki kodları inceleyin.
4. Kategori tablosunda her kategorinin (Mürettebat, Çekme yetkinliği, Ekipman, Çekme ekipmanı, PMS ve arızalar, Sertifikalar, İletişim, Hava durumu, Çeki kuvveti payı, Atama, Römorkaj risk ve brifing) sonucunu görün. Ayrıntı için **Kaynak kontrolleri** tablosundaki gerekçe kodlarını okuyun.
5. Karar yetkiniz varsa **Kaptan kararını kaydet** formunda **NO-GO** veya **GO** seçin, **Görevlendirme** ve **Gerekçe** girip **Kaydet** düğmesine basın.
6. Kayıtlı kararları **Kaptan kararları** listesinde görün.

## Durumlar ne anlama gelir
- **Hazır (READY):** kaynak kontrolleri bir engel göstermiyor. Bu bilgilendirmedir, karar değildir; GO kararını yine Tug Master veya Kaptan kaydeder.
- **Koşullu (CONDITIONAL):** "Koşullar" listesindeki koşullar sağlanırsa ilerlenebilir; koşulları karar veren kişi değerlendirir.
- **Hazır değil (NOT_READY):** en az bir kritik engel var.
- **İnceleme gerekli (REVIEW_REQUIRED):** bazı kaynaklar bilinmiyor veya inceleme bekliyor. Bilinmeyen kontrol asla "Hazır" sayılmaz.
- **Geçti / Başarısız / Bilinmiyor:** tek bir kategori veya kontrolün sonucu.
- **GO / NO-GO:** Tug Master veya Kaptan tarafından kaydedilen insan kararı ve gerekçesi.

## Yapamazsınız / dikkat
- GO yalnızca HAZIR veya KOŞULLU bir değerlendirmeye karşı kaydedilebilir; NO-GO her durumda kaydedilebilir.
- Risk ve brifing tamamlanmadan veya katılımcı teyitleri eksikken READY oluşmaz; bu durumda GO seçeneği kapalıdır (13.03 §3).
- Kıyı kullanıcısının atama önerisi Tug Master'ın gemideki emniyet kararını ortadan kaldırmaz (13.03 §7).
- Değerlendirme kararınızdan önce değişirse karar kabul edilmez; güncel değerlendirmeyi yeniden inceleyin.
- Çevrimdışıyken komutlar devre dışıdır.

## Sorun olursa
- **"Değerlendirme değişti" mesajı:** **Yeniden yükle** ile güncel değerlendirmeyi inceleyip kararı yeniden verin.
- **"Sonuç belirsiz" mesajı:** **Yeniden dene** aynı komutu gönderir.
- **Hazırlık beklenmedik:** kategori tablosu ve kaynak kontrollerindeki gerekçeleri açın; kontrol sahibi veya alan uzmanıyla inceleyin (13.03 §10).
- **Yetki yok:** rol, römorkör ve görevlendirme bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).

## İlgili sayfalar
