# Güvenlik, audit ve kişisel veri başlangıç sözleşmesi

Durum: zorunlu mimari gereksinimler; mevcut OIDC/cookie, server membership, HTTP security/read audit ve EF write audit uygulanmıştır. Üretim IAM rollout, tam audit kapsamı ve hukuki uyum kabulü henüz tamamlanmadı.

## Kimlik ve parola

- Mevcut web akışı ASP.NET Core OpenIdConnect ile OIDC code+PKCE ve güvenli cookie oturumu kullanır; üretim IdP seçimi/rollout açıktır. Login/reset/MFA/session akışları özel kriptografiyle yazılmaz.
- Yerel parola tutulursa yalnız versioned ve salted Identity PasswordHasher çıktısı saklanır. Düz metin, geri çözülebilir şifreleme veya doğrudan SHA/MD5 parola saklama yöntemi değildir.
- Hash bileşeni hazırdır; yerel Identity kullanıcı tabloları yoktur. Gerçek OIDC login/logout/session endpoint'leri ve güvenli cookie oturumu foundation paketinde uygulanmıştır; üretim IdP/MFA kararı açıktır. Hash biçimi eski kaldığında `SuccessRehashNeeded` sonucu girişte yeni hash'e geçiş gerektirir.
- Girişte brute-force/rate limit, hesap kilitleme, güvenli reset token'ı, admin için MFA ve session iptali gerekir. Web'de mevcut güvenli cookie/OIDC kararı foundation sözleşmesinde kayıtlıdır; üretim rollout ve session revocation kapıları açıktır; bearer token localStorage'a gelişigüzel yazılmaz.
- Seed verisinde gerçek kullanıcı, parola/hash, sabit admin parolası veya erişim token'ı bulunmaz. Demo hesap gerekiyorsa kontrollü bootstrap ve ortam secret store'u kullanılır.

## Her kullanıcının işlem izi

İş olayları audit, HTTP erişim olayları access/security log, çalışma hataları diagnostic log olarak tutulur. Ortak correlation/trace ID ile ilişkilendirilir.

| Olay | Kaydedilecek bağlam | Uygulama noktası |
|---|---|---|
| Login/logout, başarısız giriş, MFA/reset | Sonuç, aktör/anonim bağlam, güvenli correlation, zaman | Mevcut OIDC login/logout/session handler + SecurityAudit; IdP MFA/reset ve üretim rollout açık |
| Liste/detay görüntüleme, indirme, arama, dışa aktarma | Yetkili aktör, tenant, kaynak türü/kimliği, eylem, sonuç | Mevcut company/vessel/profile/workspace read/search handler + SecurityAudit; indirme/export ve tam ürün kapsamı açık |
| Create/update/delete | Aktör, tenant, entity/kimlik, eylem, UTC, correlation | Mevcut EF SaveChanges audit'i; business write ve audit aynı transaction'da |
| Yetki reddi, başarısız deneme | Aktör ve scope, eylem, hata sınıfı/sonuç | Mevcut bağımsız SecurityAudit writer + authorization result handler; business rollback dışında; üretim dayanıklılığı/export açık |
| Onay, publish, release, kritik karar | Yetki/rol, gerekçe, rule/evidence sürümü, karar sonucu | Domain handler ve server-side yetki; henüz kurulmadı |
| Seed/import ve background iş | Ayrı sistem aktörü, job/dataset sürümü, sonuç | Seed import `system:seed-import` aktörüyle kayıt üretir |

İlk EF audit'i entity adı/kimliği ve işlem metadatasını yazar; raw before/after snapshot yazmaz. İleride alan bazlı before/after gerekiyorsa yalnız izinli alanlar redaction ile eklenir. Parola/hash, cookie, Authorization header, token, DB connection string, hassas sağlık verisi ve tam request/response body audit/diagnostic log'a girmez. Arama metni ve URL query string'i kişisel veri taşıyabileceğinden varsayılan olarak kaydedilmez.

Audit event EF üzerinden elle eklenemez/değiştirilemez/silinemez. PostgreSQL migration'ı UPDATE/DELETE/TRUNCATE engeli koyar. Ayrı schema kullanılır. Üretimde application ve migrator DB rolleri ayrılmalı; tablo sahibi/superuser uygulama rolü olarak kullanılmamalı. DB yöneticisine karşı mutlak değişmezlik iddia edilmez; ayrı append-only depoya aktarım, erişim denetimi ve kurcalama tespiti release gereksinimidir.

Mevcut API tenant kimliği ve aktörü doğrulanmış issuer/subject ve sunucunun provision ettiği membership üzerinden üretir; istemcinin gönderdiği actor/tenant değeri güvenilir kabul edilmez. EF query filter tek başına tam güvenlik sınırı sayılmaz: backend policy, yazma kontrolü, tenant içeren ilişkisel FK ve gerçek negatif testler birlikte gerekir. Admin import context'i yalnız ayrı migrator işleminde kullanılır, normal web isteğine sunulmaz.

## GDPR / KVKK ve gizlilik

Ürünün uygulanacak mevzuatı; müşteri, kişisel veri kapsamı, veri lokasyonu, tarafların rolleri ve aktarım modeline göre netleştirilir. GDPR veya KVKK uyumu yalnız yazılım ayarıyla ilan edilmez; yetkili değerlendirme gerekir.

- Veri envanteri: hangi alan, hangi amaç, hukuki dayanak, veri sahibi, erişen rol, kaynak, ülke/aktarım, saklama ve silme politikası.
- Privacy by design/default: asgari veri, en dar görünürlük, tenant/vessel/job izolasyonu, crew/sağlık/protected-report için ayrı projection ve alan bazlı maskeleme.
- TLS, DB/object storage ve yedeklerde şifreleme, secret store, anahtar rotasyonu ve ayrı ortam erişimleri.
- Saklama süreleri veri sınıfına göre kontrollü policy'dir; tüm audit kayıtlarına otomatik “süresiz tut” uygulanmaz. Hukuki saklama/legal hold ile veri sahibi silme talepleri uzlaştırılır; gerekli yerde kimlik eşlemesi anonimleştirilir veya erişimi kısıtlanır.
- Erişim/düzeltme/taşınabilirlik/silme talepleri yetkili iş akışından geçer; DB, object storage, cache, arama/embedding index ve yedek geri yükleme süreçlerini kapsar.
- Loglar da kişisel veri taşıyabilir: erişim kontrolü, purpose limitation, minimizasyon, saklama ve export denetimi uygulanır. IP/device bilgisi yalnız gerekçelendirilmiş kullanım ve süreyle tutulur.
- Veri işleyen/sorumlu rolleri, DPA/alt işleyen envanteri, uluslararası aktarım ve gerekiyorsa DPIA değerlendirilir. AI/embedding sağlayıcısına gerçek müşteri verisi varsayılan olarak gönderilmez.
- Olay/ihlal yönetimi bildirim zamanlarını ve yetkili kişileri kapsamalı; DR, restore ve güvenlik tatbikatları kanıtlanmalı.

## Faz kabulünde zorunlu testler

Cross-tenant/role/vessel erişim reddi; actor spoofing; audit ile business transaction tutarlılığı; audit mutasyon engeli; yanlış parola ve hash doğrulama; loglarda secret/PII sızıntısı; retention/legal-hold ve export yetkisi. Ürün geliştikçe ilgili testler eklenmeden iş tamamlandı sayılmaz.

Resmi kaynaklar:

- [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- [GDPR resmî metni — özellikle madde 5, 17, 25, 30, 32–35](https://eur-lex.europa.eu/eli/reg/2016/679/oj/eng)
- KVKK uygulanabilirlik ve operasyonel gereksinimleri ayrıca ilgili resmi kaynaklarla değerlendirilmelidir.

## DEV-OIDC-LAB-01 sınırı

Yerel Docker Keycloak fixture üretim IdP kararı değildir. Yalnız Development ve açık localhost authority için özel CA chain trust uygulanır; hostname/expiry/EKU doğrulaması, HTTPS metadata ve imza/issuer/audience/nonce/state/correlation/CSRF korunur. Sistem root store değişmez. Güvenli yerel bootstrap ve sensitive artifact yasağı [görev sözleşmesinde](tasks/dev-oidc-lab-01.md). Bu fixture tüm HTTP audit kapsamı, MFA, GDPR/KVKK veya production kabulü iddiası oluşturmaz.
