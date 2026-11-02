"""Export the real Release API contract from an owned ephemeral loopback host."""
import json
from pathlib import Path
import socket
import subprocess
import time
import urllib.request

from oidc_lab import ROOT, environment, root_tool


def export():
    env = environment()
    env.pop('POSEIDON_LAB_CONFIG', None)
    with socket.socket() as reservation:
        reservation.bind(('127.0.0.1', 0))
        port = reservation.getsockname()[1]
    # No login, provider, database mutation, request-body logging or ambient auth.
    process = subprocess.Popen([root_tool('dotnet'), str(ROOT / 'backend/Poseidon.Api/bin/Release/net10.0/Poseidon.Api.dll'),
                                '--urls', f'http://127.0.0.1:{port}'], cwd=ROOT, env=env,
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        for _ in range(90):
            if process.poll() is not None:
                raise RuntimeError('export_host_failed')
            try:
                with urllib.request.urlopen(f'http://127.0.0.1:{port}/openapi/v1.json', timeout=3) as response:
                    data = response.read(16 * 1024 * 1024 + 1)
                break
            except (OSError, TimeoutError):
                time.sleep(1)
        else:
            raise RuntimeError('export_deadline')
        document = json.loads(data)
        if len(data) > 16 * 1024 * 1024 or not document.get('paths') or not document.get('components', {}).get('schemas'):
            raise RuntimeError('export_contract_missing')
        (ROOT / 'contracts/openapi.v1.json').write_bytes(data)
        return {'status': 'exported', 'paths': len(document['paths']), 'schemas': len(document['components']['schemas'])}
    finally:
        if process.poll() is None:
            process.terminate()
        try:
            process.wait(timeout=15)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=15)


if __name__ == '__main__':
    try:
        print(json.dumps(export()))
    except Exception:
        print(json.dumps({'status': 'failed', 'stage': 'actual_api_export'}))
        raise SystemExit(1) from None
