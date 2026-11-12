"""OBSERVATION-SIGNED-FIXTURE-01: pure disposable localhost configuration.

No provider/runtime/database calls and no observations or approvals are created.
The explicit source-bundle entry points publish through immutable private generations.
"""
from copy import deepcopy
import uuid

AUTHORITY = 'https://localhost:8443/realms/poseidon-lab'
TENANT = '10000000-0000-0000-0000-000000000001'
SHIP = '30000000-0000-0000-0000-000000000001'
PERMISSION = 'maintenance.observation.write'
BASE_PERMISSIONS = {'directory.read', 'equipment.read', 'maintenance.read', 'maintenance.write'}


def _uuid(value):
    try:
        if not isinstance(value, str) or str(uuid.UUID(value)) != value or uuid.UUID(value).int == 0:
            raise ValueError()
    except (ValueError, TypeError, AttributeError):
        raise RuntimeError('observation_fixture_uuid_invalid') from None
    return value


def _same_policy(actual, expected):
    if not isinstance(actual, dict) or set(actual) != {'Sources'} or not isinstance(actual['Sources'], list) or len(actual['Sources']) != 1:
        return False
    rule = actual['Sources'][0]
    wanted = expected['Sources'][0]
    return isinstance(rule, dict) and set(rule) == set(wanted) and all(type(rule[key]) is type(value) and rule[key] == value for key, value in wanted.items())


def configure(config, metadata):
    """Add one same-run synthetic source and only its writer capability to a copy.

    This is test configuration, not an approved production source/unit/freshness
    catalog. Actual login, scoped GET and server capability checks remain required.
    """
    if not isinstance(config, dict) or not isinstance(metadata, dict) or metadata.get('schemaVersion') != 'poseidon.bundle-fixture-plan.v1':
        raise RuntimeError('observation_fixture_plan_required')
    for key in ('runId', 'tenantId', 'vesselId', 'writerActorId', 'writerRoleId'):
        _uuid(metadata.get(key))
    if metadata['tenantId'] != TENANT or metadata['vesselId'] != SHIP or metadata.get('state') != 'CONFIGURATION_ONLY_REQUIRES_REAL_LOGIN_AND_HTTP':
        raise RuntimeError('observation_fixture_scope_invalid')
    authentication = config.get('Authentication')
    federation = authentication.get('Oidc') if isinstance(authentication, dict) else None
    if not isinstance(federation, dict) or any(federation.get(key) != AUTHORITY for key in ('Authority', 'Issuer')) or federation.get('ClientId') != 'poseidon-local':
        raise RuntimeError('observation_fixture_provider_invalid')
    members = federation.get('Memberships')
    if not isinstance(members, list) or any(not isinstance(member, dict) for member in members):
        raise RuntimeError('observation_fixture_membership_invalid')
    writers = [member for member in members if member.get('ActorId') == metadata['writerActorId']]
    if len(writers) != 1:
        raise RuntimeError('observation_fixture_writer_ambiguous')
    writer = writers[0]
    if (writer.get('TenantId') != TENANT or writer.get('Issuer') != AUTHORITY or writer.get('ActorKind') != 'Human'
            or writer.get('TenantWideRead') is not True or writer.get('TenantWideManage') is not True):
        raise RuntimeError('observation_fixture_writer_scope_invalid')
    _uuid(writer.get('Subject'))
    roles = writer.get('RoleReferences')
    permissions = writer.get('Permissions')
    if not isinstance(roles, list) or not 1 <= len(roles) <= 16 or any(not isinstance(role, str) for role in roles) or metadata['writerRoleId'] not in roles or len(set(roles)) != len(roles):
        raise RuntimeError('observation_fixture_role_invalid')
    for role in roles:
        _uuid(role)
    if not isinstance(permissions, list) or any(not isinstance(value, str) for value in permissions) or not BASE_PERMISSIONS.issubset(permissions):
        raise RuntimeError('observation_fixture_base_permission_required')
    expected = {'Sources': [{
        'TenantId': TENANT, 'VesselId': SHIP, 'AssetId': SHIP,
        'Source': 'SYNTHETIC-OBS-' + metadata['runId'], 'Version': 'SYNTHETIC-OBS-v1-' + metadata['runId'],
        'Kind': 'Meter', 'Unit': 'hours', 'FreshnessSeconds': 3600, 'MaxScale': 4,
    }]}
    existing = config.get('MaintenanceObservations')
    empty = isinstance(existing, dict) and set(existing) == {'Sources'} and isinstance(existing['Sources'], list) and len(existing['Sources']) == 0
    if 'MaintenanceObservations' in config and not empty and not _same_policy(existing, expected):
        raise RuntimeError('observation_fixture_existing_policy_preserved')
    candidate = deepcopy(config)
    candidate['MaintenanceObservations'] = expected
    selected = candidate['Authentication']['Oidc']['Memberships'][members.index(writer)]
    if PERMISSION not in selected['Permissions']:
        selected['Permissions'].append(PERMISSION)
    return candidate
