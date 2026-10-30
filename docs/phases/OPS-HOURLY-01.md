# OPS-HOURLY-01 — Hesap kotası ve saatlik kontrol

Kullanıcı isteği: 31 Ekim 2026, saatte bir kullanım kontrolü, Esra Pro / Kadir Plus görevlendirme ve kullanıcıdan onay beklemeden geliştirme. Tam geliştirme yetkisi ve PR/merge kontrol kuralları korunur.

Esra CLI profiliyle kod yazıldı. İki ayrı CODEX_HOME/keyring, API-key taşımama, native CLI, profil başına tek worker, görev başına ayrı worktree ve süre sınırı eklendi. `codex_watch.py once` model çağrısı yapmadan iki hesabın plan/kota metadatasını atomik kaydeder. Her pencerede %85 kullanım rezervi; Esra öncelikli, yalnız doğrulanmış kendi kotası uygun hesap seçilir. Bu yüzdeler iki planın aynı token kapasitesine sahip olduğu anlamına gelmez.

Saatlik Windows görevi için kurucu hazırdır: `Poseidon-HourlyQuota`, Interactive/Limited, pythonw, parolasız, mutlak/alıntılanmış yollar, IgnoreNew ve beş dakika sınırı. Kurulum ve görev çalışması aşağıda gerçek kanıtla kaydedilmeden aktif denmez.

## Doğrulama

- Esra kod commit `faf24cf`; bağımsız Kadir QA bir bloklayıcı buldu: metadata kapanışı belirsiz tek profil global watch kilidini tutarak sağlıklı hesabın sonraki kontrollerini de durduruyordu.
- Düzeltme `c60ee1b`: yalnız belirsiz profil kilidi korunur; global metadata kilidi bırakılır; sonraki tick sağlıklı profili tekrar sorgular. Snapshot yazma hatası da global kilidi tutmaz. Belirsiz/busy profil varken model dispatch yapılmaz.
- 44 genel Python testi ve bir ayrı Windows scheduler construction testi geçti; 0 failed/skipped. Windows kontrolü Linux'ta skip edilmek yerine ayrı zorunlu CI job'ında çalışır. Güncel analiz testleriyle birlikte kökte 51 genel test + bir Windows kontrolü beklenir; hedef commit'te tekrar doğrulanır.
- Canlı metadata sorgusu: Esra `prolite`, haftalık %18; Kadir `plus`, 5 saat %68 ve haftalık %56. İki hesap da bu sorguda uygundu. Değerler anlık gözlemdir, sabit kota vaadi değildir.
- Gerçek Esra kod worker'ı dosya yazdı ve commit üretti. Shell başlangıç engeli, proje SDK10 host'u değiştirilmeden yanına doğrulanmış .NET9.0.22 Core/Desktop runtime kurulmasıyla giderildi. Bunlar araç bağımlılıklarıdır; ürün hedefi .NET10 kaldı.

## Sınır

Saatlik izleme tek başına 7/24 çok görevli geliştirme servisi değildir. İzleyicinin opt-in görevi tek bounded iş üretir; geçmiş jobs/receipt varken tekrar başlatmaz, sonuç needs_review olur. Kuyruk, bağımsız QA, CI ve yayın/merge koordinasyonu ayrı OPS-COORDINATOR paketinde geliştirilir. Host ve kullanıcı oturumu çalışabilir durumda olmalıdır; kotalar birleştirilmez/sıfırlanmaz. Hiçbir runtime/uzman güvenlik onayı üretilmez.

## Kapanış ve gerçek çalışma

Kadir profili bağımsız QA `06c916e` hedefini onayladı; değişen iki belirsiz-metadata regresyonunu ve Windows kurulum construction testini tekrar çalıştırdı (3 passed, 0 skip/fail). İki GitHub koşusunda üçer job success: push41136015963, PR41136071349; [PR #5](https://github.com/kadirvurgun/Poseidon/pull/5) main ile birleşti (`d295262`).

`Poseidon-HourlyQuota` Windows görevi gerçek olarak kaydedildi ve çalıştırıldı. 31 Ekim 01:09:23 +03:00 manuel ilk çalışma ve 01:10 otomatik başlangıç: LastTaskResult=0. Sonraki planlı çalışma 02:10:22 +03:00. Snapshot UTC zaman damgalı ve sonraki kontrol bilgisi içerir. Son kayıtta Kadir 5 saat %85 / haftalık %58 olduğu için rezerv politikası yeni iş başlatmıyor; Esra meşgul olduğundan bu tick'te dispatch adayı yok. Metadata görevi model çağırmadı. Değerler anlık gözlemdir.

Saatlik izleme **aktif**. Çok görevli geliştirme koordinatörü bu paketin kapsamı değildir ve henüz aktif sayılmaz. [Komutlar ve teknik sınırlar](../hourly-automation.md).
