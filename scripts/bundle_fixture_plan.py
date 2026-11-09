"""DEV-BUNDLE-FIXTURE-01: explicit isolated software fixture configuration only.

Source: 19.01 paragraphs 68-123/189-245; source module task contracts.
Never applied to existing operator profiles; no provider claims, signed sessions,
verified evidence, approval decisions or fake HTTP receipts are created here.
"""
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import hashlib
import uuid

TENANT = '10000000-0000-0000-0000-000000000001'
SHIP = '30000000-0000-0000-0000-000000000001'
FOREIGN_TENANT = '10000000-0000-0000-0000-000000000002'
FOREIGN_SHIP = '30000000-0000-0000-0000-000000000003'
DAILY_SOURCE = '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2128-2185:v46.3'
FAMILIES = {'Noon': ('Master', 'position_lat'), 'Deck': ('ChiefOfficer', 'handover_note'), 'Engine': ('ChiefEngineer', 'alarms')}
WRITER_PERMISSIONS = ('equipment.read', 'equipment.write', 'equipment.history.read', 'maintenance.read',
    'maintenance.write', 'maintenance.history.read', 'maintenance.owner.eligible', 'certificate.read',
    'certificate.write', 'certificate.renewal.write', 'certificate.extract', 'defect.read', 'defect.write', 'workorder.read',
    'workorder.write', 'operations.fuel.read', 'operations.fuel.write', 'operations.port.read',
    'operations.port.write', 'operations.downtime.read', 'operations.downtime.write')


def scoped_id(run_id, lane, kind, purpose):
    # Same exact IDs consumed by signed daily fixture; not a runtime identity claim.
    value = hashlib.sha256(f'poseidon.daily.signed-fixture.v1:{run_id}:{lane}:{kind}:{purpose}'.encode()).hexdigest()
    return str(uuid.UUID(value[:32]))


def _uuid(value):
    try:
        if str(uuid.UUID(value)) != value or uuid.UUID(value).int == 0:
            raise ValueError()
    except (ValueError, TypeError, AttributeError):
        raise RuntimeError('bundle_fixture_uuid_invalid') from None
    return value


def configure(config, realm, authority, project, run_id, now=None):
    """Return a validated copy and metadata plan for a fresh disposable provider.

    Actual login and HTTP capability checks remain mandatory after provisioning.
    Existing appointments/offline policies are rejected rather than overwritten.
    """
    _uuid(run_id)
    now = now or datetime.now(timezone.utc)
    if now.tzinfo is None or now.utcoffset() != timedelta(0):
        raise RuntimeError('bundle_fixture_utc_required')
    candidate = deepcopy(config)
    federation = candidate.get('Authentication', {}).get('Oidc', {})
    if (realm.get('realm') != 'poseidon-lab' or authority != 'https://localhost:8443/realms/poseidon-lab'
            or federation.get('Authority') != authority or federation.get('Issuer') != authority
            or federation.get('ClientId') != 'poseidon-local'):
        raise RuntimeError('bundle_fixture_provider_invalid')
    actors = {}
    for name in ('lab-manager', 'lab-reviewer', 'lab-restricted', 'lab-foreign'):
        users = [u for u in realm.get('users', []) if u.get('username') == name]
        if len(users) != 1 or users[0].get('enabled') is not True:
            raise RuntimeError('bundle_fixture_provider_identity_required')
        matches = [m for m in federation.get('Memberships', []) if m.get('Issuer') == authority and m.get('Subject') == users[0].get('id')]
        expected_tenant = FOREIGN_TENANT if name == 'lab-foreign' else TENANT
        if len(matches) != 1 or matches[0].get('TenantId') != expected_tenant:
            raise RuntimeError('bundle_fixture_membership_invalid')
        member = matches[0]
        _uuid(member.get('ActorId'))
        if not isinstance(member.get('Permissions'), list) or any(not isinstance(p, str) for p in member['Permissions']):
            raise RuntimeError('bundle_fixture_permissions_invalid')
        actors[name] = member
    if len({m['ActorId'] for m in actors.values()}) != 4:
        raise RuntimeError('bundle_fixture_actor_collision')
    writer, reviewer, restricted, foreign = [actors[name] for name in ('lab-manager', 'lab-reviewer', 'lab-restricted', 'lab-foreign')]
    if writer.get('TenantWideManage') is not True or writer.get('TenantWideRead') is not True:
        raise RuntimeError('bundle_fixture_writer_scope_invalid')
    if (restricted.get('TenantWideManage') or restricted.get('TenantWideRead') or
            foreign.get('TenantWideManage') or foreign.get('TenantWideRead')):
        raise RuntimeError('bundle_fixture_restricted_scope_invalid')
    if candidate.get('DailyOffline', {}).get('Policies') or reviewer.get('DailyReviewAuthorities'):
        raise RuntimeError('bundle_fixture_existing_policy_preserved')
    for member in (writer, reviewer, foreign):
        if member.get('ActorKind') not in (None, 'Unspecified', 'Human'):
            raise RuntimeError('bundle_fixture_actor_kind_preserved')
        member['ActorKind'] = 'Human'
        role = str(uuid.uuid5(uuid.NAMESPACE_URL, project + '/source-bundle/' + member['ActorId']))
        refs = member.setdefault('RoleReferences', [])
        if not isinstance(refs, list) or len(refs) > 15 or len(set(refs)) != len(refs):
            raise RuntimeError('bundle_fixture_role_conflict')
        for ref in refs:
            _uuid(ref)
        if not refs:
            refs.append(role)
        for kind in FAMILIES:
            for action in ('read', 'write'):
                permission = 'daily.' + kind.lower() + '.' + action
                if permission not in member['Permissions']:
                    member['Permissions'].append(permission)
    for permission in WRITER_PERMISSIONS:
        if permission not in writer['Permissions']:
            writer['Permissions'].append(permission)
    reviewer_role = reviewer['RoleReferences'][0]
    grants = reviewer.setdefault('VesselGrants', [])
    existing = [g for g in grants if g.get('VesselId') == SHIP]
    if len(existing) > 1:
        raise RuntimeError('bundle_fixture_vessel_grant_conflict')
    if existing:
        existing[0]['CanEdit'] = True
    else:
        grants.append({'VesselId': SHIP, 'CanEdit': True})
    authorities = []
    for kind, (duty, _) in FAMILIES.items():
        permission = 'daily.' + kind.lower() + '.review'
        if permission not in reviewer['Permissions']:
            reviewer['Permissions'].append(permission)
        for lane in ('desktop', 'narrow'):
            authorities.append({'Id': scoped_id(run_id, lane, kind, 'review-authority'),
                'VesselId': SHIP, 'RecordId': scoped_id(run_id, lane, kind, 'review'), 'Kind': kind,
                'RoleId': reviewer_role, 'Duty': duty, 'SourceVersion': DAILY_SOURCE,
                'AppointmentSource': 'FICTIONAL disposable software review fixture; no operational appointment',
                'ValidFrom': (now-timedelta(minutes=1)).isoformat(), 'ValidUntil': (now+timedelta(hours=8)).isoformat()})
    reviewer['DailyReviewAuthorities'] = authorities
    policies = []
    for kind, (_, field) in FAMILIES.items():
        policies.append({'Kind': kind, 'Version': 'SYNTHETIC-BUNDLE-'+run_id, 'SourceVersion': DAILY_SOURCE,
            'ResourcePolicy': 'SelectedExistingOrNewDraft', 'StoragePolicyReference': 'SYNTHETIC IndexedDB software test only',
            'KeyPolicyReference': 'SYNTHETIC WebCrypto software test only', 'RetentionPolicyReference': 'SYNTHETIC eight hour lab retention',
            'RetentionSeconds': 28800, 'LeaseSeconds': 3600, 'NotBefore': (now-timedelta(minutes=1)).isoformat(),
            'ExpiresAt': (now+timedelta(hours=8)).isoformat(), 'MaxEnrollments': 16, 'MaxRecords': 8,
            'MaxEncryptedBytes': 262144, 'MaxDeviceRecords': 32, 'MaxDeviceEncryptedBytes': 1048576,
            'Fields': [{'Key': field, 'ClassificationReference': 'SYNTHETIC declaration test; not production classification',
                        'Purpose': 'Disposable offline behavior test', 'PrivacyClass': 'NonPersonalOperationalDraft'}],
            'Scopes': [{'TenantId': TENANT, 'ActorId': writer['ActorId'], 'VesselId': SHIP, 'Kind': kind}]})
    candidate['DailyOffline'] = {'Policies': policies}
    plan = {'schemaVersion': 'poseidon.bundle-fixture-plan.v1', 'runId': run_id, 'sourceVersion': DAILY_SOURCE,
        'tenantId': TENANT, 'vesselId': SHIP, 'foreignTenantId': FOREIGN_TENANT, 'foreignVesselId': FOREIGN_SHIP,
        'writerActorId': writer['ActorId'], 'reviewerActorId': reviewer['ActorId'],
        'restrictedActorId': restricted['ActorId'], 'foreignActorId': foreign['ActorId'],
        'reviewerAccount': 'lab-reviewer', 'foreignAccount': 'lab-foreign',
        'state': 'CONFIGURATION_ONLY_REQUIRES_REAL_LOGIN_AND_HTTP', 'extraction': 'UNCONFIGURED_REQUIRES_EXPLICIT_PROTOCOL_PEER'}
    return candidate, plan
