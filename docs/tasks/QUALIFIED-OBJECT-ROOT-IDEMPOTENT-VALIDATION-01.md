# QUALIFIED-OBJECT-ROOT-IDEMPOTENT-VALIDATION-01

State: AUTHORED_UNVERIFIED. Acceptance=false; canonicalSP added0. Base0e197a29d661af6b896a146fbfece60bbc48d5ea. All new tests/native ACL actions/build/provider/browser execution is DEFERRED until the coherent source freeze, independent QA and a separate Root execution lease.

The actual owned genuine gate failed at QF preflight: [bundle source root](../../scripts/bundle_oidc_lab.py) called the unchanged [private directory creator](../../scripts/oidc_lab.py) again, reaching [Set-Acl](../../scripts/oidc-lab-private.ps1) while the real owned API was already running. The original OS error text is unavailable. This task does not diagnose AccessDenied, a lock defect or production ACL behavior from that source location.

Read-only post-cleanup diagnosis found the existing exact owned temporary object root already satisfies the creator contract: current-user owner, protected DACL, one current-user FullControl OI/CI allow, no other/inherited/deny grants. Two object files were retained; metadata lock/access probes succeeded after cleanup. Existing successful ACLs need validation, not repeated mutation.

The scope supports [QOF-05](QUALIFIED-OWNING-SIGNED-FIXTURE-01.md) and the existing [secure-file contract](secure-files-01.md): local Development fixture storage remains outside repository/content/web roots and owner-only. Active baseline remains [active.json](../sources/active.json). This is an engineering gate prerequisite, not a new business entity, approval, verified seed, storage provider acceptance or ship-safety decision.

## Acceptance and traceability

- **QOR-01:** [bundle_object_root.ensure](../../scripts/bundle_object_root.py) derives the same exact worktree/project temporary path. No arbitrary CLI/request path, actor or tenant input is added. Repository/Git ancestors, links/junctions, non-directory leaves and changed identity fail closed.
- **QOR-02:** Existing roots are read-only: Windows uses current token plus descriptor snapshot only, requiring the exact creator owner/protected single FullControl OI/CI allow contract; POSIX requires current effectiveUID and exact0700. Unknown, broad, deny, inherited or wrong-owner ACLs fail without repair. Existing object bytes/handles remain untouched.
- **QOR-03:** Missing roots are created exclusively, restricted exactly once before runtime/object writes using the unchanged creator, then owner/path/identity/ACL are read back. Racing roots are not adopted; failed restriction/readback retains evidence without cleanup/retry. No restriction method is called for existing roots.
- **QOR-04:** Approved oidc_lab.py and oidc-lab-private.ps1 remain byte-identical. QF source/run/adapter/scanner/producer checks, real commands, actor grants, signed authentication and full manifest assertions remain unchanged. Only source_object_root delegates to the bounded helper.
- **QOR-05:** [Source-authored regressions](../../scripts/test_bundle_object_root.py) cover fresh-once/readback, retained/open objects with no restriction, malformed/broad/deny/inherited/foreign ACL, identity substitution, creation race, path/Git/reparse/real parent aliases and native Windows/POSIX behavior. Platform skips are explicit applicability, never full-OS acceptance. Required Windows and Linux CI protocol steps include the new suite.

The Windows reader reuses only snapshot/token reads from [lab_profile_acl.WindowsAcl](../../scripts/lab_profile_acl.py). Its Firefox-profile privileged allowlist/protect method is not used for object roots. Object-root policy remains the strict single-current-user contract, without widening to SYSTEM/Administrators or disabling filesystem/TLS/browser checks.

## Deferred verification and limitations

At the later matching gate, execute the new Python suite on pinned Windows Python3.13.13 and applicable Linux Python3.13.13, then affected source-bundle protocols and required exact-source scan/genuine continuation under a separate lease. No test, ACL restriction, provider restart or old failed-lab modification occurred during authoring. The original genuine0e failure/source/artifacts/private state remain immutable; full register/TLS had not run there. Source-only review and a normal commit are not test PASS or canonical phase acceptance.
