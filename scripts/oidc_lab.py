"""DEV-OIDC-LAB-01: explicit Windows local synthetic fixture; never production bootstrap.

Only safe stage/count results reach stdout. Child diagnostics are discarded because
authentication failures can contain URLs, credentials or provider response bodies.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import secrets
import socket
import ssl
import subprocess
import sys
import time
import urllib.request
import uuid
import tempfile
import shutil
from copy import deepcopy
from oidc_lab_humans import catalog_membership

ROOT = Path(__file__).resolve().parents[1]
PRIVATE = ROOT / '.runtime/oidc-lab'
AUTHORITY = 'https://localhost:8443/realms/poseidon-lab'
APP = 'https://localhost:5443'
PROJECT = 'poseidon-lab-' + hashlib.sha256(str(ROOT).encode()).hexdigest()[:12]
STAGE = 'initialization'


def root_tool(name):
    if os.name != 'nt':
        executable = shutil.which(name)
        if executable:
            return executable  # CI setup-dotnet/setup-node owns exact pinned tool versions.
    relative = {'dotnet': 'dotnet/dotnet.exe', 'node': 'node/node-v26.13.0-win-x64/node.exe'}[name]
    for directory in (ROOT, *ROOT.parents):
        path = directory / '.tools' / relative
        if path.is_file():
            return str(path)
    raise RuntimeError('pinned_root_tool_missing')


def stage(value):
    global STAGE
    STAGE = value # Internal finite labels only; never exception messages or child output.


def run(args, *, env=None, timeout=120):
    process = subprocess.Popen(args, cwd=ROOT, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        result = process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        if os.name == 'nt' and process.poll() is None:
            # Only this still-live child tree; never process-name/global termination.
            subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=15)
        if process.poll() is None:
            process.kill()
        process.wait(timeout=15)
        raise RuntimeError('child_timeout') from None
    if result:
        raise RuntimeError('child_failed')


def private_directory(path):
    path.mkdir(parents=True, exist_ok=True)
    if os.name == 'nt':
        # SID avoids localized/user display names. Protect before writing anything.
        run(['pwsh', '-NoProfile', '-File', str(ROOT / 'scripts/oidc-lab-private.ps1'),
             '-Action', 'restrict', '-Directory', str(path)])
    else:
        path.chmod(0o700)


def write(path, value):
    path.write_text(value, encoding='utf-8')
    if os.name != 'nt':
        path.chmod(0o600)


def atomic_private_json(path, value):
    """Crash-safe update inside the existing restricted private directory only."""
    path = Path(path)
    if path.is_symlink() or not path.resolve().is_relative_to(PRIVATE.resolve()):
        raise RuntimeError('lab_private_path_invalid')
    descriptor, temporary = tempfile.mkstemp(prefix='.human-update-', dir=path.parent)
    try:
        with os.fdopen(descriptor, 'w', encoding='utf-8') as stream:
            json.dump(value, stream)
            stream.flush()
            os.fsync(stream.fileno())
        if os.name != 'nt':
            os.chmod(temporary, 0o600)
        os.replace(temporary, path)
    finally:
        Path(temporary).unlink(missing_ok=True)


def make_configuration():
    """Pure synthetic input, used by tests as well as explicit provisioning."""
    manager, restricted, reviewer = str(uuid.uuid4()), str(uuid.uuid4()), str(uuid.uuid4())
    credentials = {name: {'username': name, 'password': secrets.token_urlsafe(32)}
                   for name in ('lab-manager', 'lab-restricted', 'lab-reviewer')}
    secret = secrets.token_urlsafe(48)
    realm = {
        'realm': 'poseidon-lab', 'enabled': True, 'sslRequired': 'all',
        'registrationAllowed': False, 'resetPasswordAllowed': False,
        'rememberMe': False, 'bruteForceProtected': True,
        'accessTokenLifespan': 300, 'ssoSessionIdleTimeout': 1800,
        'clients': [{'clientId': 'poseidon-local', 'enabled': True, 'protocol': 'openid-connect',
                     'publicClient': False, 'secret': secret, 'standardFlowEnabled': True,
                     'directAccessGrantsEnabled': False, 'serviceAccountsEnabled': False,
                     'redirectUris': [APP + '/signin-oidc'], 'webOrigins': [APP],
                     'attributes': {'pkce.code.challenge.method': 'S256',
                                    'post.logout.redirect.uris': APP + '/signout-callback-oidc'}}],
        'users': [{'id': subject, 'username': name, 'enabled': True,
                   'firstName': 'Synthetic', 'lastName': {'lab-manager': 'Manager', 'lab-restricted': 'Restricted', 'lab-reviewer': 'Reviewer'}[name],
                   'email': name + '@example.invalid', 'emailVerified': False,
                   'credentials': [{'type': 'password', 'value': credentials[name]['password'], 'temporary': False}]}
                  for name, subject in [('lab-manager', manager), ('lab-restricted', restricted), ('lab-reviewer', reviewer)]]}
    tenant = '10000000-0000-0000-0000-000000000001'
    memberships = [{'Issuer': AUTHORITY, 'Subject': subject, 'ActorId': str(uuid.uuid4()),
                    'TenantId': tenant, 'Permissions': ['directory.read', 'directory.write', 'workspace.search'] if wide else ['directory.read'],
                    'TenantWideManage': wide, 'TenantWideRead': wide,
                    'VesselGrants': [] if wide else [{'VesselId': '30000000-0000-0000-0000-000000000001', 'CanEdit': False}]}
                   for subject, wide in [(manager, True), (restricted, False), (reviewer, False)]]
    database_password = secrets.token_urlsafe(48)
    config = {'ConnectionStrings': {'Poseidon': f'Host=localhost;Port=55432;Database=poseidon_lab;Username=poseidon_lab;Password={database_password};Include Error Detail=false'},
              'Authentication': {'Oidc': {'Authority': AUTHORITY, 'Issuer': AUTHORITY,
                                          'ClientId': 'poseidon-local', 'ClientSecret': secret, 'Memberships': memberships}},
              'DevelopmentLab': {'OidcCaPath': str(PRIVATE / 'ca.pem'), 'WebRoot': str(ROOT / 'frontend/dist')},
              'Kestrel': {'Endpoints': {'LocalLab': {'Url': APP, 'Certificate': {'Path': str(PRIVATE / 'tls.pem'), 'KeyPath': str(PRIVATE / 'tls.key')}}}},
              'Logging': {'LogLevel': {'Default': 'None'}}}
    return realm, credentials, database_password, config


def init():
    stage('private_configuration')
    if (PRIVATE / 'ready').exists():
        return # Never rotate credentials or overwrite operator edits on restart.
    if PRIVATE.exists() and any(PRIVATE.iterdir()):
        raise RuntimeError('partial_configuration_preserved')
    private_directory(PRIVATE)
    (PRIVATE / 'realm').mkdir()
    realm, credentials, password, config = make_configuration()
    write(PRIVATE / 'realm/poseidon-lab-realm.json', json.dumps(realm))
    write(PRIVATE / 'credentials.json', json.dumps(credentials))
    write(PRIVATE / 'database-password', password)
    write(PRIVATE / 'provider.env', 'KC_BOOTSTRAP_ADMIN_USERNAME=lab-bootstrap\nKC_BOOTSTRAP_ADMIN_PASSWORD=' + secrets.token_urlsafe(48) + '\n')
    write(PRIVATE / 'appsettings.json', json.dumps(config))
    stage('certificate_generation')
    run(['pwsh', '-NoProfile', '-File', str(ROOT / 'scripts/oidc-lab-certificates.ps1'), '-Directory', str(PRIVATE)])
    write(PRIVATE / 'ready', 'DEV-OIDC-LAB-01\n')


def compose(*args):
    run(['docker', 'compose', '-p', PROJECT, '-f', str(ROOT / 'infra/oidc-lab/compose.yaml'), *args], timeout=300)


def wait_https(url):
    context = ssl.create_default_context(cafile=str(PRIVATE / 'ca.pem'))
    # No redirects or raw failures logged; hostname validation remains enabled.
    for _ in range(120):
        try:
            with urllib.request.urlopen(url, context=context, timeout=2) as response:
                if response.status == 200:
                    return
        except Exception:
            pass
        time.sleep(1)
    raise RuntimeError('https_readiness_timeout')


def environment():
    # Prevent ambient operator/production configuration from extending a lab array
    # or adding extra Kestrel listeners after the explicit local JSON is loaded.
    # Default host/web-host providers strip these prefixes before chaining host
    # configuration into app configuration. Compare their effective keys too;
    # JSON overrides values, but cannot remove additional endpoint/array entries.
    blocked = ('authentication:oidc:', 'connectionstrings:', 'kestrel:', 'developmentlab:', 'securefiles:', 'secretreferences:',
               'workspacecontext:', 'workspacenavigation:')
    exact = {'poseidon_database', 'poseidon_lab_config', 'urls', 'http_ports', 'https_ports'}

    def isolated(key):
        key = key.lower().replace('__', ':')
        for prefix in ('dotnet_', 'aspnetcore_'):
            if key.startswith(prefix):
                key = key[len(prefix):]
                break
        return not key.startswith(blocked) and key not in exact

    env = {key: value for key, value in os.environ.items()
           if isolated(key)}
    env.update({'ASPNETCORE_ENVIRONMENT': 'Development', 'DOTNET_ENVIRONMENT': 'Development',
                'POSEIDON_LAB_CONFIG': str(PRIVATE / 'appsettings.json'),
                'DOTNET_ROOT': str(Path(root_tool('dotnet')).parent)})
    return env


def start():
    init()
    # An existing healthy task-owned app makes start idempotent; no reimport/reseed.
    if (PRIVATE / 'app.pid').exists() and owned_process('check'):
        wait_https(APP + '/health/live')
        return
    # Compose can reuse its own database/provider on a retry. App port must be free.
    with socket.socket() as connection:
        if connection.connect_ex(('127.0.0.1', 5443)) == 0:
            raise RuntimeError('app_port_in_use')
    stage('compose_start')
    compose('up', '-d', '--wait')
    stage('provider_https_readiness')
    wait_https(AUTHORITY + '/.well-known/openid-configuration')
    env = environment()
    config = json.loads((PRIVATE / 'appsettings.json').read_text())
    env['POSEIDON_DATABASE'] = config['ConnectionStrings']['Poseidon']
    stage('database_migration')
    for attempt in range(30):
        try:
            run([root_tool('dotnet'), 'run', '--project', 'backend/Poseidon.DbMigrator', '-c', 'Release', '--no-build',
                 '--', '--seed', 'backend/seed/synthetic-demo.json'], env=env)
            break
        except RuntimeError:
            if attempt == 29:
                raise
            time.sleep(1)
    stage('app_https_readiness')
    process = subprocess.Popen([root_tool('dotnet'), str(ROOT / 'backend/Poseidon.Api/bin/Release/net10.0/Poseidon.Api.dll'),
                                '--Lab:Instance', str(PRIVATE)], cwd=ROOT, env=env,
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                               creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    write(PRIVATE / 'app.pid', str(process.pid))
    wait_https(APP + '/health/live')


def owned_process(action):
    if not (PRIVATE / 'app.pid').exists():
        return False
    pid = int((PRIVATE / 'app.pid').read_text())
    # PID reuse must never stop an unrelated host. Match unique worktree marker AND DLL.
    result = subprocess.run(['pwsh', '-NoProfile', '-File', str(ROOT / 'scripts/oidc-lab-private.ps1'),
                             '-Action', action, '-ProcessId', str(pid), '-Directory', str(PRIVATE), '-Dll',
                             str(ROOT / 'backend/Poseidon.Api/bin/Release/net10.0/Poseidon.Api.dll')],
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    return result.returncode == 0


def stop():
    stage('owned_services_stop')
    owned_process('stop')
    compose('down') # No --volumes, no prune, no global container deletion.


def browser():
    stage('real_provider_browser')
    write(PRIVATE / 'browser-counts.json', json.dumps({'status': 'not_completed', 'passed': 0, 'failed': 0, 'failureLines': []}))
    env = environment()
    env['POSEIDON_LAB_PRIVATE'] = str(PRIVATE)
    env['POSEIDON_LAB_PYTHON'] = sys.executable
    env['NODE_EXTRA_CA_CERTS'] = str(PRIVATE / 'ca.pem')
    env.pop('NODE_TLS_REJECT_UNAUTHORIZED', None)
    run([root_tool('node'), str(ROOT / 'tests/Poseidon.Browser.Tests/lab/run.ts')], env=env, timeout=300)


def register_membership(config):
    """Explicit lab-only capability enrollment; preserve identities and all other grants."""
    realm = json.loads((PRIVATE / 'realm/poseidon-lab-realm.json').read_text())
    subjects = [u['id'] for u in realm['users'] if u['username'] == 'lab-manager']
    if len(subjects) != 1:
        raise RuntimeError('lab_manager_not_unique')
    members = [m for m in config['Authentication']['Oidc']['Memberships']
               if m['Issuer'] == AUTHORITY and m['Subject'] == subjects[0]]
    if len(members) != 1 or not members[0]['TenantWideRead'] or not members[0]['TenantWideManage']:
        raise RuntimeError('lab_manager_scope_invalid')
    for permission in ('regulatory.draft.read', 'regulatory.draft.write'):
        if permission not in members[0]['Permissions']:
            members[0]['Permissions'].append(permission)
    return config


def prepare_register():
    stage('explicit_lab_register_membership')
    init()
    # Stop only this marked app, never rotate provider accounts or remove volumes.
    owned_process('stop')
    path = PRIVATE / 'appsettings.json'
    config = register_membership(json.loads(path.read_text()))
    write(path, json.dumps(config))


def register_browser():
    stage('real_provider_register_browser')
    env = environment()
    env['POSEIDON_LAB_PRIVATE'] = str(PRIVATE)
    env['POSEIDON_LAB_PYTHON'] = sys.executable
    env['NODE_EXTRA_CA_CERTS'] = str(PRIVATE / 'ca.pem')
    env.pop('NODE_TLS_REJECT_UNAUTHORIZED', None)
    run([root_tool('node'), str(ROOT / 'tests/Poseidon.Browser.Tests/lab/register-run.ts')], env=env, timeout=600)


def navigation_membership(config, realm):
    """Explicit synthetic signed-host setup; labels/mappings never grant runtime authority.

    Work on a copy so a conflicting operator catalog cannot partially enroll roles.
    This function performs no provider calls and never changes provider identities.
    """
    candidate = deepcopy(config)
    tenant = '10000000-0000-0000-0000-000000000001'
    subjects = [u['id'] for u in realm['users'] if u['username'] == 'lab-manager']
    if realm.get('realm') != 'poseidon-lab' or len(subjects) != 1:
        raise RuntimeError('lab_navigation_identity_invalid')
    federation = candidate['Authentication']['Oidc']
    if (federation.get('Authority') != AUTHORITY or federation.get('Issuer') != AUTHORITY or
            federation.get('ClientId') != 'poseidon-local'):
        raise RuntimeError('lab_navigation_provider_invalid')
    members = [m for m in candidate['Authentication']['Oidc']['Memberships']
               if m['Issuer'] == AUTHORITY and m['Subject'] == subjects[0]]
    if (len(members) != 1 or members[0].get('TenantId') != tenant or
            members[0].get('TenantWideRead') is not True or members[0].get('TenantWideManage') is not True):
        raise RuntimeError('lab_navigation_scope_invalid')
    member = members[0]
    daily = str(uuid.uuid5(uuid.NAMESPACE_URL, PROJECT + '/synthetic-writer-assignment'))
    review = str(uuid.uuid5(uuid.NAMESPACE_URL, PROJECT + '/synthetic-review-assignment'))
    refs = member.get('RoleReferences', [])
    if not isinstance(refs, list) or len(refs) > 16:
        raise RuntimeError('lab_navigation_roles_invalid')
    try:
        canonical = [str(uuid.UUID(ref)) for ref in refs]
    except (ValueError, TypeError, AttributeError):
        raise RuntimeError('lab_navigation_roles_invalid') from None
    # Unknown operator roles need explicit manual reconciliation; never invent
    # meanings/catalog entries for them or silently discard their attribution.
    if (canonical != refs or len(set(refs)) != len(refs) or
            any(ref not in (daily, review) for ref in refs)):
        raise RuntimeError('lab_navigation_existing_roles_require_manual_setup')
    member['RoleReferences'] = list(dict.fromkeys([*refs, daily, review]))
    labels = candidate.setdefault('WorkspaceContext', {}).setdefault('RoleCatalog', {})
    mapping = candidate.setdefault('WorkspaceNavigation', {})
    for catalog, key in ((labels, 'Roles'), (mapping, 'Mappings')):
        catalog.setdefault('Version', 'SYNTHETIC-LAB-NAVIGATION-v1')
        version = catalog['Version']
        if (not isinstance(version, str) or not 1 <= len(version) <= 64 or
                version.strip() != version or any(ord(c) < 32 or 127 <= ord(c) <= 159 for c in version)):
            raise RuntimeError('lab_navigation_catalog_invalid')
        rows = catalog.setdefault(key, [])
        if not isinstance(rows, list) or len(rows) > 4096 or any(not isinstance(row, dict) for row in rows):
            raise RuntimeError('lab_navigation_catalog_invalid')
    for ref, tr, en, keys in (
        (daily, 'KURGUSAL günlük iş rolü', 'FICTIONAL daily work role',
         ['today', 'fleet', 'crew', 'equipment', 'maintenance', 'tug-command', 'tug-job-board']),
        (review, 'KURGUSAL inceleme rolü', 'FICTIONAL review role',
         ['fleet', 'import', 'audit', 'sources', 'requirements']),
    ):
        for rows, expected in (
            (labels['Roles'], {'TenantId': tenant, 'Reference': ref, 'Tr': tr, 'En': en}),
            (mapping['Mappings'], {'TenantId': tenant, 'Reference': ref,
                                  'Modes': ['Fleet', 'Ship', 'Tug'], 'Keys': keys}),
        ):
            matches = [row for row in rows if str(row.get('TenantId', '')).lower() == tenant and
                       str(row.get('Reference', '')).lower() == ref]
            if matches and (len(matches) != 1 or matches[0] != expected):
                raise RuntimeError('lab_navigation_operator_catalog_preserved')
            if not matches:
                if len(rows) >= 4096:
                    raise RuntimeError('lab_navigation_catalog_budget')
                rows.append(expected)
    # Explicit read-only fixture grants for the five real workspace families.
    # No new write, safety, verifier, owner or source-validation capability.
    for permission in ('crew.read', 'equipment.read', 'maintenance.read', 'import.read',
                       'mode.tug', 'mode.cross', 'tug.read'):
        if permission not in member['Permissions']:
            member['Permissions'].append(permission)
    return candidate


def prepare_bundle():
    """Explicit disposable fixture enrollment only; never production operational appointments."""
    stage('explicit_lab_bundle_configuration')
    port = int(os.environ.get('POSEIDON_GATE_CLAMD_PORT', '0'))
    if not 0 < port <= 65535:
        raise RuntimeError('real_scanner_port_required')
    with socket.create_connection(('127.0.0.1', port), timeout=2) as peer:
        peer.sendall(b'zPING\0')
        if peer.recv(5) != b'PONG\0':
            raise RuntimeError('real_scanner_unready')
    prepare_register()
    path = PRIVATE / 'appsettings.json'
    config = json.loads(path.read_text())
    realm = json.loads((PRIVATE / 'realm/poseidon-lab-realm.json').read_text())
    subject = next(u['id'] for u in realm['users'] if u['username'] == 'lab-manager')
    member = next(m for m in config['Authentication']['Oidc']['Memberships'] if m['Issuer'] == AUTHORITY and m['Subject'] == subject)
    for permission in ('audit.read', 'file.read', 'file.write'):
        if permission not in member['Permissions']:
            member['Permissions'].append(permission)
    if not member.get('RoleReferences'):
        member['RoleReferences'] = [str(uuid.uuid5(uuid.NAMESPACE_URL, PROJECT + '/synthetic-writer-assignment'))]
    config = navigation_membership(config, realm)
    config = catalog_membership(config, realm, AUTHORITY, PROJECT)
    objects = Path(tempfile.gettempdir()) / (PROJECT + '-audit-files-objects')
    private_directory(objects)
    config['SecureFiles'] = {'Adapter': 'LocalDevelopment', 'Root': str(objects), 'MaxBytes': 1048576,
                             'MaxPixels': 1048576, 'ScanSeconds': 15, 'ClamdPort': port, 'CapabilitySeconds': 60,
                             'RetentionReference': 'synthetic-lab-only', 'ScannerReference': 'pinned-ephemeral-clamd'}
    write(path, json.dumps(config))


def prepare_humans():
    """Explicit real-provider reviewer setup; no session or source approval fabrication.

    Existing labs are upgraded through their owned provider API, never by replacing
    the realm/volume or converting the restricted actor. Diagnostics stay finite.
    """
    stage('explicit_lab_human_provider_setup')
    init()
    config_path, realm_path = PRIVATE / 'appsettings.json', PRIVATE / 'realm/poseidon-lab-realm.json'
    config, realm = json.loads(config_path.read_text()), json.loads(realm_path.read_text())
    credentials_path = PRIVATE / 'credentials.json'
    credentials = json.loads(credentials_path.read_text())
    federation = config.get('Authentication', {}).get('Oidc', {})
    if (realm.get('realm') != 'poseidon-lab' or federation.get('Authority') != AUTHORITY or
            federation.get('Issuer') != AUTHORITY or federation.get('ClientId') != 'poseidon-local'):
        raise RuntimeError('lab_human_provider_invalid')
    planned = [u for u in realm['users'] if u['username'] == 'lab-reviewer']
    if len(planned) > 1:
        raise RuntimeError('lab_human_user_ambiguous')
    if not planned:
        credentials['lab-reviewer'] = {'username': 'lab-reviewer', 'password': secrets.token_urlsafe(32)}
        reviewer = {'id': str(uuid.uuid4()), 'username': 'lab-reviewer', 'enabled': True, 'firstName': 'Synthetic', 'lastName': 'Reviewer',
                    'email': 'lab-reviewer@example.invalid', 'emailVerified': False,
                    'attributes': {'poseidonFixture': [PROJECT + '/reviewer']},
                    'credentials': [{'type': 'password', 'value': credentials['lab-reviewer']['password'], 'temporary': False}]}
        realm['users'].append(reviewer)
        # Persist random credentials before provider writes so interrupted retry recovers the same account.
        atomic_private_json(credentials_path, credentials); atomic_private_json(realm_path, realm)
    else:
        reviewer = planned[0]
    if (credentials.get('lab-reviewer', {}).get('username') != 'lab-reviewer' or
            not credentials['lab-reviewer'].get('password') or reviewer.get('enabled') is not True):
        raise RuntimeError('lab_human_private_identity_invalid')
    compose('up', '-d')
    wait_https(AUTHORITY + '/.well-known/openid-configuration')
    import urllib.parse
    context = ssl.create_default_context(cafile=str(PRIVATE / 'ca.pem'))
    bootstrap = dict(line.split('=', 1) for line in (PRIVATE / 'provider.env').read_text().splitlines() if '=' in line)
    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, request, peer, code, message, headers, target):
            return None
    opener = urllib.request.build_opener(NoRedirect(), urllib.request.HTTPSHandler(context=context))
    def exchange(path, *, payload=None, token=None, form=False):
        # Fixed loopback origin, verified private CA/hostname. Nothing from a response is logged.
        raw = urllib.parse.urlencode(payload).encode() if form else json.dumps(payload).encode() if payload is not None else None
        headers = {'Content-Type': 'application/x-www-form-urlencoded' if form else 'application/json'}
        if token:
            headers['Authorization'] = 'Bearer ' + token
        request = urllib.request.Request('https://localhost:8443' + path, data=raw, headers=headers)
        with opener.open(request, timeout=15) as response:
            body = response.read(1048577)
            if len(body) > 1048576:
                raise RuntimeError('lab_human_provider_response_budget')
            return json.loads(body) if body else None
    token = exchange('/realms/master/protocol/openid-connect/token', form=True, payload={
        'grant_type': 'password', 'client_id': 'admin-cli', 'username': bootstrap['KC_BOOTSTRAP_ADMIN_USERNAME'],
        'password': bootstrap['KC_BOOTSTRAP_ADMIN_PASSWORD']})['access_token']
    users_path = '/admin/realms/poseidon-lab/users'
    query = users_path + '?username=lab-reviewer&exact=true'
    found = exchange(query, token=token)
    if len(found) == 0:
        payload = deepcopy(reviewer)
        payload['attributes'] = {'poseidonFixture': [PROJECT + '/reviewer']}
        exchange(users_path, payload=payload, token=token)
        found = exchange(query, token=token)
    if len(found) != 1 or found[0].get('enabled') is not True or (found[0]['id'] != reviewer['id'] and
            found[0].get('attributes', {}).get('poseidonFixture') != [PROJECT + '/reviewer']):
        raise RuntimeError('lab_human_existing_provider_user_preserved')
    actual_id = found[0]['id']
    if str(uuid.UUID(actual_id)) != actual_id:
        raise RuntimeError('lab_human_provider_identity_invalid')
    old_id = reviewer['id']; reviewer['id'] = actual_id
    members = federation['Memberships']
    matching = [m for m in members if m.get('Issuer') == AUTHORITY and m.get('Subject') in (old_id, actual_id)]
    if len(matching) > 1:
        raise RuntimeError('lab_human_membership_ambiguous')
    if matching:
        matching[0]['Subject'] = actual_id
    else:
        members.append({'Issuer': AUTHORITY, 'Subject': actual_id, 'ActorId': str(uuid.uuid5(uuid.NAMESPACE_URL, PROJECT + '/reviewer-actor')),
                        'TenantId': '10000000-0000-0000-0000-000000000001', 'Permissions': ['directory.read'],
                        'TenantWideRead': False, 'TenantWideManage': False,
                        'VesselGrants': [{'VesselId': '30000000-0000-0000-0000-000000000001', 'CanEdit': False}]})
    config = catalog_membership(config, realm, AUTHORITY, PROJECT)
    # Existing ownership marker controls app shutdown; provider volumes and other services are preserved.
    owned_process('stop')
    # Config first: interrupted retry can match either the planned or actual
    # provider subject before the realm observation is updated, without a new actor.
    atomic_private_json(config_path, config); atomic_private_json(realm_path, realm)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['init', 'start', 'stop', 'browser', 'prepare_register', 'prepare_humans', 'prepare_bundle', 'register_browser'])
    action = parser.parse_args().action
    try:
        if sys.version_info[:3] != (3, 13, 13):
            raise RuntimeError('pinned_python_required')
        globals()[action]()
        print('DEV-OIDC-LAB-01 ' + action + ' PASS')
    except Exception:
        # Raw exceptions are deliberately not printed, even for provisioning failures.
        print('DEV-OIDC-LAB-01 ' + action + ' FAILED at ' + STAGE + '; private state preserved; no raw diagnostics retained')
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
