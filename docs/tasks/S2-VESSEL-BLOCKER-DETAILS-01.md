# S2-VESSEL-BLOCKER-DETAILS-01

Durum: **AUTHORED / TEST EXECUTION DEFERRED / NOT ACCEPTED**. DEV-020/T-S2-001
vessel profile ile DEV-039/T-S5-001 onboarding tüketicisinin kaynak uyumlu blocker
detayıdır; canonical görev veya story point eklemez.

## Kaynak ve sınır

- Aktif 18.01 Backlog `sheet:Backlog/row:21`, SHA256
  `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`:
  IMO, flag, class, ship type, GT, build/trading/cargo context versioned; provenance
  ve freshness görünür.
- Aktif 18.01 Backlog `sheet:Backlog/row:40`, aynı SHA: onboarding completeness,
  eksik kritik alan `REVIEW_REQUIRED`; bağımlılıklar DEV-020 ve DEV-027.
- Yeni 59.01 Fleet master `paragraph:55`, SHA256
  `daa1a931b1fa90a4a4f3418700264980ced60691673c919888448da7c1bbce7f`:
  eksik kaynakla taslak kaydedilebilir; yalnız etkilenen akış bloke olur; belirsiz
  class/flag/tonaj `REVIEW_REQUIRED` kalır. Her blocker `subjectId`, `fieldPath`,
  `reasonCode` ve gereken policy/evidence türünü döndürür.

Bu alt paket completeness ve açıklama sağlar. Vesselı ACTIVE yapmaz, operasyonel
atama veya requirement uygunluğu üretmez, verification/approval/evidence kabulü
oluşturmaz. `Known` beyanın doğrulanmış olduğu iddia edilmez.

## Kabul ölçütleri

| Kimlik | Ölçüt |
|---|---|
| VBD-01 | Her eksik/unknown onboarding alanı exact vessel subjectId, izinli fieldPath, gerekçe kodu ve çözüm türüyle ayrı blocker döndürür. |
| VBD-02 | Missing → `REQUIRED_DATA_MISSING/REQUIRED_DECLARATION`; Unknown/invalid known → `REQUIRED_DATA_UNKNOWN/POLICY_OR_EVIDENCE`; ayrım sessizce No/NotApplicable/Ready olmaz. |
| VBD-03 | Profil yokluğu `profile/PROFILE_MISSING/PROFILE_DECLARATION`; bütün beyanlar dolu olsa da `profile/VERIFICATION_NOT_ESTABLISHED/QUALIFIED_EVIDENCE` kalır. |
| VBD-04 | Eski aggregate blocker kodları passport composition için korunur ve structured detayların distinct reason kümesiyle birebirdir. |
| VBD-05 | Client exact closed contractı, foreign subject, bilinmeyen field/reason/resolution, eksik/duplicate ve field-state uyumsuzluğunu reddeder. Subject ID UI'da gösterilmez. |
| VBD-06 | UI blockerı alan adı ve gerekli çözümle açıklar; yeni metinler 17 katalogda tamdır ve RTL katalog bütünlüğünü bozmaz. |

## Yazılmış kontroller

- Domain: Missing/Unknown/invalid/full declaration/profile-missing structured
  mappingi ve qualified-evidence blockerı.
- Gerçek API/PostgreSQL: saved profile revisionında exact vessel subject,
  field/reason/resolution; tam beyanın yalnız verification blockerı taşıması.
- Client contract: blocker/aggregate küme eşliği, alan state eşliği, foreign ve
  bilinmeyen değer reddi.
- React: sekiz eksik declaration çözümü, qualified evidence, TR/EN görünüm ve
  subject kimliğini DOM'a taşımama.

Kullanıcının faz sonu test kadansı nedeniyle bu authoring adımında test, build,
lint, typecheck ve Playwright çalıştırılmadı. Bir sonraki büyük kapı aynı hedefte
tam backend/frontend/browser kontrolleri, bağımsız QA ve required CI gerektirir.
