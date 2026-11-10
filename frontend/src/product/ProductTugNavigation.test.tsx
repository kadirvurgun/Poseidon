import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { ProductApp } from './ProductApp'
import { openNavigationTestWorkView } from './workspace-navigation.test-fixture'

// The shared context header has its own API/session tests; this case exercises the real owning draft guard.
vi.mock('./WorkspaceContextBar', () => ({ WorkspaceContextBar: () => null }))
const id = '10000000-0000-0000-0000-000000000001'
const vessel = { id, companyId: '10000000-0000-0000-0000-000000000002', code: 'T1', name: 'Synthetic Tug', kind: 'Tug', version: 1, canEdit: true }
const response = (body: unknown) => ({ ok: true, status: 200, json: async () => body }) as Response
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); window.history.replaceState(null, '', '/') })

it('guards the actual Tug workspace link and retains a cancelled unsaved vessel edit', async () => {
  window.history.replaceState(null, '', `/app/fleet/${id}`)
  vi.stubGlobal('fetch', vi.fn(async (url: string) => response(
    url.includes('auth/session') ? { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false, tenantId: id, actorId: vessel.companyId, csrfToken: 'synthetic' }
      : url.includes('/workspace/lenses') ? { tenantId: id, actorId: vessel.companyId, vesselId: id, mode: 'Tug', state: 'Configured', version: 'synthetic-v1', canSearch: false, lenses: [{ reference: id, tr: 'KURGUSAL rol', en: 'FICTIONAL role', items: [{ key: 'tug-command', href: `/app/tug-command?vessel=${id}`, group: 'daily' }] }] } : url === '/api/v1/workspace' ? { destinations: [{ key: 'tug-command', href: '/app/tug-command' }], canSearch: false, canWriteRegulatory: false }
        : url.includes('vessels?') ? { items: [vessel], offset: 0, limit: 25, hasMore: false } : vessel)))
  const user = userEvent.setup(); render(<ProductApp />)
  await user.click(await screen.findByRole('button', { name: /^Düzenle$/ }))
  await user.type(screen.getByLabelText('Ad *'), ' retained')
  await openNavigationTestWorkView(user)
  await user.selectOptions(await screen.findByLabelText('Menü merceği'), id)
  await user.click(await within(screen.getByRole('navigation', { name: 'POSEIDON' })).findByRole('button', { name: 'Römorkör merkezi' }))
  expect(await screen.findByRole('dialog')).toBeVisible()
  expect(window.location.pathname).toBe(`/app/fleet/${id}`)
  await user.click(screen.getByRole('button', { name: 'Düzenlemeye devam et' }))
  expect(screen.getByLabelText('Ad *')).toHaveValue('Synthetic Tug retained')
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
})
