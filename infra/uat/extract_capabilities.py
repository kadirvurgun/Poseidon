"""Extract the API's closed permission allow-list and navigation key list from source.

Run from the repository root at deploy time so the UAT membership profiles never drift from
the deployed build:  py infra/uat/extract_capabilities.py > capabilities.json
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def between(text: str, start: str, end: str) -> str:
    i = text.index(start)
    return text[i:text.index(end, i)]


def main() -> int:
    identity = (ROOT / 'backend/Poseidon.Api/FoundationIdentity.cs').read_text(encoding='utf-8')
    lens = (ROOT / 'backend/Poseidon.Api/WorkspaceLensEndpoints.cs').read_text(encoding='utf-8')
    permissions = re.findall(r'"([a-z0-9_.]+)"', between(identity, 'KnownPermissions =', '];'))
    keys = re.findall(r'"([a-z0-9-]+)"', between(lens, 'string[] known = [', '];'))
    if len(permissions) < 50 or len(set(permissions)) != len(permissions) or len(keys) < 20:
        print('capability_extraction_failed', file=sys.stderr)
        return 1
    json.dump({'permissions': permissions, 'navigationKeys': keys}, sys.stdout, indent=1)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
