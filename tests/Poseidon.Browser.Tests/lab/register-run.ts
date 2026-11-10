import { tlsCases } from './tls-cases.ts';
import { strictContext } from './strict-browser.ts';
import { writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { login } from './journeys.ts';
import { registerCases } from '../register-specs/cases.ts';
import { loadRegisterLaneFixture, registerLaneEnvironment } from './register-lane-fixture.ts';
import { runSignedCase, type SignedCasePhase } from './signed-case.ts';

// Direct library execution of the SAME manifest used by drafts.spec.ts. Never
// serialize exceptions, DOM, network data, tokens or storage into diagnostics.
const directory = process.env.POSEIDON_LAB_PRIVATE!;
Object.assign(process.env, {
  POSEIDON_REGISTER_DIRECT: '1', POSEIDON_REGISTER_URL: 'https://localhost:5443',
  POSEIDON_REGISTER_AUTHENTICATION: 'GenuinePerCase',
});
const { default: config } = await import('../register.playwright.config.ts');
const selectedCases = registerCases.filter(entry => !process.env.POSEIDON_REGISTER_CASE || process.env.POSEIDON_REGISTER_CASE === entry.name);
const selectedProjects = config.projects!.filter(project => !process.env.POSEIDON_REGISTER_PROJECT || process.env.POSEIDON_REGISTER_PROJECT === project.name);
const counts = { status: 'running', discovered: selectedCases.length * selectedProjects.length,
  passed: 0, failed: 0, results: [] as { project: string; name: string; passed: boolean; milliseconds: number; line: number | null; phase: SignedCasePhase }[] };
const persist = () => writeFileSync(resolve(directory, 'register-counts.json'), JSON.stringify(counts));
persist();
try {
  const laneFixture = loadRegisterLaneFixture(process.env);
  await tlsCases();
  if (!selectedCases.length || !selectedProjects.length) throw new Error('Unknown case or project selection');
  for (const project of selectedProjects) {
    Object.assign(process.env, registerLaneEnvironment(laneFixture, project.use?.viewport?.width));
    for (const entry of selectedCases) {
      const owned = await strictContext({ ...project.use, baseURL: config.use!.baseURL });
      const { context } = owned;
      const started = Date.now();
      let passed = false; let line: number | null = null;
      let phase: SignedCasePhase = 'LOGIN';
      try {
        const page = await context.newPage(); page.setDefaultTimeout(15_000);
        await runSignedCase(page, entry, login, value => { phase = value }); passed = true; counts.passed++;
      } catch (error) {
        counts.failed++;
        const match = error instanceof Error ? error.stack?.match(/cases\.ts:(\d+):/) : null;
        line = match ? Number(match[1]) : null;
      } finally {
        await owned.close();
        counts.results.push({ project: project.name!, name: entry.name, passed, milliseconds: Date.now() - started, line, phase });
        persist();
      }
    }
  }
} catch { counts.failed++; }
finally {
  counts.status = counts.discovered > 0 && counts.passed === counts.discovered && counts.failed === 0 ? 'passed' : 'failed';
  persist();
}
process.exitCode = counts.status === 'passed' ? 0 : 1;
