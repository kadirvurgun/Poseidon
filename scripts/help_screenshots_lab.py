"""USER-GUIDE-01 UG-2: documentation screenshot actions on the DEV-OIDC-LAB-01 local lab.

Explicit, local, synthetic only; never production bootstrap. Kept separate from oidc_lab.py so
its independently reviewed source stays unchanged. Only safe stage results reach stdout.

  prepare_screenshots  read-only grants for lab-manager in the private (gitignored) lab config,
                       then the Development-only FICTIONAL demo fixtures (DbMigrator --demo-fixtures)
  screenshots          run tests/Poseidon.Browser.Tests/screenshots/capture.ts
                       (POSEIDON_SCREENSHOTS_MODE=compare|update|discover)
"""
import argparse
from copy import deepcopy
import json
import sys

import oidc_lab as lab

# Read-only capabilities only; no write, manage, safety, verifier, signer, owner or
# source-validation permission is ever added for screenshots.
SCREENSHOT_READ_PERMISSIONS = ('regulatory.draft.read', 'maintenance.read', 'mode.tug', 'mode.cross', 'tug.read', 'membership.read')


def screenshot_membership(config, realm):
    """Pure: returns a copy with lab-manager's read grants added; every other member is preserved."""
    candidate = deepcopy(config)
    subjects = [u['id'] for u in realm['users'] if u['username'] == 'lab-manager']
    if realm.get('realm') != 'poseidon-lab' or len(subjects) != 1:
        raise RuntimeError('lab_screenshot_identity_invalid')
    members = [m for m in candidate['Authentication']['Oidc']['Memberships']
               if m['Issuer'] == lab.AUTHORITY and m['Subject'] == subjects[0]]
    if len(members) != 1 or members[0].get('TenantWideRead') is not True:
        raise RuntimeError('lab_screenshot_scope_invalid')
    for permission in SCREENSHOT_READ_PERMISSIONS:
        if permission not in members[0]['Permissions']:
            members[0]['Permissions'].append(permission)
    return candidate


def prepare_screenshots():
    lab.stage('explicit_lab_screenshot_configuration')
    lab.init()
    lab.owned_process('stop')  # The app re-reads memberships on the next start.
    path = lab.PRIVATE / 'appsettings.json'
    realm = json.loads((lab.PRIVATE / 'realm/poseidon-lab-realm.json').read_text())
    lab.write(path, json.dumps(screenshot_membership(json.loads(path.read_text()), realm)))
    lab.stage('demo_fixture_import')
    env = lab.environment()
    env['POSEIDON_DATABASE'] = json.loads(path.read_text())['ConnectionStrings']['Poseidon']
    lab.run([lab.root_tool('dotnet'), 'run', '--project', 'backend/Poseidon.DbMigrator', '-c', 'Release', '--no-build',
             '--', '--demo-fixtures', 'backend/seed/synthetic-demo.json'], env=env)


def screenshots():
    lab.stage('real_provider_screenshots')
    env = lab.environment()
    env['POSEIDON_LAB_PRIVATE'] = str(lab.PRIVATE)
    env['POSEIDON_LAB_PYTHON'] = sys.executable
    env['NODE_EXTRA_CA_CERTS'] = str(lab.PRIVATE / 'ca.pem')
    env.pop('NODE_TLS_REJECT_UNAUTHORIZED', None)
    lab.run([lab.root_tool('node'), str(lab.ROOT / 'tests/Poseidon.Browser.Tests/screenshots/capture.ts')], env=env, timeout=1800)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['prepare_screenshots', 'screenshots'])
    action = parser.parse_args().action
    try:
        if sys.version_info[:3] != (3, 13, 13):
            raise RuntimeError('pinned_python_required')
        lab.bind_selected_runtime()
        globals()[action]()
        print('USER-GUIDE-01 UG-2 ' + action + ' PASS')
    except Exception:
        # Raw exceptions are deliberately not printed (they can carry private paths or provider output).
        print('USER-GUIDE-01 UG-2 ' + action + ' FAILED at ' + lab.STAGE + '; private state preserved; no raw diagnostics retained')
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
