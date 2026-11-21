"""Report frontend i18n values that were copied verbatim from en.json (untranslated).

For every locale catalog under frontend/src/i18n/messages, count leaf string values that are
byte-identical to en.json. Values that are legitimately language-neutral are skipped:
placeholder/number/punctuation-only values, established all-caps acronyms (SOF, ISM, GO/NO-GO,
DWT ...), short unit tokens (d, h, min, m, kn ...) and explicitly allow-listed key/value pairs.
English variants (en-GB) are excluded because identical text is expected there.

Usage: py scripts/i18n_untranslated.py [--locale de] [--prefix copies.SafetyEvents_tsx_copy]
                                       [--keys] [--max N]
Exit code 1 when --max is given and any reported locale exceeds N untranslated values.
No network or model calls.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MESSAGES = ROOT / 'frontend' / 'src' / 'i18n' / 'messages'
SOURCE_LOCALE = 'en'
EXCLUDED_LOCALES = frozenset({'en', 'en-GB'})

PLACEHOLDER = re.compile(r'\{[^{}]*\}|%[sdif]|<[^<>]+>')
WORD = re.compile(r'[^\W\d_]+', re.UNICODE)
UNIT_TOKENS = frozenset({'d', 'h', 'min', 'm', 's', 'ms', 'kn', 'kt', 'nm', 'km', 'kg', 't', 'mt', 'kW', 'kWh',
                         'l', 'L', 'mm', 'cm', 'x', 'bar', 'rpm', 'hPa', 'cbm',
                         'LoH', 'eNOAD', 'ePTW'})  # mixed-case maritime abbreviations kept verbatim
# Exact values that are product/brand names or standards kept verbatim in every language.
ALLOWED_VALUES = frozenset({'POSEIDON', 'OCIMF', 'SIRE 2.0', 'TMSA', 'OVID', 'Wi-Fi', 'PWA', 'OIDC', 'PostgreSQL'})


def flatten(node: object, prefix: str = '') -> dict[str, object]:
    out: dict[str, object] = {}
    if isinstance(node, dict):
        for key, value in node.items():
            out.update(flatten(value, f'{prefix}.{key}' if prefix else key))
    else:
        out[prefix] = node
    return out


def is_language_neutral(value: str) -> bool:
    if value in ALLOWED_VALUES:
        return True
    words = WORD.findall(PLACEHOLDER.sub(' ', value))
    if not words:
        return True  # placeholders, numbers, punctuation only
    return all((len(w) >= 2 and w.isupper()) or w in UNIT_TOKENS for w in words)


def untranslated(source: dict[str, object], target: dict[str, object], prefix: str = '') -> list[str]:
    keys = []
    for key, value in source.items():
        if prefix and not key.startswith(prefix):
            continue
        if isinstance(value, str) and target.get(key) == value and not is_language_neutral(value):
            keys.append(key)
    return keys


def load(path: Path) -> dict[str, object]:
    return flatten(json.loads(path.read_text(encoding='utf-8')))


def report(messages: Path = MESSAGES, locales: list[str] | None = None, prefix: str = '',
           include_keys: bool = False) -> dict[str, object]:
    source = load(messages / f'{SOURCE_LOCALE}.json')
    available = sorted(p.stem for p in messages.glob('*.json') if p.stem not in EXCLUDED_LOCALES)
    selected = [l for l in (locales or available) if l in available]
    result: dict[str, object] = {'source': SOURCE_LOCALE, 'prefix': prefix, 'sourceKeys': len(source), 'locales': {}}
    for locale in selected:
        target = load(messages / f'{locale}.json')
        keys = untranslated(source, target, prefix)
        entry: dict[str, object] = {'untranslated': len(keys),
                                    'missingKeys': len([k for k in source if k not in target]),
                                    'extraKeys': len([k for k in target if k not in source])}
        if include_keys:
            entry['keys'] = keys
        result['locales'][locale] = entry
    return result


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--messages', type=Path, default=MESSAGES)
    parser.add_argument('--locale', action='append', dest='locales')
    parser.add_argument('--prefix', default='')
    parser.add_argument('--keys', action='store_true', help='include untranslated key names')
    parser.add_argument('--max', type=int, default=None, help='fail when a locale exceeds this count')
    args = parser.parse_args(argv)
    data = report(args.messages, args.locales, args.prefix, args.keys)
    json.dump(data, sys.stdout, ensure_ascii=False, indent=2)
    sys.stdout.write('\n')
    if args.max is not None and any(v['untranslated'] > args.max for v in data['locales'].values()):
        return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
