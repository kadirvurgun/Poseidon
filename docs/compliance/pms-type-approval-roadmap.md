# PMS type approval roadmap (DEV-117)

> **Sertifika/onay iddiası değildir.** Bu belge bir plan taslağıdır (state `DRAFT`). Hiçbir klas kuruluşu POSEIDON'u incelemedi, ön değerlendirmedi veya bir belge vermedi. Satış ve ürün materyali, resmi bir karar belgesi düzenlenmeden onay ifadesi kullanmaz (22.01 `paragraph:78`).

Makine okunur kopya: [`pms-type-approval-roadmap.json`](pms-type-approval-roadmap.json) — `scripts/validate_roadmaps.py` ile doğrulanır.

İzlenebilirlik: 18.01 `sheet:Backlog/row:118` DEV-117 / T-V463-003 ("Target class; applicable approval scheme; gap list; required PMS features/test evidence; owner/timeline; no certification claim before award"); 22.01 Certification Proof & Finalization Roadmap `paragraph:26-28, 35-41, 42-90`; 03.02 `paragraph:23, 254`. NOZZLE kamu materyali (03.01 `NOZZLE Kaynakları/row:18-19`) yalnız rakip bağlamıdır, gereksinim kaynağı değildir.

## Kanıt düzeyi (22.01 `paragraph:35-41`)

Mevcut düzey en fazla **Seviye 2 — Implemented** (kod derlenir, DB migrate olur, otomatik testler geçer) ve bu, S0–S8 kabul edilen paketler için geçerlidir. Seviye 3 (QA/SME doğrulaması), 4 (pilot), 5 (dış güvence) ve 6 (resmi karar) yoktur.

## Hedef klas ve şema

| Alan | Durum |
|---|---|
| Hedef klas | **Seçilmedi.** Adaylar: DNV, ClassNK, Türk Loydu. Kural: önce pilot müşterinin klası, sonra ticari öncelik (22.01 `paragraph:50`). Karar: Product Council (CA-01). |
| Uygulanacak şema | **İncelenmedi.** Seçilen kuruluşun kendi yayımlanmış belgeleri CA-02'de Class/Technical SME tarafından okunur; bu roadmap şema gereksinimi uydurmaz. |
| Roadmap onayı | Product Council — **onay bekleniyor** (onaylayan: —, tarih: —) |

## Aşamalar (22.01 `paragraph:44-79`)

| Aşama | Çıktı | Kapı sahibi | Durum |
|---|---|---|---|
| CA-01 Target class selection | Hedef klas listesi | Product Council | Başlamadı |
| CA-02 Scheme review | Onay gereksinim matrisi (PMS kapsamı, veri bütünlüğü, change control, test, siber, yedek, audit) | Class/Technical SME | Başlamadı |
| CA-03 Product gap | Klas gap backlog'u | PO + Engineering | **Taslak gap listesi (aşağıda)** |
| CA-04 Evidence pack | Kontrollü kanıt paketi | QA | Başlamadı |
| CA-05 Pre-assessment | Bulgu listesi | Class/RO | Başlamadı |
| CA-06 Remediation | Kapanmış bulgular | Engineering/QA | Başlamadı |
| CA-07 Formal audit/test | Denetim/test raporu | Class/RO | Başlamadı |
| CA-08 Award | Kuruluşun düzenlediği belge | Yetkili klas kuruluşu | Başlamadı |

## Gap listesi ve PMS özellik/test kanıtı (22.01 `paragraph:80-90`)

Hedef tarihler Product Council tarafından CA-01 sonrasında girilir; şimdilik **boş**. Kanıt bağlantıları repo içi yazılmış ama bağımsız doğrulanmamış (`EVIDENCE_AUTHORED_UNVERIFIED`) çıktılardır.

| ID | Alan | Sahip rol | Hedef tarih | Durum | Kanıt | Kalan |
|---|---|---|---|---|---|---|
| PMS-GAP-01 | Ekipman hiyerarşisi ve tekil ekipman kimliği | Technical Product Owner | — | Authored, doğrulanmadı | `EquipmentHierarchyPolicy.cs`, `docs/tasks/equipment-hierarchy-policy-01.md` | CA-02 kriterleri; QA kaydı |
| PMS-GAP-02 | Planlı/plansız bakım yaşam döngüsü, tekrarlayan bakım | Technical Product Owner | — | Authored, doğrulanmadı | `MaintenancePlanningPolicy.cs` | Plansız bakım eşlemesi |
| PMS-GAP-03 | Çalışma saati / ölçüm noktası bütünlüğü ve izlenebilir kaynak | Technical Product Owner | — | Authored, doğrulanmadı | `PmsMeasurementPolicy.cs` | Sayaç bütünlüğü kanıt paketi |
| PMS-GAP-04 | İş emri erteleme/onay kontrolleri, gerekçe ve audit | Technical Product Owner | — | Authored, doğrulanmadı | `MaintenancePostponementPolicy.cs`, `WorkOrderExecution.cs` | Azami uzatma politikası SME tarafından girilene kadar UNCONFIGURED |
| PMS-GAP-05 | Kritik ekipman ve kritik görev | Class/Technical SME | — | Açık | `CriticalStatePolicy.cs` | Onaylı kritiklik kataloğu yok (uzman kararı, seed edilmez) |
| PMS-GAP-06 | İş planı/revizyon geçmişi ve filo standart iş havuzu | Technical Product Owner | — | Açık | — | Filo standart iş havuzu uygulanmadı |
| PMS-GAP-07 | Tamamlama kanıtı, doğrulayan ve değişmez bakım geçmişi | QA Lead | — | Authored, doğrulanmadı | `WorkOrderExecution.cs`, `docs/tasks/audit-trace-01.md` | Bağımsız QA doğrulaması |
| PMS-GAP-08 | Arıza/safeguard/kısıt bağlantısı ve readiness | Technical Product Owner | — | Authored, doğrulanmadı | `DefectLifecyclePolicy.cs` | Readiness yayılım test kanıtı |
| PMS-GAP-09 | Yedek/restore, kullanıcı yetkisi, audit ve kontrollü yazılım değişikliği | DevOps Lead | — | Açık | `docs/operations/dr-runbook.md`, `scripts/dr_rehearsal.py`, `SecurityAudit.cs` | Ölçülmüş restore provası ve imzalı rollback (DEV-071) DEFERRED |
| PMS-GAP-10 | Versiyonlu migration ve regresyon test kanıtı | Engineering Lead | — | Authored, doğrulanmadı | CI workflow, EF migrations | Release commit'ine bağlı kontrollü regresyon kanıtı |

## Açık insan kapıları

- CA-01 hedef klas kararı (Product Council), CA-02 şema okuması (Class/Technical SME), roadmap onayı — hiçbiri otomasyonla kapatılamaz.
- Kritiklik kataloğu ve erteleme azami uzatması uzman parametresidir; tanımlanana kadar UNCONFIGURED/REVIEW_REQUIRED kalır.
