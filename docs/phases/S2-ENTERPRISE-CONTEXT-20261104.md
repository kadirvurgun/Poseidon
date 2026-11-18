# S2 — vessel context and enterprise workspace

Status: **AUTHORING UI PATTERN CORRECTIONS AND HISTORY SCOPE FIX; verification DEFERRED; 0/42 SP accepted**.

Reporter snapshot `2026-11-04T16:46:07.951096Z`; integrated source checkpoint `54679c560dc4ddad3c4ad21196090dd44f5f0e03`.
General accepted progress remains **103/970 SP = 10.62%, 20/121 tasks, 0/196 screens**.
This is an authoring milestone, not an independent phase acceptance.

The user requested continuation after S1 and deferred test execution until the
larger delivery package is ready. S1 engineering acceptance is recorded separately;
its [PR #15](https://github.com/kadirvurgun/Poseidon/pull/15) passed the required
head checks and independent review and was normally merged at
`2026-11-04T16:44:08Z` as `856f75e6a143fb81e4f41ddcb2a593185179618a`. Merged-main CI is
still in progress as last observed; publication completion is not yet claimed.
S2 authoring does not accept any S2 task.

## Controlled source

Baseline: `poseidon-v46.3-20261030`, selected by
[active source configuration](../sources/active.json). Original workbook:
`analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`.
SHA256: `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
Rows below were read from the hash-checked original workbook. Its immutable
`Not Started` source status is distinct from implementation progress.

| Task / trace | Locator | SP | Literal acceptance criteria | Dependencies |
|---|---|---:|---|---|
| DEV-020 / T-S2-001 | sheet:Backlog/row:21 | 8 | IMO no, flag, class, ship type, GT, build/trading/cargo context versioned kaydedilir; provenance/freshness görünür. | DEV-005, DEV-014 |
| DEV-021 / T-S2-002 | sheet:Backlog/row:22 | 5 | Context topbar görünür; vessel switch authorization check yapar; unsaved critical form context change'i uyarır. | DEV-015, DEV-020 |
| DEV-022 / T-S2-003 | sheet:Backlog/row:23 | 8 | Role lens ile sade menü gelir; Full product search authorized power users'a açıktır; 172 ekran düz menü olarak gösterilmez. | DEV-015, DEV-021 |
| DEV-023 / T-S2-004 | sheet:Backlog/row:24 | 8 | Exception cards priority/source/Why/owner/next action taşır; loading/empty/error/offline states deterministiktir. | DEV-021, DEV-022 |
| DEV-024 / T-S2-005 | sheet:Backlog/row:25 | 8 | Critical state için source/reason/input freshness/blocker listesi döner; unauthorized source detail sızmaz. | DEV-016, DEV-023 |
| DEV-025 / T-S2-006 | sheet:Backlog/row:26 | 5 | Keyboard focus, contrast, status text/icon, responsive breakpoints ve form error semantics temel shell'de PASS. | DEV-022 |

## Work package and dependency order

Authoring branches start from S1 publication candidate
`8a18a436c0bff4caaf4d1117360d0fc2fe1842bc`. Separate worktrees own backend and
frontend changes; the integration branch owns this report and browser scenarios.
Existing product code is assessed against source criteria before extending it.
DEV-020 starts from accepted DEV-005/DEV-014 behavior. Dependent UI/Why work begins
with contract preparation and follows the upstream contract; neither a contract
agreement nor a local commit is independent acceptance.

The first observed UI gaps were the absence of a vessel switch control in the global
context bar and the absence of a role-lens filter in the primary enterprise menu.
The existing Today feed already carries source, priority, owner, action and Why
disclosures. Additional changes must preserve its permission withdrawal and offline
invalidation behavior. Source coverage and unverified drafts must remain explicit;
an empty feed never means operational approval.

## Authored behavior, not yet validated

The backend checkpoint adds an exact owning source kind/ID/revision to Why. The
owning regulatory profile and Today/Why now use the same scoped, conditional SQL
projection: a user without required file-read access receives no profile content,
revision, observation time or provenance. The owning profile and Why return 403;
Today uses generic Unavailable coverage and omits the restricted card. This also
closes the former separate binding-check/content-read race. Maintenance Why now
uses the selected observation's actual time, preserving Unknown when absent.

New backend regression sources cover versioned master fields, exact current/history
lineage, preservation by synthetic reseed, permission withdrawal for empty and
populated profiles, and revoked/unknown observation timestamps. There are no new
entities, migrations, seed records, mutation routes or critical-write denominator
changes. See the [backend task](../tasks/s2-vessel-why-01.md).

The integrated UI checkpoint `08420153fd4080f9054b02c0f99fac2f8bf2ca12` adds an accessible, paginated vessel chooser.
A fresh server-bound context read precedes navigation and the existing dirty-form
guard; denied, malformed, late, hidden and offline results cannot commit a switch.
Assigned role lenses now replace the primary sidebar; the independent current
search capability remains necessary for full product search. Saved source timestamps
and unverified provenance stay distinct from unsaved fields. Focus indicators,
text/icon status, responsive role context and described validation controls are
authored; contrast, keyboard and responsive acceptance await actual measurements.
The additive Why source decoder checks exact kind/ID/revision and unavailable
regulatory coverage. See the [UI task](../tasks/s2-enterprise-shell-01.md).

Source checkpoints: backend `5157e422d11af3656a2c6c78ff5de8d9ca7c1574`, browser `d29eb15`,
phase `876259b`. The read-only diff against `8a18a436c0bff4caaf4d1117360d0fc2fe1842bc` before this
report contains **32 files, +1111/−58 lines**; these source counts are
not executed test counts. No new restore/build/typecheck/OpenAPI generation,
backend/component/browser suite, security scan or independent final-target QA has
run for S2. All remain **DEFERRED / NOT RUN**; earlier S1 results are separate.

Four new mocked browser scenarios cover keyboard vessel switching with denied and
mismatched-tenant responses, dirty-form cancellation/explicit discard, and primary
role navigation without search escalation, plus actual form-action alignment,
keyboard order and horizontal overflow on desktop/mobile. The signed role-lens cases retain their
five real workspace families, real dirty guard, server projection equality and
restricted context assertions, and now assert the actual primary sidebar contents.
All these tests are **AUTHORED / NOT RUN**, without new browser or QA acceptance.

Two additional signed case sources exercise genuine Ship/Tug context changes and
the nested native chooser/discard-confirmation cancel/commit flow. They use actual
server context responses and verify that abandoning an edit does not mutate the
original vessel. They have not run; the old S1 native 122-result ledger remains
unchanged and is not evidence for this new S2 catalog.

## Required verification at the bundle gate

All checks in this section are **DEFERRED**, with no new PASS evidence:

- Backend domain and real PostgreSQL/API integration tests for profile revision,
  provenance, incomplete/unknown fields, concurrency, audit atomicity and tenant/
  vessel/role denial; Why details must respect their owning source's permissions.
- React component tests for authorized context switching, cancellation of dirty
  forms, role-filtered navigation, denied search, context refresh/offline, accessible
  errors and focus, and Today/Why state transitions.
- Desktop/mobile Playwright journeys for keyboard operation, context switching and
  permission withdrawal. Mocked transport verifies browser behavior only; signed
  real-backend journeys remain separate evidence.
- Locked restore/build, lint/typecheck, actual host/OpenAPI consumer parity,
  migration/model parity if changed, secret scanning, and the complete relevant
  regressions at a single reviewable package gate.
- Independent QA on the final target commit, then required push/PR CI before any
  normal merge. No source acceptance totals or screen counts change during authoring.

No new dependencies, production provider choice, safety decision, verified evidence,
human appointment or pilot sign-off is authorized by this work package. The next
delivery report will identify actual changed behavior and exact executed results,
including failures and unresolved gates.

## Current delivery checkpoint

At `2026-11-04T16:46:07.951096Z`, S1 [PR #15](https://github.com/kadirvurgun/Poseidon/pull/15)
has been normally merged at `2026-11-04T16:44:08Z` as `856f75e6a143fb81e4f41ddcb2a593185179618a`.
Both metadata-head [push CI41863704444](https://github.com/kadirvurgun/Poseidon/actions/runs/41863704444)
and [PR CI41865316963](https://github.com/kadirvurgun/Poseidon/actions/runs/41865316963)
passed all4 jobs/19 commands with native API137/582 and actual critical82 proof.
Independent exact-head review preceded the normal merge. Original F acceptance
receipts retain their original target. [Merged-main CI41870412787](https://github.com/kadirvurgun/Poseidon/actions/runs/41870412787)
is still in progress as last observed by the CI agent; publication complete remains
false until actual main checks close. S2 remains fully **DEFERRED / NOT RUN**.
Its next step is to freeze the combined source target, refresh the AST-only graph
and open one integrated generation/build/test/browser/security gate, then obtain
independent exact-target QA and the required publication checks.

## Quality correction continuation

At `2026-11-04T16:50:32.271950Z`, the user requested consistent UI patterns and correction of existing button/page layout problems in the next delivery. Authoring continues across Directory/Fleet, profile, Today, Technical and Crew action layouts. The earlier 6-task authoring milestone is retained as history; the bundle gate has not opened. Independent source preflight found HIGH `S2-QA-001` in regulatory history authorization (separate binding gate and content page); a single-snapshot whole-history fix and deterministic interleaving regression are being authored. No test, build, type generation or visual acceptance is claimed.


## Ayrı S3 authoring sürerken S2'nin gerçek durumu — 2026-11-04T19:03:15.495960Z

Üstteki eski authoring/DEFERRED anlatımı kendi kayıt zamanıdır. S2 temizC6
`75d734285706848e55e9a6276aaa64ac789b8e59` ayrı worktree'de dondurulmuş halde kaldı. Gerçek ordinary58/58 veRelease
PASS; source-equalC5frontend1011/1011/build/lint,mocked visual569text/559enabled,
0failure ve scans;backend506137unit/589integration/82operation/565capture sonuçları
özgün issued hedefleriyle korunur. S2accepted0/42 ve noPR değişmedi.

Signed02 TLS3 ve gerçek login baseline sonuçlarından önce setup'ta FAILED kapandı;
0 kayıtlı vaka/full126 sonucu yok. Dar setup ve one-first-reject tanıları tam02
hatasını yeniden üretmedi; nedenUNISOLATED,preflightPASS veya flaky sayılmaz.
Recovery receipt tamowned runner kapanışını,Fpreview strictTLS200/PID2576/creation
134382912023864810 sağlığını,orijinal private/user pointer korunmasını veownedPG02'nin
stopped/retained durumunu kendi eski kaydı olarak tutar. Sonraki03 gerçekten çalıştı
ancak context kurulmadan import sınırında FAILED kapandı: TLS_SERVER_CONSTRUCTOR
CREATED; STRICT_CONTEXT_SETUP içinde host-strict-proxy.mjs:9 THREW. Locator'lar
`.runtime/s2-enterprise-20261104/signed-attempt-03/tls-stage-diagnostic.json`,
aynı dizindeki `s2-preflight-result.json` ve signed-preparation altındaki
`failed03-resource-closure.json`. TLS3/login sonuçları, kayıtlı vaka veya full126
başarısı yok. F preview PID35972/creation134382923558803470 strictTLS200 ile
geri geldi; exactownedPG03 stopped/retained ve tüm runner'lar kapalı. SOURCE journey
ve HOST helper'ın iki fiziksel @playwright/test yüklemesi güçlü kaynak hipotezidir;
yetkili tek read-only ordered-import probe sonucu veya causal fix PASS'i henüz
yok. Minimal dependency alias incelemede;04 runtime/full126 yapılmadı.
Yeni S3 backend/frontend kodu bu S2 sonuçlarının
kapsamında değildir. Güncel genel kabul103/970SP10.62%,20/121iş,0/196ekran.


## Native04 kapanışı ve daha sonraki kullanıcı cadence kararı — 2026-11-04T20:02:53.991719Z

Üstteki önceki03 hata/hipotez anlatımı tarihsel kalır. Ordered duplicate-physical
Playwright import nedeni daha sonra doğrulandı; minimal04 alias source68da49 ve
positive1eca457 düzeltmesi bu prefix'i kapattı. Eski failure receipt'leri değişmedi.

Native04 `PREFLIGHT_FROZEN_PENDING_INDEPENDENT_REVIEW` statüsüyle `2026-11-04T19:16:08.613356+00:00` tarihinde kapanmıştı:
TLS 3 / 3 PASS, gerçek baseline 2 login PASS; bootstrap, discovery, freeze çıktıları
issued. Discovery 63 tanım × 2 proje = 126 beklenen yürütme; **registeredExecutions=0**
ve **fullMatrixExecuted=false**. Bu full126 veya bağımsız son S2 kabulü değildir.
Native locator `.runtime/s2-enterprise-20261104/signed-attempt-04/s2-preflight-result.json`,
SHA256 `47f75e702093cd5456e953627027704b5a28cbbde2eb7dea91c9dd5a7b976600`. Önceden kapanmış native sonuç failed/interrupted
diye yeniden etiketlenmedi.

Daha sonraki `.runtime/s2-enterprise-20261104/signed-preparation/user-cadence-stop04.json`
workstate'i `INTERRUPTED_BY_USER_CADENCE`; yalnız kalan full gate work'ünü erteledi. Native04
byte'ları aynen kaldı. Sekiz runner kapalı, F preview PID19856 / creation134382933639272220
strict TLS health200, private12/user pointer/F binary korunmuş, exactowned04 PG
stopped/retained. Full approval yok; S2 accepted0/42 ve noPR. Test/build/generation
actual PR aşamasına ertelendi; S4 authoring ayrı başladı.
