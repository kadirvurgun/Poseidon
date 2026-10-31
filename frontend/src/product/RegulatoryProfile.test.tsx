import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useState } from 'react'
import { RegulatoryProfile } from './RegulatoryProfile'
import { RequiredDataField } from './RequiredDataField'
import { ProductApp } from './ProductApp'
import { loadProfile, saveProfile } from './profile-api'
import { profileFields } from './profile-contract'
import type { Profile, ProfileContent } from './profile-contract'

const vessel = { id: 'vessel-one', companyId: 'company-one', code: 'S1', name: 'Synthetic Ship', kind: 'Ship', version: 7, canEdit: true }
const content = Object.fromEntries(profileFields.map(([key]) => [key, { state: 'Missing', value: null }])) as ProfileContent
const profile = { tenantId: 'tenant-one', vessel, version: 19, revision: 1, savedAt: '2026-01-01T00:00:00Z', provenance: 'Synthetic', content } as Profile
const history = { items: [{ revision: 1, savedAt: profile.savedAt, provenance: 'Synthetic', content }], offset: 0, limit: 25, hasMore: false }
const reply = (body: unknown, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => body }) as Response
const callbacks = () => ({ id: vessel.id, language: 'en' as const, csrf: 'synthetic-csrf', onDirty: vi.fn(), onSaving: vi.fn() })
function mock(save: (init: RequestInit) => Promise<Response> = async () => reply({ ...profile, version: 20, revision: 2 })) {
  const fetchMock = vi.fn((url: string, init: RequestInit) => url.endsWith('/history?offset=0&limit=25') ? Promise.resolve(reply(history)) : init?.method === 'PUT' ? save(init) : Promise.resolve(reply(profile)))
  vi.stubGlobal('fetch', fetchMock); return fetchMock
}
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

describe('VESSEL-PROFILE-DRAFT-01 component and transport coverage (mocked API, no external IdP)', () => {
  it('renders accessible draft fields, server context, unknown provenance and saved history without readiness', async () => {
    mock(); render(<RegulatoryProfile {...callbacks()} />)
    expect(screen.getAllByRole('status')[0]).toHaveTextContent('Loading profile')
    expect(await screen.findByText(/tenant-one/)).toHaveTextContent('S1 · Synthetic Ship')
    expect(screen.getByLabelText('GT')).toBeDisabled()
    expect(screen.getByLabelText('Flag — Data state')).toHaveValue('Missing')
    expect(screen.getByText(/Mandatory submission policy is unresolved/)).toBeVisible()
    expect(screen.getByText(/does not authorize submission or operations/)).toBeVisible()
    expect(screen.getByRole('heading', { name: 'Version history' })).toBeVisible()
    expect(screen.getByText(/Version 1 ·/)).toHaveTextContent('Synthetic')
    expect(screen.queryByText('Ready', { exact: true })).not.toBeInTheDocument()
  })

  it('saves incomplete drafts with explicit Unknown and Known zero and bounded same-origin transport', async () => {
    const fetchMock = mock(async init => reply({ ...profile, content: JSON.parse(init.body as string).content, version: 20, revision: 2 }))
    const props = callbacks(); const user = userEvent.setup(); render(<RegulatoryProfile {...props} />)
    await screen.findByLabelText('GT')
    await user.selectOptions(screen.getByLabelText('Flag — Data state'), 'Unknown')
    await user.selectOptions(screen.getByLabelText('GT — Data state'), 'Known')
    await user.type(screen.getByLabelText('GT'), '0')
    await user.click(screen.getByRole('button', { name: 'Save draft' }))
    await screen.findByText('Draft saved.')
    const call = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')!
    expect(call[0]).toBe('/api/v1/vessels/vessel-one/regulatory-profile')
    expect(call[1]).toMatchObject({ credentials: 'same-origin', method: 'PUT', headers: { 'X-CSRF-TOKEN': 'synthetic-csrf', 'Idempotency-Key': expect.any(String) } })
    expect(JSON.parse(call[1].body as string)).toMatchObject({ expectedVersion: 19, content: { flag: { state: 'Unknown', value: null }, gt: { state: 'Known', value: 0 } } })
    expect(props.onDirty).toHaveBeenLastCalledWith(false)
  })

  it('retains conflict values and blocks another save until explicit reload even after more edits', async () => {
    const fetchMock = mock(async () => reply({ code: 'version_conflict' }, 409)); const user = userEvent.setup()
    render(<RegulatoryProfile {...callbacks()} />); await screen.findByLabelText('Flag')
    await user.selectOptions(screen.getByLabelText('Flag — Data state'), 'Known'); await user.type(screen.getByLabelText('Flag'), 'Local declaration')
    await user.click(screen.getByRole('button', { name: 'Save draft' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Your draft is retained')
    await user.type(screen.getByLabelText('Flag'), ' retained')
    expect(screen.getByLabelText('Flag')).toHaveValue('Local declaration retained')
    expect(screen.getByRole('button', { name: 'Save draft' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Reload saved draft' }))
    await waitFor(() => expect(screen.getByLabelText('Flag — Data state')).toHaveValue('Missing'))
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT')).toHaveLength(1)
  })

  it('freezes uncertain save and retries an identical key/payload across language changes with no double submit', async () => {
    let complete!: (response: Response) => void
    let saves = 0
    const fetchMock = mock(async () => { if (++saves === 1) throw new TypeError('lost response'); return new Promise<Response>(r => { complete = r }) })
    const props = callbacks(); const user = userEvent.setup(); const view = render(<RegulatoryProfile {...props} />)
    await screen.findByLabelText('GT'); await user.click(screen.getByRole('button', { name: 'Save draft' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Fields are frozen')
    expect(screen.getByLabelText('Flag — Data state')).toBeDisabled()
    expect(props.onSaving).toHaveBeenLastCalledWith(true)
    view.rerender(<RegulatoryProfile {...props} language="tr" />)
    await user.dblClick(screen.getByRole('button', { name: 'Aynı komutu tekrar dene' }))
    const calls = fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT')
    expect(calls).toHaveLength(2); expect(calls[1][1].body).toBe(calls[0][1].body); expect(calls[1][1].headers).toEqual(calls[0][1].headers)
    complete(reply({ ...profile, version: 20, revision: 2 })); await screen.findByText('Taslak kaydedildi.')
    expect(props.onSaving).toHaveBeenLastCalledWith(false)
  })

  it('associates safe server validation with fields and never displays arbitrary server text', async () => {
    mock(async () => reply({ code: 'validation_failed', errors: { imo: ['secret or unsafe error text'] } }, 400))
    const user = userEvent.setup(); render(<RegulatoryProfile {...callbacks()} />); await screen.findByLabelText('IMO')
    await user.click(screen.getByRole('button', { name: 'Save draft' }))
    await waitFor(() => expect(screen.getByLabelText('IMO')).toHaveAttribute('aria-invalid', 'true'))
    expect(screen.getByLabelText('IMO')).toHaveAccessibleDescription('Check the value and data state.')
    expect(screen.queryByText('secret or unsafe error text')).not.toBeInTheDocument()
  })

  it.each([[401, 'Sign in to continue.'], [403, 'You do not have permission'], [404, 'Profile not found'], [503, 'Unable to load']])('shows load failure %s and allows retry', async (status, text) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(reply({}, status)))
    render(<RegulatoryProfile {...callbacks()} />)
    expect((await screen.findAllByRole('alert'))[0]).toHaveTextContent(text)
    expect(screen.getAllByRole('button', { name: 'Retry loading' }).length).toBeGreaterThan(0)
  })

  it('shows absent draft and empty history and honors server read-only context', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string) => Promise.resolve(reply(url.includes('/history?') ? { ...history, items: [] } : { ...profile, version: 0, revision: 0, savedAt: null, provenance: 'Missing', vessel: { ...vessel, canEdit: false } }))))
    render(<RegulatoryProfile {...callbacks()} />)
    expect(await screen.findByText('No saved profile draft yet.')).toBeVisible()
    expect(screen.getByText('No saved versions.')).toBeVisible(); expect(screen.getByText('Read-only profile')).toBeVisible()
    expect(screen.queryByRole('button', { name: 'Save draft' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Flag — Data state')).toBeDisabled()
  })

  it('keeps Known false separate from Missing and Unknown in reusable boolean fields with TR/EN labels', async () => {
    function Field() {
      const [value, setValue] = useState<{ state?: string; value?: boolean | null }>({ state: 'Missing', value: null })
      return <RequiredDataField kind="boolean" language="en" label="Declared choice" value={value} onChange={setValue} />
    }
    const user = userEvent.setup(); render(<Field />)
    await user.selectOptions(screen.getByLabelText('Declared choice — Data state'), 'Known')
    expect(screen.getByLabelText('Declared choice')).toHaveValue('')
    await user.selectOptions(screen.getByLabelText('Declared choice'), 'false')
    expect(screen.getByLabelText('Declared choice')).toHaveValue('false')
    await user.selectOptions(screen.getByLabelText('Declared choice — Data state'), 'Unknown')
    expect(screen.getByLabelText('Declared choice')).toBeDisabled(); expect(screen.getByLabelText('Declared choice')).toHaveValue('')
  })

  it('rejects a wrong-vessel or malformed successful response instead of rendering foreign data', async () => {
    const fetchMock = vi.fn().mockResolvedValue(reply({ ...profile, vessel: { ...vessel, id: 'wrong-vessel' } })); vi.stubGlobal('fetch', fetchMock)
    await expect(loadProfile(vessel.id, new AbortController().signal)).rejects.toMatchObject({ status: 502 })
    fetchMock.mockResolvedValue(reply({ ...profile, content: { ...content, gt: { state: 'Unknown', value: 0 } } }))
    await expect(saveProfile(vessel.id, { expectedVersion: 19, content }, 'csrf', 'synthetic-command-key', new AbortController().signal)).rejects.toMatchObject({ status: 502 })
  })

  it('opens from fleet details and guards vessel/tab navigation while a profile draft is dirty', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string) => Promise.resolve(reply(url.includes('/auth/session') ?
      { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, csrfToken: 'csrf', tenantId: 'tenant-one' } :
      url.includes('/history?') ? history : url.includes('/regulatory-profile') ? profile :
      url.includes('/vessels?') ? { items: [vessel], offset: 0, limit: 25, hasMore: false } : vessel))))
    const user = userEvent.setup(); render(<ProductApp />); await user.click(screen.getByRole('button', { name: 'EN' }))
    await user.click(await screen.findByRole('button', { name: 'Synthetic Ship' })); await user.click(await screen.findByRole('button', { name: 'Regulatory profile' }))
    await screen.findByLabelText('Flag'); await user.selectOptions(screen.getByLabelText('Flag — Data state'), 'Unknown')
    await user.click(screen.getByRole('button', { name: /^Close$/ }))
    expect(screen.getByRole('dialog')).toBeVisible()
    expect(screen.getByLabelText('Flag — Data state')).toHaveValue('Unknown')
    const event = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(event); expect(event.defaultPrevented).toBe(true)
  })
})
