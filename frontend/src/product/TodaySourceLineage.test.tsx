import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { decodeToday, decodeWhy } from './today-api'
import { TodayWorkspace } from './TodayWorkspace'

const vesselId = '30000000-0000-0000-0000-000000000001', other = '30000000-0000-0000-0000-000000000002'
const now = '2026-11-04T12:00:00Z'
const href = `/app/fleet/${vesselId}/profile`
const why = { vesselId, cardKey: 'regulatory-draft', rule: 'TW-DRAFT-01', reason: 'DraftUnverified',
  inputs: { known: 0, missing: 20, unknown: 0, reason: 'DraftUnverified' }, readAt: now, savedAt: now, sourceObservedAt: null,
  freshness: 'Unverified', provenance: 'Missing', blockers: ['VerificationUnavailable', 'OperationalSourcesUnavailable'],
  owner: null, nextAction: 'ReviewDraft', href }
const today = { vessel: { id: vesselId, companyId: other, code: 'SYN-1', name: 'FICTIONAL vessel', kind: 'Ship', version: 1, canEdit: true }, readAt: now,
  coverage: ['RegulatoryDraft', 'OperationalTasks', 'SafetyDecisions', 'Permits', 'EvidenceVerification', 'TechnicalMaintenance', 'TugPlanning'].map((source, i) => ({ source, status: i ? 'Unavailable' : 'Available' })),
  cards: { items: [{ key: 'regulatory-draft', priority: 'Advisory', source: 'RegulatoryDraft', reason: 'DraftUnverified', owner: null, nextAction: 'ReviewDraft', href }], offset: 0, limit: 25, hasMore: false } }
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

it('DEV-024 accepts a saved incomplete draft with Missing provenance without turning it into an absent or verified source', () => {
  expect(decodeWhy(why, vesselId)).toMatchObject({ reason: 'DraftUnverified', provenance: 'Missing', freshness: 'Unverified', savedAt: now })
  expect(decodeWhy({ ...why, source: { kind: 'RegulatoryDraft', id: vesselId, revision: 3 } }, vesselId).source?.revision).toBe(3)
})
it.each([
  { kind: 'RegulatoryDraft', id: other, revision: 3 },
  { kind: 'MaintenancePlan', id: vesselId, revision: 3 },
  { kind: 'RegulatoryDraft', id: vesselId, revision: null },
  { kind: 'RegulatoryDraft', id: vesselId, revision: 0 },
  { kind: 'RegulatoryDraft', id: vesselId, revision: 1.5 },
  { kind: 'RegulatoryDraft', id: vesselId, revision: 3, fileName: 'must-not-leak' }, null,
])('rejects wrong-scope, unknown, missing or malformed source lineage %#', source => {
  expect(() => decodeWhy({ ...why, source }, vesselId)).toThrow()
})
it('binds absent-draft lineage to the selected vessel with no invented revision', () => {
  const absent = { ...why, reason: 'DraftAbsent', inputs: { known: 0, missing: 0, unknown: 0, reason: 'DraftAbsent' },
    savedAt: null, blockers: ['DraftAbsent', 'OperationalSourcesUnavailable'], source: { kind: 'RegulatoryDraft', id: vesselId, revision: null } }
  expect(decodeWhy(absent, vesselId).source?.revision).toBeNull()
  expect(() => decodeWhy({ ...absent, source: { ...absent.source, revision: 1 } }, vesselId)).toThrow()
})
it('supports unavailable owning-profile coverage while rejecting a profile card that would disclose its hidden source', () => {
  const redacted = { ...today, coverage: today.coverage.map((c, i) => i ? c : { ...c, status: 'Unavailable' }), cards: { ...today.cards, items: [] } }
  expect(decodeToday(redacted, vesselId).cards.items).toEqual([])
  expect(() => decodeToday({ ...redacted, cards: today.cards }, vesselId)).toThrow()
})
it('DEV-023 shows explicit unavailable coverage and an honest empty page with no profile disclosure action', async () => {
  const data = { ...today, coverage: today.coverage.map(c => ({ ...c, status: 'Unavailable' })), cards: { ...today.cards, items: [] } }
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify(data))))
  render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  expect(await screen.findByText('Regulatory draft: Unavailable in this context')).toBeVisible()
  expect(screen.getByText(/No returned items/)).toHaveTextContent('does not mean all clear')
  expect(screen.queryByRole('button', { name: 'Why?' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Review draft' })).not.toBeInTheDocument()
})
it('discloses the exact authorized source revision with freshness and blockers only after a fresh Why read', async () => {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => new Response(JSON.stringify(url.includes('why-engine') ? { ...why, source: { kind: 'RegulatoryDraft', id: vesselId, revision: 3 } } : today))))
  const user = userEvent.setup(); render(<TodayWorkspace id={vesselId} language="en" offline={false} onNavigate={vi.fn()} />)
  await screen.findByRole('button', { name: 'Why?' })
  expect(screen.queryByText('Source revision', { selector: 'dt' })).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Why?' }))
  const term = await screen.findByText('Source revision', { selector: 'dt' })
  expect(term.nextElementSibling).toHaveTextContent('3')
  expect(screen.getByText('Source reference', { selector: 'dt' }).nextElementSibling).toHaveTextContent(vesselId)
  expect(screen.getByText(/Input freshness is unverified/)).toBeVisible()
  const disclosure = screen.getByRole('region', { name: 'Why?' })
  expect(within(disclosure).getByText('Verification source unavailable')).toBeVisible()
})
