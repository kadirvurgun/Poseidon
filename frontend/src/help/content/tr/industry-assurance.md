---
key: industry-assurance
title: Endüstri güvencesi (SIRE 2.0)
roles: [DPA / HSEQ, Regulatory / Assurance, Master]
permissions: [assurance.inspection.read, assurance.inspection.import, assurance.import.accept, assurance.evidence.write, assurance.finding.link, assurance.mapping.write, file.read, hse.safety_event.read]
related: [safety-events, audit-ready, regulatory-review]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Endüstri güvencesi sayfası, bir tanker için SIRE 2.0 denetim sonuçlarını çalışma kitabından içe aktarır, değişiklikleri insan incelemesine sunar, bulguları emniyet aksiyonlarına bağlar ve saha kanıtını kaydeder. Cevapladığı soru şudur: "Bu denetimin hangi bulguları açık, hangisine aksiyon bağlandı ve kanıtı nerede?" İçe aktarmalar taslaktır: değişen her kalemi başka bir kişi kabul eder. Bulgular açık ve doğrulanmamış kalır; bu sayfa bulguyu kapatmaz veya doğrulamaz. Zincir Bulgu → Aksiyon → Doğrulama şeklindedir ve doğrulama ile etkinlik Olaylar ve ramak kala sayfasındaki aksiyonda ayrı insan kayıtlarıdır (13.03 §4, §8).

## Kim kullanır
- **DPA / HSEQ:** denetim bulgularını aksiyona bağlamak ve Bulgu → Aksiyon → Doğrulama zincirini izlemek için (13.03 §4).
- **Regulatory / Assurance kullanıcısı:** sütun eşlemelerini yönetmek, içe aktarmayı hazırlamak veya incelemek için (13.03 §8).
- **Master ve gemi personeli:** bulguya ait saha kanıtını (fotoğraf) kaydetmek için.

## Başlamadan önce
- Bir gemi seçin. SIRE 2.0 yalnız tankerlere uygulanır. Gemi profilinde gemi tipi beyan edilmemişse içe aktarmalar **inceleme gerekli** kalır.
- Okumak için denetim okuma yetkisi gerekir. İçe aktarma, içe aktarmayı kabul, kanıt yazma, bulgu bağlama ve eşleme yazma ayrı yetkilerdir. Aksiyon bağlamak için emniyet olaylarını okuma yetkisi, kanıt kaydetmek için dosya okuma yetkisi de gerekir.
- Sütun şeması sentetiktir; lisanslı OCIMF biçimi kullanılmaz.
- Çevrimdışıyken yalnız fotoğraf kayıtları bu cihazda kuyruğa alınır; içe aktarma ve kararlar bağlantı gerektirir.

## Adım adım
1. **Gemi seçin** ve **Uygulanabilirlik** satırını kontrol edin.
2. Gerekirse **Sütun eşlemeleri** bölümünde **Eşleme anahtarı** girip her sütun için bir satır (başlık=alan) yazın ve **Yeni sürüm olarak kaydet** düğmesine basın.
3. **İçe aktarmayı hazırla** bölümünde **Eşleme sürümü**, **Denetim referansı**, **Denetim tarihi** ve **Çalışma kitabı (.xlsx)** seçip **İncelemeye gönder** düğmesine basın.
4. Başka bir kişi **İçe aktarma incelemesi** bölümünde her değişen kalem için **Kabul et** veya **Mevcudu koru** seçer, en az 10 karakterlik **Gerekçe** yazar ve **İçe aktarmayı kabul et** ya da **İçe aktarmayı reddet** düğmesine basar.
5. **Denetimler** listesinden bir denetimi açın. **Kalemler** ve **Bulgular** bölümlerini inceleyin.
6. Bir bulguyu aksiyona bağlamak için **Emniyet aksiyonu kimliği** girip **Aksiyonu bağla** düğmesine basın.
7. Saha kanıtı için **Medya**, **Temiz dosya kimliği**, **Dosya revizyonu** ve **Dosya özeti (SHA-256)** girip **Saha kanıtı kaydet** düğmesine basın. Çevrimdışıyken düğme **Bu cihaza kaydet** olur; bağlantı gelince **Şimdi gönder** ile gönderin.

## Durumlar ne anlama gelir
- **Tanker: uygulanabilir / Uygulanamaz / Gemi tipi bilinmiyor: inceleme gerekli:** gemi tipi bilinmiyorsa sonuç **İnceleme gerekli** kalır; uygulanamaz olarak varsayılmaz.
- **İnceleme bekliyor / Kabul edildi / Reddedildi:** içe aktarmanın durumu.
- **Yeni / Değişti / Değişmedi / Bu içe aktarmada yok:** kalemin önceki kabul edilen içe aktarmaya göre durumu. Bu içe aktarmada olmayan kalem **Korunur (asla silinmez)**.
- **Olumsuz gözlem / Olumlu gözlem / Gözlem yok / Bilinmiyor:** kalemdeki gözlem.
- **Açık / Doğrulanmamış:** bulgular burada her zaman açık ve doğrulanmamış görünür.
- **Bağlı aksiyon yok / Aksiyon bağlı:** bulgunun bir emniyet aksiyonuna bağlı olup olmadığı. Aksiyon bağlı olması, bulgunun doğrulandığı veya aksiyonun etkili olduğu anlamına gelmez.
- **Çevrimiçi kaydedildi / Çevrimdışı kaydedildi:** kanıtın nasıl kaydedildiği.

## Yapamazsınız / dikkat
- İçe aktarmayı hazırlayan kişi aynı içe aktarmayı inceleyemez: "Bu içe aktarmayı siz hazırladınız; başka biri incelemeli."
- Bu sayfa bulgu kapatmaz ve doğrulamaz. Doğrulandı, etkili demek değildir; etkinlik aksiyon üzerinde ayrı bir insan değerlendirmesidir.
- Video kanıtı, operatör taranmış video dosya türünü etkinleştirene kadar kullanılamaz.
- Bir kalem sonraki içe aktarmada yoksa silinmez; geçmiş korunur.

## Sorun olursa
- **İstek reddedildi:** altında listelenen nedenleri giderin.
- **Kayıt bu arada değişti. Yenileyip yeniden inceleyin.** **Yenile** düğmesine basın.
- **Sonuç bilinmiyor (bağlantı kesildi).** **Aynı isteği yeniden dene** düğmesini kullanın.
- **Endüstri güvencesi kayıtlarını okuma yetkiniz yok.** veya **Bu işlemi yapma yetkiniz yok.** Rol ve gemi bağlamınızı kontrol edin; gerekiyorsa kiracı yöneticisine başvurun (13.03 §10).
- Kanıt doğrulanamıyorsa dosya kimliğini, revizyonunu ve SHA-256 özetini kontrol edin; gerekiyorsa doğrulayıcıya başvurun (13.03 §10).

## İlgili sayfalar
