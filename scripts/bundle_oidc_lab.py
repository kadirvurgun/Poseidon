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
import bundle_private as private_io
import bundle_crew_fixture as crew_fixture
import bundle_crew_boundary_fixture as crew_boundary
import bundle_observation_fixture as observation_fixture
import lab_runtime
import bundle_qualified_fixture_plan as qualified_plan
import bundle_foundation_policy_fixture as foundation_policy
import bundle_object_root

BASE_ENVIRONMENT = lab.environment
FIXTURE_NAMES = ('DAILY','CERTIFICATE','CERTIFICATE_EXTRACTION','SHIP_OPERATIONS','DEFECT','WORK_ORDER','DOWNTIME','OBSERVATION')
NAVIGATION_KEYS = ['noon-report','deck-daily-log','engine-daily-log','fuel-bunker-log','port-activity-sof',
    'operational-downtime','defects','work-order-center','certificate-command-center','renewal-workflow',
    'audit','import','sources','requirements']


def init_source_bundle(app_port=None, provider_port=None, database_port=None):
    lab.stage('fresh_disposable_source_bundle')
    if lab.PRIVATE.exists() and any(lab.PRIVATE.iterdir()):
        raise RuntimeError('fresh_disposable_bundle_profile_required')
    run_id = str(uuid.uuid4())
    ports = (app_port, provider_port, database_port)
    if any(port is not None for port in ports) and any(port is None for port in ports):
        raise RuntimeError('owned_lab_all_ports_required')
    runtime = None if ports == (None, None, None) else lab_runtime.create(run_id, *ports)
    lab_runtime.preflight(runtime)
    lab.bind_runtime(runtime)
    lab.selected_compose_ownership()  # Reject retained previous-run services/volumes before genesis writes.
    lab.private_directory(lab.PRIVATE)
    (lab.PRIVATE/'realm').mkdir()
    realm,credentials,database_password,config=lab.make_configuration()
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
    config,metadata=plan.configure(config,realm,lab.AUTHORITY,lab.PROJECT,run_id)
    config,realm,credentials=crew_boundary.configure(config,realm,credentials,metadata)
    if runtime is not None:
        metadata['ownedRuntime'] = runtime
        private_io.publish_json(lab.PRIVATE/lab_runtime.FILE, runtime)
    daily=str(uuid.uuid5(uuid.NAMESPACE_URL,lab.PROJECT+'/synthetic-writer-assignment'))
    mappings=[row for row in config['WorkspaceNavigation']['Mappings'] if row['TenantId']==plan.TENANT and row['Reference']==daily]
    if len(mappings)!=1:
        raise RuntimeError('synthetic_navigation_mapping_invalid')
    mappings[0]['Keys']=list(dict.fromkeys([*mappings[0]['Keys'],*NAVIGATION_KEYS]))
    # Build all five identities before the first immutable genesis is published.
    # No generic three-identity ready marker is ever published first.
    for name,value in [('realm/poseidon-lab-realm.json',realm),('credentials.json',credentials),('appsettings.json',config)]:
        private_io.publish_json(lab.PRIVATE/name,value)
    private_io.publish_bytes(lab.PRIVATE/'database-password',database_password.encode('utf-8'))
    bootstrap={'KC_BOOTSTRAP_ADMIN_USERNAME':'lab-bootstrap','KC_BOOTSTRAP_ADMIN_'+'PASSWORD':secrets.token_urlsafe(48)}
    private_io.publish_bytes(lab.PRIVATE/'provider.env',('\n'.join(key+'='+value for key,value in bootstrap.items())+'\n').encode())
    private_io.publish_config(config)
    lab.stage('source_bundle_certificate_generation')
    lab.run(['pwsh','-NoProfile','-File',str(lab.ROOT/'scripts/oidc-lab-certificates.ps1'),'-Directory',str(lab.PRIVATE)])
    if any(not (lab.PRIVATE/name).is_file() or (lab.PRIVATE/name).is_symlink() or (lab.PRIVATE/name).stat().st_size==0 for name in ('ca.pem','tls.pem','tls.key')):
        raise RuntimeError('source_bundle_tls_missing')
    private_io.publish_json(lab.PRIVATE/'source-bundle-plan.json',metadata)
    private_io.publish_bytes(lab.PRIVATE/'ready',b'DEV-OIDC-LAB-01\n')


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
    config,_=private_io.selected()
    config=lab.register_membership(config)
    members=[m for m in config['Authentication']['Oidc']['Memberships'] if m.get('ActorId')==metadata['writerActorId'] and m.get('TenantId')==metadata['tenantId']]
    if len(members)!=1:
        raise RuntimeError('source_bundle_writer_invalid')
    for permission in ('audit.read','file.read','file.write'):
        if permission not in members[0]['Permissions']:
            members[0]['Permissions'].append(permission)
    config = observation_fixture.configure(config, metadata)
    objects=source_object_root()
    config['SecureFiles']={'Adapter':'LocalDevelopment','Root':str(objects),'MaxBytes':1048576,
        'MaxPixels':1048576,'ScanSeconds':15,'ClamdPort':port,'CapabilitySeconds':60,
        'RetentionReference':'synthetic-lab-only','ScannerReference':'pinned-ephemeral-clamd'}
    private_io.publish_config(config)


def source_object_root():
    """Privately create once; validate retained API object storage without ACL writes."""
    return bundle_object_root.ensure(lab)


def prepare_crew_fixture():
    """Explicit isolated legacy-storage fixture; no production privacy policy."""
    import base64
    lab.stage('explicit_synthetic_crew_storage_fixture')
    metadata = private_io.read_json(lab.PRIVATE/'source-bundle-plan.json')
    config, _ = private_io.selected()
    configured = crew_fixture.configure(config, metadata)
    key = lab.PRIVATE/'source-bundle-crew-storage-key'
    if key.exists():
        if key.is_symlink() or not key.is_file() or key.stat().st_size != 44:
            raise RuntimeError('bundle_crew_fixture_key_invalid')
        crew_fixture.validate_key(key.read_text(encoding='ascii'))
    else:
        if config.get('CrewProfilePolicy'):
            raise RuntimeError('bundle_crew_fixture_key_missing')
        private_io.publish_bytes(key, base64.b64encode(secrets.token_bytes(32)))
    if configured != config:
        private_io.publish_config(configured)


def renew_fixture_window():
    lab.stage('explicit_synthetic_daily_fixture_window')
    metadata = private_io.read_json(lab.PRIVATE/'source-bundle-plan.json')
    realm = private_io.read_json(lab.PRIVATE/'realm/poseidon-lab-realm.json')
    config, _ = private_io.selected()
    configured = plan.renew_synthetic_window(config, realm, lab.AUTHORITY, lab.PROJECT, metadata)
    private_io.publish_config(configured)


def require_foundation_policy_fixtures():
    """Read-only prerequisite; ordinary source/qualified writes cannot opt in implicitly."""
    lab.stage('foundation_policy_source_write_preflight')
    path = lab.PRIVATE/'foundation-policy-fixture-plan.json'
    if not path.is_file():
        raise RuntimeError('foundation_policy_explicit_preparation_required')
    metadata = private_io.read_json(lab.PRIVATE/'source-bundle-plan.json')
    captured = private_io.read_json(path)
    config, _ = private_io.selected()
    foundation_policy.require_applied(config, metadata, captured, lab.PROJECT)


def prepare_foundation_policy_fixtures():
    """Explicit pre-start fixture setup only; no provider/HTTP/database actions.

    Exact repeats are read-only, including after source preparation. First apply
    refuses running/old business attempts rather than changing their field/window
    policy. Source-approved oidc_lab.py and other policy sections stay unchanged.
    """
    lab.stage('explicit_foundation_policy_fixture')
    metadata = private_io.read_json(lab.PRIVATE/'source-bundle-plan.json')
    config, _ = private_io.selected()
    path = lab.PRIVATE/'foundation-policy-fixture-plan.json'
    captured = private_io.read_json(path) if path.exists() else foundation_policy.create_plan(metadata, lab.PROJECT)
    configured = foundation_policy.configure(config, metadata, captured, lab.PROJECT)
    if foundation_policy.exact(configured, config):
        if not path.exists():
            raise RuntimeError('foundation_policy_original_plan_required')
        return
    forbidden = ('source-bundle-command-', 'qualified-owning-', 'register-counts')
    existing = {'source-bundle-provision-attempt.json', 'source-bundle-observed.json', 'source-bundle-environment.json',
                'source-bundle-lane-environment.json', 'source-bundle-preparation.json'}
    if ((lab.PRIVATE/'app.pid').exists() or any(file.name in existing or file.name.startswith(forbidden) for file in lab.PRIVATE.iterdir())
            or not (lab.PRIVATE/'ready').is_file()):
        raise RuntimeError('foundation_policy_fresh_prestart_run_required')
    # All in-memory checks complete before either immutable publication.
    if not path.exists():
        private_io.publish_json(path, captured)
    private_io.publish_config(configured)


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
    env.pop('POSEIDON_BROWSER_QUALIFIED_OWNING_FIXTURE', None)
    for name in FIXTURE_NAMES:
        env.pop('POSEIDON_BROWSER_'+name+'_FIXTURE', None)
    if (lab.PRIVATE/'source-bundle-plan.json').is_file():
        selected,selected_config=private_io.selected()
        lab.bind_runtime(lab_runtime.validate_selected(selected, lab.PRIVATE))
        env['POSEIDON_LAB_CONFIG']=str(selected_config)
        env.pop(crew_fixture.REFERENCE, None)
        if selected.get('CrewProfilePolicy',{}).get('KeyReferences',{}).get('synthetic-bundle-v1') == crew_fixture.REFERENCE:
            key = lab.PRIVATE/'source-bundle-crew-storage-key'
            if key.is_symlink() or not key.is_file() or key.stat().st_size != 44:
                raise RuntimeError('bundle_crew_fixture_key_missing')
            env[crew_fixture.REFERENCE] = crew_fixture.validate_key(key.read_text(encoding='ascii'))
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
    env.pop('POSEIDON_LAB_RUNTIME_FILE', None)
    if lab.CURRENT_RUNTIME is not None:
        env['POSEIDON_LAB_RUNTIME_FILE'] = str(lab.PRIVATE/lab_runtime.FILE)
    env['POSEIDON_REGISTER_URL'] = lab.APP
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
    require_foundation_policy_fixtures()
    lab.stage('real_signed_source_fixture_preparation')
    if not (lab.PRIVATE/'source-bundle-plan.json').is_file():
        raise RuntimeError('fresh_disposable_bundle_plan_required')
    lab.run([lab.root_tool('node'),str(lab.ROOT/'scripts/bundle_fixture_provision.ts')],env=environment(),timeout=600)
    lab.stage('exact_observed_source_appointments')
    observed_path=lab.PRIVATE/'source-bundle-observed.json'
    if observed_path.is_symlink() or observed_path.stat().st_size>32768:
        raise RuntimeError('observed_source_boundary')
    metadata=json.loads((lab.PRIVATE/'source-bundle-plan.json').read_text())
    observed=json.loads(observed_path.read_text())
    config,_=private_io.selected()
    configured=plan.configure_observed(config,metadata,observed)
    # The established explicit helper is Test-only and attributed EF; no request
    # selects tenant/actor/content and no normal seed invents historical authority.
    for lane in ('desktop','narrow'):
        batch=metadata['legacyBatches'][lane]
        if str(uuid.UUID(batch))!=batch or uuid.UUID(batch).int==0:
            raise RuntimeError('legacy_batch_uuid_invalid')
        env=environment();env['DOTNET_ENVIRONMENT']='Test';env['ASPNETCORE_ENVIRONMENT']='Test'
        env['POSEIDON_DATABASE']=config['ConnectionStrings']['Poseidon']
        lab.stage('explicit_'+lane+'_legacy_storage_fixture')
        lab.run([lab.root_tool('dotnet'),'run','--project','backend/Poseidon.DbMigrator','-c','Release','--no-build',
                 '--','--legacy-crew-browser-fixture',batch],env=env,timeout=120)
    private_io.publish_config(configured)
    lab.stage('owned_source_fixture_host_restart')
    if not lab.owned_process('stop'):
        raise RuntimeError('owned_fixture_host_required')
    start()
    lab.stage('real_signed_pending_source_fixture_preparation')
    lab.run([lab.root_tool('node'),str(lab.ROOT/'scripts/bundle_fixture_legacy_finish.ts')],env=environment(),timeout=600)


def prepare_qualified_fixtures():
    """Explicit opt-in real-command FICTIONAL witnesses, separate from exact-eight.

    Never mutates another profile or provider. Each appointment generation is
    additive, private and immutable; only this run's exact owned app restarts.
    """
    require_foundation_policy_fixtures()
    lab.stage('qualified_fixture_owned_preflight')
    metadata = private_io.read_json(lab.PRIVATE/'source-bundle-plan.json')
    config, _ = private_io.selected()
    # Existing normal fixture preparation and actual owned scanner remain prerequisites.
    if len(source_fixture_environment()) != len(FIXTURE_NAMES):
        raise RuntimeError('qualified_fixture_source_bundle_required')
    lanes = private_io.read_json(lab.PRIVATE/'source-bundle-lane-environment.json')
    if (lanes.get('schemaVersion') != 'poseidon.bundle.signed-lane-fixtures.v1' or lanes.get('runId') != metadata.get('runId')
            or set(lanes.get('lanes', {})) != {'desktop', 'narrow'} or not lab.owned_process('check')):
        raise RuntimeError('qualified_fixture_owned_running_source_bundle_required')
    secure = config.get('SecureFiles', {})
    if (secure.get('Adapter') != 'LocalDevelopment' or secure.get('Root') != str(source_object_root())
            or secure.get('ScannerReference') != 'pinned-ephemeral-clamd' or type(secure.get('ClamdPort')) is not int):
        raise RuntimeError('qualified_fixture_actual_owned_scanner_required')
    with socket.create_connection(('127.0.0.1', secure['ClamdPort']), timeout=2) as peer:
        peer.sendall(b'zPING\0')
        if peer.recv(5) != b'PONG\0':
            raise RuntimeError('qualified_fixture_actual_owned_scanner_required')
    producer = str(lab.ROOT/'scripts/bundle_qualified_fixture_provision.ts')
    lab.stage('qualified_fixture_actual_isolated_vessels')
    lab.run([lab.root_tool('node'), producer, 'vessels'], env=environment(), timeout=600)
    vessels = qualified_plan.validate_vessels(metadata, private_io.read_json(lab.PRIVATE/'qualified-owning-observed-vessels.json'))
    path = lab.PRIVATE/'qualified-owning-plan.json'
    if not path.exists():
        attempt = private_io.read_json(lab.PRIVATE/'qualified-owning-provision-attempt.json')
        private_io.publish_json(path, qualified_plan.create(metadata, vessels, qualified_plan.instant(attempt['createdAt'])))
    fixture_plan = qualified_plan.validate(private_io.read_json(path))

    def install(sources=None, witnesses=None):
        current, _ = private_io.selected()
        candidate = qualified_plan.configure(current, metadata, fixture_plan, sources, witnesses, vessels=vessels)
        if candidate != current:
            # Confirm exact ownership before publication or any stop. Old selected
            # config/process/generation is retained; provider and volumes are untouched.
            if not lab.owned_process('check'):
                raise RuntimeError('qualified_fixture_exact_owned_host_required')
            private_io.publish_config(candidate)
            if not lab.owned_process('stop'):
                raise RuntimeError('qualified_fixture_exact_owned_host_required')
            start()

    install()
    lab.stage('qualified_fixture_actual_sources')
    lab.run([lab.root_tool('node'), producer, 'sources'], env=environment(), timeout=600)
    sources = private_io.read_json(lab.PRIVATE/'qualified-owning-observed-sources.json')
    install(sources)
    lab.stage('qualified_fixture_actual_witnesses')
    lab.run([lab.root_tool('node'), producer, 'witnesses'], env=environment(), timeout=1200)
    witnesses = private_io.read_json(lab.PRIVATE/'qualified-owning-observed-witnesses.json')
    install(sources, witnesses)
    lab.stage('qualified_fixture_actual_independent_verification')
    lab.run([lab.root_tool('node'), producer, 'verify'], env=environment(), timeout=1200)


def qualified_fixture_environment():
    """Separate additive descriptor; never a ninth key in exact-eight decoding."""
    path = lab.PRIVATE/'qualified-owning-fixture.json'
    if not path.exists():
        raise RuntimeError('qualified_fixture_preparation_required')
    value = private_io.read_json(path)
    metadata = private_io.read_json(lab.PRIVATE/'source-bundle-plan.json')
    fixture_plan = qualified_plan.validate(private_io.read_json(lab.PRIVATE/'qualified-owning-plan.json'))
    qualified_plan.validate_descriptor(fixture_plan, value)
    vessels = qualified_plan.validate_vessels(metadata, private_io.read_json(lab.PRIVATE/'qualified-owning-observed-vessels.json'))
    if (value.get('schemaVersion') != 'poseidon.qualified-owning.signed-fixture.v1'
            or value.get('runId') != metadata.get('runId') or value.get('expiresAt') != fixture_plan['expiresAt']
            or value.get('vessels') != vessels['vessels']
            or any(fixture_plan[key] != metadata.get(key) for key in ('runId', 'tenantId', 'writerActorId', 'reviewerActorId', 'writerRoleId', 'reviewerRoleId'))):
        raise RuntimeError('qualified_fixture_scope_invalid')
    # The strict TS consumer repeats private boundary, full schema, time bounds,
    # selected config/runtime and actual authenticated current-source validation.
    return {'POSEIDON_BROWSER_QUALIFIED_OWNING_FIXTURE': str(path.resolve())}


def start():
    metadata=private_io.read_json(lab.PRIVATE/'source-bundle-plan.json')
    if metadata.get('schemaVersion')!='poseidon.bundle-fixture-plan.v1' or not (lab.PRIVATE/'ready').is_file():
        raise RuntimeError('coherent_source_bundle_genesis_required')
    private_io.selected()
    original=lab.environment
    try:
        lab.environment=environment
        lab.start()
    finally:
        lab.environment=original


def register_browser():
    require_foundation_policy_fixtures()
    lab.stage('real_provider_source_bundle_browser')
    if any(os.environ.get(key) for key in ('POSEIDON_REGISTER_CASE','POSEIDON_REGISTER_PROJECT')):
        raise RuntimeError('full_bundle_manifest_required')
    env=environment();fixtures=source_fixture_environment()
    if len(fixtures)!=len(FIXTURE_NAMES):
        raise RuntimeError('source_fixture_environment_required')
    env.update(fixtures)
    env.pop('POSEIDON_BROWSER_QUALIFIED_OWNING_FIXTURE', None)
    env.update(qualified_fixture_environment())
    lane_file=lab.PRIVATE/'source-bundle-lane-environment.json'
    if lane_file.is_symlink() or not lane_file.is_file() or lane_file.stat().st_size>8192:
        raise RuntimeError('source_lane_fixture_required')
    env['POSEIDON_BROWSER_LANE_FIXTURE']=str(lane_file.resolve())
    lab.run([lab.root_tool('node'),str(lab.ROOT/'tests/Poseidon.Browser.Tests/lab/register-run.ts')],env=env,timeout=1800)


def stop():lab.stop()


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['init_source_bundle','prepare_bundle','prepare_crew_fixture','prepare_foundation_policy_fixtures','renew_fixture_window','prepare_source_fixtures','prepare_qualified_fixtures','start','stop','register_browser'])
    parser.add_argument('--app-port', type=lab_runtime.cli_port)
    parser.add_argument('--provider-port', type=lab_runtime.cli_port)
    parser.add_argument('--database-port', type=lab_runtime.cli_port)
    args=parser.parse_args(argv)
    try:
        if sys.version_info[:3]!=(3,13,13):raise RuntimeError('pinned_python_required')
        ports = (args.app_port, args.provider_port, args.database_port)
        if args.action == 'init_source_bundle':
            init_source_bundle(*ports)
        else:
            if any(port is not None for port in ports):
                raise RuntimeError('owned_lab_ports_genesis_only')
            lab.bind_selected_runtime()
            if (lab.PRIVATE/'source-bundle-plan.json').exists():
                private_io.selected()  # Verify the immutable generation before any action mutates runtime.
            # Both explicit producers and the final register require the same
            # already-applied policy; this check never implicitly enables it.
            if args.action in ('prepare_source_fixtures', 'prepare_qualified_fixtures', 'register_browser'):
                require_foundation_policy_fixtures()
            globals()[args.action]()
        print('DEV-BUNDLE-FIXTURE-01 '+args.action+' COMPLETED; software fixture only')
        return 0
    except Exception:
        print('DEV-BUNDLE-FIXTURE-01 '+args.action+' FAILED at '+lab.STAGE+'; private state preserved')
        return 1


if __name__=='__main__':raise SystemExit(main())
