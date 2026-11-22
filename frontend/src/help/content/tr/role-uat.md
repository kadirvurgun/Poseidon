---
key: role-uat
title: Rol UAT kayıtları
roles: [Product Owner, Tenant Admin]
permissions: [uat.read, uat.record, uat.threshold.record, uat.attest]
related: [pilot-gate, pilot-program]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Rol UAT kayıtları sayfası, gemi ve römorkör rolleriyle yapılan kullanıcı kabul testi (UAT) koşularını, senaryo sonuçlarını ve bulguları **insan kanıtı** olarak kaydeder ve bunları kabul edilen eşikle karşılaştırır. Cevapladığı soru şudur: "Her rol ve senaryo için teyitli UAT kanıtı insanlarca kabul edilmiş eşiği karşılıyor mu?" Sayfanın üstündeki uyarı geçerlidir: "Yalnız insan UAT kanıtı kaydedilir. Sistem UAT'yi asla kabul etmez ve pilot GO kararı vermez; otomatik test sonuçları ektir, PASS değildir."

## Kim kullanır
- **UAT kolaylaştırıcısı:** koşu açar, katılımcının gözlenen senaryo sonuçlarını ve bulguları kaydeder, koşuyu teyide gönderir. Katılımcı rolleri gemi için Master, DPA, Chief Engineer ve Crew gibi rollerdir (13.03 §2, §4, §5, §6).
- **Teyit eden kişi:** kolaylaştırıcıdan farklı bir kişi olarak koşuyu teyit eder.
- **UAT sahipleri / Ürün Sahibi (Product Owner):** kabul edilen eşiği kaydeder ve sonucu **Pilot go/no-go incelemesi** sayfasında kullanır.

## Başlamadan önce
- Sayfayı görmek için tenant geneli okuma ve UAT okuma yetkisi gerekir. Koşu ve sonuç kaydı, eşik kaydı ve teyit için ayrı yetkiler gerekir.
- Eşiği UAT sahipleri önceden kabul etmiş olmalıdır; sistem eşik belirlemez.
- Katılımcı için bir takma ad belirleyin. Ad, e-posta veya personel numarası kullanmayın.
- Kayıt canlı bağlantı gerektirir; çevrimdışıyken kayıt devre dışıdır.

## Adım adım
1. **Operasyon modu** düğmelerinden **Gemi** veya **Römorkör** seçin.
2. **Kabul edilen eşiğe göre UAT kanıtı** bölümünde durumu, **Açık engelleyiciler**, **Açık majör bulgular**, **Teyitli koşular**, **Teyit bekleyen** sayılarını ve senaryo tablosunu inceleyin.
3. Eşik yoksa **Kabul edilen eşiği kaydet** formunda **Asgari senaryo geçme oranı**, **Azami açık majör bulgu**, **Tasarım uyumu**, **Mutabakat referansı** ve **Gerekçe** girip **Kaydet** düğmesine basın.
4. **UAT koşuları** bölümünde **Yeni UAT koşusu** formuyla **Rol**, **Ortam**, **Build commit** ve **Katılımcı takma adı** girip **Kaydet** düğmesine basın.
5. Koşuyu açın. **Sonuç kaydet** ile **Senaryo** ve **Sonuç** seçin; gerekiyorsa **Yardım alındı** ve **Otomatik kanıt referansı** ekleyin. Sorun için **Bulgu kaydet** ile **Önem** ve **Bulgu özeti** girin.
6. Senaryoları kaydettiğinizde **Koşuyu teyide gönder** düğmesine basın (yalnız kolaylaştırıcı). Sonucu olmayan senaryolar **Koşulmadı** sayılır. Kolaylaştırıcıdan farklı bir kişi **Koşuyu teyit et** formunda **Teyit beyanı** yazıp **Kaydet** düğmesine basar.
7. **Açık bulgular** listesinde bir bulguyu **Çözüm** ve **Çözüm notu** ile **Çöz** düğmesine basarak kapatın.

## Durumlar ne anlama gelir
- **Eşik belirlenmedi:** "İnsan tarafından kabul edilmiş eşik kaydı yok. Eşik kaydedilene kadar UAT geçemez."
- **Ölçütler karşılanmadı / Kanıt eksik:** teyitli kanıt eşiği karşılamıyor veya her rol ve senaryo kapsanmamış.
- **Ölçütler karşılandı; insan kararı bekleniyor:** en iyi durum budur. UAT kabulü veya pilot GO değildir; karar insanındır.
- **Koşulmadı:** senaryo için sonuç yok. Koşulmamış senaryo geçmiş sayılmaz.
- **Açık / Gönderildi / Teyit edildi:** koşunun durumu. Eşik hesabına yalnız teyit edilmiş koşular girer.
- **Geçti / Kaldı / Engellendi / Koşulmadı:** katılımcının gözlenen senaryo sonucu.
- **Engelleyici / Majör / Minör:** bulgunun önemi. Açık engelleyici varsa kapı başarısız olur.
- **Kurgusal fikstür:** sentetik örnek kayıt; gerçek UAT kanıtı değildir.

## Yapamazsınız / dikkat
- Kendi kolaylaştırdığınız koşuyu teyit edemezsiniz: "Teyit, kolaylaştırıcıdan farklı bir kişi tarafından kaydedilmelidir."
- Kendi bildirdiğiniz bulguyu çözemezsiniz. Engelleyici bulgu yalnız **Çözüldü (düzeltildi ve yeniden test edildi)** olarak kapanır; **Risk kabul edildi** seçeneği yoktur.
- Otomatik (Playwright) sonuç yalnız ek kanıttır; sonuç katılımcının gözlenen sonucudur.
- Sistem UAT'yi kabul etmez, eşik koymaz ve teyit veya imza üretmez.

## Sorun olursa
- **Bu kayıtlara erişiminiz yok.** Rol ve tenant kapsamınızı kontrol edip Tenant Admin'e başvurun (13.03 §10).
- **Kayıt kiracınızda bulunamadı.** Koşu bağlantısını ve seçili modu kontrol edin.
- **Kayıt değişti veya daha önce kullanıldı. Yeniden yükleyin.** **Yeniden yükle** ile güncel durumu açın.
- **Sonuç belirsiz. Yeniden deneme aynı komutu gönderir.** **Yeniden dene** ile aynı komutu güvenle gönderin.
- **Kurallar gereği reddedildi.** Örneğin senaryo koşunun rolüne ait değildir veya engelleyici bulgu risk kabulüyle kapatılamaz.
- **İzin verilmiyor.** Örneğin bu koşuyu siz kolaylaştırdınız ve teyit edemezsiniz ya da bu bulguyu siz bildirdiniz ve çözemezsiniz.
- **Çevrimdışısınız. Bağlantı dönene kadar kayıt devre dışı.** Bağlantı dönünce **Yenile** düğmesine basın.

## İlgili sayfalar
