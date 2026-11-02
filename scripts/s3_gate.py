"""Owned real loopback S3 driver for the later gate. Never run during authoring.

Requires an operator-built, source-labelled MinIO image by immutable local image ID.
No production vendor, subscription, ambient AWS credential, shared service or fake S3 peer.
"""
import argparse
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import time
import urllib.request
import uuid
from oidc_lab import ROOT, environment, private_directory, write

LABEL = 'poseidon.files-source-s3-gate'


def isolated_environment():
    # A disposable actual-host test must never inherit an operator's lab config
    # selector or database. Test hosts provision their own owned PostgreSQL scope.
    return {k: v for k, v in environment().items()
            if k not in ('POSEIDON_LAB_CONFIG', 'POSEIDON_DATABASE')
            and not k.startswith(('AWS_', 'AMAZON_', 'MINIO_', 'POSEIDON_GATE_S3_'))}


def docker(*args):
    result = subprocess.run(['docker', *args], capture_output=True, timeout=60)
    if result.returncode:
        raise RuntimeError('owned_s3_command_failed')
    return result.stdout.decode().strip()


def owned(identity, owner, name, require_binding=True):
    record = json.loads(docker('inspect', identity))[0]
    if record['Id'] != identity or record['Name'] != '/' + name or record['Config'].get('Labels', {}).get(LABEL) != owner:
        raise RuntimeError('ownership_mismatch')
    if not require_binding:
        return None
    ports = record['NetworkSettings']['Ports'].get('9000/tcp', [])
    if len(ports) != 1 or ports[0]['HostIp'] != '127.0.0.1':
        raise RuntimeError('non_loopback_binding')
    return int(ports[0]['HostPort'])


def run(image, command, child_timeout=900):
    if type(child_timeout) is not int or not 1 <= child_timeout <= 3600:
        raise RuntimeError('invalid_child_deadline')
    if not re.fullmatch(r'sha256:[0-9a-f]{64}', image) or not command:
        raise RuntimeError('explicit_owned_image_and_command_required')
    record = json.loads(docker('image', 'inspect', image))[0]
    labels = record['Config'].get('Labels') or {}
    if record['Id'] != image or labels.get('org.opencontainers.image.source') != 'https://github.com/minio/minio' or not re.fullmatch('[0-9a-f]{40}', labels.get('org.opencontainers.image.revision', '')):
        raise RuntimeError('source_label_required')
    owner = uuid.uuid4().hex; name = 'poseidon-files-s3-' + owner
    private = ROOT / '.runtime/files-source-s3' / owner
    private_directory(private)
    access = secrets.token_hex(16); credential = secrets.token_urlsafe(48)
    config = private / 'container.env'
    write(config, 'MINIO_ROOT_USER=' + access + '\nMINIO_ROOT_PASSWORD=' + credential + '\nMINIO_BROWSER=off\n')
    identity = None
    try:
        identity = docker('run', '--detach', '--pull', 'never', '--name', name, '--label', LABEL + '=' + owner,
                          '--publish', '127.0.0.1::9000', '--env-file', str(config), image, 'server', '/data')
        port = owned(identity, owner, name); endpoint = f'http://127.0.0.1:{port}'
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        deadline = time.monotonic() + 60
        while True:
            try:
                with opener.open(endpoint + '/minio/health/ready', timeout=2) as response:
                    if response.status == 200:
                        break
            except OSError:
                pass
            if time.monotonic() >= deadline:
                raise RuntimeError('owned_s3_readiness_deadline')
            time.sleep(.5)
        owned(identity, owner, name)  # Reconfirm exact binding immediately before handing it to tests.
        env = isolated_environment()
        env.update({'POSEIDON_GATE_S3_ENDPOINT': endpoint, 'POSEIDON_GATE_S3_ACCESS': access,
                    'POSEIDON_GATE_S3_SECRET': credential, 'POSEIDON_GATE_S3_OWNER': owner})
        # Child logs may contain failed SDK request diagnostics; never echo them.
        result = subprocess.run(command, cwd=ROOT, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=child_timeout)
        return result.returncode
    finally:
        if identity:
            owned(identity, owner, name, require_binding=False)
            docker('rm', '--force', identity)  # Exact owned resource only, no volumes/prune.
        config.unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image', required=True)
    parser.add_argument('--child-timeout', type=int, default=900, help='Explicit bounded child deadline, 1..3600 seconds; the full API bundle needs more than 15 minutes.')
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    try:
        command = args.command[1:] if args.command[:1] == ['--'] else args.command
        code = run(args.image, command, args.child_timeout)
        print(json.dumps({'stage': 'owned_s3_child', 'exit_code': code}))
        return code
    except Exception:
        print(json.dumps({'stage': 'owned_s3_setup_or_cleanup_failed'}))
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
