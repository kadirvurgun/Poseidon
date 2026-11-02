"""Lossless, value-free author proposals for independently reviewed scan findings.

This produces no exceptions, trust pins or reviewer receipts. Every current
identity must have an explicit, source-bound triage decision; missing or stale
source fails closed. Shared syntax bodies reduce review input without dropping
archive locations, occurrences or source bindings.
"""
from pathlib import Path

import secret_scan
from classify_file_findings import source_context
from source_review_evidence import context_evidence


CLASSIFICATIONS = frozenset(('formal-reference', 'synthetic-fixture', 'public-example', 'unresolved'))


def build_package(root, targets, decisions, author_reference):
    root = Path(root).resolve()
    if not isinstance(author_reference, str) or not author_reference:
        raise ValueError('author-required')
    identities = {secret_scan.object_hash(t): t for t in targets}
    if not identities or len(identities) != len(targets) or set(identities) != set(decisions):
        raise ValueError('incomplete-or-duplicate-triage')
    contexts, cache, bodies, entries = {}, {}, {}, []
    for identity, target in sorted(identities.items()):
        decision = decisions[identity]
        if (set(decision) != {'classification', 'rationale', 'provenance'}
                or decision['classification'] not in CLASSIFICATIONS
                or not isinstance(decision['rationale'], str) or len(decision['rationale']) < 40
                or not isinstance(decision['provenance'], list) or not decision['provenance']):
            raise ValueError('missing-source-triage')
        if target['path'] not in contexts:
            contexts[target['path']] = source_context(root, target['path'])
        text, raw, source = contexts[target['path']]
        if target not in secret_scan.findings(text, target['path'], raw=raw, source=source):
            raise ValueError('stale-source-binding')
        presentation_key = secret_scan.digest(text.encode('utf-8'))
        if presentation_key not in cache:
            shape = context_evidence(text, target)
            shape.pop('source')
            body_hash = secret_scan.object_hash(shape)
            bodies[body_hash] = shape
            cache[presentation_key] = body_hash
        evidence = {'target': target, 'syntax_body_sha256': cache[presentation_key],
                    'provenance': decision['provenance']}
        proposal = {'target': target, 'classification': decision['classification'],
                    'rationale': decision['rationale'], 'evidence_sha256': secret_scan.object_hash(evidence),
                    'author_reference': author_reference}
        entries.append({'proposal': proposal, 'proposal_sha256': secret_scan.object_hash(proposal),
                        'evidence': evidence})
    package = {'schema_version': 'source-review-package-v1', 'status': 'pending-independent-review',
               'is_exception_file': False, 'entries': entries, 'syntax_bodies': bodies,
               'receipts': [], 'exceptions': []}
    # Includes authored prose as well as syntax. A malformed manifest must not
    # turn an otherwise value-free review package into another disclosure.
    def check(node):
        if isinstance(node, str):
            if any(pattern.search(node) for pattern in secret_scan.RULES.values()):
                raise ValueError('unsafe-review-content')
        elif isinstance(node, dict):
            for key, value in node.items():
                check(key)
                check(value)
        elif isinstance(node, list):
            for value in node:
                check(value)
    check(package)
    return package
