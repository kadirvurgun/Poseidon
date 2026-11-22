---
key: towage-plan
title: Römorkaj operasyon planı
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [towage-risk-briefing, tug-assignment, tug-readiness, towage-execution]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Römorkaj operasyon planı, gönderilmiş bir römorkör işi için **römorkör rollerini ve konfigürasyonunu, bilgi alışverişini, operasyon limitlerini, haberleşmeyi, acil bırakmayı, acil durum planını ve snap-back bölgesi kontrollerini** kaydeder. Cevapladığı soru şudur: "Bu iş nasıl yürütülecek, hangi limitlerle ve hangi acil durum düzenlemeleriyle?" Plan onayı atanmış Kaptan veya Römorkör Kaptanı'nın insan kaydıdır; GO kararı veya çalışma izni değildir. Limitler kaynağıyla birlikte girilen plan değerleridir; sistem varsayılan değer sağlamaz.

## Kim kullanır
- **Römorkör operasyon koordinatörü / planlayıcı:** planı hazırlamak ve onaya göndermek için (13.03 §7).
- **Tug Master / Kaptan:** konfigürasyon, halat, BP/kapasite, haberleşme, limitler, acil durum ve işi durdurma koşullarını doğrulayıp planı onaylamak veya iade etmek için (13.03 §3).

## Başlamadan önce
- Römorkörü **Römorkör operasyonları** sayfasından açın ve **Gönderilmiş bir römorkör işi seçin** listesinden işi seçin.
- Planı düzenlemek için römorkör yazma ve plan yazma yetkisi gerekir. Plandaki römorkörler, bu iş için güncel atama önerisi olan römorkörlerden seçilir.
- Onay veya iade için koordinatör römorkör için sunucuda kayıtlı bir **Görevlendirme** (Römorkör Kaptanı veya Kaptan) gerekir.

## Adım adım
1. **Durum** ve **Revizyon** bilgisini, ardından **Gönderim öncesi eksikler** listesini okuyun.
2. **Römorkör rolleri ve konfigürasyonu** bölümünde **Ekle** ile römorkör ekleyin; **Konum**, **Römorkaj yöntemi**, **Koordinatör (lider) römorkör**, **Halat kaynağı**, **Bağlantı noktası**, **Römorkaj halatı ekipman kimliği** ve **Acil bırakma ekipmanı kimliği** alanlarını girin.
3. **Kaptan / kılavuz kaptan / Römorkör Kaptanı bilgi alışverişi** metnini yazın.
4. **Operasyon limitleri (plan girdileri)** bölümünde her **Parametre** için **Değer** ve **Limit kaynağı** girin. Yeni limit boş başlar.
5. **Haberleşme planı** (birincil ve yedek kanal, çalışma dili, haberleşme kaybı prosedürü), **Acil bırakma**, **Acil durum planı** ve **Halat kırılma (snap-back) bölgesi kontrolleri** bölümlerini doldurun.
6. **Taslak oluştur** veya **Taslağı kaydet** ile kaydedin; eksikler tamamlanınca **Onaya gönder** düğmesine basın.
7. Onaylayan kişi **Görevlendirme** seçip en az 10 karakterlik **Gerekçe** yazar ve **Onayla** ya da **İade et** düğmesine basar.
8. Onaylı planı değiştirmek gerekirse gerekçe yazıp **Değiştir** düğmesini kullanın. **Onay kayıtları** ve **Revizyonlar** listeleri geçmişi gösterir.

## Durumlar ne anlama gelir
- **Taslak:** plan düzenlenebilir.
- **İncelemede:** plan onaya gönderildi; karar bekleniyor.
- **İade edildi:** onaylayan kişi planı gerekçesiyle geri gönderdi; düzeltilip yeniden gönderilir.
- **Onaylandı:** Kaptan veya Römorkör Kaptanı planın bu revizyonunu onayladı. Bu bir GO kararı değildir; hazırlık kapısı ve brifing ayrıca gerekir.
- **Bilinmiyor:** alan için değer seçilmedi. Snap-back bölgeleri için "Bilinmiyor", "Hayır" yerine geçmez.
- **Bu plan kaydedildikten sonra römorkör talebi değişti.:** plan eski talep revizyonuna dayanıyor; gözden geçirilmelidir.

## Yapamazsınız / dikkat
- Planı yazan veya onaya gönderen kişi aynı planı onaylayamaz.
- Zorunlu bölümler eksikken plan onaya gönderilemez; eksik taslak kaydedilebilir.
- Sistem limit, kanal veya eşik değeri önermez; her değer kaynağıyla birlikte planlayıcıdan gelir.
- Onaylı plan da operasyona başlama izni değildir; başlama için güncel brifing teyitleri ve Tug Master'ın GO kaydı gerekir (13.03 §3).
- Çevrimdışıyken komutlar devre dışıdır.

## Sorun olursa
- **"Zorunlu bölümler eksik." mesajı:** **Gönderim öncesi eksikler** listesindeki kodları tamamlayın.
- **"Kayıt değişti" mesajı:** **Yeniden yükle** ile güncel planı inceleyin.
- **"Sonuç bilinmiyor" mesajı:** **Yeniden dene** aynı isteği tekrar gönderir.
- **Yetki yok:** rol, römorkör ve görevlendirme bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).

## İlgili sayfalar
