import { useSyncExternalStore } from 'react'

export type AppearanceMode = 'light' | 'dark' | 'system'
export type Palette = 'emerald' | 'ocean' | 'graphite'
export type Appearance = Readonly<{ mode: AppearanceMode; palette: Palette }>
export const appearanceKey = 'poseidon.appearance.v1'
const defaults: Appearance = { mode: 'system', palette: 'emerald' }

export function parseAppearance(value: string | null): Appearance {
  try {
    const parsed: unknown = JSON.parse(value ?? 'null')
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return defaults
    const { mode, palette } = parsed as Record<string, unknown>
    return {
      mode: mode === 'light' || mode === 'dark' || mode === 'system' ? mode : defaults.mode,
      palette: palette === 'emerald' || palette === 'ocean' || palette === 'graphite' ? palette : defaults.palette,
    }
  } catch { return defaults }
}

function read(): Appearance {
  try { return parseAppearance(window.localStorage.getItem(appearanceKey)) } catch { return defaults }
}

let current = read()
const listeners = new Set<() => void>()

function apply() {
  const dark = current.mode === 'dark' || (current.mode === 'system' && window.matchMedia?.('(prefers-color-scheme: dark)').matches)
  const root = document.documentElement
  root.classList.toggle('dark', !!dark)
  root.classList.toggle('light', !dark)
  root.dataset.appearanceMode = current.mode
  root.dataset.palette = current.palette
  root.style.colorScheme = dark ? 'dark' : 'light'
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', dark ? '#1c1917' : '#ffffff')
}

function publish(next: Appearance) {
  current = next
  apply()
  listeners.forEach(listener => listener())
}

export function setAppearance(next: Appearance) {
  // Only closed cosmetic values reach storage or DOM; never store account/scope information.
  const safe = parseAppearance(JSON.stringify(next))
  try { window.localStorage.setItem(appearanceKey, JSON.stringify(safe)) } catch { /* Session-only preference still works. */ }
  publish(safe)
}

export function initializeAppearance() {
  publish(read())
  const media = window.matchMedia?.('(prefers-color-scheme: dark)')
  const systemChanged = () => { if (current.mode === 'system') apply() }
  const storageChanged = (event: StorageEvent) => {
    try { if (event.storageArea && event.storageArea !== window.localStorage) return } catch { return }
    if (event.key === appearanceKey || event.key === null) publish(parseAppearance(event.newValue))
  }
  media?.addEventListener('change', systemChanged)
  window.addEventListener('storage', storageChanged)
  return () => { media?.removeEventListener('change', systemChanged); window.removeEventListener('storage', storageChanged) }
}

function subscribe(listener: () => void) { listeners.add(listener); return () => { listeners.delete(listener) } }
export function useAppearance() {
  const appearance = useSyncExternalStore(subscribe, () => current)
  return { appearance, setAppearance }
}
