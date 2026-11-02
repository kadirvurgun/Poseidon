import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { RegulatoryProfile } from './RegulatoryProfile'
import { ConstructionRecordPicker } from './ConstructionRecordPicker'
import { loadProfile, loadProfileHistory } from './profile-api'
import { profileFields, type ProfileContent } from './profile-contract'

const vessel = '10000000-0000-0000-0000-000000000001', fileId = '10000000-0000-0000-0000-000000000002'
const content = { ...Object.fromEntries(profileFields.map(([key]) => [key, { state: 'Missing', value: null }])), constructionRecord: null } as ProfileContent
const profile = { tenantId: '10000000-0000-0000-0000-000000000003', vessel: { id: vessel, companyId: fileId, name: 'FICTIONAL ship', code: 'S1', kind: 'Ship', version: 1, canEdit: true }, version: 1, revision: 1, content, provenance: 'Synthetic', savedAt: '2026-11-02T00:00:00Z' }
const file = { id: fileId, vesselId: vessel, name: 'fictional-contract.png', state: 'CleanTransport', version: 1, revision: 1, checksum: 'A'.repeat(64), length: 4 }
const files = { items: [file], offset: 0, hasMore: false, canWrite: false, available: true, maxBytes: 10000 }
const response = (value: unknown, status = 200) => ({ ok: status === 200, status, json: async () => value }) as Response
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })

it('saves power and a distinct contract date with an actual selected file reference, preserving incomplete build date', async () => {
  const fetch = vi.fn(async (url: string, init?: RequestInit) => {
    if (url.endsWith('/files?offset=0')) return response(files)
    if (url.includes('/history?')) return response({ items: [], offset: 0, limit: 25, hasMore: false })
    return response(init?.method === 'PUT' ? { ...profile, version: 2, revision: 2, content: JSON.parse(init.body as string).content } : profile)
  }); vi.stubGlobal('fetch', fetch)
  const user = userEvent.setup()
  render(<RegulatoryProfile id={vessel} language="en" csrf="synthetic-csrf" onDirty={vi.fn()} onSaving={vi.fn()} />)
  await screen.findByLabelText('Main propulsion power (kW)')
  await user.selectOptions(screen.getByLabelText('Main propulsion power (kW) — Data state'), 'Known')
  await user.type(screen.getByLabelText('Main propulsion power (kW)'), '9007199254740993')
  await user.selectOptions(screen.getByLabelText('Construction contract date (YYYY-MM-DD) — Data state'), 'Known')
  await user.type(screen.getByLabelText('Construction contract date (YYYY-MM-DD)'), '2022-03-14')
  await user.selectOptions(screen.getByLabelText('Class / build record file'), fileId)
  await user.click(screen.getByRole('button', { name: 'Save draft', exact: true })); await screen.findByText('Draft saved.')
  const saved = fetch.mock.calls.find(([, init]) => init?.method === 'PUT')![1]!
  const body = JSON.parse(saved.body as string)
  expect(body.content.mainPropulsionKw).toEqual({ state: 'Known', value: '9007199254740993' })
  expect(body.content.contractForConstructionDate).toEqual({ state: 'Known', value: '2022-03-14' })
  expect(body.content.buildDate).toEqual({ state: 'Missing', value: null })
  expect(body.content.constructionRecord).toEqual({ id: fileId, revision: 1, checksum: file.checksum })
  expect(screen.queryByText('Ready', { exact: true })).not.toBeInTheDocument()
})
it.each([
  { mainPropulsionKw: { state: 'Known', value: 3600 } },
  { mainPropulsionKw: { state: 'Known', value: '3600 kW' } },
  { contractForConstructionDate: { state: 'Known', value: '2022-02-30' } },
  { constructionRecord: { id: fileId, revision: '1', checksum: file.checksum } },
  { constructionRecord: { id: fileId, revision: 1, checksum: file.checksum + '\n' } },
  { constructionRecord: { id: fileId, revision: 1, checksum: file.checksum, verified: true } },
  { constructionRecord: undefined },
])('rejects malformed current and immutable-history owning wire values %j', async invalid => {
  const bad = JSON.parse(JSON.stringify({ ...content, ...invalid }))
  vi.stubGlobal('fetch', vi.fn(async (url: string) => response(url.includes('/history?') ? { items: [{ revision: 1, content: bad, savedAt: profile.savedAt, provenance: 'Synthetic' }], offset: 0, limit: 25, hasMore: false } : { ...profile, content: bad })))
  await expect(loadProfile(vessel, new AbortController().signal)).rejects.toMatchObject({ code: 'invalid_contract' })
  await expect(loadProfileHistory(vessel, 0, new AbortController().signal)).rejects.toMatchObject({ code: 'invalid_contract' })
})
it('denies new selection without withdrawing unrelated profile rights, and withdraws a previously selected record', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => response({ code: 'permission_denied' }, 403)))
  const denied = vi.fn(), changed = vi.fn()
  const view = render(<ConstructionRecordPicker vessel={vessel} language="tr" value={null} disabled={false} onChange={changed} onDenied={denied} />)
  await screen.findByRole('alert'); expect(denied).not.toHaveBeenCalled(); expect(screen.getByLabelText('Klas / inşa kayıt dosyası')).toBeDisabled()
  expect(changed).not.toHaveBeenCalled()
  view.unmount()
  render(<ConstructionRecordPicker vessel={vessel} language="en" value={{ id: fileId, revision: 1, checksum: file.checksum }} disabled={false} onChange={changed} onDenied={denied} />)
  await waitFor(() => expect(denied).toHaveBeenCalledWith(expect.objectContaining({ status: 403 })))
})
