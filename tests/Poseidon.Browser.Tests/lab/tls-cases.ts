import { createServer } from 'node:https';
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { strictContext } from './strict-browser.ts';

// Actual browser navigation and real TLS handshakes. No routes/proxies/fulfill,
// no auth, no error text or browser artifacts in the persisted result.
export async function tlsCases() {
  const directory = process.env.POSEIDON_LAB_PRIVATE!;
  const results: { code: string; passed: boolean }[] = [];
  async function reject(code: string, url: string, trust: boolean, expected: string) {
    const owned = await strictContext({}, trust);
    let passed = false;
    try {
      await (await owned.context.newPage()).goto(url, { timeout: 15000 });
    } catch (error) {
      passed = error instanceof Error && error.message.includes(expected);
    } finally { await owned.close(); }
    results.push({ code, passed });
  }
  const server = createServer({ key: readFileSync(resolve(directory, 'tls.key')), cert: readFileSync(resolve(directory, 'tls.pem')) }, (_, response) => response.end('TLS fixture'));
  try {
    await reject('app_untrusted_ca', 'https://localhost:5443/health/live', false, 'SEC_ERROR_UNKNOWN_ISSUER');
    await reject('provider_untrusted_ca', 'https://localhost:8443/realms/poseidon-lab/.well-known/openid-configuration', false, 'SEC_ERROR_UNKNOWN_ISSUER');
    await new Promise<void>((ok, fail) => { server.once('error', fail); server.listen(0, '127.0.0.2', ok); });
    const address = server.address();
    if (!address || typeof address === 'string') throw new Error('tls_fixture_address');
    await reject('trusted_ca_wrong_hostname', `https://127.0.0.2:${address.port}/`, true, 'SSL_ERROR_BAD_CERT_DOMAIN');
  } finally {
    if (server.listening) await new Promise<void>(ok => server.close(() => ok()));
    writeFileSync(resolve(directory, 'tls-counts.json'), JSON.stringify({ discovered: 3, passed: results.filter(r => r.passed).length, failed: 3 - results.filter(r => r.passed).length, skipped: 0, results }));
  }
  if (results.length !== 3 || results.some(r => !r.passed)) throw new Error('tls_rejection_gate');
}
