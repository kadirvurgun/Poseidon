import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, it } from 'vitest'
import { todayPriorityTone } from './TodayWorkspace'


// THEME-SAFETY-01 / QA note 21.11: the Today card stripe and its badge carry the same meaning.
const css = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'enterprise.css'), 'utf8')
/** Value of the first border-inline-start* declaration in the rule whose selector is exactly `selector`. */
const stripe = (selector: string) => {
  const start = css.indexOf(`${selector} {`)
  if (start < 0) return ''
  const body = css.slice(start + selector.length + 2, css.indexOf('}', start))
  return /border-inline-start[^:]*:\s*([^;]+);/.exec(body)?.[1].trim() ?? ''
}

it('maps Attention and Review to warning and everything else to neutral, never info/primary', () => {
  expect(todayPriorityTone('Attention')).toBe('warning')
  expect(todayPriorityTone('Review')).toBe('warning')
  expect(todayPriorityTone('Info')).toBe('neutral')
  expect(todayPriorityTone('UNKNOWN')).toBe('neutral')
})

it('uses warning stripes for warning badges and a muted stripe for the neutral default', () => {
  expect(stripe('.enterprise-shell .today-feed-card[data-priority="Attention"]')).toBe('var(--warning-border)')
  expect(stripe('.enterprise-shell .today-feed-card[data-priority="Review"]')).toBe('var(--warning-border)')
  expect(stripe('.enterprise-shell .today-feed-card')).toBe('3px solid var(--muted-foreground)')
  expect(css).not.toMatch(/today-feed-card[^{]*\{[^}]*var\(--(?:primary|info-text|info-border)\)/)
})
