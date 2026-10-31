"""Full unittest discovery with a machine-readable result, including skipped tests."""
import argparse
import json
from pathlib import Path
import unittest


def summary(result):
    counts = dict(total=result.testsRun, failed=len(result.failures), errors=len(result.errors),
                  skipped=len(result.skipped), expectedFailures=len(result.expectedFailures),
                  unexpectedSuccesses=len(result.unexpectedSuccesses))
    counts['passed'] = max(0, counts['total'] - sum(v for k, v in counts.items() if k != 'total'))
    return counts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    suite = unittest.defaultTestLoader.discover('scripts', pattern='test_*.py')
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    counts = summary(result)
    args.report.write_text(json.dumps(counts), encoding='utf-8')
    return 0 if counts['passed'] > 0 and counts['passed'] == counts['total'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
