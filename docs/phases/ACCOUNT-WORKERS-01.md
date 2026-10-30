# ACCOUNT-WORKERS-01 — Ayrı Pro/Plus worker ve görev devri

Durum: **iki hesap girişi doğrulandı; canlı paralel görev kabulü engelli; PR draft**. Kullanıcı isteği: Esra Pro ve Kadir Plus hesaplarını paralel veya kota sonrası devam için kullanmak.

## Hazırlanan paket

- Ayrı CODEX_HOME + Windows keyring; UI oturumu ve iş Git hesabı korunur.
- Resmi app-server account/read ve rateLimits/read üzerinden plan/kota kontrolü.
- Görev başına ayrı Git worktree ve kayıtlı görev durumu; hesap başına tek worker.
- Açık terminal kota hatasında, ilk işlem bittikten sonra aynı işin diğer uygun hesaba devri; partial dosyalar korunur.
- İki hesap doluysa bekleme; timeout/diğer hata veya belirsiz worker durumunda durma/reconciliation.
- Bounded run, parallel ve resume komutları; başarılı çıktı bağımsız QA için needs_review olur.

## Kanıtlar

- **29 Python testi geçti:** 25 worker/protokol test yöntemi + 4 belge aktarım testi; 0 fail / skip (`py -m unittest discover -s scripts -p 'test_*.py' -q`). Native/Node Windows-POSIX launcher, metadata secret redaksiyonu ve izin argümanları da kapsandı.
- Gerçek Git worktree'leri, sahte hesap/CLI ile parallel izolasyonu, kota devri, partial koruma, busy hesap, aynı işi yeniden yürütmeme, interrupt cleanup ve belirsiz süreçte kilit koruma doğrulandı.
- İki profil için canlı OAuth/plan/kota doğrulaması geçti: Esra `prolite`, Kadir `plus`. Profil izin listesi açık Pro varyantlarını kabul eder; fiyat/kapasite eşitliği iddiası yok.
- Bağımsız QA bulduğu resume yarışı, interrupt cleanup ve tarihî quota sınıflandırma sorunlarının regresyonlarla giderildiğini doğruladı; draft teslimini onayladı. Son commit doğrulaması ayrıca kaydedilecek.
- Esra gerçek model turn'ü tamamlandı ancak rapor dosyası oluşmadı; alt oturum read-only bildirdi. Bu görev kabul edilmedi. Kullanıcının tam yetki talebi doğrultusunda explicit full-access CLI ayarlandı; bu ayarla canlı yazma henüz doğrulanmadı.
- Paralel denemelerde metadata RPC ve süreç kapanış hataları yaşandı; başarısız işler/kilitler korunur. Bunlar kota hatası sayılmadı. Kontrol testleri canlı failover veya 24 saat servis kanıtı değildir.

## Açık kabul adımları

1. Tamamlandı: kullanıcı iki ayrı OAuth girişini yaptı; sunucu hesap/plan/kota doğrulandı. Yeniden giriş istenmiyor.
2. Metadata/kapanış engeli çözülür, stale süreçler uzlaştırılır; iki küçük gerçek görevin dosya çıktısı ayrı worktree'lerde doğrulanır ve bağımsız incelenir.
3. Draft PR'ın tüm CI kontrolleri ve son commit QA onayı tamamlanır.
4. Sürekli servis, günlük çalışma sınırları, host/restart reconciliation ve faz raporu tetikleri ayrı pakette tamamlanır.

Hesap kotaları birleştirilmez/sıfırlanmaz. Hesabın kendi erişim ve limitleri geçerlidir. Doğal bir kota hatası oluşmadan bütün üyelik kotasını tüketen bir “limit testi” yapılmaz; devir karar mantığı kontrollü regresyonlarla sınanır.

## Önceki teslimin kapanışı

[Foundation PR #1](https://github.com/kadirvurgun/Poseidon/pull/1), hedef commit'in bağımsız QA'sı ve iki başarılı GitHub CI kontrolünden sonra main'e alındı. Merge: a7d5eafcf7ba952c03ff9d41b072c38b4d07304a. Bu teslim bütün S0 veya IAM/React/Playwright kabulü değildir.

Ayrıntılı kurulum ve komutlar: [Codex hesapları](../codex-accounts.md).
