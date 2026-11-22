// USER-GUIDE-01 UG-2 (D4/D5, UG-DOC-02/03): deterministic documentation screenshots of every guide's
// "Step by step" section, captured against the local OIDC lab (scripts/oidc_lab.py screenshots).
//
// Modes (POSEIDON_SCREENSHOTS_MODE): `compare` (default) re-captures and pixel-diffs against the
// committed PNGs without writing them; `update` regenerates PNG + provenance JSON; `discover` writes
// the accessible tree of each guide route to the private lab directory to help author steps.json.
// Like lab/run.ts this is a direct Playwright library runner: no test-runner DOM snapshots, traces,
// videos or HAR. Only bounded counts/codes reach the private report; credentials never leave memory.
import { labRuntimeFromEnvironment } from '../lab/lab-runtime.ts'
import { strictContext } from '../lab/strict-browser.ts'
import { login } from '../lab/journeys.ts'
import { expect, type Locator, type Page } from '@playwright/test'
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { clickKind, clickName, screenshotLocales, shotName, validateClick, validateMapping, viewports, type ClickTarget, type Localized, type ScreenshotLocale, type Target, type ViewportName } from './steps.ts'
import { paletteScreenshot } from './png-palette.ts'
import { allowedDatasets, generator, generatorFiles, gitBlobSha1, provenanceSchema, scanDomText, visibleTenant, type Provenance } from './provenance.ts'

const mode = process.env.POSEIDON_SCREENSHOTS_MODE ?? 'compare'
if (!['compare', 'update', 'discover'].includes(mode)) throw new Error('screenshot_mode_invalid')
const root = resolve(import.meta.dirname, '../../..')
const output = resolve(root, 'frontend/src/help/screenshots')
const privateRoot = process.env.POSEIDON_LAB_PRIVATE!
const mapping = validateMapping(JSON.parse(readFileSync(resolve(output, 'steps.json'), 'utf8')))
const git = (...args: string[]) => execFileSync('git', args, { cwd: root, encoding: 'utf8' }).trim()
const commit = git('rev-parse', 'HEAD')
// QA N3: provenance identifies the generator by the git blob of each generator file and records
// whether the working tree differed from HEAD. `update` refuses a dirty generator, so committed
// screenshots always point at a commit that contains exactly the capture code and mapping used.
const generatorBlobs = Object.fromEntries(generatorFiles.map(path => [path, gitBlobSha1(readFileSync(resolve(root, path)))]))
const dirty = git('status', '--porcelain', '--', ...generatorFiles) !== '' ||
  generatorFiles.some(path => git('rev-parse', `HEAD:${path}`) !== generatorBlobs[path])
if (mode === 'update' && dirty) throw new Error('screenshot_generator_dirty')
// Midnight UTC of the capture day: never later than a server timestamp issued that day, so a short-lived
// server validity (departure readiness, 60 s) is not shown as stale merely because the browser clock is pinned.
const fixedClock = '2026-11-22T00:00:00.000Z'
const footerHeight = 22
const threshold = Number(process.env.POSEIDON_SCREENSHOTS_THRESHOLD ?? '0.002')
const onlyGuide = process.env.POSEIDON_SCREENSHOTS_GUIDE
const runViewports = (process.env.POSEIDON_SCREENSHOTS_VIEWPORTS?.split(',') ?? mapping.committedViewports) as ViewportName[]
if (runViewports.some(item => !(item in viewports))) throw new Error('screenshot_viewport_invalid')
const credentials = JSON.parse(readFileSync(resolve(privateRoot, 'credentials.json'), 'utf8')) as Record<string, { password: string }>
const secrets = Object.values(credentials).map(item => item.password)

type Shot = { name: string; result: 'written' | 'match' | 'diff' | 'missing' | 'error' | 'discovered'; ratio?: number; findings?: string[]; failure?: string; bytes?: number }
const report = { status: 'running', mode, commit, threshold, shots: [] as Shot[] }
const persist = () => writeFileSync(resolve(privateRoot, 'screenshot-report.json'), JSON.stringify(report, null, 1))
persist()

// Bounded failure code: first line only, URLs and quoted values removed.
const code = (error: unknown) => (error instanceof Error ? error.message : 'step_failed').split('\n')[0]
  .replace(/https?:\/\/\S+/g, '<url>').slice(0, 240)

const pattern = (value: string) => value.startsWith('re:') ? new RegExp(value.slice(3)) : value
function locate(scope: Page | Locator, target: Target, locale: ScreenshotLocale): Locator {
  const base = target.within ? locate(scope, target.within, locale) : scope
  const text = (value: Localized) => pattern(value[locale])
  const exact = (value?: Localized) => value ? !value[locale].startsWith('re:') : undefined
  const found = target.role
    ? base.getByRole(target.role as Parameters<Page['getByRole']>[0], target.name ? { name: text(target.name), exact: exact(target.name) } : {})
    : target.label ? base.getByLabel(text(target.label), { exact: exact(target.label) }) : base.getByText(text(target.text!), { exact: exact(target.text) })
  return target.nth === undefined ? found.first() : found.nth(target.nth)
}

/** QA N1: resolves an allow-listed click and re-checks the real element right before clicking. */
async function verifiedClick(page: Page, target: ClickTarget, locale: ScreenshotLocale) {
  validateClick(target)
  const scope = target.within ? locate(page, target.within, locale) : page
  const name = clickName(target)[locale]
  const element = clickKind(target) === 'button'
    ? scope.getByRole('button', { name, exact: true })
    : scope.locator('summary').filter({ hasText: name })
  await expect(element).toHaveCount(1)
  if (clickKind(target) === 'button') {
    await expect(element).toHaveRole('button')
    await expect(element).toHaveAccessibleName(name)
  } else {
    // A disclosure's computed name also carries CSS-generated decoration (the +/− marker), so its own
    // text content must equal the allow-listed name exactly, and it must be a closed <details> summary.
    const state = await element.evaluate(node => ({ tag: node.tagName, text: node.textContent?.trim(),
      closed: node.parentElement?.tagName === 'DETAILS' && !(node.parentElement as HTMLDetailsElement).open }))
    if (state.tag !== 'SUMMARY' || state.text !== name || !state.closed) throw new Error('click_disclosure_mismatch')
  }
  await element.click()
}

async function settle(page: Page) {
  await page.waitForLoadState('networkidle')
  // A pane still announcing "Loading…" (role=status) or aria-busy resolves later (for example the vessel
  // file list against the lab's absent file service); wait for it, bounded, so its end state is captured.
  await page.waitForFunction(() => ![...document.querySelectorAll('[role="status"], [aria-busy="true"]')].some(node =>
    node.getAttribute('aria-busy') === 'true' || /loading|yükleniyor/i.test(node.textContent ?? '')), undefined, { timeout: 10_000 }).catch(() => undefined)
  await page.evaluate(() => document.fonts.ready.then(() => undefined))
  await page.waitForTimeout(300)
}

async function overlay(page: Page, boxes: { x: number; y: number; width: number; height: number }[], step: number) {
  await page.evaluate(({ boxes, step, stamp, footerHeight }) => {
    const host = document.createElement('div')
    host.id = 'poseidon-doc-overlay'
    host.setAttribute('aria-hidden', 'true')
    host.style.cssText = 'position:fixed;inset:0;pointer-events:none;z-index:2147483647'
    for (const [index, box] of boxes.entries()) {
      const ring = document.createElement('div')
      ring.style.cssText = `position:absolute;left:${box.x - 4}px;top:${box.y - 4}px;width:${box.width + 8}px;height:${box.height + 8}px;` +
        'border:3px solid var(--primary);border-radius:8px;box-shadow:0 0 0 3px color-mix(in oklch, var(--primary) 25%, transparent)'
      const badge = document.createElement('div')
      badge.textContent = String(step)
      // QA N5: the badge sits outside the ring (left of it, or above it near the left edge) so it never covers text.
      const outside = box.x >= 40
      const left = outside ? box.x - 38 : Math.max(2, box.x - 4)
      const top = Math.max(2, Math.min(window.innerHeight - footerHeight - 30, outside ? box.y - 4 : box.y - 36))
      badge.style.cssText = `position:absolute;left:${left}px;top:${top}px;width:28px;height:28px;border-radius:50%;display:flex;` +
        'align-items:center;justify-content:center;background:var(--primary);color:var(--primary-foreground);font:700 15px/1 system-ui,sans-serif;' +
        'box-shadow:0 0 0 2px var(--background)'
      // One number per step: only the first target gets the badge, so badges never cover adjacent rings.
      if (index === 0) host.append(ring, badge)
      else host.append(ring)
    }
    const footer = document.createElement('div')
    footer.textContent = stamp
    footer.style.cssText = `position:absolute;left:0;right:0;bottom:0;height:${footerHeight}px;display:flex;align-items:center;padding:0 10px;` +
      'background:var(--muted);color:var(--muted-foreground);font:12px/1 system-ui,sans-serif;border-top:1px solid var(--border)'
    host.append(footer)
    document.body.append(host)
  }, { boxes, step, footerHeight, stamp: `POSEIDON · ${commit.slice(0, 12)} · synthetic data · USER-GUIDE-01 UG-2` })
}

/**
 * QA N5 + mobile composition: keeps the natural page when every target is already fully visible below
 * any sticky/fixed header and above the commit footer. Otherwise it scrolls so that the union of all
 * targets sits just below that header when the union fits the viewport (needed on the 390 px mobile
 * viewport, where stacked targets rarely share one screen), else it falls back to the first target.
 */
async function reveal(page: Page, targets: Locator[]) {
  const handles = await Promise.all(targets.map(target => target.elementHandle()))
  await page.evaluate(([elements, footerHeight]) => {
    const nodes = elements as Element[]
    const union = (list: Element[]) => {
      const boxes = list.map(node => node.getBoundingClientRect())
      return { top: Math.min(...boxes.map(box => box.top)), bottom: Math.max(...boxes.map(box => box.bottom)) }
    }
    // Bottom edge of any sticky/fixed layer painted above the element's top-left corner.
    const cover = (element: Element) => {
      const box = element.getBoundingClientRect()
      let bottom = 0
      for (const node of document.elementsFromPoint(Math.min(window.innerWidth - 1, Math.max(0, box.left + 8)), Math.min(window.innerHeight - 1, Math.max(0, box.top + 2)))) {
        if (node === element || element.contains(node) || node.contains(element)) break
        for (let current: Element | null = node; current; current = current.parentElement) {
          const position = getComputedStyle(current).position
          if (position === 'sticky' || position === 'fixed') { bottom = Math.max(bottom, current.getBoundingClientRect().bottom); break }
        }
      }
      return bottom
    }
    // Start from the top of every scroll container: a preparation click scrolls its button into view by
    // an amount that depends on how far late content had rendered, which must not decide the composition.
    for (const node of nodes) for (let container = node.parentElement; container; container = container.parentElement) container.scrollTop = 0
    window.scrollTo(0, 0)
    const topmost = (list: Element[]) => list.reduce((best, node) => node.getBoundingClientRect().top < best.getBoundingClientRect().top ? node : best)
    const available = window.innerHeight - footerHeight - 4
    const all = union(nodes)
    if (all.top >= cover(topmost(nodes)) + 8 && all.bottom <= available) return
    // The union fits below a header of similar height: compose all targets; otherwise only the first.
    const group = all.bottom - all.top <= available - cover(topmost(nodes)) - 48 ? nodes : [nodes[0]]
    const anchor = topmost(group)
    anchor.scrollIntoView({ block: 'start', inline: 'nearest' })
    const delta = cover(anchor) + 40 - union(group).top
    if (delta <= 0) return
    let container: Element | null = anchor.parentElement
    while (container && !(container.scrollHeight > container.clientHeight && /(auto|scroll)/.test(getComputedStyle(container).overflowY))) container = container.parentElement
    if (container) container.scrollTop -= delta
    else window.scrollBy(0, -delta)
  }, [handles, footerHeight] as const)
  await Promise.all(handles.map(handle => handle?.dispose()))
}

/** Waits (bounded, 3 s) until every target's box and the page height are unchanged across two 150 ms samples. */
async function steady(page: Page, targets: Locator[]) {
  let previous = ''
  for (let attempt = 0; attempt < 20; attempt++) {
    const sample = JSON.stringify([await Promise.all(targets.map(target => target.boundingBox())), await page.evaluate(() => document.documentElement.scrollHeight)])
    if (sample === previous) return
    previous = sample
    await page.waitForTimeout(150)
  }
}

/** Fraction of differing pixels (ignoring the commit footer) decoded in an isolated about:blank page. */
async function difference(blank: Page, previous: Buffer, current: Buffer): Promise<number> {
  return blank.evaluate(async ({ a, b, footerHeight }) => {
    const decode = async (base64: string) => {
      const bytes = Uint8Array.from(atob(base64), char => char.charCodeAt(0))
      const bitmap = await createImageBitmap(new Blob([bytes], { type: 'image/png' }))
      const canvas = new OffscreenCanvas(bitmap.width, bitmap.height)
      const context = canvas.getContext('2d')!
      context.drawImage(bitmap, 0, 0)
      return context.getImageData(0, 0, bitmap.width, bitmap.height)
    }
    const [left, right] = await Promise.all([decode(a), decode(b)])
    if (left.width !== right.width || left.height !== right.height) return 1
    const rows = left.height - footerHeight
    let changed = 0
    for (let index = 0; index < left.width * rows * 4; index += 4) {
      const delta = Math.max(Math.abs(left.data[index] - right.data[index]), Math.abs(left.data[index + 1] - right.data[index + 1]), Math.abs(left.data[index + 2] - right.data[index + 2]))
      if (delta > 24) changed++
    }
    return changed / (left.width * rows)
  }, { a: previous.toString('base64'), b: current.toString('base64'), footerHeight })
}

const pngSize = (bytes: Buffer) => ({ width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) })

async function prepare(page: Page, locale: ScreenshotLocale) {
  await page.evaluate(locale => {
    localStorage.setItem('poseidon.locale', locale)
    localStorage.setItem('poseidon.appearance.v2', JSON.stringify({ v: 2, mode: 'light', palette: 'emerald' }))
  }, locale)
}

async function capture(page: Page, blank: Page, key: string, locale: ScreenshotLocale, viewport: ViewportName) {
  const guide = mapping.guides[key]
  for (const step of guide.steps) {
    if (step.unavailable) continue
    const name = shotName(locale, key, step.step, viewport)
    const shot: Shot = { name, result: 'error' }
    report.shots.push(shot)
    try {
      await page.goto(step.route ?? guide.route)
      await settle(page)
      for (const action of step.actions) {
        await verifiedClick(page, action.click, locale)
        await settle(page)
      }
      const targets = step.targets.map(target => locate(page, target, locale))
      // Late content (a details pane filling in, a focus scroll) must not race the composition: wait until
      // the targets stop moving, compose, then wait again before measuring.
      await steady(page, targets)
      await reveal(page, targets)
      await steady(page, targets)
      const size = viewports[viewport]
      const boxes = []
      for (const [index, target] of targets.entries()) {
        const box = await target.boundingBox()
        if (!box) throw new Error('target_not_visible:' + index)
        // A ring is drawn only for a target that is on screen; the first target must always be.
        const onScreen = box.y + box.height > 0 && box.y < size.height - footerHeight && box.x + box.width > 0 && box.x < size.width
        if (!onScreen && index === 0) throw new Error('target_off_screen:0')
        if (onScreen) boxes.push(box)
      }
      const domText = (await page.locator('body').innerText()).replace(/[ \t]+/g, ' ').replace(/\n{3,}/g, '\n\n').trim().slice(0, 20000)
      const findings = scanDomText(domText, secrets)
      if (findings.length) { shot.findings = findings; throw new Error('privacy_scan_failed') }
      await overlay(page, boxes, step.step)
      const masks = [page.locator('time'),
        page.getByText(/\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2}[./]\d{1,2}[./]\d{2,4}\b|\b\d{1,2}:\d{2}\b/),
        page.getByText(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|\b[0-9a-f]{8}…/i)]
      // Masks are painted in the page itself (not as a screenshot layer), so they keep the page's stacking
      // order: a sticky header or the commit footer still covers a masked value scrolled beneath it.
      // The masked value is also replaced by a fixed-width placeholder, so a timestamp whose length varies
      // between runs (trailing fractional-second digits) cannot shift the layout or the mask width.
      for (const mask of masks) await mask.evaluateAll(nodes => {
        const value = /\d{4}-\d{2}-\d{2}(?:[T ]\d{1,2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})?)?|\b\d{1,2}[./]\d{1,2}[./]\d{2,4}(?:,? \d{1,2}:\d{2}(?::\d{2})?)?|\b\d{1,2}:\d{2}(?::\d{2}(?:\.\d+)?)?\b|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}|\b[0-9a-f]{8}…/gi
        for (const node of nodes) {
          const walker = document.createTreeWalker(node, NodeFilter.SHOW_TEXT)
          for (let text = walker.nextNode(); text; text = walker.nextNode())
            text.textContent = node.tagName === 'TIME' ? (text.textContent?.trim() ? '0000-00-00 00:00' : text.textContent) : text.textContent!.replace(value, '0000-00-00 00:00')
        }
        for (const node of nodes) for (const element of [node, ...node.querySelectorAll('*')] as HTMLElement[]) {
          for (const [property, value] of [['color', 'transparent'], ['-webkit-text-fill-color', 'transparent'], ['background', '#c8cdd3'],
            ['border-color', 'transparent'], ['box-shadow', 'none'], ['text-shadow', 'none'], ['fill', 'transparent'], ['stroke', 'transparent']])
            element.style.setProperty(property, value, 'important')
        }
      })
      // Deterministic palette reduction (png-palette.ts) in every mode, so compare diffs like against like.
      const png = paletteScreenshot(await page.screenshot({ animations: 'disabled', caret: 'hide', scale: 'css' }))
      await page.evaluate(() => document.getElementById('poseidon-doc-overlay')?.remove())
      shot.bytes = png.length
      // Viewports not committed to the repository (see steps.json committedViewports) are written to the
      // private lab directory only, so a mobile verification run never adds uncommitted repo files.
      const folder = mapping.committedViewports.includes(viewport) ? output : resolve(privateRoot, 'screenshots-uncommitted')
      const file = resolve(folder, name + '.png')
      if (mode === 'update') {
        mkdirSync(dirname(file), { recursive: true })
        writeFileSync(file, png)
        const provenance: Provenance = { schemaVersion: provenanceSchema, generator, commit, generatorBlobs, dirty, key, locale, step: step.step, viewport,
          datasets: [...allowedDatasets], tenant: visibleTenant, fixedClock,
          png: { sha256: createHash('sha256').update(png).digest('hex'), ...pngSize(png) }, domText }
        writeFileSync(resolve(folder, name + '.json'), JSON.stringify(provenance, null, 1) + '\n')
        shot.result = 'written'
      } else if (!existsSync(file)) shot.result = 'missing'
      else {
        shot.ratio = await difference(blank, readFileSync(file), png)
        shot.result = shot.ratio <= threshold ? 'match' : 'diff'
      }
    } catch (error) {
      shot.result = 'error'
      shot.failure ??= code(error)
    } finally { persist() }
  }
}

async function discover(page: Page, key: string, locale: ScreenshotLocale) {
  mkdirSync(resolve(privateRoot, 'screenshot-discovery'), { recursive: true })
  const guide = mapping.guides[key]
  // The bare route plus every distinct step route, and the state after each step's own preparation actions.
  const routes = [...new Set(guide.steps.flatMap(item => item.route ? [item.route] : []))]
  for (const step of [{ step: 0, route: guide.route, actions: [] as typeof guide.steps[number]['actions'] },
    ...routes.map((route, index) => ({ step: 100 + index, route, actions: [] as typeof guide.steps[number]['actions'] })),
    ...guide.steps.filter(item => item.actions.length)]) {
    const name = `${locale}/${key}/${step.step}`
    try {
      await page.goto(step.route ?? guide.route)
      await settle(page)
      for (const action of step.actions) {
        await verifiedClick(page, action.click, locale)
        await settle(page)
      }
      const main = page.locator('main')
      writeFileSync(resolve(privateRoot, 'screenshot-discovery', `${key}.${locale}.${step.step}.yml`), await (await main.count() ? main : page.locator('body')).ariaSnapshot())
      report.shots.push({ name, result: 'discovered' })
    } catch (error) { report.shots.push({ name, result: 'error', failure: code(error) }) }
    persist()
  }
}

/**
 * One fresh browser context and real sign-in per (viewport, locale) block: the app session has a fixed
 * 30-minute lifetime without sliding renewal, and a full two-viewport run takes longer than that.
 */
async function session(viewport: ViewportName, locale: ScreenshotLocale, work: (page: Page, blank: Page) => Promise<void>) {
  const owned = await strictContext({ baseURL: labRuntimeFromEnvironment().appOrigin, viewport: viewports.desktop, reducedMotion: 'reduce', colorScheme: 'light' })
  try {
    const page = await owned.context.newPage()
    page.setDefaultTimeout(15_000)
    page.setDefaultNavigationTimeout(30_000)
    await login(page, 'lab-manager', '/app/today')
    await page.clock.setFixedTime(new Date(fixedClock))
    // Sign in on the desktop layout (the sign-in entry it asserts), then switch to the captured viewport.
    await page.setViewportSize(viewports[viewport])
    const blank = await owned.context.newPage()
    await blank.goto('about:blank')
    await prepare(page, locale)
    await work(page, blank)
  } finally { await owned.close() }
}

try {
  const keys = Object.keys(mapping.guides).filter(key => !onlyGuide || key === onlyGuide)
  if (mode === 'discover') {
    for (const locale of screenshotLocales) await session('desktop', locale, async page => { for (const key of keys) await discover(page, key, locale) })
  } else {
    for (const viewport of runViewports) for (const locale of screenshotLocales)
      await session(viewport, locale, async (page, blank) => { for (const key of keys) await capture(page, blank, key, locale, viewport) })
  }
  const bad = report.shots.filter(shot => !['written', 'match', 'discovered'].includes(shot.result))
  report.status = bad.length ? 'failed' : 'passed'
} catch (error) {
  report.status = 'failed'
  report.shots.push({ name: 'runner', result: 'error', failure: code(error) })
} finally { persist() }
process.exitCode = report.status === 'passed' ? 0 : 1
