---
key: audit
title: İşlem geçmişi
roles: [DPA / HSEQ, Regulatory / Assurance, Tenant Admin]
permissions: [audit.read]
related: [audit-ready, user-admin, regulatory-review]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
İşlem geçmişi sayfası, yetki alanınızdaki iş kayıtlarında yapılan değişikliklerin **salt okunur** ve kime ait olduğu belirtilmiş kaydını gösterir. Cevapladığı soru şudur: "Bu kayıt ne zaman, hangi işlemle, hangi aktör ve rol bağlamında değişti?" Güvenlik olayları ayrı kısıtlanır ve bu sayfada görünmez (13.03 §4, §9).

## Kim kullanır
- **DPA / HSEQ:** bulgu, aksiyon ve doğrulama kayıtlarının izini sürmek için (13.03 §4).
- **Regulatory / Assurance kullanıcısı:** denetim ve inceleme öncesi kayıt geçmişini kontrol etmek için (13.03 §8).
- **Tenant Admin:** üyelik ve yetki değişikliklerini, vekalet başlangıç-bitiş zamanlarını denetlemek için (13.03 §9).

## Başlamadan önce
- Sayfayı görmek için işlem geçmişi okuma yetkisi gerekir. Yetkiniz yoksa "İşlem geçmişi yetkisi gerekiyor." mesajı görünür.
- Yalnız yetkili olduğunuz kiracı ve gemi kapsamındaki kayıtlar listelenir.
- Belirli bir kaydı arıyorsanız **Araç kimliği** veya **Nesne kimliği** değerini hazır edin. Diğer sayfalardaki geçmiş bağlantıları bu sayfayı filtreli açar.

## Adım adım
1. Filtre alanlarını doldurun: **Araç kimliği**, **Nesne kimliği**, **Nesne türü** ve **İşlem** (**Tümü**, **Oluşturuldu**, **Güncellendi**, **Silindi**).
2. **Filtrele** düğmesine basın. Filtreleri sıfırlamak için **Filtreleri temizle** düğmesini kullanın.
3. **Yetkili kayıtlar** listesinde **Zaman**, **Nesne** ve **İşlem** sütunlarını inceleyin. Sayfalar arasında **İlk sayfa** ve **Sonraki** ile gezinin.
4. Bir satırdaki nesne bağlantısına basarak **Kayıt ayrıntıları** bölümünü açın. **Aktör referansı**, **Atanmış rol referansları**, **Güvenilir yetkiler**, **Gerekçe**, **Üst veri**, **Kaynak sürümü** ve **Korelasyon** alanlarını okuyun.
5. Değişmez revizyon kanıtı varsa **Değişmez revizyonu aç** düğmesiyle kaydın o anki halini görün. **Kapat** ile ayrıntıları kapatın.

## Durumlar ne anlama gelir
- **Oluşturuldu / Güncellendi / Silindi:** kayıt üzerindeki işlem türü.
- **Kaydedildi:** üst veri eksiksiz kaydedilmiş.
- **Tarihsel üst veri eksik / Tarihsel kimlik mevcut değil / Tarihsel bağlam mevcut değil:** eski kayıtlarda bu bilgi tutulmamıştı. Eksik bilgi tahmin edilerek doldurulmaz.
- **Değerler gizlendi:** alan değeri gizlilik nedeniyle gösterilmez; yalnız değiştiği bilgisi görünür.
- **Bu kayıt için değişmez revizyon kanıtı mevcut değil.** Bu kayıt türü için revizyon görüntüsü yoktur.

## Yapamazsınız / dikkat
- Sayfa salt okunurdur. İşlem geçmişi kaydı düzenlenemez veya silinemez.
- Bir kaydın işlem geçmişinde görünmesi, işin doğrulandığı veya etkili olduğu anlamına gelmez; Doğrulandı ≠ Etkili (13.03 §4).
- Bu sayfa iş değişikliklerinin kaydıdır. Tek başına tam kullanıcı eylemi denetimi veya gizlilik uyumu kanıtı değildir; güvenlik olayları ayrı tutulur.
- Liste yalnız yetkili olduğunuz kapsamı gösterir. Kapsamınız dışındaki kayıtlar listede yer almaz; bir kaydı göremiyorsanız bu, değişiklik yapılmadığı anlamına gelmez.

## Sorun olursa
- **İşlem geçmişi yetkisi gerekiyor.** Rol ve kapsamınızı kontrol edin; gerekirse Tenant Admin ile görüşün (13.03 §10).
- **Devam etmek için oturum açın.** Yeniden oturum açın.
- **Geçmişe ulaşılamıyor. Filtreleri kontrol edip tekrar deneyin.** Kimlik değerlerinin tam ve doğru olduğunu kontrol edip **Tekrar dene** düğmesine basın.
- **Bu filtrelere uyan yetkili kayıt yok.** Filtreleri genişletin.
- Uygulama hatası sürerse korelasyon kimliği, ekran, zaman ve adımlarla destek ekibine bildirin (13.03 §10).

## İlgili sayfalar
