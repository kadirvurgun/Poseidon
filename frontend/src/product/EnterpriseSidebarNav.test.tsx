import { cleanup, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { Wrench } from 'lucide-react'
import { EnterpriseSidebarNav, type SidebarDestination } from './EnterpriseSidebarNav'
import { enterpriseFocusTargets } from './enterprise-focus'

afterEach(cleanup)
const items: SidebarDestination[] = [
  ['fleet', 'Fleet'], ['noon-report', 'Noon report'], ['fuel-bunker-log', 'Fuel and ROB'],
  ['defects', 'Defects'], ['crew', 'Crew'], ['certificate-command-center', 'Certificates'], ['companies', 'Companies'],
].map(([key, label]) => ({ key, label, href: '/app/' + key, icon: Wrench }))
const props = { items, currentKey: 'defects', language: 'en' as const, onNavigate: vi.fn() }

it('opens the current module and keeps the other authorized destinations in collapsed, keyboard-safe groups', () => {
  const view = render(<nav aria-label="Test sidebar"><EnterpriseSidebarNav {...props} /></nav>)
  expect(screen.getByRole('button', { name: 'Technical and maintenance' })).toHaveAttribute('aria-expanded', 'true')
  expect(screen.getByRole('button', { name: 'Defects', exact: true })).toHaveAttribute('aria-current', 'page')
  expect(screen.getByRole('button', { name: 'Overview' })).toHaveAttribute('aria-expanded', 'false')
  expect(screen.getByText('Fleet')).not.toBeVisible()
  expect(enterpriseFocusTargets(view.container).some(node => node.textContent === 'Fleet')).toBe(false)
})

it('uses one expanded module at a time without changing the owning page or its permissions', async () => {
  const onNavigate = vi.fn(), user = userEvent.setup(); render(<EnterpriseSidebarNav {...props} onNavigate={onNavigate} />)
  await user.click(screen.getByRole('button', { name: 'Crew management' }))
  expect(screen.getByRole('button', { name: 'Technical and maintenance' })).toHaveAttribute('aria-expanded', 'false')
  expect(screen.getByText('Defects')).not.toBeVisible()
  expect(onNavigate).not.toHaveBeenCalled()
  await user.click(screen.getByRole('button', { name: 'Crew', exact: true }))
  expect(onNavigate).toHaveBeenCalledWith('/app/crew')
})

it('reveals the committed route after navigation while withdrawing removed capabilities', async () => {
  const user = userEvent.setup(), view = render(<EnterpriseSidebarNav {...props} />)
  await user.click(screen.getByRole('button', { name: 'Overview' }))
  view.rerender(<EnterpriseSidebarNav {...props} currentKey="noon-report" />)
  expect(screen.getByRole('button', { name: 'Ship operations' })).toHaveAttribute('aria-expanded', 'true')
  expect(screen.getByRole('button', { name: 'Noon report' })).toHaveAttribute('aria-current', 'page')
  view.rerender(<EnterpriseSidebarNav {...props} currentKey="defects" items={items.filter(item => item.key !== 'defects')} />)
  expect(screen.queryByText('Defects')).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Technical and maintenance' })).not.toBeInTheDocument()
})

it('supports an empty or initially loading menu and native keyboard activation in either language', async () => {
  const user = userEvent.setup(), view = render(<EnterpriseSidebarNav {...props} items={[]} currentKey={undefined} />)
  expect(screen.queryByRole('button')).not.toBeInTheDocument()
  view.rerender(<EnterpriseSidebarNav {...props} language="tr" />)
  const group = screen.getByRole('button', { name: 'Genel bakış' }); group.focus(); await user.keyboard('{Enter}')
  expect(group).toHaveAttribute('aria-expanded', 'true')
  expect(within(document.getElementById(group.getAttribute('aria-controls')!)!).getByRole('button', { name: 'Fleet' })).toBeVisible()
})
