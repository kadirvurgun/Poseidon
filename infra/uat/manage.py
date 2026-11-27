#!/usr/bin/env python3
"""POSEIDON UAT operator tool. Runs on the VPS as the 'deploy' user in /opt/poseidon-uat.

  python3 manage.py init --domain uat.example.com
  python3 manage.py add-user ayse.po --profile po        # prints a one-time temporary password
  python3 manage.py render                               # re-render config after capabilities change
  python3 manage.py list-users

SYNTHETIC UAT ONLY. Profiles never include human-authority permissions (verify/approve/decide/
attest/sign...) and never create appointments; those stay a deliberate operator edit of
state/profiles.json. No secret is printed except a new user's one-time temporary password.
"""
import argparse
import json
import os
import re
import secrets
import subprocess
import sys
import uuid
from pathlib import Path

HOME = Path(__file__).resolve().parent
SECRETS, STATE, CONFIG, REALM = HOME / 'secrets', HOME / 'state', HOME / 'config', HOME / 'keycloak/realm'
REALM_NAME, CLIENT_ID = 'poseidon', 'poseidon-uat'
TENANT = '10000000-0000-0000-0000-000000000001'   # backend/seed/synthetic-demo.json
COMPANY = '20000000-0000-0000-0000-000000000001'
ROLE_NAMESPACE = 'poseidon-uat/navigation-role/'
# Human-authority capabilities: never granted by default (AGENTS.md: no manufactured sign-offs).
AUTHORITY = re.compile(r'(\.(verify|approve|decide|attest|sign|verdict|assess|effectiveness|finalize|govern|qualify|close|release|complete|publish|triage|investigate|review)$)'
                       r'|\.(accept|reject|revoke|eligible|owner)$|\.verify\.record$|\.override\.'
                       r'|^human\.|^uat\.(attest|threshold\.record)$|^record\.purge\.approve$|^evidence\.verify$')
ADMIN_ONLY = {'membership.manage', 'membership.security.suspend', 'record.hold', 'record.retention.manage',
              'record.purge.request', 'config.draft'}


def fail(code: str) -> None:
    print(code, file=sys.stderr)
    raise SystemExit(2)


def write_private(path: Path, text: str, overwrite: bool = False) -> None:
    if path.exists() and not overwrite:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + '.tmp')
    fd = os.open(tmp, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(fd, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    os.replace(tmp, path)


def read_env(path: Path) -> dict:
    out = {}
    for line in path.read_text(encoding='utf-8').splitlines():
        if '=' in line and not line.startswith('#'):
            k, v = line.split('=', 1)
            out[k] = v
    return out


def domain() -> str:
    return read_env(HOME / '.env')['UAT_DOMAIN']


def issuer() -> str:
    return f'https://{domain()}/auth/realms/{REALM_NAME}'


def capabilities() -> dict:
    path = HOME / 'capabilities.json'
    if not path.exists():
        fail('capabilities_missing: deploy copies capabilities.json from the built source')
    return json.loads(path.read_text(encoding='utf-8'))


def default_profiles(caps: dict) -> dict:
    perms = caps['permissions']
    ordinary = [p for p in perms if not AUTHORITY.search(p)]
    return {
        '_note': 'Edit deliberately. Authority permissions (verify/approve/decide/...) are excluded by default; '
                 'adding them here is an operator decision for SYNTHETIC UAT only and never a real sign-off.',
        'excludedAuthorityPermissions': [p for p in perms if AUTHORITY.search(p)],
        'admin': ordinary,
        'po': [p for p in ordinary if p not in ADMIN_ONLY],
        'viewer': [p for p in ordinary if p.endswith('.read') or p in ('workspace.search', 'mode.tug', 'mode.cross', 'notification.acknowledge')],
    }


def navigation(caps: dict) -> tuple[list, dict, dict]:
    keys = caps['navigationKeys']
    chunks = [keys[i:i + 14] for i in range(0, len(keys), 14)]
    if len(chunks) > 16:
        fail('navigation_roles_over_budget')
    roles, labels, mappings = [], [], []
    for n, chunk in enumerate(chunks, 1):
        ref = str(uuid.uuid5(uuid.NAMESPACE_URL, ROLE_NAMESPACE + str(n)))
        roles.append(ref)
        labels.append({'TenantId': TENANT, 'Reference': ref, 'Tr': f'KURGUSAL UAT rolü {n}', 'En': f'FICTIONAL UAT role {n}'})
        mappings.append({'TenantId': TENANT, 'Reference': ref, 'Modes': ['Fleet', 'Ship', 'Tug'], 'Keys': chunk})
    version = 'UAT-NAVIGATION-v1'
    return roles, {'Version': version, 'Roles': labels}, {'Version': version, 'Mappings': mappings}


def load_users() -> list:
    path = STATE / 'users.json'
    return json.loads(path.read_text(encoding='utf-8')) if path.exists() else []


def render() -> None:
    caps = capabilities()
    profiles_path = STATE / 'profiles.json'
    write_private(profiles_path, json.dumps(default_profiles(caps), indent=1))
    profiles = json.loads(profiles_path.read_text(encoding='utf-8'))
    known = set(caps['permissions'])
    roles, role_catalog, nav = navigation(caps)
    memberships = []
    for user in load_users():
        perms = [p for p in profiles[user['profile']] if p in known]
        memberships.append({
            'Issuer': issuer(), 'Subject': user['subject'], 'ActorId': user['actorId'], 'TenantId': TENANT,
            'Permissions': perms, 'TenantWideRead': True, 'TenantWideManage': user['profile'] in ('admin', 'po'),
            'VesselGrants': [], 'RoleReferences': roles, 'ActiveCompanyId': COMPANY, 'ActorKind': 'Human'})
    config = {
        'Logging': {'LogLevel': {'Default': 'Information', 'Microsoft.AspNetCore': 'Warning'}},
        'Authentication': {'Oidc': {'Authority': issuer(), 'Issuer': issuer(), 'ClientId': CLIENT_ID,
                                    'ClientSecretReference': 'POSEIDON_OIDC_CLIENT_SECRET', 'Memberships': memberships}},
        'DataProtection': {'KeyDirectory': '/var/lib/poseidon/keys'},
        'RateLimiting': {'Store': 'Postgres', 'PartitionKeyReference': 'POSEIDON_RATE_LIMIT_KEY'},
        # No SMTP in UAT: queued invitation e-mail is finalized as NOT_CONFIGURED; admins share links by hand.
        'InvitationDelivery': {'Mode': 'Disabled'},
        'WorkspaceContext': {'RoleCatalog': role_catalog},
        'WorkspaceNavigation': nav,
    }
    write_private(CONFIG / 'appsettings.Staging.json', json.dumps(config, indent=1), overwrite=True)
    os.chmod(CONFIG / 'appsettings.Staging.json', 0o644)  # read by the non-root container user; holds no secret
    print(f'rendered: {len(memberships)} membership(s), {len(roles)} navigation role(s)')


def realm(client_secret: str) -> dict:
    app = f'https://{domain()}'
    return {
        'realm': REALM_NAME, 'enabled': True, 'sslRequired': 'external',
        'displayName': 'POSEIDON', 'displayNameHtml': 'POSEIDON', 'loginTheme': 'poseidon',
        'registrationAllowed': False, 'resetPasswordAllowed': False, 'rememberMe': False,
        'bruteForceProtected': True, 'failureFactor': 10, 'passwordPolicy': 'length(12) and notUsername',
        'accessTokenLifespan': 300, 'ssoSessionIdleTimeout': 1800,
        'internationalizationEnabled': True, 'supportedLocales': ['tr', 'en'], 'defaultLocale': 'tr',
        'clients': [{'clientId': CLIENT_ID, 'enabled': True, 'protocol': 'openid-connect', 'publicClient': False,
                     'secret': client_secret, 'standardFlowEnabled': True, 'directAccessGrantsEnabled': False,
                     'serviceAccountsEnabled': False, 'redirectUris': [app + '/signin-oidc'], 'webOrigins': [app],
                     'attributes': {'pkce.code.challenge.method': 'S256',
                                    'post.logout.redirect.uris': app + '/signout-callback-oidc'}}],
    }


def init(args) -> None:
    if not re.fullmatch(r'[a-z0-9.-]+\.[a-z]{2,}', args.domain):
        fail('domain_invalid')
    if (STATE / 'initialized').exists():
        fail('already_initialized: secrets are never rotated by init')
    write_private(HOME / '.env', f'UAT_DOMAIN={args.domain}\n')
    pg, kc_db = secrets.token_urlsafe(32), secrets.token_urlsafe(32)
    client = secrets.token_urlsafe(48)
    write_private(SECRETS / 'postgres-password', pg)
    write_private(SECRETS / 'keycloak-db-password', kc_db)
    write_private(SECRETS / 'db.env', f'POSEIDON_DATABASE=Host=postgres;Port=5432;Database=poseidon;Username=poseidon;Password={pg};Include Error Detail=false\n')
    write_private(SECRETS / 'api.env', f'POSEIDON_OIDC_CLIENT_SECRET={client}\nPOSEIDON_RATE_LIMIT_KEY={secrets.token_urlsafe(48)}\n')
    write_private(SECRETS / 'keycloak.env', f'KC_DB_PASSWORD={kc_db}\nKC_BOOTSTRAP_ADMIN_USERNAME=uat-bootstrap\nKC_BOOTSTRAP_ADMIN_PASSWORD={secrets.token_urlsafe(32)}\n')
    write_private(REALM / f'{REALM_NAME}-realm.json', json.dumps(realm(client), indent=1))
    os.chmod(REALM / f'{REALM_NAME}-realm.json', 0o644)  # Keycloak container reads it; contains the client secret
    os.chmod(SECRETS / 'postgres-password', 0o644)      # compose file secret for the postgres uid
    os.chmod(SECRETS / 'keycloak-db-password', 0o644)
    write_private(STATE / 'users.json', '[]')
    write_private(STATE / 'initialized', 'ok\n')
    print('initialized. Next: first deploy (GitHub Actions), then: python3 manage.py add-user <name> --profile admin')


def compose(*cmd: str, stdin: str | None = None, capture: bool = False) -> str:
    tag = (HOME / 'deployed-tag').read_text().strip() if (HOME / 'deployed-tag').exists() else ''
    if not tag:
        fail('not_deployed_yet')
    env = dict(os.environ, POSEIDON_TAG=tag)
    result = subprocess.run(['docker', 'compose', '-f', str(HOME / 'compose.uat.yaml'), *cmd], cwd=HOME, env=env,
                            input=stdin, text=True, capture_output=capture, check=True)
    return result.stdout if capture else ''


def kcadm(script: str, capture: bool = False) -> str:
    login = ('/opt/keycloak/bin/kcadm.sh config credentials --server http://localhost:8080/auth --realm master '
             '--user "$KC_BOOTSTRAP_ADMIN_USERNAME" --password "$KC_BOOTSTRAP_ADMIN_PASSWORD" >/dev/null && ')
    return compose('exec', '-T', 'keycloak', 'bash', '-c', login + script, capture=capture)


def add_user(args) -> None:
    if not re.fullmatch(r'[a-z][a-z0-9._-]{2,40}', args.username):
        fail('username_invalid')
    users = load_users()
    if any(u['username'] == args.username for u in users):
        fail('user_exists')
    one_time = secrets.token_urlsafe(12)  # temporary sign-in credential, shown once
    body = json.dumps({'username': args.username, 'enabled': True, 'firstName': args.first_name or args.username,
                       'lastName': args.last_name or 'UAT', 'email': f'{args.username}@example.invalid',
                       'emailVerified': True})
    kcadm(f"/opt/keycloak/bin/kcadm.sh create users -r {REALM_NAME} -f - <<'JSON'\n{body}\nJSON")
    found = json.loads(kcadm(f'/opt/keycloak/bin/kcadm.sh get users -r {REALM_NAME} -q exact=true -q username={args.username} --fields id', capture=True))
    if len(found) != 1:
        fail('keycloak_user_lookup_failed')
    # token_urlsafe output is [A-Za-z0-9_-] only, so single quoting is safe.
    kcadm(f"/opt/keycloak/bin/kcadm.sh set-password -r {REALM_NAME} --username {args.username} --temporary --new-password '{one_time}'")
    users.append({'username': args.username, 'profile': args.profile, 'subject': found[0]['id'], 'actorId': str(uuid.uuid4())})
    write_private(STATE / 'users.json', json.dumps(users, indent=1), overwrite=True)
    render()
    compose('up', '-d', '--force-recreate', '--no-deps', 'api')
    print(f'user {args.username} ({args.profile}) created. One-time temporary password (change on first login): {one_time}')


def branding(_args=None) -> None:
    # Idempotent: applies the POSEIDON login theme and Turkish/English locales to an existing realm.
    kcadm(f"/opt/keycloak/bin/kcadm.sh update realms/{REALM_NAME} -s loginTheme=poseidon -s displayName=POSEIDON "
          "-s displayNameHtml=POSEIDON -s internationalizationEnabled=true -s defaultLocale=tr "
          "-s 'supportedLocales=[\"tr\",\"en\"]'")
    print('branding applied')


def list_users(_args) -> None:
    for u in load_users():
        print(f"{u['username']:<24} {u['profile']}")


def main() -> None:
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest='cmd', required=True)
    p = sub.add_parser('init')
    p.add_argument('--domain', required=True)
    p.set_defaults(fn=init)
    p = sub.add_parser('add-user')
    p.add_argument('username')
    p.add_argument('--profile', choices=['admin', 'po', 'viewer'], required=True)
    p.add_argument('--first-name')
    p.add_argument('--last-name')
    p.set_defaults(fn=add_user)
    sub.add_parser('render').set_defaults(fn=lambda _a: render())
    sub.add_parser('list-users').set_defaults(fn=list_users)
    sub.add_parser('branding').set_defaults(fn=branding)
    args = parser.parse_args()
    args.fn(args)


if __name__ == '__main__':
    main()
