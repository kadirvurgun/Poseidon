import { useEffect, useId, useRef } from 'react'
import './form-error-summary.css'

export type FormErrorItem = Readonly<{ fieldId: string; label: string; message: string }>

/**
 * DEV-025 / 65.03 paragraph 167, 285-286: the first error summary comes to the top of the form,
 * every entry links to its field (which itself carries aria-invalid + aria-describedby), and
 * focus moves to the summary once per submit attempt (`attempt` changes). It is not a live
 * region: focus already announces it, and typing a correction never triggers an announcement.
 */
export function FormErrorSummary({ title, errors, attempt }: { title: string; errors: readonly FormErrorItem[]; attempt: number }) {
  const id = useId()
  const node = useRef<HTMLDivElement>(null)
  const shown = errors.length > 0
  useEffect(() => { if (attempt > 0 && shown) node.current?.focus() }, [attempt, shown])
  if (!shown) return null
  return <div ref={node} className="form-error-summary" tabIndex={-1} role="group" aria-labelledby={`${id}-title`}>
    <p id={`${id}-title`} className="form-error-summary-title">{title}</p>
    <ul>{errors.map(error => <li key={error.fieldId}><a href={`#${error.fieldId}`} onClick={event => {
      event.preventDefault()
      const field = document.getElementById(error.fieldId)
      field?.focus()
      field?.scrollIntoView?.({ block: 'center' })
    }}>{error.label}: {error.message}</a></li>)}</ul>
  </div>
}
