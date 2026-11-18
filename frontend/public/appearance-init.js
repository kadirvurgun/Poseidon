/* global localStorage, window, document */
// Apply closed cosmetic preferences before the first stylesheet/module paint.
// Keep accepted values/defaults aligned with src/theme/appearance-store.ts and theme-tokens.ts.
// Only allow-listed names reach the DOM as data attributes; token values live in CSS.
(() => {
  // undefined = key absent (migrate from v1), null = unreadable/corrupt (defaults).
  const read = key => { try { const raw = localStorage.getItem(key); return raw === null ? undefined : JSON.parse(raw) } catch { return null } }
  let value = read('poseidon.appearance.v2')
  if (value === undefined) value = read('poseidon.appearance.v1')
  if (!value || typeof value !== 'object' || (value.v !== undefined && value.v !== 2)) value = null
  const pick = (list, item, fallback) => list.includes(item) ? item : fallback
  const accents = ['emerald', 'teal', 'cyan', 'sky', 'blue', 'indigo', 'violet', 'rose', 'orange', 'amber']
  const bases = { stone: '#0c0a09', zinc: '#09090b', neutral: '#0a0a0a', slate: '#020618', gray: '#030712' }
  const mode = pick(['light', 'dark', 'system'], value?.mode, 'system')
  const palette = pick(['emerald', 'ocean', 'graphite', ...(value?.v === 2 ? ['custom'] : [])], value?.palette, 'emerald')
  const custom = value?.v === 2 && value.custom && typeof value.custom === 'object' ? value.custom : {}
  const dark = mode === 'dark' || (mode === 'system' && window.matchMedia?.('(prefers-color-scheme: dark)').matches)
  const root = document.documentElement
  root.classList.toggle('dark', !!dark)
  root.classList.toggle('light', !dark)
  root.dataset.appearanceMode = mode
  root.dataset.palette = palette
  let themeColor = dark ? '#1c1917' : '#ffffff'
  if (palette === 'custom') {
    const base = pick(Object.keys(bases), custom.base, 'stone')
    const accent = pick(accents, custom.accent, 'emerald')
    root.dataset.base = base
    root.dataset.accent = accent
    root.dataset.chart = custom.chart === 'accent' ? accent : pick(accents, custom.chart, 'cyan')
    root.dataset.radius = pick(['0', '0.3', '0.5', '0.625', '0.75', '1'], custom.radius, '0.625')
    if (dark) themeColor = bases[base]
  }
  root.style.colorScheme = dark ? 'dark' : 'light'
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', themeColor)
})()
