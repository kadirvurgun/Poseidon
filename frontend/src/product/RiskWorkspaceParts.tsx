import { localizedCopy } from '../i18n/messages'
import type { Locale as AppLocale } from '../i18n'
import type { ReactNode } from 'react'
import { Button } from '../components/ui/button'
import type { RiskResult } from './risk-api'

export const riskCopy = localizedCopy('RiskWorkspace_tsx_copy')
export type RiskCopy = (typeof riskCopy)[AppLocale]
export type RiskIssue = 'offline' | 'conflict' | 'incomplete' | 'forbidden' | 'stale' | 'error' | null

// A non-approved, unknown or not-assessed value is never rendered with the positive tone.
export function RiskState({ value, t }: { value: string; t: RiskCopy }) {
  const key = ('st_' + value) as keyof RiskCopy
  const label = typeof t[key] === 'string' ? t[key] as string : value
  const tone = value === 'APPROVED' || value === 'PUBLISHED' || value === 'CURRENT' ? 'recorded' : value === 'RETURNED' || value === 'SUPERSEDED' ? 'muted' : 'open'
  return <span className={`risk-badge risk-badge-${tone}`} data-state={value}>{label}</span>
}
export function RiskRatingResult({ result, t }: { result: RiskResult; t: RiskCopy }) {
  if (result.state === 'ASSESSED') return <span className="risk-badge risk-badge-open" data-state="ASSESSED">{result.result} · {result.rank}</span>
  return <span className="risk-badge risk-badge-unknown" data-state={result.state}>{result.state === 'METHOD_UNCONFIGURED' ? t.methodMissing : t.notAssessed}</span>
}
export function RiskBanner({ issue, t, onReload, missing }: { issue: RiskIssue; t: RiskCopy; onReload?: () => void; missing?: string[] }) {
  if (!issue) return null
  const text = { offline: t.offline, conflict: t.conflict, incomplete: t.incomplete, forbidden: t.forbidden, stale: t.stale, error: t.error }[issue]
  return <div className={`risk-banner risk-banner-${issue}`} role={issue === 'offline' ? 'status' : 'alert'} data-issue={issue}>
    <p>{text}</p>
    {missing && missing.length > 0 && <ul>{missing.map(code => <li key={code}><code>{code}</code></li>)}</ul>}
    {onReload && issue !== 'offline' && <Button variant="outline" size="sm" onClick={onReload}>{t.reload}</Button>}
  </div>
}
export function RiskField({ label, children, hint }: { label: string; children: ReactNode; hint?: string }) {
  return <label className="risk-field"><span>{label}</span>{children}{hint && <small>{hint}</small>}</label>
}
export function RiskTriState({ value, onChange, t, label }: { value: boolean | null; onChange: (value: boolean | null) => void; t: RiskCopy; label: string }) {
  return <RiskField label={label}><select value={value === null ? '' : value ? 'yes' : 'no'} onChange={e => onChange(e.target.value === '' ? null : e.target.value === 'yes')}>
    <option value="">{t.unknown}</option><option value="yes">{t.yes}</option><option value="no">{t.no}</option></select></RiskField>
}
export function RiskReason({ value, onChange, t }: { value: string; onChange: (value: string) => void; t: RiskCopy }) {
  return <RiskField label={t.reason}><textarea value={value} minLength={10} maxLength={1000} rows={2} onChange={e => onChange(e.target.value)}/></RiskField>
}
export const reasonValid = (value: string) => value.trim().length >= 10 && value.length <= 1000
