---
key: fuel-bunker-log
title: Yakıt, ikmal ve ROB
roles: [Chief Engineer, Master]
permissions: [operations.fuel.read, operations.fuel.write]
related: [noon-report, engine-daily-log, port-activity-sof, operational-downtime]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Yakıt, ikmal ve ROB sayfası, seçili geminin **yakıt ve ikmal beyanlarını** kaynak geçmişiyle birlikte doğrulanmamış taslak olarak tutar. Cevapladığı soru şudur: "Ne kadar yakıt alındı veya tüketildi, önceki ve sonraki ROB için ne beyan ettik, hangi BDN, tedarikçi, numune ve tank referansları buna ait ve beyan edilen ROB zinciri bir kayıttan diğerine sürüyor mu?" Sayfa miktarları düzeltmez, emisyon hesaplamaz, uyum veya doğrulama oluşturmaz.

## Kim kullanır
- **Başmühendis / makine zabitleri:** ikmal, tüketim ve ROB beyanlarını kanıt referanslarıyla kaydetmek için (13.03 §5).
- **Kaptan (Master):** yakıt beyanlarını kaptan incelemeli öğle özetleriyle yan yana izlemek için (13.03 §2).

## Başlamadan önce
- Üst bağlam çubuğunda **doğru şirket ve gemiyi** seçin. Gemi seçili değilse sayfa "Gemi seçin" listesini gösterir.
- Okumak için `operations.fuel.read` gerekir. Oluşturup kaydetmek için `operations.fuel.write` ve bu gemide düzenleme hakkı gerekir.
- Zamanlar tarih, saniye ve Z veya açık UTC farkı içermelidir; saat dilimsiz yerel saat kabul edilmez.

## Adım adım
1. Gemiyi seçin. **Yakıt ve ROB kayıtları** dönen kayıtları listeler. **Dönen sayfayı filtrele** yalnız bu sayfayı daraltır.
2. Listenin altındaki **ROB süreklilik incelemesi** bölümünü okuyun. Beyan edilen her yakıt türü için dengeyi (önce + alınan − tüketilen − sonra), **Önceki kayıtla süreklilik** ve **İnceleme işaretleri** bilgisini gösterir.
3. **Kaptan incelemeli Noon yakıt ve ROB özetleri** bölümünü bağlam olarak okuyun. Beyan edilen metin olarak gösterilir, deftere kopyalanmaz.
4. **Yeni taslak kaydı** düğmesine basın. **Kayıt ve ikmal bağlamı**, **Miktar ve ROB beyanları** ve **Yakıt özellikleri ve numune kaydı** gruplarını doldurun. Her alan için beyan edilen değeri, **Eksik** ya da **Bilinmiyor** durumunu kullanın.
5. **Doğrulanmamış taslağı kaydet** düğmesine basın. Her kayıt **Kayıtlı sürüm geçmişi** altına yeni sürüm ekler.
6. Kayıtlı bir öğle raporu veya makine kaydından başlamak için o sayfadaki **Kayıtlı kaynaktan yakıt kaydı aç** düğmesini kullanın. **Bağlı günlük beyan** burada salt okunur kalır.

## Durumlar ne anlama gelir
- **Taslak beyan / Doğrulanmamış taslak kaydı:** değerleri bir kişi beyan etmiştir; hiçbir şey doğrulanmamıştır.
- **İnceleme gerekli:** her kaydın uyum inceleme durumu; uyum değerlendirilmemiştir.
- **Bekliyor; doğrulama kanıtlanmış değil:** doğrulama inceleme durumu; doğrulama yoktur.
- **Uyumsuzluk saptanmadı:** beyan edilen sayılar birbirini tutuyor. Bu doğrulama değildir.
- **Bilinmiyor:** süreklilik değerlendirilemiyor; örneğin bir değer eksik ya da eski geçmiş değerlendirme penceresinin dışında. Bilinmiyor; "uyumsuzluk yok" ya da "Hazır" anlamına gelmez.
- **Eksik / Bilinmiyor:** sıfırdan ayrıdır.

## Yapamazsınız / dikkat
- İnsan incelemesi, operasyon/hukuk onayı, sonlandırma ve çevrimdışı kayıt bu sayfada kullanılamaz.
- Sistem farkları insan incelemesi için işaretler; hiçbir miktarı düzeltmez ve yakıt uyumuna karar vermez.
- Yalnızca en yeni 100 kayıt değerlendirilir. Tank referansı yoksa çok tanklı gemide görülen fark veri hatası değil, eksik tank verisi sorusudur.
- Birimi çözümlenmemiş öğle veya makine kaydı miktarları alınan, tüketilen veya ROB değerine dönüştürülmez.

## Sorun olursa
- **"Kayıtlı kaynak değişti.":** girdileriniz korunur; yeniden kaydetmeden önce açıkça yenileyin veya bırakın.
- **"Sonuç bilinmiyor.":** yeniden yazmayın, **Aynı operasyon komutunu tekrar dene** düğmesini kullanın.
- **"Beyan değerlerini, açık saat farklarını ve güncel kaynak yetkisini kontrol edin.":** işaretli alanı düzeltin veya Eksik ya da Bilinmiyor bırakın.
- **Operasyon kaynağı veya erişimi kullanılamıyor:** **Kaynak okumayı tekrar dene** düğmesini kullanın; erişim reddi sürerse rolünüzü ve gemi bağlamınızı kiracı yöneticinizle kontrol edin (13.03 §10).

## İlgili sayfalar
