---
key: audit-ready
title: Denetime hazır iz
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [audit.read, human.trace.read]
related: [requirements, sources, audit]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Denetime hazır iz sayfası, bir gemi için size atanmış iz zincirlerini aşama aşama gösterir: Gereksinim → kontrol → kanıt → doğrulama → karar; durumlar, zaman damgaları ve beyan edilen kaynak konumlarıyla. Cevapladığı soru şudur: "Bu kanıt hangi gereksinime dayanıyor, kim doğruladı ve hangi karar kaydedildi?" Kaydedilmiş zincir bir insan kaydıdır; onay veya hazırlık beyanı değildir (13.03 §4, §8).

## Kim kullanır
- **Mevzuat / güvence kullanıcısı (Regulatory / Assurance):** Gereksinim→Kontrol→Kanıt→Doğrulama zincirinde yetim kayıt kalmadığını denetler (13.03 §8).
- **DPA / HSEQ:** denetim, PSC ve inceleme hazırlığında Bulgu→Aksiyon→Doğrulama izini kanıtıyla gösterir (13.03 §4).

## Başlamadan önce
- Sayfa için denetim okuma ve insan iz okuma yetkisi, ayrıca o gemi için geçerli bir iz okuyucu ataması gerekir. Yetki tek başına yetmez; atamanız yoksa zincir listelenmez.
- Doğru gemiyi seçin. Liste yalnız size atanmış zincirleri gösterir.
- Sayfa canlı bağlantıyla yüklenir. Çevrimdışıyken gösterilen veri eski olabilir.

## Adım adım
1. **Gemi** listesinden bir gemi seçin (**Gemi seçin**).
2. **Atanmış zincirler** listesinden bir zinciri açın.
3. **İz aşamaları** bölümünde her aşamayı (Gereksinim, Kontrol, Kanıt, Doğrulama, Karar) açarak **Durum**, **Kayıt zamanı**, **Revizyon**, **İçerik özeti** ve **Kaynak konumu (beyan, doğrulanmamış)** bilgilerini inceleyin.
4. **Açık maddeler** varsa listelenen kodları ilgili kayıt sahibine iletin.
5. Okunabilir atanmış zincir varsa **Denetim paketi** bölümünde **Yeni paket derle** ile paket derleyin, **Derlenmiş paketler** listesinden açın ve gerekirse **JSON dışa aktar** düğmesini kullanın.
6. Veriyi tazelemek için başlıktaki **Yeniden dene** düğmesine basın.

## Durumlar ne anlama gelir
- **Kaydedilmiş zincir — yalnızca insan kaydı, onay değildir:** tüm aşamalar kayıtlı. Bu, gemiyi veya işlemi hazır ya da uygun ilan etmez.
- **İnceleme gerekli:** en az bir aşama eksik, bilinmiyor veya kullanılamıyor. Bilinmeyen veya erişilemeyen girdi hiçbir zaman tamam gösterilmez.
- **Kaydedilmedi:** o aşama için kayıt yok; örneğin doğrulama veya karar henüz verilmedi.
- **Kullanılamıyor / UNKNOWN:** kayıt şu an okunamıyor; içerik varsayılmaz.
- **Eski: bir girdi yenilendi veya süresi doldu; inceleme gerekli.** Kanıt veya kaynak sonradan değişti; zinciri yeniden inceleyin.
- **Konum belirtilmemiş:** kaynak konumu girilmemiş; konum beyandır, doğrulanmış değildir.
- Doğrulanmış bir kanıt, aksiyonun etkili olduğu anlamına gelmez: Doğrulandı ≠ Etkili (13.03 §4).
- **Denetim paketi:** "Sistem derlemesi; resmî kayıt değildir." Atamalarınız dışındaki kalemler yalnız sayılır, gösterilmez; **Yerini aldı (iz için saklanır)** kalemler güncel sayılmaz.

## Yapamazsınız / dikkat
- Bu sayfadan kayıt düzenleyemez, doğrulama veya karar veremezsiniz; yalnız okursunuz. Doğrulama ve karar ilgili sayfalarda atanmış insan tarafından kaydedilir.
- Size atanmamış zincirleri göremezsiniz; gizli zincirlerin adı gösterilmez, yalnız kullanılamayan atanmış zincirler sayıyla belirtilir.
- Bütünlük ve bağlam özetleri yetki veya onay değildir.

## Sorun olursa
- **Bu görünüm için yetkiniz yok.** Rol, gemi ve atama bağlamınızı kontrol edin; Tenant Admin'e başvurun (13.03 §10).
- **Bu gemi için size atanmış iz zinciri yok.** Doğru gemiyi seçtiğinizi kontrol edin; atama gerekiyorsa yetkili kişiye başvurun.
- **İz yüklenemedi.** **Yeniden dene** düğmesine basın.
- **Zincir bulunamadı veya artık size görünür değil.** Atamanız sona ermiş veya kayıt kapsam dışına çıkmış olabilir.
- Kayıt doğrulanamıyorsa kanıt ve kökenini kontrol edin, gerekirse doğrulayıcıya başvurun (13.03 §10).

## İlgili sayfalar
