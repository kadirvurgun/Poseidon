---
key: voice-event-draft
title: Sesli olay taslağı
roles: [Crew / OOW / Engineer, Master, DPA / HSEQ]
permissions: [voice.draft.read, voice.draft.write, hse.safety_event.report]
related: [safety-events]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Sesli olay taslağı sayfası, kısa bir sesli veya yazılı notu emniyet olayı bildirimi için **taslak önerilerine** çevirir. Cevapladığı soru şudur: "Söylediğim veya yazdığım nottan olay türü, yer, zaman, ne olduğu ve ilk önlem doğru çıkarıldı mı?" Öneriler sabit yerel kurallardan gelir; her biri sizin sözlerinize bir güven değeriyle bağlıdır. Taslağı bir insan düzenler ve onaylar; hiçbir şey otomatik gönderilmez, kapatılmaz veya raporlanmaz (13.03 §1, §6).

## Kim kullanır
- **Mürettebat / OOW / Mühendis ve Master:** sahada hızlıca not alıp olay bildirimini başlatmak için (13.03 §2, §6).
- **DPA / HSEQ:** **Ölçülen kullanılabilirlik (bu gemi)** değerlerini izlemek için. Bu değerler operasyonel gözlemdir, doğruluk iddiası değildir.

## Başlamadan önce
- Yetkili olduğunuz bir gemi seçin. Taslak oluşturmak için sesli taslak yazma yetkisi ve o gemide düzenleme kapsamı gerekir.
- Onaylanan taslağın emniyet olayı taslağına aktarılması için ayrıca emniyet olayı bildirme yetkisi gerekir.
- Gizlilik: ses kaydı saklanmaz. Konuşma yalnızca bu kurulumdaki yerel motorla işlenir; yerel motor yoksa notu yazın veya yapıştırın. İsim ve kişisel sağlık bilgisi kullanmayın.
- Çevrim dışıyken hiçbir komut gönderilmez.

## Adım adım
1. **Yeni sesli veya yazılı not** düğmesine basın.
2. Yerel motor yapılandırılmışsa **Yerel motorla kaydet**, bitince **Durdur ve yazıya dök** düğmesine basın. Aksi halde **Not metni** alanına kısa notunuzu yazın: ne, nerede, ne zaman, ne yaptınız.
3. **Notun tarihi** alanını kontrol edin. Bu tarih yalnızca "bugün", "dün" gibi ifadeleri çözmek için kullanılır.
4. **Taslak önerileri oluştur** düğmesine basın.
5. Her alanı kontrol edin: **Olay türü**, **Ne zaman oldu (yerel saat)**, **Gemideki yer**, **Yer ayrıntısı**, **Ne oldu**, **Alınan ilk önlem**. Her alanın altında önerinin güven değeri, kuralı ve notunuzdaki ilgili parça görünür. **Kural notları** listesini de okuyun.
6. Yanlış önerileri düzeltip **Düzenlemeleri kaydet** düğmesine basın. **Onaydan önce hâlâ gerekli:** listesi boşalınca **Taslağı onayla** düğmesine basın. Onay, her alanı kontrol ettiğinizi kaydeder; olay göndermez veya kapatmaz.
7. Onaydan sonra emniyet olayı taslağı oluşturulduysa **Emniyet olayı taslağını aç** bağlantısıyla Olaylar ve ramak kala sayfasına geçip taslağı orada tamamlayıp gönderin. Taslak henüz oluşturulmadıysa ve yetkiniz varsa **Emniyet olayı taslağını şimdi oluştur** düğmesini kullanın.
8. Not bir olay değilse veya hatalıysa **Gerekçe** seçip gerekirse **Not (Diğer için zorunlu)** yazarak **Taslağı reddet** düğmesine basın.

## Durumlar ne anlama gelir
- **Taslak (onaysız):** öneriler henüz bir kişi tarafından onaylanmadı.
- **Bir kişi onayladı:** alanlar kontrol edildi; bu bir emniyet olayı bildirimi değildir, hiçbir şey gönderilmedi.
- **Reddedildi:** ret gerekçesi değerlendirme için kaydedildi.
- **Önerildi, değişmedi / Sizin değiştirdiğiniz / Sizin girdiğiniz / Boş:** alanın kaynağı. Önerildi, değişmedi bir kişinin kontrol etmesi gereken öneridir.
- **Yazıldı veya yapıştırıldı / Yerel konuşma motoru:** notun nasıl girildiği. Yerel motordan gelen metni düzenlerseniz yazılı not olur.
- **Gün olmadan saat belirtildi; tarih varsayılmadı** gibi kural notları eksik bilgiyi gösterir; sistem eksik değeri tahmin etmez.
- **2 eksik** gibi bir not: listede taslağın kaç zorunlu alanının boş olduğunu gösterir.

## Yapamazsınız / dikkat
- Öneriler yalnız taslaktır; güven değeri doğruluk garantisi değildir. Onay vermeden önce her alanı kendiniz kontrol edin.
- Bu sayfa emniyet olayı göndermez, kapatmaz veya raporlamaz. Bildirimi Olaylar ve ramak kala sayfasında siz gönderirsiniz.
- Kanıttan söz edildiyse kanıtı kendiniz ekleyin; sayfa kanıt eklemez.
- Kaydedilmemiş düzenleme varken **Taslağı onayla** kullanılamaz; önce düzenlemeleri kaydedin.
- Emniyet olayı bildirme yetkiniz yoksa onaylanan taslaktan emniyet olayı taslağı oluşturulmaz.

## Sorun olursa
- **Konuşma yeteneği yok: yerel motor yapılandırılmamış.** veya **Yazıya dökme tamamlanmadı.** Notu yazın veya yapıştırın.
- **Mikrofon erişimine izin verilmedi.** Tarayıcı izinlerini kontrol edin ya da notu yazın.
- **Kayıt bu arada değişti.** **Yeniden yükle** düğmesine basıp düzenlemenizi tekrar yapın.
- **Çevrim dışısınız. Hiçbir şey gönderilmedi.** Bağlantı gelince **Aynı komutu yeniden dene** düğmesini kullanın.
- **Bu işlem için yetkiniz yok.** Rol ve gemi bağlamınızı kontrol edin; gerekiyorsa kiracı yöneticisine başvurun (13.03 §10).

## İlgili sayfalar
