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


def safe_review_json(value):
    """HTML-safe quote encoding cannot turn safe multiline excerpts into findings.

    Validate decoded content first, then serialized bytes. Encoding never grants
    an exception: actual credentials in decoded strings are rejected outright.
    The decoded object and its canonical proposal/evidence hashes stay identical.
    """
    def validate(node):
        if isinstance(node, str) and any(p.search(node) for p in secret_scan.RULES.values()):
            raise ValueError('unsafe-decoded-review-content')
        if isinstance(node, dict):
            for key, child in node.items():
                validate(key); validate(child)
        if isinstance(node, list):
            for child in node:
                validate(child)
    validate(value)
    encoded = json.dumps(value, ensure_ascii=False, separators=(',', ':'))
    # Only quotes inside string content change; JSON delimiters stay intact.
    encoded = encoded.replace('\\"', '\\u0022').replace("'", '\\u0027')
    if json.loads(encoded) != value:
        raise ValueError('review-serialization-roundtrip')
    if any(p.search(encoded) for p in secret_scan.RULES.values()):
        raise ValueError('unsafe-serialized-review-content')
    return encoded


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


def exact_call_evidence(text, target):
    """Bind a withheld span to the actual call argument and its local definitions."""
    matches = [m for m in secret_scan.RULES[target['rule']].finditer(text)
               if m.start() == target['occurrence']
               and secret_scan.digest(m.group().encode()) == target['fingerprint']]
    if len(matches) != 1:
        raise ValueError('exact-call-match-unresolved')
    tree = ast.parse(text)
    nodes = list(ast.walk(tree))
    parents = {child: parent for parent in nodes for child in ast.iter_child_nodes(parent)}
    lines = text.splitlines(keepends=True)
    starts = [0]
    for line in lines:
        starts.append(starts[-1] + len(line))
    def offset(line, column):
        return starts[line - 1] + len(lines[line - 1].encode('utf-8')[:column].decode('utf-8'))
    def enclosing(node):
        while node in parents:
            node = parents[node]
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.Lambda)):
                return node
        return None
    match = matches[0]
    calls = []
    for node in nodes:
        if not isinstance(node, ast.Call):
            continue
        start, end = offset(node.lineno, node.col_offset), offset(node.end_lineno, node.end_col_offset)
        if not start <= match.start() < match.end() <= end:
            continue
        scope = enclosing(node)
        names = {n.id for arg in [*node.args, *(k.value for k in node.keywords)]
                 for n in ast.walk(arg) if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Load)}
        selected, included = [], set()
        # Resolve chains of local/module assignments; include all eligible
        # definitions rather than silently selecting one conditional branch.
        for _ in range(8):
            added = False
            for definition in nodes:
                if not isinstance(definition, (ast.Assign, ast.AnnAssign)) or definition in included:
                    continue
                targets = definition.targets if isinstance(definition, ast.Assign) else [definition.target]
                stored = {n.id for t in targets for n in ast.walk(t) if isinstance(n, ast.Name)}
                if (not names.intersection(stored) or definition.lineno > node.end_lineno
                    or enclosing(definition) not in (None, scope)):
                    continue
                included.add(definition); selected.append(definition); added = True
                if definition.value is not None:
                    names.update(n.id for n in ast.walk(definition.value)
                                 if isinstance(n, ast.Name) and isinstance(n.ctx, ast.Load))
            if not added:
                break
        if len(selected) > 32:
            raise ValueError('exact-call-binding-budget')
        budget = [10000]
        calls.append({'start': start, 'end': end, 'line': node.lineno,
                      'scope_line': scope.lineno if scope is not None else None,
                      'call_shape': ast_shape(node, budget),
                      'reference_definitions': [ast_shape(n, budget) for n in sorted(selected, key=lambda n: n.lineno)]})
    if not calls or len(calls) > 16:
        raise ValueError('exact-call-unresolved-or-budget')
    return {'target_sha256': secret_scan.object_hash(target), 'matched_start': match.start(),
            'matched_end': match.end(), 'calls': sorted(calls, key=lambda c: c['end'] - c['start']),
            'semantics': 'exact call argument and scope-bound reference shapes; constants withheld; not approval'}


def build_semantic_package(root, pending, supplement_targets=()):
    root = Path(root).resolve()
    entries, bodies, contexts, presentations, identity_keys = [], {}, {}, {}, set()
    supplement_targets = set(supplement_targets)
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
            contexts[target['path']] = actual, body_hash, text
        actual, body_hash, text = contexts[target['path']]
        if target not in actual:
            raise ValueError('stale-source-binding')
        evidence = {'target': target, 'semantic_body_sha256': body_hash,
                    'provenance': entry['evidence']['provenance']}
        if identity in supplement_targets:
            evidence['exact_call_argument_evidence'] = exact_call_evidence(text, target)
        proposal = {**proposal, 'evidence_sha256': secret_scan.object_hash(evidence)}
        entries.append({'proposal': proposal, 'proposal_sha256': secret_scan.object_hash(proposal),
                        'evidence': evidence})
    if not entries:
        raise ValueError('empty-source-package')
    if not supplement_targets <= identity_keys:
        raise ValueError('unknown-supplemental-target')
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
    parser.add_argument('--supplement-target', action='append', default=[])
    args = parser.parse_args()
    package = build_semantic_package(args.root, json.loads(args.input.read_text(encoding='utf-8')), args.supplement_target)
    args.output.write_text(safe_review_json(package) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps({'entries': len(package['entries']), 'semantic_bodies': len(package['semantic_bodies']),
                      'approved': 0, 'exceptions': 0}))


if __name__ == '__main__':
    try:
        main()
    except Exception:
        print(json.dumps({'status': 'semantic-source-precondition-failed', 'exceptions': 0}))
        raise SystemExit(2) from None
