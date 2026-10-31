# Sürekli ajan geliştirme sistemi

## Kullanıcı kararları

- Hedef: https://github.com/kadirvurgun/Poseidon
- Ajanlar geliştirme için tam yetkili; push ve PR açma açıkça yetkilendirildi.
- Tam geliştirme yetkisi kapsamında, zorunlu testler ve bağımsız inceleme geçtikten sonra normal PR merge hedeflenir. Force push, depo silme ve canlı müşteri verisinde yıkıcı işlemler bu çalışma döngüsünün parçası değildir.
- Ayrı Esra Pro ve Kadir Plus profillerinin kimlik doğrulaması tamamlandı; her hesap yalnız kendi kotasıyla değerlendirilir. Claude zorunlu bağımlılık değildir.
- 7/24 host seçimi, kullanım bütçesi, pilot kapasitesi, veri lokasyonu ve IdP henüz yanıtlanmadı.
- Kullanıcı her faz bitiminde bilgi verilmesini istedi: teslim edilenler, test sonuçları, engeller, mevcut aşama ve sonraki adım raporlanır. Yerel alt paket ile tam sprint kabulü ayrı belirtilir.

## Hedef akış

### Etkin teslim biçimi — 01.11.2026 kullanıcı kararı

Küçük görev başına ayrı PR yerine, kullanılabilir ve bütünlüklü ürün paketleri
hazırlanır. Backend, UI, migration ve anlamlı test dosyaları birlikte yazılır;
paket tamamlanana kadar test/build ve otomatik push/PR başlatılmaz. Yerel commit
kontrol noktasıdır, doğrulama veya kabul değildir. Paket bitince entegre kapı,
bağımsız hedef-commit QA ve gereken push/PR CI uygulanır; ancak sonra merge edilir.
Bu tercih testleri atlamaz veya merge kapısını kaldırmaz.

DIR-WORKSPACE-01 sırasında küçük görevleri tek tek yayımlayan geliştirme/publish
sürücüsü durduruldu; hazırlanmış S0 tehdit-modeli taslağı aynı ürün dalına korundu.
Saatlik kota/metadata izleme ayrı kalır. Geliştirme Esra profilinde yürütülür;
Kadir yalnız doğrulanmış Esra kota sınırı ve kendi uygun kotasıyla yedektir.
Yeni küçük PR açan döngü, bu teslim tercihine uyarlanmadan yeniden etkinleştirilmez.

Koordinatör → hazır görev → role özel worktree → geliştirici ve testleri → bağımsız QA → commit/push → PR → tam gereken CI → merge → sonraki görev.

Koordinatör kalıcı görev kuyruğu ve lease tutar. Her görev requirement ID, kaynak sürümü/bölümü, kabul kriterleri, bağımlılıklar, rol, branch, doğrulama komutları ve faz taşır. Geliştirici kendi görevini tek başına kabul edemez. UI geliştirici contract üzerinden çalışır; backend geliştirici API değişikliğini ve consumer etkisini birlikte ele alır.

Kalıcı durumlar: queued, running, verifying, awaiting-ci, done, retryable, blocked. İşlem çökünce lease süresi dolan görev yeniden değerlendirilir; remote branch/commit/PR kontrol edilmeden aynı dış işlem tekrarlanmaz. Aynı dal ve dosyalar eşzamanlı ajanlara verilmez. Testler hedef commit üzerinde geçmeden done üretilmez.

Başlangıç hata politikası önerisi: görev başına en fazla 3 düzeltme denemesi, çalıştırma başına süre sınırı, rate limitte bekleme, sürekli aynı hatada blocked. Günlük kota veya bütçe dolduğunda yeni model işi başlatılmaz. Yetki olması sınırsız maliyet veya sınırsız tekrar anlamına gelmez.

GitHub Actions CI'ı çalıştırır; sürekli koordinatör ayrı bir makinede servis olarak çalışır. Windows uykuya geçerse veya makine kapanırsa yerel geliştirme durur. GitHub push tetikleri ile PR tetikleri concurrency grubu ile birleştirilir; gereken test kapsamı korunur. Otomasyon hesabının push ettiği commitlerin CI tetiklediği gerçek bir denemeyle doğrulanmalıdır.

## Codex ve Claude

Codex CLI `codex exec --json` ile etkileşimsiz görevler çalıştırabilir ve kaydedilmiş oturumu kullanabilir. Üyelik kotasıyla sürekli ve sınırsız çalışma garanti edilmez. İki profil ayrı kimlik doğrulaması ve kendi kota pencereleriyle kullanılır; kota aşmaya yönelik sınırsız rotasyon tasarlanmaz. [Saatlik kontrol paketi](hourly-automation.md) varsayılan olarak yalnız metadata okur; model işi açıkça verilen güvenilir görev gerektirir.

Claude gelecekte ayrı bir worker adapter olabilir. Satın alma veya hesap paylaşımı kurulumun ön koşulu değildir. Kullanılacaksa yetkili kişisel/ekip erişimi, headless çalışma ve limitler kendi resmi dokümanlarından doğrulanır.

Resmi kaynaklar:

- https://learn.chatgpt.com/docs/non-interactive-mode
- https://developers.openai.com/codex/auth/

## Bu hazırlıkta doğrulanan durum

- Yerelde .NET 10 SDK, Node, Python 3.13, Claude CLI ve Graphify 0.9.15 bulundu. Docker daemon ve PostgreSQL container çalışması doğrulandı; Claude login doğrulanmadı.
- Codex CLI proje içi `.tools/codex/` altında kurulur; kimlik bilgileri repoya yazılmaz.
- Önceki 403 kurulum engeli güncel durum değildir: doğrulanmış PR #1 ve PR #3 merge edildi. Worker temeli ve kota watcher PR #5 ile QA/CI sonrası merge edildi; draft PR #2 superseded olarak kapatıldı.
- Graphify Codex skill'i ve proje keşif talimatı kuruldu. Ürünün RAG servisi kurulmuş değildir.
- Saatlik metadata görevi `Poseidon-HourlyQuota` gerçekten kaydedildi ve exit 0 ile çalıştı; son gözlenen koşu 31.10.2026 11:10:23 +03. Bu görev model işi yapmaz. [Gece koordinatörü](nightly-automation.md) ayrı geliştirme paketidir; gerçek aktivasyon ve uçtan uca deneme kanıtı [durum raporunda](project-status.md) izlenir.

## Devreye alma kabulü

1. GitHub hesabıyla fetch, görev dalına push, PR açma ve CI sonuçlarını okuma doğrulanır.
2. Host ve kota politikası kesinleşir; Codex login kendi hesabıyla tamamlanır.
3. Uygulama iskeleti ve gerçek test komutları kurulur; eksik testte başarısız olan CI yayınlanır.
4. Tek küçük görev, bağımsız QA ve push→CI→merge döngüsünden geçirilir.
5. Kalıcı koordinatör ve yeniden başlatma/lease testi tamamlanır; ancak sonra sürekli servis etkinleştirilir.
