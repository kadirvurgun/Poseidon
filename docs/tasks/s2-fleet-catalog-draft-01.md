# S2-FLEET-CATALOG-DRAFT-01 — Kalıcı katalog taslağı ve değişmez geçmiş

18.11.2026. **AUTHORED_UNVERIFIED_NOT_ACCEPTED.** DEV-020 / T-S2-001 ve PLAN-1511-5901/5401 için iç persistence alt paketi. Önceki S2-PROFILE-FOUNDATION-01 üzerine yazıldı; tam native Fleet producer, yayınlanmış katalog veya S2 kabulü değildir. Accepted SP +0.

Dal: `foundation/s2-analysis-20261117`; ortak worktree'deki önceki analiz/tema/profil değişiklikleri korunur. Backend görevi; hedef bağımsız QA ve CI sonraki birleşik kapıda. Kullanıcı geliştirmeye devamı yetkilendirdi; test/build ertelemesi devam ediyor.

Kaynak: [118 kaynak59 +4 kaynak54 exact paragraf alıntısı](s2-fleet-catalog-draft-01.sources.json), 59.01 Sürüm2 SHA256 `daa1a931b1fa90a4a4f3418700264980ced60691673c919888448da7c1bbce7f`, paragraf88–118,507–528,534–579,915,1320–1321,1856–1873. TypeRow alanlarının taslak authoring karşılığı tutulur; yayınlı TypeRow veya accepted VesselTypeCatalogRevision üretilmez. Katalog kayıt yetkisi/onayı Marine/PO ve kontrollü kaynak sahibine aittir. Bu işin yeni tablo/codec/teknik authoring sınırları uygulama tasarımıdır; kanonik yeni FR/TC veya hukuk kuralı değildir.

| İz | Kabul odağı | Kaynak / yazılan davranış |
| --- | --- | --- |
| CAT-DRAFT-01 | Boş ve kısmi katalog taslağı saklanabilir; member mode bilinmiyorsa SHIP/TUG seçilmez | FleetCatalogDraftPolicy; nullable title/code/label/mode; kapalı JSON, NFC/trim, kimlik/kod tekrar kontrolü |
| CAT-DRAFT-02 | Profil kodları0..50 ve native JSON pointerları0..100; sınır aşımı kesilmez, pointerdan şema/izin üretilmez | Policy bounds ve syntax; gerçek writer/schema/catalog üyeliği bağı ayrı açık |
| CAT-DRAFT-03 | Tenant başına kök ve immutable revizyon; önceki revizyon aynı tenant/catalog ve exact expectedRevisiona bağlı | EF composite FK/unique/query filter; migration, root ardışıklığı ve deferred snapshot constraint |
| CAT-DRAFT-04 | Aynı tenant+commandId/actor/intent tekrarında ilk immutable sonuç; farklı intent/actor/catalog conflict | Revision içindeki durable command outcome; current head replay yerine dönmez |
| CAT-DRAFT-05 | Serializable yarışta tek yeni revizyon ve audit; başkasının bekleyen değişikliği komuta eklenmez | Writer owned transaction + xmin; PostgresFailure sınıflandırması; pending/ambient transaction reddi |
| CAT-DRAFT-06 | History EF ve DB UPDATE/DELETE/TRUNCATE ile değişmez; root hard delete yok | Guard + PostgreSQL trigger; önceki revizyon bağını cross-tenant splice testi |
| CAT-DRAFT-07 | Aktör/audit EF bağlamından; body/title/member/hash audit summaryye kopyalanmaz | AuditMetadata Tenant scope, catalog UUID, exact revision ve kontrollü DRAFT kodu |
| CAT-DRAFT-08 | Seed opsiyonel, idempotent, eksik/sentetik; operator editini korur; selectable member veya kabul üretmez | Explicit administrative fixture, boş entries, SyntheticUnverified; normal SeedImporter'a bağlanmadı |

İki tablo `fleet.CatalogDrafts` ve `fleet.CatalogDraftRevisions`. Mutable head `Version` alanı mevcut Npgsql xmin CAS depolama tokenıdır; native/public59 Version sözleşmesi gibi yeniden adlandırılmaz. Immutable `Revision` int64 ve `Id` UUID, prior/command/outcome bağları kayıpsız saklanır. Draft codec `FLEET_TYPE_CATALOG_DRAFT_V1`; hashler yalnız bu yerel draft/intent içindir, RFC8785 catalog export proofu, legal source hashı veya accepted catalog kimliği değildir.

Migration `20261117090000_FleetCatalogDraftFoundation` 17.11'de başlatılan authoring kaydıdır; 18.11 devamında tamamlanan kaynak kodu aynı kimlikte korunur. Migration SQL'i DDL/guard içindir; uygulama business yazmaları audited SaveChanges ile yapılır. Yeni fixture normal açılış/import'a eklenmediği için önceki normal seed/audit sayımları yeniden tahmin edilmedi.

## Yetki ve aktivasyon sınırı

Writer bir HTTP endpointi, role/action değerlendirme servisi veya katalog kabulü değildir. Tenant/actor sahipliği enforcementı gerçek API yetkisi yerine sunulmaz. Host registration, exact authoring action, commit-time source/authorization fence, 54.01 governance ve bağımsız Marine/PO kabulü uygulanmadan bu writer doğrudan HTTP'ye açılmayacak. Mevcut legacy endpoint/wire/receipt değiştirilmez. Accepted katalog/şema bağları hâlâ yok; FleetQualityPolicy public projectiona bağlanmaz ve taslaklardan COMPLETE üretilmez.

## Testler ve birleşik kapı

Yazılan domain testi: 6 metot / 9 tanımlı senaryo; PostgreSQL integration:10 metot. **Hepsi DEFERRED / NOT RUN.** Test projeleri mevcut; testler skip edilmedi. Shared gerçek PostgreSQL fixture migration/normal seed önkoşulunu ister; bu fixture gerçek koşum sayılmaz.

Migration designer/model snapshot henüz üretilmedi ve model uyumu doğrulanmadı. Birleşik kapıda önce exact authored migration ile generated designer/snapshot uzlaştırılmalı; aynı tabloları tekrar yaratan kör ikinci migration eklenmemeli. PendingModelChanges uyarısını bastırmak, testleri skip etmek veya eşik düşürmek çözüm değildir. Bu önkoşul çözülmeden migration/integration PASS iddiası yoktur.

Gereken sonraki kontroller: repo-local `.tools/dotnet` build/domain/persistence integration; model/migration doğrulaması ve taze PostgreSQL; row/lineage/rollback/race; ilgili seed/source/security regresyonları; bağımsız exact-target QA ve CI. UI/API contract/browser değişikliği bu alt pakette yok; native host eklendiğinde gerçek API/rol/scope/revoke/audit/offline receipt senaryoları gerekir.

Sonraki geliştirme: 54.01/59.01 katalog governance ve gerçek native schema/reference selector bağları; committed authority fence ve yetkili authoring host; sonra accepted kaynaktan mevcut Fleet row/detail tamlık tüketimi. Gerçek onay/kurulum kimlikleri dış girdidir; sentetik veriyle üretilmez.

[Faz raporu](../phases/S2-FLEET-CATALOG-DRAFT-01.md).


54.01 Rev4 SHA256 `73fcde366550ff1eb23aaebb33c8d246f4173b03b5c416a1b74736078a36c892` paragraf567–569 ve2004 ayrıca pinlendi: `fleet.reference.catalog_ref` için varsayılan yok, approved catalog revision olmadan yeni seçim/submit kapalı; tarihsel değer okunabilir. Parametre registration tasarımında acceptedOwnerBinding/nativeBinding null ve runtimeStatus NOT_REGISTERED_NOT_IMPLEMENTED beyanı vardır; bu çalışan kurulum incelemesi değildir. Draft persistence bu policy referansını kendi kendine doldurmaz veya etkinleştirmez.
