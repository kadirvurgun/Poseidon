# S0-THREATS-01

Source: 18.01 Backlog row7 DEV-006 / T-S0-006; docs/security-baseline.md

Prepare docs/security/threat-model.md from docs/security-baseline.md and the merged s0 ADRs. DEV-006 requires tenant isolation, authn/authz, file upload, protected reporting, secrets, audit, offline devices and AI prompt/data threats. Cover each with concrete abuse scenarios, current controls versus gaps, verification and responsible role; map P0 mitigations to actual DEV IDs obtained by bounded scripts/query_sources.py. No invented implementation, retention durations, production hosting or compliance approval. Call out EF write audit versus HTTP read/security auditing and bootstrap/admin context boundaries. Include log redaction, source documents treated as data, tenant-bound retrieval and no AI authority over safety. This is draft engineering evidence until authorized security review. No source-pack edits, SP acceptance or runtime changes.

Acceptance:
- Each required threat area has assets, trust boundary, abuse case, mitigation, verification and owner
- P0 mitigation table maps real backlog IDs and explicitly records unimplemented controls
- No GDPR/KVKK compliance or expert approval claimed

## Local documentation delivery — 2026-11-01

Phase: S0, draft engineering evidence awaiting authorized security review. Traceability: S0-THREATS-01 / DEV-006 / T-S0-006. Responsible role: Security/Architect. This local package does not accept DEV-006, complete a canonical sprint/release gate or change accepted SP. Publication and independent review remain coordinator-only. Original task instructions above are preserved.

Delivered [threat model](../security/threat-model.md) from the security baseline and S0 ADRs in this checkout. ADR approval fields remain OPEN. Scoped current-code inspection found foundation OIDC/session/directory and selected HTTP security events beyond older baseline/ADR implementation descriptions; the draft distinguishes that partial code from full operational security coverage. No runtime/authentication changes or real login were performed.

| Acceptance / traceability | Evidence | Local result |
|---|---|---|
| S0-THREATS-01-AC1 / DEV-006 | TM-01–TM-08 each specify assets, trust boundary, concrete abuse, current controls, mitigation/gaps, verification and owner | Documented; runtime cases remain implementation gates |
| S0-THREATS-01-AC2 / T-S0-006 | P0 table maps real B workbook rows and task IDs, distinguishes partial/unimplemented controls and explicitly leaves additional assignment open | Workbook row/ID/task/priority consistency checked; no mitigation accepted |
| S0-THREATS-01-AC3 | Draft/review status, no GDPR/KVKK compliance or expert approval, no invented retention/hosting, no AI safety authority | Documented; security/legal/Marine/Regulatory review OPEN |
| Security baseline boundaries | TM-05 redaction; TM-06 EF write versus selected HTTP/security coverage, independent sink limitations, audited SaveChanges and bootstrap/admin separation; TM-08 untrusted sources and tenant-bound retrieval | Explicit current evidence versus gaps |

Source retrieval used bounded local `py scripts/query_sources.py "<phrase>" --limit 4`, with no model calls or corpus loaded into context. Successful backlog-bearing phrases included `DEV-006`, `tenant`, `DEV-015`, `DEV-016`, `DEV-018`, `DEV-029`, `DEV-033`, `DEV-034` and `DEV-051`. Each mapped row was then read directly from the active 18.01 workbook: Backlog rows 7, 14–20, 30, 34–36, 39, 52 and 69. Bounded `protected reporting`, `Protected report` and `prompt` lookups located the cited 15.01/17.01/19.01 DOCX paragraphs, which were read directly using ZIP/XML paragraph locators. Source classification remains candidate/unverified; historical archives are not passing evidence.

The repository backend skill was applied for tenant/audit/authority boundaries; no delegation was used. Code graph MCP tools were unavailable and this worktree has no `graphify-out/graph.json`; targeted reads followed the ADR links. Only documentation changed, so no code graph update was required. Project-status, phase reports, source packs and canonical accounting remain untouched under the trusted two-file scope.

## Repeatable documentation tests

Run from the worktree root with `py -` (PowerShell here-string input works). These tests check source traceability, structural coverage, relative links, preserved task content and edit boundaries. They cannot establish runtime mitigation effectiveness or replace independent semantic/security review.

```python
from pathlib import Path
import hashlib
import json
import re
import subprocess
from openpyxl import load_workbook

allowed = {'docs/security/threat-model.md', 'docs/tasks/s0-threats-01.md'}
model = Path('docs/security/threat-model.md').read_text(encoding='utf-8')
for n in range(1, 9):
    block = model.split(f'### TM-{n:02d} — ', 1)[1].split('\n### ', 1)[0]
    for field in ('Assets / boundary', 'Abuse', 'Current controls',
                  'Gaps / mitigation', 'Verification', 'Owner'):
        assert f'**{field}:' in block, (n, field)

active = json.loads(Path('docs/sources/active.json').read_text(encoding='utf-8'))
source = Path(active['source_root']) / (
    '06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx')
digest = hashlib.sha256(source.read_bytes()).hexdigest()
assert digest in model, 'Backlog source hash changed'
wb = load_workbook(source, read_only=True, data_only=True)
mapped = set(re.findall(r'(DEV-\d{3}) \((\d+); (T-[A-Z0-9-]+)\)', model))
assert mapped, 'No mapped rows'
for dev, row_number, task in mapped:
    row = next(wb['Backlog'].iter_rows(
        min_row=int(row_number), max_row=int(row_number), values_only=True))
    assert (row[0], row[8], row[12]) == (dev, 'P0', task), (dev, row_number)
wb.close()
all_ids = set(re.findall(r'DEV-\d{3}', model.split('## P0 mitigation backlog')[1]))
assert all_ids == {dev for dev, _, _ in mapped}, 'Unverified mitigation ID'

links = 0
for name in sorted(allowed):
    path = Path(name)
    text = path.read_text(encoding='utf-8')
    assert all(line == line.rstrip() for line in text.splitlines()), name
    # Exclude code blocks from Markdown link parsing.
    prose = re.sub(r'```.*?```', '', text, flags=re.S)
    for target in re.findall(r'\]\(([^)]+)\)', prose):
        if '://' in target or target.startswith('#'):
            continue
        assert (path.parent / target.split('#')[0]).is_file(), (name, target)
        links += 1
task_path = 'docs/tasks/s0-threats-01.md'
assert Path(task_path).read_bytes().startswith(subprocess.check_output(
    ['git', 'show', 'HEAD:' + task_path])), 'Original task content changed'
changed = set(subprocess.check_output(
    ['git', 'diff', '--name-only', 'HEAD'], text=True).splitlines())
untracked = set(subprocess.check_output(
    ['git', 'ls-files', '--others', '--exclude-standard'], text=True).splitlines())
assert changed | untracked <= allowed, changed | untracked
for name in ('docs/project-status.md', 'docs/project-status.json'):
    assert Path(name).read_bytes() == subprocess.check_output(
        ['git', 'show', 'HEAD:' + name]), name
assert not subprocess.check_output(['git', 'diff', 'HEAD', '--', 'docs/phases/'])
subprocess.run(['git', 'diff', '--check'], check=True)
print(f'PASS: 8 threat areas; {len(mapped)} P0 source mappings; '
      f'{links} relative links; original task preserved; only 2 scoped files; '
      'canonical status/phase files unchanged')
```

## Verification and handoff

Results below belong only to this local uncommitted documentation package. Runtime abuse cases in the threat model are future acceptance requirements, not executed/passing product tests. Full backend/frontend/PostgreSQL/browser checks and independent QA remain coordinator gates under [architecture](../architecture.md) and [workflow](../../.github/workflows/backend-foundation.yml); they are not certified by this document. No publish step was run.

| Local verification | Exact result |
|---|---|
| Repeatable documentation tests above; `git diff --check` | PASS: 8 threat areas, 15 distinct P0 source mappings, 20 relative links; original task preserved; only the 2 scoped files changed; canonical status/phase files unchanged |
| `py scripts/verify_sources.py` | PASS: baseline poseidon-v46.3-20261030; 269 files, 34,958 chunks, 0 mismatches; lineage verifier did not expose corpus contents to model context |
| `py -m unittest discover -s scripts -p 'test_*.py' -v` | PASS: 127 tests in 164.967s; 0 failures/errors/skips; automation uses mocks/synthetic fixtures, not real model dispatch or account login |
| Backend/frontend/PostgreSQL/browser suites and independent security QA | Not run for this documentation-only local package; remain required coordinator checks, not passing or waived |

Open blockers: authorized security review, implementation/evidence for residual P0 gaps, scope assignments identified in the table and the ADR/security/legal/device/provider decisions listed in the model. Next step: coordinator obtains independent review and assigns the gaps before publication or capability enablement. No canonical accepted SP or status update is part of this delivery.
