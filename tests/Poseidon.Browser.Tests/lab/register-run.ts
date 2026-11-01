import { chromium } from '@playwright/test';
import { writeFileSync, unlinkSync } from 'node:fs';
import { resolve } from 'node:path';
import { login } from './journeys.ts';
import { registerCases } from '../register-specs/cases.ts';

// Direct library execution of the SAME manifest used by drafts.spec.ts. Never
// serialize exceptions, DOM, network data, tokens or storage into diagnostics.
const directory = process.env.POSEIDON_LAB_PRIVATE!;
const writer = resolve(directory, 'register-writer.json');
const restricted = resolve(directory, 'register-restricted.json');
Object.assign(process.env, {
  POSEIDON_REGISTER_DIRECT: '1', POSEIDON_REGISTER_URL: 'https://localhost:5443',
  POSEIDON_REGISTER_WRITER_STATE: writer, POSEIDON_REGISTER_RESTRICTED_STATE: restricted,
});
const { default: config } = await import('../register.playwright.config.ts');
const selectedCases = registerCases.filter(entry => !process.env.POSEIDON_REGISTER_CASE || process.env.POSEIDON_REGISTER_CASE === entry.name);
const counts = { status: 'running', discovered: selectedCases.length * config.projects!.length,
  passed: 0, failed: 0, results: [] as { project: string; name: string; passed: boolean; milliseconds: number; line: number | null }[] };
const persist = () => writeFileSync(resolve(directory, 'register-counts.json'), JSON.stringify(counts));
persist();
let browser;
try {
  if (!selectedCases.length) throw new Error('Unknown case selection');
  browser = await chromium.launch();
  for (const [account, path] of [['lab-manager', writer], ['lab-restricted', restricted]]) {
    const context = await browser.newContext({ baseURL: config.use!.baseURL, ignoreHTTPSErrors: true });
    try {
      await login(await context.newPage(), account, '/app/today');
      // Private transient state comes only from genuine signed provider login.
      await context.storageState({ path });
    } finally { await context.close(); }
  }
  for (const project of config.projects!) {
    for (const entry of selectedCases) {
      const context = await browser.newContext({ ...project.use, baseURL: config.use!.baseURL,
        ignoreHTTPSErrors: true, storageState: entry.restricted ? restricted : writer });
      const started = Date.now();
      let passed = false; let line: number | null = null;
      try {
        const page = await context.newPage(); page.setDefaultTimeout(15_000);
        await entry.run(page); passed = true; counts.passed++;
      } catch (error) {
        counts.failed++;
        const match = error instanceof Error ? error.stack?.match(/cases\.ts:(\d+):/) : null;
        line = match ? Number(match[1]) : null;
      } finally {
        await context.close();
        counts.results.push({ project: project.name!, name: entry.name, passed, milliseconds: Date.now() - started, line });
        persist();
      }
    }
  }
} catch { counts.failed++; }
finally {
  await browser?.close();
  for (const path of [writer, restricted]) { try { unlinkSync(path); } catch { /* no state retained */ } }
  counts.status = counts.discovered > 0 && counts.passed === counts.discovered && counts.failed === 0 ? 'passed' : 'failed';
  persist();
}
process.exitCode = counts.status === 'passed' ? 0 : 1;
