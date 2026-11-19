import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { DailyEngineReferencePanel } from './DailyEngineReferencePanel'
import { navigationTestIdentity } from './workspace-navigation.test-fixture'
import { dailyActor, dailyRecordFixture, dailyVessel } from './daily-test-fixtures'
import { engineReferenceSource, owningSources } from './daily-engine-reference-api'

const wo = '10000000-0000-4000-8000-000000000009', eventId = '10000000-0000-4000-8000-00000000000a'
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const identity = () => ({ ...navigationTestIdentity, authenticated: true, setupRequired: false, canReadCompanies: false, canCreate: false, canSearch: false, canRead: true, actorId: dailyActor, tenantId: dailyVessel, csrfToken: 'synthetic-engine-reference' })
const record = () => dailyRecordFixture('Engine')
const pin = { kind: 'WorkOrder', id: wo, revision: 1, version: 3, hash: 'b'.repeat(64), sourceVersion: owningSources.WorkOrder }
const refs = (items: unknown[] = [], canLink = true, defect = 'UNAVAILABLE') => ({ recordId: record().id, vesselId: dailyVessel, recordRevision: record().revision, recordVersion: record().version,
  referenceVersion: items.length ? 4 : 0, sequence: items.length, canLink, sources: [{ kind: 'WorkOrder', state: 'AVAILABLE' }, { kind: 'Defect', state: defect }], items, offline: 'ONLINE_ONLY', sourceVersion: engineReferenceSource })
const linked = { kind: 'WorkOrder', pin, sequence: 1, state: 'LINKED_CURRENT', qualification: 'REVIEW_REQUIRED', label: 'FICTIONAL-WO-7', href: `/app/work-order-center/${dailyVessel}?id=${wo}`, reason: null }
const props = () => ({ owner: `${dailyVessel}/${dailyActor}`, session: identity(), record: record(), language: 'en' as const, enabled: true })
function transport(state: { refs: unknown; write?: (init: RequestInit) => Promise<Response> }) {
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    if (url.endsWith('/auth/session')) return json(identity())
    if (init?.method === 'POST') return state.write ? state.write(init) : json({})
    if (url.includes('/reference-candidates?')) return json({ vesselId: dailyVessel, kind: 'WorkOrder', offset: 0, limit: 25, hasMore: false,
      items: [{ pin, label: 'FICTIONAL-WO-7', detail: null, provenance: 'UserDeclaredUnverified', savedAt: '2026-11-19T10:00:00Z', qualification: 'REVIEW_REQUIRED', href: `/app/work-order-center/${dailyVessel}?id=${wo}` }] })
    return json(state.refs)
  })
  vi.stubGlobal('fetch', fetch); return fetch
}
beforeEach(() => { vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true) })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('DEV-099 / T-OPS2-003 engine log WO/defect links (authored)', () => {
  it('links an existing readable work order by exact pinned revision instead of free text', async () => {
    const state: { refs: unknown; write?: (init: RequestInit) => Promise<Response> } = { refs: refs() }
    const sent: RequestInit[] = []
    state.write = async init => { sent.push(init); state.refs = refs([linked]); return json({ id: eventId, recordId: record().id, vesselId: dailyVessel, sequence: 1, referenceVersion: 4, recordRevision: 1, operation: 'LINK', target: pin, actorId: dailyActor, savedAt: '2026-11-19T10:00:00Z', qualification: 'REVIEW_REQUIRED', sourceVersion: engineReferenceSource }) }
    transport(state); const user = userEvent.setup(); render(<DailyEngineReferencePanel {...props()} />)
    expect(await screen.findByText('No work order or defect is linked.')).toBeVisible()
    expect(screen.getByText(/Defect: lookup unavailable — UNKNOWN/)).toBeVisible()
    await user.selectOptions(screen.getByLabelText('Record type'), 'WorkOrder')
    await user.selectOptions(await screen.findByLabelText('Record'), wo)
    await user.click(screen.getByRole('button', { name: 'Link' }))
    expect(await screen.findByText('Link change recorded.')).toBeVisible()
    const body = JSON.parse(sent[0].body as string)
    expect(body).toEqual({ expectedRecordVersion: record().version, recordRevision: record().revision, expectedReferenceVersion: 0, operation: 'LINK', target: pin })
    expect(new Headers(sent[0].headers).get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/)
    expect(await screen.findByRole('link', { name: 'FICTIONAL-WO-7' })).toHaveAttribute('href', `/app/work-order-center/${dailyVessel}?id=${wo}`)
    expect(screen.getByText(/review required/)).toBeVisible()
    expect(screen.queryByText(/verified|approved|closed/i)).not.toBeInTheDocument()
  })
  it('shows unreadable or superseded links as UNKNOWN / changed, never as current', async () => {
    transport({ refs: refs([{ kind: 'Defect', pin: null, sequence: 1, state: 'UNKNOWN', qualification: 'REVIEW_REQUIRED', label: null, href: null, reason: 'REFERENCE_NOT_READABLE' },
      { ...linked, sequence: 1, state: 'LINKED_SUPERSEDED', reason: 'REFERENCE_CHANGED_SINCE_LINK' }].map((x, i) => ({ ...x, sequence: i + 1 }))) })
    render(<DailyEngineReferencePanel {...props()} />)
    expect((await screen.findAllByText('UNKNOWN')).length).toBeGreaterThan(0)
    expect(screen.getByText('Newer revision exists')).toBeVisible()
    expect(screen.getByText(/the linked record has changed since it was linked/)).toBeVisible()
    expect(screen.queryByText('Current revision')).not.toBeInTheDocument()
  })
  it('retains the exact command after an unknown outcome and retries it with the same key', async () => {
    const keys: string[] = [], bodies: string[] = []; let attempt = 0
    const state: { refs: unknown; write?: (init: RequestInit) => Promise<Response> } = { refs: refs() }
    state.write = async init => { keys.push(new Headers(init.headers).get('Idempotency-Key')!); bodies.push(init.body as string); attempt++
      if (attempt === 1) return json({ code: 'service_unavailable' }, 503)
      state.refs = refs([linked]); return json({ id: eventId, recordId: record().id, vesselId: dailyVessel, sequence: 1, referenceVersion: 4, recordRevision: 1, operation: 'LINK', target: pin, actorId: dailyActor, savedAt: '2026-11-19T10:00:00Z', qualification: 'REVIEW_REQUIRED', sourceVersion: engineReferenceSource }) }
    transport(state); const user = userEvent.setup(); render(<DailyEngineReferencePanel {...props()} />)
    await user.selectOptions(await screen.findByLabelText('Record type'), 'WorkOrder')
    await user.selectOptions(await screen.findByLabelText('Record'), wo)
    await user.click(screen.getByRole('button', { name: 'Link' }))
    expect(await screen.findByText(/The outcome is unknown/)).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Retry same change' }))
    await waitFor(() => expect(keys).toHaveLength(2))
    expect(keys[1]).toBe(keys[0]); expect(bodies[1]).toBe(bodies[0])
  })
  it('stays read-only without link authority and withdraws content offline', async () => {
    transport({ refs: refs([linked], false) })
    const view = render(<DailyEngineReferencePanel {...props()} />)
    expect(await screen.findByText('You cannot change links on this log entry.')).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Remove link' })).not.toBeInTheDocument()
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    view.rerender(<DailyEngineReferencePanel {...props()} />)
    expect(screen.getByText(/You are offline/)).toBeVisible()
    expect(screen.queryByText('FICTIONAL-WO-7')).not.toBeInTheDocument()
  })
})
