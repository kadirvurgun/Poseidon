# WINDOWS-OBJECT-ROOT-NATIVE-01

State: AUTHORED, independent source review and targeted verification pending. Acceptance=false; canonicalSP added0. Base116701b64390fd337310d3a785e92bb8f2d76bbd. All new execution is deferred except the specifically authorized single failing native method and its post-review targeted verification.

The actual Windows protocol gate failed [the fresh/exclusive-child regression](../../scripts/test_bundle_object_root.py) at its closed restriction assertion. One authorized, isolated reproduction ran only that method:1test/1failure/0error/0skip. The actual PowerShell child returned1 with the fixed `Set-Acl`/`SeSecurityPrivilege` categories at the original [creator](../../scripts/oidc-lab-private.ps1) line9. Raw output remained in memory. This does not diagnose the earlier genuine object-root failure or alter its retained evidence.

## Source and scope

This is a correction to [QOR-03/QOR-05](QUALIFIED-OBJECT-ROOT-IDEMPOTENT-VALIDATION-01.md) and the existing [secure-file storage boundary](secure-files-01.md). Root explicitly authorized the narrow exception to that earlier task's QOR-04 byte-identity constraint for the PowerShell setter. [The Python creator](../../scripts/oidc_lab.py), [object-root helper](../../scripts/bundle_object_root.py), existing-root read-only validation, owner/identity/reparse/race boundaries, protected single-owner FullControl OI/CI ACL and all authority/fixture/provider contracts remain unchanged.

The fresh `DirectorySecurity` modifies only Owner and Access rules. The direct `FileSystemAclExtensions.SetAccessControl` API persists those modified sections, retaining the exact protected current-user FullControl DACL without asking for SACL/audit privilege. Audit and group sections are never read, written or cleared. No privilege elevation, broader grant, retry, exception suppression or ACL repair is introduced. See the [.NET setter](https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/System.IO.FileSystem.AccessControl/src/System/IO/FileSystemAclExtensions.cs) and [modified-section persistence](https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/System.IO.FileSystem.AccessControl/src/System/Security/AccessControl/FileSystemSecurity.cs).

## Shared caller review

| Caller | Existing contract and bounded impact |
|---|---|
| [oidc_lab.private_directory](../../scripts/oidc_lab.py) | Same internal path, mkdir and explicit Windows restrict command; POSIX0700 unchanged. The setter changes only persistence of its already-authored owner/DACL. |
| [oidc_lab.init](../../scripts/oidc_lab.py) | Returns on existing ready marker and rejects partial nonempty genesis before restricting; no configuration/account rotation added. |
| [bundle init](../../scripts/bundle_oidc_lab.py) | Existing fresh/private/runtime ownership preflights remain before first private writes. |
| [bundle object-root ensure](../../scripts/bundle_object_root.py) | Restrict is called exactly once only after exclusive creation. Existing roots use two read-only ACL snapshots and identity checks; open object bytes/handles remain untouched. |
| [ordinary browser gate](../../scripts/browser_gate.py) | Same fresh per-owner private evidence path before generated configuration. No invocation or legacy path change in this task. |
| [owned S3 gate](../../scripts/s3_gate.py) | Same fresh per-owner private environment directory; owner/provider cleanup, raw-output closure and process containment unchanged. No provider invocation in this task. |
| [oidc_lab.prepare_files](../../scripts/oidc_lab.py) | Legacy explicit object-root preparation retains its existing call policy. This task does not invoke it, broaden its scope or rewrite an existing external root. |
| [PowerShell process check/stop](../../scripts/oidc-lab-private.ps1) | Branches are byte-identical and do not reach the restrict setter. |

## Acceptance and tests

- **WOR-01:** On the ordinary Windows identity, the actual fresh-root native method succeeds with the exact owner/protected one-rule FullControl OI/CI ACL; the parent descriptor is unchanged. No SACL privilege is requested or granted.
- **WOR-02:** Repeated existing-root validation with a child held exclusively open performs no additional restriction and preserves both descriptor and object bytes. Existing denial/race/identity/readback tests and their assertions remain intact.
- **WOR-03:** The authored shared-creator native regression exercises the real Python creator plus actual captured PowerShell setter, validates its exact child ACL and unchanged parent, and fabricates no successful child response. Its execution is deferred to the larger gate.
- **WOR-04:** After opposite-role source QA, rerun only the original failing native method on pinned Python3.13.13 with raw output in memory and closed source locations/counts. Full Windows/Linux protocols and the larger affected native/genuine gate remain separate required work; platform skips do not imply cross-OS acceptance.
- **WOR-05:** Preserve the original116 failed protocol report, original2b backend receipt,665 source comparison, all old private labs/profiles/objects and parent ACLs. No backend/C#/policy/auth/source-catalog/dependency changes, new authority or story-point acceptance. The new helper bytes require an explicit later consumer/evidence binding; the prior116 parity is not relabeled.
