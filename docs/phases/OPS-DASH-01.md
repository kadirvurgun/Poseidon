# OPS-DASH-01 — iki hesap, model politikası ve kalıcı izleme

Kaynak: kullanıcının 31.10.2026 dashboard, iki ayrı hesap, saatlik kota,
yenilenme alarmı, otomatik devam ve uygun model talepleri.
Durum: **yerelde doğrulanan alt paket; bağımsız QA/CI ve kalıcı aktivasyon açık**.
Canonical S0 kabulü değişmedi: 8/970 SP (%0,82), 2/121 görev.

## Teslim edilen davranış

- React `/agents` TR/EN ekranı gerçek loopback `/ops/status` servisini 5 saniyede
  bir okur. Esra Pro ve Kadir Plus görevleri ayrıdır. “Çalışıyor” ile yeni göreve
  kota uygunluğu ayrı belirtilir. Eksik/eski/meşgul metadata hazır kabul edilmez.
- Kadir, arayüz alt paketini `024dc08`; Esra servis alt paketini `bb39655` olarak
  ayrı OAuth profilleri/worktree'lerle üretti. Ana sohbet hesabının değiştirildiği
  iddia edilmez. Credential veya kimlik verileri dashboard'a taşınmaz.
- [Model politikası](../model-policy.md): rutin iş/test yazımı/rutin QA için
  GPT-6 Luna medium; karmaşık geliştirme/güvenlik QA için GPT-6.1 Sol medium.
  Gerçek model ve effort CLI komutuna bağlanır ve oturum kaydında saklanır.
  Desteklenmeyen/bilinmeyen erişimde çağrı harcanmaz; sessiz yükseltme yoktur.
- Kadir üzerinden iki gerçek Luna salt okunur incelemesi tamamlandı; bunlar
  onay üretmedi. Eksik bağlam nedeniyle raporlanan belge hedefleri için QA verisine
  sınırlı gerçek dosya/link kanıtı eklendi. Başarısız inceleme kabul sayılmadı.
- Son kullanıcı kararıyla Esra geliştirme ve QA için birincil; yalnız doğrulanmış
  kota rezervinde Kadir kendi uygun kotasıyla değerlendirilebilir. Esra profilinin
  metadata kimliği beklenen Esra hesabıyla gerçekten eşleşti (ham kimlik saklanmadı).
  Esra/Luna gerçek ADR incelemesi geçti; [PR #9](https://github.com/kadirvurgun/Poseidon/pull/9)
  tam yerel gate ve tüm push/PR CI kontrolleri sonrasında `7f3ea00` olarak merge edildi.
- Metadata internal error -32603 için aynı profil/lock içinde tek proactive
  refresh denemesi; diğer/bilinmeyen hatalarda bekleme. Esra'da gerçek düzeltme
  doğrulandı; yeni oturum/credential kopyalama gerekmedi.
- API yalnız GET ve tam loopback Host/route kabul eder. Geçmiş alanları allowlist
  ile sınırlandırılır. Kalıcı driver HTTP'yi model işinden ayrı thread'de tutar.
- Gerçek reset epoch + 60 saniye hedefleri ve Windows wake task yapımı; haftalık
  pencere ayrıca korunur. Saat dolması kota yenilendi kanıtı sayılmaz.
- Sekiz saatlik pencere sınırları restart ile sıfırlanmaz. Güvenli devam,
  dependency, exact commit bağımsız QA, dört CI kontrolü ve merge kapısı korunur.

## Doğrulama

| Kontrol | Son gözlenen sonuç |
| --- | --- |
| .NET solution / gerçek PostgreSQL integration dahil | 43 passed, 0 failed, 0 skipped |
| Arayüz component | 34 passed |
| Lint/typecheck/build | Başarılı |
| Playwright desktop + mobile | 10 passed; 4 gerçek .NET API smoke, 6 sentetik ops contract |
| Model routing ve gerçek komut/receipt bağlama | 7 passed; Esra-first/busy/unknown/quota geçişleri dahil |
| Son hesap yönlendirme regresyonları | 31 passed; yeni Esra-first kuralı ve açık legacy paralel modu ayrı doğrulanır |
| Runtime/HTTP/lease/erken metadata yenileme | 17 passed |
| Sabit gate/QA adaptörü | 15 passed |
| Tam Python ve Windows installer paketi | 122 Python passed, 0 failed/skipped; 10 Windows installer passed (6 nightly, 1 hourly, 3 runtime) |
| Güncel analiz kaynak/hash/lineage | 269 dosya, 34.958 bölüm, 0 uyuşmazlık |
| Canlı loopback / tarayıcı | HTTP 200; iki kart ve Esra “Çalışıyor” canlı doğrulandı |

## Açık kapılar ve sonraki adım

Tam yerel gate geçti. [PR #8](https://github.com/kadirvurgun/Poseidon/pull/8) için
bağımsız QA'nın eksik reset zamanı bulgusu düzeltildi; component ve browser
regresyonları eklendi. Proxy üzerinden gerçek HTTP 200 zaten gözlendi; Host
aktarımı ayrıca açık `changeOrigin: true` ile sabitlendi. Yeni exact commit QA
ve uzaktaki dört CI kontrolü tamamlanınca merge edilecek. `ba225c3` bağımsız
incelemede Esra/Sol medium ile onaylandı; sonraki main merge ve test-fixture
değişikliği yeni hedef commit üzerinde yeniden incelenecek. Windows `Poseidon-DevelopmentRuntime` ve reset
görevi henüz gerçekten kaydedilmiş sayılmaz; installer preview testi aktivasyon
kanıtı değildir. İlk ADR işi `ee0aea4` için doğrulanan başlangıç uyarısı hatası
operatör kaydıyla reconcile edildi; gerçek Esra bağımsız QA ve PR/CI/merge
zinciri tamamlandı. Kalan kaynaklı iki görev kalıcı driver kuyruğunda ilerleyecek.

7/24 erişim Windows'un açık, uygun oturumun kullanılabilir ve hostun uyanık
olmasına bağlıdır. WakeToRun donanım/politika desteği gerektirir; kapalı makineyi
başlatma garantisi değildir. Bu paket ürünün GDPR/KVKK veya deniz operasyonu
hazırlık kabulünü oluşturmaz.
