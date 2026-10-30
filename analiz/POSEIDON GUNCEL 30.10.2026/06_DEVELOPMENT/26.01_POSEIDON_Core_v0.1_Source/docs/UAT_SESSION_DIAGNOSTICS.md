# Sentetik UAT oturum tanısı — 29 Ekim 2026

Taban: 699969b25265bb14ee721e53c9be60df787eab5c.
Bu paket auth hatasının giderildiğini iddia etmez; davranışı değiştirmeden
yeni olayları ayırt etmeyi sağlar. Kullanıcının eski olayının nedeni açık.

## Kesin bulgular ve kanıt sınırı

Kullanıcı log özeti: POST LOGIN 409, POST LOGIN 409, POST LOGIN 400,
GET SESSION 401. Önceki dar kuyrukta SESSION 200 de vardı.
Mevcut login handler'ında 409, başarılı AuthenticateAsync sonucundan sonra
aktif oturumu değiştirmeyi reddeder; yeni ticket oluşturmaz, süreyi uzatmaz.
400, CSRF veya istek sınırı hatası olabilir; eski özet hangisi olduğunu söylemez.
401 tek başına 20 dakika dolduğunu göstermez. Cookie yokluğu, çözülemeyen cookie,
bulunamayan/expired ticket veya principal reddi ayırt edilmemiştir.
Uygulama yeniden başlarsa RAM ticket/key kaybı beklenen güvenli davranıştır.
Browser cookie'sini paylaşan sekmelerde logout diğer sekmelerin oturumunu da bitirir.

Yeni testler:
- Logout sonrası yeni login farklı ticket/cookie ve yeni 20 dakikalık süre üretir.
  Eski cookie 401 alırken yeni cookie çalışır.
- Aynı geçerli cookie ile eşzamanlı iki login 409 olur; expiry değişmez.
- Eksik CSRF login 400 olur.
- Yalnız test belleğinde ticket expiry geçmişe alınır: SESSION 401; eski cookie
  taşınırken yeni anonymous CSRF alınarak login yeniden başarılı olur.
Bu test sırası eski kullanıcı olayının sebebinin kanıtı değildir.

IMO mevcut sözleşmesi frontend/backend'de yalnız isteğe bağlı yedi ASCII rakamdır.
123 uzunluk hatası verir; 1234568 için checksum reddi uygulanmaz. Checksum, registry
ve IMO uygulanabilirliği doğrulanmış sayılmaz. Bu paket validation kuralını değiştirmez.

## Yeni sabit tanı olayları

Yalnız DevelopmentUat etkin olduğunda event 4812 ile UTC timestamp, sabit olay
kodu, AGE_SECONDS ve REMAINING_SECONDS yazılır. Kodlar:
PROCESS_STARTED, TICKET_CREATED, TICKET_EXPIRED, TICKET_MISSING, TICKET_REMOVED,
LOGIN_CREATED, LOGIN_EXISTING_SESSION, LOGIN_CSRF_REJECTED,
AUTH_401_COOKIE_PRESENT, AUTH_401_COOKIE_ABSENT, PRINCIPAL_REJECTED, LOGOUT_COMPLETED.

Cookie var/yok bilgisi yalnız boolean sınıfıdır; cookie değeri yoktur.
Ticket handle, kullanıcı/tenant/şirket, isim, ham başlık, exception, token, URL,
IP veya sabit kişi/session hash'i yazılmaz. Ayrı oturumları izleyen ID eklenmez.
TICKET_MISSING nedenin expiry olduğunu söylemez; RAM kaybı/revocation da olabilir.
AUTH_401_COOKIE_PRESENT, cookie'nin geçerli veya çözülebilir olduğunu söylemez.
TICKET_REMOVED tek başına logout sebebi değildir; açık logout ayrıca kaydedilir.

Yaş/süre yalnız server ticket zamanlarından hesaplanır, ±86400 saniyede sınırlanır.
AGE_SECONDS=-1 ve REMAINING_SECONDS=-86401 bilinmeyen değerlerdir. UTC wall clock
kullanılır; gerçek elapsed/monotonic süre veya saat senkronizasyonu kanıtı değildir.
Bu nedenle saat sıçramaları ayrıca değerlendirilmelidir. Başarılı her normal
metadata isteği için yeni oturum olayı veya kullanıcı izi üretilmez.

## Tek kısa, salt-okunur komut (inceleme/yayın sonrası)

    python3 scripts/uat-session-status.py

Sabit task logunun son 128 KiB kısmından son 40 tanı olayını okur. Başlangıçta
kesilmiş satır ve tamamlanmamış son satır atılır; tam allowlist formatı dışındaki
metin yazdırılmaz. Symlink, owner/path ve dosya türü kontrolleri vardır. Raw log,
query veya cookie paylaşma ihtiyacı yoktur. Dosya yoksa SESSION_CHECK_UNAVAILABLE;
eski build logunda tanı yoksa NO_SESSION_DIAGNOSTICS_IN_RETAINED_TAIL verir.
Tanı uygulamayı/DB'yi başlatmaz, durdurmaz, logu/lock/PID dosyasını değiştirmez.

Eski loga yeni timestamp veya olay eklenemez. Kullanıcının uygulaması şu anda
kapalı; inceleme/yayın ve güvenli startup parent tarafından ayrıca yönetilir.
Launcher'ın yeni başlangıçta app.log'u yenilediği unutulmamalıdır; bu paket mevcut
logu elle silmez. Açık kaydedilmemiş formun otomatik kaydı veya kurtarıldığı iddiası yoktur.

## Değişmeyen güvenlik sınırı

20 dakika TTL, sliding=false, CSRF, exact origin/host, özel port/owner kontrolü,
opaque Secure/HttpOnly cookie, server scope ve logout/idempotency bariyerleri değişmez.
Üretim/OIDC için yeni tanı olayları etkinleşmez. Hiçbir otomatik login/retry/recovery
eklenmedi. Native TLS, gerçek Codespaces yeniden denemesi ve kullanıcı kabulü açıktır.
