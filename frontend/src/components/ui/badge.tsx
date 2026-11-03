import type { ComponentProps } from 'react'
import { cn } from '../../lib/utils'

export function Badge({ className, tone = 'neutral', ...props }: ComponentProps<'span'> & { tone?: 'neutral' | 'info' | 'warning' | 'danger' }) {
  return <span data-slot="badge" data-tone={tone} className={cn('inline-flex items-center gap-1.5 rounded-md border px-2 py-0.5 text-xs font-medium whitespace-nowrap', className)} {...props} />
}
