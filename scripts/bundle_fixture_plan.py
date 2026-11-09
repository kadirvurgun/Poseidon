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
    'operations.port.write', 'operations.downtime.read', 'operations.downtime.write', 'control.read', 'control.write',
    'evidence.read', 'evidence.write', 'applicability.read', 'applicability.evaluate', 'rules.write',
    'rules.review', 'regulatory.qualify', 'human.review.read', 'human.decide', 'import.read', 'import.write',
    'import.legacy.convert', 'crew.identity.convert')


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
    # Tenant-wide regulatory source authoring is explicit to this fresh synthetic reviewer.
    reviewer['TenantWideRead'] = True
    for permission in ('regulatory.draft.read', 'regulatory.draft.write', 'applicability.read', 'rules.write', 'regulatory.qualify'):
        if permission not in reviewer['Permissions']:
            reviewer['Permissions'].append(permission)
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
        'reviewerRoleId': reviewer_role, 'writerRoleId': writer['RoleReferences'][0],
        'legacyBatches': {lane: str(uuid.uuid4()) for lane in ('desktop', 'narrow')},
        'state': 'CONFIGURATION_ONLY_REQUIRES_REAL_LOGIN_AND_HTTP', 'extraction': 'UNCONFIGURED_REQUIRES_EXPLICIT_PROTOCOL_PEER'}
    return candidate, plan


def configure_observed(config, metadata, observed, now=None):
    """Exact-object synthetic recording/source-review appointments after real HTTP authoring.

    Only metadata from the disposable real API provisioner is accepted. This
    records no decisions/verification and never installs a production catalog.
    """
    now=now or datetime.now(timezone.utc)
    if now.tzinfo is None or now.utcoffset()!=timedelta(0):raise RuntimeError('bundle_fixture_utc_required')
    if (metadata.get('tenantId')!=TENANT or metadata.get('vesselId')!=SHIP or observed.get('schemaVersion')!='poseidon.bundle-observed-sources.v1' or
        observed.get('runId')!=metadata.get('runId') or set(observed.get('lanes',{}))!={'desktop','narrow'}):
        raise RuntimeError('bundle_observed_scope_invalid')
    candidate=deepcopy(config);members=candidate['Authentication']['Oidc']['Memberships']
    selected={}
    for name,actor in (('writer',metadata['writerActorId']),('reviewer',metadata['reviewerActorId'])):
        matches=[m for m in members if m.get('ActorId')==actor and m.get('TenantId')==TENANT and m.get('ActorKind')=='Human']
        if len(matches)!=1:raise RuntimeError('bundle_observed_actor_invalid')
        expected_role=metadata[name+'RoleId']
        _uuid(expected_role)
        if expected_role not in (matches[0].get('RoleReferences') or []):raise RuntimeError('bundle_observed_role_invalid')
        selected[name]=matches[0]
        if matches[0].get('RegulatoryAuthorities') or name=='writer' and matches[0].get('HumanAuthorities'):
            raise RuntimeError('bundle_observed_existing_appointments_preserved')
        matches[0]['RegulatoryAuthorities']=[]
    writer=selected['writer'];reviewer=selected['reviewer'];writer['HumanAuthorities']=[]
    from_at=(now-timedelta(minutes=1)).isoformat();until=(now+timedelta(hours=8)).isoformat()
    contract={'SourceLocator':'FICTIONAL/bundle/source-contract; software test only','Type':'FICTIONAL_DECLARATION'}
    ids=set()
    for lane,values in observed['lanes'].items():
        if not isinstance(values,dict) or set(values)!={'noEvaluationVesselId','control','evidence','rule'}:
            raise RuntimeError('bundle_observed_shape_invalid')
        for key in ('noEvaluationVesselId','control','evidence','rule'):
            value=values.get(key)
            identifier=value.get('id') if isinstance(value,dict) else value
            _uuid(identifier)
            if identifier in ids:raise RuntimeError('bundle_observed_object_reused')
            ids.add(identifier)
        rule=values['rule'];_uuid(rule.get('sourceId'))
        if set(rule)!={'id','revision','version','sourceId','sourceRevision'} or any(set(values[k])!={'id','revision','version'} for k in ('control','evidence')):
            raise RuntimeError('bundle_observed_shape_invalid')
        if (type(rule.get('sourceRevision')) is not int or not 1<=rule['sourceRevision']<=9007199254740991 or
                any(type(values[k].get('revision')) is not int or not 1<=values[k]['revision']<=9007199254740991 or type(values[k].get('version')) is not int or not 1<=values[k]['version']<=4294967295 for k in ('control','evidence','rule'))):
            raise RuntimeError('bundle_observed_pin_invalid')
        evidence=values['evidence']['id'];control=values['control']['id'];role=metadata['writerRoleId']
        writer['HumanAuthorities'].append({'Id':str(uuid.uuid4()),'VesselId':SHIP,'EvidenceId':evidence,'ControlId':control,
            'OperationId':str(uuid.uuid4()),'RoleId':role,'Capability':'human.decide','ValidFrom':from_at,'ValidUntil':until,
            'Recording':{'DecisionType':'SYNTHETIC-BUNDLE-RECORDING','SubjectType':'Evidence','SubjectId':evidence,
                'AuthorityRole':'DPA','Semantics':'HumanRecording','SourceLocator':'15.01/paragraph:99;18.01/Backlog/row:32'}})
        for member,role,actions in ((reviewer,metadata['reviewerRoleId'],['draft','review','own']),(writer,metadata['writerRoleId'],['qualify'])):
            member['RegulatoryAuthorities'].append({'Id':str(uuid.uuid4()),'RuleId':rule['id'],'SourceId':rule['sourceId'],
                'SourceRevision':rule['sourceRevision'],'RoleId':role,'Actions':actions,'ValidFrom':from_at,'ValidUntil':until,'Contract':contract})
    return candidate


def renew_synthetic_window(config, realm, authority, project, metadata, now=None):
    """Renew only the exact run-owned daily software fixtures, never other grants.

    Long verification sessions can outlive the initial eight-hour lab window.
    Operator edits or a different run require manual reconciliation rather than
    extending them. Old immutable configuration generations remain retained.
    """
    current = deepcopy(config)
    members = current.get('Authentication', {}).get('Oidc', {}).get('Memberships', [])
    reviewers = [m for m in members if m.get('ActorId') == metadata.get('reviewerActorId') and m.get('TenantId') == TENANT]
    if len(reviewers) != 1 or not isinstance(current.get('DailyOffline'), dict):
        raise RuntimeError('bundle_fixture_window_scope_invalid')
    old_daily = deepcopy(current['DailyOffline'])
    old_review = deepcopy(reviewers[0].get('DailyReviewAuthorities'))
    if not isinstance(old_review, list):
        raise RuntimeError('bundle_fixture_window_scope_invalid')
    del current['DailyOffline']
    reviewers[0].pop('DailyReviewAuthorities', None)
    expected, expected_metadata = configure(current, realm, authority, project, metadata['runId'], now)
    if any(expected_metadata[key] != metadata.get(key) for key in
            ('tenantId','vesselId','writerActorId','reviewerActorId','reviewerRoleId','writerRoleId','sourceVersion')):
        raise RuntimeError('bundle_fixture_window_scope_invalid')
    expected_review = next(m['DailyReviewAuthorities'] for m in expected['Authentication']['Oidc']['Memberships']
        if m.get('ActorId') == metadata['reviewerActorId'])
    def without_dates(value, dates):
        return [{key: item for key,item in row.items() if key not in dates} for row in value]
    if (set(old_daily) != {'Policies'} or
            without_dates(old_daily.get('Policies', []), {'NotBefore','ExpiresAt'}) !=
            without_dates(expected['DailyOffline']['Policies'], {'NotBefore','ExpiresAt'}) or
            without_dates(old_review, {'ValidFrom','ValidUntil'}) != without_dates(expected_review, {'ValidFrom','ValidUntil'})):
        raise RuntimeError('bundle_fixture_operator_window_preserved')
    result = deepcopy(config)
    result['DailyOffline'] = expected['DailyOffline']
    next(m for m in result['Authentication']['Oidc']['Memberships'] if m.get('ActorId') == metadata['reviewerActorId'])['DailyReviewAuthorities'] = expected_review
    return result
