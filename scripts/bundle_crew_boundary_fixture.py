"""Fresh-genesis-only crew persona with an exact six-capability boundary.

The caller extends its in-memory provider import before the first immutable
publication. This module performs no I/O or provider requests and never rotates
an existing principal or changes the four established fixture accounts.
"""
from copy import deepcopy
import re
import secrets
import uuid

import lab_runtime

ACCOUNT = 'lab-crew-boundary'
PERMISSIONS = ('directory.read', 'crew.read', 'crew.write', 'crew.identity.read',
               'crew.certificate.read', 'crew.history.read')
ORIGINALS = {'lab-manager': 'writerActorId', 'lab-reviewer': 'reviewerActorId',
             'lab-restricted': 'restrictedActorId', 'lab-foreign': 'foreignActorId'}


def invalid():
    raise RuntimeError('crew_boundary_fixture_invalid')


def identifier(value):
    if type(value) is not str:
        invalid()
    try:
        parsed = uuid.UUID(value)
    except (ValueError, AttributeError):
        invalid()
    if str(parsed) != value or parsed.int == 0:
        invalid()
    return value


def identity(run, purpose):
    return str(uuid.uuid5(uuid.UUID(identifier(run)), 'poseidon.crew-boundary.v1/' + purpose))


def _password(value):
    return type(value) is str and re.fullmatch(r'[A-Za-z0-9_-]{32,128}', value) is not None


def configure(config, realm, credentials, metadata):
    """Return copies with exactly one scoped persona, or preserve our exact copy.

    Idempotence applies only within the same not-yet-published run. The immutable
    publisher separately rejects any later identity insertion into an old lab.
    """
    if any(type(value) is not dict for value in (config, realm, credentials, metadata)):
        invalid()
    if metadata.get('schemaVersion') != 'poseidon.bundle-fixture-plan.v1' or realm.get('realm') != 'poseidon-lab':
        invalid()
    for key in ('runId', 'tenantId', 'vesselId', 'foreignTenantId', *ORIGINALS.values()):
        identifier(metadata.get(key))
    if metadata['tenantId'] == metadata['foreignTenantId']:
        invalid()
    authentication = config.get('Authentication')
    if type(authentication) is not dict or type(authentication.get('Oidc')) is not dict:
        invalid()
    federation = authentication['Oidc']
    issuer = lab_runtime.authority(config)
    runtime = lab_runtime.configured(config)
    if (federation.get('Authority') != issuer or federation.get('Issuer') != issuer or
            federation.get('ClientId') != 'poseidon-local' or
            runtime is not None and runtime['runId'] != metadata['runId']):
        invalid()
    users, members = realm.get('users'), federation.get('Memberships')
    if type(users) is not list or type(members) is not list or len(users) not in (4, 5) or len(members) != len(users):
        invalid()
    if any(type(value) is not dict for value in (*users, *members)):
        invalid()
    names = [user.get('username') for user in users]
    if any(type(name) is not str for name in names) or len(set(names)) != len(names):
        invalid()
    present = ACCOUNT in names
    expected_names = set(ORIGINALS) | ({ACCOUNT} if present else set())
    if set(names) != expected_names or set(credentials) != expected_names:
        invalid()
    subjects, actors, roles = [], [], []
    for member in members:
        subjects.append(identifier(member.get('Subject')))
        actors.append(identifier(member.get('ActorId')))
        identifier(member.get('TenantId'))
        if member.get('Issuer') != issuer:
            invalid()
        references = member.get('RoleReferences', [])
        if type(references) is not list or len(references) > 16:
            invalid()
        roles.extend(identifier(ref) for ref in references)
    if len(set(subjects)) != len(subjects) or len(set(actors)) != len(actors):
        invalid()
    if {identifier(user.get('id')) for user in users} != set(subjects):
        invalid()
    for name, actor_key in ORIGINALS.items():
        user = next(user for user in users if user['username'] == name)
        matches = [member for member in members if member['Subject'] == user['id']]
        secret = credentials[name]
        tenant = metadata['foreignTenantId'] if name == 'lab-foreign' else metadata['tenantId']
        if (user.get('enabled') is not True or len(matches) != 1 or matches[0]['ActorId'] != metadata[actor_key] or
                matches[0]['TenantId'] != tenant or type(secret) is not dict or set(secret) != {'username', 'password'} or
                secret.get('username') != name or not _password(secret.get('password')) or
                user.get('credentials') != [{'type': 'password', 'value': secret['password'], 'temporary': False}]):
            invalid()
    subject, actor, role = (identity(metadata['runId'], purpose) for purpose in ('subject', 'actor', 'audit-role'))
    original_members = [member for member in members if member['Subject'] != subject]
    if (len(original_members) != 4 or any(member['ActorId'] == actor for member in original_members) or
            any(role in member.get('RoleReferences', []) for member in original_members)):
        invalid()
    occupied = {value for member in original_members for value in
                (member['Subject'], member['ActorId'], *member.get('RoleReferences', []))}
    if len({subject, actor, role}) != 3 or occupied.intersection((subject, actor, role)):
        invalid()
    expected_member = {'Issuer': issuer, 'Subject': subject, 'ActorId': actor, 'TenantId': metadata['tenantId'],
        'Permissions': list(PERMISSIONS), 'TenantWideRead': False, 'TenantWideManage': False,
        'VesselGrants': [{'VesselId': metadata['vesselId'], 'CanEdit': True}], 'RoleReferences': [role], 'ActorKind': 'Human'}
    if present:
        secret = credentials[ACCOUNT]
        if (type(secret) is not dict or set(secret) != {'username', 'password'} or secret.get('username') != ACCOUNT or
                not _password(secret.get('password'))):
            invalid()
        password = secret['password']
    else:
        if subject in subjects or actor in actors or role in roles:
            invalid()
        password = secrets.token_urlsafe(32)
    expected_user = {'id': subject, 'username': ACCOUNT, 'enabled': True, 'firstName': 'Synthetic',
        'lastName': 'CrewBoundary', 'email': ACCOUNT + '@example.invalid', 'emailVerified': False,
        'credentials': [{'type': 'password', 'value': password, 'temporary': False}]}
    if present:
        own_user = next(user for user in users if user['username'] == ACCOUNT)
        own_member = next((member for member in members if member['Subject'] == subject), None)
        if (own_user != expected_user or own_user.get('enabled') is not True or own_user.get('emailVerified') is not False or
                own_user['credentials'][0].get('temporary') is not False or own_member != expected_member or
                own_member.get('TenantWideRead') is not False or own_member.get('TenantWideManage') is not False or
                own_member['VesselGrants'][0].get('CanEdit') is not True):
            invalid()
    copied, imported, private = deepcopy(config), deepcopy(realm), deepcopy(credentials)
    if not present:
        copied['Authentication']['Oidc']['Memberships'].append(expected_member)
        imported['users'].append(expected_user)
        private[ACCOUNT] = {'username': ACCOUNT, 'password': password}
    return copied, imported, private
