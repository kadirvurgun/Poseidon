import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { changeAppLanguage } from '../i18n'
import { ApiError } from './api'
import { DocumentCompareWorkspace, readCompareRoute } from './DocumentCompareWorkspace'
import { VoiceEventDraftWorkspace, readVoiceRoute } from './VoiceEventDraftWorkspace'
import { compareRevisions, decodeDocument, decodeVoiceDraft, freezeS9Command } from './s9-wpc-api'
import { documentFixture, documentListFixture, s9Actor, s9Document, s9Draft, s9Tenant, s9Vessel, voiceCapsFixture, voiceDraftFixture, voiceMetricsFixture,
  voicePageFixture } from './s9-wpc-test-fixtures'

const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status })
type Handler = (path: string, init?: RequestInit) => Response | undefined
function transport(handler: Handler) {
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    const path = new URL(url, 'https://synthetic.invalid').pathname
    if (path === '/api/v1/auth/session') return json({ authenticated: true, canRead: true, actorId: s9Actor, tenantId: s9Tenant, csrfToken: 'synthetic-s9-csrf' })
    return handler(path, init) ?? json({ code: 'permission_denied' }, 403)
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}
beforeEach(async () => { vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true); await changeAppLanguage('en', false) })
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('S9 WP-C wire decoding', () => {
  it('rejects a confirmation without its human record and unknown document policies', () => {
    expect(decodeVoiceDraft(voiceDraftFixture(), s9Vessel, s9Draft).id).toBe(s9Draft)
    expect(() => decodeVoiceDraft(voiceDraftFixture({ state: 'CONFIRMED', handoff: 'PENDING_EVENT_DRAFT_INTEGRATION', canEdit: false }), s9Vessel)).toThrow(ApiError)
    // S9 integration: a confirmed hand-off is either a linked event draft or pending with a closed reason; never both, never neither.
    const human = { state: 'CONFIRMED', confirmedBy: s9Actor, confirmedAt: '2026-11-20T09:00:00Z', corrections: [], canEdit: false, canConfirm: false }
    const event = '00000000-0000-4000-8000-0000000600e1'
    expect(decodeVoiceDraft(voiceDraftFixture({ ...human, handoff: 'EVENT_DRAFT_CREATED', safetyEventId: event }), s9Vessel).safetyEventId).toBe(event)
    expect(decodeVoiceDraft(voiceDraftFixture({ ...human, handoff: 'PENDING_EVENT_DRAFT_INTEGRATION', handoffReason: 'SAFETY_EVENT_REPORT_PERMISSION_REQUIRED' }), s9Vessel).handoffReason)
      .toBe('SAFETY_EVENT_REPORT_PERMISSION_REQUIRED')
    expect(() => decodeVoiceDraft(voiceDraftFixture({ ...human, handoff: 'EVENT_DRAFT_CREATED' }), s9Vessel)).toThrow(ApiError)
    expect(() => decodeVoiceDraft(voiceDraftFixture({ ...human, handoff: 'PENDING_EVENT_DRAFT_INTEGRATION' }), s9Vessel)).toThrow(ApiError)
    expect(() => decodeVoiceDraft(voiceDraftFixture({ ...human, handoff: 'PENDING_EVENT_DRAFT_INTEGRATION', safetyEventId: event, handoffReason: 'SAFETY_EVENT_REPORT_PERMISSION_REQUIRED' }), s9Vessel)).toThrow(ApiError)
    expect(() => decodeVoiceDraft(voiceDraftFixture({ safetyEventId: event }), s9Vessel)).toThrow(ApiError)
    expect(() => decodeVoiceDraft(voiceDraftFixture({ audioRetention: 'RETAINED' }), s9Vessel)).toThrow(ApiError)
    expect(() => decodeDocument(documentFixture({ contentPolicy: { ...documentFixture().contentPolicy, download: 'MAYBE' } }), s9Vessel, s9Document)).toThrow(ApiError)
    expect(() => decodeDocument(documentFixture({ classification: 'INTERNAL' }), s9Vessel, s9Document)).toThrow(ApiError)
  })
  it('compares metadata locally and states bytes only, never a review', () => {
    const doc = documentFixture()
    const result = compareRevisions(doc.revisions[0] as never, doc.revisions[1] as never)
    expect(result.state).toBe('READY'); expect(result.bytes).toBe('BYTE_DIFFERENT')
    expect(result.rows.find(r => r.field === 'mime')!.status).toBe('SAME')
    expect(result.rows.find(r => r.field === 'publicationState')!.status).toBe('CHANGED')
    expect(compareRevisions(doc.revisions[1] as never, doc.revisions[1] as never).state).toBe('SAME_REVISION')
  })
  it('freezes only known command paths', () => {
    expect(() => freezeS9Command('POST', 'voice-event-drafts/../admin?vesselId=' + s9Vessel, {})).toThrow(ApiError)
    expect(freezeS9Command('POST', `voice-event-drafts?vesselId=${s9Vessel}`, {}).key).toMatch(/^[0-9a-f-]{36}$/)
  })
  it('reads canonical routes', () => {
    expect(readVoiceRoute({ pathname: `/app/voice-event-draft/${s9Vessel}`, search: `?id=${s9Draft}` })).toEqual({ vessel: s9Vessel, id: s9Draft })
    expect(readCompareRoute({ pathname: '/app/document-compare/not-a-guid', search: '' })).toEqual({ vessel: null, document: null })
  })
})

describe('DEV-062 voice event draft workspace', () => {
  beforeEach(() => history.replaceState(null, '', `/app/voice-event-draft/${s9Vessel}?id=${s9Draft}`))
  const handler = (draft = voiceDraftFixture()): Handler => (path, init) =>
    path.endsWith('/voice-event-drafts/capabilities') ? json(voiceCapsFixture()) : path.endsWith('/voice-event-drafts/metrics') ? json(voiceMetricsFixture())
    : path.endsWith('/voice-event-drafts') && !init?.method ? json(voicePageFixture()) : path.endsWith(`/voice-event-drafts/${s9Draft}`) && !init?.method ? json(draft) : undefined

  it('offers typing instead of recording when no local engine exists, and shows traced suggestions', async () => {
    transport(handler())
    render(<VoiceEventDraftWorkspace/>)
    const detail = await screen.findByTestId('voice-detail')
    expect(screen.getByTestId('voice-privacy')).toHaveTextContent('audio is not stored')
    expect(within(detail).getByText(/TYPE_EXPLICIT/)).toBeInTheDocument()
    expect(detail.querySelector('[data-field="eventType"]')).toHaveAttribute('data-origin', 'EXTRACTED')
    expect(within(detail).getByRole('status')).toHaveTextContent('When it happened')
    // Nothing missing may be confirmed.
    expect(within(detail).getByRole('button', { name: 'Confirm draft' })).toBeDisabled()
    await userEvent.setup().click(await screen.findByRole('button', { name: 'New voice or typed note' }))
    expect(screen.getByTestId('speech-unavailable')).toHaveTextContent('no local engine is configured')
    expect(screen.queryByRole('button', { name: 'Record with local engine' })).toBeNull()
  })

  it('confirms with the exact version and content hash, CSRF and an idempotency key', async () => {
    const complete = voiceDraftFixture({ fields: { ...voiceDraftFixture().fields, occurredAt: '2026-11-19T09:15' }, missing: [], canConfirm: true,
      fieldOrigins: { ...voiceDraftFixture().fieldOrigins, occurredAt: 'USER_ENTERED' } })
    const confirmed = { ...complete, state: 'CONFIRMED', revision: 2, confirmedBy: s9Actor, confirmedAt: '2026-11-20T09:00:00Z', canEdit: false, canConfirm: false,
      handoff: 'EVENT_DRAFT_CREATED', safetyEventId: '00000000-0000-4000-8000-0000000600e1', corrections: [{ key: 'occurredAt', outcome: 'ADDED', required: true }] }
    const fetch = transport((path, init) => path.endsWith('/confirm') && init?.method === 'POST' ? json(confirmed) : handler(complete)(path, init))
    render(<VoiceEventDraftWorkspace/>)
    const detail = await screen.findByTestId('voice-detail')
    await userEvent.setup().click(within(detail).getByRole('button', { name: 'Confirm draft' }))
    const call = fetch.mock.calls.find(([url, init]) => String(url).includes('/confirm') && init?.method === 'POST')!
    const headers = call[1]!.headers as Record<string, string>
    expect(headers['X-CSRF-TOKEN']).toBe('synthetic-s9-csrf'); expect(headers['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/)
    expect(JSON.parse(String(call[1]!.body))).toEqual({ expectedVersion: 11, contentHash: 'a'.repeat(64) })
    // S9 integration: the confirmed candidate links the NAMED safety event draft it created (never a submitted event).
    expect(await screen.findByTestId('voice-handoff')).toHaveAttribute('data-handoff', 'EVENT_DRAFT_CREATED')
  })

  it('shows the forbidden state without leaking data', async () => {
    transport(() => json({ code: 'permission_denied' }, 403))
    render(<VoiceEventDraftWorkspace/>)
    expect(await screen.findByRole('alert')).toHaveTextContent('You do not have permission')
    expect(screen.queryByTestId('voice-detail')).toBeNull()
  })
})

describe('DEV-114 document compare workspace', () => {
  beforeEach(() => history.replaceState(null, '', `/app/document-compare/${s9Vessel}?document=${s9Document}`))
  const handler = (doc = documentFixture()): Handler => (path, init) =>
    path.endsWith('/controlled-documents') ? json(documentListFixture()) : path.endsWith(`/controlled-documents/${s9Document}`) && !init?.method ? json(doc) : undefined

  it('keeps restricted content closed, compares metadata and never calls a byte match a review', async () => {
    const fetch = transport(handler())
    render(<DocumentCompareWorkspace/>)
    const main = await screen.findByTestId('document-compare')
    expect(await within(main).findByTestId('content-policy')).toHaveTextContent('needs explicit permission')
    await userEvent.setup().click(within(main).getByRole('button', { name: 'Compare' }))
    expect(within(main).getByTestId('byte-statement')).toHaveTextContent('needs a person’s review')
    expect(within(main).getByTestId('restricted-left')).toBeInTheDocument()
    expect(within(main).queryByRole('button', { name: 'Load content (recorded access)' })).toBeNull()
    expect(fetch.mock.calls.some(([url]) => String(url).includes('content-access') || String(url).includes('/bytes'))).toBe(false)
  })

  it('records an acknowledgement with If-Match, CSRF and the displayed content hash', async () => {
    const fetch = transport((path, init) => path.endsWith('/revisions/2/acknowledgements') && init?.method === 'POST' ? json(documentFixture()) : handler()(path, init))
    render(<DocumentCompareWorkspace/>)
    const main = await screen.findByTestId('document-compare')
    const user = userEvent.setup()
    await user.click(await within(main).findByRole('button', { name: 'Compare' }))
    const right = within(main).getByTestId('compare-right')
    const record = within(right).getByRole('button', { name: 'Record acknowledgement' })
    expect(record).toBeDisabled()
    await user.click(within(right).getByRole('checkbox'))
    await user.click(record)
    const call = fetch.mock.calls.find(([url, init]) => String(url).includes('/acknowledgements') && init?.method === 'POST')!
    const headers = call[1]!.headers as Record<string, string>
    expect(headers['If-Match']).toBe('"21"'); expect(headers['X-CSRF-TOKEN']).toBe('synthetic-s9-csrf')
    expect(JSON.parse(String(call[1]!.body))).toEqual({ displayedContentSha256: '2'.repeat(64), statementAccepted: true })
  })

  it('shows a neutral unavailable state for a hidden document', async () => {
    transport((path, init) => path.endsWith(`/controlled-documents/${s9Document}`) ? json({ code: 'resource_not_found' }, 404) : handler()(path, init))
    render(<DocumentCompareWorkspace/>)
    expect(await screen.findByRole('alert')).toHaveTextContent('not available to you')
  })
})
