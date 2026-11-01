"""Targeted, redacted authoring inventory from the archived failed gate (not a scan gate).

Reads only cited inputs/members and exact nearby candidate contexts. Originals are read-only.
Proposals never suppress scanner findings or authorize publication/rotation.
"""
import io
import json
import zipfile
from pathlib import Path
import secret_scan

BASE = '0bb2492f8eff66e92109ebad18c8c8c55b64621b'


def source_context(root, locator):
    parts = locator.split('!')
    first = parts.pop(0).removesuffix('#text').removesuffix('#utf16')
    original = secret_scan.read_file(root / first, root)
    raw = original; expanded = 0
    if len(parts) > secret_scan.MAX_DEPTH + 1:
        raise ValueError('archive-depth')
    for part in parts:
        name = part.removesuffix('#text').removesuffix('#utf16')
        with zipfile.ZipFile(io.BytesIO(raw)) as archive:
            infos = archive.infolist()
            if len(infos) > 10000 or len({m.filename for m in infos}) != len(infos):
                raise ValueError('archive')
            member = archive.getinfo(name)
            expanded += member.file_size
            if member.flag_bits & 1 or member.file_size > secret_scan.MAX_MEMBER or expanded > secret_scan.MAX_EXPANDED:
                raise ValueError('archive')
            with archive.open(member) as stream:
                raw = stream.read(secret_scan.MAX_MEMBER + 1)
            if len(raw) != member.file_size:
                raise ValueError('archive')
    if locator.endswith('#text'):
        from xml.etree import ElementTree
        decoded = secret_scan.presentation(raw)
        if '<!DOCTYPE' in decoded or '<!ENTITY' in decoded:
            raise ValueError('xml')
        text = ''.join(ElementTree.fromstring(raw).itertext())
    elif locator.endswith('#utf16'):
        text = secret_scan.presentation(raw)
    else:
        text = raw.decode('utf-8', errors='replace')
    return text.replace('\r\n', '\n').replace('\r', '\n'), raw, secret_scan.source_revision(original)


def context_for(root, locator):
    return source_context(root, locator)[0]


def inventory(root):
    from source_review_evidence import context_evidence
    evidence = json.loads((root / 'docs/phases/AUDIT-FILES-BUNDLE-01.evidence.json').read_text(encoding='utf-8'))
    origins = [('repository_scan', evidence['repository_scan']), ('final_changed_input_scan', evidence['final_changed_input_scan'])]
    entries = {}; missing = []; cache = {}; contexts = {}
    # Historical author suggestions remain preserved, but confer neither classification
    # proof nor approval under v2. All conclusive decisions belong to independent review.
    for scope, report in origins:
        for old in report['findings']:
            path = old['path']
            try:
                if path not in cache:
                    cache[path] = source_context(root, path)
                text, raw, source = cache[path]
                candidates = list(secret_scan.findings(text, path, raw=raw, source=source))
                candidates = [f for f in candidates if f['rule'] == old['rule'] and abs(f['line'] - old['line']) <= 8]
                if not candidates:
                    raise ValueError('locator-unresolved')
                nearest = min(abs(f['line'] - old['line']) for f in candidates)
                for target in candidates:
                    if abs(target['line'] - old['line']) != nearest:
                        continue
                    key = secret_scan.object_hash(target)
                    context_id = secret_scan.object_hash({k: target[k] for k in ('path', 'context_sha256', 'immutable_source_revision')})
                    if context_id not in contexts:
                        contexts[context_id] = context_evidence(text, target)
                    item = entries.setdefault(key, {'target': target, 'classification': 'unresolved',
                        'rationale': 'Safe structural evidence is supplied for independent analysis. Historical author proposals are not approvals; generation and consumer provenance, operator reuse and remediation remain unresolved until conclusive independent review.',
                        'evidence_sha256': secret_scan.object_hash(contexts[context_id]),
                        'author_reference': 'FILES-SOURCE-COMPLETION-01-author',
                        'status': 'pending-independent-review', 'context_evidence_id': context_id,
                        'rotation_remediation_gate': 'unresolved-private-review-and-operator-assessment-open', 'origins': []})
                    origin = {'evidence': 'docs/phases/AUDIT-FILES-BUNDLE-01.evidence.json', 'scope': scope, 'archived_line': old['line']}
                    if origin not in item['origins']:
                        item['origins'].append(origin)
            except (ValueError, OSError, KeyError, zipfile.BadZipFile):
                missing.append({'path': secret_scan.safe_path(path), 'rule': old['rule'], 'line': old['line'], 'scope': scope, 'status': 'unresolved-locator'})
    for item in entries.values():
        proposal = {k: item[k] for k in ('target', 'classification', 'rationale', 'evidence_sha256', 'author_reference')}
        item['proposal_sha256'] = secret_scan.object_hash(proposal)
    ledger = {'schema_version': secret_scan.IDENTITY_VERSION, 'task': 'FILES-SOURCE-COMPLETION-01',
        'status': 'pending-independent-review', 'is_exception_file': False, 'baseline': BASE,
        'previous_checkpoint': '97a1500e937de43ca3e83d0c1e49e034c22f6af7',
        'previous_review': {'receipt_sha256': '02183cf634aff093beb33a8952db99b071ad02aa5f2ccd23967be2b2963bd523',
            'status': 'rejected', 'approved': 0, 'blocked_proposals': 159},
        'archived_repository_findings': 156, 'archived_changed_findings': 6,
        'entries': list(entries.values()), 'unresolved_locators': missing}
    review = {'schema_version': 'safe-source-review-v2', 'pending_ledger_sha256': secret_scan.object_hash(ledger),
        'contexts': contexts, 'proposals': [{'proposal_sha256': x['proposal_sha256'], 'target': x['target'],
            'context_evidence_id': x['context_evidence_id'], 'evidence_sha256': x['evidence_sha256']} for x in entries.values()],
        'limitations': ['No approval or rotation assertion', 'Hashed symbols permit exact reference correlation without source-value disclosure',
            'Structural excerpts do not prove external consumers or runtime non-reuse; unresolved cases must not be waived']}
    workflow_path = root / '.github/workflows/backend-foundation.yml'
    if workflow_path.is_file():
        workflow = secret_scan.read_file(workflow_path, root)
        review['ci_wiring'] = {'workflow': '.github/workflows/backend-foundation.yml',
            'immutable_source_revision': secret_scan.source_revision(workflow), 'execution': 'DEFERRED',
            'required_jobs': [
                {'job': 'persistence-and-security', 'command': 'python3 scripts/secret_scan.py --mode repository'},
                {'job': 'frontend-and-browser', 'command': 'python3 scripts/secret_scan.py --mode client --bundle frontend/dist',
                 'input': 'actual npm production build output'}],
            'static_wiring_check': 'SafeSourceEvidenceTests.test_required_ci_jobs_scan_repository_and_actual_postbuild_client_directly'}
    if len(json.dumps(review)) > 12 * 1024 * 1024:
        raise ValueError('evidence-budget')
    return ledger, review


def main():
    root = Path(__file__).resolve().parents[1]
    ledger, review = inventory(root)
    for name, result in [('files-source-classifications.pending.json', ledger), ('files-source-review-evidence.pending.json', review)]:
        destination = root / 'docs/security' / name
        destination.write_text(json.dumps(result, indent=2, ensure_ascii=True) + '\n', encoding='utf-8')
    print(json.dumps({'unique_current_occurrences': len(ledger['entries']), 'shared_contexts': len(review['contexts']),
        'unresolved_locators': len(ledger['unresolved_locators']), 'approved': 0}))


if __name__ == '__main__':
    try:
        main()
    except Exception:
        # Exceptions may contain hostile filenames or parser input. No raw traceback.
        print(json.dumps({'status': 'required-input-or-evidence-authoring-failure'}))
        raise SystemExit(2)
