import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AppearanceControls } from './AppearanceControls'
import { initializeAppearance } from './appearance-store'

const root = document.documentElement
let dispose: () => void
beforeEach(() => {
  localStorage.clear()
  vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false, addEventListener: () => {}, removeEventListener: () => {} })))
  dispose = initializeAppearance()
})
afterEach(() => {
  dispose(); vi.restoreAllMocks(); localStorage.clear(); root.className = ''; root.style.colorScheme = ''
  for (const key of ['palette', 'appearanceMode', 'base', 'accent', 'chart', 'radius']) delete root.dataset[key]
})

describe('DEV-025 theme builder modal semantics (PR #18 QA notes)', () => {
  it('DEV025-AC-04 makes the page behind the sheet inert while open and restores it on close', async () => {
    const user = userEvent.setup()
    render(<><header data-testid="bar"><AppearanceControls language="en" /></header><main data-testid="main"><input aria-label="Draft" defaultValue="unsaved" /></main><aside data-testid="already" inert /></>)
    await user.click(screen.getByText('Appearance'))
    await user.click(screen.getByRole('button', { name: 'Customize…' }))
    const dialog = screen.getByRole('dialog', { name: 'Theme builder' })
    expect(dialog.closest('[inert]')).toBeNull()
    expect(screen.getByTestId('main').closest('[inert]')).not.toBeNull()
    expect(screen.getByTestId('bar').closest('[inert]')).not.toBeNull()
    await user.keyboard('{Escape}')
    await act(async () => { await Promise.resolve() })
    expect(screen.getByTestId('main').closest('[inert]')).toBeNull()
    expect(screen.getByTestId('bar').closest('[inert]')).toBeNull()
    // An element that was inert before the modal opened stays inert.
    expect(screen.getByTestId('already')).toHaveAttribute('inert')
    expect(screen.getByRole('button', { name: 'Customize…' })).toHaveFocus()
  })

  it('DEV025-AC-04 never puts the tabindex=-1 preview samples into the Tab cycle', async () => {
    const user = userEvent.setup()
    render(<AppearanceControls language="en" />)
    await user.click(screen.getByText('Appearance'))
    await user.click(screen.getByRole('button', { name: 'Customize…' }))
    const dialog = screen.getByRole('dialog', { name: 'Theme builder' })
    const samples = [within(dialog).getByRole('button', { name: 'Primary action' }), within(dialog).getByRole('button', { name: 'Secondary' })]
    for (let i = 0; i < 40; i++) {
      await user.tab()
      expect(dialog).toContainElement(document.activeElement as HTMLElement)
      expect(samples).not.toContain(document.activeElement)
    }
  }, 30_000)
})
