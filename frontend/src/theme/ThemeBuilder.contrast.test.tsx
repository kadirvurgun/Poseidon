import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { appearanceKey, initializeAppearance } from './appearance-store'
import { ThemeBuilder } from './ThemeBuilder'

// Every real allowed combination passes (theme-tokens.test.ts); force a failing report to prove the UI blocks it.
vi.mock('./theme-tokens', async original => ({
  ...await original<typeof import('./theme-tokens')>(),
  contrastReport: () => ({ blocked: true, checks: [{ mode: 'light', foreground: 'primary-foreground', background: 'primary', ratio: 2.1, ok: false }] }),
}))

let dispose: () => void
beforeEach(() => { localStorage.clear(); vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false, addEventListener: () => {}, removeEventListener: () => {} }))); dispose = initializeAppearance() })
afterEach(() => { dispose(); localStorage.clear() })

describe('THEME-BUILDER-CONTRAST UI guard', () => {
  it('shows the failing pair and refuses to apply a theme below 4.5:1', async () => {
    const user = userEvent.setup()
    const onClose = vi.fn()
    render(<ThemeBuilder language="en" onClose={onClose} />)
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByRole('alert')).toHaveTextContent('light --primary-foreground/--primary 2.10:1')
    const apply = within(dialog).getByRole('button', { name: 'Apply' })
    expect(apply).toBeDisabled()
    await user.click(apply)
    expect(onClose).not.toHaveBeenCalled()
    expect(localStorage.getItem(appearanceKey)).toBeNull()
  })
})
