#!/usr/bin/env python3
"""Offline preparation only. Never starts services, calls GitHub or prints secrets."""
import argparse, json, os, re, sys
from urllib.parse import urlsplit

EXPECTED_REPOSITORY = 'emgt41/Poseidon'
EXPECTED_OWNER = 'emgt41'
PORT = 8080

def assess(env, origin, visibility, creator, free_only):
    errors = []
    name = env.get('CODESPACE_NAME', '')
    if env.get('CODESPACES') != 'true': errors.append('NOT_A_CODESPACE')
    if env.get('GITHUB_REPOSITORY') != EXPECTED_REPOSITORY: errors.append('WRONG_REPOSITORY')
    if not re.fullmatch(r'[a-z0-9]+(?:-[a-z0-9]+)*', name): errors.append('INVALID_CODESPACE_NAME')
    if env.get('GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN') != 'app.github.dev': errors.append('UNREVIEWED_FORWARDING_DOMAIN')
    expected = f'https://{name}-{PORT}.app.github.dev'
    # Exact comparison excludes credentials, alternate ports, paths, query and fragments.
    if origin != expected or urlsplit(origin).scheme != 'https': errors.append('WRONG_EXTERNAL_ORIGIN')
    if visibility != 'private': errors.append('VISIBILITY_NOT_CREATOR_PRIVATE')
    if creator != EXPECTED_OWNER: errors.append('WRONG_CREATOR')
    if not free_only: errors.append('FREE_ONLY_NOT_CONFIRMED')
    return {
        'status': 'REJECTED' if errors else 'CONFIGURATION_VALID_UNVERIFIED',
        'configurationErrors': errors,
        'architectureBlockers': [],
        'creatorStatements': 'UNVERIFIED_INPUT_NOT_GITHUB_ACCESS_OR_QUOTA_PROOF',
        'githubVisibility': 'NOT_QUERIED',
        'remainingEntitlement': 'NOT_QUERIED',
        'servicesStarted': False,
        'portsForwarded': False,
        'uatReady': False
    }

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--origin', required=True)
    p.add_argument('--creator', required=True)
    p.add_argument('--visibility', required=True)
    p.add_argument('--free-only-confirmed', action='store_true')
    args = p.parse_args()
    print(json.dumps(assess(os.environ, args.origin, args.visibility, args.creator, args.free_only_confirmed), indent=2))
    return 3 # Offline input validation alone never starts the runtime; use uat.py.
if __name__ == '__main__': sys.exit(main())
