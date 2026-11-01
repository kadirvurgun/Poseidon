"""Ordinary desktop/mobile regression with an owned real audit database; no identity replacement."""
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import time
import uuid
import signal

from oidc_lab import ROOT, environment, private_directory, root_tool, write

# Keep the verified PostgreSQL index pin consistent with infra/oidc-lab/compose.yaml.
IMAGE = 'postgres:18.6@sha256:979bccfde35445db618c65107d27d08c6c21339d8dc3ad4c830c6633947be0e4'
LABEL = 'poseidon.browser-gate'


def docker(*args):
    result = subprocess.run(['docker', *args], capture_output=True, timeout=180)
    if result.returncode:
        raise RuntimeError('database_container_failed')
    return result.stdout.decode().strip()


def cleanup(identity, name, owner):
    record = json.loads(docker('inspect', identity))[0]
    if record['Id'] != identity or record['Name'] != '/' + name or record['Config'].get('Labels', {}).get(LABEL) != owner:
        raise RuntimeError('ownership_mismatch')
    docker('rm', '--force', identity)  # No volumes/prune; never unrelated services.


def browser(args, env):
    process = subprocess.Popen(args, cwd=ROOT, env=env, start_new_session=os.name != 'nt',
                               stdout=sys.stdout, stderr=sys.stderr,
                               creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    try:
        return process.wait(timeout=600)
    except subprocess.TimeoutExpired:
        if process.poll() is None:
            if os.name == 'nt':
                subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=15)
            else:
                os.killpg(process.pid, signal.SIGTERM)
            process.wait(timeout=15)
        raise RuntimeError('owned_browser_timeout') from None


def main():
    owner = uuid.uuid4().hex
    name = 'poseidon-browser-' + owner
    private = ROOT / '.runtime/browser-gate' / owner
    private_directory(private)
    password = secrets.token_urlsafe(48)
    config = private / 'database.env'
    write(config, 'POSTGRES_USER=poseidon_gate\nPOSTGRES_DB=poseidon_gate\nPOSTGRES_PASSWORD=' + password + '\n')
    identity = None
    try:
        identity = docker('run', '--detach', '--name', name, '--label', LABEL + '=' + owner,
                          '--env-file', str(config), '--publish', '127.0.0.1::5432', IMAGE)
        binding = json.loads(docker('inspect', identity))[0]['NetworkSettings']['Ports']['5432/tcp']
        if len(binding) != 1 or binding[0]['HostIp'] != '127.0.0.1':
            raise RuntimeError('database_binding_invalid')
        for attempt in range(60):
            try:
                docker('exec', identity, 'pg_isready', '-U', 'poseidon_gate', '-d', 'poseidon_gate')
                break
            except RuntimeError:
                if attempt == 59:
                    raise RuntimeError('database_readiness_deadline') from None
                time.sleep(1)
        env = environment()
        env.pop('POSEIDON_LAB_CONFIG', None)
        env['DOTNET_ENVIRONMENT'] = 'Development'
        env['POSEIDON_DATABASE'] = f"Host=127.0.0.1;Port={binding[0]['HostPort']};Database=poseidon_gate;Username=poseidon_gate;Password={password};Include Error Detail=false"
        env['PATH'] = str(Path(root_tool('dotnet')).parent) + os.pathsep + str(Path(root_tool('node')).parent) + os.pathsep + env['PATH']
        result = subprocess.run([root_tool('dotnet'), 'run', '--project', 'backend/Poseidon.DbMigrator', '--configuration', 'Release', '--no-build', '--', '--seed', 'backend/seed/synthetic-demo.json'], cwd=ROOT, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=90)
        if result.returncode:
            raise RuntimeError('database_migration_failed')
        npm = ([root_tool('node'), str(Path(root_tool('node')).parent / 'node_modules/npm/bin/npm-cli.js')]
               if os.name == 'nt' else ['npm'])
        return browser([*npm, 'test', '--prefix', 'tests/Poseidon.Browser.Tests', '--', *sys.argv[1:]], env)
    except Exception:
        print(json.dumps({'status': 'failed', 'stage': 'owned_browser_gate'}))
        return 1
    finally:
        try:
            if identity:
                cleanup(identity, name, owner)
        finally:
            config.unlink()
            private.rmdir()


if __name__ == '__main__':
    raise SystemExit(main())
