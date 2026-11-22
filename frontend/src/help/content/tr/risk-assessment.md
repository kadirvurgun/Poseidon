---
key: risk-assessment
title: Risk değerlendirmesi
roles: [Master, Tug Master, DPA / HSEQ, Crew / OOW / Engineer]
permissions: [hse.risk.read, hse.risk.write, hse.risk.approve, hse.risk.acknowledge]
related: [risk-template-library, high-risk-work, safety-events]
screenshots: []
reviewed: false
---
## Bu sayfa ne işe yarar
Risk değerlendirmesi sayfası, bir gemideki iş veya operasyon için tehlikeleri, olası sonuçları, kontrol önlemlerini, başlangıç ve kalıntı riski ve işi durdurma koşullarını kayıt altına alır. Cevapladığı soru şudur: "Bu iş için tehlikeler ve kontroller düşünüldü mü, incelendi mi ve katılımcılar teyit etti mi?" Sayfa **yalnız geliştirme hedefidir**: buradaki onay kayıtlı bir insan incelemesidir; çalışma izni, uzman risk kabulü veya HSE kapısı geçişi değildir (13.03 §2, §4).

## Kim kullanır
- **Master ve Tug Master:** gemideki veya römorkördeki iş için değerlendirmeyi hazırlatmak ve işi durdurma kararını vermek için. Karar yetkisi onlardadır (13.03 §2, §3).
- **DPA / HSEQ:** risk yönetişimini izlemek ve inceleme kararını kaydetmek için (13.03 §4).
- **Mürettebat / OOW / Mühendis:** katılımcı olarak onaylanan değerlendirmeyi kendi adına teyit etmek veya itiraz etmek için (13.03 §6).

## Başlamadan önce
- Risk değerlendirmelerini açmak için bir gemi seçin. Okumak için risk okuma, taslak yazmak için risk yazma yetkisi ve o gemide düzenleme kapsamı gerekir. İnceleme kararı için risk onay, teyit için risk teyit yetkisi gerekir.
- Derecelendirme için kiracıda yayımlanmış bir risk yöntemi (matris) olmalıdır. Yoksa derecelendirmeler **Değerlendirilmedi** kalır. Matrisi kiracı HSEQ sahibi yapılandırır, başka bir kişi yayımlar (Risk şablon kütüphanesi sayfası).
- Risk komutları güncel çevrimiçi bağlantı ister; çevrimdışıyken hiçbir şey kuyruğa alınmaz veya onaylanmaz.

## Adım adım
1. Gemiyi seçin ve listeden bir değerlendirmeyi açın ya da **Yeni değerlendirme** düğmesine basın.
2. **Değerlendirme numarası** girin. İsterseniz **İş türü** girip **Yayımlanmış şablondan başla (yalnız kopya)** ile uygun bir şablonu seçin; şablon yeni taslağa yalnız kopyalanır.
3. **Başlık**, **Operasyon / iş bağlamı**, **İş türü**, **İş kapsamı** ve **Risk yöntemi** alanlarını doldurun. **İşi durdurma gerekli** için **Evet**, **Hayır** veya **Bilinmiyor** seçin ve gerekiyorsa **İşi durdurma koşulları** yazın.
4. **Tehlike ekle** ile her tehlike için **İş adımı**, **Olası sonuçlar**, **Başlangıç riski** ve **Kalıntı riski** (Olasılık, Şiddet, Gerekçe) girin. **Kontrol ekle** ile **Kontrol önlemleri**, **Kontrol sorumlusu** ve **Bariyer referansları** ekleyin.
5. **Katılımcılar** alanına satır başına aktör kimliği ve rol yazın. Gerekirse **Bağlantı ekle** ile ilgili kayıtları bağlayın.
6. **Taslağı kaydet**, ardından **İncelemeye gönder** düğmesine basın. **Engelleyen maddeler** listesi eksikleri gösterir.
7. İnceleyici, en az 10 karakterlik **Gerekçe** yazıp **Onayla (insan inceleme kaydı)** veya **İade et** düğmesine basar.
8. Onaydan sonra her katılımcı kendi adına **Teyit et** veya gerekçeyle **İtiraz et** düğmesine basar. Değişiklik gerekirse **Revize et (yeni taslak açar)** kullanılır.

## Durumlar ne anlama gelir
- **Taslak / İncelemede / İade edildi / Onaylandı (insan kaydı):** değerlendirmenin yaşam döngüsü. Onaylandı bir insan inceleme kaydıdır; çalışma izni değildir.
- **Değerlendirilmedi / Yöntem yapılandırılmadı:** risk derecesi yok. Sistem varsayılan bir matris, çarpım veya düşük/orta/yüksek eşiği kullanmaz.
- **İşi durdurma gerekli — Bilinmiyor:** henüz karar verilmedi; "Hayır" olarak okunmaz.
- **Onaydan önce teyit uygulanmaz / Teyitler bekleniyor / Tüm katılımcılar teyit etti / İtiraz var – çözülmedi:** katılımcı teyit durumu. Bir itiraz, başka birinin teyidiyle kapanmaz.
- **Sabitlendikten sonra değişti / Kullanılamıyor:** bağlı kayıt sabitlendiği revizyondan sonra değişti veya artık erişilemiyor; yeniden inceleyin.

## Yapamazsınız / dikkat
- İnceleme döngüsünün yazarı veya gönderen kişi aynı değerlendirmeyi onaylayamaz (görev ayrılığı).
- Herkes yalnız kendi adına ve tam onaylanan revizyon için teyit verir.
- Buradaki onay çalışma izni, uzman risk kabulü veya HSE kapısı değildir. İşe başlama ve işi durdurma kararı Master veya Tug Master'dadır.
- Şablondan başlatılan taslağa derecelendirme, imza, katılımcı teyidi veya onay taşınmaz.
- Çevrimdışıyken değerlendirme gönderilemez, onaylanamaz veya teyit edilemez.

## Sorun olursa
- **Listelenen maddeler tamamlanana kadar engelli.** Engelleyen maddeleri tamamlayıp tekrar gönderin.
- **Bu kayıt başka yerde değişti.** **Yeniden yükle** düğmesiyle güncel revizyonu görün, sonra işlemi tekrarlayın.
- **Bu işlemi yapma yetkiniz yok (görev ayrılığı veya kapsam).** Başka bir yetkili kişiden inceleme isteyin veya rol ve gemi bağlamınızı kontrol edin; gerekiyorsa kiracı yöneticisine başvurun (13.03 §10).
- **Risk çalışma alanı mevcut erişiminizle kullanılamıyor.** Yetkinizi kontrol edin.
- Derecelendirme beklenmedik görünüyorsa kullanılan risk yöntemini ve revizyonunu kontrol edin; kontrol sorumlusuna veya HSEQ sahibine danışın (13.03 §10).

## İlgili sayfalar
