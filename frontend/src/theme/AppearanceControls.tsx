import { useId, useRef } from 'react'
import { Palette as PaletteIcon } from 'lucide-react'
import { useAppearance } from './appearance-store'
import type { AppearanceMode, Palette } from './appearance-store'
import type { Locale } from '../i18n'
import './appearance-controls.css'

// Only TR/EN copy is authored; every non-Turkish locale falls back to English.
export function AppearanceControls({ language }: { language: Locale }) {
  const { appearance, setAppearance } = useAppearance()
  const details = useRef<HTMLDetailsElement>(null)
  const id = useId()
  const en = language !== 'tr'
  return <details className="appearance-controls" ref={details} onKeyDown={event => {
    if (event.key === 'Escape' && details.current?.open) {
      event.preventDefault(); details.current.open = false; details.current.querySelector('summary')?.focus()
    }
  }}>
    <summary><PaletteIcon aria-hidden="true" /><span>{en ? 'Appearance' : 'Görünüm'}</span></summary>
    <div className="appearance-panel">
      <label htmlFor={`${id}-mode`}>{en ? 'Color mode' : 'Görünüm modu'}</label>
      <select id={`${id}-mode`} value={appearance.mode} onChange={event => setAppearance({ ...appearance, mode: event.target.value as AppearanceMode })}>
        <option value="system">{en ? 'System' : 'Sistem'}</option>
        <option value="light">{en ? 'Light' : 'Açık'}</option>
        <option value="dark">{en ? 'Dark' : 'Koyu'}</option>
      </select>
      <label htmlFor={`${id}-palette`}>{en ? 'Color theme' : 'Renk teması'}</label>
      <select id={`${id}-palette`} value={appearance.palette} onChange={event => setAppearance({ ...appearance, palette: event.target.value as Palette })}>
        <option value="emerald">{en ? 'Emerald' : 'Zümrüt'}</option>
        <option value="ocean">{en ? 'Ocean' : 'Okyanus'}</option>
        <option value="graphite">{en ? 'Graphite' : 'Grafit'}</option>
      </select>
    </div>
  </details>
}
