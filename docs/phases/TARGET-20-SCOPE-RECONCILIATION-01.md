# TARGET-20-SCOPE-RECONCILIATION-01

**PLANNING/TRACEABILITY RECONCILIATION, NOT ACCEPTANCE.**

The prior candidate ledger still selected only33 tasks/211SP even though actual
Crew/Equipment/PMS/Tug/Import authoring was progressing. Seven current-source
stories are now explicit selected candidates, with their exact criteria, source
locator, dependencies, trace IDs and actual authoring state. Original candidates,
all970SP and actual accepted8SP are preserved. Candidate totals are40 tasks/272SP;
no story was promoted to ACCEPTED and no software/test/QA/CI claim was fabricated.

Active18.01 Development_Backlog_TR.xlsx SHA256
09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:

| Task | Backlog row | SP | Current state |
|---|---:|---:|---|
| DEV-047 / T-S7-001 |48|8|Crew checkpoint incomplete; remaining fields/offline/effective selection pending|
| DEV-052 / T-S8-001 |53|8|Equipment authored unverified; full gate/source acceptance pending|
| DEV-053 / T-S8-002 |54|13|PMS authored unverified; adapter/policy completion and gates pending|
| DEV-069 / T-S11-001 |70|8|Import actual native author running; no package acceptance|
| DEV-080 / T-TUG-001 |81|8|Tug context/type source-backed software completion pending|
| DEV-081 / T-TUG-002 |82|8|Scoped draft intake authored unverified; complete criteria/gates pending|
| DEV-082 / T-TUG-003 |83|8|Board authored incomplete; planning/assignment/assessment software pending|

Total newly selected61SP is potential scope, not earned progress. The earlier
source-dependency scenario has133 unaffected original candidate SP; adding61
produces a conditional194SP path. This is not proof those133 or61 SP satisfy
all AC/dependencies. DEV-070 owner reconciliation/sign-off remains a separate
unselected5SP proposal, with no fake owner acceptance. Original source-owner,
reviewer, legal/Marine/pilot gates remain as actually required by their sources.

After the larger real gate, exact-target independent engineering QA and required
CI, the separate source reviewer must assess the actual ledger's complete40-task
selection clause-by-clause and in dependency order, with individual gaps. It must
not silently review only the obsolete first33 or count authored checkpoint points.
No test suite/build/typecheck/browser was executed in this reconciliation. Only
local source extraction and JSON arithmetic/uniqueness/accepted-count invariants
were used to compose the plan; these are not product test or acceptance evidence.
