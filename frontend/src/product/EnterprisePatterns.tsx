import type { ComponentProps, ReactNode } from 'react'
import { Button } from '../components/ui/button'
import './enterprise-patterns.css'

export function EnterprisePageHeader({ title, eyebrow, description, actions, actionsLabel }: { title: ReactNode; eyebrow?: ReactNode; description?: ReactNode; actions?: ReactNode; actionsLabel?: string }) {
  return <div className="enterprise-page-heading enterprise-page-header"><div className="enterprise-heading-copy">{eyebrow && <p className="enterprise-page-eyebrow">{eyebrow}</p>}<h1>{title}</h1>{description && <p>{description}</p>}</div>{actions && <div className="enterprise-heading-actions" role={actionsLabel ? 'group' : undefined} aria-label={actionsLabel}>{actions}</div>}</div>
}

export function EnterpriseSectionHeader({ title, description, actions, headingId }: { title: ReactNode; description?: ReactNode; actions?: ReactNode; headingId?: string }) {
  return <div className="enterprise-section-header"><div className="enterprise-heading-copy"><h2 id={headingId}>{title}</h2>{description && <p>{description}</p>}</div>{actions && <div className="enterprise-heading-actions">{actions}</div>}</div>
}

export function EnterpriseActions({ children, align = 'end', layout = 'wrap', ...props }: ComponentProps<'div'> & { align?: 'start' | 'end' | 'between'; layout?: 'wrap' | 'form' }) {
  return <div {...props} data-layout={layout} role={props['aria-label'] ? 'group' : props.role} className={`enterprise-actions enterprise-actions-${align}${props.className ? ` ${props.className}` : ''}`}>{children}</div>
}

export function EnterpriseToolbar({ children, ...props }: ComponentProps<'div'>) {
  return <div {...props} className={`enterprise-toolbar${props.className ? ` ${props.className}` : ''}`}>{children}</div>
}

export function EnterpriseFormGrid({ children, ...props }: ComponentProps<'fieldset'>) {
  return <fieldset {...props} className={`enterprise-form-grid${props.className ? ` ${props.className}` : ''}`}>{children}</fieldset>
}

export function EnterpriseField({ label, htmlFor, children, ...props }: ComponentProps<'div'> & { label: ReactNode; htmlFor: string }) {
  return <div {...props} className={`enterprise-form-field${props.className ? ` ${props.className}` : ''}`}><label htmlFor={htmlFor}>{label}</label>{children}</div>
}

export function EnterprisePagination({ previous, next, onPrevious, onNext, previousDisabled, nextDisabled, children, label }: {
  previous: string; next: string; onPrevious: () => void; onNext: () => void; previousDisabled: boolean; nextDisabled: boolean; children?: ReactNode; label: string
}) {
  return <nav className="enterprise-pagination" aria-label={label}>{children && <span className="enterprise-pagination-summary">{children}</span>}<div><Button variant="outline" size="sm" disabled={previousDisabled} onClick={onPrevious}>{previous}</Button><Button variant="outline" size="sm" disabled={nextDisabled} onClick={onNext}>{next}</Button></div></nav>
}
