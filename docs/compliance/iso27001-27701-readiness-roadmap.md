# ISO/IEC 27001 / 27701 readiness roadmap (DEV-118)

> **Sertifika/onay iddiası değildir.** Bu belge bir hazırlık planı taslağıdır (state `DRAFT`). ISMS veya PIMS kapsamı onaylanmadı, uygulanmadı, iç denetimden veya akredite bir kuruluşun dış denetiminden geçmedi. Belge yalnız resmi dış denetim sonrasında var olur (22.01 `paragraph:122-124`). GDPR/KVKK uygunluğu da bu belgeyle ilan edilmez (`docs/security-baseline.md`).

Makine okunur kopya: [`iso27001-27701-readiness-roadmap.json`](iso27001-27701-readiness-roadmap.json) — `scripts/validate_roadmaps.py` ile doğrulanır.

İzlenebilirlik: 18.01 `sheet:Backlog/row:119` DEV-118 / T-V463-004 ("ISMS/PIMS scope, controls/evidence owners, audit readiness, provider/tenant/data boundaries; roadmap approved"); 22.01 `paragraph:29-31, 91-124`; 03.02 `paragraph:292-293`.

## Kapsam (öneri, onaysız)

| Sistem | Önerilen kapsam | Durum |
|---|---|---|
| ISMS | Ürün şirketi: bulut çalışma ortamı, geliştirme hattı ve müşteri destek sınırı (22.01 `paragraph:95-97`) | Taslak öneri |
| PIMS | POSEIDON'un işlediği müşteri ve mürettebat kişisel verisi; mürettebat sağlık/kimlik alanları ve korumalı emniyet raporları dahil (03.02 `paragraph:293`) | Taslak öneri |

## Sağlayıcı / tenant / veri sınırları

| Sınır | Durum | Not |
|---|---|---|
| Sağlayıcı | Açık | Üretim IdP, barındırma/dağıtım, nesne depolama ve yedek deposu seçilmedi (`docs/architecture.md`); her biri tedarikçi siciline girer. |
| Tenant | Authored, doğrulanmadı | Tenant/vessel/job kapsamı güvenilir membership'ten sunucuda uygulanır; query filter tek sınır değildir (`FoundationIdentity.cs`, `docs/security-baseline.md`). |
| Veri | Açık | Veri sınıfı başına envanter, amaç, hukuki dayanak, saklama ve aktarım kontrolü gerekir. AI/embedding sağlayıcısına varsayılan olarak gerçek müşteri verisi gitmez. Pilot telemetrisi yalnız takma adlı bucket taşır (DEV-072). |

## Kontrol alanları, sahipler ve kanıt (22.01 `paragraph:92-124`)

Hedef tarihler Security Owner + Product Owner tarafından roadmap onayıyla girilir; şimdilik **boş**.

| ID | Alan | Sahip rol | Hedef tarih | Durum | Mevcut kanıt | Kalan |
|---|---|---|---|---|---|---|
| ISO-GAP-01 | ISMS kapsam beyanı, varlık/hizmet envanteri | Security Owner | — | Açık | — | Kapsam beyanı ve envanter |
| ISO-GAP-02 | Bilgi güvenliği risk sicili ve işlem planı | Security Owner | — | Açık | — | Risk yöntemi, sicil, işlem planı |
| ISO-GAP-03 | Erişim: IAM/MFA/ayrıcalıklı erişim, RBAC/nesne kapsamı, görev ayrılığı | Security Owner | — | Authored, doğrulanmadı | `FoundationIdentity.cs`, `MembershipEndpoints.cs`, güvenlik baseline'ı | Üretim IdP/MFA ve ayrıcalıklı erişim gözden geçirmesi |
| ISO-GAP-04 | Güvenli SDLC: kod/bağımlılık/secret/release kontrolleri | Engineering Lead | — | Authored, doğrulanmadı | CI workflow, `docs/dependency-policy.md`, `scripts/secret_scan.py` | Yazılı SDLC politikası ve periyodik gözden geçirme kaydı |
| ISO-GAP-05 | Tedarikçi/bulut ve alt işleyen riski | Product Owner | — | Açık | — | Tedarikçi sicili, sözleşme/DPA, gözden geçirmeler |
| ISO-GAP-06 | Olay tespiti, müdahale ve bildirim | Security Owner | — | Açık | `SecurityAudit.cs` | IR planı, bildirim süreleri, tatbikat kaydı |
| ISO-GAP-07 | İş sürekliliği/DR: yedek, restore, RTO/RPO | DevOps Lead | — | Açık | `docs/operations/dr-runbook.md`, `scripts/dr_*.py` | Ölçülmüş restore testi ve onaylı RPO/RTO (DEV-071 DEFERRED) |
| ISO-GAP-08 | Privacy/PIMS: işleme envanteri, roller, saklama, aktarım | Data Protection Officer | — | Açık | güvenlik baseline'ı | Envanter, sorumlu/işleyen rolleri, saklama çizelgesi, aktarım değerlendirmesi, gerekirse DPIA |
| ISO-GAP-09 | İç denetim ve yönetim gözden geçirmesi | Security Owner | — | Açık | — | Denetim programı, ilk iç denetim, yönetim gözden geçirmesi |
| ISO-GAP-10 | Dış belgelendirme denetimi | Product Owner | — | Açık | — | ISO-GAP-01..09 kapandıktan sonra akredite kuruluş seçimi |

## Denetime hazırlık

| Kalem | Durum |
|---|---|
| İç denetim | Başlamadı |
| Yönetim gözden geçirmesi | Başlamadı |
| Dış denetim | Başlamadı |
| Genel hazırlık | **NOT_READY** |

## Onay (insan kaydı)

Roadmap onayı: Security Owner + Product Owner — **onay bekleniyor** (onaylayan: —, tarih: —). Otomasyon bu alanı dolduramaz; `validate_roadmaps.py` onaylayan ve tarih olmadan `APPROVED` durumunu reddeder.
