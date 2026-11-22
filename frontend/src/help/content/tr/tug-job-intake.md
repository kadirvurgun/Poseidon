---
key: tug-job-intake
title: İş talebi
roles: [Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-job-board, tug-command, tug-assignment, towage-plan]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
İş talebi sayfası, seçili römorkör bağlamında yeni bir **römorkör iş talebini taslak olarak** yapılandırır. Cevapladığı soru şudur: "Bu iş için hizmet verilecek gemi, iş tipi, zaman penceresi, konum ve gereken kaynak (römorkör sayısı, çekiş gücü) nedir?" Kaydedilen talep taslak bir beyandır; operasyonel atama, uygunluk, izin veya emniyet onayı değildir.

## Kim kullanır
- **Römorkör operasyon koordinatörü / Kıyı operasyonları:** talebi yapılandırmak; hizmet verilecek gemi, iş tipi, zaman penceresi ve gerekli kapasite bilgisini girmek için (13.03 §7).

## Başlamadan önce
- Üst bağlam çubuğunda **doğru şirket ve römorkörü** seçin.
- Römorkör yazma yetkisi gerekir. Yetkiniz yoksa form açılmaz ve "Erişim kullanılamıyor." görünür.
- Eksik taslak kaydedilebilir. Gönderim öncesinde zorunlu operasyon verileri ve yetkili inceleme tamamlanmalıdır.

## Adım adım
1. **Talep ve hizmet verilecek gemi** bölümünde **Talep kaynağı**, **Müşteri referansı**, **Hizmet verilecek gemi**, **Gemi IMO**, **İş tipi**, **Öncelik** ve **Talep sorumlusu** alanlarını doldurun. Seçim yapılmayan listeler "Bilinmiyor" olarak kalır.
2. **Talep edilen zaman ve konum** bölümünde **Talep başlangıcı (UTC)**, **Talep bitişi (UTC, koşullu)**, **Operasyon alanı**, **Alış noktası** ve **Varış noktası** bilgilerini girin. Saatler UTC'dir.
3. **Kaynak ihtiyacı ve notlar** bölümünde **Gerekli BP (ton, kesin ondalık)**, **Gerekli römorkör sayısı**, **Özel gereksinimler** ve **Notlar** alanlarını girin.
4. **Taslağı kaydet** düğmesine basın. Kayıt başarılıysa iş detayı açılır ve "Taslak kaydedildi. Operasyon onayı kullanılamıyor." mesajı görünür.
5. Vazgeçmek için **Değişiklikleri bırak** düğmesini kullanın.
6. İş detayında **Talebi gönder** ile talebi planlamaya kaydedin. Zorunlu veri eksikse gönderim engellenir ve eksik alanlar listelenir.

## Durumlar ne anlama gelir
- **Taslak:** talep kaydedildi, düzenlenebilir; henüz gönderilmedi.
- **Gönderildi:** talep planlama için kaydedildi. Onay, atama veya hazırlık kararı değildir.
- **İptal edildi:** talep kapandı; yeniden başlatılamaz.
- **Bilinmiyor:** alan için değer seçilmedi. Bilinmiyor bir varsayılan değer değildir ve "Hayır" ya da "Uygulanamaz" yerine geçmez.
- **Bu değeri kontrol edin.:** sunucu bu alanı reddetti; alanı düzeltip yeniden kaydedin.

## Yapamazsınız / dikkat
- Bu sayfa römorkör atamaz ve işi hazır ilan etmez. Atama önerisi ayrı bir adımdır ve Tug Master'ın gemideki emniyet kararını ortadan kaldırmaz (13.03 §7).
- Zorunlu veri eksikken talep gönderilemez; eksik taslak ise kaydedilebilir.
- Gönderilmiş talebin alanları bu formdan değiştirilemez; iptal edilen talep yeniden başlatılamaz, yeni talep oluşturun.
- Çevrimdışıyken taslak yalnız bu sayfada korunur ve kaydedilemez.

## Sorun olursa
- **"Kaydın sonucu belirsiz" mesajı:** aynı deneme korunur; bağlandıktan sonra **Tekrar dene** ile aynı komutu gönderin. Yerel girdileri bırakmak gönderilmiş bir komutu iptal etmez.
- **"Çakışma" mesajı:** taslağınız korunur; yeni deneme için bırakıp yeniden yükleyin.
- **Alanları kontrol edin:** işaretli alanları düzeltin.
- **Erişim kullanılamıyor:** rol ve römorkör bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).

## İlgili sayfalar
