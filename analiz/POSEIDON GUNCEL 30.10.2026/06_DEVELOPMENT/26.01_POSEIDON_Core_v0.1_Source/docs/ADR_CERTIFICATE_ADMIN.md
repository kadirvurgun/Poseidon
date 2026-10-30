# ADR-CERT-ADMIN-001–004 — İdari belge kayıtları ve tarih takibi

27 Ekim 2026. Taban `cdb39a5658dd5d2f6036e1cd52c9608b9b25fedf` (83 dosyalık,
incelemesi tamamlanmış idari envanter adayı). Bunlar devredilmiş ürün/uygulama
kararlarıdır; tarihsel kanonik onay, mevzuat kuralı, gerçek hesap yetkisi, üretim
izni veya kullanıcı kabulü değildir. Ana dal üzerinde merge yapılmaz.

## Kaynak ve kapsam

Görevde sağlanan 14.01 ERD, 16.01 API, 13.06/39-modül master, 14.02 veri sözlüğü
ve 15.01 ortak altyapı özetleri esas alınır. Certificate root vessel/type/issuer,
isteğe bağlı issue/expiry; metadata intake hiçbir zaman VERIFIED/VALID/readiness
ayarlamaz. Survey window, authoritative catalogue, renewal/suspension ve verifier
yetkisi açıktır. Prototipteki karışık state listeleri ve Available varsayılanı
alınmaz. POL-DEFAULT-01 eksik DRAFT ve belirsiz Yes/No için varsayılan üretmeme
politikasıdır. Bu ADR kaynakların tüm düzenleyici testlerini geçtiğini iddia etmez.

Yeni yolculuk: yetkili gemi/römorkörü seç → eksik taslak → metadata → açık idari
kayıt → liste/tarih filtreleri/detail → gerekçeli düzeltme/reopen → güvenli tarihçe.
Adı **İdari belge kayıtları ve tarih takibi**; tüm sertifika/sörvey modülü değildir.

## Durum, yetki ve kimlik

DRAFT / RECORDED, sertifika UUID'si değişmez. Mevcut sertifikalar migration ile DRAFT
kalır; tam alan veya gelecek tarihten RECORDED çıkarılmaz. Kayda almak için mevcut
scope içindeki vessel_id, nonblank declared type ve issuer gerekir. Tarih veya
certificate_no zorunlu değildir. Boş expiry NO_EXPIRY_DATE; perpetual/valid değildir.
Kayıtlı expiry düzeltmesi yenileme işlemi değildir. Aynı numaralı ikinci kayıt
mümkündür; idari UUID ayrıdır, global numara uniqueness/supersession uydurulmaz.

DRAFT create/edit: draft:write. Record, RECORDED metadata edit, reopen:
**certificate:manage**. Inventory manager, writer, şirket üyeliği veya Master/DPA/admin
etiketi bu yetkiyi vermez. IdentityGrant.CanManageCertificates varsayılan false;
gerçek hesap/IdP grant'i yoktur. Yeni certificate-manager yalnız Development sentetik
fixture'ıdır; inventory:manage otomatik verilmez.

Nesne erişimi her read/write/replay/history sayfasında güncel tenant + company +
parent vessel (owner_subject veya açık vessel grant) koşuludur. Sertifika yaratıcısı
veya aynı şirket üyeliği tek başına erişim vermez. Parent değişmez. DRAFT ve RECORDED
gemiler seçilebilir; parent reopen sertifikayı iptal etmez/yeniden sınıflandırmaz.
Parent name/IMO/flag/adminState canlı yetkili projeksiyondur; belge otoritesinin
beyanına kopyalanmış doğrulanmış gerçek değildir.

Yanıttaki verificationAssessment=NOT_PERFORMED ve operationalAssessment=NOT_PERFORMED
bu uygulama diliminin yapmadığı değerlendirmelerdir; dışarıda hiç doğrulama yapılmadığına
dair tarihsel kanıt değildir. İleride gerçek verification motoru geldiğinde sözleşme
ayrı tasarlanmalıdır.

## Kapalı metadata sözleşmesi

| Anahtar | Sahiplik / temsil |
|---|---|
| id | Eski draftId'nin aynı UUID'si; yeni DTO'da alias; yeni nesne kimliği oluşturulmaz |
| vessel_id | Var olan yetkili gemi UUID'si; create'de zorunlu, sonra değişmez |
| type | Beyan belge tipi; document_name/certificate_name yazma alias'ı değildir |
| issuer | Beyan issuer metni; RO/class/otorite kataloğu değildir |
| certificate_no | İsteğe bağlı beyan numarası; identity veya global unique değil |
| issue | İsteğe bağlı YYYY-MM-DD; valid_from değildir |
| expiry | İsteğe bağlı YYYY-MM-DD, 'Kayıtlı bitiş tarihi'; hukuki geçerlilik hükmü değil |
| notes | İsteğe bağlı plain text; evidence/HTML/link çalıştırma yok |

Sağlanan değerler string/nonblank: 200 karakter, notes4000. Belirsiz değerler atlanır;
boş, duplicate, unknown alanlar reddedilir. Gerçek ISO takvim tarihi ve iki tarih
varsa expiry>=issue gerekir. Gelecek issue yalnız metadata uyarısıdır; blanket gelecek
tarih yasağı yoktur. Yeni root/alias/evidence/ref/availability/verification alanı yoktur.
Metadata replace tam sözleşmedir; UI önce tam kaydı okur. RECORDED düzenleme ve reopen
için trim edilmiş 1–500 karakter reason zorunlu; reason kısıtlı audit içindedir,
tarihçeye çıkmaz. Zorunlu type/issuer silmek için önce reopen gerekir.

## UTC tarih sunumu ve sayfalama

Sunucudaki TimeProvider UTC takvim günü asOfDate; date-only girdiler timestamp değildir.
Aradaki takvim günü farkı daysToRecordedExpiry ve tam birbirini dışlayan kovalar:

| Fark | Bucket | İddia |
|---|---|---|
| Expiry yok | NO_EXPIRY_DATE | Bitiş tarihi girilmemiş |
| <0 | PAST_DATE | Kayıtlı bitiş tarihi geçmiş |
| 0 | TODAY | Kayıtlı bitiş tarihi bugün |
| 1…30 | WITHIN_30_DAYS | Kayıtlı bitiş tarihine 1–30 gün |
| 31…60 | DAYS_31_TO_60 | 31–60 gün |
| 61…90 | DAYS_61_TO_90 | 61–90 gün |
| >90 | AFTER_90_DAYS | 90 günden fazla; 'valid' değil |

Bunlar yeni idari gösterim sınırları, statutory/survey window değildir. UI kalıcı
uyarı gösterir: yalnız girilen metadata; özgünlük, hukuki geçerlilik ve operasyonel
hazırlık değerlendirilmedi. asOfDate/timeBasis=UTC_CALENDAR_DATE açıkça gösterilir.
Issue/expiry/asOfDate browser timezone'a dönüştürülmez. History olayının UTC zamanını
formatlamak, date-only metadata'yı timestamp'e çevirmekle karıştırılmaz.

İlk koleksiyon isteği bugünün server UTC tarihini alır; cursor aynı asOfDate'i taşır.
Gece yarısından sonraki devam sayfası aynı tarih filtresini kullanır. Refresh/ilk sayfa
cursor'u atar ve yeni günü alır. Tarih değişimi aggregate state/version/audit üretmez.
Kayıtlar sayfalar arasında değişebilir; bu historical data snapshot değildir.
Cursor format/query/scope/date bağlamı anahtarsız consistency hash ile bağlıdır;
kriptografik sunucu-köken kanıtı veya yetki değildir. Future/malformed tarih reddedilir,
arbitrary asOfDate query kabul edilmez; bir istemci hash'i yeniden hesaplayabilir.
Sunucu yanıtı kullanılan tarihi açıkça bildirir. Bu tasarım inventory cursor sınırını
korur; yeni credential/key üretmez. Gerçek tarihsel sorgu veya anti-tamper garantisi
sunulmaz.

## API, transaction ve tarihçe

Bearer `/api/v1/certificate-register`, cookie `/bff/api/v1/certificate-register`.
Cookie mutasyonları CSRF, bütün istekleri gösterilen scope stamp'i ister. Bearer API
cookie ile doğrulanmaz. Capabilities endpoint yalnız kendi canWriteDraft/canManage
bilgisidir; nesne grant'i değildir.

Liste: q(type/issuer/number), exact vessel_id/adminState/dateBucket, pageSize50/100,
UUID ASC keyset cursor. q trim100; wildcard yüzde/alt çizgi/ters slash literal,
ASCII-only fold; Türkçe linguistic collation iddiası yok. Unknown/duplicate/oversized
query400. items/nextCursor/hasMore/asOfDate/timeBasis; toplam veya valid count yok.
GET detail ETag, full data, completeness(missingRequiredFields/invalidFields),
parentVessel/dateTracking ve boundary labels. Create DRAFT; metadata replacement;
record boş body/{}; reopen reason. Genel /submit halen403; verify/revoke/renew/delete yok.

Idempotency UUID her yazıda, If-Match güncelleme/komutta; eksik428, bozuk400, stale412,
yetki403, erişilemeyen404, metadata422, transition/key409. Namespace/action/object/
version/body fingerprint'e dahildir. Aynı anahtar lock'u, kilitli certificate sürümü,
state'e bağlı yetki, aggregate/audit/replay tek transaction içindedir. Replay CURRENT
parent visibility ve özgün action permission'ı yeniden denetler; revoked grant cached
success vermez. Exact replay özgün date/parent projeksiyonunu döndürebilir; UI sonrasında
GET ile güncel kaydı okur. DB failure503 başarı değildir.

History: eventId,occurredAt,version,action,changedFields,actorRelation SELF/OTHER.
Certificate yedi alanı + adminState sabit izin listesi; bilinmeyen action tarihi
metadata değişikliği olur. Değerler, raw actor/issuer/sub, names/email/rol geçmişi,
reason/correlation/IP/before-after çıkmaz. Mevcut append-only kayıtlar yeniden yazılmaz.
Version DESC,event UUID DESC, page20/100; inventory ve koleksiyon cursor'undan ayrı
domain binding; her sayfa güncel parent erişimiyle okunur.

## Migration ve shared browser korumaları

001/002 değişmez; yeni003, certificate_schema_version1/2 ekler. Eski certificate UUID,
parent FK, owner/audit/idempotency korunur; yeni yüzeye ilk yazı schema2 olur. Legacy
v1 DRAFT davranışı korunur; v2/RECORDED legacy write/replay409 certificate_resource_required.
RECORDED legacy GET409; eski collection yalnız DRAFT. DB eski UPDATE koruması iki türün
ayrı transaction-local marker'ını denetler. Bu compatibility guard SQL-admin'e karşı
yeni güvenlik grant'i değildir. Eski binary'ye güvenli rollback garanti edilmez;
forward fix/test edilmiş restore gerekir. Gerçek deployment yapılmadı.

Shared API ilk await öncesi logout/mutation reservation'ını korur. Certificate ekranı
parentBusy ve kendi busy kilidini uygular. Lost response → retry403, önceki commit'in
olmadığını kanıtlamaz; özgün uncertain key tutulur. Onaylı POST sonrası retry yalnız GET.
Expiry'de belirsiz pending korunur, ayrılma/yeniden giriş uyarısı olası commit'i açıklar.
Otomatik write retry veya başka hesap altında sessiz save yoktur.

## Açık alan/modül kapıları

| Kapı | Açık yetenek; bu pakette tamamlanmış sayılmaz |
|---|---|
| G1 | Source-owned/effective-dated type/issuer/applicability catalogues; ship/tug/flag koşullu tarihler |
| G2 | Gerçek DocumentVersion/Evidence upload; scope/storage/type/size/malware/hash/retention/hold |
| G3 | Actor authority, ayrı evidence/verifier/result bağları; row168 çatışması ve gerçek doğrulama |
| G4 | Survey windows/plan/pre-survey, authoritative inclusivity, booking, renewal/supersession/suspend/revoke |
| G5 | Readiness/PSC/deficiency/requirement/evidence freshness ve karar gerekçesi |
| G6 | Outbox/notifications/authorized recipients; offline/reconciliation/sync |
| G7 | Gerçek IdP/üyelik/key/TLS işletimi, private user browser access ve kullanıcı UAT |

Document register idari root dilimi uygulanır; statutory-readiness, command-center'ın
legal valid/critical-gap/readiness projeksiyonları, survey-planner, renewal-workflow,
pre-survey checklist ve evidence verification ayrı açık işlevlerdir. Tarih kovaları
bunları tamamlamaz. Hiçbir timer email/push veya legal-state olayı üretmez.
