# FOUNDATION-POLICY-SIGNED-FIXTURE-01

This opt-in disposable-lab supplement addresses source-confirmed fixture gaps. It does not approve production personal-data processing, offline storage, human verification, regulatory qualification or safety decisions. All new execution is **DEFERRED** until the larger frozen bundle gate. Independent source review and actual genuine-provider/browser evidence remain required.

## Authority and source

- Active source: `poseidon-v46.3-20261030`; source-derived independent proposal SHA256 `f97d47411ed96df4f6f9eb387f2f477a7e260e0e564710e70ac42ae21778ca18`.
- `18.01` Backlog 22/23/26/48 and `13.04` role-lens contract: original role references/labels/modes remain; no duplicated mapping reference or new role is created.
- Active `14.02` rows 1581/1609, AB/AO/BH/BJ/BO: client UUID/version/idempotency; no implicit approved policy; protected personal fields and pending field classification; material conflict requires human resolution; review remains required. Workbook SHA256 `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`.
- `15.01` paragraphs 228–245: private person-file/purpose/capability boundary. Actual implementation remains authoritative for endpoint policy validation.
- Actual contracts: [workspace navigation](../../backend/Poseidon.Api/WorkspaceLensEndpoints.cs), [offline policy](../../backend/Poseidon.Api/OfflineApplicationPolicy.cs), [private-file policy](../../backend/Poseidon.Api/CrewFilePolicy.cs), and [original fleet ordering](../../backend/Poseidon.Api/FoundationEndpoints.cs).

## Bounded implementation and acceptance

1. [Pure configuration helper](../../scripts/bundle_foundation_policy_fixture.py) accepts only exact original synthetic role/catalog/navigation state: daily 21 keys and review 5. It changes these to daily 15/review 7, preserving all 21 union keys, original daily/review anchors, reference order, labels and Fleet/Ship/Tug modes. It never splits one reference into duplicate mappings.
2. Both policies are captured together with a strict ten-key, same-run plan. The original fixed tenant/seed Ship and actual writer/reviewer actors/role references must match the existing source plan and trusted localhost configuration. Existing empty policy objects are allowed; null, nonempty unknown/operator, partial, foreign-run or drifted policy states fail without input mutation.
3. Offline capture is only `crew.draft.watch-group.v1`, `watch_group`, fictional `SYNTHETIC-A/B`, and the exact original writer/Ship scope. The captured UTC window is eight hours plus a one-minute start allowance, bounded by nine hours. Lease/record/byte/device/enrollment budgets are explicit and inside unchanged backend guards. `NonPersonalControlledDraftDeclaration` is a software-test configuration value; policy references explicitly say `SYNTHETIC-UNAPPROVED`. No expiry-driven deletion or production field-classification approval is claimed.
4. Private files allow Photo and Passport only. Exactly `crew.documents.read/write` are added to the original writer only when existing directory, crew, identity and file read/write prerequisites hold. Other memberships, identities, existing grants and provider inputs remain unchanged. A distinct unrelated same-run fifth membership may be preserved; there is no four-member cardinality assumption or new persona provisioning.
5. Existing applied state must match recursively by exact types, keys, values and list order, including bool/int/float differences. Exact repeat preserves original plan/window/config generation. Revocation, partial configuration or expired windows are rejected, never silently repaired or renewed.
6. [Driver](../../scripts/bundle_oidc_lab.py) exposes explicit `prepare_foundation_policy_fixtures`. All validation finishes before publish-once plan/immutable config publication. First apply rejects an app PID or existing source/qualified/register business-attempt artifacts. It does not stop/restart services or change an existing host/profile/database. A crash after plan publication can repeat against that original plan; a completed repeat is read-only.
7. Source preparation and register entry require the applied policy before writes. The selected immutable configuration is read through existing private-generation guards. Ambient policy overrides continue to be scrubbed. Later normal integration must retain the qualified-fixture CLI verb and apply the same source-write preflight to it.
8. [Consumer preflight](../../tests/Poseidon.Browser.Tests/register-specs/foundation-policy-context.ts) reads bounded private regular metadata, rejects links/aliases/BOM/malformed UTF8/duplicate keys and widened POSIX mode/foreign ownership, binds both plans to the selected runtime, then requires actual authenticated original writer/tenant/read permission and the unchanged actual first fleet item to be the original Ship. [Source setup](../../scripts/bundle_fixture_provision.ts), [offline setup](../../tests/Poseidon.Browser.Tests/register-specs/offline-application-cases.ts), and the two positive [person-file setups](../../tests/Poseidon.Browser.Tests/register-specs/person-file-cases.ts) check this before business writes. They do not reorder/find a later matching vessel or broaden selection. Existing restricted negative case stays unchanged.

No changes to `oidc_lab.py`, CrewPositionPolicy or TugPlanning are authorized here. The separately reviewed qualified-fixture task owns dedicated vessel/Position/Tug policies. It adds `tug.write` only to its original writer to exercise the ordinary Tug-job API; this supplement does not add it. No direct database writes, seed VERIFIED evidence or approvals are introduced.

## Future explicit invocation order

Run only in a newly owned isolated lab after source freeze/review and coordinator gate authorization, using the existing driver `--app-port/--provider-port/--database-port` contract and its selected immutable private generation. Genesis creates the run UUID; there is no caller-controlled run-ID override:

1. `init_source_bundle` with three validated ports in an empty owned private directory.
2. `prepare_bundle`, then `prepare_crew_fixture`.
3. Explicit `prepare_foundation_policy_fixtures` before `start` or any business preparation.
4. `start`, `prepare_source_fixtures`, separately integrated `prepare_qualified_fixtures`, then `register_browser`.

Omitting explicit preparation fails closed. Existing profiles, retained private generations and previous failed genuine attempts must remain untouched. The original source plan/Ship and actor A/B remain fixed across all steps.

## Regression sources and evidence

- [Python regressions](../../scripts/test_bundle_foundation_policy_fixture.py): exact navigation/union/anchors; only two writer permissions; unrelated fifth actor preservation; null/operator/partial/foreign states; recursive numeric type drift; prerequisite revocation; expired/fresh-window rejection; no input mutation; pre-start driver publish-once/idempotence and no writes when preparation is missing or business attempts exist.
- [Native Node regressions](../../tests/Poseidon.Browser.Tests/lab/foundation-policy-context.test.ts): strict plan/base/runtime binding; actual wrong owner/tenant/first Ship; no fallback to later items; bounded/private/BOM/UTF8/duplicate/link/mode negatives; complete selected immutable-generation binding.
- [CI](../../.github/workflows/backend-foundation.yml) adds mandatory Python and native Node protocol commands before provider/genuine execution. Existing whole-manifest/privacy/fixture/frontend gates remain.
- Authored only: tests, build, typecheck, lint, discovery, provider, HTTP, database and browser execution **0 / DEFERRED**. No acceptance or canonical phase/SP promotion is made by this delivery.
