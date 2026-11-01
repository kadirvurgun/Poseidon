# LAB-NAVIGATION-FIXTURE-01

Status: AUTHORED_UNVERIFIED / NOT ACCEPTED. No provider, test, browser, build,
typecheck or security scan executed. This is supporting lab software for the
later combined foundation gate, not a separately accepted canonical feature.

## Sources and ownership

Active baseline `poseidon-v46.3-20261030`; source Backlog SHA
`09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`:
`06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`,
`sheet:Backlog/row:22` DEV-021 / T-S2-002,
`row:23` DEV-022 / T-S2-003 and `row:26` DEV-025 / T-S2-006.
The implemented menu and its exact UX locators remain defined in
[NAVIGATION-ROLE-LENS-01](navigation-role-lens-01.md).

Existing real signed-host journeys require two configured role references and
ordinary module read grants on the explicitly enrolled writer fixture. The
old `prepare_bundle` assigned one reference without a role-label catalog or
navigation mappings, so these journeys could not reach the configured menu.
The synthetic JSON example is not a runtime default or an identity enrollment.

Owner: coordinator-authored integration support in an isolated preparation
worktree based on `138a83c5660f70ec5739e2e204e78bf6d99d6823`. S0 native source
authoring continues independently; this patch is integrated only through the
later foundation gate with normal source-origin history.

## Acceptance cases and actual behavior

| Local trace | Behavior | Authored verification |
|---|---|---|
| LAB-NRL-01 | Explicit `prepare_bundle` calls a pure copy-based helper for the one genuine `poseidon-lab` manager subject and fixed synthetic tenant, retaining actor/issuer/subject/vessel grants and other members. No provider roles, cookies or browser state are fabricated. | Actual helper tests compare original inputs, credentials/configuration and all nonwriter members; wrong realm/tenant/scope/duplicate subject deny. |
| LAB-NRL-02 | Two deterministic synthetic UUID role references resolve through matching tenant-local TR/EN labels and Fleet/Ship/Tug mappings. Only the existing explicitly invoked disposable fixture setup receives the closed ordinary read/mode capability list. No verifier, safety, owner or new business-write grants. | Exact reference binding, two menu projections, ordinary grant difference and idempotent repetition tests. Real endpoint/browser proof is still required. |
| LAB-NRL-03 | Unknown existing role references and conflicting or duplicate operator-maintained labels/mappings are rejected before changing the input. Other tenant entries remain intact. Version/array budgets fail closed. | Adversarial configuration preservation, malformed roles, duplicates, foreign tenant entries and capacity tests. |
| LAB-NRL-04 | Ambient host-prefixed/case/delimiter variants of WorkspaceContext and WorkspaceNavigation configuration cannot extend private lab arrays. | Existing environment isolation test matrix expanded; it does not replace genuine runtime endpoint or browser checks. |

Implementation: `scripts/oidc_lab.py`, `scripts/test_lab_navigation_fixture.py`
and the expanded `scripts/test_oidc_lab.py`. Existing `prepare_bundle` still
requires a genuinely responding local ClamAV service and existing private lab
configuration, and stops only the marked lab app through the existing path.
No lab commands or private configuration writes were performed while authoring.
The helper is never called by production API startup or normal lab `init/start`.

No new operational entity, migration, seed, dependency or external resource.
The new fixture references are test attribution, not human appointments or source
validation. Existing operator configurations containing other assigned roles
require deliberate manual reconciliation rather than silent replacement.

All seven new tests and the extended environment matrix are **DEFERRED / NOT
RUN**. At the later gate, execute Python regressions plus genuine signed OIDC
host tests including all three registered navigation journeys on both supported
viewport configurations. Configure other domain/private file/offline fixture
requirements separately; this patch supplies only navigation prerequisites.
No publication, QA approval, production readiness, passing security scan or
canonical SP award. Canonical accepted total remains8/970.
