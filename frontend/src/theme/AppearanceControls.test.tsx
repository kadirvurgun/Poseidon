import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { runInNewContext } from 'node:vm'
import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AppearanceControls } from './AppearanceControls'
import { appearanceKey, initializeAppearance, setAppearance } from './appearance-store'

let dispose: () => void
let darkSystem: boolean
let mediaChanged: (() => void) | undefined
beforeEach(() => {
  localStorage.clear()
  darkSystem = false
  mediaChanged = undefined
  vi.stubGlobal('matchMedia', vi.fn(() => ({
    get matches() { return darkSystem },
    addEventListener: (_: string, callback: () => void) => { mediaChanged = callback },
    removeEventListener: () => { mediaChanged = undefined },
  })))
  dispose = initializeAppearance()
})
afterEach(() => { dispose(); vi.restoreAllMocks(); localStorage.clear(); document.documentElement.className = ''; delete document.documentElement.dataset.palette; delete document.documentElement.dataset.appearanceMode; document.documentElement.style.colorScheme = '' })

describe('THEME-FOUNDATION-01 appearance behavior; browser/contrast acceptance deferred', () => {
  it('changes mode and palette without losing a form or changing a safety state', async () => {
    const user = userEvent.setup()
    render(<><AppearanceControls language="tr" /><input aria-label="Taslak" defaultValue="Kaydedilmemiş" /><p role="status">REVIEW_REQUIRED</p></>)
    await user.click(screen.getByText('Görünüm'))
    await user.selectOptions(screen.getByLabelText('Görünüm modu'), 'dark')
    await user.selectOptions(screen.getByLabelText('Renk teması'), 'ocean')
    expect(document.documentElement).toHaveClass('dark')
    expect(document.documentElement.dataset.palette).toBe('ocean')
    expect(JSON.parse(localStorage.getItem(appearanceKey)!)).toEqual({ mode: 'dark', palette: 'ocean' })
    expect(screen.getByLabelText('Taslak')).toHaveValue('Kaydedilmemiş')
    expect(screen.getByRole('status')).toHaveTextContent('REVIEW_REQUIRED')
    act(() => { dispose(); dispose = initializeAppearance() })
    expect(screen.getByLabelText('Görünüm modu')).toHaveValue('dark')
    expect(screen.getByLabelText('Renk teması')).toHaveValue('ocean')
  })

  it('follows OS changes only in system mode and keeps two mounted controls synchronized', async () => {
    const user = userEvent.setup()
    const { container } = render(<><AppearanceControls language="tr" /><AppearanceControls language="en" /></>)
    const controls = container.querySelectorAll('details')
    await user.click(within(controls[0]).getByText('Görünüm'))
    await user.click(within(controls[1]).getByText('Appearance'))
    act(() => { darkSystem = true; mediaChanged?.() })
    expect(document.documentElement).toHaveClass('dark')
    await user.selectOptions(screen.getByLabelText('Color mode'), 'light')
    expect(screen.getByLabelText('Görünüm modu')).toHaveValue('light')
    act(() => { darkSystem = true; mediaChanged?.() })
    expect(document.documentElement).toHaveClass('light')
    expect(screen.getByLabelText('Color theme')).toHaveValue('emerald')
  })

  it('keeps working for this tab when persistence is blocked', async () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new DOMException('blocked', 'SecurityError') })
    const user = userEvent.setup(); render(<AppearanceControls language="en" />)
    await user.click(screen.getByText('Appearance'))
    await user.selectOptions(screen.getByLabelText('Color mode'), 'dark')
    expect(document.documentElement).toHaveClass('dark')
    expect(screen.getByLabelText('Color mode')).toHaveValue('dark')
  })

  it('receives cross-tab preference changes and resets safely after corrupt data or storage clear', () => {
    render(<AppearanceControls language="en" />)
    act(() => window.dispatchEvent(new StorageEvent('storage', { key: appearanceKey, newValue: '{"mode":"dark","palette":"graphite"}' })))
    expect(document.documentElement).toHaveClass('dark')
    expect(document.documentElement.dataset.palette).toBe('graphite')
    act(() => window.dispatchEvent(new StorageEvent('storage', { key: appearanceKey, newValue: '{bad' })))
    expect(document.documentElement.dataset.appearanceMode).toBe('system')
    act(() => setAppearance({ mode: 'dark', palette: 'ocean' }))
    act(() => window.dispatchEvent(new StorageEvent('storage', { key: null, newValue: null })))
    expect(document.documentElement.dataset.palette).toBe('emerald')
  })

  it('closes on Escape and restores keyboard focus to the appearance summary', async () => {
    const user = userEvent.setup(); render(<AppearanceControls language="en" />)
    await user.click(screen.getByText('Appearance'))
    screen.getByLabelText('Color mode').focus()
    await user.keyboard('{Escape}')
    expect(screen.getByText('Appearance').closest('details')).not.toHaveAttribute('open')
    expect(screen.getByText('Appearance').closest('summary')).toHaveFocus()
  })

  it('applies stored preference before React and ignores arbitrary DOM values', () => {
    const script = readFileSync(fileURLToPath(import.meta.url).replace(/src[\\/]theme[\\/]AppearanceControls\.test\.tsx$/, 'public/appearance-init.js'), 'utf8')
    const boot = () => runInNewContext(script, { localStorage, window, document })
    localStorage.setItem(appearanceKey, '{"mode":"dark","palette":"graphite"}')
    boot()
    expect(document.documentElement).toHaveClass('dark')
    expect(document.documentElement.dataset.palette).toBe('graphite')
    localStorage.setItem(appearanceKey, '{"mode":"evil","palette":"url(https://other.test)"}')
    boot()
    expect(document.documentElement.dataset.palette).toBe('emerald')
    expect(document.documentElement.dataset.appearanceMode).toBe('system')
  })
})
