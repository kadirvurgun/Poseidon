# Sahipli runtime port izolasyonu — 13.11.2026

Durum: **GENUINE02_FAILED_PARTIAL_MANIFEST_CORRECTION_BATCH_AUTHORING**, acceptance=false. Kullanıcı kesintisiz geliştirme
ve faz tamamlandığında PR/merge istedi. Merge için aynı hedefin gerekli CI ve
bağımsız QA koşulları geçerlidir. Özgün tam gerçek browser hedefi `235d3ea`; güncel kaynak kontrol noktası
`0c5f8f3` (S3 receiving `7722114`, QF receiving `2f6f59a`, foundation policy `201280c`, sınırlı crew personası ve incelenmiş ürün/test/UI düzeltmeleri). Aşağıdaki68f kayıtları
özgün yürütme hedefini korur. Kaynak yazımı ve bağımsız inceleme `68fb45c` üzerinde tamamlandı; büyük
paket PR-hazırlık kapısı yetkilendirildi. İlk DEFERRED kayıtları kendi
tarihsel kapsamında korunur; aşağıdaki bölüm gerçek yürütme sonucudur.

## Gerekçe ve kapsam

Gerçek imzalı observation/browser kapısının mevcut laboratuvarı API5443,
provider8443 ve DB55432 kullanıyordu. İkinci laboratuvar bu portlarla açık
önizlemeyle birlikte çalışamaz. Yeni görev her sahipli çalıştırma için açık,
birbiriyle uyumlu API/provider/database portlarını değişmez bir referansa bağlar.
Eski varsayılanlar korunur; mevcut preview, private profil ve DB değiştirilmez.

Kaynaklar: DEV-OIDC-LAB-01 LAB01-LAB05; OBSERVATION-SIGNED-FIXTURE-01 koşullar1–6;
aktif19.01 paragraflar26/29/30; [otomasyon](../automation.md) ve
[güvenlik temeli](../security-baseline.md). Bu mühendislik görevi ek canonical
story point, onaylı üretim kaynağı veya operasyonel güvenlik kararı üretmez.

## Yazılan backend alt kapsamı

Kaynak commit `06e91936b3ea5b5f75105a7cdf897fd1756de07b`, receiving merge
`ae341862db7e91913e19309ad63ffbd3704cbb94`.
[Backend görev sözleşmesi](../tasks/BUNDLE-PORT-ISOLATION-BACKEND-01.md).

- Açık `DevelopmentLab:OidcPort` yalnız canonical1024..65535 tamsayısını kabul
  eder. Yokluğu mevcut8443 varsayılanını korur; bozuk veya null seçim reddedilir.
- Yerel CA ve port seçimi Development sınırındadır; issuer/authority tam aynı
  localhost HTTPS realm'i ve seçilen portu kullanır.
- Backchannel doğrudan IPv4loopback'e bağlanır. Proxy, redirect ve cookie yok;
  istek hedefi, realm, query/fragment/userinfo ve Host başlığı denetlenir.
- CA, hostname, EKU ve süre doğrulaması korunur. Production için TLS gevşetmesi yok.
- Kaynak tesliminde 60 yeni vaka yazıldı; statik kaynak envanteri71 ve yürütme0
  olarak kaydedildi. Güncel gerçek API-unit sonucu aşağıdaki tabloda yer alır.

## Paralel geliştirme ve doğrulama

Python/TypeScript ortak beş alanlı owned-runtime referansı, immutable generation
bağı, compose/driver/provisioner ve browser origin bağlantıları ayrı branch'te
yazılıp birleşik hedefe alındı. Tüm portların sahipliği ve çakışması mutasyondan önce denetlenmeli;
farklı çalıştırma veya generation açıkça reddedilmeli. Gerçek fixture'lar ve
oturum/yetki/receipt sınırları korunmalı. Bağımsız QA kaynak ve test sözleşmesini
incelemesini tamamladı; gerçek büyük paket kapısı başladı ve aşağıdaki protokol
hatası teşhis edildi.

[Önceki yerel native paket](OBSERVATION-SIGNED-FIXTURE-20261112.md) kendi5e/8a
hedefinde1524ön yüz,1007backendunit,79PG,837API ve117kritik kapsam ile geçti.
Bu sonuç yeni port backend kaynağının PASS kanıtı değildir. Yeni kaynak
tamamlanıp bağımsız incelendikten sonra hedef sabitlenir; anlamlı protokol,
backend/TLS, gerçek isolated runtime/browser ve gerekli regresyon kapıları
çalışır. Ham private/native değerler raporlara alınmaz.

Windows farkı düzeltildi, dar vaka ve tam ilgili protokol kapıları geçti.
İlk gerçek yeni runtime fixture hazırlığını tamamladıktan sonra manifest keşfinde durdu;
teşhis edilen kaynak bağımlılığı düzeltildi. İkinci runtime aşağıdaki gerçek
95PASS/58FAIL/23tamamlanmayan sonuçla sona erdi; yeni PR/push/merge yok.
Canonical kabul **20/121 iş,103/970SP=%10,62;0/196 ayrı ekran** olarak değişmedi.

## Gerçek kapı sonucu — hedef68fb45c

| Kapsam | Sonuç |
|---|---|
| Release solution build | PASS,73,130s. |
| Persistence/Domain/API birim | 75+756+236=1067 PASS; hata/skip0. Transport sınıfı bu toplamın içindedir. |
| Persistence gerçekPG | Önceki5e hedefinin79 sonucu,258 değişmeyen bağımlı girdi ve scrubber eşitliğiyle bağlı; yeni koşu sayılmaz. |
| API entegrasyonu | 837/837 PASS; dört ayrık tam shard210/209/209/209, hata/skip0; kritik gerçek HTTP/audit117/117,107route template. |
| Yeni Python runtime suite | 19 keşfedildi,7 çalıştı:6PASS/1ERROR; kalan12 çalışmadı, kapı durduruldu. |
| Node/Linux/taramalar | İlk68f koşusunda durdu; sonraki gerçek kapsam aşağıda ayrı hedefiyle kayıtlı. |
| Gerçek provider/browser | İlk68f kapısında NOT_STARTED; sonraki gerçek girişim aşağıda. |

Başarısız test:
`test_lab_runtime.LabRuntimeTests.test_explicit_genesis_publishes_one_matching_runtime_across_plan_config_and_generation`,
test locator `scripts/test_lab_runtime.py:180`; kaynak guard
`scripts/lab_runtime.py:119–121,138–139`. Bir dar teşhis koşusu, değişmeyen geçerli
dosyanın pathname lstat ve açık fstat metadata değerlerinin Windows'ta yalnız
`st_ctime_ns` alanında farklı olduğunu doğruladı. Ham değer, private yol veya
yapılandırma dışarı alınmadı. Özgün hata/çıktı hashleri ayrı kayıtta korunur.

PORT-R01 düzeltmesi2aa43ed yalnız Windows'taki uyumsuz cross-API zaman karşılaştırmasını
uyarladı. Aynı API'nin before/after ctime kontrolü ile identity, sahiplik,
izin, boyut ve mtime kontrolleri korundu; POSIX sınırı gevşetilmedi. Bağımsız
kaynak incelemesi, önce başarısız vaka, sonra ilgili tam protokol kapısı geçti.
Yeni hedefe backend kanıtının girdi bağı652aynı source/build/resource/harness girdisiyle bağımsız doğrulandı;
gerçek yürütmenin özgün68f hedefi değiştirilmez.

S01/S02 hash/numeric parity ve S03/S04 BOM/private izin kaynak bulguları68f'de
**RESOLVED_SOURCE_ONLY** olarak kaydedildi. Bu temiz statik inceleme sonraki
gerçek Windows hatasını runtime PASS'a dönüştürmez. Windows'ta koşmayan3 yeni
POSIX vakası ayrıca gerçek Linux yürütmesiyle kapanmalıdır; atlanmış kontrol
tamamlanmış sayılmaz. Root AST güncellemesi exit0:19.499 düğüm,40.272 kenar;
zero-node/HTML büyüklük uyarıları korunur, runtime kanıtı değildir.

## Sonraki gerçek protokol kapısı — eb58432

Windows Python82:81PASS/1POSIXskip; Node68:66PASS/2POSIXskip; hata0.
Linux Python22:21PASS/1Windows-native skip; Node14/14PASS. Sabit resmî
Python3.13.13/Node26.13.0 image digestleri doğrulandı, networknone; kalan sahipli
container0. Yeni22Python ve14Node kimliğinin her biri uygun OS üzerinde gerçek
PASS aldı; her OS için0skip iddiası yok. Özgün68f başarısız kanıt silinmedi.

eb gerçek repo taraması2744 dosyada2 bulgu üretti: değişen source-context
nedeniyle eski tam-kaynak incelemeleri iki sentetik fixture ifadesini kapsamıyordu.
Gerçek bağımsız inceleme ve coordinator yetki kontrolü yapıldı;
[inceleme kaydı](../reviews/source-review-port-20261113.md) b0b1281 ile alındı.
Dış protected coordinator trust164→166 tek yazımla güncellendi ve byte-eşit
geri okundu; eski164 korunur. Tarama kuralı gevşetilmedi. Yeni235d gerçek repo
taraması2750dosya/0bulgu, yeni build istemci taraması20dosya/0bulgu PASS.

## Gerçek yeni laboratuvar girişimleri ve GEN-S01

İlk yeni laboratuvar **eb58432** üzerinde gerçek genesis, Clam PING/PONG,
provider/API başlatma, migrate/seed ve genuine browser login ile **8 imzalı
kaynak descriptor'ı +2 şerit** hazırladı. Sonra register manifest keşfi
`ERR_UNSUPPORTED_DIR_IMPORT` ile durdu: `defect-fields.ts:1` üzerinden
`i18n/labels` ve Vite `import.meta.glob` zinciri. Downtime/work-order aynı
bağımlılığı taşıyordu. Tam register/TLS3 çağrılmadı; kayıtlı iş vakası yürütme0.
Setup browser işlemleri gerçektir. Tarihsel15PASS/159FAIL farklı hedefte korunur.

Sahipli app/compose/extraction/Clam temizliği PASS; browser profili0, kalan
sahipli container0. Private durum ve2 kendi volume'u korundu. İlk laboratuvar
yeniden yazılmadı veya yeniden başlatılmadı.

[REGISTER-SOURCE-PIN-SEPARATION-01](../tasks/REGISTER-SOURCE-PIN-SEPARATION-01.md)
düzeltmesi6621da1, receiving235d3ea: üç canonical kaynak değeri byte-eşit saf
contract modülüne taşındı; UI re-export'ları korundu. Bağımsız kaynak incelemesi
0blocker. Tam manifest import regresyonu ve üç UI sözleşme vakası eklendi.
CI manifest kontrolü provider başlamadan çalışır. Güncel235d gerçek Node
manifest import testi1/1PASS,1,798s;30 register ailesi ve3core callback yüklendi.
Lint33,595s PASS/0hata/uyarı; typecheck15,231s PASS/0diagnostic. Tam bileşenler
**1527/1527PASS**,147dosya,304,070s; hata/pending/todo/unhandled0. Üç yeni pin
UI vakası bu toplamın içinde3/3PASS. Taze build4,900s PASS,20dosya/5.521.820byte;
CHUNK_SIZE ve STATIC_AND_DYNAMIC_IMPORT uyarıları korunur. Kaynak/client taraması
yukarıdaki gerçek sayılarla PASS; hiçbir native sonuç varsayımla taşınmadı.

İkinci **yeni ve temiz** laboratuvar aynı235d hedefinde hazırlandı:
locked restore5,748s; APIRelease33,978s; DbMigrator6,476s; locked offline npm73,168s;
taze Vite build5,798s PASS;156 artifact. Provider/private genesis/browser henüz
başlamadı olarak artifact hazırlığı kaydedildi. Frontend kapısı ve backend girdi
incelemesi geçti; ayrı attempt02 lease ile tam, filtresiz desktop+narrow manifest
ve üç strict TLS red vakası başlatıldı. TLS3/3PASS. Gerçek manifest88vaka×2proje=176.
Son gerçek sonuç **153/176tamamlanan=95PASS/58FAIL,23TAMAMLANMADI**; mevcut
1800s child sınırı1800,648s'de doldu. Masaüstü88=55PASS/33FAIL; dar65=40PASS/25FAIL,
23koşmadı. TLS3/3PASS. Set eşitliği sağlanmadığı için kabul yok. İlk üç günlük insan
kayıt vakası günlük review geçmişindeki kapalı details içeriğini açmadan görünür
bekliyor; API200, exact receipt/actor/authority/revision/hash, byte-eşit replay,
yeni key409 ve kalıcı okuma kontrolleri bu hatadan önce geçti. Ayrı235d tabanlı
REGISTER-JOURNEY-RECOVERY-01 dalında exact immutable entry gerçek summary ile
açılıp status ve iç lineage görünürlüğü denetleniyor. Diğer günlük offline,
sertifika, enrollment, navigation, dosya ve lineage hatalarının actual HTTP/matcher
teşhisi yeni kapalı raporla alınacak; mevcut driver bunları korumuyordu. Özgün
koşu değiştirilmedi/tekrar başlamadı; tamamlanmayan23vaka skip/PASS sayılmadı.
Sahipli app/compose/extraction/Clam kapatıldı, iki kendi volume'u korundu.
Bir süreçsiz sahipli geçici profilin silinmesi otomatik incelemede “blocked by
policy” ile reddedildi; ek neden dönmedi. Yeniden deneme/başka yol yok;
cleanup bu profil için **BLOCKED**, tümü PASS iddiası yok.
Ekran/HAR/trace/ham DOM veya private/native çıktı kaydedilmez.

## Açık bağımlılık ve faz kapıları

Gerçek dependency incelemesi10moderate/0high/0critical geliştirme bağımlılığı
bulgusunu tek danışma kaydına bağladı: CVE-2026-118764/GHSA-g3hv-75wg-m249.
Production omit-dev bulgu0. Kilitli parent consumer'lar varsayılan optimizationLevel1
kullanıyor; saldırgan desen+level>=2 koşulu bounded incelemede bulunmadı.
Uyumlu sıradan patched parent güncellemesi yok; audit-all exit1 ve
**OPEN_UNREMEDIATED** korunur. Zorla güncelleme, muafiyet veya release kabulü yok.

S2 DEV-020..025 için6iş/42SP kaynak/uygulama envanteri çıkarıldı; bağımsız
clause/dependency, gerçek UI ve CI kanıtı olmadan kabul edilmez. Canonical durum
**20/121 iş,103/970SP=%10,62;0/196 ayrı ekran**. S0/S1 kabulü korunur; S8 veya
release tamamlanmış ilan edilmez. Sonraki adım gerçek14vaka tanısı ve qualified
fixture üreticisi/düzeltmeler → combined freeze/tüm kapılar → bağımsız faz kabulü
→ normal push/PR/required CI → koşullar geçerse merge.

Root Graphify AST güncellemesi235d üzerinde exit0:19.516düğüm/40.311kenar,
95,762s. Zero-node/HTML büyüklük uyarıları korunur; runtime kanıtı değildir.
Kapalı JSON kanıtları ignored runtime alanında özgün hedefleriyle tutulur.

## Geliştirme devamı — ayrı S3 dalı

DEV-026/T-S3-001 Backlog27 ve aktif19.01 yeniden deneme/veri kaybı koşulları
üzerinden somut hata bağımsız doğrulandı: qualification UNKNOWN gövde/anahtarı,
sonraki401/403/404/409 veya reload sonrasında kaybolabiliyor. Bu yanıtlar ilk
komutun rollback olduğunu kanıtlamaz; sonradan yeni UUID yeni DRAFT revizyonu
üretebilir. Backend mevcut grant/CSRF ve exact receipt sınırını doğru koruyor.
DEV-005/008 önkoşullarının kabul receipt hashleri bağımsız doğrulandı.

S3-QUALIFICATION-OWNER-RECOVERY-01 yeni235d tabanlı ayrı worktree'de yazıldı:
asıl actor/tenant/context ve exact envelope korunması, güncel oturum/capability/CSRF,
inkâr sonrası belirsizliğin korunması, kesin receipt sonrası read-only sonuç ve
anlamlı UI/API/genuine regresyon kaynakları. Yeni kapsamın test/build yürütmesi
kendi sonraki büyük paket kapısına DEFERRED; ürün tamamlandı/PASS/kabul iddiası
yok. Düzeltme ef9cb2c ve receiving7722114 içinde kaynak incelemesi8QORCLEAR;
mevcut235d genuine/CI kanıtları bu hedefe taşınmadı.

Root ayrıca ayrı dalda9gerçek-host/PostgreSQL regresyon kaynağı yazdı: altı
güncel izin/appointment reddi, başka actor/tenant ve yeniden kurulan body/key
çatışmaları sonrası asıl envelope ile aynı receipt/no-new-revision/business-audit
eşitliği. Bunlar **AUTHORED_ONLY/DEFERRED**, bağımsız kaynak incelemesi CLEAR;
çalıştırılmış veya PASS sayılmaz. Yeni entity/migration/seed eklenmedi.

## Sonraki kaynak paketleri — yürütme sınırı

REGISTER-JOURNEY-RECOVERY-01 `faf7f91`: exact günlük entry disclosure ve ayrı
kapalı tanı sidecar'ı bağımsız incelendi. DIAG-S01 actualOpenAPI `/health/live`
seçimi ve DIAG-S02 multiline-message/gerçek source-bounds bulguları kaynakta
giderildi. Gerçek Node gizlilik8/8 ardından tam manifest import1/1PASS;
hata/skip/cancel/todo0. Kaynak konumu açıkça INFERRED_ERROR_STACK_FRAME; mesaj,
özelURL/body/header/DOM kaydı yok. PasifHTTP yalnız ana case context'inin statik
OpenAPIroute template/method/status/count alanlarını tutar. Gerekli14temsilci
teşhis vakası yeni temiz worker'da tek yetkili çağrıda tamamlandı:1PASS/13FAIL,
0tamamlanmayan; TLS3/3PASS. Düzeltilen günlük disclosure gerçek geçti. Yeni
sahipli app/compose/peer/Clam ve profiller0kalıntıyla kapandı; iki kendi volume'u
korundu, eski blocked profile dokunulmadı. APIRequestContext ve peer response'ları
pasif page/context event ölçümüne dahil değildir; tamamı gözlendi denmez.
Bu alt kapı bütün176, S2 veya release kabulü değildir.

S3 `afa706e`9dosyalık source paketi normal merge ile oluştu; test/build yürütmesi
DEFERRED. QOR-S01 desteklenmeyen16ReactTestingLibraryByRole seçeneği ve QOR-S02
geçFile.arrayBuffer eski edit closure'ı bağımsız bulundu. Üç dosyada düzeltme ve
4yeni davranış regresyonu yazıldı;8QOR koşulu yeniden kaynak incelemesinde CLEAR.
Normal ef9cb2c commit'i receiving7722114'e alındı. Asıl actor/
tenant/rule/vessel/body/key korunması, güncel session/capability/CSRF, exact
receipt/readback ve privateDOM sınırları kalır. AFA AST exit0:19.557/40.432,
155,467s; bu daha sonraki düzeltmelerin veya runtime'ın kanıtı değildir.

QUALIFIED-OWNING-SIGNED-FIXTURE-01 özgün235d dalında yazıldı;23bd sürümünün
QF-S01 tip-eşitliği bulgusu edac5f99 ile düzeltilip bağımsız SOURCE_CLEAR oldu,
normal receiving2f6f59a ile entegre edildi. Özgün8+2hazırlığı tek başına owning/
compliance pozitif vakalarının VERIFIED kaynaklarını üretmiyordu. Yeni açık
opt-in hazırlayıcı3FICTIONALrule/6typed witness için normalAPI, gerçek dosya/
Clam/currentness ve farklı sentetikA/B görev ayrımı kullanacak; gerçek yürütme
DEFERRED. Verified seed, rawSQL/IgnoreFilters veya gerçek insan/Marine/SME imzası
üretilmez. Strict additive descriptor, immutable config/journal ve bounded lookup
kaynakları ile testleri yazıldı; orijinal prefix/type/VERIFIED/current/uniqueness
kontrolleri korundu. Foundation policy201280c ve sınırlı crew persona0c5f8f3
entegrasyonları da bağımsız SOURCE_CLEAR; test sayıları yürütmeden PASS sayılmaz.
Üretim katalog/uzman mekanizması onayı bu yazılım hazırlığıyla oluşmaz.
