# Sentetik creator-private Codespaces UAT — 28 Ekim 2026

Taban: `916b6906d1dcaf9541aa8756dbbec49bb3e73c7e`. Önceki pasif hazırlığın yerine çalışan, varsayılan kapalı Development BFF runtime ve tek uygulama başlangıç supervisor'ı uygulanmıştır. **Yerel emülasyon PASS; gerçek Codespaces erişimi ve kullanıcı UAT henüz doğrulanmadı.** Bu turn Codespace, uzak yayın, gerçek hesap/IdP, müşteri verisi veya ücretli servis oluşturulmadı. Tarihsel restore script ve HEAD pini değiştirilmedi.

## Çalışan mimari

`DevelopmentUat:Enabled=true` yalnız Development ortamında kabul edilir. Gerçek OIDC BFF, eski synthetic bearer veya evidence intake aynı anda açık olamaz. Bir sabit sentetik `uat-manager` kimliği server-owned tenant/company ve draft/inventory/certificate yetkilerine sahiptir; evidence yetkisi yoktur. Browser kimlik/tenant/grant seçemez. Kimlik anahtarı issuer+subject olarak kalır; Session.synthetic yalnız gösterim bilgisidir. Üretim ve mevcut bearer/OIDC sözleşmeleri varsayılanda değişmez.

Giriş mevcut `/bff/login` POST+antiforgery sözleşmesini kullanır; aktif session üstüne giriş 409, unsafe returnUrl 400. OIDC sağlayıcı çağrısı veya tarayıcı access token'ı yoktur. RAM ticket store, tahmin edilemeyen handle, 20 dakika sabit süre, Secure/HttpOnly/SameSite=Strict cookie ve process-only Data Protection kullanılır. Düzeltilmiş sürüm anahtar/cert/client-secret dosyası oluşturmaz. İlk yerel kontrolde ASP.NET DataProtectionHostedService bağımsız key warmup yüzünden görev HOME dizininde bir test anahtar dosyası oluşturmuştu. İçeriği okunmadan/aktarılmadan yalnız bu görev dosyası silindi. Framework key repository de RAM-only yapıldı; negatif test ve son gerçek süreç/browser koşusunda disk üzerinde sıfır key XML doğrulandı. Bu ilk kusur gizlenmiş bir PASS değildir. Restart mevcut cookie'leri geçersiz kılar; DB örnek kayıtları korunur. En çok 100 aktif session; süresi dolanlar yeni girişte ayıklanır. Logout CSRF+görüntülenen scope stamp ister; GET logout yoktur. Bearer API cookie'yi kabul etmez. Ürün session/uncertain retry/işlem bariyerleri korunur.

Tek Kestrel listener `127.0.0.1:8080` statik React ve BFF'yi birlikte sunar. Runtime yalnız loopback bağlantı ve açıkça yapılandırılmış tam HTTPS host kabul eder. X-Forwarded-* ile host veya şema seçilmez. İncelenmiş geliştirme proxy sözleşmesi olarak şema HTTPS'e atanır; **bu TLS doğrulaması değildir**. Tüm unsafe isteklerde tam Origin eşitliği, BFF yazılarında ayrıca antiforgery ve scope kontrolü vardır. Null/missing/cross-origin reddedilir. Sentetik modda `Referrer-Policy: same-origin` gerekir: `no-referrer` gerçek tarayıcı form POST Origin'ini null yapıyordu; kontrol gevşetilmeden bu uyumsuzluk giderildi. Normal OIDC modunun politikası değişmedi.

## Tek başlangıç ve kolay durdurma

Bağımsız inceleme ve ayrı Codespace oluşturma aşamasından **sonra**, yalnız teslim edilen doğru branch/commit üzerinde terminalde:

```sh
python3 scripts/uat.py --free-only-confirmed
```

Bu komut yalnız ücretsiz hakkın kullanıcı tarafından doğrulandığını beyan eder; quota sorgusu değildir. GitHub'ın otomatik verdiği credential yalnız `gh` salt-okunur Codespace/port sorgularına process ortamıyla aktarılır, yazılmaz/yazdırılmaz. Gerçek owner `emgt41`, özel `emgt41/Poseidon` repo, tam beklenen HTTPS URL ve yalnız **8080 Private** portu doğrulanmadan uygulama başlamaz. Beklenen 8080 Ports satırı yoksa kullanıcı Ports panelinde 8080'i **Private** olarak ekleyip tekrar dener. `org/public`, ek DB/API portu, farklı host/owner/repo veya sorgu hatası başlatmayı reddeder. Kod port visibility değiştirmez.

Runtime foreground çalışır. **Ctrl+C** kendi uygulamasını ve kendi PostgreSQL sürecini kapatır; aynı komutla tekrar başlatılır. Başlatma kilidi aynı anda ikinci supervisor'ı reddeder. Restart öncesi “server stopped” mesajını bekleyin. Codespace'i tamamen durdurmak: GitHub Your Codespaces → ilgili ortam → … → Stop codespace. Yeniden açmak için aynı ortam adına tıklayın, ardından başlangıç komutunu tekrar çalıştırın. Sekmeyi kapatmak Codespace'i durdurmaz. Stop depolamayı silmez; durmuş depolama da kotayı tüketir. Silme otomatik değildir.

`.devcontainer` yalnız 8080'i forward eder, başka otomatik port keşfini kapatır; install/start lifecycle hook yoktur. Container mevcut unprivileged `postgres` OS kullanıcısıyla çalışır; root başlatma reddedilir. Resmî sürümlü .NET SDK 10.0.403, Node 24.19.0, PostgreSQL 17.11 image katmanları ve Debian araçları kullanılır. Image tag'leri/apt OS paketleri digest/snapshot kilitli değildir; byte-identical container iddiası yoktur. Repository/NuGet/npm lock dosyaları kullanılır.

Supervisor yalnız kendi `$HOME/.poseidon-synthetic-uat` alanını oluşturur; mevcut tanımsız alanı, symlink veya başka checkout marker'ını reddeder. PostgreSQL yalnız özel Unix socket kullanır; TCP listener yoktur. Yeni local cluster peer authentication/host reject ile kurulur; gerçek sunucu hesabı/grant değiştirilmez. `poseidon_test_uat` yalnız sentetik DB'dir. Yeni container'ın database initialization'ı bilinçli opt-in Development başlangıcıdır; mevcut gerçek DB'ye bağlanma seçeneği yoktur. Rebuild container içindeki sentetik verileri kaybettirebilir; bu bir backup/DR çözümü değildir.

## Build ve secret inheritance

Başlangıç, child process ortamını dar allowlist'ten kurar: GitHub token, ConnectionStrings, PG*, proxy, VITE, npm ayarları, loader değişkenleri app/build'e devredilmez. Dotenv dosyası varsa değerini okumadan build reddedilir. NuGet yalnız açık resmî kaynak config'i ve locked restore kullanır; güvenlik/audit kontrolü kapatılmaz. `npm ci --ignore-scripts` kullanılır; incelenmiş build komutu daha sonra çalışır. ASP.NET geliştirme sertifikası üretimi açıkça kapalıdır. Kullanıcının Codespaces secrets/dotfiles eklememesi gerekir; bu süreç izolasyonu kötü niyetli aynı-OS-kullanıcısına karşı sandbox değildir.

Visibility başlangıçta ve çalışırken 5 saniye aralıkla salt-okunur tekrar denetlenir; hata veya uygunsuz durumda app ve görev DB'si durdurulur. Sorguların timeout süresi nedeniyle anlık visibility değişiminde sıfır gecikme garantisi yoktur. Kullanıcı **çalışırken portu public/org yapmamalıdır**. GitHub tarafındaki creator-private enforcement'ın yerini bu polling almaz. Otomatik token sorgu için yeterli değilse komut fail-closed kalır; yeni token/permission/grant üretmeye çalışmaz.

## Fiilî doğrulama

- Mevcut PostgreSQL süreç yokluğu `pg_ctl status` ile doğrulandı. Aynı onaylı veri dizini, Unix socket ve port 55432 seçenekleriyle yeniden başlatıldı; readiness PASS. Önceki kayıtta temiz shutdown kanıtı yok; kesin sonlandırma nedeni bilinmiyor. Güvenlik/ağ ayarı değiştirilmedi.
- Backend: **176 PASS, 0 SKIP**, gerçek PostgreSQL ve mevcut OIDC browser fixture dahil. Yeni 15 UAT testi Development-only, config çatışmaları, Origin/host, CSRF, cookie, logout, bearer ayrımı, unsafe return, expiry/revocation ve başka process store reddini kapsar.
- Frontend: **103 PASS**, typecheck/build PASS. İlk hazırlık turn'ündeki inherited VITE bayrağı sorunu temiz test ortamıyla giderilmişti; bu runtime turn'ünde ürün testleri değiştirilerek geçiş sağlanmadı.
- Offline başlangıç/visibility/secret-inheritance testleri: **27 PASS**; API yanıtı şekilleri sentetik fixture ile sınandı, canlı GitHub sonucu değildir.
- Mevcut gerçek Chromium OIDC/BFF matrisi: **6/6 PASS**, 390×844 / 768×1024 / 1440×900, TR/EN.
- Yeni gerçek Kestrel + ayrı sentetik PostgreSQL + Chromium matrisi: **6/6 PASS**. Giriş, opaque Secure/HttpOnly cookie, CSRF reddi, bearer ayrımı, evidence kapalı, tug metadata kayıt, kayıp create yanıtı/same-key retry, dirty-edit iptali, deferred logout bariyeri, tekrar giriş ve yatay taşma kontrolü.
- İki browser matrisi HTTPS/redirect'i Playwright routing ile emüle eder. Chromium harness'leri yalnız bu yerel testlerde `--no-sandbox` kullanır; sonuçlar tarayıcı sandbox güvenliğini kanıtlamaz. Handler'ın exact local Location'ı aynı-origin yeni navigation'a çevrilir; native redirect/TLS veya gerçek Codespaces adresi kanıtı değildir. Windows/Android fiziksel cihaz testi NOT_RUN.
- Yerel supervisor'ın `--local-emulation --skip-build` mühendislik yolu, önceden hazırlanmış son build ile readiness ve gerçek app/DB start-stop-restart çalıştırdı. Bu flag Codespaces modunda reddedilir. DB örnekleri task-owned alanda tutulur; dump/dosya payload'ı arşive alınmaz.

## Tam olarak kalan dış doğrulama engelleri

1. Docker build'in resmî `mcr.microsoft.com/dotnet/sdk:10.0.403-noble` blob indirmesi **Forbidden** ile başarısız oldu. Container build PASS değildir. İlk yanlış Debian SDK tag'i mevcut noble tag ile düzeltildi; son engel registry blob erişimidir. Erişim ayarı değiştirilmedi veya reddedilen transfer dolaşılmadı.
2. Tam temiz build supervisor'ı NuGet service index/vulnerability verisine erişemedi (NU1301/NU1900); başlangıç durdu, servis başlamadı. Kurulu onaylı dependency cache ile ayrı build/test PASS; audit kontrolü kapatılarak cold-start başarısı uydurulmadı. Gerçek Codespaces'te container/toolchain + locked restore/npm kurulumu ayrıca sınanmalı.
3. Gerçek Codespace oluşturma, automatic credential yetkisi, private port sorgu schema/sonucu, dış host/proxy peer/header davranışı, GitHub owner oturumu ve ücretsiz kalan haklar **NOT_RUN**. Tek kullanıcıya açılabilen URL yok; hazır diye bildirilmeyecek.
4. Esra kabulü PENDING. Gerçek Windows/Android browser, oturumsuz/farklı GitHub hesabından erişim reddi, gerçek HTTPS cookie/Origin ve restart deneyimi kullanıcı adresi oluşunca sınanmalı. Gerçek IdP, üretim, off-site DR, scanner/evidence kullanımı veya operasyonel onay iddiası yoktur.

## Kullanıcının ayrı oluşturma aşamasındaki adımları

İncelenmiş branch/commit verildikten sonra emgt41 kişisel hesabı ve özel repo seçilir; ücretsiz kalan haklar doğrulanır. Kart/paid kullanım açılmaz; ücretsiz devam edilemiyorsa durulur. Yalnız Private 8080 ve uygulamanın verdiği gerçek HTTPS adresi kullanılır. Windows ve Android tarayıcıda aynı GitHub hesabıyla açılır; cihaz kurulumu gerekmez. Yalnız örnek veri kullanılır, otomatik credential paylaşılmaz. UAT senaryoları önceki inventory/certificate belgeleriyle yürütülür; teknik PASS kullanıcı kabulü değildir.

## Resmî kaynaklar

[Port forwarding](https://docs.github.com/en/codespaces/developing-in-a-codespace/forwarding-ports-in-your-codespace), [Codespaces güvenliği](https://docs.github.com/en/codespaces/reference/security-in-github-codespaces), [billing](https://docs.github.com/en/billing/concepts/product-billing/github-codespaces), [stop/start](https://docs.github.com/en/codespaces/developing-in-a-codespace/stopping-and-starting-a-codespace), [lifecycle](https://docs.github.com/en/codespaces/about-codespaces/understanding-the-codespace-lifecycle), [CLI port sorgu alanları](https://cli.github.com/manual/gh_codespace_ports). Genel Free plan kotası kullanıcının gerçek kalan kotası değildir; bu paket billing/account ayarı değiştirmez.


## İnceleme düzeltmesi — PostgreSQL başlangıç yaşam döngüsü

`pg_started` yalnız başarılı `pg_ctl start` sonrasında işaretleniyordu; kısmi start/timeout/Ctrl+C bir child sürecini geride bırakabilirdi. Detached `pg_ctl start` kaldırıldı. Supervisor artık PostgreSQL'i doğrudan child handle ile başlatır. Önce görev marker'ı, özel dizin sahipliği/inode ve `pg_ctl status=3` doğrulanır; çalışan veya belirsiz cluster sahiplenilmez/durdurulmaz. Start girişimi process creation öncesinde işaretlenir; SIGINT/SIGTERM handle atanıncaya kadar ertelenir, child kendi normal signal mask'ine döner.

`finally`, uygulama cleanup'ı hata verse bile görev DB cleanup'ını çalıştırır. Yaşayan child, görev marker/dizin kimliği ve PID dosyasındaki PID ile eşleştirilir. PID dosyası henüz yoksa yalnız eldeki child handle'a SIGINT gönderilir. v3 kapatmada `pg_ctl -D ... stop` kullanmaz: yalnız retained child handle ile SIGINT gönderilir ve reap beklenir; böylece kontrol ile path tabanlı stop arasındaki TOCTOU kaldırılır. PID/ownership uyuşmazlığı başka sürece sinyal göndermez. Kapatma tamamlanamazsa durum açık hata olarak kalır; “stopped” başarısı verilmez. SIGKILL/host kaybında finally çalışacağı iddia edilmez; önceden çalışan cluster otomatik sahiplenilmez.

v2: 14 deterministik lifecycle testi ve önceki 8 kontrol toplam 22 PASS idi. v3 aşağıdaki beş gerçek-sinyal regresyonuyla toplam 27 PASS oldu. Start failure, readiness timeout, Ctrl+C, PID öncesi kesinti, spawn failure, mevcut/unknown server, foreign marker/PID, stop timeout, mask/handle sırası ve restart denetlendi. Ayrıca sadece mevcut görev-sahipli yerel UAT cluster'ında gerçek postmaster ile readiness timeout ve interrupt enjekte edildi: iki child reap edildi, her iki durumda status=3; sonraki gerçek restart ready oldu ve kontrollü kapandı. Başka DB cluster'ı durdurulmadı.

v2 düzeltmesi sonrası 176 backend / 103 frontend PASS, typecheck/build PASS, iki browser matrisi 6/6 PASS. Bunlar **cached dependency + skip-build supervisor emülasyonudur; temiz container başlangıcı değildir**. Önceki Docker registry Forbidden ve NuGet audit erişim hataları yeniden denenmedi, kontroller gevşetilmedi. Gerçek Codespaces ve kullanıcı erişimi hâlâ NOT_RUN/PENDING.


## v3 — queued ikinci sinyal cleanup hatasını örtemez

Cleanup sırasında RuntimeError oluşunca signal mask'i açarken teslim edilen ikinci Ctrl+C v2'de hatayı KeyboardInterrupt ile değiştirebiliyordu. v3 `ShutdownOutcome.cleanup_error` alanında başarısız kapanış sonucunu mask açılmadan önce saklar. Unmask sırasında teslim edilen sinyal yakalanır; cleanup hatası önceliklidir. CLI de saklanan sonucu denetler: cleanup failure için stderr `Cleanup failed: ...`, exit 1; `Stopped` yazılmaz. Başarılı kapanışta sıradan veya ikinci Ctrl+C hâlâ exit 0/Stopped olur.

Yeni testler gerçek `run()`/CLI yolunu fake cluster ile çalıştırır; mask altında sürece gerçek SIGINT/SIGTERM kuyruğa alınır. Subprocess ve ağ çağrıları sıfırdır. Queued ikinci SIGINT + close timeout, queued SIGTERM + close timeout, sıradan başarılı stop, ikinci SIGINT sonrası başarılı stop ve sinyalsiz cleanup failure sonuçları denetlenir. Tüm 27 offline test PASS. Yalnız görev cluster'ında gerçek timeout/interrupt, child reap, ardından ready restart/stop ve cached skip-build supervisor start/stop yeniden PASS.

v3'te yalnız supervisor/docs/offline test dosyaları değişti. `src/` ve `tests/Backend/` dosyaları v2 snapshot ile SHA-256/byte karşılaştırıldı; 176 backend, 103 frontend, typecheck/build ve iki 6/6 browser matrisi kanıtları **v2'den taşınır, bu turn tekrar çalıştırılmadı**. Temiz container başlangıcı hâlâ doğrulanmadı. Docker/NuGet blocked operasyonları yeniden denenmedi; Chromium `--no-sandbox` browser sandbox güvenliğini kanıtlamaz. Bağımsız teknik inceleme ve gerçek Codespaces/UAT kabulü bekleniyor.

## 28 Ekim 2026 — gerçek başlangıç npm config çakışması düzeltmesi

Düzeltme tabanı: `37f06d7619740c73e52cb38f3762b533a3b50fbe` (128 dosya yayımlanmış kaynakla byte düzeyinde doğrulandı). Kullanıcının Codespaces çıktısı backend build 0 hata/uyarı sonrasında npm `double-loading config "/dev/null" as "global", previously loaded as "user"` hatasını gösterdi. Bu bir ağ/izin hatası değil: `clean_env()` hem `NPM_CONFIG_USERCONFIG` hem `NPM_CONFIG_GLOBALCONFIG` için aynı yolu veriyordu; npm aynı config yolunu iki katmanda yüklemeyi reddediyor. Aynı hata yerel gerçek npm 11.9.0 ile yeniden üretildi.

İki değişken artık repo içindeki **ayrı**, yalnız yorum içeren `.devcontainer/npm-user.npmrc` ve `.devcontainer/npm-global.npmrc` dosyalarını gösterir. Kullanıcının HOME/.npmrc veya devralınan global config'i açılmaz; token/proxy/registry/TLS/audit env değerleri devralınmaz. TLS doğrulaması ve audit kapatılmadı; `npm ci --ignore-scripts` aynen korunur. Runtime, BFF, CSRF, origin, private port denetimi, sahiplik veya cleanup davranışı değiştirilmedi. Yeni package/dependency veya hesap/secret eklenmedi.

Fiilî kontroller:

- 30 supervisor/offline test PASS, 0 SKIP. Yeni üç test **gerçek npm** kullanır: eski hatanın tekrar üretimi, poisoned sentetik HOME/global config'e rağmen `strict-ssl=true`/`audit=true`/resmî registry çözümlemesi, gerçek local fixture `npm ci --ignore-scripts --offline` kurulumu. Install/postinstall sentinel'leri oluşmadı; fixture testi ağ istemez.
- Ayrı gerçek uygulama kurulumu: temiz environment ile `npm ci --ignore-scripts --prefix src/Frontend --cache <mevcut-onaylı-cache>` exit 0, 133 paket kuruldu. Bu komutta `--offline`, `--no-audit`, `strict-ssl=false` veya TLS bypass yoktur. Mevcut cache kullanıldığı için boş cache indirmesi kanıtı değildir; log bağımsız bir vulnerability audit sonucu iddiası taşımaz.
- Aynı temiz environment altında frontend **103/103 PASS**, typecheck ve BFF/UAT build PASS. Node 24.19.0, npm 11.9.0. Dependency lock dosyaları değişmedi.
- Backend/browser ürün kaynakları tabanla aynı; bu dar npm düzeltmesinde backend ve browser matrisleri yeniden çalıştırılmadı. Önceki ürün kanıtları tarihsel olarak korunur. Kullanıcı Codespaces'inde düzeltilmiş startup henüz denenmedi; actual private URL/UAT hazır iddiası yoktur.
- Önceki Docker registry ve NuGet audit engelleri bu turn **yeniden denenmedi**. Codespace/billing/erişim ayarı değiştirilmedi; yeni Codespace, push/deploy veya kullanıcı cihazında işlem yapılmadı. Kullanıcının mevcut Codespaces backend build başarısı kullanıcı çıktısıdır; yerel eski cold-start engelleri geriye dönük PASS yapılmadı.

### İnceleme ve yayın sonrasında en kısa güncelleme

**Şimdi kullanıcıdan tekrar çalıştırma istenmez.** Parent düzeltmeyi bağımsız inceleyip aynı dala yayımladıktan sonra, mevcut Codespaces terminalinde:

```sh
git status --short
git pull --ff-only origin feature/private-codespaces-uat
git rev-parse HEAD
python3 scripts/uat.py --free-only-confirmed
```

`git status` temiz değilse pull yerine değişiklikler korunup parent'a bildirilir; reset/clean/stash otomasyonu yoktur. HEAD, parent'ın vereceği yeni incelenmiş commit ile eşleşmelidir. Ayrı container rebuild/yeni Codespace gerekmez: değişiklik Python env yolları ve repo config dosyalarıdır. `$HOME/.poseidon-synthetic-uat` silinmez, DB init/reset yapılmaz; mevcut runtime verileri korunur. Uygulama zaten çalışıyorsa önce kendi terminalinde Ctrl+C ve başarılı stop beklenir. Ücretsiz/private koşulları aynen geçerlidir.

Kaynak: [npm config katmanları](https://docs.npmjs.com/cli/v11/configuring-npm/npmrc/), [npm ci / ignore-scripts](https://docs.npmjs.com/cli/v11/commands/npm-ci/). Bu düzeltme cold-start'ın npm config engelini kapatır; sonraki gerçek startup adımlarını otomatik başarılı saymaz.

## 28 Ekim 2026 — Gözlenen Codespaces Host dönüşümü

Taban: `a53135db708a422ee7ff159427f11994f493b41b`. Sahip tanılaması,
aynı uygulamada beklenen dış Host ile yerel GET için 200, tarayıcıdan gelen
başarısız GET günlüğünde ise tam `localhost:8080` gösterdi. Bu, gerçek proxy'nin
bütün başlıklarının veya peer adresinin gözlemlendiği anlamına gelmez.

Development UAT artık yalnız doğrulanmış Codespaces origin yapılandırıldığında,
loopback peer üzerinden **tam `localhost:8080`** taşıma authority'sini de kabul eder.
Mevcut dış Host yolu korunur. Diğer localhost yazımları, IP authority'leri,
portlar veya birleşik Host değerleri bu istisnaya dahil değildir. Uygulama Host
ve scheme'i doğrulanmış sabit origin'den alır; Forwarded/X-Forwarded-* hiçbir
kimlik, origin veya yönlendirme yetkisi sağlamaz. Güvensiz metotların Origin
kontrolü hâlâ dış HTTPS origin'e karşıdır; antiforgery ayrıca zorunludur.
Loopback listener, Development-only/default-off davranışı, creator/private-port
kontrolleri, sentetik kapsam ve Secure/HttpOnly cookie değişmedi. Localhost
emülasyon origin'i bu Codespaces istisnasını etkinleştirmez.

Tarayıcı regresyonu `UAT_ORIGIN=https://synthetic-owner-8080.app.github.dev`
ve `UAT_TRANSPORT_HOST=localhost:8080` ile gerçek loopback Kestrel'e yönlendirilir.
Bu HTTPS/proxy emülasyonudur; gerçek Codespaces retesti veya Windows/Android cihaz
kabulü değildir. Chromium test harness `--no-sandbox` kullanır ve tarayıcı sandbox
güvenliğini kanıtlamaz. Docker/NuGet cold-start kısıtları bu çalışmada yeniden
denenmedi; mevcut önbelleklerle `--no-restore` kullanıldı.

Bağımsız inceleme ve ayrı yayın tamamlandıktan sonra sahibi çalışan launcher'ı
Ctrl+C ile durdurabilir; `git status --short` temizse `git pull --ff-only origin
feature/private-codespaces-uat` ve `git rev-parse HEAD` ile incelemecinin bildirdiği
commit'i doğrulayıp `python3 scripts/uat.py --free-only-confirmed` çalıştırabilir.
Bu bir yayın/yeniden başlatma yapıldığı iddiası değildir. Reset/clean, yeni Codespace
veya veri silme gerekmez; HOME altındaki sentetik runtime verisi korunur.

## 28 Ekim 2026 — Supervisor kilidinin açık yaşam süresi

Taban `dd44f67c57c502c5b7fe9f30bc2d4f8052991731`. Eski kodda kilit dosyası
explicit olarak kapanmıyordu. Traceback referansı tutulunca `run()` dönmüş olsa
bile açık dosya/kilit tutulabiliyordu; izole gerçek flock regresyonu bunu tekrar
üretti. Bu bulgu, sahibin bildirdiği Errno 11 hatasının kesin nedeni değildir:
aynı errno başka işletim sistemi kaynaklarından da gelebilir. Normal süreç çıkışı
kilidi serbest bırakır; terminal etiketi tek başına süreç hâlâ yaşıyor kanıtı değildir.

Kilit artık context manager ile build, runtime ve cleanup boyunca tutulur; run
çıkarken, başarı/hata/interrupt ayrımı olmadan kapanır. Stopped mesajı bundan
sonra yazılır. Önceden tutulan kilit açık bir hata ile reddedilir; kilit dosyası
silinmez/değiştirilmez, başka süreç durdurulmaz. Cleanup başarısızlığının nonzero
sonucu ve ikinci sinyal koruması aynen korunur. Gerçek kullanıcı ortamındaki
mevcut kilidi bu patch uzaktan çözmez; önce salt-okunur tanılama gerekir.

34 startup testi PASS (gerçek geçici flock dosyaları, sahte cluster; DB/ağ yok
olan kilit testlerinde). İlk kaynakta dört yeni kontrol 3 FAIL + 1 ERROR verdi.
Ürün/backend/frontend kodu değişmedi; önceki 187 backend, 103 frontend ve iki
6'lı tarayıcı matrisi bu patch üzerinde yeniden çalıştırılmış sayılmaz.

## 28 Ekim 2026 — Başlatma ve cleanup hatalarını ayrı koruma

Taban `aa51a7d59c8e93ee25e2bb2e38c67c26dfe3b3de`. Gerçek Stop/resume sonrası
bildirilen `Cluster PID differs from owned child` güvenlik reddi aynen korunur.
Bu mesaj asıl başlatma hatasını finally üzerinden örtebiliyordu. Artık orijinal
hata nesnesi ve sabit aşama tutulur; cleanup da başarısızsa önce sabit aşama/hata
sınıfı (ve OSError için yalnız errno) yazılır. Subprocess argümanı, bağlantı
bilgisi, ortam veya hata payload'u yeni tanı satırında gösterilmez. İkinci
SIGINT ve cleanup nonzero davranışı korunur. Başlatma/sinyal/ownership davranışı
gevşetilmedi; PID dosyası otomatik silinmez, mevcut süreç benimsenmez.

37 startup testi PASS. Yeni, ayrı sentetik PostgreSQL 17.11 kümesinde owned
SIGQUIT sonrası WAL recovery ve veri korunması; var olmayan PID marker'ıyla
normal restart; başka canlı task-owned PID marker'ında no-adoption/no-signal;
son clean restart/stop kontrolleri PASS. Bu test container restart/PID namespace
reprodüksiyonu değildir. Sahibin gerçek hatasının nedeni hâlâ tanılama bekliyor.
DB/readiness ve fixed log sınıfları istenmiştir; tek başına eski log satırı
şimdiki process state kanıtı değildir. Kullanıcıya rerun, dosya silme veya PID
kill talimatı verilmez. Backend/frontend/tarayıcı ürün testleri değişmeyen kod
üzerinde bu turda yeniden çalıştırılmadı. Yeni altyapı, dependency indirme veya
üretim/veri kurtarma işlemi yapılmadı.

### Tek komutluk salt-okunur kimlik tanılaması

İncelenen sürüm ayrı olarak yayımlandıktan sonra, normal proje terminalinde:

```sh
python3 scripts/uat-diagnose.py
```

Argüman/path/stop/start modu yoktur. Yalnız kaynakta tanımlı HOME görev dizini
ve yerel Unix soketi 55433 incelenir. Owner marker, özel dizinler, symlink reddi,
PID/soket lock eşleşmesi, executable, process başlangıç tick'i ve server UTC
başlangıç zamanı doğrulanır. SQL yalnız server settings ve başlangıç zamanını
okur; kullanıcı tablolarını okumaz. psql -X/ON_ERROR_STOP/no-password, explicit
local user/database, read-only transaction seçeneği ve 5 saniye timeout vardır.
Ambient libpq/service/password/proxy ortamı aktarılmaz; parola dosyası /dev/null.

Çıktı yalnız TASK_DB_IDENTITY_MATCH veya CHECK_INCONCLUSIVE. Eşleşme anlık bir
kimlik gözlemidir; process parent/önceki launcher ilişkisini kanıtlamaz, shutdown
izni vermez. PID namespace dönüşümü, kaybolan/değişen süreç, malformed dosya,
symlink, izin/araç/SQL sorunu belirsiz sonuç üretir; otomatik kurtarma yoktur.
Betik signal göndermez, flock almaz, dosya silmez/değiştirmez, SQL yazmaz.
Gerçek Codespaces nedeni hâlâ açık; bu ekleme onu çözüldü diye sunmaz.

### Sabit aşama kodlu tanılama (f3d689f sonrası)

Aynı tek komut `python3 scripts/uat-diagnose.py` artık ilk başarısız kontrolü
`CHECK_INCONCLUSIVE:<KOD>` şeklinde bildirir. Başarı çıktısı değişmedi.

- PATH_CHECK: görev dizini varlığı, sahiplik, izin veya symlink kontrolü.
- MARKER_CHECK: görev/checkout marker kontrolü.
- PID_CHECK: postmaster.pid varlığı, formatı veya yerel dizin/port/soket alanları.
- SOCKET_PID_CHECK: soket lock dosyası ile postmaster.pid ilk alanlarının eşleşmesi.
- SOCKET_CHECK: Unix soketinin tipi, sahipliği veya varlığı.
- EXECUTABLE_CHECK: postgres/psql yolu, executable erişimi veya process exe eşleşmesi.
- PROCESS_CHECK: /proc bilgisine erişim veya format; PROCESS_NAMESPACE_CHECK,
  PROCESS_UID_CHECK, PROCESS_STATE_CHECK: namespace/UID/process state önkoşulu.
- SQL_CONNECT: yalnız açık yerel socket/port/user/database üzerindeki psql çağrısı
  başarısız/timeout; SQL_RESULT_CHECK: beklenen JSON/sayısal sonuç biçimi.
- DATA_DIRECTORY_CHECK, SQL_SOCKET_CHECK, START_TIME_CHECK: sunucudan okunan
  değerlerin görev tanımıyla eşleşmemesi.
- SNAPSHOT_CHANGED: SQL sırasında process/PID/soket snapshot'ı değişmiş veya
  tekrar okunamamış. Alt process kontrolleri kendi sabit kodlarını koruyabilir.
- ARGUMENT_CHECK, INTERRUPTED, INTERNAL_CHECK: desteklenmeyen argüman,
  kullanıcı kesintisi veya sınıflandırılamayan beklenmedik durum.

Hiçbir kod ham değer/hata mesajı, PID, yol, credential veya process argümanı
ifşa etmez. Kontroller gevşetilmedi; başarısız aşama durur. Eksik NSpid veya
namespace dönüşümü hâlâ belirsizdir. Sahipten raw logs/env istenmez. Bu kodlar
nedeni daraltır; örneğin SQL_CONNECT tek başına yanlış rol veya timeout ayrımı
kanıtı değildir. Herhangi bir MATCH/ret sonucu otomatik shutdown yetkisi değildir.

57 startup/diagnostic test PASS; önceden oluşturulmuş görev fixture'ı üzerinde
real PostgreSQL MATCH ve clean stop sonrası PID_CHECK doğrulandı. Yeni DB yok.
Gerçek Codespaces ret nedeni sahibi bu yeni çıktıyı çalıştırana kadar açık.

### PostgreSQL başlangıç fazları ve kernel peer doğrulaması

8600bbf tabanındaki START_TIME_CHECK eşitlik varsayımı hatalıydı. PostgreSQL 17
`InitProcessGlobals` başında MyStartTimestamp/MyStartTime alır; pid dosyasına
MyStartTime yazılır. PgStartTime ise PostmasterMain'in daha sonraki aşamasında
alınır ve pg_postmaster_start_time() bunu döndürür. Bu iki fazın iki saniye içinde
olma garantisi yoktur. /proc start tick'i bu epoch karşılaştırmasına katılmıyordu.
Resmî kaynaklar:
- https://github.com/postgres/postgres/blob/REL_17_STABLE/src/backend/postmaster/postmaster.c
- https://github.com/postgres/postgres/blob/REL_17_STABLE/src/backend/utils/init/miscinit.c
- https://github.com/postgres/postgres/blob/REL_17_STABLE/src/backend/utils/adt/timestamp.c

Arbitrary zaman toleransı büyütülmedi. Yerine Linux AF_UNIX SO_PEERCRED ile
socket listener PID/UID'si doğrudan pidfile ve mevcut kullanıcıya bağlanır; SQL
öncesi ve sonrası eşleşme zorunludur. Bu read-only kernel seçeneği listen/connect
anındaki kimliği verir: https://man7.org/linux/man-pages/man7/unix.7.html
Ek bağlantılar aynı görev Unix soketine gider, timeout 3 saniye, uygulama verisi
göndermez. SQL hâlâ explicit local/read-only/5 saniye timeout. Sonraki snapshot,
process tick/executable/UID/namespace, pidfile ve socket inode kontrolleri korunur.
Peer özelliği erişilemiyorsa veya PID/UID farklıysa SOCKET_PEER_CHECK ile fail
closed; SQL sonrası değişiklik SNAPSHOT_CHANGED. Hiçbir signal/start/stop yok.

START_TIME_CHECK artık yalnız pozitif olmayan SQL zamanını reddeder; parse ve
finite kontrolü SQL_RESULT_CHECK'te kalır. İki farklı wall-clock fazının farkı
bir process identity kriteri değildir. Bu kontrol drop değil, dolaylı zaman
varsayımının doğrudan kernel peer kimliğiyle değiştirilmesidir. MATCH yine
shutdown yetkisi veya launcher-parent ilişkisi kanıtı değildir.

61 test PASS: modelde gecikmiş/geri kaymış faz saatleri aynı kernel peer ile
false-reject olmaz; yanlış/değişen/erişilemeyen peer reddedilir. Gerçek yerel
PostgreSQL fixture'ında peer PID/UID eşleşmesi ve clean stop sonrası ret PASS.
Gerçek PostgreSQL'in yavaş startup gecikmesi veya Codespaces clock/PID sorunu
bu fixture'da yeniden üretilmiş sayılmaz. Kullanıcının eski START_TIME_CHECK
sonucu ilk kontrolleri geçtiğini gösterir, son snapshot'lar henüz çalışmamıştı.
Gerçek ilk launcher PID uyuşmazlığının nedeni hâlâ açık; owner retesti beklenir.

### İncelenen görev DB'si için açık graceful recovery komutu

851cd41 tabanından sonraki yardımcı, eski supervisor'ın bıraktığı çalışan
sentetik görev DB'sini yalnız ayrı ve açık komutla durdurmak içindir. Önceki
TASK_DB_IDENTITY_MATCH anlık kanıttır; bu yardımcı onu tekrar kullanmaz, işlem
anında bütün tanılamayı yeniden çalıştırır. Kullanıcı ortamında otomatik çalışmaz.

İnceleme/yayın ve HEAD doğrulamasından sonra önerilen tek recovery komutu:

```sh
python3 scripts/uat-stop-task-db.py --graceful
```

- Yalnız kaynakta sabit HOME görev dizini; arbitrary PID/path parametresi yok.
- Mevcut supervisor.lock inode'u üzerinde nonblocking exclusive flock gerekir;
  doluysa STOP_REFUSED_SUPERVISOR_BUSY. Dosya oluşturulmaz/silinmez/değiştirilmez.
- /proc/self NSpid == os.getpid kontrolü tanılamaya da eklendi. Hedef namespace,
  executable, UID, socket kernel peer, pidfile ve process start tick doğrulanır.
- Linux pidfd_open ile hedef pinlenir. Pin sonrası tam SQL/peer tanılaması ve
  snapshot tekrarları yapılır. PID yeniden kullanılırsa veya pin çıkmışsa ret.
- Yalnız signal.pidfd_send_signal ile SIGTERM (PostgreSQL smart shutdown).
  Sayısal PID kill, pg_ctl stop, SIGKILL/fast escalation veya adoption yok.
- En fazla 30 saniye process çıkışı beklenir. Timeout: STOP_PENDING_NO_ESCALATION;
  yeni launcher/retry/force önerilmez, durum ayrıca incelenir.
- Exit sonrası PostgreSQL'in kendi PID/socket dosyaları kalmışsa
  STOPPED_REQUIRES_INSPECTION; dosya silinmez. Diğer hatalar
  STOP_REFUSED_OR_UNCONFIRMED; Ctrl+C STOP_INTERRUPTED_STATE_UNKNOWN.
- TASK_DB_STOPPED yalnız pinlenmiş process çıktı ve kalan görev PID/socket
  dosyaları bulunmadıysa yazılır. Başarısız/pending çıktı sonrası launcher
  çalıştırılmamalı. TASK_DB_STOPPED sonrası normal launcher bir kez başlatılabilir;
  yeni hata varsa durulup yeni güvenli cause sınıfı incelenir.

Bu işlem DB servisini benimseyip supervisor'a bağlamaz; yeni oturum/credential,
DB reset, veri silme veya security ayarı yapmaz. Smart shutdown mevcut SQL
oturumlarının bitmesini bekleyebilir; bu yüzden pending sonucu mümkündür.
Önceki aa51 Python exception'ı kalıcı saklanmadığından yeni script geçmiş asıl
hatayı geri çıkaramaz. postgres.log yalnız kendi server olaylarını içerir,
launcher Python exception'ı için kesin kanıt değildir. Normal yeni başlangıçta
v7 hata koruması yürürlükte kalır; tanı için ikinci supervisor başlatılmaz.

72 startup/recovery testi PASS. Gerçek mevcut sentetik PostgreSQL fixture'ında
pidfd SIGTERM, normal çıkış, aynı kümenin restart'ı ve sentetik satır korunması
PASS. Yerel fixture sahibi yolunu test modülünde eşleştirmek dışında marker
config değiştirilmedi; yeni DB yok. Gerçek kullanıcı Codespace'i durdurulmadı.
İlk launcher PID mismatch root cause henüz açıklanmış veya yeniden üretilmiş
sayılmaz. Bu bounded recovery aracı otomatik ürün/deployment readiness değildir.

### Helper import bytecode düzeltmesi

Taban e374d84. Kullanıcının tek Git değişikliği `?? scripts/__pycache__/` idi.
Eski helper'ın importlib loader'ı uat-diagnose bytecode'unu üretip clean-source
guard'ını tetikliyordu; temiz geçici Git deposunda aynı durum tekrar üretildi.

Helper artık reviewed diagnostic kaynak baytlarını doğrudan compile/exec ile
module namespace'ine yükler: bu import için .pyc ne üretilir ne tüketilir.
Sadece `/scripts/__pycache__/uat-diagnose.cpython-*.pyc` eski generated cache
pattern'i ignore edilir. Tüm cache dizini, *.py veya diğer pyc dosyaları ignore
edilmez. Launcher clean-source guard kodu değişmedi. Mevcut cache silinmez.
Geçerli timestamp header'ı taşıyan hostile sentetik cache ile helper'ın onu
çalıştırmadığı test edildi. Tracked kaynak editi, untracked .py (cache dizini
altında da) ve başka module cache dosyası hâlâ guard'ı engeller.

76 test PASS; gerçek helper subprocess ve gerçek git status --porcelain
kullanıldı. DB/helper --graceful eylemi bu düzeltme testinde çağrılmadı.
İnceleme/yayın sonrası normal fast-forward pull ve `git status --short`
kontrolü yeterlidir; cache silme/reset/clean veya stop-helper tekrarı gerekmez.
Başka değişiklik görünürse korunup incelenmelidir. Kullanıcının durdurulmuş
DB'si bu çalışma sırasında başlatılmadı. Ürün/auth/lifecycle kodu genişletilmedi.

### Login öncesi 400 için sabit sınır tanılaması

557de811 tabanında owner log'u GET ROOT 200, GET CSRF 200, POST LOGIN 400
BEFORE_ENDPOINT gösterdi. Bu, Host/Origin/peer kontrolünün hangisinin reddettiğini
söylemez; mevcut log geçmiş Origin başlığını geri vermez. Hiçbir Origin değeri
varsayılmadı. Yeni tanılama kabul/ret davranışını değiştirmez.

Yalnız opt-in Development UAT sınır reddinde event 4811 şu sabit mesajı üretir:
POSEIDON_UAT_REJECT LOGIN <KOD>. KOD: PEER_NOT_LOOPBACK, HOST_NOT_ALLOWED,
ORIGIN_MISSING, ORIGIN_NULL, ORIGIN_LOCAL_HTTP_8080, ORIGIN_LOCAL_HTTPS_8080,
ORIGIN_MULTIPLE, ORIGIN_OTHER. Başka request path'leri yalnız OTHER_REQUEST
etiketlidir; yeni mesaj raw Host/Origin/path/query/body/cookie/token içermez.
Forwarded başlıklara yeni güven yok; canonical Origin ve CSRF reddi aynıdır.

İnceleme/yayın ve kontrollü restart sonrası login bir kez denenip şu okunabilir:

```sh
python3 scripts/uat-login-status.py
```

Betik yalnız son 128 KiB app.log içindeki allowlisted LOGIN kodunu yazar:
LOGIN_REJECT:<KOD>. Diğer çıktılar NO_LOGIN_REJECTION_OBSERVED veya
LOGIN_CHECK_UNAVAILABLE. Token/URL/log satırı kopyalanmaz. Log'daki son retin
eski denemeye ait olabileceği unutulmamalı; yeni launcher app.log'u yeniden açar.
Mevcut çalışan uygulama bu kodu restart olmadan yüklemez. Şimdilik gerçek owner
Origin bilinmiyor ve login sorununun çözüldüğü iddia edilmiyor.

35 ilgili backend testi PASS; 79 startup/parser testi PASS. Gerçek yerel Kestrel
fixture'ına kasıtlı http://localhost:8080 Origin verildiğinde ROOT200, LOGIN400
ve doğru sabit kod/parser PASS. Bu üretilmiş fixture gerçek Codespaces Origin
kanıtı değildir. DB bağlantısı kullanılmadı; fixture app durduruldu. Tam backend,
frontend ve browser matrix bu instrumentation-only turunda NOT_RERUN.

Parser sınır düzeltmesi: son128KiB okuması dosyanın ortasından başlıyorsa ilk
satır koşulsuz atlanır. Böylece kesilmiş başka bir satırın sonu geçerli ret koduna
dönüşmez. Offset tam satır sınırına gelse bile konservatif ilk satır atlanır;
sonraki tam kayıtlar korunur. Küçük dosyada offset0 ilk kayıt korunur. Bu yüzden
NO_LOGIN_REJECTION_OBSERVED kesin ret yokluğu kanıtı değildir. 82 Python testi
PASS; backend/HTTP davranışı bu parser-only düzeltmede değişmedi/yeniden koşulmadı.

### Cold-start readiness probe timeout düzeltmesi (29 Ekim)

Taban a27cb4f. Gerçek owner sonucu POSTGRES_START/SUBPROCESS_TIMEOUT ve cleanup
PID mismatch idi; sonradan diagnostic MATCH alındı. Kod sırası nedeniyle cleanup
izlenen child'a ulaşmışsa başlangıçtaki pg_ctl status geçmiştir. Bu örnekte
TimeoutExpired üretebilen sonraki bounded çağrı pg_isready'dir. pg_ctl burada
sadece status için kullanılır; gerçek child doğrudan postgres Popen'dır.
Postgres stdout'u dosyadır; pg_isready stdout capture pipe kullanmaz. Bu kaynak
incelemesi actual child PID farkının geçmişte neden oluştuğunu kanıtlamaz.

Resmî PostgreSQL 17 pg_isready varsayılan bağlantı timeout'u 3 saniyedir:
https://www.postgresql.org/docs/17/app-pg-isready.html
Eski launcher dışarıdan 2 saniyede kesip tek probe timeout'unu fatal sayıyordu.
Yeni akış explicit pg_isready -t1, en fazla2s/probe ve toplam30s monotonic
readiness deadline kullanır. Geçici probe TimeoutExpired, sınır içinde not-ready
olarak yeniden kontrol edilir. Timeout süreleri büyütülmedi; eski60-iteration
sayacı gerçek wall-clock deadline ile değiştirildi. Child exit/invalid probe
hâlâ fail closed. Ready yanıtı artık ayrıca owner/directory ve pidfile==retained
child PID kontrolünden geçer; başka ready server benimsenmez.

Cleanup pid mismatch reddi, signal mask/child handle/supervisor lock davranışı
korundu. Deadline biterse yalnız mevcut cleanup yolu çalışır; uyuşmazlık varsa
signal verilmez. Gerçek Codespaces child/PID farkı kök nedeni UNKNOWN kalır.

86 startup/lifecycle testi PASS. Mevcut task-owned PostgreSQL fixture'ı kısa süre
SIGSTOP/SIGCONT ile geciktirildi: eski2s probe TimeoutExpired verdi; yeni bounded
readiness aynı child için3 probe içinde geçti. Bunlar yalnız test-owned child'a
uygulandı; kullanıcı ortamında sinyal/restart yok. Fixture clean stop ile bitti,
yeni DB yok. Bu actual Codespaces stop/resume/PID mismatch reproduksiyonu değil.
Ürün/backend/frontend/browser kodu değişmedi; bu tur NOT_RERUN. Kullanıcının DB'si
hâlen çalışıyor olabileceğinden inceleme/yayın öncesi launcher tekrarı önerilmez.
