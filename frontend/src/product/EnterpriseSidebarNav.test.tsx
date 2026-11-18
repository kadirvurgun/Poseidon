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
  expect(screen.getByRole('button', { name: 'Defects' })).toHaveAttribute('aria-current', 'page')
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
  await user.click(screen.getByRole('button', { name: 'Crew' }))
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

// DEV-022 / 65.03 paragraph 51: the menu filter keeps only authorized items client-side and
// never names, counts or matches a hidden entry.
it('DEV022-AC-10 filters only the authorized menu items by localized label and opens every matching group', async () => {
  const user = userEvent.setup(), onNavigate = vi.fn()
  render(<EnterpriseSidebarNav {...props} onNavigate={onNavigate} />)
  const filter = screen.getByRole('searchbox', { name: 'Filter menu' })
  expect(filter).toHaveAccessibleDescription('Only screens you are authorized to open are searched.')
  await user.type(filter, 'REP')
  expect(screen.getByRole('button', { name: 'Noon report' })).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Fleet' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Crew' })).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Overview' })).not.toBeInTheDocument()
  await user.keyboard('{ArrowDown}')
  expect(screen.getByRole('button', { name: 'Noon report' })).toHaveFocus()
  await user.keyboard('{Enter}')
  expect(onNavigate).toHaveBeenCalledWith('/app/noon-report')
  expect(filter).toHaveValue('')
})

it('DEV022-AC-10 a hidden destination is never matched by key or label and no hidden count is shown', async () => {
  const user = userEvent.setup()
  const view = render(<EnterpriseSidebarNav {...props} items={items.filter(item => item.key !== 'defects')} />)
  const filter = screen.getByRole('searchbox', { name: 'Filter menu' })
  for (const probe of ['defects', 'Defects']) {
    await user.clear(filter); await user.type(filter, probe)
    expect(screen.getByRole('status')).toHaveTextContent('No authorized menu item matches this filter.')
    expect(screen.queryByRole('button', { name: /Defects/ })).not.toBeInTheDocument()
    expect(view.container.textContent).not.toMatch(/\d/)
  }
})

it('DEV022-AC-10 folds Turkish I/İ/ı/i and diacritics, and Escape clears before closing the drawer', async () => {
  const user = userEvent.setup(), drawerEscape = vi.fn()
  const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') drawerEscape() }
  document.addEventListener('keydown', onKey)
  const turkish: SidebarDestination[] = [['work-order-center', 'İş emirleri'], ['crew', 'Mürettebat listesi'], ['fleet', 'Filo']]
    .map(([key, label]) => ({ key, label, href: '/app/' + key, icon: Wrench }))
  render(<EnterpriseSidebarNav {...props} language="tr" items={turkish} currentKey="fleet" />)
  const filter = screen.getByRole('searchbox', { name: 'Menüde ara' })
  await user.type(filter, 'is EMIR')
  expect(screen.getByRole('button', { name: 'İş emirleri' })).toBeVisible()
  await user.clear(filter); await user.type(filter, 'murettebat')
  expect(screen.getByRole('button', { name: 'Mürettebat listesi' })).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Filo' })).not.toBeInTheDocument()
  await user.keyboard('{Escape}')
  expect(filter).toHaveValue('')
  expect(drawerEscape).not.toHaveBeenCalled()
  expect(screen.getByRole('button', { name: 'Genel bakış' })).toHaveAttribute('aria-expanded', 'true')
  await user.keyboard('{Escape}')
  expect(drawerEscape).toHaveBeenCalledTimes(1)
  document.removeEventListener('keydown', onKey)
})

it('DEV022-AC-10 clear button restores the grouped menu and returns focus to the filter', async () => {
  const user = userEvent.setup(); render(<EnterpriseSidebarNav {...props} />)
  const filter = screen.getByRole('searchbox', { name: 'Filter menu' })
  await user.type(filter, 'crew')
  await user.click(screen.getByRole('button', { name: 'Clear menu filter' }))
  expect(filter).toHaveValue('')
  expect(filter).toHaveFocus()
  expect(screen.getByRole('button', { name: 'Technical and maintenance' })).toHaveAttribute('aria-expanded', 'true')
  expect(screen.getByRole('button', { name: 'Overview' })).toHaveAttribute('aria-expanded', 'false')
})

it('DEV022-AC-10 ArrowUp/ArrowDown move between filtered results, ArrowUp on the first returns to the filter and Tab is unchanged', async () => {
  const user = userEvent.setup(); render(<EnterpriseSidebarNav {...props} />)
  const filter = screen.getByRole('searchbox', { name: 'Filter menu' })
  await user.type(filter, 'o')
  const [noon, fuel, companies] = ['Noon report', 'Fuel and ROB', 'Companies'].map(name => screen.getByRole('button', { name }))
  await user.keyboard('{ArrowDown}'); expect(noon).toHaveFocus()
  await user.keyboard('{ArrowDown}'); expect(fuel).toHaveFocus()
  await user.keyboard('{ArrowDown}'); expect(companies).toHaveFocus()
  await user.keyboard('{ArrowDown}'); expect(companies).toHaveFocus()
  await user.keyboard('{ArrowUp}'); expect(fuel).toHaveFocus()
  await user.keyboard('{ArrowUp}'); expect(noon).toHaveFocus()
  await user.keyboard('{ArrowUp}'); expect(filter).toHaveFocus()
  expect(filter).toHaveValue('o')
  await user.tab(); expect(screen.getByRole('button', { name: 'Clear menu filter' })).toHaveFocus()
  await user.tab(); expect(noon).toHaveFocus()
  await user.tab(); expect(fuel).toHaveFocus()
  await user.tab({ shift: true }); expect(noon).toHaveFocus()
})

it('DEV022-AC-10 arrow movement skips items inside collapsed groups', async () => {
  const user = userEvent.setup(); render(<EnterpriseSidebarNav {...props} />)
  const defects = screen.getByRole('button', { name: 'Defects' }); defects.focus()
  await user.keyboard('{ArrowDown}'); expect(defects).toHaveFocus()
  await user.keyboard('{ArrowUp}'); expect(screen.getByRole('searchbox', { name: 'Filter menu' })).toHaveFocus()
})
