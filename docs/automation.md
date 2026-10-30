# Sürekli ajan geliştirme sistemi

## Kullanıcı kararları

- Hedef: https://github.com/kadirvurgun/Poseidon
- Ajanlar geliştirme için tam yetkili; push ve PR açma açıkça yetkilendirildi.
- Tam geliştirme yetkisi kapsamında, zorunlu testler ve bağımsız inceleme geçtikten sonra normal PR merge hedeflenir. Force push, depo silme ve canlı müşteri verisinde yıkıcı işlemler bu çalışma döngüsünün parçası değildir.
- Kullanıcı iki ChatGPT/Codex hesabını belirtti: Esra Pro ve Kadir Plus. Ayrı oturumlarla paralel görev veya kota sonrası iş devri istendi. [Worker kurulumu](codex-accounts.md) bu isteği uygular; planlar sunucu tarafından doğrulanmadan kullanıma hazır sayılmaz. Claude zorunlu bağımlılık değildir.
- 7/24 host seçimi, kullanım bütçesi, pilot kapasitesi, veri lokasyonu ve IdP henüz yanıtlanmadı.
- Kullanıcı her faz bitiminde bilgi verilmesini istedi: teslim edilenler, test sonuçları, engeller, mevcut aşama ve sonraki adım raporlanır. Yerel alt paket ile tam sprint kabulü ayrı belirtilir.

## Hedef akış

Koordinatör → hazır görev → role özel worktree → geliştirici ve testleri → bağımsız QA → commit/push → PR → tam gereken CI → merge → sonraki görev.

Koordinatör kalıcı görev kuyruğu ve lease tutar. Her görev requirement ID, kaynak sürümü/bölümü, kabul kriterleri, bağımlılıklar, rol, branch, doğrulama komutları ve faz taşır. Geliştirici kendi görevini tek başına kabul edemez. UI geliştirici contract üzerinden çalışır; backend geliştirici API değişikliğini ve consumer etkisini birlikte ele alır.

Kalıcı durumlar: queued, running, verifying, awaiting-ci, done, retryable, blocked. İşlem çökünce lease süresi dolan görev yeniden değerlendirilir; remote branch/commit/PR kontrol edilmeden aynı dış işlem tekrarlanmaz. Aynı dal ve dosyalar eşzamanlı ajanlara verilmez. Testler hedef commit üzerinde geçmeden done üretilmez.

Başlangıç hata politikası önerisi: görev başına en fazla 3 düzeltme denemesi, çalıştırma başına süre sınırı, sürekli aynı hatada blocked. Hesap kotası dolunca o oturum durur; kullanıcının açık talebiyle diğer yetkili ve uygun oturum aynı worktree'den devam edebilir. Her iki hesap da doluysa beklenir. Kullanıcı tarafından belirlenen toplam bütçe/günlük görev sınırı aşılmaz. Yetki olması sınırsız maliyet veya sınırsız tekrar anlamına gelmez.

GitHub Actions CI'ı çalıştırır; sürekli koordinatör ayrı bir makinede servis olarak çalışır. Windows uykuya geçerse veya makine kapanırsa yerel geliştirme durur. GitHub push tetikleri ile PR tetikleri concurrency grubu ile birleştirilir; gereken test kapsamı korunur. Otomasyon hesabının push ettiği commitlerin CI tetiklediği gerçek bir denemeyle doğrulanmalıdır.

## Codex ve Claude

Codex CLI `codex exec --json` ile etkileşim gerektirmeyen görevler çalıştırabilir. Pro/Plus üyeliği genel OpenAI API bakiyesi değildir; API kullanımı ayrı faturalandırılır. Her worker ayrı OAuth home/keyring ile giriş yapar. Kotalar birleştirilmez veya sıfırlanmaz. Kota sonrası görev devri yalnız diğer hesabın kendi hakları ve doğrulanmış müsaitliği içinde yapılır; özel session geçmişi taşınmaz. 7/24 kapasite garantisi yoktur.

Claude gelecekte ayrı bir worker adapter olabilir. Satın alma veya hesap paylaşımı kurulumun ön koşulu değildir. Kullanılacaksa yetkili kişisel/ekip erişimi, headless çalışma ve limitler kendi resmi dokümanlarından doğrulanır.

Resmi kaynaklar:

- https://learn.chatgpt.com/docs/non-interactive-mode
- https://developers.openai.com/codex/auth/

## Bu hazırlıkta doğrulanan durum

- Yerelde .NET 10 SDK, Node, Python 3.13, Claude CLI ve Graphify 0.9.15 bulundu. Docker daemon ve PostgreSQL container çalışması doğrulandı; Claude login doğrulanmadı.
- Codex CLI proje içi `.tools/codex/` altında kurulur; kimlik bilgileri repoya yazılmaz.
- GitHub kimliği repo bazında kadirvurgun olarak ayrıldı; iş hesabı avurgun1 global ayarları korunur. OAuth workflow kapsamı onaylandı; push, PR #1, iki remote CI ve merge tamamlandı: https://github.com/kadirvurgun/Poseidon/pull/1 . Main merge commit: a7d5eafcf7ba952c03ff9d41b072c38b4d07304a.
- Graphify Codex skill'i ve proje keşif talimatı kuruldu. Ürünün RAG servisi kurulmuş değildir.
- Sürekli scheduler/servis yoktur. Foundation için 12 unit/guard + 10 PostgreSQL integration ve iki remote CI geçti; PR #1 main'e alındı. Pro/Plus worker kontrol katmanında 20 worker + 4 belge aktarım testi geçti; gerçek hesap girişleri ve küçük canlı görevler bekleniyor. HTTP/React/Playwright testleri yoktur. Sadece komutların varlığı sürekli otomasyonun aktif olduğu anlamına gelmez.

## Devreye alma kabulü

1. GitHub hesabıyla fetch, görev dalına push, PR açma ve CI sonuçlarını okuma doğrulanır.
2. Host ve kota politikası kesinleşir; Codex login kendi hesabıyla tamamlanır.
3. Uygulama iskeleti ve gerçek test komutları kurulur; eksik testte başarısız olan CI yayınlanır.
4. Tek küçük görev, bağımsız QA ve push→CI→merge döngüsünden geçirilir.
5. Kalıcı koordinatör ve yeniden başlatma/lease testi tamamlanır; ancak sonra sürekli servis etkinleştirilir.
