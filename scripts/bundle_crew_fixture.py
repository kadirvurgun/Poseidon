"""Explicit software-only privacy fixture for the bundle's legacy protection case.

Never installed from request data or a production operator catalog. Only the
fictional writer receives the complete legacy-conversion field scope; reviewer,
restricted and foreign memberships remain byte-equivalent.
"""
from copy import deepcopy
import re
import uuid

REFERENCE = 'POSEIDON_BUNDLE_CREW_FIXTURE_KEY'
FIELDS = ('Nationality', 'DateOfBirth', 'PassportNo', 'PassportExpiry', 'SeamanBookNo',
    'SeamanBookExpiry', 'CompanyStartDate', 'SeaServiceMonths', 'TankerExperienceMonths',
    'VesselTypeExperience', 'CompanyExperienceMonths', 'EmergencyContact',
    'PreferredLanguage', 'ContactPhone', 'Email', 'MedicalExpiry')


def configure(config, metadata):
    run = metadata.get('runId')
    try:
        if str(uuid.UUID(run)) != run or uuid.UUID(run).int == 0:
            raise ValueError()
    except (ValueError, TypeError, AttributeError):
        raise RuntimeError('bundle_crew_fixture_run_required') from None
    if (metadata.get('schemaVersion') != 'poseidon.bundle-fixture-plan.v1' or
            metadata.get('tenantId') != '10000000-0000-0000-0000-000000000001' or
            config.get('Authentication', {}).get('Oidc', {}).get('Issuer') != 'https://localhost:8443/realms/poseidon-lab'):
        raise RuntimeError('bundle_crew_fixture_scope_required')
    try:
        actor = metadata.get('writerActorId')
        if str(uuid.UUID(actor)) != actor or uuid.UUID(actor).int == 0:
            raise ValueError()
    except (ValueError, TypeError, AttributeError):
        raise RuntimeError('bundle_crew_fixture_writer_required') from None
    candidate = deepcopy(config)
    members = candidate['Authentication']['Oidc']['Memberships']
    matches = [m for m in members if m.get('ActorId') == metadata.get('writerActorId') and m.get('TenantId') == metadata['tenantId']]
    if len(matches) != 1 or matches[0].get('ActorKind') != 'Human':
        raise RuntimeError('bundle_crew_fixture_writer_required')
    writer = matches[0]
    version = 'SYNTHETIC-' + run
    personal = []
    for field in FIELDS:
        group = ('medical' if field == 'MedicalExpiry' else
            'contact' if field in ('EmergencyContact', 'PreferredLanguage', 'ContactPhone', 'Email') else
            'employment' if field in ('CompanyStartDate', 'SeaServiceMonths', 'TankerExperienceMonths', 'VesselTypeExperience', 'CompanyExperienceMonths') else 'identity')
        rule = {'Field': field, 'Purpose': 'crew-profile',
            'ReadCapability': 'crew.profile.' + group + '.read',
            'WriteCapability': 'crew.profile.' + group + '.write'}
        if field in ('Nationality', 'PreferredLanguage', 'VesselTypeExperience'):
            rule['ControlledValues'] = ['SYNTHETIC-UNVERIFIED']
        personal.append(rule)
    expected = {
        'CrewIdentityPolicy': {'Version': version, 'Purpose': 'synthetic-crew-identity',
            'SourceReference': 'FICTIONAL isolated bundle ' + run + '; no lawful-purpose approval',
            'EmployeeNumberMode': 'MutableDeclaration', 'TenantIds': [metadata['tenantId']],
            'Fields': [{'Field': field, 'Purpose': 'synthetic-crew-identity',
                'ReadCapability': 'crew.profile.identity.read', 'WriteCapability': 'crew.profile.identity.write'}
                for field in ('FullName', 'EmployeeNo')]},
        'CrewProfilePolicy': {'Version': version, 'ImportPurpose': 'crew-profile',
            'ActiveKeyId': 'synthetic-bundle-v1', 'KeyReferences': {'synthetic-bundle-v1': REFERENCE}, 'Fields': personal},
    }
    # Existing operator policies are not a template to extend, replace or repair.
    for key, value in expected.items():
        if key in candidate and candidate[key] != value:
            raise RuntimeError('bundle_crew_existing_policy_preserved')
    for key, value in expected.items():
        candidate[key] = value
    permissions = {'crew.read', 'crew.write', 'crew.identity.read', 'crew.identity.write',
        'crew.identity.convert', 'import.read', 'import.write', 'import.legacy.convert'}
    permissions.update(rule[action] for rule in personal for action in ('ReadCapability', 'WriteCapability'))
    if not isinstance(writer.get('Permissions'), list) or any(not isinstance(p, str) for p in writer['Permissions']):
        raise RuntimeError('bundle_crew_fixture_permissions_invalid')
    writer['Permissions'] = list(dict.fromkeys([*writer['Permissions'], *sorted(permissions)]))
    return candidate


def validate_key(raw):
    import base64
    if not isinstance(raw, str) or not re.fullmatch(r'[A-Za-z0-9+/]{43}=', raw):
        raise RuntimeError('bundle_crew_fixture_key_invalid')
    try:
        decoded = base64.b64decode(raw, validate=True)
    except ValueError:
        raise RuntimeError('bundle_crew_fixture_key_invalid') from None
    if len(decoded) != 32 or base64.b64encode(decoded).decode('ascii') != raw:
        raise RuntimeError('bundle_crew_fixture_key_invalid')
    return raw
