"""Mandatory scan with optional externally pinned, independently reviewed source findings.

Never derive trust from repository documents or print inputs, pins or matched data.
No reviewed exception/pin is created here. Missing or invalid trust fails closed.
"""
import argparse
import json
import os
import re
from pathlib import Path

import secret_scan

TRUST_VARIABLE = 'POSEIDON_SOURCE_REVIEW_SHA256'
EXCEPTION_DOCUMENT = 'docs/security/files-source-exceptions.json'


def review_pins(environment):
    raw = environment.get(TRUST_VARIABLE)
    if raw is None or raw == '':
        return []
    if not isinstance(raw, str) or len(raw) > 70000:
        raise ValueError('review-trust')
    values = json.loads(raw)
    if not isinstance(values, list) or not values or len(values) > 1024 or any(
            not isinstance(value, str) or not re.fullmatch('[0-9a-f]{64}', value) for value in values):
        raise ValueError('review-trust')
    if len(set(values)) != len(values):
        raise ValueError('review-trust')
    return values


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--mode', required=True, choices=['repository', 'client'])
    parser.add_argument('--bundle', default='frontend/dist')
    args = parser.parse_args(argv)
    try:
        root = args.root.resolve()
        pins = review_pins(os.environ)
        document = root / EXCEPTION_DOCUMENT
        exists = document.exists() or document.is_symlink()
        # An exception file cannot install its own receipt hashes, and configured
        # trust must not silently disappear when its reviewed document is missing.
        if bool(pins) != exists:
            raise ValueError('review-trust-document-binding')
        arguments = ['--root', str(root), '--mode', args.mode, '--bundle', args.bundle]
        if exists:
            arguments.extend(['--exceptions', str(document)])
            for pin in pins:
                arguments.extend(['--review-sha256', pin])
        # Always perform the full real scan (or fail nonzero on invalid trust).
        # Same source-bound validation and input/archive/client coverage remain.
        return secret_scan.main(arguments)
    except Exception:
        print(json.dumps({'rule': 'required-review-trust-or-scan-input-failure'}))
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
