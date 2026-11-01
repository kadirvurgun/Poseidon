import { firefox, type BrowserContext, type BrowserContextOptions } from '@playwright/test';
import { mkdtempSync, mkdirSync, readFileSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, relative, resolve, sep } from 'node:path';
import { spawnSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';

// Every browser gets a fresh NSS profile; only this owned CA is added. Firefox
// keeps normal hostname, chain, expiry and EKU verification. No browser error
// overrides, route substitutions, host/user trust mutation or client certs.
export async function strictContext(options: BrowserContextOptions = {}, trust = true): Promise<{ context: BrowserContext; close: () => Promise<void> }> {
  const privateRoot = realpathSync(process.env.POSEIDON_LAB_PRIVATE!);
  const expected = realpathSync(resolve(import.meta.dirname, '../../..', '.runtime/oidc-lab'));
  if (privateRoot !== expected || options.ignoreHTTPSErrors) throw new Error('invalid_lab_boundary');
  const directory = mkdtempSync(resolve(privateRoot, 'strict-profile-'));
  const owner = randomUUID();
  writeFileSync(resolve(directory, '.owner'), owner, { flag: 'wx' });
  const profile = resolve(directory, 'profile'); mkdirSync(profile);
  const cleanup = () => {
    const target = realpathSync(directory);
    const child = relative(privateRoot, target);
    if (target !== directory || !child || child.startsWith('..' + sep) || isAbsolute(child) || dirname(target) !== privateRoot || readFileSync(resolve(target, '.owner'), 'utf8') !== owner) throw new Error('cleanup_ownership');
    rmSync(target, { recursive: true });
  };
  try {
    if (trust) {
      const result = spawnSync(process.env.POSEIDON_LAB_PYTHON!, [resolve(import.meta.dirname, '../../../scripts/lab_profile_ca.py'), profile, firefox.executablePath(), owner], { stdio: 'ignore', windowsHide: true });
      if (result.status !== 0) throw new Error('profile_ca_unavailable');
    }
    const context = await firefox.launchPersistentContext(profile, {
      ...options, ignoreHTTPSErrors: false,
      firefoxUserPrefs: { 'security.enterprise_roots.enabled': false, 'security.certerrors.mitm.auto_enable_enterprise_roots': false },
    });
    return { context, close: async () => { try { await context.close(); } finally { cleanup(); } } };
  } catch { cleanup(); throw new Error('strict_profile_setup'); }
}
