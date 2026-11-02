"""Current value-free finding identities for coordinator review; never an exception file."""
import json
from pathlib import Path
import subprocess

import secret_scan
from source_review_evidence import context_evidence

ROOT = Path(__file__).resolve().parents[1]


def report(root=ROOT):
    root = Path(root).resolve()
    covered, findings = secret_scan.scan(root, 'repository')
    previous = json.loads((root / 'docs/security/files-source-classifications.pending.json').read_text())
    prior = {secret_scan.object_hash(e['target']): e for e in previous['entries']}
    entries = []
    for target in sorted(findings, key=lambda t: (t['path'], t['occurrence'], t['rule'])):
        identity = secret_scan.object_hash(target)
        old = prior.get(identity)
        evidence = None
        # Nested/archive presentations retain their exact original-byte identities.
        # Do not manufacture a context when its hashed locator cannot be reopened.
        path = root / target['path']
        if path.suffix.lower() in ('.cs', '.py', '.ts', '.tsx') and path.is_file():
            raw = secret_scan.read_file(path, root)
            if secret_scan.source_revision(raw) != target['immutable_source_revision']:
                raise ValueError('changed-during-review')
            evidence = context_evidence(secret_scan.presentation(raw), target)
        classification = 'unresolved'
        rationale = 'Pattern identity requires exact-target independent classification and any applicable private trust/rotation remediation.'
        # This dedicated unit fixture consumes its constants only in local Identity
        # hash/verification assertions. This proposal supplies no scanner trust.
        if target['path'] == 'tests/Poseidon.Persistence.Tests/PasswordHashingTests.cs' and evidence is not None and target['immutable_source_revision'] == 'git-blob-sha256:f7240a8213aa4e489e16bc42bc851280530f678f00d0635c77d58a92b8ced169':
            classification = 'synthetic-fixture'
            rationale = ('Dedicated PasswordHashingTests unit fixture; the literal is an input to local Identity password hashing and verification assertions. '
                         'This is an identity-bound engineering classification proposal only; coordinator must independently review the exact source and consumers.')
        entries.append({'target': target, 'classification': classification,
                        'status': 'pending-independent-review', 'rationale': rationale,
                        'author_reference': 'FOUNDATION-ACCEPTANCE-BUNDLE-01-author',
                        'previous_exact_identity': old is not None,
                        'previous_evidence_sha256': old.get('evidence_sha256') if old else None,
                        'context_evidence': evidence,
                        'context_evidence_sha256': secret_scan.object_hash(evidence) if evidence else None,
                        'rotation_remediation_gate': classification == 'unresolved'})
    return {'schema_version': 'foundation-current-review-v1', 'task': 'FOUNDATION-ACCEPTANCE-BUNDLE-01',
            'status': 'PENDING_INDEPENDENT_REVIEW_SECURITY_GATE_FAILED', 'is_exception_file': False,
            'observed_head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
            'covered_tracked_files': covered, 'current_findings': len(findings),
            'previous_pending_findings': len(prior),
            'retained_exact_identities': sum(e['previous_exact_identity'] for e in entries),
            'entries': entries, 'receipts': [], 'exceptions': [],
            'authority': 'No pin, reviewer approval, waiver, trust variable or credential rotation supplied. Old rejected review remains rejected.'}


if __name__ == '__main__':
    try:
        result = report()
        output = ROOT / 'docs/security/foundation-acceptance-review.pending.json'
        output.write_text(json.dumps(result, indent=2, ensure_ascii=True) + '\n')
        print(json.dumps({k: result[k] for k in ('status', 'covered_tracked_files', 'current_findings', 'retained_exact_identities')}))
    except Exception:
        print(json.dumps({'status': 'failed', 'stage': 'current_value_free_classification'}))
        raise SystemExit(1) from None
