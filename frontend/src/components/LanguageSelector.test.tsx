import { act, cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { LanguageSelector } from './LanguageSelector'
import { changeAppLanguage, registerLocaleLoader, useAppLanguage } from '../i18n'

const disposers: (() => void)[] = []
beforeEach(async () => { localStorage.clear(); await changeAppLanguage('tr', false) })
afterEach(() => { cleanup(); for (const dispose of disposers.splice(0)) dispose() })
function ControlledSelector() { const [locale, onChange] = useAppLanguage(); return <LanguageSelector locale={locale} onChange={onChange} /> }

describe('shared accessible language selector', () => {
  it('offers all native-language options and supports normal keyboard focus', async () => {
    const user = userEvent.setup(), onChange = vi.fn()
    render(<LanguageSelector locale="tr" onChange={onChange} />)
    const selector = screen.getByRole('combobox', { name: 'Dil seçin' })
    expect(screen.getAllByRole('option')).toHaveLength(17)
    expect(screen.getByRole('option', { name: 'العربية' })).toHaveAttribute('lang', 'ar')
    expect(screen.getByRole('option', { name: '繁體中文' })).toHaveAttribute('value', 'zh-Hant')
    await user.tab(); expect(selector).toHaveFocus()
    await user.selectOptions(selector, 'en-GB')
    expect(onChange).toHaveBeenCalledExactlyOnceWith('en-GB')
  })
  it('changes shared language and direction, retaining the same selector DOM node', async () => {
    const user = userEvent.setup()
    render(<ControlledSelector />)
    const selector = screen.getByRole('combobox', { name: 'Dil seçin' })
    await user.selectOptions(selector, 'ar')
    expect(screen.getByRole('combobox', { name: 'اختر اللغة' })).toBe(selector)
    expect(selector).toHaveValue('ar')
    expect(document.documentElement.dir).toBe('rtl')
    await user.selectOptions(selector, 'zh-Hans')
    expect(screen.getByRole('combobox', { name: '选择语言' })).toHaveValue('zh-Hans')
    expect(document.documentElement.dir).toBe('ltr')
  })
  it('exposes catalog failure in the active language without private error details', async () => {
    disposers.push(registerLocaleLoader(async () => { throw new Error('sensitive-runtime-detail') }))
    render(<ControlledSelector />)
    await act(async () => { await changeAppLanguage('ar') })
    const alert = screen.getByRole('alert')
    expect(alert).toHaveTextContent('Dil yüklenemedi. Önceki seçiminiz korundu.')
    expect(alert).not.toHaveTextContent('sensitive-runtime-detail')
    expect(screen.getByRole('combobox')).toHaveAttribute('aria-describedby', alert.id)
    expect(screen.getByRole('combobox')).toHaveValue('tr')
  })
  it('leaves the previous option committed while asynchronous catalogs load', async () => {
    let finish!: () => void
    disposers.push(registerLocaleLoader(() => new Promise<void>(resolve => { finish = resolve })))
    render(<ControlledSelector />)
    let changing!: Promise<void>
    act(() => { changing = changeAppLanguage('fr') })
    expect(screen.getByRole('combobox')).toHaveValue('tr')
    expect(screen.getByRole('combobox')).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByRole('status')).toHaveTextContent('Dil yükleniyor…')
    await act(async () => { finish(); await changing })
    expect(screen.getByRole('combobox', { name: 'Choisir la langue' })).toHaveValue('fr')
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })
})
