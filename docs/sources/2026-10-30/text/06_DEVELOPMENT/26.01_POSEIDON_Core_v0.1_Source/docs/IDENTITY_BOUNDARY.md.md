# IDENTITY_BOUNDARY.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/docs/IDENTITY_BOUNDARY.md
SHA256: b72fb8fd0583e4c3b3b8d3ddd6a18fee80ab2ad61c429e4a7a949d0125238731

## lines:1-40

# Kimlik sınırı — ilk güvenli dilim

Durum: geliştirme temeli; production-ready değildir. Taban:
`26c190ac3b86c1e4f3edc30b4553f2f5f6578d47`. Uygulama kurum altyapısında,
aynı HTTPS kökeninde tarayıcıdan çalışmayı hedefler. Kurum içi hesap ve varsa
AD/SSO federasyonu aynı OIDC sınırının arkasında sağlanabilir. Bu dilim sağlayıcı
seçmez, parola sistemi yazmaz, hesap/client/anahtar/sertifika üretmez veya kurmaz.

## Kimlik ve yetki sözleşmesi

- Kimlik yalnız protokol doğrulamasından geçmiş tam `iss` + `sub` çiftidir.
  Email/name eşleme anahtarı değildir. OIDC issuer değerleri tam eşleşir.
- `IIdentityGrantResolver` sunucunun yönettiği üyelik kaynağına bağlanacak porttur.
  Bir kimlik için tek aktif tenant/company ve bu tenant/company ile tutarlı gemi
  grant'leri döner; yok, boş, yinelenen veya tutarsız eşleme reddedilir. Çoklu kurum
  seçim akışı henüz yoktur. Tarayıcıdan scope/tenant/company seçimi kabul edilmez.
- BFF doğrulanmış IdP scope/role/email/name claim'lerini doğrudan yetki olarak
  kullanmaz. Yeni principal yalnız sunucu resolver sonucundan üretilir; her cookie
  isteğinde üyelik yeniden denetlenir. Resolver hatası veya iptal oturumu reddeder.
- Mevcut bearer API imza/issuer/audience/lifetime doğrulamasını ve imzalı
  tenant/company/vessel/permission claim sözleşmesini korur. Bu eski yol için
  claim yönetişimi gerçek IdP devreye alım kapısıdır; yeni resolver'a sessizce
  geçirilmedi. Geçersiz/yinelenen scope claim'leri artık açıkça reddedilir.
- Gerçek kimlik actor anahtarı `oidc:` + JSON kodlanmış issuer/subject çiftinin
  SHA-256 değeridir; iki issuer'ın aynı subject'i çakışmaz. Development sentetik
  kimliklerinin mevcut actor anahtarları korunur. Daha önce gerçek bearer subject
  ile kayıt üretilmişse owner/audit/idempotency geçişi ayrıca planlanmalıdır;
  otomatik veri migration'ı yapılmaz. Bu davranış değişikliği bilinçlidir.

## Opt-in kapıları

Varsayılan `Bff:Enabled=false`: handler ve BFF endpoint'leri kaydedilmez.
Mevcut bearer ve açık Development sentetik kullanım aynı kalır.
Enabled için `Bff:Authority`, tam `Bff:Issuer`, `Bff:ClientId`, `Bff:ClientSecret`,
`Bff:Origin` gerekir. Authority/issuer HTTPS; origin yalnız HTTPS kökeni olmalıdır.
Client ID API `Authentication:Audience` ile aynı olamaz. Sentetik modla birleştirme
reddedilir. Gizli değerleri kaynak dosyalarına koymayın. Bu belgede çalışır gerçek
client/credential örneği yoktur. Sertifika/client assertion adaptörü henüz yoktur.

Ayrıca host composition şu bağımlılıkları açıkça sağlamalıdır:

## lines:41-80

1. `IIdentityGrantResolver`: kurumun yönettiği yetki kaynağı.
2. `IBffInfrastructure.Tickets`: `ITicketStore` uygulaması. Yüksek entropili handle,
   sunucuda expiry/revocation, korunmuş veri, çok düğüm tutarlılığı ve cleanup gerekir.
3. `IBffInfrastructure.DataProtection`: önceden kurulmuş, korunmuş key yönetimi.
   Kaynakta production implementation veya otomatik key provision yoktur.
   Default Data Protection provider BFF açıldığında kullanılmaz.

Bunlar sağlanmadan başlangıç hata verir. `IsEphemeral=true` yalnız `Testing`
ortamında kabul edilir; Production veya Development fallback yoktur. Test store ve
`EphemeralDataProtectionProvider` yalnız test projesindedir. `Testing` bir üretim
ortam etiketi olarak kullanılmamalıdır; uygulama kaynak ağacında bu store yoktur.
Bu kapılar operatör implementasyonunun güvenlik incelemesinin yerine geçmez.

## HTTP sözleşmesi

Tüm `/bff` talepleri yapılandırılmış aynı HTTPS kökenine ait host üzerinden gelmeli;
aksi 400. Reverse proxy güven politikası ve HTTPS termination kurulumu ayrıca gerekir;
bu dilim forwarded-header/ağ ayarları eklemez. Yanıtlar no-store/no-referrer'dır.

| Yol | Davranış |
| --- | --- |
| GET `/bff/antiforgery` | CSRF request token döner; Secure/HttpOnly antiforgery cookie üretir. Giriş sonrası yeni token alınmalıdır. |
| POST `/bff/login?returnUrl=/...` | CSRF zorunlu, yalnız dar yerel returnUrl; Code+PKCE challenge. Varsayılan `/`. |
| `/bff/callback` | ASP.NET Core OIDC handler; state/correlation/nonce/token validation framework tarafından işlenir. Genel uygulama POST endpoint'i değildir. Geçersiz state 401. |
| GET `/bff/session` | Yalnız BFF cookie şeması; güncel sunucu eşlemesi ve oturum scope'u. |
| POST `/bff/logout` | Kimlik ve CSRF zorunlu; yerel ticket silinir, cookie kaldırılır; 204. |
| GET `/bff/logout` | Logout yapmaz; endpoint yok. |

State-changing cookie endpoint'lerinde `X-CSRF-TOKEN` zorunludur; SameSite tek
savunma değildir. Callback kendi OIDC state/correlation/nonce korumasını kullanır.
Kullanıcı alanı/proxy endpoint'leri eklendiğinde ayrı cookie policy ve CSRF kontrolü
zorunludur. Bu dilim `/api/v1/*` API yetkisini cookie'ye genişletmez.

Session cookie `__Host-Poseidon.Session`: Secure, HttpOnly, Path=/, SameSite=Strict,
20 dakika, sliding renewal kapalı. İçinde framework'ün koruduğu opaque ticket
referansı bulunur; principal server store'dadır. OIDC correlation/nonce cookie'leri
geçici protokol cookie'leridir; bunlar da Secure/HttpOnly'dir.
`SaveTokens=false`: access/refresh/ID token ticket veya tarayıcı cookie'sine
kaydedilmez. Token exchange sırasında token'lar handler belleğinde işlenir.

## lines:81-120

`IBffTokenStore` ilerideki server token saklama/silme sözleşmesidir; bu dilimde
uygulaması ve çağrısı yoktur. ID token API'ye access token olarak gönderilmez.

## Doğrulama ve açık kapılar

2026-10-27, mevcut .NET SDK 10.0.403 / ASP.NET Core 10.0.14 ile locked restore ve
build başarılı (0 uyarı, 0 hata). **86 passed / 0 failed / 0 skipped**. Özgün 37
backend testi ve gerçek PostgreSQL kullanan üç suite korunarak çalıştırıldı.
İlk denemede durmuş yerel PostgreSQL nedeniyle 3 bağlantı hatası vardı; mevcut
küme aynı Unix socket/port ayarlarıyla başlatıldıktan sonra tamamı geçti. Yeniden
kurulum, yeni DB/rol/hesap, listener ayarı değişikliği yapılmadı.

Testler: bearer JWT yanlış issuer/audience/imza/expiry; Production sentetik reddi;
server grant yokluğu/yanlış identity/tenant/company/vessel; güvenli config kapıları;
local returnUrl; Code+PKCE challenge; sahte callback state reddi; CSRF eksikliği ve
başka tarayıcı token'ı; opaque session storage zorunluluğu; cookie bayrakları;
cookie'nin bearer API'ye yetki vermemesi; yerel logout/ticket silme; revocation,
expiry ve tampered cookie; HTTP veya yanlış host reddi. Anahtarlar process-only.

- **NOT_RUN:** Gerçek dış IdP ile authorization callback, token redemption,
  issuer/audience/nonce birlikte protokol entegrasyonu, gerçek discovery/JWKS rotation,
  AD federasyonu, yerel kurum hesabı, gerçek tarayıcı E2E ve proxy/HTTPS deployment.
- **NOT_IMPLEMENTED:** Refresh, token store adaptörü, downstream BFF API proxy,
  IdP RP-initiated/front/back-channel logout, çoklu tenant seçim akışı, client
  assertion/certificate adaptörü. Yerel logout IdP oturumunu kapatmaz.
- **AYRI ONAY/KURULUM:** IdP seçimi/kurulumu, client kaydı, gerçek sır/sertifika,
  korunmuş persisted Data Protection keys, production ticket/üyelik store'ları,
  kullanıcı provisioning, güvenlik incelemesi, migration ve deployment.

## Resmî tasarım kaynakları

- [Microsoft ASP.NET Core OIDC/BFF](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0): confidential Code+PKCE ve BFF yönü.
- [Microsoft antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0): cookie state değişikliklerinde CSRF doğrulaması.
- [Microsoft SessionStore](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.cookies.cookieauthenticationoptions.sessionstore?view=aspnetcore-10.0): cookie içinde kimlik yerine session identifier.
- [OIDC Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html): issuer/subject kimliği ve ID token doğrulaması.
- [OAuth Security BCP, RFC 9700](https://www.rfc-editor.org/rfc/rfc9700.html): authorization code, PKCE ve redirect sınırları.

## Entegrasyon incelemesi: callback regresyonu

İlk 82 test yalnız doğrudan fixture cookie üretimini kapsadığı için P1 hatasını

## lines:121-141

kaçırıyordu: ASP.NET Core 10.0.14 varsayılan claim actions, `OnTokenValidated`
sonrasında eşlenmiş principal'dan `iss` claim'ini siliyordu. Callback redirect'i
başarılıydı ancak sonraki `/bff/session` 401 dönüp oturumu iptal ediyordu.

Yeni test gerçek OIDC handler'ı, ephemeral RSA imzalı ID token ve yalnız process
içindeki sahte token endpoint'i ile çalıştırır. Code redemption ve PKCE verifier,
state/correlation/nonce, imza/issuer/audience doğrulaması, cookie yazımı ve sonraki
session isteği atlanmaz. Düzeltme öncesinde valid senaryosu tam session adımında
401 ile başarısız oldu; üç negatif senaryo geçti. `ClaimActions.Clear()` sonrası
86 testin tamamı geçti. Principal zaten server resolver tarafından allowlist
edildiği için sonradan IdP claim ekleme/silme işlemi yapılmamalıdır. Store içindeki
issuer korunması, token saklanmaması ve dış email/admin claim'lerinin taşınmaması
da denetlenir. Callback replay aynı kodu yeniden redeem edemez.

Bu test canlı sağlayıcı uyumluluğu, keşif/JWKS taşıması veya production tarayıcı
girişi değildir. İlk Library arşivindeki kod bu P1 düzeltmesini içermez; birleşik
teslim bu arşivin yerini alır.

Framework kanıtı:
[10.0.14 options](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectOptions.cs),
[10.0.14 handler](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs).
