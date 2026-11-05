import { useState } from 'react'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { CertificateWorkspace } from './CertificateWorkspace'
import type { CertificateExtractionReviewProps } from './CertificateExtractionReview'
import { certificateActor, certificateCapsFixture, certificateFixture, certificateId, certificateVessel } from './certificate-test-fixtures'

// A child-boundary probe: actual extraction protocol/security tests live with
// CertificateExtractionReview. These tests exercise the parent's form guard.
vi.mock('./CertificateExtractionReview', () => ({ CertificateExtractionReview: function Probe(props: CertificateExtractionReviewProps) {
  const [retained, setRetained] = useState(false)
  return <section aria-label="Document child boundary">
    <button disabled={!props.enabled} onClick={() => { setRetained(true); props.onDirty(true) }}>Retain document suggestions</button>
    {retained && props.enabled && <><p>Retained original suggestions</p><button onClick={() => { setRetained(false); props.onDirty(false) }}>Discard child suggestions</button></>}
    {!props.enabled && <p>Child source locked</p>}
  </section>
} }))
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status })
beforeEach(() => {
  history.replaceState(null, '', '/app/certificate-command-center/' + certificateVessel + '?id=' + certificateId)
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
  vi.stubGlobal('fetch', vi.fn(async (url: string) => {
    if (url.endsWith('/auth/session')) return json({ authenticated: true, canRead: true, actorId: certificateActor, tenantId: certificateVessel, csrfToken: 'synthetic-only' })
    if (url.includes('/certificate-command-center/capabilities?')) return json(certificateCapsFixture())
    if (url.includes('/certificates/' + certificateId + '/history?')) return json({ items: [certificateFixture()], offset: 0, limit: 25, hasMore: false })
    if (url.endsWith('/certificates/' + certificateId)) return json(certificateFixture())
    return json({}, 403)
  }))
})
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals() })
async function open() {
  render(<CertificateWorkspace />)
  const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'EN' }))
  await screen.findByLabelText('Certificate number')
  return user
}
describe('T-S6-001/003 child assistance ownership and independent guard', () => {
  it('locks child confirmation while ordinary fields are dirty and keeps those fields unchanged', async () => {
    const user = await open()
    await user.type(screen.getByLabelText('Certificate number'), 'LOCAL untouched')
    expect(screen.getByRole('button', { name: 'Retain document suggestions' })).toBeDisabled()
    expect(screen.getByLabelText('Certificate number')).toHaveValue('LOCAL untouched')
  })
  it('aggregates retained child work without allowing an ordinary save to overwrite it', async () => {
    const user = await open()
    await user.click(screen.getByRole('button', { name: 'Retain document suggestions' }))
    expect(screen.getByLabelText('Certificate number')).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Save unverified draft' })).toBeDisabled()
    const unload = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(unload)
    expect(unload.defaultPrevented).toBe(true)
    await user.click(screen.getByRole('button', { name: 'Discard child suggestions' }))
    expect(screen.getByLabelText('Certificate number')).not.toBeDisabled()
  })
  it('withdraws a child while offline without unmounting its retained work', async () => {
    const user = await open()
    await user.click(screen.getByRole('button', { name: 'Retain document suggestions' }))
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false)
    act(() => window.dispatchEvent(new Event('offline')))
    expect(await screen.findByText('Child source locked')).toBeVisible()
    expect(screen.queryByText('Retained original suggestions')).not.toBeInTheDocument()
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
    act(() => window.dispatchEvent(new Event('online')))
    await waitFor(() => expect(screen.getByText('Retained original suggestions')).toBeVisible())
    const unload = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(unload)
    expect(unload.defaultPrevented).toBe(true)
  })
})
