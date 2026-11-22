---
key: tug-command
title: Römorkör operasyonları
roles: [Tug Master, Tug Operations Coordinator, Shore Operations, Crew]
permissions: [mode.tug, tug.read]
related: [tug-job-board, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## Bu sayfa ne işe yarar
Menüde **Römorkör merkezi** olarak görünen bu sayfa, seçili römorkörün **kaynak bağlamını** gösterir. Cevapladığı soru şudur: "Bu römorkör için hangi rol, tip, çekiş gücü (BP) ve ekipman beyanları kayıtlı, bunlar nereden geliyor ve ne kadar güncel?" Sayfa yalnız beyanları ve kaynaklarını gösterir; operasyonel hazırlık, uygunluk, izin veya emniyet onayı vermez. Üstteki uyarı bunu açıkça yazar: "Kaynak beyanları · operasyonel hazırlık bilinmiyor".

## Kim kullanır
- **Tug Master:** kendi römorkörünün rol, BP ve ekipman bağlamını işe başlamadan önce doğrulamak için (13.03 §3).
- **Römorkör operasyon koordinatörü / Kıyı operasyonları:** iş panosuna ve iş talebine geçmeden önce doğru römorkör bağlamında olduğunu görmek için (13.03 §7).
- **Mürettebat:** kendisine açılan Gemi Modu ekranlarını ve rol merceğini görmek için (13.03 §6).

## Başlamadan önce
- Römorkör modu ve römorkör okuma yetkisi gerekir. Yetkiniz yoksa sayfa "Erişim kullanılamıyor." gösterir.
- Bağlam seçili değilse sayfa **Römorkör seç** başlığıyla yetkili römorkörleri listeler; birini açın.
- Ekipman listesi ayrıca ekipman okuma yetkisi ister; yoksa ekipman kaynağı "kullanılamıyor veya yetki verilmemiş" olarak görünür.

## Adım adım
1. **Römorkör seç** listesinden römorkörü açın. Başlık altında **Römorkör bağlamı** satırı römorkör kodu, adı ve modunu gösterir.
2. **Rol (sunucu ataması)** ve **Römorkör modu menü merceği** alanlarında size sunucuda atanmış rolü ve menü merceğini okuyun.
3. **Mod görünürlüğü** alanında Gemi Modu ekranlarının açık olup olmadığını görün. **Gemi Modu ekranları** bölümü yalnız size yetkilendirilmiş ekranlara bağlantı verir.
4. **Römorkör tipi**, **Tip kaynağı / sürümü** ve **Tip kaynak gözlemi / politikası** alanlarından tipin hangi kaynak ve sürümden geldiğini kontrol edin.
5. **Profil BP (doğrulanmamış)**, **BP kaynağı / sürümü / niteliği** ve **BP güncelliği / gözlemi** alanlarını okuyun. Bu değer beyan edilmiş bir değerdir, ölçülmüş değildir.
6. **Ekipman bağlamı** bölümünde ekipman beyanlarını, seviyelerini, revizyonlarını ve kaynak niteliklerini görün.
7. Sayfadaki sekmelerden **İş panosu** veya (yazma yetkiniz varsa) **İş talebi** ekranına geçin ya da **Yeni talep** düğmesini kullanın.

## Durumlar ne anlama gelir
- **Bilinmiyor — rol ataması yok:** sunucuda bu römorkör için size rol ataması kayıtlı değil. Bu bir "rol yok, sorun yok" sonucu değildir; yönetici ataması gerekir.
- **Rol etiketleri kullanılamıyor / Menü merceği kullanılamıyor:** kaynak şu anda okunamıyor. Eksik bilgi boş kabul edilmez.
- **Römorkör modu için mercek yapılandırılmamış:** bu modda size özel menü merceği tanımlı değil.
- **Römorkör modunda gizli. Mod-arası rol gerekir.:** Gemi Modu ekranları için açıkça yetkilendirilmiş mod-arası rol gerekir.
- **Profil BP (doğrulanmamış):** beyan edilen çekiş gücü. Doğrulanmış bir test sonucu değildir.
- **Ekipman beyanı dönmedi. Doğrulanmış operasyonel uygunluk bilinmiyor.:** liste boş olsa bile ekipmanın uygun olduğu varsayılmaz.
- **Bilinmiyor / Kullanılamıyor:** kaynak değeri yok veya okunamadı. Bilinmiyor; "Hazır" veya "Hayır" yerine geçmez.

## Yapamazsınız / dikkat
- Bu sayfada hiçbir alan düzenlenmez ve hiçbir hazırlık sonucu üretilmez. Operasyon kararı ve emniyet yetkisi gemideki Tug Master'dadır (13.03 §3).
- Kıyıdan yapılan atama önerisi Tug Master'ın gemideki emniyet kararını ortadan kaldırmaz (13.03 §7).
- Mod-arası yetki beyan edilmiş olsa da her Gemi Modu ekranı ayrıca sunucu yetkisi ister.
- Çevrimdışıyken güncel profil kaynakları gösterilmez; yeniden bağlanıp erişimi ve kaynak sürümlerini kontrol edin.

## Sorun olursa
- **Erişim kullanılamıyor:** rol, römorkör ve nesne bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).
- **"Güncel kimlik ve araç erişimi kontrol edilmeli" uyarısı:** **Erişimi tekrar kontrol et** düğmesini kullanın.
- **Veri alınamadı:** bağlantınızı kontrol edip **Tekrar dene** düğmesine basın.
- **Beklenmeyen BP veya ekipman bilgisi:** kaynak ve sürüm alanlarını not edip kaynak sahibiyle inceleyin; kayıt doğrulanamıyorsa kanıtı ve kaynağını kontrol edin (13.03 §10).

## İlgili sayfalar
