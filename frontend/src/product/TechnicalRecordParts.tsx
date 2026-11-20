import type { ReactNode } from 'react'
import { Button } from '../components/ui/button'
import type { TechnicalIssue } from './technical-records-api'

type Copy = { offline: string; conflict: string; incomplete: string; forbidden: string; stale: string; error: string; reload: string }

// Shared states for the DEV-055/DEV-056/DEV-103 screens: offline, conflict, incomplete, forbidden, stale, error.
export function TechnicalBanner({ issue, t, onReload, missing }: { issue: TechnicalIssue; t: Copy; onReload?: () => void; missing?: string[] }) {
  if (!issue) return null
  const text = { offline: t.offline, conflict: t.conflict, incomplete: t.incomplete, forbidden: t.forbidden, stale: t.stale, error: t.error }[issue]
  return <div className={`technical-banner technical-banner-${issue}`} role={issue === 'offline' ? 'status' : 'alert'} data-issue={issue}>
    <p>{text}</p>
    {missing && missing.length > 0 && <ul>{missing.map(code => <li key={code}><code>{code}</code></li>)}</ul>}
    {onReload && issue !== 'offline' && <Button variant="outline" size="sm" onClick={onReload}>{t.reload}</Button>}
  </div>
}
export function TechnicalField({ label, children, hint }: { label: string; children: ReactNode; hint?: string }) {
  return <label className="technical-field"><span>{label}</span>{children}{hint && <small>{hint}</small>}</label>
}
