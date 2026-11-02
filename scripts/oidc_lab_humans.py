"""Explicit synthetic multi-human fixture setup for the isolated signed OIDC lab.

No token/session generation, real source approval, production authority or output.
The caller must use the existing private lab provider lifecycle and real login.
"""
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import uuid

TENANT = '10000000-0000-0000-0000-000000000001'
SHIP = '30000000-0000-0000-0000-000000000001'
SOURCE = 'FICTIONAL signed-host DEV-047 catalog; software fixture only'


def catalog_membership(config, realm, authority, project, now=None):
    """Return a fully validated copy. Never replace an operator appointment/catalog.

    Existing two-account private labs need explicit real-provider reviewer setup;
    this routine cannot invent a provider user or convert the restricted account.
    """
    candidate = deepcopy(config)
    now = now or datetime.now(timezone.utc)
    federation = candidate.get('Authentication', {}).get('Oidc', {})
    if (realm.get('realm') != 'poseidon-lab' or authority != 'https://localhost:8443/realms/poseidon-lab' or
            federation.get('Authority') != authority or federation.get('Issuer') != authority or
            federation.get('ClientId') != 'poseidon-local'):
        raise RuntimeError('lab_human_provider_invalid')
    members = federation.get('Memberships', [])
    selected = []
    for name in ('lab-manager', 'lab-reviewer'):
        users = [u for u in realm.get('users', []) if u.get('username') == name]
        if (len(users) != 1 or users[0].get('enabled') is not True or not users[0].get('id')):
            raise RuntimeError('lab_human_real_provider_setup_required')
        found = [m for m in members if m.get('Subject') == users[0]['id'] and m.get('Issuer') == authority]
        if len(found) != 1 or found[0].get('TenantId') != TENANT:
            raise RuntimeError('lab_human_membership_invalid')
        selected.append(found[0])
    if (selected[0].get('ActorId') == selected[1].get('ActorId') or
            any(not m.get('ActorId') or sum(n.get('ActorId') == m['ActorId'] and n.get('TenantId') == TENANT for n in members) != 1 for m in selected)):
        raise RuntimeError('lab_human_actor_not_distinct')
    for member in selected:
        role = str(uuid.uuid5(uuid.NAMESPACE_URL, project + '/synthetic-review-assignment'))
        refs = member.setdefault('RoleReferences', [])
        if (not isinstance(refs, list) or len(refs) > 16 or role not in refs and len(refs) == 16 or len(set(refs)) != len(refs) or
                any(not isinstance(r, str) or str(uuid.UUID(r)) != r for r in refs)):
            raise RuntimeError('lab_human_roles_invalid')
        if role not in refs:
            refs.append(role)
        if member.get('ActorKind') not in (None, 'Unspecified', 'Human'):
            raise RuntimeError('lab_human_operator_kind_preserved')
        member['ActorKind'] = 'Human'
        permissions = member.setdefault('Permissions', [])
        if not isinstance(permissions, list):
            raise RuntimeError('lab_human_permissions_invalid')
        for permission in ('directory.read', 'directory.write', 'crew.read', 'crew.write', 'crew.history.read', 'crew.catalog.govern'):
            if permission not in permissions:
                permissions.append(permission)
        # Reviewer stays vessel-scoped; writer's existing wider grant is preserved.
        grants = member.setdefault('VesselGrants', [])
        matches = [g for g in grants if g.get('VesselId') == SHIP]
        if len(matches) > 1:
            raise RuntimeError('lab_human_scope_conflict')
        if not matches:
            grants.append({'VesselId': SHIP, 'CanEdit': True})
        else:
            matches[0]['CanEdit'] = True  # Explicit disposable fixture enrollment only.
        authorities = member.setdefault('CrewCatalogAuthorities', [])
        existing = [g for g in authorities if g.get('VesselId') == SHIP]
        identifier = str(uuid.uuid5(uuid.NAMESPACE_URL, project + '/catalog/' + member['ActorId']))
        if existing:
            if (len(existing) != 1 or existing[0].get('Id') != identifier or existing[0].get('SourceReference') != SOURCE or
                    existing[0].get('RoleId') != role or existing[0].get('Actions') != ['draft', 'review', 'adopt', 'withdraw']):
                raise RuntimeError('lab_human_operator_authority_preserved')
            # Do not silently renew an expired appointment. The operator must explicitly reconcile it.
            if datetime.fromisoformat(existing[0]['Until'].replace('Z', '+00:00')) <= now:
                raise RuntimeError('lab_human_appointment_expired')
        else:
            authorities.append({'Id': identifier, 'VesselId': SHIP, 'RoleId': role, 'SourceReference': SOURCE,
                                'Actions': ['draft', 'review', 'adopt', 'withdraw'],
                                'From': (now - timedelta(minutes=1)).isoformat(), 'Until': (now + timedelta(hours=8)).isoformat()})
    return candidate
