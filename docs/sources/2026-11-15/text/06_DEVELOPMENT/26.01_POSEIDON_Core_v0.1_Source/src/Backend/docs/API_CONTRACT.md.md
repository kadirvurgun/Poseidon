# API_CONTRACT.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/src/Backend/docs/API_CONTRACT.md
SHA256: 62d80e1c32772b032790f991977f537fbe266376d6482ab851a9cef67eed62be

## lines:1-40

# Draft API contract (development implementation)

Base URL configured through ASPNETCORE_URLS; suggested http://127.0.0.1:5080.
All API endpoints require Bearer authentication except the explicit Development-only
synthetic session endpoint. Public GET /health/ready returns HTTP 200 or 503:
`{"status":"ready|not_ready","persistence":"PostgreSQL"}`; checks connectivity,
PostgreSQL >=17 and required tables; no secrets. No external IdP is yet configured.

| Method | Route | Result |
|---|---|---|
| POST | /api/v1/dev/session | Only explicit Development mode, fixture login |
| GET | /api/v1/session | Current verified/synthetic session |
| GET | /api/v1/vessels | `{items:Draft[]}` |
| POST | /api/v1/vessels | Create metadata draft |
| GET | /api/v1/vessels/{draftId} | Draft + ETag |
| POST | /api/v1/vessels/{draftId} | Replace draft metadata + ETag |
| GET | /api/v1/certificates | `{items:Draft[]}` |
| POST | /api/v1/certificates | Create metadata draft |
| GET | /api/v1/certificates/{draftId} | Draft + ETag |
| POST | /api/v1/certificates/{draftId} | Replace metadata + ETag |
| POST | /api/v1/{vessels or certificates}/{draftId}/submit | Always 403 |

Save request `{saveMode:"DRAFT",data:{...}}`; Idempotency-Key UUID header mandatory.
Updates require If-Match header with quoted positive version (`"1"`). Missing fields
are removed on replacement. HTTP 200 save/read Draft:
`{draftId:UUID,version:number,persistenceState:"DRAFT",submissionAllowed:false,data:{...}}`.
ETag is quoted version; no automatic finalization. No DELETE or verified state.
Vessel fields: name, company_id, imo_no, official_no, flag, vessel_type, notes.
Certificate fields: vessel_id, type, certificate_no, issuer, issue,
expiry, notes. Values must be nonblank strings or omitted; metadata optional
except certificate vessel_id for authorization. UUID refs; dates YYYY-MM-DD; IMO seven
digits. Unknown/duplicate/forged fields rejected. Notes max4000; other strings max200.
List limited to 500; no pagination contract yet.

Errors `{code:string,message:string}`: 400 invalid JSON/missing idempotency key;
403 submission disabled/company scope; 404 draft or vessel inaccessible;
409 idempotency conflict; 412 stale version; 422 invalid provided data;
428 missing update version; 503 database failure (save not confirmed).
Framework 401/403 authentication/authorization failures may have empty bodies.

## lines:41-80

Default OAuth2 bearer JWT auth: Authentication__Authority HTTPS, Authentication__Audience;
validated server claims sub, tenant_id, company_id, repeated vessel_id; writes also
permission=draft:write. X-Tenant headers ignored. New vessels accessible to creator
subject only, or explicitly signed vessel_id claim; company/tenant enforced separately.

Synthetic mode: ASPNETCORE_ENVIRONMENT=Development and SyntheticIdentity__Enabled=true.
Any other environment with synthetic enabled fails startup. No static passwords or
shared admin tokens. POST /api/v1/dev/session `{fixture:"tenant-a"}` returns
`{accessToken:string,session:{subject,tenantId,companyId,vesselIds:UUID[],synthetic:true}}`.
Fixtures tenant-a, tenant-b, tenant-a-restricted. A restricted shares A tenant/company
but owns no A vessels. Tokens random, in-process, expire after one hour.
GET /api/v1/session returns the inner session object. Database__Initialize=true is
also Development-only, applying schema and synthetic company seed. Details in ../README.md.

Additional negative-test fixtures: tenant-a-other-company uses the same synthetic-a subject in a different company of tenant A; tenant-a-readonly has no draft:write permission. These exist only in explicit Development synthetic mode.

Controlled16.01 alignment: CertificateDraftFields canonical shared keys are exactly
vessel_id/type/issuer/issue/expiry. Former unpublished certificate_type/issue_date/
expiry_date keys are rejected (422 unknown_field). certificate_no and notes are
explicit development extensions, as are certificate detail/update and explicit
submit-rejection routes. No controlled-source file was modified for this correction.

## INVENTORY-001 browsing extension (2026-10-27)

Vessel GET lists now accept q, flag, vessel_type, pageSize and cursor and return
`{items,nextCursor,hasMore}`. No-query default changed from first500 to first50;
max pageSize100. Certificate lists retain the earlier bound. New query semantics
are implementation decisions, not pre-existing canonical approval. Both bearer and
explicit BFF vessel routes use the same scope-checked keyset query. See
[ADR_INVENTORY_BROWSING.md](../../../docs/ADR_INVENTORY_BROWSING.md) for strict input
bounds, literal wildcards, ASCII-only folding, query/scope-bound cursor, compatibility
and non-snapshot caveats. No history-read or new lifecycle endpoint is added.

## ADR-INV-ADMIN-001–004: ayrı idari envanter yüzeyi

Yeni uygulama kararları, eski kanonik lifecycle onayı değildir. Ayrıntılar:
`docs/ADR_INVENTORY_ADMIN.md`. Bearer kökü `/api/v1/inventory/vessels`;
cookie kökü `/bff/inventory/vessels`. Cookie tüm mutasyonlarında CSRF ve tüm
isteklerde X-Poseidon-Scope zorunlu. API tokenları/BFF cookie'leri birbirine genişlemez.

## lines:81-120

- GET collection: mevcut q/flag/vessel_type/pageSize/cursor + exact
  `adminState=DRAFT|RECORDED`, `asset_mode=SHIP|TUG`; items/nextCursor/hasMore.
- GET /capabilities: çağıranın canWriteDraft/canManage yetenekleri; nesne yetkisi değildir.
- GET /{id}: `{id,version,adminState,data,completeness,createdAt,modifiedAt}` + ETag.
  completeness `{missingRequiredFields,invalidFields}`; uyumluluk yüzdesi yok.
- POST collection `{data:{...}}`: eksik DRAFT; şirket sunucudan. create-as-RECORDED yok.
- POST /{id}/metadata `{data:{...}}`: tam yeni metadata replacement.
- POST /{id}/commands/record: `{}` veya boş body; yalnız DRAFT→RECORDED.
- POST /{id}/commands/reopen `{reason:"..."}`: yalnız RECORDED→DRAFT.
- GET /{id}/history: dar alan adları/SELF-OTHER projeksiyonu; ayrı cursor ve pageSize20/100.

Her yazıda Idempotency-Key UUID; güncelleme/komutlarda quoted If-Match. Eksik 428,
bozuk anahtar 400, eski sürüm 412, action grant eksik 403, nesne yok/yetkisiz404,
metadata422, geçiş/idempotency çakışması409. Metadata DRAFT'ta draft:write,
RECORDED'ta inventory:manage; record/reopen inventory:manage. UI yetki düğmeleri
sunucu denetiminin yerine geçmez. Versioned aggregate/audit/replay atomik.

Legacy `/api/v1/vessels` ve BFF draft karşılığı yalnız DRAFT listeler. schema2 DRAFT
okuması sınırlı yedi eski string alanıdır; geniş metadata için yeni route şart.
schema2 veya RECORDED legacy yazı/replay409 inventory_resource_required. RECORDED
legacy single GET409; hiçbir cevap yanlış DRAFT iddiası üretmez. Legacy submit403.
Sertifika taslak kayıtları değişmedi; RECORDED gemiye yetkili bağlantı mümkündür.
Arayüzde eski Gemi taslakları sekmesi uyumluluk görünümüdür; tam yeni idari yönetim
İdari envanter sekmesindedir. Sertifika gemi seçimi yeni tam inventory reader kullanır.

## ADR-CERT-ADMIN-001–004: idari belge kayıtları ve tarih takibi

Yeni root `/api/v1/certificate-register`; explicit cookie karşılığı
`/bff/api/v1/certificate-register` (CSRF + scope stamp). Scope temel parent vessel
üzerinden aynıdır; DRAFT/RECORDED parent kabul edilir. Yeni bağımsız certificate:manage,
writer/inventory:manage/meslek etiketiyle verilmez. Ayrıntılar docs/ADR_CERTIFICATE_ADMIN.md.

GET collection q/vessel_id/adminState/dateBucket/pageSize/cursor; UUID ASC keyset.
Yanıt items/nextCursor/hasMore/asOfDate/timeBasis; counts yok. Sayfa cursor'u UTC
asOfDate'i gece yarısı boyunca tutar; refresh yeniler; data snapshot değildir.
GET capabilities canWriteDraft/canManage; GET /{id} ETag +
{id,version,adminState,data,completeness,dateTracking,verificationAssessment,
operationalAssessment,parentVessel}. id eski draftId'nin aynı UUID'sidir.
DateTracking {asOfDate,timeBasis,daysToRecordedExpiry,bucket,issueDateInFuture}.
İki assessment NOT_PERFORMED bu uygulama diliminin sınırıdır, dış tarihsel doğrulama iddiası değil.

## lines:121-153

POST collection {data}; POST /{id}/metadata {data,reason?} (RECORDED'da reason zorunlu);
POST /{id}/commands/record boş/{}; POST /{id}/commands/reopen {reason}.
Yedi eski certificate metadata alanı korunur; create vessel_id zorunlu; record ayrıca
type/issuer ister. Tarihler isteğe bağlı; vessel immutable; kayıtta required alanlar
silinemez. Reasons restricted audit500. Metadata replace tam data sözleşmesidir.
History dar inventory ile aynı şekil ve certificate-only alan adlarıdır.

Legacy certificate DRAFT v1 çalışır; v2/RECORDED update ve replay409;
RECORDED single GET409, legacy list DRAFT-only, submit403. Certificate UI yeni
Belge kayıtları sekmesidir; eski Sertifika taslakları uyumluluk görünümüdür.
Kayıtlı expiry düzeltmesi 'renewal' değildir; doğrulama/legal validity/readiness,
evidence upload, survey ve notifications yoktur.

## Quarantined evidence intake (new development/test contract)

Disabled by default. Explicit `EvidenceIntake:Enabled=true` requires Development or Testing and an existing private `EvidenceIntake:Root`; all other environments fail closed. Linux x64 only. No directory creation, chmod, scanner, download, deletion, production provider or permission provisioning occurs. One evidence database/schema uses one root. Startup reconciles every retained object against reservations and every stored row against length/hash; unknown files, missing stored files or unsafe entries fail startup without cleanup.

Bearer `/api/v1/evidence-intake` and explicit BFF `/bff/api/v1/evidence-intake` retain separate authentication. BFF requires scope stamp and mutation antiforgery. Read permission follows the current target object; mutation requires independent `evidence:capture` AND `inventory:manage` for Vessel or `certificate:manage` for Certificate, even for drafts. Only the separate Development `evidence-manager` fixture has all three capabilities by default.

- `GET /capabilities?targetType=Vessel|Certificate&targetId=UUID`: authorized capture capability, maxBytes; inaccessible target404; authorized readers without capture grants receive canCapture=false and may read receipts/history.
- `POST /`: closed metadata `{targetType,targetId,filename,declaredMime,expectedBytes,expectedSha256?,title?,source?,description?,capturedAt?}`, Idempotency-Key UUID. Metadata body max16KiB, binary1..5,242,880bytes. Filename NFC1..128, one extension, no path/encoded separator/control/bidi. MIME PDF/PNG/JPEG must match extension. Optional strings nonblank, title/source200, description2000; capturedAt UTC timestamp. Server derives scope/actor/object UUID.
- `GET /?targetType=...&targetId=...&pageSize=...&cursor=...`: default50/max100 stable UUID ascending; target/scope-bound cursor, not authority or snapshot. Unknown/duplicate filters rejected.
- `GET /{id}`: receipt with ETag, nested `metadata`, state, scanState, integrityStatus, sha256, actualBytes, previousId/supersededBy, `canDownload=false`, `evidenceType=ATTACHMENT`, assessment boundaries `NOT_PERFORMED`.
- `POST /{id}/content`: raw body with exact reserved Content-Type, Idempotency-Key UUID and If-Match. Intent commits RESERVED→RECEIVING before file creation; only one attempt, no retransmission even with same key/digest. Reconcile uncertainty using GET. RESERVED expires after15minutes on attempted upload. Bytes beyond reservation are rejected before disk write. Signature/trailer checks are bounded recognition only, not full parsing or malware checks; PDFs containing `/Encrypt` are rejected.
- `POST /{id}/supersede`: `{metadata:<full new reservation>,reason:<nonblank max500>}` with original If-Match and new key reserves same-target child. Only a currently stored original may be replaced; one active or successfully stored child reservation per original. REJECTED/EXPIRED children remain retained and charged but permit a new replacement reservation. Child stored commit emits supersession event and exposes link; original metadata/hash/file stays immutable. Reason is restricted, absent from receipt/history.
- `GET /{id}/history`: existing bounded safe-history six fields; fixed state/link field names, SELF/OTHER only. No raw audit identity, reason, metadata values or paths.

States RESERVED→RECEIVING→STORED_QUARANTINED or REJECTED; expiry becomes EXPIRED. Aborted/unconfirmed IO may remain RECEIVING indefinitely. ScanState always UNAVAILABLE; content always quarantined. Hash COMPUTED/MATCHED/MISMATCH is integrity only. Rejected recognized content commits rejection then HTTP422; recover receipt with GET. No clean/verified/available inference.

Reserve locks tenant quota and idempotency atomically:100MiB reserved bytes per tenant,10 active RESERVED/RECEIVING reservations per actor. All reservations remain byte-charged permanently, including rejected/expired/orphaned files; there is no quota release or cleanup. Successful key replay returns current receipt after authorization, without double charge. File create uses generated basename, anchored directory descriptor and O_EXCL/O_NOFOLLOW; all ancestors are opened without following links. Flush file to disk, close, fsync directory, then commit metadata/audit. Files are never overwritten or removed, including postcommit faults. Database/file atomicity is recovered conservatively through retained RECEIVING state and startup reconciliation, not a distributed transaction. Multi-process root reuse, manual recovery and production storage operations need a separate design/release.

Fault injection interface `IEvidenceFaults.Checkpoint`: before_file, after_file (closed+directory-flushed), before_commit, after_commit. Test fixtures may throw; default is no-op. No actual malware/provider testing is implied.
