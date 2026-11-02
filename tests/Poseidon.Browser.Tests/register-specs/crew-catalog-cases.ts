import { expect, type Page } from '@playwright/test'

async function independentCatalog(page: Page) {
  // Each context logs in through the real configured provider. No synthetic storageState,
  // copied cookies, elevated IdP claims or direct database positive fixtures.
  const { strictContext } = await import('../lab/strict-browser.ts')
  const { login } = await import('../lab/journeys.ts')
  const vessel = '30000000-0000-0000-0000-000000000001'
  const root = `/api/v1/vessels/${vessel}/crew/position-catalog`
  const session = await page.request.get('/api/v1/auth/session'); expect(session.status()).toBe(200)
  const writer = await session.json()
  const peer = await strictContext({ baseURL: 'https://localhost:5443' })
  try {
    const reviewerPage = await peer.context.newPage()
    await login(reviewerPage, 'lab-reviewer', '/app/today')
    const reviewer = await (await peer.context.request.get('/api/v1/auth/session')).json()
    expect(reviewer.authenticated).toBe(true); expect(reviewer.actorId !== writer.actorId).toBe(true)
    expect(reviewer.tenantId === writer.tenantId).toBe(true)
    await page.goto(`/app/crew-roster/${vessel}`)
    await page.getByRole('button', { name: 'EN', exact: true }).click()
    await page.getByRole('button', { name: 'Position catalog governance', exact: true }).click()
    const region = page.getByRole('region', { name: 'Position catalog governance', exact: true })
    const policy = { version: 'FICTIONAL-catalog-' + crypto.randomUUID(), sourceReference: 'FICTIONAL signed-host DEV-047 catalog; software fixture only',
      timeZone: 'UTC', periodSemantics: 'StartInclusiveEndExclusive', positions: [{ tenantId: writer.tenantId, vesselId: vessel,
        positionId: crypto.randomUUID(), code: 'FICTIONAL-' + crypto.randomUUID(), rank: 'FICTIONAL Rank', capacity: null,
        department: null, provenance: 'SyntheticUnverified', from: null, to: null }] }
    await region.getByLabel('Reason', { exact: true }).fill('FICTIONAL source proposal for software regression only')
    await region.getByLabel('Source-defined catalog proposal (JSON)', { exact: true }).fill(JSON.stringify(policy))
    const [proposal] = await Promise.all([page.waitForResponse(r => new URL(r.url()).pathname === root + '/events' && r.request().method() === 'POST'), region.getByRole('button', { name: 'Propose revision', exact: true }).click()])
    expect(proposal.status()).toBe(200)
    async function record(state: 'Reviewed' | 'Adopted' | 'Withdrawn', author = false) {
      const client = author ? page.request : peer.context.request
      const before = await client.get(root); expect(before.status()).toBe(200)
      const catalog = await before.json(), action = { Reviewed: 'review', Adopted: 'adopt', Withdrawn: 'withdraw' }[state]
      const authority = catalog.actions.filter((a: { action: string }) => a.action === action); expect(authority.length).toBe(1)
      return client.post(root + '/events', { headers: { 'X-CSRF-TOKEN': author ? writer.csrfToken : reviewer.csrfToken, 'Idempotency-Key': crypto.randomUUID() },
        data: { expectedVersion: catalog.version, authorityId: authority[0].authorityId, state, reason: 'FICTIONAL independent catalog command; no safety/source acceptance' } })
    }
    expect((await record('Reviewed', true)).status()).toBe(409)
    expect((await record('Reviewed')).status()).toBe(200)
    expect((await record('Adopted', true)).status()).toBe(403)
    expect((await record('Adopted')).status()).toBe(200)
    const current = await page.request.get(`/api/v1/vessels/${vessel}/crew/position-policy`); expect(current.status()).toBe(200)
    const lookup = await current.json(); expect(lookup.version).toBe(policy.version); expect(lookup.items.some((p: { id: string }) => p.id === policy.positions[0].positionId)).toBe(true)
    expect((await record('Withdrawn')).status()).toBe(200)
    const withdrawn = await (await page.request.get(`/api/v1/vessels/${vessel}/crew/position-policy`)).json()
    expect(withdrawn.version).toBe(null); expect(withdrawn.items.length).toBe(0)
  } finally { await peer.close() }
}
export const crewCatalogCases = [{ name: 'crew-durable-catalog-independent-signed-humans-current-consumer-withdrawal', run: independentCatalog, restricted: false }] as const
