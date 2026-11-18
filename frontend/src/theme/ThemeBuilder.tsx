import { useEffect, useId, useMemo, useRef, useState } from 'react'
import type { KeyboardEvent as ReactKeyboardEvent } from 'react'
import { createPortal } from 'react-dom'
import { defaultAppearance, getAppearance, previewAppearance, setAppearance } from './appearance-store'
import type { Appearance, AppearanceMode } from './appearance-store'
import {
  accentColors, autoAdjustedForegrounds, baseColors, contrastReport, exportCss, exportJson, parseThemeFile,
  presetB3Zheoix4U, radiusOptions, swatchColor, textContrastFloor,
} from './theme-tokens'
import type { AccentColor, BaseColor, ChartColor, CustomTheme, RadiusOption } from './theme-tokens'
import type { Locale } from '../i18n'
import { trapTab, useInertBackground } from '../components/a11y/modal'

// Only TR/EN copy is authored; every non-Turkish locale falls back to English (same convention as AppearanceControls).
const copy = {
  en: {
    title: 'Theme builder', intro: 'Changes preview live on the whole app. Apply saves them in this browser only.',
    presets: 'Presets', emerald: 'Emerald', ocean: 'Ocean', graphite: 'Graphite', preset: 'b3Zheoix4U (Stone · Emerald · Cyan chart)', customActive: 'Custom theme',
    presetHint: 'A built-in preset is active; changing a control below switches to a custom theme.',
    mode: 'Color mode', system: 'System', light: 'Light', dark: 'Dark', base: 'Base color', accent: 'Accent color', chart: 'Chart colors', followAccent: 'Follow accent',
    radius: 'Radius', preview: 'Preview', primaryAction: 'Primary action', secondaryAction: 'Secondary', card: 'Card surface', cardText: 'Muted supporting text',
    statusNote: 'Status colors (warning, danger, info) keep their fixed colors and labels in every theme.',
    statusLike: 'This accent resembles a status color. State is never shown by accent color alone.',
    warning: 'Warning', danger: 'Danger', info: 'Info', contrast: 'Contrast check (WCAG text 4.5:1)', lowest: 'Lowest text contrast',
    adjusted: 'Foreground auto-adjusted for contrast', blocked: 'This combination does not meet 4.5:1 text contrast and cannot be applied.', passes: 'All text pairs meet the floor in light and dark.',
    exportCss: 'CSS variables', exportJson: 'Theme JSON', copy: 'Copy', copied: 'Copied.', copyFailed: 'Copy failed; select the text manually.',
    import: 'Import theme JSON', importAction: 'Import', importOk: 'Theme imported into the preview. Apply to save.',
    importRejected: 'Rejected: only a POSEIDON theme JSON with allowed values can be imported.',
    reset: 'Reset to default', cancel: 'Cancel', apply: 'Apply', close: 'Close theme builder',
  },
  tr: {
    title: 'Tema oluşturucu', intro: 'Değişiklikler tüm uygulamada canlı önizlenir. Uygula yalnız bu tarayıcıda kaydeder.',
    presets: 'Hazır temalar', emerald: 'Zümrüt', ocean: 'Okyanus', graphite: 'Grafit', preset: 'b3Zheoix4U (Taş · Zümrüt · Camgöbeği grafik)', customActive: 'Özel tema',
    presetHint: 'Hazır tema etkin; aşağıdaki bir ayarı değiştirmek özel temaya geçirir.',
    mode: 'Görünüm modu', system: 'Sistem', light: 'Açık', dark: 'Koyu', base: 'Taban rengi', accent: 'Vurgu rengi', chart: 'Grafik renkleri', followAccent: 'Vurguyu izle',
    radius: 'Köşe yarıçapı', preview: 'Önizleme', primaryAction: 'Birincil işlem', secondaryAction: 'İkincil', card: 'Kart yüzeyi', cardText: 'Yardımcı soluk metin',
    statusNote: 'Durum renkleri (uyarı, tehlike, bilgi) her temada sabit renk ve etiketlerini korur.',
    statusLike: 'Bu vurgu bir durum rengine benziyor. Durum hiçbir zaman yalnız vurgu rengiyle gösterilmez.',
    warning: 'Uyarı', danger: 'Tehlike', info: 'Bilgi', contrast: 'Kontrast denetimi (WCAG metin 4.5:1)', lowest: 'En düşük metin kontrastı',
    adjusted: 'Kontrast için ön plan rengi otomatik ayarlandı', blocked: 'Bu kombinasyon 4.5:1 metin kontrastını sağlamıyor ve uygulanamaz.', passes: 'Tüm metin çiftleri açık ve koyu modda eşiği sağlıyor.',
    exportCss: 'CSS değişkenleri', exportJson: 'Tema JSON', copy: 'Kopyala', copied: 'Kopyalandı.', copyFailed: 'Kopyalanamadı; metni elle seçin.',
    import: 'Tema JSON içe aktar', importAction: 'İçe aktar', importOk: 'Tema önizlemeye alındı. Kaydetmek için Uygula.',
    importRejected: 'Reddedildi: yalnız izin verilen değerlere sahip POSEIDON tema JSON içe aktarılabilir.',
    reset: 'Varsayılana dön', cancel: 'Vazgeç', apply: 'Uygula', close: 'Tema oluşturucuyu kapat',
  },
} as const

const colorNames: Record<'en' | 'tr', Record<BaseColor | AccentColor, string>> = {
  en: { stone: 'Stone', zinc: 'Zinc', neutral: 'Neutral', slate: 'Slate', gray: 'Gray', emerald: 'Emerald', teal: 'Teal', cyan: 'Cyan', sky: 'Sky', blue: 'Blue', indigo: 'Indigo', violet: 'Violet', rose: 'Rose', orange: 'Orange', amber: 'Amber' },
  tr: { stone: 'Taş', zinc: 'Çinko', neutral: 'Nötr', slate: 'Arduvaz', gray: 'Gri', emerald: 'Zümrüt', teal: 'Deniz mavisi', cyan: 'Camgöbeği', sky: 'Gök', blue: 'Mavi', indigo: 'Çivit', violet: 'Menekşe', rose: 'Gül', orange: 'Turuncu', amber: 'Kehribar' },
}
const statusLikeAccents: readonly AccentColor[] = ['rose', 'orange', 'amber']
type PresetId = 'emerald' | 'ocean' | 'graphite' | 'b3Zheoix4U'
const sameTheme = (a: CustomTheme, b: CustomTheme) => a.base === b.base && a.accent === b.accent && a.chart === b.chart && a.radius === b.radius

export function ThemeBuilder({ language, onClose }: { language: Locale; onClose: () => void }) {
  const lang = language === 'tr' ? 'tr' : 'en'
  const t = copy[lang]
  const names = colorNames[lang]
  const id = useId()
  const dialog = useRef<HTMLDivElement>(null)
  const backdrop = useRef<HTMLDivElement>(null)
  const heading = useRef<HTMLHeadingElement>(null)
  const [draft, setDraft] = useState<Appearance>(getAppearance)
  const [importText, setImportText] = useState('')
  const [status, setStatus] = useState('')
  const report = useMemo(() => contrastReport(draft), [draft])
  const adjusted = draft.palette === 'custom' ? autoAdjustedForegrounds(draft.custom.accent) : []
  const lowest = Math.min(...report.checks.map(check => check.ratio ?? 0))
  const css = useMemo(() => exportCss(draft), [draft])
  const json = useMemo(() => exportJson(draft), [draft])
  const presetValue: PresetId | null = draft.palette !== 'custom' ? draft.palette : sameTheme(draft.custom, presetB3Zheoix4U) ? 'b3Zheoix4U' : null

  useEffect(() => { previewAppearance(draft) }, [draft])
  // DEV-025 / UX-016: the page behind the sheet is inert (pointer, Tab and AT) while it is open.
  useInertBackground(backdrop)
  useEffect(() => { heading.current?.focus(); return () => previewAppearance(null) }, [])

  const customize = (patch: Partial<CustomTheme>) => setDraft(value => ({ ...value, palette: 'custom', custom: { ...value.custom, ...patch } }))
  const choosePreset = (preset: PresetId) => setDraft(value => preset === 'b3Zheoix4U'
    ? { ...value, palette: 'custom', custom: presetB3Zheoix4U }
    : { ...value, palette: preset })
  const copyText = async (text: string) => {
    try { await navigator.clipboard.writeText(text); setStatus(t.copied) } catch { setStatus(t.copyFailed) }
  }
  const importTheme = () => {
    const file = parseThemeFile(importText.trim(), draft.custom)
    if (!file) { setStatus(t.importRejected); return }
    setDraft(value => ({ mode: file.mode ?? value.mode, palette: file.selection.palette, custom: file.selection.custom }))
    setStatus(t.importOk)
  }
  const apply = () => { if (report.blocked) return; setAppearance(draft); onClose() }
  const onKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); onClose(); return }
    // Preview samples (tabindex=-1) are excluded from the cycle by focusableTargets.
    trapTab(event, dialog.current, heading.current)
  }

  const radios = <T extends string>(legend: string, name: string, values: readonly T[], selected: T | null, label: (value: T) => string, onPick: (value: T) => void, swatch?: (value: T) => string) =>
    <fieldset className="theme-builder-group">
      <legend>{legend}</legend>
      <div className="theme-builder-options">
        {values.map(value => <label key={value} className="theme-builder-option">
          <input type="radio" name={`${id}-${name}`} value={value} checked={selected === value} onChange={() => onPick(value)} />
          {swatch && <span className="theme-builder-swatch" aria-hidden="true" style={{ background: swatch(value) }} />}
          <span>{label(value)}</span>
        </label>)}
      </div>
    </fieldset>

  return createPortal(<div ref={backdrop} className="theme-builder-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) onClose() }}>
    <div ref={dialog} className="theme-builder" role="dialog" aria-modal="true" aria-labelledby={`${id}-title`} aria-describedby={`${id}-intro`} onKeyDown={onKeyDown}>
      <header className="theme-builder-header">
        <h2 id={`${id}-title`} ref={heading} tabIndex={-1}>{t.title}</h2>
        <button type="button" className="theme-builder-close" aria-label={t.close} onClick={onClose}>×</button>
      </header>
      <p id={`${id}-intro`} className="theme-builder-hint">{t.intro}</p>

      {radios(t.presets, 'preset', ['emerald', 'ocean', 'graphite', 'b3Zheoix4U'] as const, presetValue,
        value => value === 'b3Zheoix4U' ? t.preset : t[value], choosePreset)}
      <p className="theme-builder-hint" data-testid="theme-builder-state">{draft.palette === 'custom' ? t.customActive : t.presetHint}</p>

      <label className="theme-builder-field" htmlFor={`${id}-mode`}>{t.mode}</label>
      <select id={`${id}-mode`} value={draft.mode} onChange={event => setDraft(value => ({ ...value, mode: event.target.value as AppearanceMode }))}>
        <option value="system">{t.system}</option><option value="light">{t.light}</option><option value="dark">{t.dark}</option>
      </select>

      {radios(t.base, 'base', baseColors, draft.custom.base, value => names[value], value => customize({ base: value }), swatchColor)}
      {radios(t.accent, 'accent', accentColors, draft.custom.accent, value => names[value], value => customize({ accent: value }), swatchColor)}
      <p className="theme-builder-hint">{t.statusNote}</p>
      {statusLikeAccents.includes(draft.custom.accent) && draft.palette === 'custom' && <p className="theme-builder-hint" role="note">{t.statusLike}</p>}

      <label className="theme-builder-field" htmlFor={`${id}-chart`}>{t.chart}</label>
      <select id={`${id}-chart`} value={draft.custom.chart} onChange={event => customize({ chart: event.target.value as ChartColor })}>
        <option value="accent">{t.followAccent}</option>
        {accentColors.map(value => <option key={value} value={value}>{names[value]}</option>)}
      </select>

      {radios(t.radius, 'radius', radiusOptions, draft.custom.radius, value => `${value}rem`, value => customize({ radius: value as RadiusOption }))}

      <section className="theme-builder-preview" aria-label={t.preview}>
        <h3>{t.preview}</h3>
        <div className="theme-builder-preview-row">
          <button type="button" className="theme-builder-sample-primary" tabIndex={-1}>{t.primaryAction}</button>
          <button type="button" className="theme-builder-sample-secondary" tabIndex={-1}>{t.secondaryAction}</button>
        </div>
        <div className="theme-builder-sample-card"><strong>{t.card}</strong><span>{t.cardText}</span></div>
        <div className="theme-builder-preview-row">
          <span className="theme-builder-status is-warning">{t.warning}</span>
          <span className="theme-builder-status is-danger">{t.danger}</span>
          <span className="theme-builder-status is-info">{t.info}</span>
        </div>
        <div className="theme-builder-chart" aria-hidden="true">{[1, 2, 3, 4, 5].map(n => <span key={n} style={{ background: `var(--chart-${n})`, height: `${30 + n * 12}%` }} />)}</div>
      </section>

      <section className="theme-builder-contrast" aria-label={t.contrast}>
        <h3>{t.contrast}</h3>
        <p data-testid="theme-builder-contrast">{t.lowest}: {lowest.toFixed(2)}:1 (≥ {textContrastFloor}:1)</p>
        {adjusted.length > 0 && <p className="theme-builder-hint">{t.adjusted}: {adjusted.map(item => `${item.mode} --${item.token}`).join(', ')}</p>}
        {report.blocked
          ? <p role="alert" className="theme-builder-blocked">{t.blocked} {report.checks.filter(check => !check.ok).map(check => `${check.mode} --${check.foreground}/--${check.background} ${check.ratio === null ? '?' : check.ratio.toFixed(2)}:1`).join('; ')}</p>
          : <p className="theme-builder-hint">{t.passes}</p>}
      </section>

      <label className="theme-builder-field" htmlFor={`${id}-css`}>{t.exportCss}</label>
      <textarea id={`${id}-css`} readOnly rows={6} value={css} spellCheck={false} />
      <button type="button" onClick={() => void copyText(css)}>{t.copy} — {t.exportCss}</button>
      <label className="theme-builder-field" htmlFor={`${id}-json`}>{t.exportJson}</label>
      <textarea id={`${id}-json`} readOnly rows={4} value={json} spellCheck={false} />
      <button type="button" onClick={() => void copyText(json)}>{t.copy} — {t.exportJson}</button>

      <label className="theme-builder-field" htmlFor={`${id}-import`}>{t.import}</label>
      <textarea id={`${id}-import`} rows={4} maxLength={1024} value={importText} spellCheck={false} onChange={event => setImportText(event.target.value)} />
      <button type="button" onClick={importTheme}>{t.importAction}</button>
      <p role="status" aria-live="polite" className="theme-builder-hint">{status}</p>

      <footer className="theme-builder-footer">
        <button type="button" onClick={() => { setDraft(value => ({ ...value, palette: defaultAppearance.palette, custom: defaultAppearance.custom })); setStatus('') }}>{t.reset}</button>
        <button type="button" onClick={onClose}>{t.cancel}</button>
        <button type="button" className="theme-builder-apply" disabled={report.blocked} onClick={apply}>{t.apply}</button>
      </footer>
    </div>
  </div>, document.body)
}
