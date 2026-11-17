# RESTORE_REHEARSAL_2026-10-27.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/docs/RESTORE_REHEARSAL_2026-10-27.md
SHA256: 86bda6aa61f3e21aacbb4b319b4a3d85d832e1efbda34df277a8cfb137567fe4

## lines:1-40

# Aynı sunucuda sentetik yedekleme / geri yükleme provası

Taban: `253a5d3a96542cfde65118898abfe90b358c1943`. Yayımlanmış 112 ürün dosyası işlem öncesinde ve sonunda byte düzeyinde aynı bulundu. Ürün kodu değiştirilmedi; yalnız bu belge, `tests/Backend/restore-rehearsal.py` ve çevrimdışı güvenlik testleri `tests/Backend/test_restore_rehearsal.py` eklendi. Commit/push/merge/deployment yapılmadı.

## Bağlantı sınırı düzeltmesi

Önceki arşiv `sameHostOnly=true` değerini koşulsuz yazıyordu; etkin DB uç noktası ve sunucu yerellik kanıtı kaydedilmediğinden önceki kayıt tek başına aynı-host kanıtı değildir. Önceki 10/85/202 sayıları kendi koşusuna aittir; aşağıdaki sonuçlar yeni düzeltilmiş koşudur.

Yeni betik herhangi bir subprocess/DB komutundan ve geçici kaynak oluşturmadan önce açık Host/Port/Username, test DB adı ve tekil kapalı bağlantı anahtarlarını doğrular. DNS, uzak adres, çoklu host, eksik port ve çelişen/ek libpq ortam seçeneklerini reddeder; kabul edilen açık değerlerden temiz PG ortamı kurar. Literal loopback veya doğrulanmış yerel Unix socket dışında hedef kabul etmez. Socket yolunun her bileşeni symlink olmayan dizin olmalı, grup/diğer yazma izni taşımamalı; kök dışındaki dizinler mevcut kullanıcı/root sahipliğinde olmalıdır. Dosya sistemi kökü namespace güven çıpasıdır; bu ortamda UID eşlemesi 65534'tür. Socket gerçek socket ve kullanıcı/root sahipliğinde doğrulanır. İzin değişikliği yapılmadı. İlk sıkı UID denemesi kök UID nedeniyle kaynak/DB komutu oluşturmadan reddedildi; namespace kökü ayrımı belgelenerek düzeltildi.

Yeni koşu `ba442f81839c4c1f`: LOCAL_UNIX_SOCKET, port 55432; DDL öncesi DB gözlemi serverAddress=null, clientAddress=null, port=55432. Bu Unix socket bağlantısının yerel kernel uç noktasını doğrular; uzak/off-site erişim testi değildir. Betik socket yolu/connection string/parola yayımlamaz. Çocuk uygulamalar açık loopback HTTP kullanır; boş ProxyHandler ortam proxy keşfini devre dışı bırakır ve tüm HTTP redirect'leri reddeder.

Yedi çevrimdışı güvenlik testi PASS: uzak/eksik/çoklu/tekrarlı bağlantı, çelişen PG değişkenleri, socket/symlink/izin kontrolleri, temiz loopback ortamı, uzak HTTP, proxy devre dışı bırakma ve redirect reddi. Geçersiz bağlantılarda subprocess ve geçici kaynak oluşturma çağrılarının sıfır olduğu mock ile denetlenir; testler uzak ağa veri göndermez.

## Gerçekleştirilen çalışma

Kurulu resmî PostgreSQL **17.11** sunucu, psql, pg_dump ve pg_restore kullanıldı; yeni araç/servis/hesap/anahtar kurulmadı. Yeni ve benzersiz `poseidon_test_restore_<run-id>_<amaç>` veritabanları ile özel geçici nesne dizinleri oluşturuldu. Önceden bulunan bir veritabanının verileri veya nesne kökü kullanılmadı/değiştirilmedi. Mevcut test bağlantısı yalnız ortamdan okundu; değerleri raporlanmadı. Bilinen mevcut test DB üzerinde sadece yerellik gözlemi, sunucu sürümü ve yeni adın kullanılmadığı kontrol edildi; yeni DB oluşturma mevcut yetkiyle yapıldı.

Son koşu beş yeni DB kullanır: source, restored, missing, corrupt, quota. Önceki ilk başarılı prova da kendi ayrı dört DB/kökünü kullanmıştı. Önceki iki koşu toplam dokuz DB kullanmıştı; bu düzeltme koşusu beş yeni DB kullandı (toplam 14 görev-sahipli DB korunuyor). Tüm koşuların görev-sahipliği manifesti kendi özel çalışma dizininde tutulur. Otomatik silme/DROP yoktur; bu sentetik yerel kaynaklar korunmuştur. Görevin açtığı uygulama süreçleri kapatılmıştır. Mevcut diğer geliştirme servisleri durdurulmadı.

Ürün API'sinden sentetik gemi/römorkör oluşturma → idari kayda alma → gerekçeli yeniden açma → tekrar kayda alma ve sertifika oluşturma → idari kayda alma yapıldı. Gemi ve sertifika için dosya alımı, önceki dosyayı koruyan gerekçeli düzeltme ve kontrollü kesilmiş bir yükleme oluşturuldu. Bunlar bildirilmiş idari metadata ve karantina alımıdır; doğrulanmış/geçerli/operasyona hazır iddiası yoktur.

Uygulama yazıları durdurulduktan sonra DB özel biçimli dump ile, nesne baytları ayrıca kopyalanarak yedeklendi. `pg_restore --exit-on-error --single-transaction --no-owner --no-acl` yalnız yeni ve boş hedef DB'ye uygulandı. `--clean`, `--create` veya DROP kullanılmadı. Uygulama restore edilmiş DB/kökle yeni gerçek .NET sürecinde başlatıldı; aynı restore daha sonra tekrar durdurulup yeni süreçte açıldı. Bu in-process TestServer provası değildir. Yalnız loopback HTTP kullanıldı; native HTTPS/BFF/gerçek IdP sınanmadı.

## Sonuçlar

| Kontrol | Fiilî sonuç |
|---|---|
| Altı tablonun sıralanmış satır SHA-256'ları ve sayıları | Kaynak = restore, PASS |
| Veri kapsamı | 3 şirket, 2 aggregate, 18 audit olayı, 14 idempotency kaydı, 4 migration, 4 alım |
| Dosyalar | 3 kabul edilmiş karantina nesnesi + 1 sıfır bayt kesinti nesnesi; hash/boyut eşitliği PASS |
| Gemi/sertifika UUID, sürüm, idari durum ve ebeveyn ilişkisi | Restore ve gerçek yeniden başlatma sonrası PASS |
| Yetki | Aynı şirkette yetkisiz aktör, başka şirket ve başka tenant için kayıt/alım/geçmiş erişimi reddedildi; PASS |
| Audit / dar güvenli geçmiş | Aynı olaylar; altı alanlı projection; ham aktör/gerekçe sızıntısı yok, PASS |
| Idempotency | Restore sonrası aynı anahtar aynı nesneyi döndürdü; olay/sürüm/kota/dosya değişmedi, PASS |
| Kota hesabı | 322 bayt rezervasyon, 245 fiziksel bayt; kesintiye ait 77 bayt rezervasyon ve 1 açık slot korundu |
| Restore edilmiş kota koruması | Ayrı quota kopyasında 9 ek rezervasyonla 10 açık slota erişildi; sonraki istek 409, aynı anahtar replay ek slot tüketmedi |
| Karantina | UNAVAILABLE tarama, NOT_PERFORMED değerlendirmeler, canDownload=false; içerik GET engelli |
| Eksik dosyalı izole kopya | Dinleme/readiness başlamadan `Stored quarantine object unavailable.`; PASS |
| Baytı değiştirilmiş izole kopya | Dinleme/readiness başlamadan `Stored quarantine integrity reconciliation failed.`; PASS |

## lines:41-63

| Orijinal ve başarılı restore | Negatif kopya deneylerinden sonra DB/dosya hashleri aynı; PASS |

Son makine kaydı **11 PASS kontrol grubu**, **86 araç komutu** ve **202 HTTP isteği** içerir. HTTP kayıtlarında yalnız yöntem/yol/status vardır; token veya gövde yoktur. Komut loglarında bağlantı ortamı yoktur, özel yerel yol `<TASK_PRIVATE>` olarak gösterilir. pg_dump/pg_restore uyarısı görülmedi. Tam birim/browser regresyonları bu görevde yeniden çalıştırılmadı; değişmeyen ürün tabanı üzerinde yukarıdaki gerçek süreç/DB provası çalıştırıldı.

## Sınırlar ve açık kapılar

Bu aynı host ve aynı PostgreSQL sürümündeki sentetik mantıksal restore'dur. Off-site yedek erişimi, kaybedilmiş host, disk/power arızası, fiziksel/WAL/PITR, production RPO/RTO/SLA, rol/ACL/tablespace ve encryption/key recovery doğrulanmadı. `--no-owner --no-acl` kullanıldığı için üretim yetki geri yükleme kanıtı değildir; ayrı role/grant değişikliği yapılmadı.

Dosya ve DB ortak transaction değildir; bu prova yazıları durdurarak tutarlı sabit durumu yedekledi. Canlı yazılar sırasında eşgüdümlü backup/reconciliation işletim süreci ve otomatik kesilmiş alım kurtarma uygulanmış sayılmaz. Bozulma kontrollü **durdurulmuş ayrı kopyalarda** yapıldı; çalışan uygulamada sonradan OS üzerinden dosya değiştirilmesini her okumada yakalama iddiası yoktur. Başlangıç reddi nedeniyle bu kopyalarda HTTP okuma servisi açılamadı.

Scanner, download/preview/OCR/AI/export, insan doğrulama, kontrollü yayın, gerçek kişisel/müşteri verisi, IdP, native TLS ve üretim depolama kapıları açık kalır. Gerçek kullanıcıya özel tarayıcı erişimi kurulmadı; Esra UAT PENDING. Bu prova tamamlanmış modül/platform veya üretim felaket kurtarma kabulü değildir.

## Güvenli tekrar

Betiğin argümansız çalışması yalnız yardım gösterir. Var olan resmî araçları ve ayrı test bağlantısını kullanın; gizli bağlantı değerini komuta/rapora yazmayın:

```sh
python3 tests/Backend/restore-rehearsal.py --run-synthetic-rehearsal
```

Script yayımlanmış taban HEAD'ini doğrular; yeni rastgele DB/kök adlarını kendisi üretir, var olan adı yeniden kullanmayı reddeder, yalnız boş yeni hedefe restore eder. Gerçek dosya yolu girdi olarak kabul etmez; zararsız fixture baytlarını üretir. Dump ve nesne baytları özel geçici dizinde kalır. Yalnız `results/result.json`, script ve bu belge teslim edilir. Bağlantı yetkisi/araç eksikse hata verir; kurulum veya yetki artırma yapmaz.

PostgreSQL'in [pg_dump 17 belgesi](https://www.postgresql.org/docs/17/app-pgdump.html), tek DB için mantıksal yedeği; [pg_restore 17 belgesi](https://www.postgresql.org/docs/17/app-pgrestore.html), arşivin ayrı DB'ye geri yüklenmesini açıklar. Nesne baytları DB dışında olduğundan ayrıca kopyalanmış ve hashleri karşılaştırılmıştır.
