# NAV-WORKSPACE-01 — prerequisite and implementation contract

Status: source preparation only; implementation awaits independent approval of profile repair `ee3339e`. NOT ACCEPTED. Coordinator owns independent QA, publication and merge. No extra agents, push, PR, merge or authentication changes in this assignment.

## Reconciled baseline

Isolated worktree branch `agents/9ff6b20f711a4809a7bf5a1a3ced7172` was clean at `ee3339e`. No uncommitted partial implementation was present to reconcile. Existing profile repair is preserved. Its phase report explicitly says fresh independent target-commit QA is pending; historical DIR approval and local repair test results do not satisfy the user's prerequisite. Approval evidence was requested before dependent implementation.

Read: AGENTS.md, backend/UI role skills, architecture, automation, security baseline, seed and dependency policy, and `docs/sources/active.json`. MCP code graph tools were unavailable; `graphify query "application navigation shell actor permissions vessel profile dirty navigation"` succeeded before targeted code reads. No graph connection is claimed. Existing ProductApp, ActorResolver, FoundationEndpoints and SecurityAudit were inspected without modification.

## Controlled sources and decisions

All paths below are relative to `analiz/POSEIDON GUNCEL 30.10.2026/`. Retrieval used `C:/Users/avurgun/AppData/Local/Programs/Python/Python313/python.exe scripts/query_sources.py <phrase> --limit 4`, then targeted source reads. Locator numbering below follows the controlled source index, including paragraphs in tables; top-level DOCX paragraph counts are different and must not be substituted. No complete chunks file was loaded into model context.

| Exact source locator | Meaning for this package |
|---|---|
| `06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`, `sheet:Backlog/row:23` | DEV-022 / T-S2-003: role-first simplified shell; full-product search only for authorized power users; dependencies DEV-015 and DEV-021. The historical 172-screen wording does not authorize a flat menu or acceptance of the current 196-screen inventory. |
| Same file, `sheet:Backlog/row:26` | DEV-025 / T-S2-006: keyboard focus, contrast, text/icon states, responsive breakpoints and form error semantics; dependency DEV-022. |
| `04_UX_PRODUCT_DESIGN/13.02_POSEIDON_v46.3_196_Ekran_Gorselli_Kullanim_Katalogu_TR.docx`, `paragraph:18` | Catalog is inventory/prototype intent; role/state permission, offline, audit and tests belong to controlled traceability. Screen count is not service count. |
| Same file, `paragraph:93`–`paragraph:109`, `paragraph:1259`–`paragraph:1272` | Fleet, vessel and global search contexts exist as candidate screen intent. Their proposed dashboard/command-center API projections are marked READY FOR BA/DEV REVIEW; these are not implemented endpoint or permission approvals. |
| `04_UX_PRODUCT_DESIGN/13.04_POSEIDON_v46.3_Design_System_ve_UX_Standartlari_TR.docx`, `paragraph:11`–`paragraph:12`, `paragraph:19`–`paragraph:25` | Sidebar/menu search; role/mode visibility; distinct Ship/Tug; human authority; no silent loss. |
| Same file, `paragraph:29`–`paragraph:42` | Topbar context/language/connection state, current destination, responsive content and mobile actions. |
| Same file, `paragraph:68`–`paragraph:76`, `paragraph:94`–`paragraph:103` | Explain empty/error/loading; persistent conflict/failed states; incomplete drafts allowed; authoritative server validation; draft is separate from verification. |
| Same file, `paragraph:105`, `paragraph:110`–`paragraph:114`, `paragraph:118`–`paragraph:131` | Server search/pagination, cohesive TR/EN, labels/errors, visible keyboard focus, contrast, role/context clarity and responsive journey gates. |

Decision NAV-D01: capability-first foundation limited to existing company/fleet/profile workflows. Full operational role catalog is unresolved. Do not invent Master/Chief Engineer roles, approved Today dashboards, safety authority or unavailable destinations. Existing server-provisioned directory permissions and vessel grants remain the authority for implemented records; source screen names do not grant permissions.

Decision NAV-D02: search requires a separate explicit server-provisioned default-deny capability, whitelisted by ActorResolver. Generic read must not imply power search. Only implemented, authorized navigation and vessel records may be returned. Existing directory filtering is not evidence of full-product-search authorization. Proposed source API names need not become dead production links.

Decision NAV-D03: selected context is untrusted until freshly authorized by the server. No speculative offline cache, fake login, production test-auth or client-supplied actor/role/tenant/grants. If location persistence is added, isolate it by trusted account and tenant without sensitive stored values. Do not introduce an operational entity just to persist navigation.

## Acceptance and authored-test plan — all NOT IMPLEMENTED / NOT RUN

These are acceptance targets, not authored or passing tests. Final implementation must replace this plan with actual test names, commands and results.

| Trace ID | Acceptance target | Required evidence |
|---|---|---|
| NAV-01 / DEV-022 | Server determines implemented destinations and search capability; restricted vessel users see no forbidden company metadata, destination, identifier or count. Direct endpoints enforce the same scope. | Real API/PostgreSQL: anonymous, default denial, tenant-wide and scoped readers/editors, cross-tenant, wrong vessel, revoked grants, spoofed request/IdP capabilities. |
| NAV-02 / DEV-022 | Explicit power search with bounded input, deterministic bounded pagination, authorized record projections and fixed audit actions/results; no raw query in logs. | API/PostgreSQL search boundaries, pagination/ties, denial and fixed audit events, forbidden ID/count non-disclosure. |
| NAV-03 / DEV-021 | Real URL navigation, refresh and Back/Forward preserve allowed selection/profile; invalid/encoded/wrong-scope/stale links fail safely. `/app` and `/agents` retain working paths; return URLs fail closed. | Route/component regressions plus API authorization and return URL regressions; real browser anonymous/setup route smoke separately. |
| NAV-04 / DEV-021 | Dirty guard covers links, vessel switch and history; cancel preserves location and exact draft; discard resets it. Uncertain save retains frozen payload/key and blocks navigation until resolved. | React route/history/profile/editor tests including language, search, mobile drawer, repeated popstate and delayed retry. Existing precision/history/conflict tests retained. |
| NAV-05 / DEV-025 | TR/EN sidebar/topbar, distinct Ship/Tug active context, mobile drawer, skip link, visible current destination, authorized keyboard search, focus restoration/live regions and responsive overflow. | React keyboard/mobile/search/focus tests; real Chromium anonymous/setup/keyboard smoke distinguished from authenticated mocked components. |
| NAV-06 / DEV-025 | Loading/empty/error/401/403, stale/revoked context and offline/failed fetch remain visible and unverified. Company/fleet management and profile precision/history/save semantics remain intact. | React transport/component regressions and full affected integrated gate after coherent implementation/test authoring. |

## Delivery gate and limitations

No product code or test changes yet. Backend build, MTP suites, real PostgreSQL/API, actual OpenAPI export/type generation, frontend checks/components and browser suite: **NOT RUN for NAV-WORKSPACE-01**, because implementation has not started. No prior profile counts are reused as NAV verification. No dependencies changed; no migration, seed or graph topology change is introduced by this preparation.

After approval evidence arrives: implement the entire package and authored tests together, then run the affected integrated gate with .NET 10 / Node 26 and existing MTP conventions; export actual OpenAPI if contracts change, regenerate consumers, update Graphify, record exact counts/errors, and commit locally for independent final-commit QA. Never self-accept. External IdP browser validation, operational roles/lookups/mandatory policy, expert/pilot/release and canonical S0/S2 gates remain open. Canonical progress remains 8/970 SP, 2/121 tasks, 0/196 accepted screens.


Coordinator prerequisite reconciliation: independent Esra Sol review approved exact profile code `ee3339e58747302c5effd9baef8f05b262c33e0e` with zero blocking issues. Committed evidence: [review](../reviews/profile-draft-ee3339e.json). This resolves the earlier missing-evidence stop. Proceed with actual NAV implementation and tests; NAV checks/independent acceptance remain pending. No canonical/IdP/expert/release approval or publication.
