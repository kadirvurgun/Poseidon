# Teknik komutlarda kullanıcı sahipliği — 11.11.2026

Durum: **AUTHORED_UNVERIFIED**, acceptance=false; yeni teknik paket testleri
**DEFERRED / ÇALIŞTIRILMADI**. Kaynak hedef `0f2de6d5403d64bf1d79267b9ec18b6b30322cd6`, bütünleştirme kontrol
noktası `7bcb79ba9dd311e72e28a7db0b3c03e346f59245`. Bu bir yerel geliştirme alt paketidir; canonical sprint,
PMS Command Center, operasyonel hazır olma veya uzman/pilot kabulü değildir.

## Analizdeki yer ve somut davranış

Aktif `poseidon-v46.3-20261030`;18.01 Backlog53/54 DEV-052/T-S8-001 ve
DEV-053/T-S8-002; kabul edilmiş çapraz DEV-014/015/019;15.01paragraph67/71/73.
Mevcut [ekipman/bakım sözleşmesinin](../tasks/equipment-maintenance-01.md)
retry/reauthorization/veri kaybetmeme koşulları uygulanır. Tam DEV-020/052/053/
055/056 kabulü varsayılmadı. [Görev ve kabul koşulları](../tasks/TECHNICAL-COMMAND-OWNER-RECOVERY-01.md).

Ekipman, bakım planı ve iş taslakları asıl actor+tenant/gemi/kayıt bağlamında
tutulur. Oturum değişimi tespit edildiğinde önceki özel form DOM'u kaldırılır;
komut başka kullanıcıya devredilmez. Her açık gönderimden önce gerçek güncel
oturum ve gemi/modül erişimi okunur; aynı kullanıcının yeni CSRF değeri kullanılır.
Cevaptan sonra kimlik/kapsam yeniden kontrol edilir; eski asenkron sonuçlar
geçerli ekranı değiştiremez. Bunlar backend yetkilendirmesinin yerine geçmez.

İlk sonucu bilinmeyen komut sonraki401/403/erişim-okuma hatasıyla silinmez.
Asıl kullanıcı yeniden yetkilendirilince yalnız açık retry, aynı anahtar/gövde/
UUID/base ile devam eder. Job rotasının kendi UUID/gövde protokolü korunur;
olmayan bir Idempotency-Key/CommandReceipt sözleşmesi icat edilmedi. Sunucu
cevabının kaynak kimliği, revision/version ve replayed yapısı doğrulanır; bozuk
HTTP200 doğrulanmış başarı sayılmaz. Doğrulanan kimlik önce URL'ye yazılır;
sonraki okuma hatası yeni UUID oluşturmaz veya düzenlemeyi açmaz.

Bakım gözlemleri erişim kontrolü sırasında özel DOM göstermeden aynı React
örneğinde kalır; taslak korunur, askıdayken gönderim yapılmaz. İlgili aksiyonlar
ortak enterprise düğmelerini kullanır.17dilde mevcut katalog mesajları kullanıldı;
yeni İngilizce fallback, kalıcı özel browser depolaması veya backend yetkisi yok.

## Doğrulama ve sınırlar

Yeni UI/adapter test kaynakları kullanıcı/tenant değişimi, güncel CSRF, önceki
belirsiz sonuçtan sonra403, plan decimal/base, job UUID, odak/askıya alma,
gecikmiş history/receipt ve bozuk cevapları kapsar. Beş gerçek API/PG test vakası
receipt izolasyonu, yetki iptali/geri verilmesi, tek revision/business audit ve
job protokolünü sınamak üzere yazıldı. Mevcut genuine-host browser fault vakası
gerçek route.fetch commit, ağ cevabı kaybı, retry öncesi gerçek session okuması,
başarısız readback ve aynı URL üzerinden açık kaynak okumasını kapsayacak şekilde
genişletildi. **Bu yeni testlerin hiçbiri çalıştırılmadı.**

Bağımsız kaynak incelemesinde kapsam içi açık bulgu0; önceden mevcut gözlem sorunu1açık. İnceleme ayrı exact-target kayıtla saklanır; statik inceleme
test/derleme geçişi, backend izolasyon kanıtı veya final kabul değildir. Önceki
yerelleştirme `23b87d4` hedefinde lint/typecheck/1437/1437component/üretim
build geçti; o sonuç yeni teknik kaynaklara taşınmadı. Önceki başarısız kanıtlar
korunur; yalnız normal yerel dal birleşimi yapıldı, yeni PR/push/uzak PR merge yok.

TOCR-S01, `76f765a` gözlem panelinde başlangıçta suspended=true olmasına rağmen
passive effect'in veri okumasını başlattığını buldu. `0f2de6d` istek öncesi askı
kontrolü ve cevap korumasını ekledi; ilk askı→resume için yeni test kaynağı yazıldı.
Bulgu geçmişi korunur. Dört ürün alt ağacının kaynak/yerel birleşim Git tree
kimlikleri eşleşir; bu yalnız kaynak karşılaştırmasıdır, yürütme kanıtı değildir.

## Açık işler ve sonraki adım

Gözlem bileşeninin kendi komut protokolünde eski unknown sonrası denial/reset ve
CSRF'ye bağlı reset davranışı ayrı kurtarma işi olarak açıktır. Bu alt paketin
askıya alma düzeltmesi tam gözlem komut kurtarması değildir. Sonraki geliştirme
bu sahipli gözlem akışını tamamlamak; yeni teknik paket yeterince büyüdüğünde
toplu gerçek kontroller, bağımsız final-target QA ve requiredCI çalıştırmaktır.
Yerelleştirme gerçek-browser/bütünleşik kapıları ve PR16 tam genuine signed
kapısı ayrı açıktır. Resmî kabul **20/121 iş,103/970SP=%10,62;0/196ayrıekran**
olarak değişmedi.

AST Graphify güncellemesi exit0:19.247düğüm,39.678kenar.783sıfır-düğümlü kaynak graf dışında kaldı; HTML büyük grafik sınırı nedeniyle üretilmedi. Türetilmiş grafik bakım kaydı ürün testi veya gereksinim kabulü değildir.
