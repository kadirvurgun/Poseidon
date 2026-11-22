import { test } from 'node:test'
import assert from 'node:assert/strict'
import { existsSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { parseHelpDocument, type HelpLocale } from '../../../frontend/src/help/help-model.ts'
import { enterpriseSections } from '../../../frontend/src/product/enterprise-sections.ts'
import { escapeHtml, groupGuides, helpSectionHeadings, loadManualInputs, ManualError, manualLocales, parseManualChapter, renderManual, repoRoot, roleChapterIds, roleChapters, type ManualInputs } from './manual.ts'

// USER-GUIDE-01 UG-4 (UG-DOC-01): offline manual generator. Every guide appears exactly once, every
// referenced screenshot exists and is embedded at its step, chapter and section headings follow the
// fixed order, authored text is escaped, output is deterministic and never claims a review.

const inputs = new Map<HelpLocale, ManualInputs>(manualLocales.map(locale => [locale, loadManualInputs(locale)]))
const html = new Map<HelpLocale, string>(manualLocales.map(locale => [locale, renderManual(inputs.get(locale)!)]))
const guideFiles = (locale: HelpLocale) => readdirSync(join(repoRoot, 'frontend/src/help/content', locale)).filter(name => name.endsWith('.md')).map(name => name.slice(0, -3)).sort()
const count = (text: string, needle: string) => text.split(needle).length - 1
const headings = (text: string, level: number) => [...text.matchAll(new RegExp(`<h${level}[^>]*>([\\s\\S]*?)</h${level}>`, 'g'))].map(match => match[1].replace(/<[^>]+>/g, ''))
const decode = (text: string) => text.replace(/&#39;/g, "'").replace(/&quot;/g, '"').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&amp;/g, '&')

for (const locale of manualLocales) {
  test(`${locale}: every page guide appears exactly once, in a sidebar group, with its 8 sections in order`, () => {
    const out = html.get(locale)!
    const keys = guideFiles(locale)
    assert.ok(keys.length >= 53, `only ${keys.length} guides`)
    assert.deepEqual(inputs.get(locale)!.guides.map(guide => guide.meta.key).sort(), keys)
    for (const key of keys) {
      assert.equal(count(out, `<article class="guide" id="page-${key}">`), 1, key)
      const article = out.slice(out.indexOf(`id="page-${key}"`), out.indexOf('</article>', out.indexOf(`id="page-${key}"`)))
      assert.deepEqual(headings(article, 4).filter(text => helpSectionHeadings[locale].includes(decode(text))).map(decode), [...helpSectionHeadings[locale]], key)
    }
    const grouped = groupGuides(inputs.get(locale)!.guides).flatMap(group => group.guides.map(guide => guide.meta.key))
    assert.deepEqual([...grouped].sort(), keys)
    // Order of the guides in the document follows the sidebar order.
    const positions = grouped.map(key => out.indexOf(`id="page-${key}"`))
    assert.deepEqual(positions, [...positions].sort((a, b) => a - b))
  })

  test(`${locale}: chapters follow UG-DOC-01 order (getting started, model, roles, pages, glossary, troubleshooting)`, () => {
    const out = html.get(locale)!
    const ids = ['contents', 'chapter-getting-started', 'chapter-common-model', 'chapter-roles', 'page-guides', 'chapter-glossary', 'chapter-troubleshooting']
    const positions = ids.map(id => out.indexOf(`id="${id}"`))
    for (const [index, position] of positions.entries()) assert.ok(position > 0, ids[index])
    assert.deepEqual(positions, [...positions].sort((a, b) => a - b))
    const roleIds = roleChapters.map(role => out.indexOf(`id="role-${role.id}"`))
    assert.ok(roleIds.every(position => position > positions[3] && position < positions[4]))
    assert.deepEqual(roleIds, [...roleIds].sort((a, b) => a - b))
    // Every in-document link has a target.
    for (const match of out.matchAll(/href="#([^"]+)"/g)) assert.ok(out.includes(`id="${match[1]}"`), `dangling #${match[1]}`)
    // h1 is the cover only; the getting-started chapter explains the help panel and its shortcut.
    assert.equal(headings(out, 1).length, 1)
    const start = out.slice(positions[1], positions[2])
    assert.match(start, /Shift<\/code> \+ <code>\?<\/code>/)
  })

  test(`${locale}: every listed screenshot exists and is embedded once at its step; unavailable steps say so`, () => {
    const out = html.get(locale)!
    let total = 0
    for (const guide of inputs.get(locale)!.guides) {
      const shots = inputs.get(locale)!.screenshots[guide.meta.key]
      assert.deepEqual(shots.map(shot => shot.step), guide.meta.screenshots)
      for (const shot of shots) {
        assert.ok(existsSync(join(repoRoot, shot.path)), shot.path)
        const encoded = Buffer.from(shot.bytes).toString('base64')
        assert.equal(count(out, encoded), 1, shot.path)
        // The picture sits inside the numbered step it illustrates.
        const article = out.slice(out.indexOf(`id="page-${guide.meta.key}"`))
        const steps = article.slice(article.indexOf('<ol class="steps">'), article.indexOf('</ol>', article.indexOf('<ol class="steps">')))
        const items = steps.split('<li>').slice(1)
        assert.ok(items[shot.step - 1].includes(encoded), `${shot.path} not at step ${shot.step}`)
        total++
      }
    }
    assert.equal(count(out, '<img '), total)
    assert.ok(total >= 30, `only ${total} screenshots`)
    const mapping = inputs.get(locale)!.mapping
    const unavailable = Object.values(mapping.guides).flatMap(guide => guide.steps.filter(step => step.unavailable)).length
    assert.equal(count(out, '<p class="no-shot"'), unavailable)
    // Images come only from the generator as inline PNG data: no external or relative fetch.
    for (const match of out.matchAll(/<img src="([^"]{0,40})/g)) assert.match(match[1], /^data:image\/png;base64,/)
    assert.doesNotMatch(out, /(src|href)="(https?:|\/\/|javascript:)/i)
  })

  test(`${locale}: draft banner, glossary of states and no approval claim`, () => {
    const out = html.get(locale)!
    assert.match(out, /<p class="draft-banner" role="note">/)
    assert.equal(count(out, '<p class="draft-badge"'), guideFiles(locale).length)
    const glossary = out.slice(out.indexOf('id="chapter-glossary"'), out.indexOf('id="chapter-troubleshooting"'))
    for (const term of ['UNKNOWN', 'REVIEW_REQUIRED', 'READY', 'CONDITIONAL', 'NOT_READY']) assert.match(glossary, new RegExp(`<strong>${term}\\b`))
    assert.match(glossary, locale === 'tr' ? /Tamamlandı ≠ Doğrulandı ≠ Etkili/ : /Completed ≠ Verified ≠ Effective/)
    const body = decode(out.replace(/<[^>]+>/g, ' ')).toLocaleLowerCase(locale)
    assert.doesNotMatch(body, /unknown\s*(=|means|is treated as)\s*(no|ready|not applicable)\b/)
    assert.doesNotMatch(body, /bilinmiyor\s*(=|ise|demek|anlamına gelir)\s*(hayır|hazır)/)
    assert.doesNotMatch(body, /(gemi|römorkör)\s+kalkabilir|is safe to (sail|depart)|may depart/)
    assert.doesNotMatch(body, locale === 'tr' ? /onaylanmış kılavuz|uzman tarafından onaylandı/ : /approved manual|reviewed and approved|expert approved/)
  })

  test(`${locale}: rendering is deterministic`, () => {
    assert.equal(renderManual(loadManualInputs(locale)), html.get(locale))
  })
}

test('TR and EN manuals contain the same pages, role chapters and screenshot steps', () => {
  const [tr, en] = manualLocales.map(locale => inputs.get(locale)!)
  assert.deepEqual(tr.guides.map(guide => guide.meta.key), en.guides.map(guide => guide.meta.key))
  for (const [index, guide] of tr.guides.entries()) {
    assert.deepEqual(roleChapterIds(guide), roleChapterIds(en.guides[index]))
    assert.deepEqual(guide.meta.screenshots, en.guides[index].meta.screenshots)
  }
})

const sample = (body: string) => `---
key: today
title: <b>Today</b> & "more"
roles: [Master]
permissions: [canRead]
related: [fleet]
screenshots: []
reviewed: false
---
## What this page is for
${body}

## Who uses it
x

## Before you start
x

## Step by step
1. First <img src=x onerror=alert(1)>
2. Second [route](/app/fleet) and [hidden](/app/not-a-guide)

## What the states mean
x

## What you cannot do / caution
x

## If something goes wrong
x

## Related pages
`

test('escapes authored text and keeps links inside the document (safe subset, no raw HTML)', () => {
  const base = inputs.get('en')!
  const today = parseHelpDocument(sample('Hello <script>alert("x")</script> **bold <i>** `<code>` [x](javascript:alert(1)) [y](https://example.invalid) [z](data:text/html,1) [a](#local-part)'), 'en')
  const fleet = base.guides.find(guide => guide.meta.key === 'fleet')!
  const out = renderManual({ ...base, guides: [today, fleet], screenshots: { today: [], fleet: [] }, mapping: { guides: {} } })
  assert.doesNotMatch(out, /<script|<img src=x|<i>|<b>Today/)
  assert.match(out, /Hello &lt;script&gt;alert\(&quot;x&quot;\)&lt;\/script&gt;/)
  assert.match(out, /<strong>bold &lt;i&gt;<\/strong>/)
  assert.match(out, /<code>&lt;code&gt;<\/code>/)
  assert.match(out, /&lt;b&gt;Today&lt;\/b&gt; &amp; &quot;more&quot;/)
  assert.match(out, /First &lt;img src=x onerror=alert\(1\)&gt;/)
  assert.doesNotMatch(out, /javascript:|https:\/\/example|data:text/)
  assert.match(out, /<a href="#page-fleet">route<\/a>/)
  assert.match(out, / and hidden<\/li>/)
  assert.match(out, /<a href="#page-today-local-part">a<\/a>/)
  assert.equal(escapeHtml(`<>&"'`), '&lt;&gt;&amp;&quot;&#39;')
})

test('fails closed on unplaced guides, unmapped roles and malformed or reviewed manual chapters', () => {
  const base = inputs.get('en')!
  const stray = parseHelpDocument(sample('x').replace('key: today', 'key: not-in-sidebar'), 'en')
  assert.throws(() => groupGuides([...base.guides, stray]), (error: Error) => error instanceof ManualError && /manual_guide_unplaced:not-in-sidebar/.test(error.message))
  const role = parseHelpDocument(sample('x').replace('roles: [Master]', 'roles: [Captain Nemo]'), 'en')
  assert.throws(() => roleChapterIds(role), /manual_role_unmapped/)
  const chapter = '---\nkey: glossary\ntitle: Glossary\nreviewed: false\n---\n## A\nbody\n'
  assert.equal(parseManualChapter(chapter).sections.length, 1)
  assert.throws(() => parseManualChapter(chapter.replace('reviewed: false', 'reviewed: { by: AI, date: 2026-11-22 }')), /manual_reviewed_must_be_false/)
  assert.throws(() => parseManualChapter(chapter.replace('key: glossary', 'key: other')), /manual_chapter_key_invalid/)
  assert.throws(() => parseManualChapter(chapter.replace('## A', 'stray\n## A')), /manual_body_outside_section/)
  assert.throws(() => parseManualChapter('---\nkey: glossary\ntitle: G\n---\n## A\n'), /manual_front_matter_fields_invalid/)
})

test('every sidebar section key is unique across sections', () => {
  const all = enterpriseSections.flatMap(section => section.keys)
  assert.equal(new Set(all).size, all.length)
})
