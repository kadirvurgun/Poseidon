"""Source-bound review evidence with readable semantics and withheld matches.

Authoring only: creates no exceptions, trust pins or independent approval. The
older structural renderer remains unchanged. Reviewers see redacted source
context and enclosing Python scopes rather than anonymous symbol hashes alone.
"""
import argparse
import ast
import html
import json
import re
from pathlib import Path

import secret_scan
from classify_file_findings import source_context
from source_review_evidence import ast_shape

OMITTED = '[WITHHELD]'
EXTRA = (
    re.compile(r'-----BEGIN [A-Z ]*PRIVATE KEY-----.*?-----END [A-Z ]*PRIVATE KEY-----', re.S),
    re.compile(r'\b[A-Za-z0-9.!#$%&*+/=?^_`{|}~-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b'),
    re.compile(r'\b(?:https?|postgres(?:ql)?|redis)://[^\s/@]+:[^\s/@]+@[^\s]+', re.I),
    re.compile(r'''(?ix)\b(?:username|user_name|full_name|email|phone|client_secret|api_key|access_token|password|pwd)\s*["']?\s*[:=]\s*["'][^"'\r\n]*["']'''),
)


def intervals(text):
    """Find full spans before cropping, so a window cannot expose a token suffix."""
    spans = sorted((m.start(), m.end()) for pattern in (*secret_scan.RULES.values(), *EXTRA)
                   for m in pattern.finditer(text))
    merged = []
    for start, end in spans:
        if merged and start <= merged[-1][1]:
            merged[-1] = (merged[-1][0], max(end, merged[-1][1]))
        else:
            merged.append((start, end))
    return merged


def redacted_window(text, start, end, spans=None):
    if not 0 <= start <= end <= len(text):
        raise ValueError('invalid-source-window')
    spans = intervals(text) if spans is None else spans
    chunks, cursor = [], start
    for left, right in spans:
        if right <= start or left >= end:
            continue
        left, right = max(left, start), min(right, end)
        chunks.append(text[cursor:left])
        chunks.append(OMITTED + '\n' * text[left:right].count('\n'))
        cursor = right
    chunks.append(text[cursor:end])
    result = ''.join(chunks)
    # Decode escaped document text only after original-span masking, then mask
    # newly visible encoded credential/PII spans before returning any evidence.
    decoded = html.unescape(result)
    if decoded != result:
        for pattern in (*secret_scan.RULES.values(), *EXTRA):
            decoded = pattern.sub(OMITTED, decoded)
    if any(pattern.search(decoded) for pattern in secret_scan.RULES.values()):
        raise ValueError('unsafe-semantic-rendering')
    return decoded


def semantic_context(text):
    found = list(secret_scan.findings(text, 'presentation'))
    if not found or len(found) > 256:
        raise ValueError('semantic-case-budget')
    ranges = [(max(0, f['occurrence'] - 1100), min(len(text), f['occurrence'] + 1400)) for f in found]
    scopes, values = [], []
    try:
        tree = ast.parse(text) if len(text) <= 1024 * 1024 else None
    except (SyntaxError, ValueError, RecursionError):
        tree = None
    if tree is not None:
        starts = [0]
        starts.extend(m.end() for m in re.finditer('\n', text))
        for node in ast.walk(tree):
            if isinstance(node, (ast.Assign, ast.AnnAssign)) and any(
                    node.lineno <= f['line'] <= node.end_lineno for f in found):
                if node.value is not None:
                    values.append({'line': node.lineno, 'end_line': node.end_lineno,
                                   'value_shape': ast_shape(node.value, [1500])})
            if not isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef)):
                continue
            if not any(node.lineno <= f['line'] <= node.end_lineno for f in found):
                continue
            start = starts[node.lineno - 1]
            end = starts[node.end_lineno] if node.end_lineno < len(starts) else len(text)
            complete = end - start <= 16000
            scopes.append({'line': node.lineno, 'end_line': node.end_lineno,
                           'kind': type(node).__name__, 'complete_source_included': complete})
            if complete:
                ranges.append((start, end))
    merged = []
    for start, end in sorted(ranges):
        if merged and start <= merged[-1][1]:
            merged[-1] = (merged[-1][0], max(end, merged[-1][1]))
        else:
            merged.append((start, end))
    if sum(end - start for start, end in merged) > 120000:
        raise ValueError('semantic-context-budget')
    masks = intervals(text)
    return {'presentation_sha256': secret_scan.digest(text.encode('utf-8')),
            'display': 'redacted-source-semantics; matched spans and recognized PII withheld; not approval',
            'occurrences': [{k: f[k] for k in ('rule', 'line', 'occurrence', 'fingerprint')} for f in found],
            'python_scopes': scopes,
            'python_assignment_values': values,
            'windows': [{'start': start, 'end': end, 'line': text.count('\n', 0, start) + 1,
                         'text': redacted_window(text, start, end, masks)} for start, end in merged]}


def build_semantic_package(root, pending):
    root = Path(root).resolve()
    entries, bodies, contexts, presentations, identity_keys = [], {}, {}, {}, set()
    for entry in pending['entries']:
        proposal = entry['proposal']
        target = proposal['target']
        if (set(proposal) != {'target', 'classification', 'rationale', 'evidence_sha256', 'author_reference'}
            or proposal['classification'] not in {'public-example', 'synthetic-fixture', 'formal-reference'}
            or not isinstance(proposal['rationale'], str) or len(proposal['rationale']) < 40
            or not isinstance(proposal['author_reference'], str) or not proposal['author_reference']
            or not isinstance(entry['evidence'].get('provenance'), list) or not entry['evidence']['provenance']
            or entry['evidence']['target'] != target
            or secret_scan.object_hash(entry['evidence']) != proposal['evidence_sha256']
            or secret_scan.object_hash(proposal) != entry['proposal_sha256']):
            raise ValueError('invalid-author-proposal')
        identity = secret_scan.object_hash(target)
        if identity in identity_keys:
            raise ValueError('duplicate-source-identity')
        identity_keys.add(identity)
        if target['path'] not in contexts:
            text, raw, source = source_context(root, target['path'])
            actual = list(secret_scan.findings(text, target['path'], raw=raw, source=source))
            if target not in actual:
                raise ValueError('stale-source-binding')
            presentation_hash = secret_scan.digest(text.encode('utf-8'))
            if presentation_hash not in presentations:
                shape = semantic_context(text)
                body_hash = secret_scan.object_hash(shape)
                bodies[body_hash] = shape
                presentations[presentation_hash] = body_hash
            body_hash = presentations[presentation_hash]
            contexts[target['path']] = actual, body_hash
        actual, body_hash = contexts[target['path']]
        if target not in actual:
            raise ValueError('stale-source-binding')
        evidence = {'target': target, 'semantic_body_sha256': body_hash,
                    'provenance': entry['evidence']['provenance']}
        proposal = {**proposal, 'evidence_sha256': secret_scan.object_hash(evidence)}
        entries.append({'proposal': proposal, 'proposal_sha256': secret_scan.object_hash(proposal),
                        'evidence': evidence})
    if not entries:
        raise ValueError('empty-source-package')
    result = {'schema_version': 'source-semantic-review-v1', 'status': 'pending-independent-review',
              'is_exception_file': False, 'entries': entries, 'semantic_bodies': bodies,
              'receipts': [], 'exceptions': []}
    # Check string values individually: JSON escaping must not conceal a match.
    def validate(value):
        if isinstance(value, str) and any(p.search(value) for p in secret_scan.RULES.values()):
            raise ValueError('unsafe-semantic-package')
        if isinstance(value, dict):
            for key, child in value.items():
                validate(key); validate(child)
        if isinstance(value, list):
            for child in value:
                validate(child)
    validate(result)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    package = build_semantic_package(args.root, json.loads(args.input.read_text(encoding='utf-8')))
    args.output.write_text(json.dumps(package, ensure_ascii=False, separators=(',', ':')) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps({'entries': len(package['entries']), 'semantic_bodies': len(package['semantic_bodies']),
                      'approved': 0, 'exceptions': 0}))


if __name__ == '__main__':
    try:
        main()
    except Exception:
        print(json.dumps({'status': 'semantic-source-precondition-failed', 'exceptions': 0}))
        raise SystemExit(2) from None
