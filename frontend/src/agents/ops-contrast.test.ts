import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, it } from 'vitest'
import { builtInPalettes, contrastRatio, presetB3Zheoix4U, selectionTokens, textContrastFloor, toHex } from '../theme/theme-tokens'

// QA note 21.11 (THEME-TOKEN-SWEEP-01): the .ops-ready badge (10px text) keeps >= 4.5:1 in every
// built-in palette and mode. Values are parsed from agents.css and mixed like CSS color-mix(in srgb).
const css = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'agents.css'), 'utf8')
const rule = /\.ops-ready\{color:var\(--([\w-]+)\);background:color-mix\(in srgb, var\(--([\w-]+)\) (\d+)%, var\(--([\w-]+)\)\);/.exec(css)

function mixSrgb(a: string, b: string, percentA: number): string {
  const channels = (hex: string) => [1, 3, 5].map(i => Number.parseInt(hex.slice(i, i + 2), 16))
  const [ca, cb] = [channels(toHex(a)!), channels(toHex(b)!)]
  return `#${ca.map((c, i) => Math.round(c * percentA / 100 + cb[i] * (1 - percentA / 100)).toString(16).padStart(2, '0')).join('')}`
}

it('keeps the ops ready badge text at or above 4.5:1 in every built-in palette and mode', () => {
  expect(rule).not.toBeNull()
  const [, text, tint, percent, surface] = rule!
  for (const palette of builtInPalettes) for (const mode of ['light', 'dark'] as const) {
    const tokens = selectionTokens({ palette, custom: presetB3Zheoix4U }, mode)
    const ratio = contrastRatio(tokens[text], mixSrgb(tokens[tint], tokens[surface], Number(percent)))
    expect(ratio, `${palette}/${mode}`).not.toBeNull()
    expect(ratio!, `${palette}/${mode}`).toBeGreaterThanOrEqual(textContrastFloor)
  }
})
