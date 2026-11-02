# OFFLINE-CAPTURE-WIRE-REPAIR-01

AUTHORED_UNVERIFIED / PENDING TARGET INTEGRATION. This is a concrete decoder
correction found during actual foundation authoring, not a completed story or
an independent QA approval. No tests/build/typecheck/browser executed.

## Sources and actual defect

Active Backlog SHA `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0c437d15df4674`,
`18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`,
`sheet:Backlog/row:36` DEV-035 / T-S4-003 requires explicit human resolution of
material conflicts without automatic overwrite, with retained context/audit.
Selected DEV-047 row48 retains the supported offline contract and privacy.
[Actual application contract and exact API/data locators](offline-application-01.md)
defines the one supported `crew.draft.watch-group.v1` command, its explicit
`keepServer`/`useCaptured` resolution choice and unchanged immutable lineage.
These source references do not add accepted DEV-033..035 to the candidate ledger.

The newly authored `decodeOfflineCapture` checked a choice with
`includes(String(value.choice))`. Arrays containing a permitted word and custom
conversion objects could therefore pass a purported string-enum decoder. An
object conversion could also execute code or throw a noncontract error. This
was a source observation, not a reproduced test failure or proof of server-side
authorization bypass: the server retains independent validation and authority.

## Delivered correction and verification

| Local trace | Behavior | Authored regression |
|---|---|---|
| OCW-01 | Require an actual string before checking the closed permitted choice set. Do not invoke conversion on unknown values. | Arrays, nested arrays, null, booleans, numbers and custom conversion objects reject with the contract error; conversion spy stays unused; the genuine permitted string remains accepted. |
| OCW-02 | Retain the author's shared `timestamp` calendar/UTC validation and exact captured timestamp bytes. | Invalid day/month/leap-century/year-zero/hour/minute/second values reject; valid leap years and1/7-digit supported bounds remain unchanged. No duplicate date parser was introduced. |

Two meaningful additional frontend test cases are authored, **DEFERRED / NOT RUN**.
No policy expansion, new adapter/field, source activation, new entity, migration,
seed or dependency. No replay-authority, receipt/audit or private-data behavior
is relaxed. No positive readiness or safety decision is inferred.

The isolated preparation branch first captured two exact live authoring files
in local review-basis commit `300cfe35a025267282a399d00d50f44d73e5821a`.
**Do not integrate that basis commit.** It is a snapshot for a minimal subsequent
correction, not an approved source delivery. The active Esra writer was untouched.
Integrate only the later corrective commit after the real author checkpoint
exists; preserve newer valid author changes and recheck whether the author
already fixed the issue. Do not replace a whole newer module with this snapshot.

Actual later execution must cover the affected decoder/consumer cases and
appropriate frontend checks on the final integrated target. If a full gate ran
before this correction, retain its historical evidence and rerun affected checks
before exact-target independent QA/required CI; no old passing result is inherited.
Canonical8/970SP and all source/production/human gates remain unchanged.
