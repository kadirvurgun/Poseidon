"""Bounded value-free syntax evidence. Never emits source strings, comments or literals.

Symbol hashes correlate references; only a fixed vocabulary of language/runtime names
is rendered. Syntax is evidence of shape, never proof of secret safety or approval.
"""
import ast
import keyword
import re

import secret_scan

KNOWN = frozenset(('secrets', 'token_urlsafe', 'uuid', 'uuid4', 'Guid', 'NewGuid',
                   'os', 'environ', 'getenv', 'get', 'str', 'bytes', 'encode', 'decode'))
STRING = '|'.join((r'"{3}.*?"{3}', r"'{3}.*?'{3}",
                   r'"(?:\\.|[^"\\])*"', r"'(?:\\.|[^'\\])*'"))
LEX = re.compile(r'(?s)(?P<string>' + STRING + r')|(?P<identifier>[^\W\d]\w*)|(?P<number>\d+(?:\.\d+)?)|(?P<operator>[=:.{}()\[\],;+*/<>!%&|^~@-])|(?P<space>\s+)|(?P<withheld>.)')


def symbol(value):
    result = {'symbol_sha256': secret_scan.digest(value.encode('utf-8'))}
    if value in KNOWN or keyword.iskeyword(value):
        result['known_name'] = value
    return result


def syntax_tokens(line):
    # Deliberately do not render a bare word, numeric literal, unmatched character,
    # comment or quoted text. Even hostile source identifiers cannot leak values.
    tokens = []
    if len(line) > 8192:
        return {'status': 'unresolved-line-budget', 'tokens': []}
    for match in LEX.finditer(line):
        kind = match.lastgroup
        if kind == 'space':
            continue
        token = {'kind': kind, 'column': match.start(), 'end_column': match.end()}
        if kind == 'identifier':
            token.update(symbol(match.group()))
        elif kind == 'operator':
            token['operator'] = match.group()
        if len(tokens) >= 256:
            return {'status': 'unresolved-token-budget', 'tokens': tokens}
        tokens.append(token)
    return {'status': 'lexical-only-requires-review', 'tokens': tokens}


def ast_shape(node, budget):
    budget[0] -= 1
    if budget[0] < 0:
        raise ValueError('ast-budget')
    result = {'kind': type(node).__name__}
    for key in ('lineno', 'col_offset', 'end_lineno', 'end_col_offset'):
        if hasattr(node, key):
            result[key] = getattr(node, key)
    for field, value in ast.iter_fields(node):
        if isinstance(value, ast.AST):
            result[field] = ast_shape(value, budget)
        elif isinstance(value, list):
            result[field] = [ast_shape(x, budget) if isinstance(x, ast.AST) else {'withheld': type(x).__name__} for x in value]
        elif isinstance(value, str):
            # Constant values are always withheld, including values equal to KNOWN.
            result[field] = {'literal_type': 'str'} if isinstance(node, ast.Constant) else symbol(value)
        elif value is not None:
            result[field] = {'literal_type': type(value).__name__}
    return result


def context_evidence(text, target):
    found = list(secret_scan.findings(text, target['path']))
    lines = sorted({f['line'] for f in found})
    split = text.split('\n')
    result = {'source': {k: target[k] for k in ('path', 'context_sha256', 'immutable_source_revision', 'identity_version')},
              'presentation': 'lossy-for-display-only; identity-bound-to-original-bytes',
              'occurrences': [{'rule': f['rule'], 'line': f['line'], 'occurrence': f['occurrence'], 'fingerprint': f['fingerprint']} for f in found],
              'lines': [{'line': n, **syntax_tokens(split[n - 1])} for n in lines[:256]],
              'ast_status': 'unresolved-not-parseable-python', 'generation_consumer_provenance': []}
    # A single XML line can contain many assignments. Each occurrence gets a
    # bounded structural window, even when rendering its entire line is refused.
    result['match_syntax'] = []
    for finding in found:
        start = max(0, finding['occurrence'] - 192)
        end = min(len(text), finding['occurrence'] + 512)
        result['match_syntax'].append({'rule': finding['rule'], 'occurrence': finding['occurrence'],
            'window_start': start, 'window_end': end, 'column_origin': 'window-relative',
            **syntax_tokens(text[start:end])})
    if len(lines) > 256 or len(text) > 1024 * 1024:
        result['ast_status'] = 'unresolved-context-budget'
        return result
    try:
        tree = ast.parse(text)
    except (SyntaxError, ValueError, RecursionError):
        return result
    nodes = list(ast.walk(tree))
    if len(nodes) > 20000:
        result['ast_status'] = 'unresolved-node-budget'
        return result
    selected = [n for n in nodes if isinstance(n, (ast.Assign, ast.AnnAssign, ast.Call, ast.Import, ast.ImportFrom))
                and (isinstance(n, (ast.Import, ast.ImportFrom)) or any(n.lineno <= line <= n.end_lineno for line in lines))]
    names = {n.id for statement in selected for n in ast.walk(statement) if isinstance(n, ast.Name)}
    consumers = [n for n in nodes if isinstance(n, ast.Name) and n.id in names]
    # Include the actual consuming expression, not only a Name at its line. A
    # reference passed into a call must expose the safe Call/argument shape.
    consumer_shapes = [n for n in nodes if isinstance(n, (ast.Call, ast.Assign, ast.AnnAssign, ast.Return))
                       and any(isinstance(x, ast.Name) and isinstance(x.ctx, ast.Load) and x.id in names
                               for x in ast.walk(n))]
    selected_ids = {id(n) for n in selected}
    selected.extend(n for n in consumer_shapes if id(n) not in selected_ids)
    if len(selected) > 128 or len(consumers) > 1024:
        result['ast_status'] = 'unresolved-provenance-budget'
        return result
    try:
        budget = [8000]
        result['generation_consumer_provenance'] = [ast_shape(n, budget) for n in selected]
        result['symbol_occurrences'] = [ast_shape(n, budget) for n in consumers]
        result['ast_status'] = 'python-ast-structural-only-external-provenance-unresolved'
    except (ValueError, RecursionError):
        result['generation_consumer_provenance'] = []
        result.pop('symbol_occurrences', None)
        result['ast_status'] = 'unresolved-ast-budget'
    return result
