# ACCOUNT-WORKERS-01 — Ayrı Pro/Plus worker ve görev devri

Durum: **yerel kontrol testleri geçti; canlı profil kabulü bekliyor**. Kullanıcı isteği: Esra Pro ve Kadir Plus hesaplarını paralel veya kota sonrası devam için kullanmak.

## Hazırlanan paket

- Ayrı CODEX_HOME + Windows keyring; UI oturumu ve iş Git hesabı korunur.
- Resmi app-server account/read ve rateLimits/read üzerinden plan/kota kontrolü.
- Görev başına ayrı Git worktree ve kayıtlı görev durumu; hesap başına tek worker.
- Açık terminal kota hatasında, ilk işlem bittikten sonra aynı işin diğer uygun hesaba devri; partial dosyalar korunur.
- İki hesap doluysa bekleme; timeout/diğer hata veya belirsiz worker durumunda durma/reconciliation.
- Bounded run, parallel ve resume komutları; başarılı çıktı bağımsız QA için needs_review olur.

## Kanıtlar

- **24 Python testi geçti:** 20 worker/protokol test yöntemi + 4 belge aktarım testi; 0 fail / skip.
- Gerçek Git worktree'leri, sahte hesap/CLI ile parallel izolasyonu, kota devri, partial koruma, busy hesap, aynı işi yeniden yürütmeme, interrupt cleanup ve belirsiz süreçte kilit koruma doğrulandı.
- Metadata durum sorgusu yeni profile bağlanabildi ve oturum olmadığını doğru raporladı; hesap girişleri henüz doğrulanmadı.
- Bağımsız QA bulduğu resume yarışı, interrupt cleanup ve tarihî quota sınıflandırma sorunlarının regresyonlarla giderildiğini doğruladı; draft teslimini onayladı. Son commit doğrulaması ayrıca kaydedilecek.
- Worker'larla henüz gerçek model görevi başlatılmadı. Kontrol testleri canlı failover veya 24 saat servis kanıtı değildir.

## Açık kabul adımları

1. Kullanıcı her profile ilgili hesabıyla OAuth girişini tamamlar; script sunucudan Pro/Plus planlarını doğrular.
2. Birer küçük, süre sınırlı gerçek görev ayrı worktree'lerde çalıştırılır; sonuçları bağımsız incelenir.
3. Draft PR'ın tüm CI kontrolleri ve son commit QA onayı tamamlanır.
4. Sürekli servis, günlük çalışma sınırları, host/restart reconciliation ve faz raporu tetikleri ayrı pakette tamamlanır.

Hesap kotaları birleştirilmez/sıfırlanmaz. Hesabın kendi erişim ve limitleri geçerlidir. Doğal bir kota hatası oluşmadan bütün üyelik kotasını tüketen bir “limit testi” yapılmaz; devir karar mantığı kontrollü regresyonlarla sınanır.

## Önceki teslimin kapanışı

[Foundation PR #1](https://github.com/kadirvurgun/Poseidon/pull/1), hedef commit'in bağımsız QA'sı ve iki başarılı GitHub CI kontrolünden sonra main'e alındı. Merge: a7d5eafcf7ba952c03ff9d41b072c38b4d07304a. Bu teslim bütün S0 veya IAM/React/Playwright kabulü değildir.

Ayrıntılı kurulum ve komutlar: [Codex hesapları](../codex-accounts.md).
