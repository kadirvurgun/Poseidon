---
key: pilot-program
title: Pilot program haftaları ve değer raporu
roles: [Product Owner, Tenant Admin]
permissions: [pilot.program.read, pilot.program.manage, pilot.program.record, pilot.program.triage, pilot.program.decide]
related: [pilot-gate, role-uat, audit-ready]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Pilot program sayfası, bir gemi (ve isteğe bağlı bir römorkör) için pilot haftalarını, her haftanın kontrol maddelerini, pilot sorunlarını, günlük geri bildirimi ve değer raporunu tek yerde tutar. Cevapladığı soru şudur: "Her pilot haftasının maddeleri gerçek kayıtlarla karşılandı mı ve ölçülen değerler referansa göre nasıl değişti?" Sayfanın üstündeki uyarı geçerlidir: "Yazılım bir pilotu tamamlayamaz. Bir kontrol maddesi yalnız sunucunun doğruladığı gerçek bir kayda sizin verdiğiniz bağlantıyla karşılanır; eksik referans değer Bilinmiyor kalır; haftayı kapatan ve program kararını kaydeden yalnız atanmış Ürün Sahibidir."

## Kim kullanır
- **Ürün Sahibi (Product Owner):** haftaları kapatır ve program kararını (Devam, Düzelt, Durdur, Genişlet) kaydeder. Bunun için geçerli Ürün Sahibi ataması gerekir.
- **Pilot ekibi:** kontrol maddelerine kayıt bağlar, sorun bildirir, geri bildirim ve ölçüm kaydeder.
- **Triyaj sorumlusu:** sorunları triyaj eder ve çözer.
- 13.03 bu sayfa için ayrı rol tanımlamaz; tenant yönetimi ilkeleri geçerlidir (13.03 §9).

## Başlamadan önce
- Sayfayı görmek için tenant geneli okuma ve pilot program okuma yetkisi gerekir. Program oluşturma, kayıt, triyaj ve karar için ayrı yetkiler, karar ve hafta kapanışı için ayrıca Ürün Sahibi ataması gerekir.
- Bağlayacağınız kayıtlar (örneğin sertifika, ekip veya iş emri kaydı) ilgili sayfalarda önceden oluşturulmuş olmalıdır.
- Geri bildirim için ad, e-posta, telefon veya kimlik numarası girmeyin; yalnız takma ad ve rol kullanın.
- Kayıt canlı bağlantı gerektirir.

## Adım adım
1. **Pilot programları** listesinden bir program açın. Yeni program için **Pilot programı oluştur** formunda **Program adı**, **Gemi**, **Römorkör (isteğe bağlı)**, **1. hafta başlangıç tarihi** ve **Takvim saat dilimi (IANA)** girip **Kaydet** düğmesine basın.
2. Her **Hafta** bölümünde **Kontrol maddesi**, **Kriter (kaynak)**, **Alan**, **Durum** ve **Bağlı kayıtlar** sütunlarını inceleyin.
3. Bir maddeyi karşılamak için **Kayıt bağla** formunda **Kontrol maddesi**, **Kayıt türü** ve **Kayıt kimliği** girip **Kayıt bağla** düğmesine basın. Yanlış bağlantıyı **Geri çekme gerekçesi** yazarak **Bağlantıyı geri çek** ile kaldırın.
4. **Pilot sorunları** bölümünde **Sorun bildir** ile alan, **Kategori**, önem ve **Sorun özeti** girin. Triyaj sorumlusu **Triyaj notu** ile **Triyaj** yapar, sonra sorunu çözer.
5. **Günlük geri bildirim** bölümünde **Geri bildirim kaydet** ile **Özet** ve **Duygu** girin.
6. **Değer raporu** bölümünde **Ölçüm kaydet** ile **Metrik**, **Aşama** (Referans veya Pilot dönemi), **Değer**, **Örneklem büyüklüğü**, **Dönem başlangıcı**, **Dönem sonu**, **Kanıt referansı** ve **Yöntem ve örneklem** girin.
7. Ürün Sahibi iseniz **Haftayı kapat** formunda sonucu ve gerekçeyi kaydedin. Program sonunda **Program kararını kaydet** formunda karar, **Atama** ve **Gerekçe** girip **Kaydet** düğmesine basın.

## Durumlar ne anlama gelir
- **Bağlı ve doğrulandı:** maddeye sunucunun doğruladığı gerçek bir kayıt bağlı. **Bağlantı artık geçerli değil** ve **Eksik** maddeler karşılanmamıştır.
- **Tamamlanma gerekli:** bağlanan kayıt tamamlanmış durumda olmalıdır. "Denetim izi kaydı bir kişinin yazdığını kanıtlar; kaydın veya akışın tamamlandığını kanıtlamaz."
- **İnsan kapanışına hazır / Hazır değil:** hafta için öneridir; haftayı yalnız Ürün Sahibi **Kabul edildi** veya **Kabul edilmedi** olarak kapatır.
- **Aynı gün triyaj yapıldı / Geç triyaj yapıldı / Triyaj bugün bekleniyor / Gecikmiş:** P0 sorunların programın takvim gününe göre triyaj zamanlaması. **Kaynakta aynı gün hedefi yok** olan sorunlara sistem süre uydurmaz.
- **Ölçüldü / Bilinmiyor:** değer raporunda referans veya pilot değeri eksikse sonuç **Bilinmiyor** kalır; sıfır veya iyileşme sayılmaz. **Karşılaştırılamaz** değerler kıyaslanmaz.
- **Telemetri (beyan, doğrulanmamış):** beyan edilen telemetri değeridir.
- **Genişletmeye uygun; insan kararı bekleniyor / Düzeltme önerilir:** program önerisidir, karar değildir.

## Yapamazsınız / dikkat
- "Kabul yalnız her madde doğrulanmışsa ve açık, bekleyen ya da geç kalmış P0 triyajı yoksa mümkündür."
- "Genişletme yalnız her hafta kabul edilmişse ve değerler, UAT, arızalar ve P0 temizse mümkündür."
- Haftanın veya kararın dayandığı kanıtı (bağlantı, geri bildirim, ölçüm) kendiniz oluşturduysanız o haftayı kapatamaz veya kararı kaydedemezsiniz.
- Kurgusal fikstür programında hafta kapanışı veya karar kaydedilemez.
- Sistem pilotu tamamlamaz, haftayı kapatmaz ve karar veya imza üretmez.

## Sorun olursa
- **Geçerli bir Ürün Sahibi atamanız yok; hafta kapanışları ve kararlar salt okunurdur.** Atama için Tenant Admin'e başvurun (13.03 §10).
- **Bir operatör saklama politikasını yapılandırana kadar geri bildirim alınamaz.** Sistem yöneticisinden saklama politikasını yapılandırmasını isteyin.
- **Kayıt değişti veya daha önce kullanıldı. Yeniden yükleyin.** **Yeniden yükle** ile güncel durumu açın.
- **Sonuç belirsiz. Yeniden deneme aynı komutu gönderir.** **Yeniden dene** ile aynı komutu güvenle gönderin.
- **Kurallar gereği reddedildi.** Örneğin bağlanan kayıt yok, kapsam dışında veya tamamlanmamış olabilir.
- **Bu kayıtlara erişiminiz yok.** Rol ve tenant kapsamınızı kontrol edin (13.03 §10).

## İlgili sayfalar
