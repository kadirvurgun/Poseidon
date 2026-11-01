"""Explicit Windows lab bootstrap/TLS checks; synthetic data only, no diagnostics dump."""
import http.server
import ipaddress
import json
import os
from pathlib import Path
import subprocess
import ssl
import tempfile
import threading
import unittest
from unittest.mock import patch
import urllib.request

import oidc_lab as lab


class QuietHandler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(200)
        self.end_headers()

    def log_message(self, *_):
        pass


class LocalBootstrapTests(unittest.TestCase):
    def test_actual_api_host_has_only_loopback_https_despite_prefixed_ambient_endpoints(self):
        # Exercise Host.CreateDefaultBuilder + ConfigureWebHostDefaults, not a
        # dictionary/configuration mock. Every listener belonging to this child
        # must be the lab endpoint; arbitrary dynamic HTTP ports expose the bug.
        ambient = {
            'DOTNET_Kestrel__Endpoints__InjectedDotnet__Url': 'http://0.0.0.0:0',
            'ASPNETCORE_Kestrel__Endpoints__InjectedAspnet__Url': 'http://0.0.0.0:0',
            'Kestrel__Endpoints__InjectedDirect__Url': 'http://0.0.0.0:0',
            'DOTNET_URLS': 'http://0.0.0.0:0',
            'ASPNETCORE_HTTP_PORTS': '0',
        }
        with tempfile.TemporaryDirectory() as temporary, patch.object(lab, 'PRIVATE', Path(temporary) / 'private'):
            lab.init()
            with patch.dict(os.environ, ambient):
                env = lab.environment()
            process = subprocess.Popen(
                [lab.root_tool('dotnet'), str(lab.ROOT / 'backend/Poseidon.Api/bin/Release/net10.0/Poseidon.Api.dll')],
                cwd=lab.ROOT, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                creationflags=subprocess.CREATE_NO_WINDOW)
            try:
                lab.wait_https(lab.APP + '/health/live')
                result = subprocess.run(
                    ['pwsh', '-NoProfile', '-Command',
                     f'Get-NetTCPConnection -State Listen -OwningProcess {process.pid} -ErrorAction Stop | '
                     'Select-Object LocalAddress,LocalPort | ConvertTo-Json -Compress'],
                    capture_output=True, timeout=30)
                self.assertEqual(0, result.returncode, 'listener inspection failed')
                listeners = json.loads(result.stdout)
                if isinstance(listeners, dict):
                    listeners = [listeners]
                self.assertTrue(listeners, 'API must own its HTTPS listener')
                self.assertEqual({5443}, {listener['LocalPort'] for listener in listeners})
                self.assertTrue(all(ipaddress.ip_address(listener['LocalAddress']).is_loopback for listener in listeners))
                self.assertIsNone(process.poll(), 'API must remain running')
            finally:
                if process.poll() is None:
                    process.terminate()
                process.wait(timeout=30)

    def test_actual_private_bootstrap_restart_and_strict_python_tls(self):
        with tempfile.TemporaryDirectory() as temporary, patch.object(lab, 'PRIVATE', Path(temporary) / 'private'):
            lab.init()
            files = {p.relative_to(lab.PRIVATE): p.read_bytes() for p in lab.PRIVATE.rglob('*') if p.is_file()}
            lab.init()
            self.assertEqual(files, {p.relative_to(lab.PRIVATE): p.read_bytes() for p in lab.PRIVATE.rglob('*') if p.is_file()})
            context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
            context.load_cert_chain(lab.PRIVATE / 'tls.pem', lab.PRIVATE / 'tls.key')
            server = http.server.HTTPServer(('127.0.0.1', 0), QuietHandler)
            server.socket = context.wrap_socket(server.socket, server_side=True)
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            client = ssl.create_default_context(cafile=str(lab.PRIVATE / 'ca.pem'))
            try:
                with urllib.request.urlopen(f'https://localhost:{server.server_port}', context=client, timeout=3) as response:
                    self.assertEqual(200, response.status)
                with self.assertRaises(urllib.error.URLError):
                    urllib.request.urlopen(f'https://127.0.0.1:{server.server_port}', context=client, timeout=3)
            finally:
                server.shutdown(); server.server_close(); thread.join(timeout=3)


if __name__ == '__main__':
    unittest.main()
