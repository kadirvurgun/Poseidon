---
key: pms-command-center
title: PMS komuta merkezi
roles: [Chief Engineer, Technical Superintendent, Master]
permissions: [equipment.read]
related: [work-order-center, defects, critical-spares, departure-signoff, today]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
PMS komuta merkezi, seçili geminin bakım tarafındaki **istisnalarını** tek ekranda toplar. Cevapladığı soru şudur: "Bu gemide gecikmiş kritik iş, açık iş emri, arıza uyarısı veya kritik yedek parça eksiği var mı ve neden?" Sayfa tavsiye niteliğinde bir istisna görünümüdür; uygunluk, hazırlık veya emniyet kararı değildir. Sağlıklı kalemler listelenmez, yalnız sayılır.

## Kim kullanır
- **Chief Engineer / Technical Superintendent:** gecikmiş işleri, açık iş emirlerini, arıza uyarılarını ve kritik yedek eksiklerini izlemek için (13.03 §5).
- **Master:** PMS ve arıza kayıtlarının operasyonel kısıtlama etkisini görmek için (13.03 §2).

## Başlamadan önce
- Sayfayı görmek için ekipman okuma yetkisi (`equipment.read`) gerekir.
- Her bölüm kendi modülünün okuma yetkisine bağlıdır. Örneğin arıza okuma yetkiniz yoksa **Durum uyarıları** bölümü "Erişim gerekli: gösterilmiyor" olarak kalır.
- Önce bir gemi seçin. Gemi seçmeden sayfa yalnız **Bir gemi seçin** listesini gösterir.

## Adım adım
1. Listeden gemiyi seçin. Kapsamınızda gemi yoksa "Kapsamınızda gemi yok." yazar.
2. Üstteki **İstisna özeti** rozetini ve **Son yenileme** zamanını okuyun.
3. Bölümleri inceleyin: **Gecikmiş kritik işler**, **Vadesi gelen, geciken ve incelenecek işler**, **Açık iş emirleri**, **Durum uyarıları** ve **Kritik yedek parça eksikleri**.
4. Bir kalemde **Neden** bölümünü açın. Kayıtlı neden kodlarını ve varsa **Gözlendi** zamanını görün.
5. Kaynağa gitmek için **Kaynak kaydı aç** bağlantısını kullanın. Arıza kalemi [Arıza ve geçici tedbirler](/app/defects), iş emri kalemi [İş emri merkezi](/app/work-order-center) sayfasını açar.
6. Veriyi tazelemek için **Yenile** düğmesine basın.

## Durumlar ne anlama gelir
- **Engellendi / İnceleme gerekli / Dikkat:** istisna özetinin durumu. Ayrıntı bölümlerdeki kalemlerdedir.
- **Açık istisna yok (tavsiye):** okunan kaynaklarda açık istisna bulunmadı. Bu bir hazırlık veya uygunluk sonucu değildir.
- **Kritik / Dikkat / İnceleme gerekli:** kalemin önem derecesi.
- **Kaynak kullanılamıyor: sıfır değil, bilinmiyor:** bölümün kaynağı okunamadı. Bilinmiyor; "sıfır", "sorun yok" ya da "Hazır" anlamına gelmez.
- **Erişim gerekli: gösterilmiyor:** bu bölüm için yetkiniz yok; içerik ve sayı gösterilmez.
- **Bu kaynaktan istisna yok:** bölüm okundu ve istisna dönmedi. Sağlıklı kalem sayısı ayrıca yazar.
- **Eski veri / Güncellik bilinmiyor:** kalemin dayandığı okuma güncel değil veya güncelliği bilinmiyor.

## Yapamazsınız / dikkat
- Sayfa hiçbir kaydı değiştirmez; yalnız okur. İş emri, arıza ve yedek parça kayıtları kendi sayfalarında yürütülür.
- Sayfa bir gemiyi "hazır" ya da "güvenli" ilan etmez. Kalkış kararı Master'dadır (13.03 §2).
- Bir arıza yalnız insan tarafından kaydedilen kapanışla ve kapatılan beyan sürümü için kapalı sayılır. Beyan sonradan değişirse kalem yeniden incelemeye düşer.
- Çevrimdışıyken gösterilen veri eski olabilir ve **Yenile** kullanılamaz.

## Sorun olursa
- **Kayıt yüklenemedi:** bağlantıyı kontrol edip **Yeniden yükle** düğmesini kullanın.
- **Bu işlem veya kayıt için sunucu yetkiniz yok:** rol ve gemi bağlamınızı kontrol edin; gerekirse kiracı yöneticisine başvurun (13.03 §10).
- **Beklenmeyen istisna:** **Neden** bölümünü açın ve kaynak kaydı ilgili sorumlu veya SME ile inceleyin (13.03 §10).
- **Uygulama hatası:** Correlation ID, ekran, zaman ve adımlarla destek ekibine bildirin (13.03 §10).

## İlgili sayfalar
