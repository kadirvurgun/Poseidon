# CREW-BOUNDARY-SIGNED-PERSONA-01

Status: **AUTHORED / execution DEFERRED / acceptance false**. Author: frontend
gate agent acting under a separate authoring lease; independent reviewer:
status_forecast. No canonical story points or phase acceptance are added.

Active baseline `poseidon-v46.3-20261030`: 11.01 scoped nullable crew assignment
and protected inputs; 18.01 Backlog48 DEV-047 / T-S7-001; DEV-014/015 tenant and
role isolation. Existing [crew scope](crew-full-scope-01.md) and
[crew workspace](enterprise-crew-workspace-01.md) behavior remains authoritative.

## Diagnosed fixture mismatch

The actual `faf7f91` diagnostic reached the exact owning-input text assertion at
[crew-cases.ts](../../tests/Poseidon.Browser.Tests/register-specs/crew-cases.ts):168.
Its shared manager had no certificate-read capability, but did have contact
read/write through the complete legacy privacy fixture. The journey intended
certificate absence under permitted read and contact/assessment withholding.
Those are different personas. This change preserves the manager's complete
legacy/medical coverage and supplies the intended bounded persona explicitly.

## Fresh genesis and authority boundary

[The pure helper](../../scripts/bundle_crew_boundary_fixture.py) copies the
in-memory provider import, private credential map and server membership before
the first immutable source-bundle publication. `oidc_lab.py`, the actual PKCE
login, and the four established users/credentials/actor grants remain unchanged.
No provider administrator request or existing runtime mutation is performed.

`lab-crew-boundary` receives exactly:

- `directory.read`, `crew.read`, `crew.write`, `crew.identity.read`,
  `crew.certificate.read`, `crew.history.read`;
- one editable grant for the existing original ordinary Ship from the same-run
  plan, one attributable audit role, and false tenant-wide read/manage flags;
- no contact, medical, assessment, document/file, directory-write, verification,
  human-decision appointment or operational approval authority.

Canonical run-bound subject/actor/role IDs must be unique against existing IDs.
The helper validates the existing provider/member pairings, copies all original
objects, and rejects conflicting or partial new-account state. Repeating it on
its own exact in-memory run preserves the credential and every object. Native
boolean distinctions are retained. The existing immutable identity loaders
include all five memberships and reject adding an identity after genesis.

## Exact journey and meaningful authored tests

Only `crew-assignment-policy-history-and-owning-input-boundary` declares the
closed `account: 'lab-crew-boundary'` metadata. The signed-case driver rejects
unknown, inherited, explicit-undefined or restricted-account overrides before
login; every other case keeps its manager/restricted default, original callback,
case order and no-retry behavior. Actual provider authentication is unchanged.

The journey requires a single authorized Ship before its ordinary null-personal
draft command. Directory `canEdit` requires `directory.write`, which this actor
does not receive; the actual audited Crew POST proves its scoped crew write.
All six exact input states, incomplete draft, immutable assignment history,
reload and no-qualified-conclusion assertions remain intact.

Authored checks, all **NOT RUN**:

1. Python pure-helper preservation, exact six rights/single grant, actor/subject/
   role collisions, shape/boolean/credential conflicts, idempotence and stale-run
   rejection. The complete existing manager policy is preserved.
2. Integrated genesis now requires exactly five provider/member subjects and
   checks the foreign identity by its exact reference, not array position.
3. Native signed-case tests require the boundary provider account and preserve
   defaults, denied overrides and non-retried authentication/journey failures.
4. Whole-manifest validation preserves account metadata and requires exactly
   this one dedicated override without dropping/reordering any existing case.
5. CI runs the new Python suite before provider setup, alongside the existing
   signed-case and whole-manifest Node gates.

No tests, build, typecheck, HTTP, database, provider or browser execution occurred
during authoring. Required later frozen-bundle native/scans/genuine gates and
independent review remain open. Generated private credentials are never copied
to tracked artifacts. Any actual new scanner finding requires the existing
independent review/coordinator protocol; the author invents no approval or pin.
