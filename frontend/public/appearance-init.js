/* global localStorage, window, document */
// Apply closed cosmetic preferences before the first stylesheet/module paint.
// Keep accepted values/defaults aligned with src/theme/appearance-store.ts.
(() => {
  let value
  try { value = JSON.parse(localStorage.getItem('poseidon.appearance.v1') || 'null') } catch { /* Use defaults when storage is unavailable. */ }
  const mode = ['light', 'dark', 'system'].includes(value?.mode) ? value.mode : 'system'
  const palette = ['emerald', 'ocean', 'graphite'].includes(value?.palette) ? value.palette : 'emerald'
  const dark = mode === 'dark' || (mode === 'system' && window.matchMedia?.('(prefers-color-scheme: dark)').matches)
  const root = document.documentElement
  root.classList.toggle('dark', !!dark)
  root.classList.toggle('light', !dark)
  root.dataset.appearanceMode = mode
  root.dataset.palette = palette
  root.style.colorScheme = dark ? 'dark' : 'light'
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', dark ? '#1c1917' : '#ffffff')
})()
