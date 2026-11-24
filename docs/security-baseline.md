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

## AUDIT-TRACE-01 authored extension — execution DEFERRED

Explicit server-provisioned audit.read plus actual tenant/vessel scope gates the new action history. Catalog history additionally requires regulatory.draft.read and tenant-wide scope. Scope precedes counts/paging/detail and exact immutable revision resolution. SecurityEvents remain separately restricted with no viewer; no audit permission implies log/export access. Safe new summaries withhold text/body values and retain allowlisted measurements/type/FK/revision references, trusted capability context, original xmin and the committed xmin observed at receipt save. Historical missing scope/role/reason/value/version data stays unavailable. Ordinary current preparation commands do not fabricate a safety-decision reason or approved role. No raw request/entity snapshot is added. This is not full HTTP audit, privacy/retention or GDPR/KVKK acceptance. [Coverage, exclusions and deferred tests](tasks/audit-trace-01.md).

## SECURE-FILES-01 authoring — execution DEFERRED

Explicit file.read/file.write and real vessel scope gate staged transport. Production storage is unavailable until a provider, credentials/reference, scanner and retention/operator policy are selected. The local adapter is Development-only with a dedicated exclusive root and real loopback clamd; configuration is not a successful scan. Accepted transport is not verified evidence. Capabilities bind actor/tenant/vessel/object/revision/expiry and access re-resolves trusted membership; bytes never use caller paths/URLs. Audit captures only safe transport state/name-withheld/revision metadata. Separate nullable trusted operator role references do not derive authority from capabilities or IdP labels. Actual production role catalog/assignments remain missing; no100% critical-write/GDPR acceptance. Repository and actual built-client pattern scanning plus fail-closed required inputs are authored, not run. [Scope, secret references, tests and real deployment gaps](tasks/secure-files-01.md).


## OFFLINE-APPLICATION-01 — authored, not security acceptance

Actual configurable Draft watch_group consumer now binds server-owned tenant/actor/vessel/object/device/noncredential context and source/policy fingerprint; missing policy denies capture/replay. Nonextractable AES256-GCM IndexedDB records and separate P256 enrollment/replay proof have explicit renew/revoke/loss/cleanup, quotas and retained-data controls. Current ordinary write rights precede receipt/conflict/history lookup; ordinary domain handler, receipt/conflict and scoped EF audit share a transaction. Closed audit summaries exclude values/reason/hash/key/proof; current-author conflict reason is separately protected. Static shell caching excludes API/provider/file/user data. This is neither XSS immunity, hardware attestation, instant disconnected revocation, approved production policy nor full HTTP/user-action/GDPR/DEV-011 acceptance. All security/model/browser/generation/QA execution remains DEFERRED. [Exact boundaries](tasks/offline-application-01.md).

## USER-ADMIN-01 — runtime membership administration (authored; targeted tests run, see phase report)

Tenant administrators (`membership.manage` + tenant-wide manage + role context) provision database memberships for IdP
identities (issuer+subject only; accounts, passwords and MFA stay in the IdP). Operator configuration keeps precedence for
its identities (bootstrap/break-glass). Grants are immutable revisions bounded by the granting administrator's own
authority; no self-modification; last-admin guarded; decision-authority permissions never bundled and appointments never
created. The login lookup is the single cross-tenant read, keyed only by the IdP-validated issuer+subject; the tenant is
then carried in the protected session and every request revalidates the current grant. Audit records closed bundle
codes/counts/flags and withholds permission lists and subjects. This is not production IAM rollout, IdP governance or
GDPR/KVKK acceptance. [Decisions](tasks/USER-ADMIN-01.md).

## USER-ADMIN-02 — invitation + acceptance by the identity holder (authored; targeted tests run, see phase report)

Closes USER-ADMIN-01 N1 (cross-tenant identity squatting). An admin-created membership starts `Invited`, grants nothing and
reserves no identity: its account has no subject until the identity holder, signed in through the trusted IdP in a
short (≤10 min) invitation-only session (link token in the URL fragment, `Referrer-Policy: no-referrer`) that resolves to no actor, accepts a single-use 256-bit token. Only the SHA-256 of
the token secret is stored; the token is returned once to the creating administrator and is never logged or audited.
Uniqueness covers accepted bindings only (filtered unique index); one identity holds at most one accepted membership in
any tenant (strict option). Acceptance runs SERIALIZABLE inside the token's tenant only (tenant-filtered context, no
`IgnoreQueryFilters`); every refusal is one neutral `409 invitation_unavailable`, attempts are rate-limited per identity
(shared PostgreSQL counters with keyed-hash partitions when `RateLimiting:Store=Postgres`, otherwise process-local; see [RATE-LIMIT-01](phases/RATE-LIMIT-01.md)) and each step is a SecurityEvent with closed codes. Not production IAM rollout, e-mail delivery, IdP
governance or GDPR/KVKK acceptance. [Decisions](tasks/USER-ADMIN-02.md).

## USER-ADMIN-03 — invitation e-mail outbox and tenant invitation lifetime (authored; PG tests deferred to CI)

An optional invitation e-mail is an outbox row written in the invitation's audited transaction. Recipient and token are
held only as Data Protection ciphertext bound to tenant + row and erased at the terminal state; the address is never
returned, audited or logged and the idempotency fingerprint records only a keyed HMAC of the normalized address
(INVITE-RATELIMIT-FOLLOWUPS N4). The link origin is
operator configuration (https), never the request Host header. The dispatcher writes only as the explicit system actor
`system:invitation-delivery` through tenant-scoped contexts (one read-only due scan of tenant ids); logs carry closed
outcome codes and exception type names only. SMTP uses STARTTLS (plaintext only to loopback) and a secret reference for
the password. Without a transport the state is `NOT_CONFIGURED`, never delivered; `SENT` means mail-server acceptance
only. The 54.01 `account.invite_hours` tenant value (1-168 h) is a permission-gated, version-checked, audited command.
Not production mail operations, bounce handling, Data Protection key management or GDPR/KVKK acceptance.
[Decisions](tasks/USER-ADMIN-03.md).

## INVITE-RATELIMIT-FOLLOWUPS — invitation/sign-in limits, outbox sweep decision, Data Protection key ring (authored; PG tests deferred to CI)

- Idempotency (N4): invitation create/reissue fingerprints include `HMAC-SHA256(HKDF(OIDC client secret), normalized
  address)` (`a1:`), or a per-process key (`p1:`, fails closed with 409 after a restart) for a public client. Same key +
  other address = `409 idempotency_key_reused`. The address is never stored, logged or audited.
- Sweep (N3): only `InvitationDelivery:Mode=Disabled` (a deliberate fleet-wide decision; contradictory with
  `Enabled=true`, which stops startup) finalizes queued rows as `NOT_CONFIGURED`. An instance with a missing transport logs
  a startup warning (event 4303) and leaves queued rows for a configured peer; any instance still cancels rows of
  closed/expired invitations. `Enabled=true` with an incomplete/unsafe configuration or unresolvable credential reference
  stops startup with reason codes only. Each instance logs a non-reversible configuration digest (event 4301).
- Rate limits (RATE-LIMIT-01 limiter, keyed-HMAC partitions, no raw identifiers stored or logged): invitation create +
  reissue share 60 per 15 minutes per (tenant, administrator); `GET /api/v1/auth/login` 60 per 5 minutes per client
  network (IPv4 address / IPv6 /64; `RateLimiting:LoginPermitLimit` 10-100000). Refusal = `429 request_limit` with
  `Retry-After`; the first refusal of a window is a SecurityEvent, repeats are not (no amplification). A browser
  navigation to the login gets the 429 with a minimal page that returns to `/app?auth=request_limit` (localized
  notice). Behind a reverse proxy, forwarded-header processing must be configured and restricted to the known proxies (KnownProxies/KnownNetworks), or
  all callers share one partition; `ASPNETCORE_FORWARDEDHEADERS_ENABLED` trusts `X-Forwarded-For` from any source and
  must not be used where clients can reach the API directly (they could rotate the header past the login limit). The
  IdP remains responsible for credential brute-force protection and lockout.
- Data Protection (L2): `DataProtection:KeyDirectory` (absolute path) persists one key ring under application name
  `Poseidon` for all instances. Enabling invitation e-mail outside Development/Test without it stops startup. The
  directory is operator infrastructure (shared volume, service-account-only access, backup, volume encryption); key
  protection at rest beyond that and key rotation operations are not built.
Not production mail operations, an approved quota, DDoS protection or GDPR/KVKK acceptance.
[Phase report](phases/INVITE-RATELIMIT-FOLLOWUPS-20261123.md).
