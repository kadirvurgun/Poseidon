# BFF_UI_BOUNDARY.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/docs/BFF_UI_BOUNDARY.md
SHA256: b2ee6aa72dcabeaa450b382f9a5af0e5e7a767e8b2af2fc73eac55469b73439a

## lines:1-40

# BFF tarayıcı oturumu — ikinci güvenli dilim

Taban: `ad4407639950d2af4440bbd3650c20142854d208`.
Bu belge önceki [kimlik sınırını](IDENTITY_BOUNDARY.md) genişletir; eski NOT_IMPLEMENTED
listesindeki frontend wiring ve uygulama-içi BFF taslak rotaları bu dilimde uygulanmıştır.
Gerçek IdP kurulumu, refresh, harici downstream API proxy, federasyon logout ve production
kabulü uygulanmış veya doğrulanmış değildir.

## Opt-in ve rota sınırı

`VITE_ENABLE_BFF=true` derleme seçeneği React'in kurum hesabıyla giriş görünümünü açar.
Varsayılan false; `.env.example` bunu açıkça belirtir. Bu modda manuel bearer token alanı
ve sentetik oturum seçimi gizlenir. Sunucu tarafında bağımsız `Bff:Enabled` ve önceki
altyapı kapıları geçerli kalır. Server kapalı veya yanlış yapılandırılmışsa istemci bearer/
sentetik moda düşmez. Uygulama ve `/bff/*` aynı HTTPS kökeninde sunulmalıdır; bu dilim
proxy, HTTPS termination, IdP/client kaydı veya güvenlik ayarı kurmaz. Vite Development
proxy'sine HTTP BFF geçidi eklenmedi. Varsayılan bearer/sentetik geliştirme yolu korunur.

- `/api/v1/*`: mevcut bearer/sentetik API, cookie ile yetkilendirilmez.
- `/bff/api/v1/vessels` ve `/bff/api/v1/certificates`: aynı domain store/validation,
  ayrı cookie policy. List/detail/create/update ve kapalı submit sözleşmeleri aynı.
  Write permission, tenant/company/vessel denetimi, idempotency, optimistic version,
  audit ve DRAFT-only davranışı ortak `DraftEndpoints` üzerinden korunur.
- BFF taslak POST'ları `X-CSRF-TOKEN` gerektirir. GET dışı tüm grup endpoint'leri
  antiforgery filtresinden geçer. Header sadece form token'ıdır; kimlik credential'ı değildir.
- Tüm BFF taslak talepleri `X-Poseidon-Scope` önkoşulu gerektirir: session yanıtındaki
  `subject:tenantId:companyId`. Sunucu bunu kendi doğrulanmış güncel scope'u ile karşılaştırır.
  Eksik/farklı değer 401 olur. Bu açık bilgi yetki vermez; başka sekmede hesap/kapsam
  değişince eski ekrandan işlem yapılmasını engelleyen concurrency önkoşuludur. Her istekte
  server resolver ve domain erişim kontrolleri ayrıca uygulanır.

## Giriş, süre dolması ve çıkış

İlk render'da `/bff/session` bir kez denetlenir. 401 giriş ekranında kalır; otomatik
challenge/redirect yoktur. Kullanıcı login seçtiğinde `/bff/antiforgery` alınır ve sabit
POST formu `/bff/login?returnUrl=...` adresine tam sayfa navigation başlatır. Dönüş adresi
sadece `/?lang=tr` veya `/?lang=en`; URL'deki kullanıcı returnUrl değeri kullanılmaz.
Formda yalnız `__RequestVerificationToken` taşınır. Callback başarısızlığında handler
401 ve sabit TR/EN geri dönüş bağlantısı verir; protokol ayrıntısı yansıtılmaz, döngü yoktur.

## lines:41-80

Browser auth token'ı almaz/saklamaz; JS sadece session scope ve CSRF değerini görür.
Session cookie Secure/HttpOnly ve server-side ticket referansıdır. Draft/client state
React belleğindedir; localStorage/sessionStorage veya URL'ye yazılmaz. BFF API fetch'leri
same-origin credentials ve redirect:error kullanır. Token exchange sadece sunucudadır.

Oturum sayfa tekrar odaklandığında/görünür olduğunda ve işlemler sırasında denetlenir.
401 veya hesap/kapsam değişiminde client invalid olur, pending retry temizlenir,
yazma/dolaşım durur; düzenlemeler bu sayfada görünür kalır. Yeniden giriş ya da logout,
kirli düzenlemelerde onay ister. İptal değerleri korur; login hazırlığı veya logout ağ
hatası değerleri silmez. Onaylı tam sayfa yeniden giriş mevcut belleği bırakır; otomatik
restore veya yazma tekrarı yoktur. Logout CSRF'li POST'tur, başarı/401 sonrası yerel
veriler temizlenir; başarısız logout başarılıymış gibi sunulmaz. Sunucudaki cookie
iptali sonrası eski cookie tekrar kullanılamaz. Yerel logout IdP oturumunu kapatmaz.

## Doğrulama katmanları

- Mevcut 86 backend test korunur. Yeni cookie-route/CSRF/precondition kontrolleri,
  gerçek PostgreSQL ile callback→taslaklar→scope/version/idempotency→logout/replay,
  ve gerçek Chromium testi eklenmiştir.
- Mevcut 35 frontend test korunur. BFF opt-in, oturum restore, TR/EN explicit login,
  iptal edilmiş dirty navigation/logout, başarısız logout, expiry dondurma, tekrar login
  iptali, visibility check, login hazırlık hatası, token-free transport, scope değişimi,
  sabit returnUrl testleri vardır.
- Mevcut altı viewport/dil matrisi sentetik Development yolu üzerinde aynen çalıştırılır.
- Yeni BFF browser testi gerçek Chromium + derlenmiş React + ASP.NET TestServer + gerçek
  disposable PostgreSQL kullanır. Token endpoint'i in-process fake handler ve RSA anahtarı
  process-only'dir. Login/nonce/state/PKCE/token validation/claim mapping/cookie/store ve
  domain endpoint'leri atlanmaz. Browser test host'u fixture signin endpoint'ini kullanmaz.
- Playwright tüm HTTPS isteklerini stdio köprüsüyle TestServer'a yönlendirir. Fulfilled
  redirect zinciri tekrar route edilmediği için gerçek handler Location cevabı kontrollü
  yeni navigation ile relay edilir. Dolayısıyla native HTTP redirect zinciri, TLS,
  reverse-proxy, gerçek discovery/JWKS veya dış IdP uyumluluğu **NOT_RUN**'dır. Gerçek
  callback handler davranışı ile gerçek browser UI/cookie davranışı test edilir.

Son sayılar ve hash'ler teslimdeki `result.json`/test kanıtlarıdır. Entegre browser testi
çalıştırılmazsa açıkça SKIPPED sayılır, başarı sayılmaz. Test-only geçici altyapı ve browser
köprüsü test projesindedir; runtime fallback yoktur. Test browserı mevcut Playwright /
Chromium kullanır, framework veya browser kurulumu yapmaz. Ekran görüntüsü teslim edilmez.

```sh

## lines:81-105

# Mevcut SDK ve dedicated test DB ortamı ile; gerçek secret değerlerini kaynakta tutmayın.
VITE_ENABLE_BFF=true npm --prefix src/Frontend run build -- --outDir /tmp/poseidon-bff-test-dist
POSEIDON_BFF_BROWSER_DIST=/tmp/poseidon-bff-test-dist dotnet test tests/Backend/Poseidon.Backend.Tests.csproj
# İsteğe bağlı JSON: POSEIDON_BFF_BROWSER_REPORT; mevcut kurulumlar: PLAYWRIGHT_MODULE, CHROMIUM_PATH.
```

Açık kapılar: canlı IdP/client/credential, persisted key/ticket store, provisioning,
refresh, federasyon logout, production hosting, native TLS/redirect transport, gerçek
mobil/ekran okuyucu/diğer tarayıcılar. Önceki gerçek actor kayıtlarının issuer+subject
geçişi ayrı inceleme ister. Push/commit/merge/deployment bu dilimde yoktur.

## İnceleme düzeltmesi: sekmeler arasında logout sınırı

Logout da gösterilen hesabın `X-Poseidon-Scope` önkoşulunu ve geçerli CSRF'yi gerektirir.
React yeni bir client oluşturmaz; gösterilen session'ı doğrulamış mevcut BffApi kullanılır.
CSRF beklenirken cookie A'dan B'ye değişirse eski A scope'u yeniden yazılmaz. Sunucu
mismatch veya eksik scope için `409 logout_scope_changed` döner; B ticket'ına/cookie'sine
sign-out uygulanmaz. İstemci eski client'ı invalid eder, dirty veriyi korur, yazmayı durdurur
ve TR/EN olarak çıkış yapılmadığını bildirir. Zaten invalid client'ın sonraki yerel çıkış
seçimi server'a logout yollamaz; yalnız eski ekrandaki veriyi temizler.

Yeni sunucu testleri subject değişimi, aynı subject/company değişimi ve eksik önkoşulu
kapsar; geçerli B CSRF ile reddedilen A logout sonrası B session'ı ve ticket sayısı korunur.
B kendi scope'u ile logout yapabilir. İstemci testinde CSRF promise'i geciktirilip A→B
geçişi simüle edilir; gönderilen kapsam A olarak kalır, B kapanmaz ve otomatik yazma yoktur.
