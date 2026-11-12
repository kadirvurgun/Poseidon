# BUNDLE-PORT-ISOLATION-01 — explicit owned localhost runtime

Source-author package from 8a9462d5f86ee114e3083e2904a93142f3936e64.
Execution: **DEFERRED**. Independent acceptance: **false**. No runtime is started,
reconfigured or stopped by authoring this package. Phase/status evidence belongs
to the coordinator's later frozen-target gate.

## Basis and bounded purpose

[Automation](../automation.md), active 01.11.2026 bundle cadence, and
[architecture](../architecture.md), test contract, require meaningful tests with
the product package and independent target-commit checks before acceptance.
[Security baseline](../security-baseline.md), identity and DEV-OIDC-LAB-01
sections, requires real code+PKCE, verified TLS/issuer and private artifacts.
[DEV-OIDC-LAB-01](dev-oidc-lab-01.md) LAB01–05 and
[OBSERVATION-SIGNED-FIXTURE-01](OBSERVATION-SIGNED-FIXTURE-01.md) criteria1–6
remain binding. Active source baseline is poseidon-v46.3-20261030 from
[active.json](../sources/active.json). Canonical 19.01
`analiz/POSEIDON GUNCEL 30.10.2026/07_TEST_PILOT_RELEASE/19.01_POSEIDON_v46.3_Test_Pilot_UAT_Release_Gates_TR.docx`
(SHA256 063823ea7d453adc91033c6eabb17f60906b4e6b9b4595be884eb12e02040a49),
paragraphs68–123/189–245, supplies regression/security/offline and Ship/Tug
gates; paragraphs124–135/162–176 preserve source and human authority gates.
Paragraph235 explicitly names Tug regression. These are software-test locators,
not approval of an operational source catalog or an expert/pilot sign-off.

The receiving preview already occupies the historical ports. A new source
checkout must be able to run its own complete genuine bundle without changing
that preview, its private generations, accounts, volumes or process. Historical
69bc92c signed174 evidence remains15PASS/159FAIL; new source is not a rerun or a
replacement for that evidence.

## Endpoint and publication contract

Only explicit genesis accepts port selection, all three together:

```powershell
py -3.13 scripts/bundle_oidc_lab.py init_source_bundle --app-port 15443 --provider-port 18443 --database-port 25432
```

The example numbers are inputs, not reserved/free-port evidence. Use a **new
owned isolated checkout**, pinned Python3.13.13/.NET10.0.403/Node26.13.0 and its
locked dependencies. The runtime has not been authorized to execute during this
authoring task. Omitted selection keeps5443/8443/55432 and omits new configuration
keys so existing legacy identity hashes remain unchanged. Subsequent verbs reject
all port flags; no migration of an existing nonempty private profile is provided.

Publish-once `.runtime/oidc-lab/owned-runtime.json` has exactly:

```json
{
  "schemaVersion": "poseidon.lab.owned-runtime.v1",
  "runId": "<canonical lowercase nonzero UUID>",
  "appPort": 15443,
  "providerPort": 18443,
  "databasePort": 25432
}
```

Ports are distinct native integers1024–65535; CLI spelling is canonical decimal.
The same record is `source-bundle-plan.json.ownedRuntime` and
`DevelopmentLab.OwnedRuntime` in genesis and every immutable configuration.
`DevelopmentLab.OidcPort` is the native provider integer. All authorities derive
only from HTTPS `localhost`, exact `/realms/poseidon-lab`, and those ports.
Raw port and manifest-generation integer lexemes must be canonical decimal;
policy floats elsewhere remain allowed. Kestrel has one LocalLab listener; database Host is exactly localhost with exactly
the selected Port. The private generation's existing immutable identity includes
the optional record and OidcPort; changing either cannot rotate a later snapshot.

The controlled child environment sets `POSEIDON_LAB_RUNTIME_FILE` to that exact
same-root file and `POSEIDON_REGISTER_URL` to the selected app origin. Ambient
application and Compose endpoint selectors are removed/overwritten. Metadata,
source plan, genesis, selected latest generation and browser URL must agree;
UTF8 BOM prefixes reject before decoding, preserving byte-exact immutable hashes.
On POSIX, root and binding files must remain current-UID owned with no group/other
permission bits, checked before/opened/after reads with stable identity/timestamps.
Windows retains the existing private publication ACL boundary;
missing selected markers, stale generation, duplicate JSON keys and path/link
substitution reject closed without private parser/path/body diagnostics.

## Ownership and component map

| Component | Binding |
|---|---|
| [lab_runtime.py](../../scripts/lab_runtime.py) | Shared strict Python schema, configuration binding, bounded file reads, availability preflight and Compose selectors |
| [oidc_lab.py](../../scripts/oidc_lab.py) / [bundle_oidc_lab.py](../../scripts/bundle_oidc_lab.py) | Validate before action; all3 ports checked before init/Compose/migration; own app marker retained |
| [compose.yaml](../../infra/oidc-lab/compose.yaml) | Published127.0.0.1 selected provider/database ports; internal8443/5432 unchanged; exact run/runtime identity labels |
| [bundle_private.py](../../scripts/bundle_private.py) | Optional immutable runtime identity; selected generation versus descriptor/plan |
| [lab-runtime.ts](../../tests/Poseidon.Browser.Tests/lab/lab-runtime.ts) | Same schema/ports, bounded regular files, duplicate-key rejection, genesis/latest-generation/hash-chain, recomputed immutable identity and app-origin binding |
| [journeys.ts](../../tests/Poseidon.Browser.Tests/lab/journeys.ts) | Actual provider/app origin, exact signed destination, secure cookies and genuine per-case login |
| [strict-browser.ts](../../tests/Poseidon.Browser.Tests/lab/strict-browser.ts) / [tls-cases.ts](../../tests/Poseidon.Browser.Tests/lab/tls-cases.ts) | Selected app origin; same-worktree CA and fresh NSS profile; actual wrong-CA and wrong-hostname rejection unchanged |
| [bundle_fixture_provision.ts](../../scripts/bundle_fixture_provision.ts) / [bundle_fixture_legacy_finish.ts](../../scripts/bundle_fixture_legacy_finish.ts) | Selected app and receipt binding; real signed session and current API read remain required |
| Fixture plan/crew/observation/human helpers | Same exact selected authority, existing actor/tenant/source policy gates retained |
| Backend separate author package | Development-only canonical `DevelopmentLab:OidcPort`; absent8443; exact authority/issuer, direct selected loopback transport and unchanged CA/name/EKU/expiry checks |

An occupied port is not ownership. Provider/database reuse requires exact Compose
project, service, running container, single127.0.0.1 selected binding and, for a
selected run, matching run UUID plus runtime identity labels. Current immutable
generation is validated before bundle verbs dispatch. Missing/stale private state
or orphaned previous-run labels cannot adopt listeners. Stopped services and
retained provider/database volumes also require current run/identity labels before
explicit genesis writes or start/stop actions, even with free ports. Loopback
availability checks include IPv6 localhost, without treating IPv4 Compose
ownership as authority over an IPv6 listener. App reuse retains the
existing exact DLL+worktree process marker. Stop remains exact owned process and
Compose project; no process-name/global stop, prune, volume deletion or preview
reconfiguration is introduced. Availability preflight cannot reserve the ports
against an external race; bind/start failure remains a failure with state retained.

The existing localhost certificate SAN needs no port changes. Browser cookies
are host-scoped, so ports are **not** the cookie isolation mechanism: fresh owned
per-context NSS profiles, fresh genuine per-case login and existing no-artifact
policy remain required. DataProtection application identity and private CA paths
remain per checkout. [bundle_extraction_peer.py](../../scripts/bundle_extraction_peer.py)
already binds an OS-selected loopback port0, records exact owner/PID/port, and
publishes its policy only to this private generation. [clamd_gate.py](../../scripts/clamd_gate.py)
already uses a named owned container with an OS-selected loopback port and real
PING/PONG. Neither adds a fixed fourth port. Existing object storage helper
ownership remains unchanged; no external storage/provider authority is inferred.

## Acceptance criteria and authored regression

1. Legacy defaults and immutable identity remain compatible without selection.
2. Explicit3-port genesis binds realm redirect/logout/webOrigin, API/provider/DB,
   configuration generation, provisioner, journal and browser to one owned run.
3. Invalid/partial/duplicate/untrusted inputs or foreign/stale listeners fail
   before runtime writes; occupied ports require exact ownership to be reused.
4. Missing metadata, wrong run/config generation, duplicate keys, tampering,
   nonregular/link/oversized input and ambient URL overrides cannot fall back.
5. Actual TLS, signed provider session, tenant/actor isolation, receipt readback,
   offline exact replay, no private artifacts and human authority gates remain.
6. Independent frozen-target protocol and full genuine observation/desktop/narrow
   gates demonstrate coexistence with the untouched preview before acceptance.

[test_lab_runtime.py](../../scripts/test_lab_runtime.py) authors legacy/selected
configuration and redirect tests, strict inputs, immutable run binding, malformed
files, no-write failure, all-port ownership, stale container labels and ambient
configuration negatives. [lab-runtime.test.ts](../../tests/Poseidon.Browser.Tests/lab/lab-runtime.test.ts)
authors matching TS decoding/configuration/generation/path/hash and no-fallback
regressions. Both are added to the existing protocol CI steps in
[backend-foundation.yml](../../.github/workflows/backend-foundation.yml).

Future frozen-bundle gate commands, **not executed by this author**:

```powershell
py -3.13 -m unittest discover -s scripts -p test_lab_runtime.py -v
# Use the repository pinned Node binary:
node --test tests/Poseidon.Browser.Tests/lab/lab-runtime.test.ts
```

The coordinator additionally gates existing source-fixture/private/crew/observation
protocols, backend selected transport, frontend/API package checks and the complete
genuine signed manifest/TLS probes. Runtime preparation needs actual scanner
PONG, exact owned extraction peer, complete8 source descriptors and2 signed lanes.
No credential/profile values are public test evidence. Production IdP/MFA, pilot,
source-policy approval, physical meter mapping/precision and Marine/Regulatory
human release authority remain external gates. Authoring and a local commit are
not execution PASS, full phase acceptance or merge approval.
