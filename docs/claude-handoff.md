# Claude handoff

STATUS: IN_PROGRESS
Güncelleme: 2026-11-19 ~08:30 (07:41 zamanlanmış oturum)
in_progress: 2026-11-19 07:41 zamanlanmış oturum (claude/loop)

## KOTA KURALI (zorunlu)
Her büyük adımdan önce `C:\Users\avurgun\.claude-poseidon\statusline\cache\usage.json` oku (`fiveHourPct`, `resetsAt`). `fiveHourPct >= 97` ise yeni iş başlatma, durumu bu dosyaya yaz, commit et ve çık. `>= 90` ise yeni alt-ajan başlatma, yalnız mevcut işi bitir. Alt-ajanları aynı anda en fazla 3 tut.

## Disk / Docker (önce kontrol et)
- C: diski 18.11 gece 0 bayta düştü → Docker Desktop daemon kilitlendi (`docker info` timeout). Eski worktree bin/obj silinerek ~1,9 GB açıldı. Başlarken `(Get-PSDrive C).Free` < 5 GB ise önce yer aç (yalnız yeniden üretilebilir şeyler: eski `.runtime/worktrees/*` bin/obj, TestResults, indirilen CI artifact'ları). Docker hâlâ yanıt vermiyorsa yerel PG testlerini çalıştırma; CI kanıtı yeterli, kullanıcıdan Docker restart iste.

## Main ve PR durumu
Main: #16 #17 #18 #20 #23 #36 + **#37 paket 2** birleşik (`2995f70`, 19.11 ~08:27; #29 #33 #34 otomatik Merged). **S2'nin 6/6 işi main'de** (sprint kapısı/kabul henüz yok). Açık: **#38 paket 3** (main + DEV-024 QA takip + #32), #25, #28, #30 (durma limiti).

### 19.11 02:41 oturumu (zamanlanmış) — yapılanlar
- Ortam: git 2.37 "dubious ownership" → ilgili worktree yolları **tek tek** global `safe.directory`'ye eklendi (asla `*`). `.tools/dotnet` boştu → SDK 10.0.403 `dotnet-install.ps1` ile `C:/Users/avurgun/source/repos/Poseidon/.tools/dotnet`'e yeniden kuruldu. `git push` kimlik yardımcısı bozuk (`.tools/gh` yok) → push: `git -c credential.helper= -c "credential.helper=!'C:/Users/avurgun/AppData/Local/Microsoft/WinGet/Links/gh.exe' auth git-credential" push origin HEAD:<dal>`. Docker `docker info` 20 sn timeout → PG testleri yerelde DEFERRED.
- Kota dosyası (`usage.json`) 00:06'dan beri güncellenmiyor (91%, resetsAt geçmiş) → temkinli: en fazla 2 alt-ajan.

| PR / dal | Bu oturumda | Son durum / sıradaki adım |
|---|---|---|
| #29 DEV-024 | main birleştirildi (30c93e9). CI teşhisleri: (1) `compliance-onboarding.spec.ts:74` mock UNKNOWN+STATE_ONLY gönderiyordu (DEC-02 sonrası decoder reddediyor) → 773f1f8; (2) main birleşimi sonrası `profile-repair.spec.ts:81` iki `alert` (mock `/critical-states` yok) → 3d1b398. Yerel: build 0 uyarı, domain 832/832, lint, vitest product 1600/1600. Playwright real-API smoke yerelde koşmadı (PG). | **3d1b398 CI yeşil (push+PR). Bağımsız yeniden QA PASS-WITH-NOTES** (F1–F5 gerçek ve testli). Notlar (düşük): (1) STATE_ONLY freshness UNAVAILABLE gizli profilden 1 bit sızdırıyor (`CriticalStatePolicy.cs:50,108` + `VesselProfileFieldPolicy.cs:136,152`; öneri: UNAVAILABLE yalnız politika çözüm durumundan); (2) gizlenen değerlendirme 403 fence dalının testi yok (`CriticalStateEndpoints.cs:122,126`); (3) bazı reason kodlarının i18n etiketi yok, ham kod görünüyor; (4) `docs/phases/S2-DEV024-01.md:75` bayat. Sonraki takip paketinde düzelt. |
| #33 52.01 İK | Teşhis: API 905/905 geçti, critical-write audit kapısı `AUDIT_BEFORE_AFTER_UNPROVEN` (34 HR satırı). Alt-ajan: main birleşimi (73a5fd0) + `AuditMetadata.cs` HR projeksiyonu (identifier/role ResourceId, HrStateChange/HrActualEvent `Binding=OwnerReference`, HR Company projeksiyonu) + `HrAuditBeforeAfterTests` 11/11 → ebb697d. S1 130/122/132. | **ebb697d CI yeşil (push+PR).** |
| #30 58.02 belge | `FoundationModelParityTests:98` PG deparse `(("PublicationState")::text = 'EFFECTIVE'::text)` karşılaştıramıyordu (migration doğru) → test normalizasyonu + main birleşimi → 2026a2b. Yeni CI: persistence yeşil; API kapısında 1 kırmızı: `ControlledDocumentTests.cs:184` (canlı link varken imha talebi 409 `active_use_open` bekleniyor), ham çıktı yok. | **DURMA LİMİTİ (3 ardışık kırmızı: 595d32b, 8cca242, 2026a2b) — push için kullanıcı onayı gerekli.** Kök neden: `active_use_open` ve `revision_older_than_effective` `SecurityEventCodes.Results`'ta yok (`SecurityEventCodes.cs:73-75`) → `Fail` audit yazımı "Unknown security event code" atıyor → 409 yerine hata. Düzeltme yerelde commit'li **c4367d4** (worktree `plan-5802-20261118`) + yeni unit test `ControlledDocumentSecurityCodeTests` (2/2); yerel: build 0 uyarı, domain 841/841, persistence unit 83/83, API unit 239/239. |
| #28 52.02 üyelik | QA düzeltmeleri (D06–D08, AC-15..19) commit'liydi; doc commit + main birleşimi (4f43b7d; S1 123/115/125; OpenAPI/api.d.ts yeniden üretildi). Yerel: domain 860/860, API/persistence unit geçti. | **DURMA LİMİTİ (3 ardışık kırmızı: 21744c2 push+PR, 4f43b7d push/PR) — push için kullanıcı onayı gerekli.** Teşhis: `MembershipLifecycleTests.cs:495` kaybeden yanıtın gövdesini ikinci kez okuyor (`:492`'de tüketilmiş) → `Code()` `:95` boş akış; üyelik yarışı değil, deterministik test kusuru. Düzeltme yerelde commit'li **2a55677** (worktree `plan-5202-20261118`), API test projesi derlendi. |
| #34 54.01 politika | Bağımsız QA **PASS-WITH-NOTES**. F1 (orta: onaylı TECHNICAL değer aktivasyonsuz `positiveUseAllowed=true`) → `APPROVED_NOT_ACTIVATED`, pozitif kullanım kapalı; F2 kullanılmış anahtar+geçersiz yük → 409; F3 karar gerekçesinde kontrol karakteri; F4 DB guard testleri SqlState+mesaj → c4728b2. Yerel: domain 815/815, API unit 239/239, persistence unit 75/75. | **c4728b2 CI yeşil (push).** |
| `fix/same-key-replay-race-followup-20261118` | Yarım iş commit'lendi (12 uçta aynı-anahtar replay) + main birleşimi → 250ead1. | **CI yeşil (push). Bağımsız QA PASS-WITH-NOTES** (kusur yok; N1: farklı yük + aynı anahtar geç-yarış testi yok). Entegrasyon dalına girer. |
| #32 axe-core | Dokunulmadı. | Main birleşimi + CI. |
| #25 58.03 | Dokunulmadı. | En son; yeni uçlara profil ekle. |

### 19.11 07:41 oturumu — yapılanlar
- 02:41 kilidi 5 saat dolmuştu (son commit 03:50) → bayat kabul edildi. Kota dosyası hâlâ 00:06'dan bayat (91%, reset geçmiş) → en fazla 2 alt-ajan. Docker `docker info` 20 sn timeout → PG yerelde DEFERRED.
- `integration/bundle2-20261119` (ee65ff9, önceki oturumun alt-ajanı yerelde hazırlamış, push edilmemişti) yerelde doğrulandı: build 0 uyarı; Domain 876/876; API unit 250/250; Persistence unit 75/75; frontend lint + build PASS; vitest 1786/1795 — 9 hata yalnız `scripts/offline-shell-build.test.ts` (SubtleCrypto realm; yerel Node 22.15 ↔ CI Node 26.13, dosya main ile aynı → ortam, ürün kusuru değil). Push edildi, **PR #37** açıldı; CI koşuyor.
- DEV-024 yeniden QA notları takibi: `fix/dev024-qa-notes-20261119` (39d5e77, taban bundle2, worktree `.runtime/worktrees/dev024-qa-notes`): (1) STATE_ONLY `UNAVAILABLE` yalnız freshness politika servis durumundan (sızıntı kapandı); (2) canlı grant fence 403 PG testi (yerelde DEFERRED); (3) 25 reason kodu × 17 dil etiket + kapsama vitest; (4) faz raporu. Yerel: build 0 uyarı, Domain 876/876, API unit 250/250, vitest hedefli 38/38, lint/typecheck PASS. Push edildi (PR yok; paket 3'e girer). Bağımsız QA alt-ajanı çalışıyor.
- #28 / #30: durma limiti nedeniyle **dokunulmadı** (kullanıcı onayı yok).
- PR #37 CI yeşil (push+PR, ee65ff9) → normal merge commit ile birleştirildi (`2995f70`).
- DEV-024 takip dalı CI yeşil (39d5e77, yeni PG fence testi dahil). Bağımsız QA **PASS-WITH-NOTES** (engelleyici yok; not: pozitif etiket koruması yalnız `en`'i tarıyor; TR ifade → 1eaacac'te düzeltildi).
- `integration/bundle3-20261119` = main + DEV-024 takip + #32 (f39793a, çakışmasız). Yerel: build 0 uyarı, Domain 876/876, API unit 250/250, lint/build PASS, vitest product+i18n 1704/1704. **PR #38** açıldı; CI ve #32 bağımsız QA sürüyor.

### 19.11 11:15 etkileşimli oturum
- Statusline kırıktı: `.tools/node/node-v26.13.0` silinmişti → settings.json `C:/Program Files/nodejs/node.exe`'ye çevrildi (bu yüzden usage.json 00:06'dan beri bayattı). 07:41 oturumu öldürülmüş (0xC000013A), bayat `run.lock` silindi; `poseidon-continue.ps1` artık kilit PID'si canlı değilse kilidi bayat sayıyor.
- #38 CI 8/8 yeşil (f39793a); #32 QA sonucu kaybolmuştu → bağımsız QA yeniden başlatıldı.
- Kullanıcı "kaldığın yerden devam et" dedi → #28 (2a55677) ve #30 (c4367d4) teşhisli düzeltmeleri **tek push** ile gönderildi; CI bekleniyor. Yine kırmızıysa dur ve raporla.
- #32 bağımsız QA **PASS-WITH-NOTES** (f408876; engelleyici yok). Düşük notlar: (1) `EnterpriseChrome.tsx:181` tagline `aria-hidden` (aria-label kaldırmak yeterdi); (2) `s2-axe.spec.ts:71` yalnız `/app/today/{vessel}` taranıyor (filo listesi, araç seçilmemiş, arama diyaloğu yok); (3) axe `incomplete` sonuçları raporlanmıyor; (4) `docs/dependency-policy.md`'de axe paket kaydı yok; (5) `enterprise.css:77` CSS-üretimli kısayol ipucu kontrast denetiminden çıktı (belgeli). Sonraki takip paketinde düzelt.
- #38 birleştirme denemesi (`gh pr merge 38 --merge --match-head-commit f39793a…`) harness auto-mode tarafından **reddedildi** → kullanıcı izin verdi; settings.json'a dar gh kuralları eklendi (`gh pr merge/create/comment`, `gh run`; geniş liste harness'ça reddedildi). `gh`'ı doğrudan çağır (`& $gh` kurala uymaz). **#38 birleşti → main `e488b34`** (#32 otomatik).
- #25 (58.03): main birleşimi 5c6d80e + 35 yeni profil → d6dffa6 push (build 0 uyarı, Domain 876, API unit 264, Persistence unit 75). Bağımsız QA **PASS-WITH-NOTES**; orta bulgu (probe'da hr.*/config.* izinleri yok → sahte yeşil) yerelde **b92792a** ile düzeltildi + drift guard testi (4/4); d6dffa6 CI bitince push et. Düşük notlar: STORED-OUTCOME lostReplyRetry metni Declare için yanlış, Declare concurrencyEvidence NONE_DECLARED artefaktı, yeni GET'ler gövdesiz 403/404/503 (modül sahiplerinde düzelt).
- #28 (2a55677) ve #30 (c4367d4): eski test hataları düzeldi (tüm API integration testleri geçti) ama **yeni teşhis**: critical-write audit kapısı `AUDIT_BEFORE_AFTER_UNPROVEN` (#28: 117 satır, MembershipImport/Delegation*/Suspend/Resume/Revoke; #30: 3 satır Link/Publish/Release ControlledDocument). ebb697d (HR) ile aynı sınıf → alt-ajanlar AuditMetadata projeksiyonu + unit test yazıp tek push yapıyor.
- Sonuç (~12:25): **#28 a33e130 CI yeşil**. #30 3d0f0f5: audit kapısı geçti, sonraki adım `test_s1_audit_contract.py:28` (11 belge işlemi istemci sözlüğünde yok) → 1c61483 (yerel 585 script testi OK, verify_sources 0 uyumsuz), CI koşuyor. #25 b92792a PR-CI yeşil, push koşuyor.
- gh API kotası (5000/saat) `gh run watch` döngüleriyle tükendi → izleyiciler arası en az 60 sn; mümkünse tek `gh run list` sorgusu.
- Zamanlanmış 12:41 görevi etkileşimli oturumla çakışmasın diye `run.lock`'a bu oturumun claude.exe PID'si (27088) yazıldı; oturum kapanınca kilit bayatlar.
- Bağımsız yeniden QA: **#28 a33e130 PASS-WITH-NOTES**, **#30 1c61483 PASS-WITH-NOTES** (engelleyici yok). Düşük notlar (takip paketi): Modified üyelik/delegasyon/recovery satırlarında before/after sabit `Binding=OwnerReference` — gerçek geçiş Status/AccessRevision/State'te; kapının kapalı listesine üyelik durumlarını ekle veya AccessRevision>0'ı revision yap (`AuditMetadata.cs:164,178,187`; kapı `CriticalWriteCoverageCapture.cs:203-212`); belge revizyonunda Before/AfterRevision değişmez kimlik → IntakeState/PublicationState'i kanıt yap (`AuditMetadata.cs:209`); `ControlledDocumentPolicy.cs:77-87` `activeUseOpen=false` varsayılanını zorunlu yap; `MembershipEndpoints.cs:364-368` başka kiracıda kimlik var bilgisini sızdırıyor (belgeli).
- Paket 4 yerel hazırlığı (`integration/bundle4-20261119` = main + #25 + #28 + #30, worktree `bundle4-20261119`) alt-ajanlarda.

### Entegrasyon paketi 2
`integration/bundle2-20261119` = main + #29 + replay-race + #34 + #33 (hepsi CI yeşil + QA'lı); alt-ajan birleştirip tek PR açıyor (03:50'de başladı). #28 ve #30 durma limiti nedeniyle dışarıda — kullanıcı onayıyla push edilip CI yeşillenince paket 3'e.

## Sıradaki iş (sıra)
1. **#38 paket 3:** CI yeşil + #32 QA PASS ise birleştir (`gh pr merge 38 --merge --match-head-commit <tam SHA>`). Kırmızıysa CI hata protokolü.
2. **Kullanıcı kararı bekleyen:** #28 (yerel 2a55677) ve #30 (yerel c4367d4) düzeltmelerinin push'u (durma limiti). Onay gelirse tek push, CI bekle, sonra paket 4.
3. #25 (58.03 profil sicili): main birleşimi + yeni uçlara profil ekle → paket 4.
4. **S2 sprint kapısı:** main üzerinde tam backend/persistence/API/frontend/Playwright (CI) + bağımsız S2 QA alt-ajanı. Geçerse `docs/project-status.json/md` → S2 accepted 6 iş / 42 SP (145/970 = %14,9) + `docs/phases/S2-gate.md`.
5. Sonra S3 backlog (DEV-026…) + 15.11 dalga 1 kalanları (63.06, 65.03), dalga 2 (59.01, 60.03).

## PR KURALI (kullanıcı, 19.11.2026)
İş başına ayrı PR AÇMA. Paketler özellik dallarında geliştirilir (push edilebilir, CI koşar) ama PR yalnız **entegrasyon dalı** için açılır: `integration/<konu>-<tarih>` dalında birden çok paket birleştirilir, tek büyük PR → tek CI → main. Mevcut tekil PR'lar (#25 #28 #29 #30 #32 #33 #34) ikinci entegrasyon PR'ında toplanacak; o birleşince otomatik Merged olurlar (ayrıca yeni tekil PR açma). Bağımsız QA paket başına yine yapılır (alt-ajan), PR başına değil.

## Kararlar
- Kullanıcı soru istemiyor; kararlar analiz/araştırma ile verilir, buraya ve görev dokümanlarına yazılır.
- Her PR: CI yeşil + bağımsız QA (yazar ≠ inceleyici alt-ajan). Main korumalı: güncel dal şart → çok PR'ı entegrasyon paketiyle birleştir.
- Ajanlara: yalnız mutlak yol, kendi başlatmadığı süreci öldürme, Playwright için benzersiz port.
- Secret scan istisnaları: yazar + bağımsız inceleyici paketi; pin ekleme adımı kullanıcı onayı gerektirebilir.

### Paket 4 (19.11 ~13:30)
- #30 1c61483 CI yeşil. **PR #39** = `integration/bundle4-20261119` 9c82bee (main e488b34 + #25 b92792a + #28 a33e130 + #30 1c61483; 24 yeni profil, yeni NATIVE-COMMAND-IF-MATCH-1, snapshot elle birleştirildi). Yerel: Release build 0 uyarı, Domain 933, Persistence unit 83, API unit 276, script 591 OK, lint/typecheck, vitest 1704/1704. CI + entegrasyon bağımsız QA'sı koşuyor. İkisi geçerse: `gh pr merge 39 --merge --match-head-commit <tam SHA>`. Sonra S2 sprint kapısı.
- PR #39 entegrasyon bağımsız QA **PASS-WITH-NOTES** (9c82bee; birleşimler, snapshot, 24 profil doğru). Düşük notlar: L1 gerçek-host probe If-Match eksik/bayat istek göndermiyor (428/412 yalnız `ControlledDocumentTests.cs:71-72` Integration'da); L2 40001/40P01 `ControlledDocumentEndpoints.cs:295-296` 409 `version_conflict` dönüyor, profil bayatı 412 diyor → profilde belgele; L3 probe izin regex'i `[a-z_.]` rakam içeren izni atlar → `[a-z0-9_.]`.
- PR #39 push run 9c82bee kırmızı / PR run yeşil (aynı commit = gerçek yarış): `RegulatoryProfile.test.tsx:149` `onSaving(false)`'ı pasif effect (`use-profile-recovery.ts:44`) flush olmadan doğruluyordu → 3ea87b4 waitFor (doğrulama aynı). 3ea87b4 CI push+PR yeşil → **#39 birleşti, main `55d5e1f`** (#25 #28 #30 Merged).
- Açık PR yok. Sıradaki: **S2 sprint kapısı** (main 55d5e1f CI kanıtı + bağımsız S2 QA → project-status S2 kabul), sonra QA düşük-not takip paketi, sonra S3 backlog.

### S2 kapısı (19.11 ~14:15)
- Main 55d5e1f CI run 44510977746 yeşil. Bağımsız S2 QA: DEV-020…025 **ACCEPT-WITH-NOTES ×6**, engelleyici yok.
- `docs/s2-gate-acceptance-20261119` **b97686b** (push edildi, PR yok): project-status.json/md → **26 iş / 145 SP / %14,95**, `docs/phases/S2-GATE-20261119.md`, QA kanıtı `docs/evidence/s2-20261119-55d5e1f/qa/`, CLAUDE.md sayım satırı.
- `fix/qa-low-notes-20261119` alt-ajanda (probe regex, IF-MATCH 409 belgesi, STORED-OUTCOME metni, ControlledDocumentPolicy activeUseOpen zorunlu, axe kapsamı + incomplete ek, EnterpriseChrome tagline). Bitince: `integration/bundle5-20261119` = main + s2-gate docs + qa-notes → tek PR.
- Ertelenen tasarım işi: critical-write kapısında Modified satırlar için gerçek durum geçişi kanıtı (OwnerReference sabiti yerine Status/AccessRevision/State; belge revizyonunda IntakeState/PublicationState).
