"""QUALIFIED-OWNING-SIGNED-FIXTURE-01: pure opt-in FICTIONAL lab inputs.

No provider identity, credential, outcome, receipt, decision or verified row is
created here. Observed IDs must come from the ordinary authenticated HTTP path.
"""
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import uuid
import re

import bundle_fixture_plan as base

SCHEMA = 'poseidon.qualified-owning.plan.v1'
TUG = '30000000-0000-0000-0000-000000000002'
MODES = ('Ship', 'Tug', 'Compliance')
WITNESSES = {'shipCargo': ('Ship', 'cargoPlan'), 'tugService': ('Tug', 'serviceAuthorization'),
             'tugOperation': ('Tug', 'operation'), 'tugWinch': ('Tug', 'winchFitment'),
             'shipClass': ('Compliance', 'classPack'), 'shipMsmd': ('Compliance', 'msmd')}
PLAN_KEYS = {'schemaVersion', 'runId', 'tenantId', 'shipId', 'tugId', 'writerActorId',
             'reviewerActorId', 'writerRoleId', 'reviewerRoleId', 'positionId', 'notBefore', 'expiresAt'}
RULE_KEYS = {'id', 'revision', 'sourceId', 'sourceRevision', 'requirementId', 'requirementRevision'}
WITNESS_KEYS = {'vesselId', 'id', 'revision', 'controlId', 'controlRevision', 'operationId', 'baselineEvaluationId'}
PERMISSIONS = ('directory.write', 'regulatory.draft.read', 'applicability.read', 'control.read', 'evidence.read', 'mode.ship',
               'file.read', 'equipment.read', 'mode.tug', 'mode.cross', 'tug.read', 'crew.read',
               'human.review.read')


def invalid():
    raise RuntimeError('qualified_fixture_contract_invalid')


def exact(value, expected):
    """Closed JSON comparison: bool/float must never stand in for an integer."""
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
        result = datetime.fromisoformat(value)
    except ValueError:
        invalid()
    if result.tzinfo is None or result.utcoffset() != timedelta(0):
        invalid()
    return result


def validate(plan):
    if not isinstance(plan, dict) or set(plan) != PLAN_KEYS or plan['schemaVersion'] != SCHEMA:
        invalid()
    for key in PLAN_KEYS - {'schemaVersion', 'notBefore', 'expiresAt'}:
        guid(plan[key])
    if (plan['tenantId'] != base.TENANT or plan['shipId'] == plan['tugId']
            or any(plan[key] in (base.SHIP, TUG, base.FOREIGN_SHIP) for key in ('shipId', 'tugId'))
            or plan['writerActorId'] == plan['reviewerActorId']
            or plan['writerRoleId'] == plan['reviewerRoleId']
            or plan['positionId'] != str(uuid.uuid5(uuid.UUID(plan['runId']), 'qualified-owning/v1/position'))
            or not timedelta(0) < instant(plan['expiresAt']) - instant(plan['notBefore']) <= timedelta(hours=9)):
        invalid()
    return plan


def vessel_code(run_id, kind):
    guid(run_id)
    if kind not in ('Ship', 'Tug'):
        invalid()
    return 'ZZZ-FICTIONAL-QOF-'+kind.upper()+'-'+run_id.replace('-', '')


def validate_vessels(metadata, value):
    keys = {'schemaVersion', 'runId', 'tenantId', 'writerActorId', 'vessels'}
    if (not isinstance(value, dict) or set(value) != keys or value['schemaVersion'] != 'poseidon.qualified-owning.observed-vessels.v1'
            or any(value[k] != metadata.get(k) for k in ('runId', 'tenantId', 'writerActorId'))
            or value['tenantId'] != base.TENANT or not isinstance(value['vessels'], dict) or set(value['vessels']) != {'Ship', 'Tug'}):
        invalid()
    for key in ('runId', 'tenantId', 'writerActorId'):
        guid(value[key])
    for kind, row in value['vessels'].items():
        if not isinstance(row, dict) or set(row) != {'id', 'companyId', 'kind', 'code', 'version', 'profileRevision', 'profileVersion', 'profileContentSha256'}:
            invalid()
        guid(row['id']); guid(row['companyId'])
        if (row['id'] in (base.SHIP, TUG, base.FOREIGN_SHIP) or row['kind'] != kind or row['code'] != vessel_code(value['runId'], kind)
                or any(type(row[k]) is not int or not 1 <= row[k] <= (9007199254740991 if k == 'profileRevision' else 4294967295) for k in ('version', 'profileRevision', 'profileVersion'))
                or not isinstance(row['profileContentSha256'], str) or len(row['profileContentSha256']) != 64 or any(c not in 'ABCDEF0123456789' for c in row['profileContentSha256'])):
            invalid()
    ship, tug = value['vessels']['Ship'], value['vessels']['Tug']
    if ship['id'] == tug['id'] or ship['companyId'] != tug['companyId']:
        invalid()
    return value


def create(metadata, vessels, now=None):
    if metadata.get('schemaVersion') != 'poseidon.bundle-fixture-plan.v1' or metadata.get('tenantId') != base.TENANT or metadata.get('vesselId') != base.SHIP:
        invalid()
    for key in ('runId', 'writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId'):
        guid(metadata.get(key))
    validate_vessels(metadata, vessels)
    now = now or datetime.now(timezone.utc)
    if now.tzinfo is None or now.utcoffset() != timedelta(0):
        invalid()
    return validate({'schemaVersion': SCHEMA, 'runId': metadata['runId'], 'tenantId': base.TENANT,
        'shipId': vessels['vessels']['Ship']['id'], 'tugId': vessels['vessels']['Tug']['id'], 'positionId': str(uuid.uuid5(uuid.UUID(metadata['runId']), 'qualified-owning/v1/position')), **{key: metadata[key] for key in
            ('writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId')},
        'notBefore': (now-timedelta(minutes=1)).isoformat(), 'expiresAt': (now+timedelta(hours=8)).isoformat()})


def identifier(plan, purpose):
    validate(plan)
    return str(uuid.uuid5(uuid.UUID(plan['runId']), 'qualified-owning/v1/'+purpose))


def _observed(plan, value, kind):
    names = MODES if kind == 'sources' else WITNESSES
    collection = 'rules' if kind == 'sources' else 'witnesses'
    if (not isinstance(value, dict) or set(value) != {'schemaVersion', 'runId', collection}
            or value['schemaVersion'] != 'poseidon.qualified-owning.observed-'+kind+'.v1'
            or value['runId'] != plan['runId'] or not isinstance(value[collection], dict)
            or set(value[collection]) != set(names)):
        invalid()
    used = set()
    for name, row in value[collection].items():
        keys = RULE_KEYS if kind == 'sources' else WITNESS_KEYS
        if not isinstance(row, dict) or set(row) != keys:
            invalid()
        for key in keys:
            if key.endswith('Revision') or key == 'revision':
                if type(row[key]) is not int or not 1 <= row[key] <= 9007199254740991:
                    invalid()
            else:
                guid(row[key])
        if row['id'] in used:
            invalid()
        used.add(row['id'])
        if kind == 'sources':
            for key in ('sourceId', 'requirementId'):
                if row[key] in used:
                    invalid()
                used.add(row[key])
        elif row['vesselId'] != (plan['tugId'] if WITNESSES[name][0] == 'Tug' else plan['shipId']):
            invalid()
    if kind == 'witnesses':
        rows = value[collection]
        for mode in MODES:
            selected = [rows[name] for name in rows if WITNESSES[name][0] == mode]
            if len({(r['controlId'], r['controlRevision'], r['baselineEvaluationId']) for r in selected}) != 1:
                invalid()
        if rows['tugService']['operationId'] != rows['tugOperation']['operationId']:
            invalid()
    return value[collection]


def configure(config, metadata, plan, sources=None, witnesses=None, now=None, vessels=None):
    """Append exact run-owned appointments; never replace unrelated policy/grants."""
    validate(plan)
    validate_vessels(metadata, vessels)
    if plan['shipId'] != vessels['vessels']['Ship']['id'] or plan['tugId'] != vessels['vessels']['Tug']['id']:
        invalid()
    expected = {**{key: plan[key] for key in ('runId', 'tenantId', 'writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId')}, 'vesselId': base.SHIP}
    if metadata.get('schemaVersion') != 'poseidon.bundle-fixture-plan.v1' or any(metadata.get(k) != v for k, v in expected.items()):
        invalid()
    now = now or datetime.now(timezone.utc)
    if now.tzinfo is None or not instant(plan['notBefore']) <= now < instant(plan['expiresAt']):
        invalid()
    candidate = deepcopy(config)
    selected = {}
    memberships = candidate.get('Authentication', {}).get('Oidc', {}).get('Memberships', [])
    if not isinstance(memberships, list) or any(not isinstance(m, dict) for m in memberships):
        invalid()
    for name in ('writer', 'reviewer'):
        matches = [m for m in memberships if m.get('ActorId') == plan[name+'ActorId']]
        if len(matches) != 1:
            invalid()
        member = matches[0]
        roles = member.get('RoleReferences')
        if not isinstance(roles, list) or not 1 <= len(roles) <= 16:
            invalid()
        for role in roles:
            guid(role)
        if len(set(roles)) != len(roles):
            invalid()
        if (member.get('TenantId') != plan['tenantId'] or member.get('ActorKind') != 'Human'
                or plan[name+'RoleId'] not in roles
                or not isinstance(member.get('Permissions'), list) or any(not isinstance(p, str) for p in member['Permissions']) or 'directory.read' not in member['Permissions']
                or not (member.get('TenantWideRead') is True or member.get('TenantWideManage') is True)):
            invalid()
        selected[name] = member
        extra = ('control.write', 'control.owner.eligible', 'evidence.write', 'file.write', 'equipment.write',
                 'tug.write', 'tug.plan.write', 'tug.owner.eligible', 'applicability.evaluate', 'regulatory.qualify', 'human.review.submit', 'crew.write', 'crew.position.write') if name == 'writer' else (
                 'regulatory.draft.write', 'rules.write', 'regulatory.qualify', 'human.verify')
        for permission in (*PERMISSIONS, *extra):
            if permission not in member['Permissions']:
                member['Permissions'].append(permission)
        grants = member.setdefault('VesselGrants', [])
        if not isinstance(grants, list) or any(not isinstance(g, dict) for g in grants):
            invalid()
        for vessel in (plan['shipId'], plan['tugId']):
            existing = [g for g in grants if g.get('VesselId') == vessel]
            if len(existing) > 1 or existing and existing[0].get('CanEdit') is not True:
                invalid()
            if not existing:
                grants.append({'VesselId': vessel, 'CanEdit': True})
    policy = {'Version': 'SYNTHETIC-QOF-'+plan['runId'], 'SourceVersion': '14.02:2316:1932-1957:v46.3',
        'NotBefore': plan['notBefore'], 'ExpiresAt': plan['expiresAt'],
        'Scopes': [{'TenantId': plan['tenantId'], 'ActorId': plan['writerActorId'], 'VesselId': plan['tugId'], 'EligibleTugs': [plan['tugId']]}],
        'Types': [{'Code': 'FICTIONAL-QOF', 'Tr': 'Kurgusal yazılım testi', 'En': 'Fictional software test'}],
        'Contexts': [{'TenantId': plan['tenantId'], 'VesselId': plan['tugId'], 'Code': 'FICTIONAL-QOF', 'SourceReference': 'FICTIONAL/'+plan['runId'], 'SourceRevision': 1, 'ObservedAt': plan['notBefore']}], 'BpSources': []}
    if not any(exact(candidate.get('TugPlanning'), allowed) for allowed in (None, {}, policy)):
        invalid()
    candidate['TugPlanning'] = policy
    position = {'Version': 'SYNTHETIC-QOF-'+plan['runId'], 'SourceReference': 'FICTIONAL/qualified-owning/'+plan['runId'],
        'TimeZone': 'UTC', 'PeriodSemantics': 'StartInclusiveEndExclusive', 'Positions': [{
            'TenantId': plan['tenantId'], 'VesselId': plan['shipId'], 'PositionId': plan['positionId'],
            'Code': 'FICTIONAL-QOF', 'Rank': None, 'Capacity': None, 'Department': None,
            'Provenance': 'UserDeclaredUnverified'}]}
    if not any(exact(candidate.get('CrewPositionPolicy'), allowed) for allowed in (None, {}, position)):
        invalid()
    candidate['CrewPositionPolicy'] = position

    def append(member, field, grant, match_scope):
        rows = member.setdefault(field, [])
        if not isinstance(rows, list) or any(not isinstance(g, dict) for g in rows):
            invalid()
        matches = [g for g in rows if g.get('Id') == grant['Id'] or match_scope(g)]
        if matches:
            if len(matches) != 1 or not exact(matches[0], grant):
                invalid()
        else:
            rows.append(grant)

    if witnesses is not None and sources is None:
        invalid()
    if sources is not None:
        rules = _observed(plan, sources, 'sources')
        for mode, rule in rules.items():
            for name, actions in (('reviewer', ['draft', 'review', 'own']), ('writer', ['qualify'])):
                grant = {'Id': identifier(plan, 'source/'+mode+'/'+name), 'RuleId': rule['id'], 'SourceId': rule['sourceId'],
                    'SourceRevision': rule['sourceRevision'], 'RoleId': plan[name+'RoleId'], 'Actions': actions,
                    'ValidFrom': plan['notBefore'], 'ValidUntil': plan['expiresAt'],
                    'Contract': {'SourceLocator': 'FICTIONAL/qualified-owning/'+plan['runId']+'/'+mode, 'Type': 'FICTIONAL_DECLARATION'}}
                append(selected[name], 'RegulatoryAuthorities', grant, lambda g: g.get('RuleId') == rule['id'])
    if witnesses is not None:
        for key, row in _observed(plan, witnesses, 'witnesses').items():
            for name, capability in (('writer', 'human.review.submit'), ('reviewer', 'human.verify')):
                grant = {'Id': identifier(plan, 'verification/'+key+'/'+name), 'VesselId': row['vesselId'],
                    'EvidenceId': row['id'], 'ControlId': row['controlId'], 'OperationId': row['operationId'],
                    'RoleId': plan[name+'RoleId'], 'Capability': capability,
                    'ValidFrom': plan['notBefore'], 'ValidUntil': plan['expiresAt']}
                append(selected[name], 'HumanAuthorities', grant, lambda g: g.get('EvidenceId') == row['id'] and g.get('Capability') == capability)
    return candidate


def validate_descriptor(plan, value, now=None):
    """Closed additive references only; actual HTTP currentness is checked in TS."""
    validate(plan)
    keys = {'schemaVersion', 'runId', 'tenantId', 'writerActorId', 'reviewerActorId', 'shipId', 'tugId', 'expiresAt', 'vessels', 'rules', 'witnesses'}
    if (not isinstance(value, dict) or set(value) != keys or value['schemaVersion'] != 'poseidon.qualified-owning.signed-fixture.v1'
            or any(value[k] != plan[k] for k in keys - {'schemaVersion', 'vessels', 'rules', 'witnesses'})
            or not isinstance(value['rules'], dict) or set(value['rules']) != set(MODES)
            or not isinstance(value['witnesses'], dict) or set(value['witnesses']) != set(WITNESSES)):
        invalid()
    validate_vessels(plan, {'schemaVersion': 'poseidon.qualified-owning.observed-vessels.v1', 'runId': plan['runId'], 'tenantId': plan['tenantId'],
        'writerActorId': plan['writerActorId'], 'vessels': value['vessels']})
    if value['vessels']['Ship']['id'] != plan['shipId'] or value['vessels']['Tug']['id'] != plan['tugId']:
        invalid()
    now = now or datetime.now(timezone.utc)
    if now.tzinfo is None or not instant(plan['notBefore']) <= now < instant(plan['expiresAt']):
        invalid()
    source_rows = {}
    witness_rows = {}
    used = set()
    def distinct(identifier):
        guid(identifier)
        if identifier in used:
            invalid()
        used.add(identifier)
    def checksum(value):
        if not isinstance(value, str) or len(value) != 64 or any(c not in 'ABCDEF0123456789' for c in value):
            invalid()
    for mode, row in value['rules'].items():
        if not isinstance(row, dict) or set(row) != RULE_KEYS | {'qualificationRevision', 'qualificationHash', 'baselineEvaluationId'}:
            invalid()
        if type(row['qualificationRevision']) is not int or not 1 <= row['qualificationRevision'] <= 9007199254740991:
            invalid()
        checksum(row['qualificationHash'])
        for key in ('id', 'sourceId', 'requirementId', 'baselineEvaluationId'):
            distinct(row[key])
        source_rows[mode] = {key: row[key] for key in RULE_KEYS}
    for name, row in value['witnesses'].items():
        if not isinstance(row, dict) or set(row) != WITNESS_KEYS | {'hash', 'fileId', 'fileRevision', 'fileChecksum', 'verificationId', 'verificationRevision'}:
            invalid()
        for key in ('fileRevision', 'verificationRevision'):
            if type(row[key]) is not int or not 1 <= row[key] <= 9007199254740991:
                invalid()
        for key in ('hash', 'fileChecksum'):
            checksum(row[key])
        for key in ('id', 'fileId', 'verificationId'):
            distinct(row[key])
        if row['baselineEvaluationId'] != value['rules'][WITNESSES[name][0]]['baselineEvaluationId']:
            invalid()
        witness_rows[name] = {key: row[key] for key in WITNESS_KEYS}
    _observed(plan, {'schemaVersion': 'poseidon.qualified-owning.observed-sources.v1', 'runId': plan['runId'], 'rules': source_rows}, 'sources')
    _observed(plan, {'schemaVersion': 'poseidon.qualified-owning.observed-witnesses.v1', 'runId': plan['runId'], 'witnesses': witness_rows}, 'witnesses')
    return value
