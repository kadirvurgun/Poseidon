---
key: tug-operation-history
title: Römorkör operasyon geçmişi
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [towage-execution, tug-daily-log, tug-job-board, tug-operations-offline]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Operasyon geçmişi, römorkör işlerinin, yürütmelerin, günlük defterlerin ve kanıtların **salt okunur geçmişini** gösterir. Cevapladığı soru şudur: "Hangi işler hangi sonuçla bitti, yürütmeler doğrulandı mı, kanıt kaydı var mı ve hangi günlük defterler bu işlere bağlı?" İptal edilen ve durdurulan kayıtlar görünür kalır; hiçbir şey silinmez veya yeniden etiketlenmez.

## Kim kullanır
- **Römorkör operasyon koordinatörü / Kıyı operasyonları:** tamamlanan işlerde kanıt bütünlüğünü, olay ve geçmiş kayıtlarını izlemek için (13.03 §7).
- **Tug Master:** kendi römorkörünün geçmiş işlerini ve yürütme zaman çizelgelerini görmek için (13.03 §3).

## Başlamadan önce
- Römorkörü **Römorkör operasyonları** sayfasından açın; römorkör seçili değilse sayfa "Devam etmek için bir römorkör seçin." gösterir.
- Römorkör okuma yetkisi yeterlidir; sayfa veri değiştirmez.
- Geçmiş sunucudan okunur; çevrimdışıyken yenilenemez.

## Adım adım
1. **Filtreler** formunda gerekirse **Römorkör kimliği**, **Başlangıç (UTC tarih)**, **Bitiş (UTC tarih)** ve **Sonuç** seçin; **Filtreleri uygula** düğmesine basın. Tarih aralığı en fazla 400 gündür.
2. Tablodaki her satırda **İş** (iş numarası, hizmet verilen gemi, iş tipi, plan ve brifing durumu), **Faaliyet zamanı**, **Sonuç** ve **Kanıt** sütunlarını okuyun.
3. **Yürütmeler** sütununda her yürütmenin durumunu, doğrulama durumunu, süresini, Master kaydını ve kanıt girişi sayısını görün; **Yürütme zaman çizelgesini aç** bağlantısıyla yürütme konsoluna geçin.
4. **Günlük defterler** sütunundaki bağlantılarla ilgili günlük defteri açın. "defter tarafından bağlanmamış" notu, defterin bu yürütmeyi bağlamadığını gösterir.
5. Daha eski kayıtlar için **Daha fazla yükle** düğmesini kullanın. **Okunma zamanı** verinin ne zaman okunduğunu gösterir.

## Durumlar ne anlama gelir
- **Başlamadı / Devam ediyor / Tamamlandı (kayıtlı) / Durduruldu / İptal edildi / Karışık:** işin genel sonucu. Tamamlandı (kayıtlı), işin doğrulandığı anlamına gelmez.
- **Doğrulandı, kanıt kayıtlı / Doğrulandı, kanıt girişi yok / Doğrulama reddedildi / Kanıt doğrulanmadı / Bilinmiyor:** kanıt ve doğrulama durumu.
- **doğrulama bekliyor / doğrulandı / doğrulama reddedildi:** tek bir yürütmenin doğrulama durumu.
- **Yok:** bu iş için bağlı kayıt yok. Bu, kaydın gerekmediği anlamına gelmez.

## Yapamazsınız / dikkat
- Bu sayfada hiçbir kayıt düzenlenmez, silinmez veya doğrulanmaz.
- Sessiz silme yok: iptal edilen işler ve durdurulan ya da reddedilen yürütmeler geçmişte kalır.
- Tamamlandı ≠ Doğrulandı: doğrulama ayrı bir kişinin kaydıdır.
- Gösterilen veri **Okunma zamanı** anındaki durumdur; çevrimdışıyken yenilenmez.

## Sorun olursa
- **"Filtre geçerli değil" mesajı:** römorkör kimliğinin UUID olduğunu ve tarih aralığının 400 günü aşmadığını kontrol edin.
- **"Geçmiş yüklenemedi." mesajı:** bağlantınızı kontrol edip **Yeniden dene** düğmesine basın.
- **"Bu römorkör kapsamına erişiminiz yok.":** rol ve römorkör bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).
- **Kayıt doğrulanamıyor:** yürütme zaman çizelgesindeki kanıt referansını ve kaynağını kontrol edin; doğrulayıcıya başvurun (13.03 §10).

## İlgili sayfalar
