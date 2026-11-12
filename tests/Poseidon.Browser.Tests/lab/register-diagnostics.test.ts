import { test } from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { publicFailure, responseLedger } from './register-diagnostics.ts';
const sourceCatalog = { root: 'C:/workspace/register-specs', files: { 'daily-cases.ts': Array<number>(200).fill(300) } };

test('failure metadata retains only known matcher and existing public source frame', () => {
  const marker = randomUUID();
  const root = resolve(import.meta.dirname, 'private', marker, 'register-specs');
  const sourcePath = resolve(root, 'daily-cases.ts');
  for (const location of [sourcePath, pathToFileURL(sourcePath).href]) {
    const error = { name: 'Error', message: marker, stack: `Error: ${marker}\n    at recorded (${location}:182:21)`,
      matcherResult: { name: 'toBeVisible', expected: marker, actual: marker, log: [marker] } };
    const result = publicFailure(error, { ...sourceCatalog, root });
    assert.deepEqual(result, { kind: 'EXPECTATION', matcher: 'toBeVisible', source: {
      file: 'tests/Poseidon.Browser.Tests/register-specs/daily-cases.ts', line: 182, column: 21, evidence: 'INFERRED_ERROR_STACK_FRAME' } });
    assert.equal(JSON.stringify(result).includes(marker), false);
  }
});

test('unknown errors, matchers and source files never become diagnostic strings', () => {
  const marker = randomUUID();
  const result = publicFailure({ name: marker, matcherResult: { name: marker },
    stack: `Error\n    at fn (file:///private/${marker}/register-specs/${marker}.ts:50:7)` }, sourceCatalog);
  assert.deepEqual(result, { kind: 'OTHER', matcher: null, source: null });
  assert.equal(JSON.stringify(result).includes(marker), false);
  assert.deepEqual(publicFailure(marker, sourceCatalog), result);
});

test('hostile error getters cannot leak values or break cleanup reporting', () => {
  const marker = randomUUID();
  const error = { name: 'TimeoutError', get matcherResult(): never { throw new Error(marker); } };
  const result = publicFailure(error, sourceCatalog);
  assert.deepEqual(result, { kind: 'TimeoutError', matcher: null, source: null });
  assert.equal(JSON.stringify(result).includes(marker), false);
  const messageNeverRead = { name: 'TypeError', get message(): never { throw new Error(marker); }, stack: '' };
  assert.deepEqual(publicFailure(messageNeverRead, sourceCatalog), { kind: 'TypeError', matcher: null, source: null });
});

test('valid-shaped message frames and foreign or out-of-range locations are not source evidence', () => {
  const message = `Failure\n    at forged (${sourceCatalog.root}/daily-cases.ts:99:1)`;
  for (const frame of [`    at fn (${sourceCatalog.root}/daily-cases.ts:201:7)`,
    `    at fn (${sourceCatalog.root}/daily-cases.ts:42:302)`,
    `    at fn (${sourceCatalog.root}/daily-cases.ts:0:7)`,
    '    at fn (C:/foreign/register-specs/daily-cases.ts:42:1)',
    `not-a-frame ${sourceCatalog.root}/daily-cases.ts:42:1`]) {
    assert.equal(publicFailure({ name: 'Error', message, stack: `Error: ${message}\n${frame}` }, sourceCatalog).source, null);
  }
  const result = publicFailure({ name: 'Error', message, stack: `Error: ${message}\n    at real (${sourceCatalog.root}/daily-cases.ts:42:1)` }, sourceCatalog);
  assert.equal(result.source?.line, 42);
  assert.equal(result.source?.evidence, 'INFERRED_ERROR_STACK_FRAME');
});

test('passive ledger exposes only same-origin static API templates, methods and statuses', () => {
  const marker = randomUUID();
  const origin = 'https://localhost:15443';
  const ledger = responseLedger(origin, { '/api/v1/vessels/{vesselId}/offline/enrollments': { post: {} } });
  ledger.observe(`${origin}/api/v1/vessels/${marker}/offline/enrollments?private=${marker}`, 'POST', 403);
  ledger.observe(`${origin}/api/v1/vessels/${marker}/offline/enrollments?changed=${marker}`, 'POST', 403);
  ledger.observe(`https://localhost:18443/realms/${marker}`, 'GET', 200);
  ledger.observe(`https://${marker}:${marker}@localhost:15443/api/v1/vessels/${marker}/offline/enrollments`, 'POST', 200);
  ledger.observe(`${origin}/api/v1/${marker}`, 'GET', 404);
  const result = ledger.snapshot();
  assert.deepEqual(result, { scope: 'MAIN_CASE_CONTEXT_ONLY', responses: [{
    route: '/api/v1/vessels/{vesselId}/offline/enrollments', method: 'POST', status: 403, count: 2 }], ignored: 3, overflow: 0 });
  assert.equal(JSON.stringify(result).includes(marker), false);
});

test('literal routes take precedence and invalid observations do not create fake outcomes', () => {
  const origin = 'https://localhost:15443';
  const ledger = responseLedger(origin, { '/api/v1/items/{id}': { get: {} }, '/api/v1/items/search': { get: {} } });
  ledger.observe(`${origin}/api/v1/items/search`, 'GET', 200);
  for (const [url, method, status] of [[`${origin}/api/v1/items/one`, 'post', 200],
    [`${origin}/api/v1/items/one`, 'POST', 200], ['malformed', 'GET', 200],
    [`${origin}/api/v1/items/one`, 'GET', 0], [`${origin}/api/v1/items/one`, 'GET', 200.5]] as const)
    ledger.observe(url, method, status);
  assert.deepEqual(ledger.snapshot(), { scope: 'MAIN_CASE_CONTEXT_ONLY', responses: [{
    route: '/api/v1/items/search', method: 'GET', status: 200, count: 1 }], ignored: 5, overflow: 0 });
});

test('bounded ledger preserves existing counts and signals overflow without arbitrary values', () => {
  const origin = 'https://localhost:15443';
  const paths = { '/api/v1/items/{id}': { get: {} } };
  const ledger = responseLedger(origin, paths, 1);
  ledger.observe(`${origin}/api/v1/items/one`, 'GET', 200);
  ledger.observe(`${origin}/api/v1/items/two`, 'GET', 403);
  ledger.observe(`${origin}/api/v1/items/three`, 'GET', 200);
  const first = ledger.snapshot();
  assert.equal(first.overflow, 1);
  assert.equal(first.responses[0].count, 2);
  first.responses[0].count = 99;
  assert.equal(ledger.snapshot().responses[0].count, 2);
  for (const limit of [0, -1, 1.5, 257]) assert.throws(() => responseLedger(origin, paths, limit), /invalid_diagnostic_limit/);
  for (const invalidOrigin of ['http://localhost:15443', origin + '/', origin + '/private'])
    assert.throws(() => responseLedger(invalidOrigin, paths), /invalid_diagnostic_origin/);
});

test('actual complete pinned OpenAPI accepts API telemetry and excludes legitimate health routes', () => {
  const contract = JSON.parse(readFileSync(resolve(import.meta.dirname, '../../../contracts/openapi.v1.json'), 'utf8'));
  assert.equal(Object.hasOwn(contract.paths, '/health/live'), true);
  const origin = 'https://localhost:15443';
  const ledger = responseLedger(origin, contract.paths);
  ledger.observe(`${origin}/api/v1/auth/session`, 'GET', 200);
  ledger.observe(`${origin}/health/live`, 'GET', 200);
  assert.deepEqual(ledger.snapshot(), { scope: 'MAIN_CASE_CONTEXT_ONLY', responses: [{
    route: '/api/v1/auth/session', method: 'GET', status: 200, count: 1 }], ignored: 1, overflow: 0 });
  for (const malformed of [{ '/api/v1/items': null }, { '/health/live': [] }, { 'invalid-relative-route': { get: {} } }])
    assert.throws(() => responseLedger(origin, malformed), /invalid_diagnostic_contract/);
});
