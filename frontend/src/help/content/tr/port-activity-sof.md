---
key: port-activity-sof
title: Liman faaliyeti ve SOF
roles: [Master, Chief Engineer]
permissions: [operations.port.read, operations.port.write, operations.port.finalize]
related: [noon-report, deck-daily-log, fuel-bunker-log, today]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Liman faaliyeti ve SOF sayfası, seçili geminin **liman olaylarını** kanıt referanslarıyla operasyonel kayıt olarak tutar ve atanmış bir kişinin **Statement of Facts (SOF)** belgesini içsel olarak kesinleştirmesini sağlar. Cevapladığı soru şudur: "Hangi liman olaylarını, hangi UTC sırası ve aralığıyla, hangi yük, su çekimi, kılavuz, römorkör, gecikme ve protesto referanslarıyla beyan ettik ve atanmış bir kişi bu SOF sürümünü kesinleştirdi mi?" Sayfa imza, laytime veya demuraj üretmez; kayıtlı olaylar talimat veya onaylı planlama aralığı değildir.

## Kim kullanır
- **Kaptan (Master):** varış ve liman faaliyetini kanıtlı operasyonel kayıt olarak tutmak ve SOF'u kesinleştirmek için (13.03 §2).
- **Birinci Zabit / Başmühendis:** liman olaylarını kaydetmek ve geçerli atamayla SOF'u kesinleştirmek için.

## Başlamadan önce
- Üst bağlam çubuğunda **doğru şirket ve gemiyi** seçin. Gemi seçili değilse sayfa "Gemi seçin" listesini gösterir.
- Okumak için `operations.port.read`, taslak kaydetmek için `operations.port.write` ve bu gemide düzenleme hakkı gerekir.
- Kesinleştirmek için `operations.port.write`, bu gemide düzenleme hakkı ve `operations.port.finalize` ile birlikte bu gemi için geçerli bir kesinleştirme ataması (Kaptan, Birinci Zabit veya Başmühendis) gerekir.

## Adım adım
1. Gemiyi seçin. **Kayıtlı liman olay çizelgesi** dönen olayları sunucunun UTC sırasıyla listeler. **Dönen sayfayı filtrele** yalnız bu sayfayı daraltır.
2. Listenin altındaki **Statement of Facts zaman çizelgesi** bölümünü okuyun: her olay **Kayıtlı aralık**, **Gecikme nedeni** ve **Açıklamalar** ile gösterilir.
3. **Yeni taslak kaydı** düğmesine basın. **Liman uğrağı bağlamı**, **Kayıtlı faaliyet ve zaman**, **Yük ve su çekimi beyanları** ve **Destek, istisnalar ve kanıt** gruplarını doldurun. Olay zamanları Z veya açık UTC farkı içermelidir.
4. **Doğrulanmamış taslağı kaydet** düğmesine basın. Her kayıt **Kayıtlı sürüm geçmişi** altına yeni sürüm ekler.
5. Kayıtlı kayıtta **Statement of Facts kesinleştirme** bölümünü açın. **Eksik zorunlu bilgiler** görünüyorsa önce bu alanları tamamlayın.
6. Listelenen her çözümlenmemiş referansı işaretleyerek açıkça onaylayın, **Atama** seçin ve **SOF’u kesinleştir** düğmesine basın. Sistem kimin, ne zaman, hangi görevle ve hangi gerekçeyle kesinleştirdiğini kaydeder.

## Durumlar ne anlama gelir
- **TASLAK:** SOF kesinleştirilmemiştir.
- **KESİN (içsel):** atanmış bir kişi bu sürümü kesinleştirmiştir. İçseldir: imza ya da dış mutabakat değildir.
- **İmza kaydedilmedi:** her zaman görünür; bu sayfada imza yoktur.
- **KESİN sonrası değiştirildi: yeni inceleme ve kesinleştirme gerekir.:** kayıt kesinleştirmeden sonra yeniden kaydedildi ve TASLAK durumuna döndü.
- **BİLİNMİYOR (çözümlenmemiş referanslar):** bu zorunlu referanslar için sahip arama kaynağı yok. Onaylamak onları bilinir hale getirmez.
- **İnceleme gerekli / Doğrulanmamış taslak kaydı:** liman olayları beyandır; hiçbir şey doğrulanmamıştır.

## Yapamazsınız / dikkat
- Sistem SOF'u kesinleştirmez. Kesinleştirme, atanmış Kaptan, Birinci Zabit veya Başmühendisin insan eylemidir (13.03 §2).
- Zorunlu bilgi eksikken, atama yokken veya çözümlenmemiş referanslar onaylanmadan SOF’u kesinleştir düğmesi kapalı kalır.
- Kesinleştirme imza, dış mutabakat, laytime veya demuraj değildir.
- Kesinleştirme durumu yalnız **Statement of Facts kesinleştirme** bölümünde (TASLAK / KESİN (içsel)) gösterilir. "SOF inceleme durumu" alanı, zaman çizelgesi notu ve sayfa altbilgisi kesinleştirmeyi yansıtmaz ve kesinleştirmenin kullanılamadığını söyleyebilir.
- Taslak kaydı ve kesinleştirme güncel çevrimiçi oturum gerektirir; bu sayfada çevrimdışı kayıt yoktur.

## Sorun olursa
- **"Bu gemi için geçerli kesinleştirme ataması yok.":** rolünüzü ve gemi bağlamınızı kiracı yöneticinizle kontrol edin (13.03 §10).
- **"Sonuç bilinmiyor: yeniden deneme aynı komutu bir kez gönderir.":** yeni kesinleştirme başlatmayın, **Yeniden dene** düğmesini kullanın.
- **"Komut kabul edilmedi":** gösterilen kodu okuyun, **Güncel kayıtlı kaydı oku** ile kaydı yenileyip tekrar deneyin.
- **"Kayıtlı kaynak değişti.":** yeniden kaydetmeden önce açıkça yenileyin veya bırakın.

## İlgili sayfalar
