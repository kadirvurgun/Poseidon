---
key: system-health
title: Sistem sağlığı
roles: [Tenant Admin, Support / Engineering]
permissions: [directory.read, audit.read, system.health.read]
related: [audit, notification-escalation, offline-conflict-center, user-admin]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
"Sistem sağlığı", yöneticiler için **servis gözlemlerini** tek ekranda gösterir: API, audit yazıcısı, eşitleme, bildirim işleri, dosya servisi ve girdi güncelliği. Cevapladığı soru şudur: "Seçili araç ve bu kuruluş için hangi servis sinyali gözlendi, hangisi gözlenmedi?" Yapılandırma ve eksik telemetri, sağlıklı servis veya araç hazırlığı belirtmez.

## Kim kullanır
- **System / Tenant Admin:** servis ve eşitleme sorunlarını fark edip kontrollü biçimde ele almak için (13.03 §9).
- **Support / Engineering:** bir uygulama hatası bildirildiğinde gözlemleri incelemek için (13.03 §10).

## Başlamadan önce
- Sayfa yalnız kuruluş genelinde okuma, işlem geçmişi okuma ve sistem sağlığı okuma yetkisi olan kullanıcılara görünür.
- Üst bağlam çubuğunda **doğru şirketin** seçili olduğunu doğrulayın.
- Gözlemler araç bazında istenir; önce bir araç seçmeniz gerekir.

## Adım adım
1. **Araç gözlem kapsamı** listesinden aracı seçin. Liste uzunsa **Önceki araçlar** / **Sonraki araçlar** düğmelerini kullanın.
2. "Okuma zamanı" bilgisini kontrol edin.
3. **Servis canlılığı** kartında süreç başlangıcını ve çalışma süresini okuyun.
4. **API** ve **Audit yazıcısı** kartlarında istek, hata, kalıcı kayıt ve gecikme sayılarını okuyun. Bu sayılar süreç başlangıcından beri bu kuruluş içindir.
5. **Eşitleme** kartında kaydedilmiş sonuçları, çözümlenmemiş çakışmaları ve son işlenen senkronizasyonu görün.
6. **Bildirim işleri** kartında açık bildirimleri, süresi geçmiş eskalasyonları ve değerlendirme işinin durumunu görün.
7. **Dosya servisi** ve **Girdi güncelliği** kartlarını okuyun.
8. **Pilot metrikleri** bölümünde kayıtlı pilot istemcilerinden gelen gözlemleri inceleyin.
9. Güncel veri için **Gözlemleri yenile** düğmesine basın.

## Durumlar ne anlama gelir
- **Gözlenmedi / Kullanılamıyor:** bu sinyal için gözlem yok. Bu "sağlıklı" ya da "sorun yok" anlamına gelmez.
- **Yapılandırılmış · kontrol edilmedi:** dosya servisi ayarlı görünür ama canlı depolama veya tarayıcı kontrolü yapılmadı.
- **Plan dahilinde / Çalışıyor / Gecikmiş / Zamanlayıcı etkin değil / Çalışma yarıda kaldı (kilit süresi doldu):** bildirim değerlendirme işinin durumu.
- **Çözümlenmemiş çakışmalar:** insan çözümü bekleyen offline çakışmalar. Otomatik birleştirilmezler.
- **Güncel / Eski / Bilinmiyor / Kullanılamıyor:** girdi güncelliği değerlendirmelerinin sayısı. Bilinmiyor, "Güncel" anlamına gelmez.
- **Bu anlık görüntü iki dakikadan eski:** güvenmeden önce yenileyin.

## Yapamazsınız / dikkat
- Bu sayfa salt okunurdur; servis, araç veya kayıt üzerinde işlem yapılmaz.
- Bildirim kayıtları yalnız uygulama içidir; teslimin veya herhangi bir emniyet kararının kanıtı değildir.
- Servis canlılığı yalnız bu API sürecinin yanıt verdiğini gösterir; diğer servislerin çalıştığını kanıtlamaz.
- Pilot metrikleri beyan edilmiş ve doğrulanmamıştır; kabul iddiası değildir.
- Üretimde doğrudan veri düzeltme yapılmaz; yapılandırma değişiklikleri kontrollü yayınlanır (13.03 §9).

## Sorun olursa
- **Bu bağlam için sağlık erişimi kullanılamıyor:** rol ve kuruluş kapsamınızı kontrol edin.
- **Servis gözlemleri kullanılamıyor:** sağlıklı durum varsayılmaz; bir süre sonra "Gözlemleri yenile" düğmesini kullanın.
- **Çözümlenmemiş çakışma var:** ilgili kullanıcıyı Çakışma Merkezi'ne yönlendirin (13.03 §10).
- **Uygulama hatası:** Correlation ID, ekran, zaman ve adımlarla destek/mühendislik ekibine bildirin.

## İlgili sayfalar
