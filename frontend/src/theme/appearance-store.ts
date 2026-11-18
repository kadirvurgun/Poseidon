import { useSyncExternalStore } from 'react'
import { builtInPalettes, defaultCustomTheme, parseCustomTheme, resolveChart, themeColorHex } from './theme-tokens'
import type { BuiltInPalette, CustomTheme } from './theme-tokens'

export type AppearanceMode = 'light' | 'dark' | 'system'
export type Palette = BuiltInPalette | 'custom'
export type Appearance = Readonly<{ mode: AppearanceMode; palette: Palette; custom: CustomTheme }>
export type AppearanceInput = Readonly<{ mode: AppearanceMode; palette: Palette; custom?: CustomTheme }>
/** v2 adds the closed custom theme; v1 (`{mode, palette}`) is migrated once and then removed. */
export const appearanceKey = 'poseidon.appearance.v2'
export const legacyAppearanceKey = 'poseidon.appearance.v1'
export const appearanceSchemaVersion = 2
export const defaultAppearance: Appearance = { mode: 'system', palette: 'emerald', custom: defaultCustomTheme }
const defaults = defaultAppearance

function record(value: string | null): Record<string, unknown> | null {
  try {
    const parsed: unknown = JSON.parse(value ?? 'null')
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed as Record<string, unknown> : null
  } catch { return null }
}
const isMode = (mode: unknown): mode is AppearanceMode => mode === 'light' || mode === 'dark' || mode === 'system'
const isPalette = (palette: unknown): palette is Palette => palette === 'custom' || (builtInPalettes as readonly unknown[]).includes(palette)

/** Parses a stored v2 (or v1-shaped) value; every unknown field falls back to its safe default. */
export function parseAppearance(value: string | null): Appearance {
  const parsed = record(value)
  if (!parsed) return defaults
  if (parsed.v !== undefined && parsed.v !== appearanceSchemaVersion) return defaults
  return {
    mode: isMode(parsed.mode) ? parsed.mode : defaults.mode,
    palette: isPalette(parsed.palette) ? parsed.palette : defaults.palette,
    custom: parseCustomTheme(parsed.custom),
  }
}

export function serializeAppearance(value: Appearance): string {
  return JSON.stringify({ v: appearanceSchemaVersion, mode: value.mode, palette: value.palette, custom: value.custom })
}

function sanitize(next: AppearanceInput): Appearance {
  return parseAppearance(JSON.stringify({ v: appearanceSchemaVersion, mode: next.mode, palette: next.palette, custom: next.custom ?? current.custom }))
}

function read(): Appearance {
  try {
    const stored = window.localStorage.getItem(appearanceKey)
    if (stored !== null) return parseAppearance(stored)
    const legacy = window.localStorage.getItem(legacyAppearanceKey)
    if (legacy === null) return defaults
    // v1 only had mode + built-in palette; `custom` was never part of v1, so it is ignored there.
    const old = record(legacy)
    const migrated: Appearance = {
      mode: isMode(old?.mode) ? old.mode : defaults.mode,
      palette: old?.palette !== 'custom' && isPalette(old?.palette) ? old.palette : defaults.palette,
      custom: defaults.custom,
    }
    try { window.localStorage.setItem(appearanceKey, serializeAppearance(migrated)); window.localStorage.removeItem(legacyAppearanceKey) } catch { /* Keep the migrated value for this tab. */ }
    return migrated
  } catch { return defaults }
}

let current: Appearance = read()
let preview: Appearance | null = null
const listeners = new Set<() => void>()

/** Applies closed attribute values only; the tokens themselves live in design-system.css / theme-builder.css. */
export function applyAppearanceToDocument(value: Appearance) {
  const dark = value.mode === 'dark' || (value.mode === 'system' && !!window.matchMedia?.('(prefers-color-scheme: dark)').matches)
  const root = document.documentElement
  root.classList.toggle('dark', dark)
  root.classList.toggle('light', !dark)
  root.dataset.appearanceMode = value.mode
  root.dataset.palette = value.palette
  if (value.palette === 'custom') {
    root.dataset.base = value.custom.base
    root.dataset.accent = value.custom.accent
    root.dataset.chart = resolveChart(value.custom)
    root.dataset.radius = value.custom.radius
  } else {
    for (const key of ['base', 'accent', 'chart', 'radius'] as const) delete root.dataset[key]
  }
  root.style.colorScheme = dark ? 'dark' : 'light'
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', themeColorHex(value, dark))
}

function apply() { applyAppearanceToDocument(preview ?? current) }

function publish(next: Appearance) {
  current = next
  apply()
  listeners.forEach(listener => listener())
}

export function setAppearance(next: AppearanceInput) {
  // Only closed cosmetic values reach storage or DOM; never store account/scope information.
  const safe = sanitize(next)
  preview = null
  try { window.localStorage.setItem(appearanceKey, serializeAppearance(safe)) } catch { /* Session-only preference still works. */ }
  publish(safe)
}

/** Live, unsaved preview for the theme builder; `null` restores the saved appearance. */
export function previewAppearance(next: AppearanceInput | null) {
  preview = next ? sanitize(next) : null
  apply()
}

export function getAppearance() { return current }

export function initializeAppearance() {
  preview = null
  publish(read())
  const media = window.matchMedia?.('(prefers-color-scheme: dark)')
  const systemChanged = () => { if ((preview ?? current).mode === 'system') apply() }
  const storageChanged = (event: StorageEvent) => {
    try { if (event.storageArea && event.storageArea !== window.localStorage) return } catch { return }
    if (event.key === appearanceKey) publish(parseAppearance(event.newValue))
    else if (event.key === null) publish(defaults) // clear() in another tab
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
