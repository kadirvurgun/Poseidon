import { readdirSync, readFileSync } from 'node:fs'
import { dirname, join, relative, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

// THEME-TOKEN-GUARD-01 (THEME-PALETTE-01 / THEME-SAFETY-01): every colour outside the token
// definition files comes from a CSS variable, so light/dark/system and every palette reach the
// whole product, and semantic warning/danger/info/UNKNOWN tones stay owned by protected tokens.
// A new hard-coded hex/rgb()/hsl()/oklch()/named colour in src fails this test with file:line.
const src = join(dirname(fileURLToPath(import.meta.url)), '..')

/** Explicit, small allowlist: the only files that may define literal colour values. */
export const colorLiteralAllowlist: Readonly<Record<string, string>> = {
  'design-system.css': 'token definitions: preset b3Zheoix4U + palette/semantic token values per mode',
  'theme/theme-tokens.ts': 'theme-builder palette scales (Tailwind v4 oklch) and contrast math',
  'theme/theme-builder.css': 'generated from theme-tokens.ts buildThemeStylesheet() (equality enforced by test)',
}

const namedColors = ['aliceblue', 'aqua', 'aquamarine', 'azure', 'beige', 'black', 'blue', 'brown', 'chartreuse', 'coral', 'crimson',
  'cyan', 'darkblue', 'darkgray', 'darkgreen', 'darkgrey', 'darkred', 'fuchsia', 'gold', 'gray', 'green', 'grey', 'indigo', 'ivory',
  'khaki', 'lavender', 'lightblue', 'lightgray', 'lightgreen', 'lightgrey', 'lime', 'magenta', 'maroon', 'navy', 'olive', 'orange',
  'orchid', 'pink', 'plum', 'purple', 'red', 'salmon', 'silver', 'skyblue', 'steelblue', 'tan', 'teal', 'tomato', 'turquoise',
  'violet', 'wheat', 'white', 'whitesmoke', 'yellow', 'yellowgreen']
const named = namedColors.join('|')
const colorFunction = /\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch)\(\s*[-\d.]/i
const colorProperty = /(?:color|background|border|outline|fill|stroke|shadow|caret|accent|decoration|column-rule)/i

type Finding = Readonly<{ file: string; line: number; text: string }>

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) return entry.name === 'generated' ? [] : sourceFiles(path)
    if (!/\.(css|ts|tsx)$/.test(entry.name) || /\.test\.tsx?$/.test(entry.name) || entry.name === 'test-setup.ts') return []
    return [path]
  })
}

/** Blanks comments but keeps newlines so reported line numbers stay exact. */
function blankComments(text: string, typescript: boolean): string {
  const blank = (match: string) => match.replace(/[^\n]/g, ' ')
  const withoutBlocks = text.replace(/\/\*[\s\S]*?\*\//g, blank)
  return typescript ? withoutBlocks.replace(/^\s*\/\/.*$/gm, blank) : withoutBlocks
}

export function findColorLiterals(file: string, text: string): Finding[] {
  const typescript = !file.endsWith('.css')
  const lines = blankComments(text, typescript).split('\n')
  const findings: Finding[] = []
  lines.forEach((line, index) => {
    const hits: string[] = []
    if (typescript) {
      // Only string literals that are exactly a colour; ids/anchors such as "#main" never match.
      hits.push(...Array.from(line.matchAll(/['"`]#(?:[0-9a-f]{3,4}|[0-9a-f]{6}|[0-9a-f]{8})['"`]/gi), m => m[0]))
      if (colorFunction.test(line)) hits.push(colorFunction.exec(line)![0])
      hits.push(...Array.from(line.matchAll(new RegExp(`(?:color|background|backgroundColor|borderColor|fill|stroke)\\s*:\\s*['"\`](?:${named})['"\`]`, 'gi')), m => m[0]))
    } else {
      // CSS: inspect declaration values only (selectors like #root or .white-space are not colours).
      for (const declaration of line.split(/[;{}]/)) {
        const separator = declaration.indexOf(':')
        if (separator < 0) continue
        const property = declaration.slice(0, separator).trim()
        const value = declaration.slice(separator + 1)
        if (!/^-{0,2}[a-z][\w-]*$/i.test(property)) continue
        const hex = /#(?:[0-9a-f]{3,4}|[0-9a-f]{6}|[0-9a-f]{8})\b/i.exec(value)
        if (hex) hits.push(hex[0])
        if (colorFunction.test(value)) hits.push(colorFunction.exec(value)![0])
        if (colorProperty.test(property)) {
          const word = new RegExp(`(?:^|[\\s,(])(${named})(?=$|[\\s,)!])`, 'i').exec(value)
          if (word) hits.push(word[1])
        }
      }
    }
    for (const hit of hits) findings.push({ file, line: index + 1, text: hit })
  })
  return findings
}

describe('THEME-TOKEN-GUARD-01 hard-coded colour guard', () => {
  it('finds no colour literals outside the explicit token definition allowlist', () => {
    const findings = sourceFiles(src).flatMap(path => {
      const file = relative(src, path).split(sep).join('/')
      return file in colorLiteralAllowlist ? [] : findColorLiterals(file, readFileSync(path, 'utf8'))
    })
    expect(findings.map(f => `${f.file}:${f.line} ${f.text}`)).toEqual([])
  })

  it('keeps the allowlist small and every allowlisted file present', () => {
    expect(Object.keys(colorLiteralAllowlist).length).toBeLessThanOrEqual(3)
    for (const file of Object.keys(colorLiteralAllowlist)) expect(() => readFileSync(join(src, file), 'utf8')).not.toThrow()
  })

  it('detects each literal form and ignores tokens, selectors, comments and anchors', () => {
    const css = [
      '.a { color: #63758c; }',
      '.b { box-shadow: 0 1px 2px rgb(16 45 62 / .65); }',
      '.c { background: white; }',
      '#root .white-space { white-space: normal; color: var(--foreground); }',
      '/* #ffffff documented */ .d { border-color: color-mix(in srgb, var(--primary) 20%, transparent); }',
      '.e { outline: 2px solid oklch(0.5 0.1 200); }',
    ].join('\n')
    expect(findColorLiterals('x.css', css).map(f => `${f.line} ${f.text}`)).toEqual(['1 #63758c', '2 rgb(1', '3 white', '6 oklch(0'])
    const tsx = [
      "const a = { color: '#4381da' }",
      "const href = '#main'",
      '// rgb(1 2 3) in a comment',
      "const s = { backgroundColor: 'red' }",
      "const v = 'var(--chart-2)'",
    ].join('\n')
    expect(findColorLiterals('x.tsx', tsx).map(f => `${f.line} ${f.text}`)).toEqual(["1 '#4381da'", "4 backgroundColor: 'red'"])
  })
})
