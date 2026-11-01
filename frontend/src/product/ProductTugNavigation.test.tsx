import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { ProductApp } from './ProductApp'

// The shared context header has its own API/session tests; this case exercises the real owning draft guard.
vi.mock('./WorkspaceContextBar', () => ({ WorkspaceContextBar: () => null }))
const id = '10000000-0000-0000-0000-000000000001'
const vessel = { id, companyId: '10000000-0000-0000-0000-000000000002', code: 'T1', name: 'Synthetic Tug', kind: 'Tug', version: 1, canEdit: true }
const response = (body: unknown) => ({ ok: true, status: 200, json: async () => body }) as Response
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); window.history.replaceState(null, '', '/') })

it('guards the actual Tug workspace link and retains a cancelled unsaved vessel edit', async () => {
  window.history.replaceState(null, '', `/app/fleet/${id}`)
  vi.stubGlobal('fetch', vi.fn(async (url: string) => response(
    url.includes('auth/session') ? { authenticated: true, canRead: true, canReadCompanies: false, canCreate: false, canSearch: false, csrfToken: 'synthetic' }
      : url === '/api/v1/workspace' ? { destinations: [{ key: 'tug-command', href: '/app/tug-command' }], canSearch: false }
        : url.includes('vessels?') ? { items: [vessel], offset: 0, limit: 25, hasMore: false } : vessel)))
  const user = userEvent.setup(); render(<ProductApp />)
  await user.click(await screen.findByRole('button', { name: /^Düzenle$/ }))
  await user.type(screen.getByLabelText('Ad *'), ' retained')
  await user.click(await screen.findByRole('link', { name: 'Römorkör operasyonları' }))
  expect(await screen.findByRole('dialog')).toBeVisible()
  expect(window.location.pathname).toBe(`/app/fleet/${id}`)
  await user.click(screen.getByRole('button', { name: 'Düzenlemeye devam et' }))
  expect(screen.getByLabelText('Ad *')).toHaveValue('Synthetic Tug retained')
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
})
