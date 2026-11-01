"""Targeted, redacted authoring inventory from the archived failed gate (not a scan gate).

Reads only cited inputs/members and exact nearby candidate contexts. Originals are read-only.
Proposals never suppress scanner findings or authorize publication/rotation.
"""
import hashlib
import io
import json
import re
import zipfile
from pathlib import Path
import secret_scan

BASE = '0bb2492f8eff66e92109ebad18c8c8c55b64621b'


def context_for(root, locator):
    parts = locator.split('!')
    first = parts.pop(0)
    xml_text = first.endswith('#text')
    first = first.removesuffix('#text').removesuffix('#utf16')
    raw = secret_scan.read_file(root / first, root)
    for part in parts:
        xml_text = part.endswith('#text')
        with zipfile.ZipFile(io.BytesIO(raw)) as archive:
            raw = archive.read(part.removesuffix('#text'))
    if xml_text:
        from xml.etree import ElementTree
        text = ''.join(ElementTree.fromstring(raw).itertext())
    else:
        text = raw.decode('utf-16' if locator.endswith('#utf16') else 'utf-8', errors='replace')
    return text.replace('\r\n', '\n').replace('\r', '\n')


def classify(text, match, locator):
    value = match.group()
    # Exact syntactic references; these patterns do not exempt a path or literal value.
    if re.search(r'(?:\{(?:password|db_password|database_password)\}|\$\{[A-Z_]+\}|<[^<>]+>)', value, re.I):
        return 'formal-reference', 'The exact assignment is a substitution expression or explicit documentation reference rather than a resolved credential. Its entire containing member is bound by context hash; independent syntax and consumer review is still required.'
    # Prove the assignment's RHS is a test expression, not a reusable secret.
    if re.search(r'\b(?:secrets\.token_urlsafe|uuid\.uuid4|Guid\.NewGuid)\(', value):
        return 'synthetic-fixture', 'The exact match assigns a runtime-generated random value in the owned lab/test setup. Review the context-bound generation and consumer, including absence of a persistent implementation literal, before approving this proposal.'
    return 'unresolved', 'Actual context inspected; no conclusive reference or synthetic provenance established by the bounded classifier. Requires independent private source review and credential-owner rotation/remediation assessment. Immutable controlled originals must be preserved; this finding is not waived.'


def inventory(root):
    evidence = json.loads((root / 'docs/phases/AUDIT-FILES-BUNDLE-01.evidence.json').read_text(encoding='utf-8'))
    # Read all archived evidence, including command outcomes/hashes, without exposing raw logs.
    origins = [('repository_scan', evidence['repository_scan']), ('final_changed_input_scan', evidence['final_changed_input_scan'])]
    entries = {}; missing = []; cache = {}
    proposals = json.loads((root / 'docs/security/files-source-context-proposals.json').read_text(encoding='utf-8'))
    for scope, report in origins:
        for old in report['findings']:
            path = old['path']
            try:
                if path not in cache: cache[path] = context_for(root, path)
                text = cache[path]; candidates = list(secret_scan.RULES[old['rule']].finditer(text))
                # Context, not an assumed historical line: small shifts are retained as provenance.
                candidates = [m for m in candidates if abs(text.count('\n', 0, m.start()) + 1 - old['line']) <= 8]
                if not candidates: raise ValueError('locator-unresolved')
                nearest = min(abs(text.count('\n', 0, m.start()) + 1 - old['line']) for m in candidates)
                for match in candidates:
                    line = text.count('\n', 0, match.start()) + 1
                    if abs(line - old['line']) != nearest: continue
                    fingerprint = hashlib.sha256(match.group().encode()).hexdigest()
                    key = (path, old['rule'], line, fingerprint)
                    classification, rationale = classify(text, match, path)
                    # Exact manually inspected match proposal; no scanner suppression or approval.
                    # The resulting ledger binds every separate original/member/derived context.
                    if fingerprint in proposals and hashlib.sha256(text.encode()).hexdigest() in proposals[fingerprint]['inspected_context_sha256']:
                        classification = proposals[fingerprint]['classification']
                        rationale = proposals[fingerprint]['rationale']
                    item = entries.setdefault(key, {'path': path, 'rule': old['rule'], 'line': line, 'fingerprint': fingerprint,
                        'context_sha256': hashlib.sha256(text.encode()).hexdigest(), 'immutable_source_revision': BASE,
                        'classification': classification, 'rationale': rationale, 'status': 'pending-independent-review',
                        'rotation_remediation_gate': 'operator-reuse-and-rotation-assessment-open' if classification in ('unresolved', 'public-example') else 'independent-context-validation-open', 'origins': []})
                    origin = {'evidence': 'docs/phases/AUDIT-FILES-BUNDLE-01.evidence.json', 'scope': scope, 'archived_line': old['line']}
                    if origin not in item['origins']: item['origins'].append(origin)
            except (ValueError, OSError, KeyError, zipfile.BadZipFile):
                missing.append({'path': path, 'rule': old['rule'], 'line': old['line'], 'scope': scope, 'status': 'unresolved-locator'})
    return {'task': 'FILES-SOURCE-COMPLETION-01', 'status': 'pending-independent-review', 'is_exception_file': False,
        'baseline': BASE, 'archived_repository_findings': 156, 'archived_changed_findings': 6,
        'entries': list(entries.values()), 'unresolved_locators': missing}


if __name__ == '__main__':
    root = Path(__file__).resolve().parents[1]
    result = inventory(root)
    destination = root / 'docs/security/files-source-classifications.pending.json'
    destination.parent.mkdir(exist_ok=True)
    destination.write_text(json.dumps(result, indent=2, ensure_ascii=True) + '\n', encoding='utf-8')
    counts = {}
    for entry in result['entries']: counts[entry['classification']] = counts.get(entry['classification'], 0) + 1
    print(json.dumps({'unique_current_targets': len(result['entries']), 'classification_counts': counts, 'unresolved_locators': len(result['unresolved_locators']), 'approved': 0}))
