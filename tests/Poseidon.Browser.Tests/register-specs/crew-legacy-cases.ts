import { expect, type Page } from '@playwright/test'

// Genuine signed HTTPS host + CSRF + PostgreSQL only. No auth bypass, route mocks,
// plaintext upload/conversion backdoor, historic fake consent, or provider execution here.
// Bundle-owned disposable fixture must provision a fictional plaintext old batch through
// attributed EF before running; only its nonprivate UUID is supplied to this journey.
async function legacyProtection(page: Page) {
  const batch = process.env.POSEIDON_BROWSER_LEGACY_CREW_BATCH
  if (!batch || !/^[0-9a-f-]{36}$/i.test(batch)) throw new Error('legacy_crew_fixture_uuid_required')
  const sessionResponse = await page.request.get('/api/v1/auth/session'); expect(sessionResponse.status()).toBe(200)
  const session = await sessionResponse.json()
  const vessel = process.env.POSEIDON_BROWSER_LEGACY_CREW_VESSEL
  if (!vessel || !/^[0-9a-f-]{36}$/i.test(vessel)) throw new Error('legacy_crew_fixture_vessel_uuid_required')
  const root = `/api/v1/vessels/${vessel}/imports/${batch}`, path = root + '/legacy-protection'
  const before = await page.request.get(path); expect(before.status()).toBe(200)
  const status = await before.json()
  expect(status.storageState === 'LegacyUnavailable' && status.interpretationState === 'Unavailable').toBe(true)
  expect(!('rowCount' in status) && !('sourceHash' in status) && !('headers' in status) && !('tableJson' in status)).toBe(true)
  const command = { baseVersion: status.version, reason: 'ProtectHistoricalStorage' }
  const headers = { 'X-CSRF-TOKEN': session.csrfToken as string, 'Idempotency-Key': crypto.randomUUID() }
  const converted = await page.request.post(path, { headers, data: command }); expect(converted.status()).toBe(200)
  const protectedState = await converted.json()
  expect(protectedState.storageState === 'ProtectedHistoricalV1' && protectedState.originalCaptureAuthority === 'Unknown' && protectedState.interpretationState === 'Unavailable').toBe(true)
  const replay = await page.request.post(path, { headers, data: command }); expect(replay.status()).toBe(200)
  const recovered = await replay.json(); expect(recovered.replayed === true && recovered.version === protectedState.version).toBe(true)
  const stale = await page.request.post(path, { headers: { ...headers, 'Idempotency-Key': crypto.randomUUID() }, data: command })
  expect(stale.status()).toBe(409)
  expect((await page.request.get(root)).status()).toBe(404)
  expect((await page.request.post(root + '/preview', { headers, data: {} })).status()).toBe(404)
  await page.goto(`/app/excel-migration/${vessel}?batch=${batch}`)
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByText('Import access denied.', { exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Commit atomic trial import', exact: true })).toHaveCount(0)
}
export const crewLegacyCases = [
  { name: 'crew-legacy-storage-conversion-exact-retry-retained-unsupported-schema', run: legacyProtection, restricted: false },
] as const
