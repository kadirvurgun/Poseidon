import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { useState } from 'react'
import { changeAppLanguage, i18n, registerLocaleLoader, registerResourceBundle, text, useAppLanguage } from './index'
import type { Locale } from './index'

const disposers: (() => void)[] = []
beforeEach(async () => { localStorage.clear(); await changeAppLanguage('tr', false) })
afterEach(() => { cleanup(); for (const dispose of disposers.splice(0)) dispose() })
function deferred() { let resolve!: () => void; let reject!: () => void; const promise = new Promise<void>((yes, no) => { resolve = yes; reject = no }); return { promise, resolve, reject } }

describe('application locale loading and identity', () => {
  it('loads product namespaces before committing language, direction and persistence', async () => {
    const pending = deferred()
    disposers.push(registerLocaleLoader(() => pending.promise))
    const changing = changeAppLanguage('ar')
    expect(i18n.language).toBe('tr')
    expect(document.documentElement.lang).toBe('tr')
    expect(localStorage.getItem('poseidon.locale')).toBeNull()
    pending.resolve(); await changing
    expect(i18n.language).toBe('ar')
    expect(document.documentElement.lang).toBe('ar')
    expect(document.documentElement.dir).toBe('rtl')
    expect(localStorage.getItem('poseidon.locale')).toBe('ar')
    await changeAppLanguage('zh-Hant')
    expect(document.documentElement.lang).toBe('zh-Hant')
    expect(document.documentElement.dir).toBe('ltr')
  })
  it('keeps the previous locale if a product catalog fails to load', async () => {
    await changeAppLanguage('en-GB')
    disposers.push(registerLocaleLoader(async () => { throw new Error('private-loader-detail') }))
    await changeAppLanguage('ar')
    expect(i18n.language).toBe('en-GB')
    expect(document.documentElement.lang).toBe('en-GB')
    expect(document.documentElement.dir).toBe('ltr')
    expect(localStorage.getItem('poseidon.locale')).toBe('en-GB')
  })
  it('lets the latest request win even if earlier success or failure arrives later', async () => {
    const requests = new Map<Locale, ReturnType<typeof deferred>>([['ar', deferred()], ['de', deferred()]])
    disposers.push(registerLocaleLoader(locale => requests.get(locale)?.promise ?? Promise.resolve()))
    const first = changeAppLanguage('ar'), second = changeAppLanguage('de')
    requests.get('de')!.resolve(); await second
    requests.get('ar')!.reject(); await first
    expect(i18n.language).toBe('de')
    expect(document.documentElement.dir).toBe('ltr')
    expect(localStorage.getItem('poseidon.locale')).toBe('de')
  })
  it('does not let an earlier successful load replace a newer pending selection', async () => {
    const first = deferred(), second = deferred()
    disposers.push(registerLocaleLoader(locale => locale === 'ar' ? first.promise : second.promise))
    const arabic = changeAppLanguage('ar'), german = changeAppLanguage('de')
    first.resolve(); await arabic
    expect(i18n.language).toBe('tr')
    second.resolve(); await german
    expect(i18n.language).toBe('de')
  })
  it('does not change selected locale when explicitly translating another locale', () => {
    expect(text('ar', 'common:save')).toBe('حفظ')
    expect(text('zh-Hant', 'common:save')).toBe('儲存')
    expect(text('zh-Hans', 'common:save')).toBe('保存')
    expect(i18n.language).toBe('tr')
    expect(document.documentElement.dir).toBe('ltr')
  })
  it('uses named interpolation in registered namespaces without changing canonical values', () => {
    registerResourceBundle('de', 'test-product', { receipt: 'Version {{revision}}: {{value}}' })
    const value = '9007199254740993.0000000000000001'
    expect(text('de', 'test-product:receipt', { revision: 7, value })).toBe('Version 7: ' + value)
    expect(i18n.language).toBe('tr')
    i18n.removeResourceBundle('de', 'test-product')
  })
  it('preserves local draft identity and retained command bytes while all subscribers update', async () => {
    const retained = JSON.stringify({ key: 'retained-command', criticality: 'Unknown', value: '1.000000000000001' })
    function Draft() {
      const [locale, setLocale] = useAppLanguage(), [draft, setDraft] = useState('Unsaved text')
      const [command] = useState(retained)
      return <><input aria-label="draft" value={draft} onChange={event => setDraft(event.target.value)} /><output data-testid="locale">{locale}</output><output data-testid="command">{command}</output><button onClick={() => setLocale('ar')}>change</button></>
    }
    render(<Draft />)
    const input = screen.getByLabelText('draft')
    fireEvent.change(input, { target: { value: 'Still unsaved' } })
    await act(async () => { fireEvent.click(screen.getByRole('button', { name: 'change' })) })
    expect(screen.getByLabelText('draft')).toBe(input)
    expect(input).toHaveValue('Still unsaved')
    expect(screen.getByTestId('locale')).toHaveTextContent('ar')
    expect(screen.getByTestId('command').textContent).toBe(retained)
  })
  it('safely finishes a global locale request after a subscriber unmounts', async () => {
    const pending = deferred()
    disposers.push(registerLocaleLoader(() => pending.promise))
    function Subscriber() { const [locale] = useAppLanguage(); return <span>{locale}</span> }
    const view = render(<Subscriber />), changing = changeAppLanguage('ar')
    view.unmount(); pending.resolve(); await changing
    expect(i18n.language).toBe('ar')
    expect(document.documentElement.dir).toBe('rtl')
  })
})
