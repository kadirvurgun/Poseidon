import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { runInNewContext } from 'node:vm'
import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AppearanceControls } from './AppearanceControls'
import { appearanceKey, initializeAppearance, legacyAppearanceKey, serializeAppearance } from './appearance-store'
import { buildThemeStylesheet } from './theme-tokens'

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

async function openBuilder(language: 'en' | 'tr' = 'en') {
  const user = userEvent.setup()
  render(<><AppearanceControls language={language} /><input aria-label="Draft" defaultValue="unsaved draft" /><p role="status" data-testid="safety">REVIEW_REQUIRED</p></>)
  await user.click(screen.getByText(language === 'en' ? 'Appearance' : 'Görünüm'))
  await user.click(screen.getByRole('button', { name: language === 'en' ? 'Customize…' : 'Özelleştir…' }))
  return { user, dialog: screen.getByRole('dialog', { name: language === 'en' ? 'Theme builder' : 'Tema oluşturucu' }) }
}

describe('THEME-BUILDER-01 theme builder behavior (component level; real browser/AT gate separate)', () => {
  it('THEME-BUILDER-A11Y: opens as a modal dialog, traps Tab, closes on Escape and returns focus', async () => {
    const { user, dialog } = await openBuilder()
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(within(dialog).getByRole('heading', { name: 'Theme builder' })).toHaveFocus()
    within(dialog).getByRole('button', { name: 'Apply' }).focus()
    await user.tab()
    expect(dialog).toContainElement(document.activeElement as HTMLElement)
    expect(within(dialog).getByRole('button', { name: 'Close theme builder' })).toHaveFocus()
    await user.tab({ shift: true })
    expect(within(dialog).getByRole('button', { name: 'Apply' })).toHaveFocus()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await act(async () => { await Promise.resolve() })
    expect(screen.getByRole('button', { name: 'Customize…' })).toHaveFocus()
    expect(screen.getByText('Appearance').closest('details')).toHaveAttribute('open')
  })

  it('THEME-BUILDER-LIVE: previews tokens on documentElement, Cancel/Escape reverts, Apply persists without remounting forms', async () => {
    const { user, dialog } = await openBuilder()
    await user.click(within(dialog).getByRole('radio', { name: 'Zinc' }))
    await user.click(within(dialog).getByRole('radio', { name: 'Violet' }))
    await user.click(within(dialog).getByRole('radio', { name: '0.3rem' }))
    expect(root.dataset).toMatchObject({ palette: 'custom', base: 'zinc', accent: 'violet', chart: 'cyan', radius: '0.3' })
    expect(buildThemeStylesheet()).toContain(':root[data-palette="custom"][data-accent="violet"]')
    expect(localStorage.getItem(appearanceKey)).toBeNull()
    await user.selectOptions(within(dialog).getByLabelText('Chart colors'), 'accent')
    expect(root.dataset.chart).toBe('violet')
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    expect(root.dataset.palette).toBe('emerald')
    expect(root.dataset.accent).toBeUndefined()

    await user.click(screen.getByRole('button', { name: 'Customize…' }))
    const again = screen.getByRole('dialog')
    await user.click(within(again).getByRole('radio', { name: 'Slate' }))
    await user.selectOptions(within(again).getByLabelText('Color mode'), 'dark')
    expect(root).toHaveClass('dark')
    await user.click(within(again).getByRole('button', { name: 'Apply' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(JSON.parse(localStorage.getItem(appearanceKey)!)).toEqual({ v: 2, mode: 'dark', palette: 'custom', custom: { base: 'slate', accent: 'emerald', chart: 'cyan', radius: '0.625' } })
    expect(root.dataset).toMatchObject({ palette: 'custom', base: 'slate' })
    expect(screen.getByLabelText('Color theme')).toHaveValue('custom')
    expect(screen.getByLabelText('Draft')).toHaveValue('unsaved draft')
    expect(screen.getByTestId('safety')).toHaveTextContent('REVIEW_REQUIRED')
  })

  it('THEME-BUILDER-PRESET: offers built-ins and the user preset, and Reset returns to the default palette', async () => {
    const { user, dialog } = await openBuilder('tr')
    const presets = within(dialog).getByRole('group', { name: 'Hazır temalar' })
    for (const name of ['Zümrüt', 'Okyanus', 'Grafit', 'b3Zheoix4U (Taş · Zümrüt · Camgöbeği grafik)']) expect(within(presets).getByRole('radio', { name })).toBeInTheDocument()
    expect(within(presets).getByRole('radio', { name: 'Zümrüt' })).toBeChecked()
    await user.click(within(presets).getByRole('radio', { name: 'Okyanus' }))
    expect(root.dataset.palette).toBe('ocean')
    await user.click(within(presets).getByRole('radio', { name: /b3Zheoix4U/ }))
    expect(root.dataset).toMatchObject({ palette: 'custom', base: 'stone', accent: 'emerald', chart: 'cyan', radius: '0.625' })
    await user.click(within(dialog).getByRole('radio', { name: 'Gül' }))
    expect(within(dialog).getByRole('note')).toHaveTextContent('durum rengine benziyor')
    await user.click(within(dialog).getByRole('button', { name: 'Varsayılana dön' }))
    expect(root.dataset.palette).toBe('emerald')
    expect(within(presets).getByRole('radio', { name: 'Zümrüt' })).toBeChecked()
  })

  it('THEME-BUILDER-IMPORT/EXPORT: copies CSS, rejects corrupt JSON without changing the preview, imports valid JSON', async () => {
    const { user, dialog } = await openBuilder()
    await user.click(within(dialog).getByRole('radio', { name: 'Blue' }))
    expect((within(dialog).getByLabelText('CSS variables') as HTMLTextAreaElement).value).toContain('--primary: oklch(0.488 0.243 264.376);')
    await user.click(within(dialog).getByRole('button', { name: 'Copy — CSS variables' }))
    expect(await navigator.clipboard.readText()).toContain(':root {')
    expect(within(dialog).getByText('Copied.')).toBeInTheDocument()

    const field = within(dialog).getByLabelText('Import theme JSON')
    await user.click(field)
    await user.paste('{"version":1,"palette":"custom","base":"zinc","accent":"blue","chart":"accent","radius":"0.5","style":"background:url(x)"}')
    await user.click(within(dialog).getByRole('button', { name: 'Import' }))
    expect(within(dialog).getByText(/^Rejected:/)).toBeInTheDocument()
    expect(root.dataset).toMatchObject({ base: 'stone', accent: 'blue', radius: '0.625' })

    await user.clear(field)
    await user.paste('{"version":1,"palette":"custom","base":"gray","accent":"teal","chart":"amber","radius":"1","mode":"dark"}')
    await user.click(within(dialog).getByRole('button', { name: 'Import' }))
    expect(root.dataset).toMatchObject({ palette: 'custom', base: 'gray', accent: 'teal', chart: 'amber', radius: '1' })
    expect(root).toHaveClass('dark')
    expect(localStorage.getItem(appearanceKey)).toBeNull()
  })

  it('THEME-BUILDER-CONTRAST: shows the measured lowest text contrast for the draft', async () => {
    const { dialog } = await openBuilder()
    const text = within(dialog).getByTestId('theme-builder-contrast').textContent!
    expect(Number(/: ([\d.]+):1/.exec(text)![1])).toBeGreaterThanOrEqual(4.5)
    expect(within(dialog).getByRole('button', { name: 'Apply' })).toBeEnabled()
    expect(within(dialog).queryByRole('alert')).not.toBeInTheDocument()
  })
})

describe('THEME-BUILDER-PERSIST migration, corruption and cross-tab sync', () => {
  it('migrates a v1 preference to v2 once and removes the legacy key', () => {
    dispose()
    localStorage.setItem(legacyAppearanceKey, '{"mode":"dark","palette":"graphite"}')
    dispose = initializeAppearance()
    expect(JSON.parse(localStorage.getItem(appearanceKey)!)).toMatchObject({ v: 2, mode: 'dark', palette: 'graphite' })
    expect(localStorage.getItem(legacyAppearanceKey)).toBeNull()
    expect(root.dataset.palette).toBe('graphite')
  })

  it('falls back safely for corrupt, future-version or unknown stored values', () => {
    dispose()
    localStorage.setItem(appearanceKey, '{"v":3,"mode":"dark","palette":"custom"}')
    dispose = initializeAppearance()
    expect(root.dataset.palette).toBe('emerald')
    dispose()
    localStorage.setItem(appearanceKey, '{"v":2,"mode":"dark","palette":"custom","custom":{"base":"evil","accent":"url(x)","chart":1,"radius":"9"}}')
    dispose = initializeAppearance()
    expect(root.dataset).toMatchObject({ palette: 'custom', base: 'stone', accent: 'emerald', chart: 'cyan', radius: '0.625' })
  })

  it('applies custom themes received from another tab and resets on storage clear', () => {
    render(<AppearanceControls language="en" />)
    const value = serializeAppearance({ mode: 'light', palette: 'custom', custom: { base: 'neutral', accent: 'indigo', chart: 'accent', radius: '0' } })
    act(() => window.dispatchEvent(new StorageEvent('storage', { key: appearanceKey, newValue: value })))
    expect(root.dataset).toMatchObject({ palette: 'custom', base: 'neutral', accent: 'indigo', chart: 'indigo', radius: '0' })
    expect(screen.getByLabelText('Color theme')).toHaveValue('custom')
    act(() => window.dispatchEvent(new StorageEvent('storage', { key: null, newValue: null })))
    expect(root.dataset.palette).toBe('emerald')
    expect(root.dataset.base).toBeUndefined()
  })
})

describe('THEME-BUILDER-PAINT first-paint bootstrap', () => {
  const script = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'public', 'appearance-init.js'), 'utf8')
  const boot = () => runInNewContext(script, { localStorage, window, document })

  it('applies a stored v2 custom theme before React and still reads v1 when v2 is absent', () => {
    localStorage.setItem(appearanceKey, '{"v":2,"mode":"dark","palette":"custom","custom":{"base":"slate","accent":"sky","chart":"accent","radius":"0.75"}}')
    boot()
    expect(root).toHaveClass('dark')
    expect(root.dataset).toMatchObject({ palette: 'custom', base: 'slate', accent: 'sky', chart: 'sky', radius: '0.75' })
    localStorage.clear()
    localStorage.setItem(legacyAppearanceKey, '{"mode":"light","palette":"ocean"}')
    boot()
    expect(root.dataset.palette).toBe('ocean')
    expect(root).toHaveClass('light')
  })

  it('ignores arbitrary values, custom palette in v1 and corrupt v2 data', () => {
    localStorage.setItem(appearanceKey, '{"v":2,"mode":"dark","palette":"custom","custom":{"base":"x\\" onload","accent":"url(https://other.test)","chart":"red","radius":"100"}}')
    boot()
    expect(root.dataset).toMatchObject({ palette: 'custom', base: 'stone', accent: 'emerald', chart: 'cyan', radius: '0.625' })
    localStorage.clear()
    localStorage.setItem(legacyAppearanceKey, '{"mode":"dark","palette":"custom"}')
    boot()
    expect(root.dataset.palette).toBe('emerald')
    localStorage.setItem(appearanceKey, '{corrupt')
    boot()
    expect(root.dataset).toMatchObject({ palette: 'emerald', appearanceMode: 'system' })
  })
})
