# Canonical S6 — sertifika/OCR ve Tug kaynak devamı

**Genel kabul %10,62 — 103 / 970 SP, 20 / 121 iş; ayrı ekran kabulü 0 / 196.** S0/S1 kabul edildi. S2–S6 kaynak geliştirmesi kabul yüzdesi üretmez.

Şu anda S6 üzerinde çalışılıyor. Canonical S6 **9 iş / 87 SP**; bu paketin seçimi sertifika/OCR ve Tug devamı olmak üzere **6 iş / 58 SP**. Bu sayı tamamlanan iş veya kabul değildir; S6 kabulü **0 / 87 SP**. Fuel/Bunker/BDN/ROB, Port Activity/SOF ve Downtime/LoH için kalan 3 iş / 29 SP bu seçimde yok.

Kaynak checkpoint `e3e5c739efdec16a25ca511e9c4a5d724402cbf4`; kayıt `2026-11-05T20:38:04.988646Z` (UTC).

- Gerçek vessel certificate kayıt/sürüm/geçmişi, mevcut crew credential kaynaklarından yetkili görünüm, kapsam filtreleri ve issuer/source/document bağları yazıldı. Takvim gözlemi ile hukuki doğrulama ayrı; eksik/belirsiz veri REVIEW_REQUIRED kalır.
- Renewal taslaklarında owner/action/due/survey/replacement/evidence pinleri ve immutable geçmiş var. Tamamlama/supersession kapısı açık; yeni belge bağlantısı eski sertifikayı kendiliğinden değiştirmez.
- Kaynak görevlendirmesi bulunan Human için ayrı immutable assertion kaydı yazıldı. İnsanın seçtiği beyan değeri aynen korunur; kayıt değerlendirmesi RECORDED_UNASSESSED, sertifika nitelendirmesi REVIEW_REQUIRED kalır. Passport, kayıt/event kimliklerini içeren scalar özeti, güncel/tarihsel ayrımını ve kaynak engellerini gösterir; Human actor/role/authority kimliklerini ve rationale alanını dışarıda bırakır. Literal Verified beyanı gösterilebilir; bundan hukuki nitelendirme, Ready durumu veya durum geçişi onayı çıkarılmaz.
- OCR için mevcut yetkili dosya baytlarını kullanan kapalı varsayılanlı provider protokolü ve seçili vessel alanlarını kullanıcının açık onayıyla kaydetme yazıldı. Crew tarafı review-only; güvenli owning confirm ve tam crew aday UI akışı hâlâ eksik. Üç sentetik masked örnek scorer yazılımını besler; gerçek corpus, provider koşusu veya doğruluk sonucu yok.
- Tug intake/board için gruplu kurumsal form, actor/tenant sınırı, exact retry ve güvenilir yanıt decode devamı; S5 passport için aynı fresh SQL/audit sınırında gerçek vessel certificate kaynağı bağlantısı entegre. Sertifika merkezi/yenileme arayüzü kaynak düzeyinde entegre; gözlenmiş UI sonucu yok. OCR review paneli kaynak düzeyinde bağlı; çalıştırılmadı.

**Yeni test/build/typecheck/lint/contract-generation/browser/CI çalıştırılmadı: DEFERRED / NOT RUN.** Test kaynakları ürün koduyla yazılıyor; daha büyük PR hazırlanırken koşulacak. 105 mutation template / 97 critical template / 107 operation yalnız kaynak envanteridir; PASS veya ölçülmüş coverage değildir. Bu bağımsız rapor uygulama QA’sı değildir.

Kaynak yetkili certificate verification/approval ve durum geçişi, gerçek provider/processing/privacy/retention/storage/model/prompt ayarları, uzman ground truth ve pilot kararları açık. Human assertion içinde seçilen literal State korunur ve gösterilebilir; kayıt değerlendirmesi RECORDED_UNASSESSED, sertifika nitelendirmesi REVIEW_REQUIRED kalır. Bu kayıt hukuki nitelendirme, Ready durumu veya durum geçişi onayı sağlamaz. S5’in 8 iş / 69 SP kaynak paketi ve eski kanıtlar tarihsel olarak aynen korunur; authoritative issuer catalog, kritik gereksinim evreni, manual RuleOverride ve owning lookup açıkları kapanmış sayılmaz.

Yeni PR, provider aktivasyonu veya preview yayını yok; eski F preview değişmedi. Sonraki adım açık certificate/crew/UI kriterlerini sürdürmek ve kalan29SP Fuel/Port/Downtime işleri için kaynak, bağımlılık ve lease seçimini yapmak. DEV102’nin DEV055 bağımlılığı ve owning work-order lookup’ları hâlâ açık; üç işin tamamının hazır olduğu iddia edilmez. Actual PR aşamasında toplu doğrulama ve bağımsız exact-target QA/CI kapısı çalıştırılacak.

[Güncel durum](../project-status.md) · [S5 tarihsel kaynak checkpoint’i](S5-COMPLIANCE-FOUNDATION-20261104.md) · [Canonical kayıt](../project-status.json)

## Kontrollü kaynak ve tam S6 envanteri

Aktif [poseidon-v46.3-20261030](../sources/active.json). Özgün 18.01 Backlog SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`; bu raporda yalnız rows44–47,82–83,101–103 doğrudan okundu. Özgün `source_status`, global121 task kaydı, sprintler, kabul kanıtları ve tarihsel verification değişmedi. Seçilen58SP’nin87SP’ye oranı tamamlanma yüzdesi değildir.

| İş / trace / SP | Kaynak locator | Literal kabul kriteri | Bağımlılık | Bu checkpoint |
|---|---|---|---|---|
| DEV-043 / T-S6-001 / 13 | sheet:Backlog/row:44 | VALID/DUE/SURVEY_WINDOW/EXPIRED/REVIEW_REQUIRED state; issuer/source/verification; vessel/crew scope filtreleri. | DEV-020, DEV-030 | Kaynak geliştirmesi, açık kriter; kabul yok |
| DEV-044 / T-S6-002 / 8 | sheet:Backlog/row:45 | Owner/actions/due/survey window/new certificate evidence/verification; old certificate superseded history korunur. | DEV-043 | Kaynak geliştirmesi, açık kriter; kabul yok |
| DEV-045 / T-S6-003 / 13 | sheet:Backlog/row:46 | Belge tipi/no/issuer/dates/vessel-person çıkarılır; confidence gösterilir; low confidence vurgulanır; user confirm olmadan save/state mutation yok. | DEV-018, DEV-043 | Kaynak geliştirmesi, açık kriter; kabul yok |
| DEV-046 / T-S6-004 / 8 | sheet:Backlog/row:47 | Masked sample set; field-level accuracy/correction rate ölçülür; critical field hallucination/authority bypass yok; model/prompt version loglanır. | DEV-045, DEV-009 | Kaynak geliştirmesi, açık kriter; kabul yok |
| DEV-081 / T-TUG-002 / 8 | sheet:Backlog/row:82 | Job ID; assisted vessel; job type; time window; area; required tug count/BP; priority; owner; audited create. | DEV-080 | Kaynak geliştirmesi, açık kriter; kabul yok |
| DEV-082 / T-TUG-003 / 8 | sheet:Backlog/row:83 | Job board shows planned window, assigned tugs, BP, readiness, conflict, priority and next action. | DEV-081 | Kaynak geliştirmesi, açık kriter; kabul yok |
| DEV-100 / T-OPS2-004 / 13 | sheet:Backlog/row:101 | Consumption/delivery/transfer/ROB; fuel type; supplier/BDN; quantity; sulphur/viscosity; discrepancy; compliance/verification/evidence; applicability-aware. | DEV-097, DEV-099, DEV-030 | Bu kaynak seçiminin dışında |
| DEV-101 / T-OPS2-005 / 8 | sheet:Backlog/row:102 | Structured event timeline; cargo/draught/tug/pilot/delay; Letter of Protest/evidence; voyage link; finalization; not Port OS. | DEV-097 | Bu kaynak seçiminin dışında |
| DEV-102 / T-OPS2-006 / 8 | sheet:Backlog/row:103 | Start/end/duration/cause; defect/WO/event links; operational impact; Loss of Hire candidate; claim evidence; verified closure. | DEV-055, DEV-099 | Bu kaynak seçiminin dışında |

## Kaynak davranışı, bağımsız raporlama sınırı

Bu rapor bağımsız source progress kaydıdır. Uygulama QA’sı, model parity, build, test, runtime güvenliği veya sprint kabulü üretmez. Root ürün kodunu sürdürürken taslak ayrı ignored dizinde hazırlanır; final root handoff’u ve içerik/hash incelemesi sonrası tracked raporlara uygulanır.

- [Certificate owning core](../tasks/S6-CERTIFICATE-CORE-01.md): vessel draft/revision/history, scoped existing crew projection, ayrı calendar/qualification, current file/role fences, audited EF ve optional incomplete SyntheticUnverified fixture. Clean transport doğrulanmış belge değildir.
- [Renewal draft](../tasks/S6-RENEWAL-DRAFT-01.md): exact original/replacement/file pinleri, eligible Human owner, unknown taslak ve immutable geçmiş. Draft tamamlama veya supersession değildir.
- [Assigned Human assertion](../tasks/S6-CERTIFICATE-ASSERTION-01.md): default-empty exact operator appointment, ayrı attributed ledger/receipt ve RECORDED_UNASSESSED metadata. Özgün DD2316 rows39/1754/1755 hücreleri bu raporda da bounded okundu: V/X görevlendirme ve kontrollü geçiş ister; BF/BG/BQ boş, BO REVIEW_REQUIRED. Hukuki geçiş kaynağı veya tamamlanma onayı uydurulmadı.
- [OCR source contract](../tasks/S6-CERTIFICATE-EXTRACTION-01.md): default-unavailable bounded HTTP, protected current bytes ve metadata, açık selected-fact confirmation, exact idempotent receipt, versioned scorer. Crew review-only ve üç synthetic masked software örneği gerçek operasyonel kalite kanıtı değildir.
- [Tug continuation](../tasks/s6-tug-intake-ui-continuation-20261105.md): gruplu enterprise alanları, senkron dirty/save/navigation guard, actor/tenant-owned retained command ve strict current response. Operational assignment/availability/lifecycle kabulü ayrı kalır.
- [Passport owning projection](../tasks/s6-passport-certificate-projection-20261105.md): aynı fresh SQL içinde ReturnedVisible, bağımsız25-row paging ve read-side audit sonrası currentness. AUTHORIZED_PAGE_ONLY boş olsa dahi tam catalog veya kritik evren iddiası değildir.
- [Certificate UI source contract](../tasks/s6-certificate-experience-01.md): gerçek center/renewal route ve current server capabilities, shared enterprise form/action grupları, ayrı immutable receipt/current-head akışı; runtime geometry/contrast/browser kabulü ölçülmedi.

## Exact verification sonuçları ve kalan kapı

| Kontrol | Bu S6 kaynak paketindeki sonuç |
|---|---|
| Test / backend-domain-real PostgreSQL / component | DEFERRED / NOT RUN |
| Build / typecheck / lint / generated contract / migration-model parity | DEFERRED / NOT RUN |
| Browser / signed provider / offline journeys | DEFERRED / NOT RUN |
| Yeni provider veya private runtime aktivasyonu | YAPILMADI |
| Bağımsız implementation QA / CI | YENİ SONUÇ YOK; daha sonraki actual PR kapısı |
| Yeni kabul / ölçülmüş accuracy / coverage PASS | 0; iddia edilmedi |

Eski S0/S1 ve S2 scoped actual receipts kendi exact commit/kapsamlarında kalır. Eski82/85/126 veya yeni source operation envanteri current runtime PASS olarak taşınmaz. S5 final productf905 / metadata79518 kaynak ve bounded correction review tarihçesi değiştirilmez. AST graph yenilemesi yapılırsa yalnız derived navigation kaydıdır, hiçbir QA kapısı yerine geçmez.

## Açık kabul kriterleri ve sonraki kod adımı

- Source-authorized exact certificate verification/approval and legal transition sources remain unresolved; an attributable assertion is not verified evidence, legal validity, readiness or a safety approval.
- Renewal completion and supersession remain gated; a draft replacement never mutates or silently supersedes the original certificate.
- Crew OCR is purpose-gated review-only; safe owning crew confirmation and full crew candidate UI remain unavailable.
- Real provider deployment, processing/region/privacy/retention/storage/model/prompt/secret references and exact operator grants are not activated.
- Three synthetic masked software examples are not a real operational golden corpus, expert ground truth, executed model evaluation or measured accuracy/correction rate.
- Tug continuation addresses scoped editor/layout/response reliability; assignment, availability and D-STATE-01 lifecycle/pilot gates remain separate.
- DEV-100 Fuel/Bunker/BDN/ROB, DEV-101 Port Activity/SOF and DEV-102 Downtime/LoH total29SP are outside the selected6/58SP checkpoint.
- S5 manual RuleOverride, authoritative issuer/type catalog and complete-critical-universe proof remain open; owning Voyage/port/officer/WO/evidence and real operator appointments/policies remain unresolved.
- Generated contract/model parity, build, backend/domain/real PostgreSQL, UI/component, browser/offline, independent exact-target QA and CI must run at the actual larger PR gate.

- Final ürün kaynağı e3e5c739efdec16a25ca511e9c4a5d724402cbf4 / treebf63bd168ea8ece1b4f80beb3714d3bf086e4ea8; status metadata commit’i ayrı olur, ürün hedefini değiştirmez.
- 79518 bazına göre115 dosya,+5617/-96 yalnız kaynak değişimi boyutudur; completion veya test ölçütü değildir. Root clean worktree ve tracked whitespace exit0 bildirdi; bunlar ürün doğrulama kapısı değildir.
- Human assertion UI2cbf1209 ve OCR33ace source correction normal merge ile entegre. Üç yeni gerçek-host certificate/renewal/extraction journey kaynağı merkezi registry’de; çalıştırılmadı, required private fixture/provision/provider activation yapılmadı.
## Dar bağımsız kaynak incelemesi

Root-only Tug edit/retry/navigation ve scalar passport/owning ReturnedVisible sınırı manuel okundu; bu sınırlı incelemede gösterilmiş yeni authority/veri-kaybı bulgusu yok. Bu ifade uygulama QA PASS, kapsamlı defect-free iddiası veya canonical kabul değildir. Narrative textarea ile domain kontrol politikası uyumu gözlemi kaynak kararıyla düzeltilip `6e0e06533817c40293bb031404e6e127c98a407b` üzerinde dar olarak tekrar okundu: **SOURCE_CORRECTION_REVIEWED_ONLY**. Yeni regression kaynakları çalıştırılmadı.

Receipt `.runtime/s6-certificates-20261105/qa/root-tug-passport-source-review.json`, SHA256 `4352904d7c64992bcd572d8863876cc866f26438206024e1bc8db288117aed24`; orijinal gözlem ve dar düzeltme birlikte korunur.

## Ayrı OCR kaynak bulgusu ve dar düzeltme

OCR bileşeninde ilk owning receipt sonrası session401/403 ile pending komutun stale closure yüzünden silinmesi bağımsız UI author tarafından kaynakta bulundu ve root tarafından doğrulandı. Bu bulgu önceki root-only Tug/passport incelemesinin kapsamı dışındadır; runtime koşusunda bulunmuş gibi kaydedilmez.

Düzeltme `33ace2d90f04886ae9d97b96d4f4b75ee64f9187`; durum **SOURCE_CORRECTION_REVIEWED_ONLY**. Root tarafından iletilen bağımsız UI tekrar okuması receiptReceived bayrağının post-session await öncesinde konduğunu; sonraki401/403/network ve önceki unknown sonrasındaki4xx boyunca özgün key/body/guard korunmasını doğruladı. Üç fault, alternate owner privacy ve exact body/key regression kaynakları yazıldı; çalıştırılmadı. Bu dar source correction review tüm OCR uygulaması QA PASS veya canonical kabul değildir.

## Derived navigation graph

Root tarafından bildirilen AST-only navigation kaydı: `{"HTML": "SKIPPED_GT5000_THRESHOLD", "communities": 1096, "edges": 33442, "exit_code_reported_by_root": 0, "is_validation_test_build_or_securityQA": false, "mode": "AST_ONLY_NO_LLM_NO_API_COST", "nodes": 17098, "source_head": "e3e5c739efdec16a25ca511e9c4a5d724402cbf4", "source_tree": "bf63bd168ea8ece1b4f80beb3714d3bf086e4ea8", "state": "AST_ONLY_COMPLETED_NAVIGATION_NOT_QA", "zero_node_config_noncode_warnings": 731}`. Test/build/security QA değildir; eski kanıtlar bu head’e yeniden atanmadı.

Sonraki kaynak geliştirmesi bu açık kriterleri daraltır; Fuel/Port/Downtime seçimi öncesi kaynak/bağımlılık ve lease kaydı güncellenir. Actual PR hazırlığında bütün deferred kapılar ve bağımsız hedef-commit QA gerçek sonuçla kapanmadan canonical completion, merge veya yayın kabulü verilmez.
