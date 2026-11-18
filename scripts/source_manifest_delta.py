"""Compare two immutable source manifests and write a deterministic path/hash delta."""

import argparse
import json
from pathlib import Path


def compare(previous, current):
    old = {row['source_path']: row['sha256'] for row in previous['files']}
    new = {row['source_path']: row['sha256'] for row in current['files']}
    if len(old) != len(previous['files']) or len(new) != len(current['files']):
        raise ValueError('Duplicate source paths in manifest')
    shared = set(old) & set(new)
    return {
        'baseline_id': current['baseline_id'],
        'previous_baseline_id': previous['baseline_id'],
        'previous_files': len(old),
        'current_files': len(new),
        'added': sorted(set(new) - set(old)),
        'removed': sorted(set(old) - set(new)),
        'changed': sorted(path for path in shared if old[path] != new[path]),
        'unchanged': sum(old[path] == new[path] for path in shared),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--previous', type=Path, required=True)
    parser.add_argument('--current', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    previous = json.loads(args.previous.read_text(encoding='utf-8'))
    current = json.loads(args.current.read_text(encoding='utf-8'))
    result = compare(previous, current)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: len(value) if isinstance(value, list) else value for key, value in result.items()}, ensure_ascii=False))


if __name__ == '__main__':
    main()
