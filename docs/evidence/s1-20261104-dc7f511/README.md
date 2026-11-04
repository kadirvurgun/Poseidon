# S1 engineering acceptance evidence — 2026-11-04

Implementation `dc7f5117fb99a962d70a54b1f12f2c118f1cca66`, tree `e8843d8fb0bffd56a6d7955dfc286b8113a09bfd`.
The seven original DEV-013..019 tasks total50SP. [Independent acceptance](qa/final-s1-independent-review-dc7f511-signed06.json) binds the literal current source criteria and dependencies. This is software engineering evidence; production/operator, legal, SME and pilot approvals remain separate.

[index.json](index.json) lists original locations, byte counts and SHA256 for every unchanged issued JSON. Scoped [.gitattributes](.gitattributes) preserves bytes in Git. The coordinator observed the independent agent role and issued report; repository hashes alone are not a reviewer identity trust root.

- [Exact implementation CI](https://github.com/kadirvurgun/Poseidon/actions/runs/41617601700):4jobs/19source-bound commands; API137unit/582integration with0failed/skipped. Other unissued native totals remain unavailable.
- [Full signed closure](signed/s1-complete-verification.json):122unique executions,61original cases in each of twoFirefox projects,10closed groups,0failed/pending/skipped/retried; baseline2real logins and strictTLS3 are separate. Preview, original private inputs and requested test-user configuration were restored/preserved.
- [Separate genuine provider logout](qa/genuine-logout06-execution.json):1actual case, outside the122/baseline counts; provider end-session, app session removal, protected401 and fresh credential prompt.
- [Runtime audit report](ci/critical-write-replay.json):82/82valid-successful operations,558capture instances,0invalid/unavailable. All558value-free capture files are included for offline parser replay.
- [Historical failures](history/): first CI target4978 had580total/577PASS/3FAIL and80/82diagnostic; signed05 had104PASS/18FAIL. Original bytes and targets are retained separately from passing06.

The original CI observation summary's pending-evidence label is its timestamped first snapshot; subsequent [artifact verification](ci/api-artifact-verification.json) and independent review verify the genuinely closed native run. No issued receipt has been retargeted to a later metadata/merge commit.

Replay the82-operation metadata against a clean worktree pinned to implementationF. The parser enforces the original commit/tree and clean-source binding; a later metadata or merge checkout cannot substitute forF. Application tests are not run:

```text
git worktree add --detach .runtime/s1-audit-replay dc7f5117fb99a962d70a54b1f12f2c118f1cca66
py .runtime/s1-audit-replay/scripts/critical_write_coverage.py --root .runtime/s1-audit-replay --directory docs/evidence/s1-20261104-dc7f511/ci/api-test-counts/critical-write-65319971caa245feb88e9af833c152f6 --contract .runtime/s1-audit-replay/contracts/security/s1-current-scope.v1.json --native-evidence docs/evidence/s1-20261104-dc7f511/ci/api-test-counts/integration.json
```

Private configuration, credentials, keys, cookies, DOM and SDK logs are excluded. Re-running the signed browser gate requires separately provisioned native provider actors, strictTLS and owned private fixtures; this archive does not supply those credentials. Later publication heads require their own required checks and independent source-equivalence review before normal merge.
