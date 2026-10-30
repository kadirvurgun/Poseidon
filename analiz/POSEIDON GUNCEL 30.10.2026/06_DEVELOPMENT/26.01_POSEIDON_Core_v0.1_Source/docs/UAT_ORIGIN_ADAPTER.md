# Sentetik UAT: proxy tarafından dönüştürülen Origin

29 Ekim 2026; taban 1e904a1dfb0f964e20477a65ad4dc7facbae188f.

Gerçek kullanıcı tanısı LOGIN_REJECT:ORIGIN_LOCAL_HTTPS_8080 döndürdü.
Bu gözlem login isteğinin alınan Origin sınıfını belirler; proxy kimliğini kanıtlamaz.
Önceki Host gözlemi localhost:8080 idi. Canonical uygulama adresi açıkça yapılandırılan
HTTPS Codespaces adresi olarak kalır.

## Dar uygulama kararı

Yalnız açık Development UAT modunda, Codespaces origin yapılandırılmışken,
loopback eşinden ve tam localhost:8080 Host ile gelen isteğin Origin değeri
tam https://localhost:8080 ise, X-Poseidon-Uat-Origin başlığının tek değer olarak
yapılandırılmış canonical origin ile birebir eşleşmesi gerekir. Başlık frontend
tarafından window.location.origin üzerinden üretilir; URL sorgusu, kullanıcı
girdisi veya Forwarded/X-Forwarded-* kaynağı kullanılmaz. Diğer/null/eksik Origin
reddedilir; HTTP localhost, başka port/host veya birden fazla değer kabul edilmez.
Mevcut canonical Origin yolu korunur. Gönderilen özel başlık yanlışsa o yol da reddeder.

Bu başlık proxy doğrulaması veya kimlik bilgisi değildir. Güvenlik katmanları:
- Cross-origin HTML formu bu özel başlığı gönderemez.
- Cross-origin JavaScript bu non-safelisted başlık için CORS preflight gerektirir;
  uygulama CORS izni vermez. Aynı-origin UAT login fetch açık mode:same-origin kullanır.
- Login dahil bütün BFF POST işlemlerinde mevcut ASP.NET antiforgery cookie/token
  çifti ayrıca doğrulanır. Başlık tek başına token yerine geçmez.
- Oturum sonrası yetkiler ve scope stamp, işlem bariyeri, idempotency, logout
  ve hesap değişimi korumaları korunur.
- Aynı kullanıcı yetkileriyle yerel kod çalıştırabilen veya uygulama origin'inde
  XSS elde eden saldırgan bu başlık kontrolünün koruduğu cross-site tehdit modeli
  dışındadır; değişiklik onlara karşı yeni bir güvenlik iddiası üretmez.

Sadece VITE_ENABLE_UAT=true derlemesinde login form yerine fetch kullanır.
CSRF ve origin başlıkları gönderilir, redirect:error ve credentials:same-origin
kullanılır. Sunucu başarıda 204 döndürür, token/URL döndürmez. İstemci yalnız
204 sonrasında sabit /?lang=tr veya /?lang=en adresine gider. Hata/yanıt kaybında
otomatik login tekrarı yoktur. Gerçek OIDC üst-seviye form/challenge akışı değişmez.
UAT BFF metadata/logout POST'ları da browser origin başlığını taşır; bearer
istemcileri taşımaz. Üretim, default-off ve özel port/owner kontrolleri değişmez.

## Doğrulama ve sınırlar

Sonuçlar teslim arşivindeki results.json ve test loglarındadır. Yerel Chromium
matrisi canonical HTTPS adresini emüle edip gerçek loopback Kestrel'e Host ve
Origin dönüşümüyle iletir. Native TLS, gerçek Codespaces proxy ve Windows/Android
donanımı testi değildir. Harness --no-sandbox kullanır; tarayıcı sandbox güvenliği
kanıtlanmış sayılmaz. Gerçek Codespaces'te özel başlığın korunması ve login sonrası
akış kullanıcı yeniden testi bekler. Özel başlık silinirse sistem kapalı kalır;
yerel Origin'i koşulsuz kabul eden fallback yoktur.

Yeni paket/altyapı, proxy ayarı, CORS izni, gerçek hesap, token, dış bağlantı veya
public port eklenmedi. Yasaklı Docker/NuGet uçları yeniden denenmedi. Testler
kurulu araçlar ve mevcut bağımlılık önbelleğiyle çalıştırıldı; clean container
startup kanıtı değildir. Evidence kapalı, gerçek IdP ve kullanıcı kabulü açıktır.

## Kaynaklar

- WHATWG Fetch Standard, CORS preflight ve unsafe request header kuralları:
  https://fetch.spec.whatwg.org/#cors-preflight-fetch
- GitHub Codespaces port forwarding ve private erişim:
  https://docs.github.com/en/codespaces/developing-in-a-codespace/forwarding-ports-in-your-codespace
- Microsoft dev-tunnels issue 284, benzer Origin rewrite gözlemi:
  https://github.com/microsoft/dev-tunnels/issues/284
  Bu issue Codespaces'in tüm sürümleri için garanti değildir; bu düzeltmenin
  tam eşleşmesi kullanıcının gerçek tanısına dayanır. Issue'daki tunnel ayarı uygulanmadı.

İnceleme ve yayın sonrasında mevcut güvenli stop/update/start yönergesi kullanılır.
Bu geliştirme görevi kullanıcı runtime'ını durdurmaz veya yeniden başlatmaz.
