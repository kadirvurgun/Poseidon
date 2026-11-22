---
key: tug-daily-log
title: Römorkör günlük defteri
roles: [Tug Master, Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-execution, tug-operation-history, tug-operations-offline, offline-conflict-center]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Römorkör günlük defteri; bir tarih ve vardiya için **işleri, makine çalışma saati sayaçlarını, yakıtı ve arızaları** kaydeder ve bunları günün römorkaj icralarıyla **mutabakat** için karşılaştırır. Cevapladığı soru şudur: "Bu gün/vardiyada kaç iş tamamlandı, sayaç saatleri icra süreleriyle uyuşuyor mu, ne kadar yakıt kullanıldı ve hangi arızalar bildirildi?" Mutabakat yalnızca tavsiye niteliğindedir; Kaptan incelemesi ayrı bir insan kaydıdır.

## Kim kullanır
- **Mürettebat / vardiya sorumlusu:** vardiya kaydını gerçek zamanlı ve kronolojik tutmak için (13.03 §6).
- **Tug Master / Kaptan:** gönderilen kaydı incelemek, doğrulamak veya iade etmek için (13.03 §3).

## Başlamadan önce
- Römorkörü **Römorkör operasyonları** sayfasından açın.
- Kayıt oluşturmak ve düzenlemek için römorkör yazma yetkisi gerekir.
- İnceleme için o römorkör için sunucuda kayıtlı bir **Atama** (Römorkör Kaptanı veya Kaptan) gerekir. Kaydı yazan veya gönderen kişi aynı kaydı inceleyemez.

## Adım adım
1. Listeden mevcut bir kaydı açın ya da **Kayıt tarihi** ve **Vardiya** seçip **Günlük kayıt oluştur** düğmesine basın.
2. **Saat dilimi (IANA)** alanını girin.
3. **Makine çalışma saati sayaçları** bölümünde her **Sayaç** için **Açılış okuması**, **Kapanış okuması** ve gerekirse **Sıfırlama veya tur atma kanıtı** girin.
4. **Kullanılan yakıt** bölümünde değer, birim ve **Yakıt kaynağı referansı** girin.
5. **Günün römorkaj icraları** listesinden bu kayda ait icraları işaretleyin; **Bağlı arıza tanımlayıcıları** ve **Günlük özet** alanlarını doldurun.
6. **Kaydet** düğmesine basın. **Mutabakat** satırında tamamlanan işleri, icra saatlerini, sayaç saatlerini ve bildirilen arızaları kontrol edin.
7. **Engeller** listesi boşsa **Kaptan incelemesine gönder** düğmesine basın.
8. Kaptan olarak **Atama** seçip gerekçe yazın ve **Kaptan: kaydı doğrula** ya da **Kaptan: kaydı iade et** düğmesine basın.

## Durumlar ne anlama gelir
- **Taslak / Gönderildi / İade edildi / İncelendi:** kaydın durumu.
- **Kaptan incelemesi — Beklemede / Doğrulandı / İade edildi:** Kaptan'ın insan kaydı. Gönderilmiş kayıt doğrulanmış kayıt değildir.
- **Mutabakat sağlandı / Dikkat / İnceleme gerekli / Eksik:** tavsiye niteliğindeki karşılaştırma sonucu. Gerekçe kodları altında listelenir.
- **açık aralık:** süren bir icra var; icra saatleri henüz kesin değildir.
- **Emniyet olayları — Bilinmiyor:** bu sayfa emniyet olaylarını saymaz; "olay yok" sonucu çıkarılmaz.
- **Eşitlendi / Eşitleme bekliyor / Çakışma / Başarısız:** eşitleme durumu. Eşitleme bekliyor, değişikliğin henüz sunucuya ulaşmadığını gösterir.

## Yapamazsınız / dikkat
- Kaydı yazan veya Kaptan incelemesine gönderen kişi aynı kaydı doğrulayamaz.
- Mutabakat sonucu bir onay değildir; incelemeyi Kaptan yapar.
- Kaydı tamamlamak ile doğrulamak aynı işlem değildir (13.03 §1).
- Çevrimdışıyken kaydedilmemiş tek bir değişiklik bu sayfanın belleğinde tutulur ve bağlantı dönünce özgün anahtarıyla gönderilir; sayfayı açık tutun. Gönderme ve inceleme çevrimiçi yapılır.
- Bir çakışma çıkarsa kendi kaydınızın otomatik kazanacağını varsaymayın (13.03 §6).

## Sorun olursa
- **"Sunucu eksik veya engelleyici kalemleri listeledi." mesajı:** **Engeller** listesindeki kodları giderin.
- **"Kayıt değişti veya komut çakışıyor." mesajı:** **En güncelini yükle** ile güncel kaydı inceleyin; tutulan yerel değişiklik bırakılır.
- **"Sonuç bilinmiyor." mesajı:** **Yeniden dene** aynı komutu tekrar gönderir.
- **Esaslı çakışma:** çevrimdışı kayıtlarda çakışma çıkarsa **Önemli çakışma merkezi** sayfasını kullanın (13.03 §10).

## İlgili sayfalar
