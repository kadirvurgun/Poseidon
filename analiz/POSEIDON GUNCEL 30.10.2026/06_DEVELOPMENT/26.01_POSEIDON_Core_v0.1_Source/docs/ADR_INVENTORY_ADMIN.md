# ADR-INV-ADMIN-001–004 — İdari gemi/römorkör envanteri

27 Ekim 2026. Taban `008b6105b1d964373bd7a92ee032b51b4a7ae00f`.
Bunlar kullanıcının devrettiği ürün/uygulama kararlarıdır; eski kanonik onay,
mevzuat kuralı, gerçek hesap yetkisi, üretim izni veya Esra kabulü değildir.

## İdari durum ve yetki

Tek gemi UUID'si korunur. Yeni `admin_state` yalnız DRAFT / RECORDED; eski
`persistence_state` eski şema uyumluluğu için DRAFT kalır, yeni DTO bunu durum olarak
kullanmaz. RECORDED **Envantere alındı / Recorded in inventory** demektir; READY,
ACTIVE, VERIFIED, mevzuata uygunluk, bayrak tescili veya operasyon izni değildir.
Sertifika durumu ve gemi bağlantısı değişmez. Silme/arşiv/transfer yoktur.

Taslak oluşturma/düzenleme `draft:write`; kayda alma, kayıtlı metadata düzeltme ve
tekrar açma bağımsız `inventory:manage` gerektirir. Bu yeni yetki varsayılan reddir;
CanWrite, admin veya meslek unvanından türetilmez. BFF güvenilir resolver sözleşmesine
`CanManageInventory=false` eklendi. Yalnız yeni sentetik `inventory-manager` fixture'ı
bu yetkiye sahiptir; gerçek hesap/IdP provisioning yapılmadı.

Her nesne okuma, yazma, tarihçe sayfası ve replay mevcut tenant + şirket +
(oluşturan subject veya açık vessel grant) koşulunu yeniden uygular. Aynı şirkette
bulunmak yetmez. Kimlik gerçek akışta issuer+subject bileşimidir. Salt okunur nesne
okuyucusu dar tarihçeyi görebilir; mutasyon yapamaz. BFF namespace ayrı, cookie +
CSRF + gösterilen scope önkoşulu zorunlu; bearer namespace cookie kabul etmez.

Kayda alma: sunucu şirketi + name + flag + vessel_type + açık SHIP/TUG asset_mode.
Taslak eksik kalabilir; bilinmeyenlere varsayılan verilmez. IMO isteğe bağlıdır;
römorkörde IMO zorunluluğu veya uygulanabilirliği çıkarılmaz. Kayıtlı kayıtta zorunlu
alanlar silinemez; mode değişimi önce gerekçeli reopen ister. Diğer düzeltmeler
manage yetkisiyle mümkündür. Reopen gerekçesi trim edilmiş 1–500 karakterdir,
kısıtlı audit metadata'sında tutulur, dar tarihçeye çıkmaz.

## Alan sahipliği ve teknik veri sınırları

`InventoryContracts.Fields` kapalı ve tipli izin listesidir. Şirket sunucu tarafından
alınır; verilirse tam yetkili UUID ile eşleşmelidir. owner/id/state/projection alanları
istemci girdisi değildir. Tam metadata replacement: gönderilmeyen isteğe bağlı alan
silinir; UI önce tam nesneyi okur. Eski ekranın eksik projeksiyonundan replace yapılmaz.

| Alanlar | Temsil ve sınır |
|---|---|
| name, flag, vessel_type | Beyan edilen metin; kayıt sırasında zorunlu; doğrulanmış katalog değildir |
| asset_mode | Yeni SHIP/TUG ayırıcı; subtype metninden türetilmez |
| imo_no / company_imo | İsteğe bağlı yedi rakam; checksum/registry doğrulaması yok |
| official_no, port_registry, call_sign, mmsi | İsteğe bağlı beyan metni; yeni resmî format kuralı uydurulmadı |
| gt, nt | İsteğe bağlı sayısal gros/net tonaj; ağırlık tonu değildir |
| dwt | Metrik ton (t), yeni açık idari temsil seçimi |
| loa, breadth, draft | Metre; draft yaz su çekimidir, yaşam döngüsü DRAFT değildir |
| build_date | ISO gerçek takvim tarihi; gelecekteki tarih otomatik yasak değil |
| class_society, class_no | Beyan referansı; geçerli klas/sertifika kanıtı değil |
| doc_company, company_imo | Gemi düzeyinde DOC işletmecisi beyan snapshot'ı; Company master/yetki değiştirmez |
| master_name, dpa_name | Doğrulanmamış iletişim/display adı; kullanıcı, rol, atama, manning uygunluğu yaratmaz |
| evidence_ref | Yalnız referans metni; fetch/link/attachment/evidence UUID çözümleme yok |
| notes | İsteğe bağlı 4000 karakter; tarihçede değeri gösterilmez |

Diğer metinler 200 karakter; baş/son boşluk trim edilir; HTML olarak işlenmez.
Sayılar JSONB'de JSON number, API'de tam decimal(12,3) teknik aralığı
0..999999999.999. Fazla anlamlı ondalık hassasiyeti yuvarlanmadan reddedilir;
API exponent gösterimini bu tam aralıkta destekler. UI 9 tam/3 ondalık basamaklı
noktalı düz gösterim ister. Bu sınır denizcilik geçerlilik eşiği değildir. Sıfır
bilgi, eksik değer bilinmeyendir. İçe aktarma/birim dönüşümü uygulanmadı.

createdAt yalnız gerçek DRAFT_CREATED olayından; yoksa null. modifiedAt gerçek
son audit zamanıdır. Eski UPDATE zamanı oluşturulma zamanı diye gösterilmez.
Sorumlu rol, Requirement, Evidence doğrulama, verifier, crew eligibility ve
operasyonel hazırlık ayrı alanlar olarak açıkça atanmadı/değerlendirilmedi denir;
bunların işlevleri uygulanmış sayılmaz. DOC/company master uzlaştırması gelecekteki
iştir. 27 ekran alanı 27 düzenlenebilir kök alan değildir.

## İşlem ve idempotency

Her yazı idempotency UUID gerektirir; eksik 428, bozuk 400. Güncelleme/komutlar
If-Match ister (428); eski sürüm 412, yanlış geçiş 409, geçersiz metadata 422,
yetki yok 403, erişilemeyen nesne 404. Yeni namespace/action/id/version/body
fingerprint'e dahildir. Aynı anahtar farklı girdide 409 verir.

Anahtar advisory lock ve nesne satır kilidi aynı transaction içindedir. Duruma
bağlı yetki/completeness, kilitlenen sürüm için değerlendirilir. Aggregate sürümü,
data/state, audit ve replay sonucu atomik yazılır. Replay mevcut erişim ve özgün
olayın gerekli yetkisini yeniden denetler; güncel metadata durum yetkisi de gerekir.
Özgün yönetici düzenlemesi reopen sonrası sıradan draft yazarı tarafından replay
edilemez. Replay yeni event/sürüm üretmez. UI replay sonrası güncel tam kaydı okur;
POST onaylanıp takip GET başarısızsa özgün anahtar korunur. Ağ hatası başarı değildir.

## Güvenli tarihçe

Yeni açık ürün/güvenlik politikası: nesne okuyucusu yalnız
`eventId, occurredAt, version, action, changedFields, actorRelation` alır.
SELF/OTHER tam güvenilir actor karşılaştırmasıdır; kişiyi izlenebilir kılan başka ID
yoktur. Değerler, isim/email/issuer/sub/actor hash, şirket, correlation/IP/device,
before/after JSON ve gerekçe çıkmaz. changedFields sabit inventory alan listesi ve
adminState ile sınırlıdır. Bilinmeyen tarihsel action sabit HISTORICAL_METADATA_CHANGE
olur; bilinmeyen anahtarlar atılır. Eski kayıtlar değiştirilmez.

Sıra version DESC, event UUID DESC; sayfa 20 varsayılan, 100 üst sınır. Cursor nesne,
current scope, sıralama formatı ve pageSize'a bağlıdır. Koleksiyon cursor'undan ayrı
namespace vardır. Cursor yetki değildir; her sayfa güncel nesne yetkisiyle sorgulanır.

## Migration ve eski istemciler

001 değişmedi. 002 ayrı transaction içinde ledger/advisory lock ile bir kez uygulanır;
eski kurulumda ledger yoksa idempotent 001 kabul edilip 002 uygulanır. Eski gemiler
DRAFT/schema1 olur; dolu alanlardan RECORDED çıkarılmaz. Yeni yüzeyle ilk yazı schema2
olur. Eski v1 taslak create/replace davranışı korunur. Legacy GET schema2 DRAFT için
sadece yedi eski metin alanını projekte eder. RECORDED single GET 409;
legacy koleksiyon yalnız DRAFT. Legacy v2/RECORDED yazı **replay öncesinde de** 409;
legacy submit 403. Sertifika seçim UI'si tam inventory koleksiyonunu kullanır;
sertifika yetkilendirmesi legacy DTO yerine temel gemi nesnesini kullanır.

002 DB trigger, schema2/RECORDED satırı eski UPDATE ifadesinden korur. Yeni envanter
transaction'ı yalnız bağlantısına transaction-local `poseidon.inventory_write=on`
koyar. Bu uygulama uyumluluk korumasıdır; kötü niyetli/veritabanı sahibi bir kullanıcıya
karşı bağımsız güvenlik sınırı veya SQL grant değildir. Yeni güvenlik grant'i verilmedi.

Eski binary ile **okuma güvenli rollback garanti edilmez**: eski kod RECORDED'ı DRAFT
diye etiketleyebilir. Eski binary'ye geri dağıtım yasaktır; forward fix veya doğrulanmış
yedekten kontrollü restore gerekir. Trigger eski yazı kaybını engeller; bütün uygulama
rollback'ini destekliyor diye sunulmaz. Storage + server + sürümlenmiş UI birlikte
kurulmalıdır. Gerçek deployment/migration/restore bu teslimde yapılmadı; yalnız ayrılmış
sentetik test PostgreSQL'i kullanıldı.

## Sınırlar

Arama INVENTORY-001 ASCII fold/literal wildcard davranışını korur; yeni exact
adminState/asset_mode filtreleri cursor'a dahildir. Snapshot garantisi yok; insert/
update sırasında yeniden ilk sayfa gerekebilir. Katalog/registry/class/crew/evidence/
mevzuat/operasyonel hazırlık, çevrimdışı kullanım, gerçek IdP/TLS/key işletimi ve
kurumsal private browser erişimi uygulanmış/doğrulanmış sayılmaz.

## İnceleme düzeltmesi — oturum/yazma sırası ve belirsiz replay

Aynı API istemcisi logout ve mutasyonları ilk await öncesinde rezerve eder. Çıkış
başladıktan sonra Save/Record/Reopen dahil yeni yazı gönderilmez. Önceden başlamış
yazı varken çıkış beklemeye zorlanır/reddedilir; yazının commit'i iptal edilmiş diye
sunulmaz. Parent ve child busy durumları ayrı tutulur; parent logout kilidi hem
Inventory handler'larına hem düğmelerine aktarılır. Bu istemci sıralama korumasıdır;
sunucu CSRF, kapsam, grant ve idempotency denetimlerinin yerine geçmez.

Network/5xx veya araya giren session expiry nedeniyle sonucu bilinmeyen işlem
`uncertain` olarak özgün anahtarıyla tutulur. Sonraki retry403, önceki commit'in
olmadığını kanıtlamaz; bu nedenle anahtar korunur ve yeni create düğmesi açılmaz.
Yetki geri gelince aynı anahtarla replay yapılır. POST onaylandıysa confirmedId
saklanır ve sonraki denemeler yalnız GET'tir. Expiry sırasında ilk yazı bekliyorsa
o da belirsiz sayılır. Kullanıcı ayrılmayı/yeniden girişi seçerse, kaydın sunucuda
oluşmuş olabileceği ve özgün retry anahtarının kaybolacağı açıkça belirtilir.
Yetki iptali sırasında cached success döndürülmez; sunucu403 davranışı korunur.
