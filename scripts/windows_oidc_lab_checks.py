"""Explicit Windows lab bootstrap/TLS checks; synthetic data only, no diagnostics dump."""
import http.server
from pathlib import Path
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
