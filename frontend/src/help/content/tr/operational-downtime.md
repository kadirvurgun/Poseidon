---
key: operational-downtime
title: Operasyon kesintisi merkezi
roles: [Master, Chief Engineer, Technical Superintendent, DPA]
permissions: [operations.downtime.read, operations.downtime.write, operations.downtime.review]
related: [defects, work-order-center, engine-daily-log, today]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Bu sayfa, bir geminin **beyan edilen operasyon kesintilerini** (duruşlarını) kaynak bağlantısıyla birlikte kaydeder. Cevapladığı soru şudur: "Bu gemide hangi kesinti, ne zaman, hangi nedenle ve hangi arıza veya iş emrine bağlı olarak beyan edildi; incelemesi ve kapanışı nerede?" Sayfa kira kaybı hakkı, güvenli hizmet veya emniyet serbest bırakması belirlemez.

## Kim kullanır
- **Chief Engineer / Technical Superintendent:** teknik arıza veya bakım kaynaklı duruşu kaydetmek ve arıza ya da iş emri geçmişine bağlamak için (13.03 §5).
- **Master:** duruşun operasyon etkisini izlemek ve atanmışsa incelemek için (13.03 §2).
- **Atanmış inceleyici (Master, Birinci Zabit, Başmühendis, Filo Müdürü, DPA):** duruşu doğrulamak, düzeltmeye iade etmek veya kapatmak için.

## Başlamadan önce
- Üst bağlam çubuğunda **doğru şirketin** seçili olduğunu doğrulayın; sayfa yalnız yetki alanınızdaki gemileri listeler.
- Okumak için duruş okuma, taslak açmak için yazma yetkisi gerekir. İnceleme işlemleri için ayrıca inceleme yetkisi ve bu gemi için güncel bir görev ataması gerekir.
- Bağlı bir duruş açmak için arıza veya iş emri sayfasındaki **Bağlı duruş taslağı aç** düğmesini kullanabilirsiniz.

## Adım adım
1. "Gemi seçin" listesinden gemiyi seçin. **Kayıtlı operasyon kesintileri** listesi açılır.
2. Bu sayfadaki kayıtları daraltmak için **Bu sayfadaki nedenlerde ara** alanını kullanın.
3. Yeni kayıt için **Yeni operasyon kesintisi taslağı** düğmesine basın.
4. Her alan için önce değer durumunu seçin: **Eksik**, **Bilinmiyor** veya **Beyan edilen değer**. Değeri yalnız "Beyan edilen değer" seçiliyken girersiniz.
5. "Kesinti başlangıcı" ve "Kesinti bitişi" alanlarına saniye ve açık UTC farkı içeren zaman damgası yazın.
6. **Değerlendirilmemiş taslağı kaydet** düğmesine basın. Eksik taslak kaydedilebilir.
7. **Duruş inceleme ve kapanışı** bölümünde gerekirse **Kanıt seç** ile talep kanıtı bağlayın ve **İncelemeye gönder** düğmesine basın.
8. Atanmış inceleyici iseniz "Atama" seçip **Doğrula**, **Düzeltmeye iade et** veya **Kapat** işlemini yapın.
9. **Değişmez taslak geçmişi** bölümünde önceki sürümleri inceleyin.

## Durumlar ne anlama gelir
- **Taslak / İnceleme gerekli:** kayıtlı içerik değerlendirilmemiştir. Nedenleri "Taslak neden değerlendirilmemiş durumda?" bölümünde görünür.
- **Gönderilmedi / Açık / İncelemede:** kaydın yaşam döngüsü durumu.
- **Doğrulandı (insan):** kaydı yazan kişiden farklı, atanmış bir kişi doğruladı. Tamamlandı ≠ Doğrulandı (13.03 §1).
- **Kapatıldı:** duruş kapatıldı. Doğrulanmış kapanış emniyet serbest bırakması değildir.
- **Bilinmiyor:** değer beyan edilmedi veya belirlenemedi. Bilinmiyor; "Hayır" ya da "Uygulanmaz" anlamına gelmez. Kira kaybı ve kapanış alanları bu nedenle "Bilinmiyor" olarak korunur.
- **Kira kaybı (Loss of Hire) adayı:** yalnızca adaydır; hak veya tutar belirtmez.
- **Kayıtlı aralık:** değerlendirilmemiş mühendislik gözlemidir; beyan edilen dakikanın yerine geçmez.

## Yapamazsınız / dikkat
- Duruşu kaydeden kişi onu doğrulayamaz ve kapatamaz; bunu başka bir atanmış kişi yapmalıdır.
- Eksik zorunlu bilgi varken incelemeye gönderme, doğrulama ve kapatma yapılamaz.
- Talep kanıtı bağlamak doğrulama değildir.
- Sahip kaynaktan (arıza, iş emri) açıklama, zaman veya kapanış kararı taslağa kopyalanmaz; yalnız kesin sürüm bağlantısı tutulur.
- Çevrimdışıyken güncel kaynaklar okunamaz ve çevrimdışı depolama yoktur; girdiler yalnız açık sayfada kalır.

## Sorun olursa
- **Kayıt sonucu bilinmiyor:** özgün komut korunur. Aynı hesapla "Aynı operasyon kesintisi komutunu tekrar dene" düğmesini kullanın.
- **Kayıtlı taslak değişti:** başka biri kaydı değiştirmiş. Girdileriniz korunur; "Güncel taslağı oku" ile yeniden okuyun veya bırakın.
- **Kayıtlar veya güncel erişim kullanılamıyor:** rol ve gemi bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).
- **Kaynak değişmiş:** yeni taslak için sahip kaynağın güncel sürümünü kullanın.
- **Komut kabul edilmedi:** ekran görüntüsü, zaman ve adımlarla destek ekibine bildirin.

## İlgili sayfalar
