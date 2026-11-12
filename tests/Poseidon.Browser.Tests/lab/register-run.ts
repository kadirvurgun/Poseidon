import { labRuntimeFromEnvironment } from './lab-runtime.ts';
import { tlsCases } from './tls-cases.ts';
import { strictContext } from './strict-browser.ts';
import { writeFileSync, readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { login } from './journeys.ts';
import { registerCases } from '../register-specs/cases.ts';
import { loadRegisterLaneFixture, registerLaneEnvironment } from './register-lane-fixture.ts';
import { runSignedCase, type SignedCasePhase } from './signed-case.ts';
import { publicFailure, responseLedger, type PublicFailure } from './register-diagnostics.ts';

// Direct library execution of the SAME manifest used by drafts.spec.ts. Never
// serialize exceptions, DOM, network data, tokens or storage into diagnostics.
const directory = process.env.POSEIDON_LAB_PRIVATE!;
const runtime = labRuntimeFromEnvironment();
Object.assign(process.env, {
  POSEIDON_REGISTER_DIRECT: '1', POSEIDON_REGISTER_URL: runtime.appOrigin,
  POSEIDON_REGISTER_AUTHENTICATION: 'GenuinePerCase',
});
const { default: config } = await import('../register.playwright.config.ts');
const selectedCases = registerCases.filter(entry => !process.env.POSEIDON_REGISTER_CASE || process.env.POSEIDON_REGISTER_CASE === entry.name);
const selectedProjects = config.projects!.filter(project => !process.env.POSEIDON_REGISTER_PROJECT || process.env.POSEIDON_REGISTER_PROJECT === project.name);
const diagnosticSourceRoot = resolve(import.meta.dirname, '../register-specs');
const diagnosticSources = { root: diagnosticSourceRoot, files: Object.fromEntries(readdirSync(diagnosticSourceRoot)
  .filter(file => /^[a-z][a-z0-9-]*\.ts$/.test(file)).map(file => [file,
    readFileSync(resolve(diagnosticSourceRoot, file), 'utf8').split(/\r?\n/).map(line => line.length)])) };
const diagnosticPaths = JSON.parse(readFileSync(resolve(import.meta.dirname, '../../../contracts/openapi.v1.json'), 'utf8')).paths;
const diagnostics = { schemaVersion: 'poseidon.signed-register-diagnostics.v1', setupFailure: null as PublicFailure | null,
  results: [] as { project: string; name: string; phase: SignedCasePhase; failure: PublicFailure;
    network: ReturnType<ReturnType<typeof responseLedger>['snapshot']> }[] };
const counts = { status: 'running', discovered: selectedCases.length * selectedProjects.length,
  passed: 0, failed: 0, results: [] as { project: string; name: string; passed: boolean; milliseconds: number; line: number | null; phase: SignedCasePhase }[] };
const persist = () => {
  writeFileSync(resolve(directory, 'register-counts.json'), JSON.stringify(counts));
  writeFileSync(resolve(directory, 'register-diagnostics.json'), JSON.stringify(diagnostics));
};
persist();
try {
  const laneFixture = loadRegisterLaneFixture(process.env);
  await tlsCases();
  if (!selectedCases.length || !selectedProjects.length) throw new Error('Unknown case or project selection');
  for (const project of selectedProjects) {
    Object.assign(process.env, registerLaneEnvironment(laneFixture, project.use?.viewport?.width));
    for (const entry of selectedCases) {
      // Validate the static contract before acquiring any disposable profile.
      const network = responseLedger(runtime.appOrigin, diagnosticPaths);
      const owned = await strictContext({ ...project.use, baseURL: config.use!.baseURL });
      const { context } = owned;
      const started = Date.now();
      let passed = false; let line: number | null = null;
      let phase: SignedCasePhase = 'LOGIN';
      try {
        context.on('response', response => {
          // Passive metadata only, scoped to this context. Peer contexts are not
          // implicitly observed, and no request/response body/header is accessed.
          try { network.observe(response.url(), response.request().method(), response.status()); }
          catch { network.observe('', '', 0); }
        });
        const page = await context.newPage(); page.setDefaultTimeout(15_000);
        await runSignedCase(page, entry, login, value => { phase = value }); passed = true; counts.passed++;
      } catch (error) {
        counts.failed++;
        diagnostics.results.push({ project: project.name!, name: entry.name, phase,
          failure: publicFailure(error, diagnosticSources), network: network.snapshot() });
        const match = error instanceof Error ? error.stack?.match(/cases\.ts:(\d+):/) : null;
        line = match ? Number(match[1]) : null;
      } finally {
        await owned.close();
        counts.results.push({ project: project.name!, name: entry.name, passed, milliseconds: Date.now() - started, line, phase });
        persist();
      }
    }
  }
} catch (error) { counts.failed++; diagnostics.setupFailure = publicFailure(error, diagnosticSources); }
finally {
  counts.status = counts.discovered > 0 && counts.passed === counts.discovered && counts.failed === 0 ? 'passed' : 'failed';
  persist();
}
process.exitCode = counts.status === 'passed' ? 0 : 1;
