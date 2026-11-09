"""DEV-BUNDLE-FIXTURE-01 explicit fresh disposable source-bundle lab only.

Uses the established OIDC lifecycle without altering its source-reviewed file.
No existing operator profile is migrated, replaced, or silently granted rights.
"""
import argparse
import json
import os
from pathlib import Path
import secrets
import socket
import sys
import uuid

import oidc_lab as lab
import bundle_fixture_plan as plan

BASE_ENVIRONMENT = lab.environment
FIXTURE_NAMES = ('DAILY','CERTIFICATE','CERTIFICATE_EXTRACTION','SHIP_OPERATIONS','DEFECT','WORK_ORDER','DOWNTIME')
NAVIGATION_KEYS = ['noon-report','deck-daily-log','engine-daily-log','fuel-bunker-log','port-activity-sof',
    'operational-downtime','defects','work-order-center','certificate-command-center','renewal-workflow',
    'audit','import','sources','requirements']


def init_source_bundle():
    lab.stage('fresh_disposable_source_bundle')
    if lab.PRIVATE.exists() and any(lab.PRIVATE.iterdir()):
        raise RuntimeError('fresh_disposable_bundle_profile_required')
    lab.init()
    realm = json.loads((lab.PRIVATE/'realm/poseidon-lab-realm.json').read_text())
    credentials = json.loads((lab.PRIVATE/'credentials.json').read_text())
    config = json.loads((lab.PRIVATE/'appsettings.json').read_text())
    foreign_subject, foreign_actor = str(uuid.uuid4()),str(uuid.uuid4())
    credentials['lab-foreign']={'username':'lab-foreign','password':secrets.token_urlsafe(32)}
    realm['users'].append({'id':foreign_subject,'username':'lab-foreign','enabled':True,
        'firstName':'Synthetic','lastName':'Foreign','email':'lab-foreign@example.invalid','emailVerified':False,
        'credentials':[{'type':'password','value':credentials['lab-foreign']['password'],'temporary':False}]})
    config['Authentication']['Oidc']['Memberships'].append({'Issuer':lab.AUTHORITY,'Subject':foreign_subject,
        'ActorId':foreign_actor,'TenantId':plan.FOREIGN_TENANT,'Permissions':['directory.read','directory.write'],
        'TenantWideRead':False,'TenantWideManage':False,'VesselGrants':[{'VesselId':plan.FOREIGN_SHIP,'CanEdit':True}]})
    config=lab.navigation_membership(config,realm)
    config=lab.catalog_membership(config,realm,lab.AUTHORITY,lab.PROJECT)
    config,metadata=plan.configure(config,realm,lab.AUTHORITY,lab.PROJECT,str(uuid.uuid4()))
    daily=str(uuid.uuid5(uuid.NAMESPACE_URL,lab.PROJECT+'/synthetic-writer-assignment'))
    mappings=[row for row in config['WorkspaceNavigation']['Mappings'] if row['TenantId']==plan.TENANT and row['Reference']==daily]
    if len(mappings)!=1:
        raise RuntimeError('synthetic_navigation_mapping_invalid')
    mappings[0]['Keys']=list(dict.fromkeys([*mappings[0]['Keys'],*NAVIGATION_KEYS]))
    for name,value in [('realm/poseidon-lab-realm.json',realm),('credentials.json',credentials),
                       ('appsettings.json',config),('source-bundle-plan.json',metadata)]:
        lab.atomic_private_json(lab.PRIVATE/name,value)


def prepare_bundle():
    lab.stage('explicit_source_bundle_scanner_configuration')
    metadata=json.loads((lab.PRIVATE/'source-bundle-plan.json').read_text())
    if metadata.get('schemaVersion')!='poseidon.bundle-fixture-plan.v1':
        raise RuntimeError('source_bundle_plan_required')
    port=int(os.environ.get('POSEIDON_GATE_CLAMD_PORT','0'))
    if not 0<port<=65535:
        raise RuntimeError('real_scanner_port_required')
    with socket.create_connection(('127.0.0.1',port),timeout=2) as peer:
        peer.sendall(b'zPING\0')
        if peer.recv(5)!=b'PONG\0':
            raise RuntimeError('real_scanner_unready')
    path=lab.PRIVATE/'appsettings.json'
    config=lab.register_membership(json.loads(path.read_text()))
    members=[m for m in config['Authentication']['Oidc']['Memberships'] if m.get('ActorId')==metadata['writerActorId'] and m.get('TenantId')==metadata['tenantId']]
    if len(members)!=1:
        raise RuntimeError('source_bundle_writer_invalid')
    for permission in ('audit.read','file.read','file.write'):
        if permission not in members[0]['Permissions']:
            members[0]['Permissions'].append(permission)
    objects=lab.PRIVATE/'source-bundle-objects';lab.private_directory(objects)
    config['SecureFiles']={'Adapter':'LocalDevelopment','Root':str(objects),'MaxBytes':1048576,
        'MaxPixels':1048576,'ScanSeconds':15,'ClamdPort':port,'CapabilitySeconds':60,
        'RetentionReference':'synthetic-lab-only','ScannerReference':'pinned-ephemeral-clamd'}
    lab.atomic_private_json(path,config)


def environment():
    env=BASE_ENVIRONMENT()
    for key in list(env):
        normalized=key.lower().replace('__',':')
        for prefix in ('dotnet_','aspnetcore_'):
            if normalized.startswith(prefix):
                normalized=normalized[len(prefix):];break
        if any(normalized == section or normalized.startswith(section+':') for section in ('dailyoffline','certificateextraction','crewfilepolicy','crewprofilepolicy','crewidentitypolicy','crewpositionpolicy','crewassignmentpolicy','maintenanceobservations','tugplanning','notifications','notificationscheduler','offlineapplication')):
            del env[key]
    env.pop('POSEIDON_BUNDLE_EXTRACTION_SECRET', None)
    for name in FIXTURE_NAMES:
        env.pop('POSEIDON_BROWSER_'+name+'_FIXTURE', None)
    secret=lab.PRIVATE/'extraction-peer-secret'
    if (lab.PRIVATE/'source-bundle-plan.json').is_file() and secret.exists():
        if secret.is_symlink() or secret.stat().st_size>256:
            raise RuntimeError('synthetic_peer_secret_boundary')
        value=secret.read_text(encoding='utf-8')
        if not value or any(c not in 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_' for c in value):
            raise RuntimeError('synthetic_peer_secret_invalid')
        env['POSEIDON_BUNDLE_EXTRACTION_SECRET']=value
    env['POSEIDON_LAB_PRIVATE']=str(lab.PRIVATE)
    env['POSEIDON_LAB_PYTHON']=sys.executable
    env['NODE_EXTRA_CA_CERTS']=str(lab.PRIVATE/'ca.pem')
    env.pop('NODE_TLS_REJECT_UNAUTHORIZED',None)
    return env


def source_fixture_environment():
    path=lab.PRIVATE/'source-bundle-environment.json'
    if not path.exists():
        return {}
    if path.is_symlink() or path.stat().st_size>16384 or not path.resolve().is_relative_to(lab.PRIVATE.resolve()):
        raise RuntimeError('source_fixture_environment_boundary')
    values=json.loads(path.read_text(encoding='utf-8'))
    allowed={'POSEIDON_BROWSER_'+name+'_FIXTURE' for name in FIXTURE_NAMES}
    if not isinstance(values,dict) or set(values)!=allowed:
        raise RuntimeError('source_fixture_environment_incomplete')
    for value in values.values():
        if not isinstance(value,str):
            raise RuntimeError('source_fixture_environment_invalid')
        file=Path(value)
        if not file.is_absolute() or file.is_symlink() or not file.is_file() or not file.resolve().is_relative_to(lab.PRIVATE.resolve()) or file.stat().st_size>8192:
            raise RuntimeError('source_fixture_environment_boundary')
    return values


def prepare_source_fixtures():
    lab.stage('real_signed_source_fixture_preparation')
    if not (lab.PRIVATE/'source-bundle-plan.json').is_file():
        raise RuntimeError('fresh_disposable_bundle_plan_required')
    lab.run([lab.root_tool('node'),str(lab.ROOT/'scripts/bundle_fixture_provision.ts')],env=environment(),timeout=600)


def start():
    original=lab.environment
    try:
        lab.environment=environment
        lab.start()
    finally:
        lab.environment=original


def register_browser():
    lab.stage('real_provider_source_bundle_browser')
    env=environment();fixtures=source_fixture_environment()
    if len(fixtures)!=len(FIXTURE_NAMES):
        raise RuntimeError('source_fixture_environment_required')
    env.update(fixtures)
    lab.run([lab.root_tool('node'),str(lab.ROOT/'tests/Poseidon.Browser.Tests/lab/register-run.ts')],env=env,timeout=1800)


def stop():lab.stop()


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['init_source_bundle','prepare_bundle','prepare_source_fixtures','start','stop','register_browser'])
    args=parser.parse_args(argv)
    try:
        if sys.version_info[:3]!=(3,13,13):raise RuntimeError('pinned_python_required')
        globals()[args.action]()
        print('DEV-BUNDLE-FIXTURE-01 '+args.action+' COMPLETED; software fixture only')
        return 0
    except Exception:
        print('DEV-BUNDLE-FIXTURE-01 '+args.action+' FAILED at '+lab.STAGE+'; private state preserved')
        return 1


if __name__=='__main__':raise SystemExit(main())
