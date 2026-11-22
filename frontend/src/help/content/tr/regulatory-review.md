---
key: regulatory-review
title: Regülasyon uzman incelemesi
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.review.read, regulatory.review.manage, regulatory.review.verdict]
related: [sources, requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Regülasyon uzman incelemesi, bir gemi veya römorkör için kaynak ve uygulanabilirlik kalemlerinin insan uzman tarafından incelendiği kampanyaları kaydeder. Cevapladığı soru şudur: "Bu geminin hangi kaynak ve gereksinim kalemleri yetkili bir inceleyici tarafından teyit edildi, hangileri hâlâ açık ve takipte?" Yalnız insan inceleme kaydıdır: sistem hiçbir gereksinimi onaylamaz, kural veya izin etkinleştirmez, klas ya da tip onayı iddia etmez (13.03 §8).

## Kim kullanır
- **Regulatory / Assurance kullanıcısı:** inceleme kampanyası açmak, kalemlere karar kaydetmek ve kampanyayı kapatmak için (13.03 §8).
- **Atanmış inceleyici (SME):** kendi kaynak otoritesi kapsamındaki kalemleri teyit etmek veya takibe almak için.
- **DPA / HSEQ:** açık ve takipsiz kalemleri izlemek için (13.03 §4).

## Başlamadan önce
- Sayfayı görmek için regülasyon inceleme okuma yetkisi gerekir. Kampanya açıp kapatmak için yönetim, karar kaydetmek için karar yetkisi ve bu kapsam için güncel bir inceleyici ataması gerekir.
- **İnceleme kapsamı** sekmesinden **Gemi** veya **Römorkör** seçin, ardından **Bir gemi seçin** listesinden doğru gemiyi açın (13.03 §1).
- Kaynakların [Kaynak taslakları](/app/sources) sayfasında resmî ve sürümlü olarak kayıtlı olması gerekir (13.03 §8).
- Çevrimdışıyken veriler güncel olmayabilir ve komutlar devre dışıdır.

## Adım adım
1. Kapsamı ve gemiyi seçin. Kampanya yoksa **İnceleme kampanyası aç** düğmesine basın.
2. **Kaynak sahipliği matrisi** tablosunda her kaynak otoritesinin kategorisini, açık ve teyit edilen kalem sayısını ve sahip rollerini inceleyin.
3. **İnceleme kalemleri** tablosundan bir kalem seçin.
4. **İnceleme kararını kaydet** formunda **İnceleyici ataması**, **Karar**, **Kaynak otoritesi** ve **Sahip rolü** alanlarını doldurun. Kalemin kayıtlı bir kaynağı yoksa **Yetkili kaynak referansı** ve **Kaynak sürümü / baskısı** alanlarını girin. Açık kalan bir kalem için **Takip kaydı kimliği** girin.
5. **İnceleyici notu** alanına 10–2000 karakterlik gerekçe yazın ve **Kaydet** düğmesine basın.
6. Tüm kalemler sonuçlandığında **Kampanyayı kapat** formunda **Kapanış sonucu** seçin, **Kapanış gerekçesi** yazın ve **Kampanyayı kapat** düğmesine basın.

## Durumlar ne anlama gelir
- Kampanya: **Açık**, **İncelemede**, **Açık kalemlerle kapatıldı**, **Kapatıldı**.
- Karar: **Teyit edildi**, **Uygulanamaz (teyitli)**, **İnceleme gerekli**, **Belirsiz**. Kritik veri bilinmiyorsa kalem **İnceleme gerekli** kalır; bilinmeyen kalem asla "Uygulanamaz" sayılmaz.
- **Aday (doğrulanmamış):** kaynak henüz doğrulanmış bir resmî kaynağa bağlı değil.
- **Eskimiş — sabitleme değişti** ve **Gemi profili değişti:** karar verildikten sonra kaynak veya gemi profili değişti; kalem yeniden incelenmelidir.
- **Takipsiz açık kalemler:** takip kaydı olmayan açık kalemler. Sıfırdan büyükse kampanya kapatılamaz.
- Kategoriler **Yasal mevzuat**, **Klas kuralı**, **Şirket SMS** ve **Kılavuz — mevzuat değil** birbirinin yerine geçmez (13.03 §8).

## Yapamazsınız / dikkat
- Kampanyayı açan kişi aynı kampanyada karar veremez; inceleyici ataması olmayan kullanıcıya kararlar salt okunurdur.
- Yeterliliği doğrulanmamış inceleyici açık kalemleri takibe alabilir ama teyit edemez. Eskimiş bir kalem teyit edilemez.
- Teyit, kayıtlı kaynak veya yetkili kaynak referansı ile sürüm ister. Requirement → Control → Evidence → Verification zincirinde sahipsiz veya kaynaksız kayıt bırakmayın (13.03 §8).
- **Kapatıldı** için tüm kalemler teyit edilmiş olmalıdır; aksi halde her biri takipte olan açık kalemlerle kapatın.
- Not alanında klas veya tip onayı beyan etmeyin. Römorkör kapsamında kontrollü kaynağı olmayan hiçbir römorkaj limiti yasal varsayılan değildir.
- Kararlar yalnız insan kullanıcı tarafından kaydedilir; AI ve otomasyon hesapları karar veremez.

## Sorun olursa
- **Kayıt değişti. Devam etmeden önce yeniden yükleyip inceleyin.** **Yeniden yükle** düğmesine basın.
- **Sonuç belirsiz. Yeniden dene aynı komutu gönderir.** **Yeniden dene** düğmesini kullanın.
- **İnceleme kuralları bu komutu reddetti.** Kalemin eskimiş olup olmadığını, kaynak referansını ve takip kaydını kontrol edin.
- **Bu gemi için regülasyon incelemesine erişiminiz yok.** veya **Bu işlem için yetkiniz yok.** Rol ve kapsamınızı kontrol edin; gerekirse Tenant Admin ile görüşün (13.03 §10).
- Uygulanabilirlik beklenmedik görünüyorsa kaynak ve bağlam girdilerini kontrol edip kontrol sahibi veya SME ile görüşün (13.03 §10).

## İlgili sayfalar
