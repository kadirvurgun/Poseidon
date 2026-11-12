"""BUNDLE-PORT-ISOLATION-01: explicit owned localhost endpoints, never authority.

No runtime calls on import. Legacy profiles keep their original endpoints; a
selected source-bundle run must match its publish-once descriptor and config.
"""
from pathlib import Path
import json
import os
import re
import stat
import uuid
import hashlib
import argparse
import socket
import errno

SCHEMA = 'poseidon.lab.owned-runtime.v1'
FILE = 'owned-runtime.json'
KEYS = {'schemaVersion', 'runId', 'appPort', 'providerPort', 'databasePort'}
DEFAULTS = {'appPort': 5443, 'providerPort': 8443, 'databasePort': 55432}


def cli_port(value):
    if not isinstance(value, str) or not re.fullmatch(r'[1-9][0-9]{3,4}', value) or not 1024 <= int(value) <= 65535:
        raise argparse.ArgumentTypeError('owned_lab_port_invalid')
    return int(value)


def fail():
    raise RuntimeError('owned_lab_runtime_invalid') from None


def decode(value):
    if not isinstance(value, dict) or set(value) != KEYS or value.get('schemaVersion') != SCHEMA:
        fail()
    run = value.get('runId')
    try:
        if not isinstance(run, str) or len(run) != 36 or str(uuid.UUID(run)) != run or uuid.UUID(run).int == 0:
            fail()
    except (ValueError, TypeError, AttributeError):
        fail()
    ports = [value[key] for key in DEFAULTS]
    if any(type(port) is not int or not 1024 <= port <= 65535 for port in ports) or len(set(ports)) != 3:
        fail()
    return {key: value[key] for key in ('schemaVersion', 'runId', *DEFAULTS)}


def create(run_id, app_port, provider_port, database_port):
    return decode({'schemaVersion': SCHEMA, 'runId': run_id, 'appPort': app_port,
                   'providerPort': provider_port, 'databasePort': database_port})


def endpoints(runtime=None):
    ports = DEFAULTS if runtime is None else decode(runtime)
    app = f'https://localhost:{ports["appPort"]}'
    provider = f'https://localhost:{ports["providerPort"]}'
    return {**{key: ports[key] for key in DEFAULTS}, 'app': app, 'provider': provider,
            'authority': provider + '/realms/poseidon-lab'}


def configured(config):
    if not isinstance(config, dict):
        fail()
    development = config.get('DevelopmentLab', {})
    if not isinstance(development, dict):
        fail()
    if 'OwnedRuntime' not in development:
        if 'OidcPort' in development:
            fail()
        return None
    runtime = decode(development['OwnedRuntime'])
    if type(development.get('OidcPort')) is not int or development['OidcPort'] != runtime['providerPort']:
        fail()
    selected = endpoints(runtime)
    if any(not isinstance(config.get(key), dict) for key in ('Authentication', 'Kestrel', 'ConnectionStrings')):
        fail()
    federation = config['Authentication'].get('Oidc', {})
    if not isinstance(federation, dict) or federation.get('Authority') != selected['authority'] or federation.get('Issuer') != selected['authority']:
        fail()
    entries = config.get('Kestrel', {}).get('Endpoints', {})
    if not isinstance(entries, dict) or set(entries) != {'LocalLab'} or not isinstance(entries['LocalLab'], dict) or entries['LocalLab'].get('Url') != selected['app']:
        fail()
    connection = config.get('ConnectionStrings', {}).get('Poseidon')
    if not isinstance(connection, str):
        fail()
    entries = [part.split('=', 1) for part in connection.split(';') if part]
    if any(len(part) != 2 for part in entries):
        fail()
    keys = [key.strip().lower() for key, _ in entries]
    if len(set(keys)) != len(keys) or any(key in ('server', 'data source', 'address', 'network address') for key in keys):
        fail()
    hosts = [value for key, value in entries if key == 'Host']
    ports = [value for key, value in entries if key == 'Port']
    if hosts != ['localhost'] or ports != [str(selected['databasePort'])]:
        fail()
    return runtime


def authority(config):
    return endpoints(configured(config))['authority']


def _closed(pairs):
    value = {}
    for key, item in pairs:
        if key in value:
            fail()
        value[key] = item
    return value


def _private(metadata, directory=False):
    if not (stat.S_ISDIR(metadata.st_mode) if directory else stat.S_ISREG(metadata.st_mode)) or not directory and metadata.st_nlink != 1:
        fail()
    if os.name != 'nt' and (metadata.st_mode & 0o077 or hasattr(os, 'geteuid') and metadata.st_uid != os.geteuid()):
        fail()


def _unchanged(first, second):
    return all(getattr(first, key) == getattr(second, key) for key in
        ('st_dev', 'st_ino', 'st_size', 'st_mtime_ns', 'st_ctime_ns', 'st_mode', 'st_uid'))


def _read(path, limit):
    try:
        parent_before = path.parent.lstat()
        _private(parent_before, True)
        if path.parent.is_symlink() or path.parent.is_junction() or path.parent.resolve() != path.parent.absolute():
            fail()
        before = path.lstat()
        _private(before)
        if path.is_symlink() or path.is_junction() or not stat.S_ISREG(before.st_mode) or before.st_nlink != 1 or before.st_size > limit:
            fail()
        descriptor = os.open(path, os.O_RDONLY | getattr(os, 'O_NOFOLLOW', 0) | getattr(os, 'O_NONBLOCK', 0))
        try:
            opened = os.fstat(descriptor)
            _private(opened)
            if not _unchanged(before, opened):
                fail()
            raw = os.read(descriptor, limit + 1)
            after, current, parent_after = os.fstat(descriptor), path.lstat(), path.parent.lstat()
            _private(after); _private(current); _private(parent_after, True)
            if (len(raw) > limit or len(raw) != before.st_size or not _unchanged(opened, after) or not _unchanged(opened, current) or
                    not _unchanged(parent_before, parent_after) or path.is_symlink() or path.is_junction() or
                    path.resolve() != path.absolute()):
                fail()
            return json.loads(raw.decode('utf-8'), object_pairs_hook=_closed)
        finally:
            os.close(descriptor)
    except Exception:
        fail()


def read(private):
    """Only same-worktree immutable public metadata is read, never credentials."""
    private = Path(private)
    if private.exists():
        _private(private.lstat(), True)
    path = private / FILE
    plan_path = private / 'source-bundle-plan.json'
    if path.exists() or path.is_symlink():
        if private.is_symlink() or private.is_junction() or private.resolve() != private.absolute():
            fail()
        runtime = decode(_read(path, 4096))
        plan = _read(plan_path, 65536)
        if not isinstance(plan, dict) or plan.get('schemaVersion') != 'poseidon.bundle-fixture-plan.v1' or plan.get('runId') != runtime['runId'] or plan.get('ownedRuntime') != runtime:
            fail()
        return runtime
    if plan_path.exists() and 'ownedRuntime' in _read(plan_path, 65536):
        fail()
    return None


def validate_selected(config, private):
    selected = read(private)
    if configured(config) != selected:
        fail()
    return selected


def child_environment(environment, runtime=None):
    """Overwrite only compose selectors from already validated owned metadata."""
    selected = endpoints(runtime)
    result = dict(environment)
    result['POSEIDON_LAB_PROVIDER_PORT'] = str(selected['providerPort'])
    result['POSEIDON_LAB_DATABASE_PORT'] = str(selected['databasePort'])
    result['POSEIDON_LAB_RUN_ID'] = 'legacy' if runtime is None else decode(runtime)['runId']
    result['POSEIDON_LAB_RUNTIME_IDENTITY'] = runtime_identity(runtime)
    return result


def port_free(port):
    """A temporary exclusive loopback bind is observation, never listener adoption."""
    try:
        with socket.socket() as peer:
            peer.settimeout(0.2)
            if peer.connect_ex(('127.0.0.1', port)) == 0:
                return False
        with socket.socket() as probe:
            if hasattr(socket, 'SO_EXCLUSIVEADDRUSE'):
                probe.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
            probe.bind(('127.0.0.1', port))
        return True
    except OSError:
        return False


def ipv6_free(port):
    # localhost may resolve to ::1 even though Compose publishes IPv4 only.
    if not socket.has_ipv6:
        return True
    try:
        with socket.socket(socket.AF_INET6) as peer:
            peer.settimeout(0.2)
            if peer.connect_ex(('::1', port)) == 0:
                return False
        with socket.socket(socket.AF_INET6) as probe:
            probe.setsockopt(socket.IPPROTO_IPV6, socket.IPV6_V6ONLY, 1)
            if hasattr(socket, 'SO_EXCLUSIVEADDRUSE'):
                probe.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
            probe.bind(('::1', port))
        return True
    except OSError as error:
        return error.errno in (errno.EAFNOSUPPORT, errno.EPROTONOSUPPORT, errno.EADDRNOTAVAIL)


def preflight(runtime=None, owns=None, app_owned=False):
    selected = endpoints(runtime)
    for key, service in (('appPort', None), ('providerPort', 'provider'), ('databasePort', 'database')):
        if not ipv6_free(selected[key]) and not (service is None and app_owned):
            raise RuntimeError('owned_lab_port_in_use')
        if port_free(selected[key]):
            continue
        if service is None and app_owned:
            continue
        if service is not None and owns is not None and owns(service, selected[key]):
            continue
        raise RuntimeError('owned_lab_port_in_use')


def runtime_identity(runtime=None):
    if runtime is None:
        return 'legacy'
    return hashlib.sha256(json.dumps(decode(runtime), sort_keys=True, separators=(',', ':')).encode('utf-8')).hexdigest()
