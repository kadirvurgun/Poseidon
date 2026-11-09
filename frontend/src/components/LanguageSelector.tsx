import { useId } from 'react'
import { Globe2 } from 'lucide-react'
import { isLocale, locales, nativeLocaleNames, text, useLanguageStatus } from '../i18n'
import type { Locale } from '../i18n'
import './LanguageSelector.css'

export type LanguageSelectorProps = { locale: Locale; onChange: (locale: Locale) => void; disabled?: boolean; className?: string; id?: string }

export function LanguageSelector({ locale, onChange, disabled = false, className = '', id }: LanguageSelectorProps) {
  const generatedId = useId(), selectId = id ?? generatedId
  const status = useLanguageStatus()
  return <div className={`language-selector ${className}`}>
    <label className="language-selector-control" htmlFor={selectId}>
      <Globe2 aria-hidden="true" />
      <span className="language-selector-label">{text(locale, 'common:chooseLanguage')}</span>
      <select id={selectId} value={locale} disabled={disabled} aria-label={text(locale, 'common:chooseLanguage')} aria-busy={status.pending !== null}
        aria-describedby={status.failed ? `${selectId}-error` : undefined}
        onChange={event => { if (isLocale(event.target.value)) onChange(event.target.value) }}>
        {locales.map(option => <option key={option} value={option} lang={option} dir={option === 'ar' ? 'rtl' : 'ltr'}>{nativeLocaleNames[option]}</option>)}
      </select>
    </label>
    {status.pending && <span className="language-selector-message" role="status">{text(locale, 'common:languageLoading')}</span>}
    {status.failed && <span className="language-selector-message" id={`${selectId}-error`} role="alert">{text(locale, 'common:languageLoadFailed')}</span>}
  </div>
}
