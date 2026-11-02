"""Owned ephemeral loopback scanner for AUDIT-FILES-BUNDLE-01; safe metadata only."""
import argparse
import json
from pathlib import Path
import socket
import subprocess
import time
import uuid

IMAGE = 'clamav/clamav:1.5.4@sha256:bda39705e291105049c3936e2803ac90b955172af8030f94f05dce1252df6c6d'
LABEL = 'poseidon.audit-files-gate'


def docker(*args, timeout=60):
    result = subprocess.run(['docker', *args], capture_output=True, timeout=timeout)
    if result.returncode:
        raise RuntimeError('container_command_failed')
    return result.stdout.decode().strip()


def cleanup(state):
    container = json.loads(docker('inspect', state['id']))[0]
    if (container['Id'] != state['id'] or
            container['Config'].get('Labels', {}).get(LABEL) != state['owner'] or
            container['Name'] != '/' + state['name']):
        raise RuntimeError('ownership_mismatch')
    docker('rm', '--force', state['id'])  # Exact owned container; no volumes/prune.


def start(path, deadline=240):
    if path.exists():
        raise RuntimeError('state_already_exists')
    owner = uuid.uuid4().hex
    state = {'owner': owner, 'name': 'poseidon-audit-files-' + owner, 'image': IMAGE}
    docker('pull', IMAGE, timeout=300)
    state['id'] = docker('run', '--detach', '--name', state['name'], '--label', LABEL + '=' + owner,
                         '--publish', '127.0.0.1::3310', IMAGE)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(state))
    try:
        ports = json.loads(docker('inspect', state['id']))[0]['NetworkSettings']['Ports']['3310/tcp']
        if len(ports) != 1 or ports[0]['HostIp'] != '127.0.0.1':
            raise RuntimeError('non_loopback_binding')
        state['port'] = int(ports[0]['HostPort'])
        path.write_text(json.dumps(state))
        end = time.monotonic() + deadline
        while time.monotonic() < end:
            try:
                with socket.create_connection(('127.0.0.1', state['port']), timeout=2) as peer:
                    peer.sendall(b'zPING\0')
                    if peer.recv(5) == b'PONG\0':
                        return state
            except OSError:
                pass
            time.sleep(1)
        raise RuntimeError('scanner_readiness_deadline')
    except Exception:
        cleanup(state)
        path.unlink()
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['start', 'stop'])
    parser.add_argument('--state', type=Path, required=True)
    args = parser.parse_args()
    try:
        if args.action == 'start':
            state = start(args.state)
            print(json.dumps({'status': 'ready', 'port': state['port'], 'image': IMAGE}))
        else:
            cleanup(json.loads(args.state.read_text()))
            args.state.unlink()
            print(json.dumps({'status': 'owned_container_removed'}))
        return 0
    except subprocess.TimeoutExpired:
        print(json.dumps({'status': 'failed', 'stage': 'container_command_timeout'}))
    except Exception as error:
        safe = str(error) if isinstance(error, RuntimeError) else 'lab_setup_failed'
        print(json.dumps({'status': 'failed', 'stage': safe}))
    return 1


if __name__ == '__main__':
    raise SystemExit(main())
