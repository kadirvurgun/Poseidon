# Sürekli ajan geliştirme sistemi

## Kullanıcı kararları

- Hedef: https://github.com/kadirvurgun/Poseidon
- Ajanlar geliştirme için tam yetkili; push ve PR açma açıkça yetkilendirildi.
- Tam geliştirme yetkisi kapsamında, zorunlu testler ve bağımsız inceleme geçtikten sonra normal PR merge hedeflenir. Force push, depo silme ve canlı müşteri verisinde yıkıcı işlemler bu çalışma döngüsünün parçası değildir.
- Mevcut üyelik: ChatGPT Plus/Codex. Claude erişimi henüz sağlanmadı; Claude zorunlu bağımlılık değildir.
- 7/24 host seçimi, kullanım bütçesi, pilot kapasitesi, veri lokasyonu ve IdP henüz yanıtlanmadı.
- Kullanıcı her faz bitiminde bilgi verilmesini istedi: teslim edilenler, test sonuçları, engeller, mevcut aşama ve sonraki adım raporlanır. Yerel alt paket ile tam sprint kabulü ayrı belirtilir.

## Hedef akış

Koordinatör → hazır görev → role özel worktree → geliştirici ve testleri → bağımsız QA → commit/push → PR → tam gereken CI → merge → sonraki görev.

Koordinatör kalıcı görev kuyruğu ve lease tutar. Her görev requirement ID, kaynak sürümü/bölümü, kabul kriterleri, bağımlılıklar, rol, branch, doğrulama komutları ve faz taşır. Geliştirici kendi görevini tek başına kabul edemez. UI geliştirici contract üzerinden çalışır; backend geliştirici API değişikliğini ve consumer etkisini birlikte ele alır.

Kalıcı durumlar: queued, running, verifying, awaiting-ci, done, retryable, blocked. İşlem çökünce lease süresi dolan görev yeniden değerlendirilir; remote branch/commit/PR kontrol edilmeden aynı dış işlem tekrarlanmaz. Aynı dal ve dosyalar eşzamanlı ajanlara verilmez. Testler hedef commit üzerinde geçmeden done üretilmez.

Başlangıç hata politikası önerisi: görev başına en fazla 3 düzeltme denemesi, çalıştırma başına süre sınırı, rate limitte bekleme, sürekli aynı hatada blocked. Günlük kota veya bütçe dolduğunda yeni model işi başlatılmaz. Yetki olması sınırsız maliyet veya sınırsız tekrar anlamına gelmez.

GitHub Actions CI'ı çalıştırır; sürekli koordinatör ayrı bir makinede servis olarak çalışır. Windows uykuya geçerse veya makine kapanırsa yerel geliştirme durur. GitHub push tetikleri ile PR tetikleri concurrency grubu ile birleştirilir; gereken test kapsamı korunur. Otomasyon hesabının push ettiği commitlerin CI tetiklediği gerçek bir denemeyle doğrulanmalıdır.

## Codex ve Claude

Codex CLI `codex exec --json` ile scriptsiz etkileşim gerektirmeyen görevler çalıştırabilir ve kaydedilmiş oturumu kullanabilir. Plus üyeliği genel OpenAI API bakiyesi değildir. API kullanımı ayrı faturalandırılır. Üyelik kotasıyla sürekli ve sınırsız çalışma garanti edilmez; kendi hesabıyla CLI login ve küçük bir deneme gerekir. Hesaplar arasında kota aşmaya yönelik rotasyon tasarlanmaz.

Claude gelecekte ayrı bir worker adapter olabilir. Satın alma veya hesap paylaşımı kurulumun ön koşulu değildir. Kullanılacaksa yetkili kişisel/ekip erişimi, headless çalışma ve limitler kendi resmi dokümanlarından doğrulanır.

Resmi kaynaklar:

- https://learn.chatgpt.com/docs/non-interactive-mode
- https://developers.openai.com/codex/auth/

## Bu hazırlıkta doğrulanan durum

- Yerelde .NET 10 SDK, Node, Python 3.13, Claude CLI ve Graphify 0.9.15 bulundu. Docker daemon ve PostgreSQL container çalışması doğrulandı; Claude login doğrulanmadı.
- Codex CLI proje içi `.tools/codex/` altında kurulur; kimlik bilgileri repoya yazılmaz.
- Git origin tanımlandı; origin/main fetch geçti. Push dry-run mevcut avurgun1 hesabı için 403 aldı; Write yetkisi/doğru Git oturumu ve PR için GitHub CLI login bekleniyor.
- Graphify Codex skill'i ve proje keşif talimatı kuruldu. Ürünün RAG servisi kurulmuş değildir.
- Çalışan sürekli worker/scheduler yoktur. İlk backend workflow'u yerelde oluşturuldu: 12 unit/guard + 10 gerçek PostgreSQL integration testi geçti; 4 belge aktarım testi de geçti. Bağımsız statik QA foundation kapsamında bloklayıcı bulgu bildirmedi. Remote CI doğrulanmadı; HTTP/React/Playwright testleri yoktur. Sadece dosyaların varlığı otomasyonun aktif olduğu anlamına gelmez.

## Devreye alma kabulü

1. GitHub hesabıyla fetch, görev dalına push, PR açma ve CI sonuçlarını okuma doğrulanır.
2. Host ve kota politikası kesinleşir; Codex login kendi hesabıyla tamamlanır.
3. Uygulama iskeleti ve gerçek test komutları kurulur; eksik testte başarısız olan CI yayınlanır.
4. Tek küçük görev, bağımsız QA ve push→CI→merge döngüsünden geçirilir.
5. Kalıcı koordinatör ve yeniden başlatma/lease testi tamamlanır; ancak sonra sürekli servis etkinleştirilir.
