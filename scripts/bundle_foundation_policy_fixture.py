"""FOUNDATION-POLICY-SIGNED-FIXTURE-01: pure, explicit synthetic lab supplement.

No production classification/purpose/consent or human/source/safety approval is
asserted. Only fictional nullable Draft watch capture and public PNG tests apply.
"""
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import re
import uuid

import bundle_fixture_plan as base
import lab_runtime

SCHEMA = 'poseidon.foundation-policy-fixture.v1'
PLAN_KEYS = {'schemaVersion', 'runId', 'tenantId', 'vesselId', 'writerActorId', 'reviewerActorId',
             'writerRoleId', 'reviewerRoleId', 'notBefore', 'expiresAt'}
DAILY_ANCHORS = ['today', 'fleet', 'crew', 'equipment', 'maintenance', 'tug-command', 'tug-job-board']
REVIEW_ANCHORS = ['fleet', 'import', 'audit', 'sources', 'requirements']
EXTENSIONS = ['noon-report', 'deck-daily-log', 'engine-daily-log', 'fuel-bunker-log', 'port-activity-sof',
              'operational-downtime', 'defects', 'work-order-center', 'certificate-command-center', 'renewal-workflow',
              'audit', 'import', 'sources', 'requirements']
DAILY = DAILY_ANCHORS + EXTENSIONS[:8]
REVIEW = REVIEW_ANCHORS + ['certificate-command-center', 'renewal-workflow']
DOCUMENT_PERMISSIONS = ('crew.documents.read', 'crew.documents.write')
PREREQUISITES = ('directory.read', 'directory.write', 'crew.read', 'crew.write', 'crew.identity.read',
                 'crew.identity.write', 'file.read', 'file.write')
OFFLINE_SOURCE = '3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018'


def invalid():
    raise RuntimeError('foundation_policy_fixture_invalid')


def exact(value, expected):
    """Do not let JSON floats or booleans compare equal to intended integers."""
    if type(value) is not type(expected):
        return False
    if isinstance(expected, dict):
        return value.keys() == expected.keys() and all(exact(value[key], item) for key, item in expected.items())
    if isinstance(expected, list):
        return len(value) == len(expected) and all(exact(a, b) for a, b in zip(value, expected))
    return value == expected


def guid(value):
    if not isinstance(value, str) or len(value) != 36:
        invalid()
    try:
        if str(uuid.UUID(value)) != value or uuid.UUID(value).int == 0:
            invalid()
    except (ValueError, AttributeError):
        invalid()
    return value


def instant(value):
    if not isinstance(value, str) or re.fullmatch(r'[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,6})?(?:Z|\+00:00)', value) is None:
        invalid()
    try:
        return datetime.fromisoformat(value)
    except ValueError:
        invalid()


def references(project):
    if not isinstance(project, str) or re.fullmatch(r'poseidon-lab-[0-9a-f]{12}', project) is None:
        invalid()
    return tuple(str(uuid.uuid5(uuid.NAMESPACE_URL, project+suffix)) for suffix in
                 ('/synthetic-writer-assignment', '/synthetic-review-assignment'))


def metadata_scope(metadata, project):
    if (not isinstance(metadata, dict) or metadata.get('schemaVersion') != 'poseidon.bundle-fixture-plan.v1'
            or metadata.get('tenantId') != base.TENANT or metadata.get('vesselId') != base.SHIP
            or metadata.get('sourceVersion') != base.DAILY_SOURCE):
        invalid()
    for key in ('runId', 'writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId'):
        guid(metadata.get(key))
    if metadata['writerActorId'] == metadata['reviewerActorId'] or (metadata['writerRoleId'], metadata['reviewerRoleId']) != references(project):
        invalid()


def create_plan(metadata, project, now=None):
    metadata_scope(metadata, project)
    now = datetime.now(timezone.utc) if now is None else now
    if not isinstance(now, datetime) or now.tzinfo is None or now.utcoffset() != timedelta(0):
        invalid()
    return {'schemaVersion': SCHEMA, **{key: metadata[key] for key in PLAN_KEYS - {'schemaVersion', 'notBefore', 'expiresAt'}},
            'notBefore': (now-timedelta(minutes=1)).isoformat(), 'expiresAt': (now+timedelta(hours=8)).isoformat()}


def validate_plan(plan, metadata, project, now=None):
    metadata_scope(metadata, project)
    if not isinstance(plan, dict) or set(plan) != PLAN_KEYS or plan['schemaVersion'] != SCHEMA:
        invalid()
    for key in PLAN_KEYS - {'schemaVersion', 'notBefore', 'expiresAt'}:
        if plan[key] != metadata.get(key):
            invalid()
    start, end = instant(plan['notBefore']), instant(plan['expiresAt'])
    now = datetime.now(timezone.utc) if now is None else now
    if (not isinstance(now, datetime) or now.tzinfo is None or now.utcoffset() != timedelta(0)
            or not timedelta(0) < end-start <= timedelta(hours=9) or not start <= now < end):
        invalid()
    return plan


def policies(plan):
    """Exact software-only values within the unchanged application policy guards."""
    return {
        'OfflineApplication': {
            'Version': 'SYNTHETIC-FOUNDATION-OFFLINE-'+plan['runId'], 'SourceVersion': OFFLINE_SOURCE,
            'Purpose': 'SYNTHETIC-UNAPPROVED nullable Draft watch-group software test',
            'FieldClassification': 'NonPersonalControlledDraftDeclaration',
            'DevicePolicyReference': 'SYNTHETIC-UNAPPROVED disposable run '+plan['runId'],
            'KeyPolicyReference': 'SYNTHETIC-UNAPPROVED actual browser WebCrypto test',
            'StoragePolicyReference': 'SYNTHETIC-UNAPPROVED actual encrypted IndexedDB test',
            'RetentionPolicyReference': 'SYNTHETIC-UNAPPROVED eight-hour software window; no automatic deletion claim',
            'Operation': 'crew.draft.watch-group.v1', 'Fields': ['watch_group'], 'WatchGroups': ['SYNTHETIC-A', 'SYNTHETIC-B'],
            'Scopes': [{'TenantId': plan['tenantId'], 'ActorId': plan['writerActorId'], 'VesselId': plan['vesselId']}],
            'NotBefore': plan['notBefore'], 'ExpiresAt': plan['expiresAt'], 'LeaseSeconds': 3600,
            'MaxRecords': 8, 'MaxEncryptedBytes': 32768, 'MaxDeviceRecords': 16, 'MaxDeviceEncryptedBytes': 65536, 'MaxEnrollments': 8,
        },
        'CrewFilePolicy': {'Version': 'SYNTHETIC-FOUNDATION-FILES-'+plan['runId'], 'SourceVersion': 'poseidon-v46.3-20261030',
            'Purpose': 'SYNTHETIC-UNAPPROVED-person-files-'+plan['runId'], 'Kinds': ['Photo', 'Passport']},
    }


def configure(config, metadata, plan, project, now=None):
    """Return a copy or fail without modifying any input or adding broad authority.

    Exactly two known states are accepted: fresh old21/5 with absent policies,
    or the fully applied same-run15/7 plus exact policies/permissions. Partial
    edits, revoked permissions, stale windows and operator values are not repaired.
    """
    validate_plan(plan, metadata, project, now)
    if not isinstance(config, dict):
        invalid()
    authentication = config.get('Authentication')
    if not isinstance(authentication, dict) or not isinstance(authentication.get('Oidc'), dict):
        invalid()
    federation = authentication['Oidc']
    authority = lab_runtime.authority(config)
    if (federation.get('Authority') != authority or federation.get('Issuer') != authority
            or federation.get('ClientId') != 'poseidon-local'):
        invalid()
    members = federation.get('Memberships')
    if not isinstance(members, list) or any(not isinstance(member, dict) for member in members):
        invalid()
    actors = [guid(m.get('ActorId')) for m in members]
    if len(set(actors)) != len(actors):
        invalid()
    selected = []
    for name in ('writer', 'reviewer'):
        matches = [m for m in members if m.get('ActorId') == plan[name+'ActorId']]
        if len(matches) != 1:
            invalid()
        member = matches[0]
        guid(member.get('Subject'))
        if (member.get('Issuer') != authority or member.get('TenantId') != base.TENANT or member.get('ActorKind') != 'Human'
                or sum(m.get('Issuer') == authority and m.get('Subject') == member['Subject'] for m in members) != 1):
            invalid()
        selected.append(member)
    writer, reviewer = selected
    daily, review = references(project)
    if (not exact(writer.get('RoleReferences'), [daily, review]) or not exact(reviewer.get('RoleReferences'), [review])
            or writer.get('TenantWideRead') is not True or writer.get('TenantWideManage') is not True):
        invalid()
    for member in members:
        permissions = member.get('Permissions')
        if not isinstance(permissions, list) or any(not isinstance(p, str) or not p for p in permissions) or len(set(permissions)) != len(permissions):
            invalid()
        if member is not writer and any(p in permissions for p in DOCUMENT_PERMISSIONS):
            invalid()
    if any(permission not in writer['Permissions'] for permission in PREREQUISITES):
        invalid()
    labels = {'Version': 'SYNTHETIC-LAB-NAVIGATION-v1', 'Roles': [
        {'TenantId': base.TENANT, 'Reference': daily, 'Tr': 'KURGUSAL günlük iş rolü', 'En': 'FICTIONAL daily work role'},
        {'TenantId': base.TENANT, 'Reference': review, 'Tr': 'KURGUSAL inceleme rolü', 'En': 'FICTIONAL review role'}]}
    context = config.get('WorkspaceContext')
    if not isinstance(context, dict) or not exact(context.get('RoleCatalog'), labels):
        invalid()
    row = lambda reference, keys: {'TenantId': base.TENANT, 'Reference': reference, 'Modes': ['Fleet', 'Ship', 'Tug'], 'Keys': keys}
    old = {'Version': labels['Version'], 'Mappings': [row(daily, DAILY_ANCHORS+EXTENSIONS), row(review, REVIEW_ANCHORS)]}
    applied = {'Version': labels['Version'], 'Mappings': [row(daily, DAILY), row(review, REVIEW)]}
    navigation = config.get('WorkspaceNavigation')
    expected = policies(plan)
    fresh = exact(navigation, old)
    if fresh:
        if any(key in config and not exact(config[key], {}) for key in expected) or any(p in writer['Permissions'] for p in DOCUMENT_PERMISSIONS):
            invalid()
    elif not (exact(navigation, applied) and all(key in config and exact(config[key], value) for key, value in expected.items())
              and all(p in writer['Permissions'] for p in DOCUMENT_PERMISSIONS)):
        invalid()
    candidate = deepcopy(config)
    candidate['WorkspaceNavigation'] = deepcopy(applied)
    for key, value in expected.items():
        candidate[key] = deepcopy(value)
    copy_writer = next(m for m in candidate['Authentication']['Oidc']['Memberships'] if m['ActorId'] == plan['writerActorId'])
    for permission in DOCUMENT_PERMISSIONS:
        if permission not in copy_writer['Permissions']:
            copy_writer['Permissions'].append(permission)
    return candidate


def require_applied(config, metadata, plan, project, now=None):
    if not exact(configure(config, metadata, plan, project, now), config):
        raise RuntimeError('foundation_policy_explicit_preparation_required')
