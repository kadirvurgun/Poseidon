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

ROOT = Path(__file__).resolve().parents[1]
PRIVATE = ROOT / '.runtime/oidc-lab'
AUTHORITY = 'https://localhost:8443/realms/poseidon-lab'
APP = 'https://localhost:5443'
PROJECT = 'poseidon-lab-' + hashlib.sha256(str(ROOT).encode()).hexdigest()[:12]
STAGE = 'initialization'


def root_tool(name):
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


def make_configuration():
    """Pure synthetic input, used by tests as well as explicit provisioning."""
    manager, restricted = str(uuid.uuid4()), str(uuid.uuid4())
    credentials = {name: {'username': name, 'password': secrets.token_urlsafe(32)}
                   for name in ('lab-manager', 'lab-restricted')}
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
                   'firstName': 'Synthetic', 'lastName': 'Manager' if name == 'lab-manager' else 'Restricted',
                   'email': name + '@example.invalid', 'emailVerified': False,
                   'credentials': [{'type': 'password', 'value': credentials[name]['password'], 'temporary': False}]}
                  for name, subject in [('lab-manager', manager), ('lab-restricted', restricted)]]}
    tenant = '10000000-0000-0000-0000-000000000001'
    memberships = [{'Issuer': AUTHORITY, 'Subject': subject, 'ActorId': str(uuid.uuid4()),
                    'TenantId': tenant, 'Permissions': ['directory.read', 'directory.write', 'workspace.search'] if wide else ['directory.read'],
                    'TenantWideManage': wide, 'TenantWideRead': wide,
                    'VesselGrants': [] if wide else [{'VesselId': '30000000-0000-0000-0000-000000000001', 'CanEdit': False}]}
                   for subject, wide in [(manager, True), (restricted, False)]]
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
    blocked = ('authentication__oidc__', 'connectionstrings__', 'kestrel__', 'developmentlab__')
    exact = {'poseidon_database', 'poseidon_lab_config', 'aspnetcore_urls', 'aspnetcore_http_ports', 'aspnetcore_https_ports'}
    env = {key: value for key, value in os.environ.items()
           if not key.lower().startswith(blocked) and key.lower() not in exact}
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
    run([root_tool('node'), str(ROOT / 'tests/Poseidon.Browser.Tests/lab/run.ts')], env=env, timeout=300)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['init', 'start', 'stop', 'browser'])
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
