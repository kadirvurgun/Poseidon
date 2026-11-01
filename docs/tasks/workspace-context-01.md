# WORKSPACE-CONTEXT-01

AUTHORED / UNVERIFIED. DEV-021 / T-S2-002 / 5SP remains NOT ACCEPTED.

## Source and actual behavior

18.01 Development Backlog, sheet:Backlog/row:22,
SHA09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:
global vessel/role context, visible topbar, server-authorized vessel switch,
unsaved critical form warning. The task depends on DEV-015 and DEV-020.
13.04 Design System paragraphs29–34,
SHA83489a0f0752e22410d7534cdbe0b89726f00012d869e1b868e8127f5bcc573c:
topbar tenant/vessel/tug/role/language and connection/sync context; grouped
navigation and page context.

The real ProductApp, CrewWorkspace, TechnicalWorkspace, TugWorkspace and
ImportWorkspace headers now consume one shared context bar. This covers the
currently implemented screen families, including profile/Today/audit/register
views hosted by ProductApp. There is no claim to172/196 implemented screens.
Existing guarded navigation/context switching and unsaved/uncertain handling
remain with each owning workspace; the bar performs no navigation or writes.
It supplies the missing visible role/vessel context using current server reads.

GET `/api/v1/workspace/context` and `/api/v1/workspace/context/{vessel}` resolve
the actual server actor/tenant and apply the ordinary current vessel/kind scope
before returning labels. Unexpected query selectors are rejected. Responses are
no-store, safe404/400 bodies contain no labels/counts, and reads use the existing
attributed WorkspaceRead audit path. No request constructs an actor or bypasses
tenant query filters. No new operational entity, migration or seed is introduced.

## Role label configuration and authority

`WorkspaceContext:RoleCatalog` contains a version and tenant-local definitions
Reference/Tr/En. It resolves only GUID references already provisioned in the
actor's trusted RoleContext; it cannot create membership, grant a capability,
select a different role, delegate authority or manufacture appointments. No IdP
role claim, permission name, client selector or generic admin fallback supplies a
display role. Missing attribution is Missing; incomplete/invalid/ambiguous local
definitions are Unavailable. Partial multiple-role resolution is not presented as
complete. Only the current actor's definitions are returned.

[Synthetic example](../examples/workspace-context.synthetic.json) is NOT loaded
as a runtime default. At the later private lab gate the operator must bind the
actual disposable tenant/reference to explicit synthetic display labels and a
version. This is test configuration, not a real Master/DPA appointment or legal
approval. Production definitions remain actual operator configuration.

The client validates actor/tenant/vessel binding and role envelopes before
projecting fields. Old labels disappear synchronously when identity or vessel
changes; aborted/stale responses cannot restore them. Logout removes the bar.
Current-access errors hide labels and allow explicit reload. TR/EN uses the same
returned catalog; no credentials, actor IDs or raw role references are rendered or
persisted. Browser connection state is shown factually; offline context is marked
potentially stale. Immediate disconnected revocation knowledge is not claimed.

## Authored checks, all DEFERRED

| Layer | Authored cases |
|---|---:|
| API-project pure policy | 9: trusted reference, no membership inference, five invalid/missing definitions, duplicates, partial multi-role mapping |
| Actual API/PostgreSQL | 9: current scope/role labels, all ungranted/foreign vessels, three untrusted selectors, unavailable labels, missing attribution, grant revocation and anonymous denial |
| React component | 8: bilingual labels/no IDs, delayed response isolation, same-tenant actor change, logout, explicit retry, Missing/Unavailable and factual offline state |
| Client wire boundary | 11: safe projection, actor/tenant/vessel mismatch, malformed enum arrays, duplicates, empty labels, partial roles and unexpected/missing vessel |
| Actual-host Playwright | 2 registered cases: five authorized headers checked against real API and restricted missing-scope direct URL; no route mocks |

39 cases are an authored inventory, not execution results. Suites/build/typecheck,
OpenAPI generation, browser/provider lab, independent QA and CI were NOT RUN.
The private browser case deliberately fails for absent actual fixture role-label
configuration rather than skipping or accepting an unconfigured role as proof.
The client interface is a handwritten validated projection of the new server DTO,
not a claim of completed OpenAPI generation; the larger gate must regenerate and
check the actual contract. Source-only whitespace and AST maintenance are not
product validation. No source/SP/task/phase acceptance is awarded here.

## Remaining full-story gates

Integrate with final Domain changes rather than replacing those workspaces;
preserve their actual private profile/offline/planning flows and navigation
guards. Run prior context-switch/dirty-form cases and these cases together at the
larger bundle gate, then independent exact-target QA/required CI and source
acceptance. Resolve actual DEV-015/020 dependencies. The bar does not implement
future delegation or authoritative queue sync status: those require the owning
source contracts and real runtime state; connection status does not imply sync,
verification, freshness approval or operational readiness. Technical header
authoring alone does not close every13.04 UX or DEV-021 acceptance criterion.
