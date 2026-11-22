// USER-GUIDE-01 UG-2 (D4, UG-DOC-02): closed schema of the per-step screenshot mapping that lives
// next to the guides (frontend/src/help/screenshots/steps.json). Pure; no browser or filesystem.
//
// A step names the page route, an optional list of preparation clicks and the elements that receive
// the numbered marker. Marker targets use accessible role + name, label or text; a CSS selector is
// never accepted. Preparation clicks are limited to the explicit allow-list below (QA N1): a mapping
// edit alone can never introduce a new click, and the runner re-checks the element's real role and
// accessible name in the browser immediately before clicking.

export const screenshotLocales = ['tr', 'en'] as const
export type ScreenshotLocale = (typeof screenshotLocales)[number]
export const viewports = Object.freeze({ desktop: { width: 1440, height: 900 }, mobile: { width: 390, height: 844 } })
export type ViewportName = keyof typeof viewports

export type Localized = Readonly<Record<ScreenshotLocale, string>>
export type Target = Readonly<{
  role?: string
  /** Accessible name, exact unless it starts with "re:" (then a case-sensitive RegExp source). */
  name?: Localized
  label?: Localized
  text?: Localized
  nth?: number
  within?: Target
}>
/** A preparation click: an allow-listed button or disclosure <summary>, exact names, optionally scoped. */
export type ClickTarget = Readonly<{ role: 'button'; name: Localized; within?: Target } | { disclosure: Localized; within?: Target }>
export type Action = Readonly<{ click: ClickTarget }>

/**
 * The only clicks a screenshot run may perform. Each one opens or reveals read-only content (a Why
 * disclosure, a record's details); none saves, submits, approves, signs, grants or changes state.
 * Names are exact per locale (no pattern, no case folding); adding an entry is a reviewed code change.
 */
export const allowedClicks: ReadonlyArray<Readonly<{ kind: 'button' | 'disclosure'; name: Localized }>> = Object.freeze([
  { kind: 'button', name: { tr: 'Neden?', en: 'Why?' } },
  { kind: 'disclosure', name: { tr: 'Neden / sonraki adım', en: 'Why / next action' } },
  { kind: 'button', name: { tr: 'Synthetic Operator A', en: 'Synthetic Operator A' } },
  { kind: 'button', name: { tr: 'Hesap add41584', en: 'Account add41584' } },
  { kind: 'button', name: { tr: 'Synthetic Ship A', en: 'Synthetic Ship A' } },
])
export const clickKind = (target: ClickTarget) => 'disclosure' in target ? 'disclosure' as const : 'button' as const
export const clickName = (target: ClickTarget): Localized => 'disclosure' in target ? target.disclosure : target.name
/**
 * A step the lab-manager screenshot account cannot honestly show (for example it needs a write grant
 * the read-only screenshot enrollment deliberately lacks). It is listed explicitly instead of staging
 * a permission or state; the help panel shows the text step without an image.
 */
export const unavailableReasons = Object.freeze(['write_permission_required'] as const)
export type StepSpec = Readonly<{ step: number; actions: readonly Action[]; targets: readonly Target[]; unavailable?: (typeof unavailableReasons)[number] }>
export type GuideSpec = Readonly<{ route: string; steps: readonly StepSpec[] }>
export type ScreenshotMapping = Readonly<{
  schemaVersion: 'poseidon.help.screenshots.v1'
  /** Viewports actually committed to the repository (D4 lists both; see the UG-2 report for the size decision). */
  committedViewports: readonly ViewportName[]
  guides: Readonly<Record<string, GuideSpec>>
}>

const roles = new Set(['button', 'link', 'heading', 'textbox', 'searchbox', 'combobox', 'table', 'row', 'article', 'region',
  'list', 'listitem', 'group', 'navigation', 'tab', 'tablist', 'status', 'note', 'form', 'checkbox', 'radio', 'cell', 'columnheader'])

function fail(reason: string): never { throw new Error('screenshot_mapping_invalid:' + reason) }
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)

function localized(value: unknown, field: string): Localized {
  if (!object(value) || Object.keys(value).sort().join() !== 'en,tr') fail(field)
  for (const locale of screenshotLocales) {
    const text = value[locale]
    if (typeof text !== 'string' || !text || text.length > 160) fail(field)
    if (text.startsWith('re:')) { try { new RegExp(text.slice(3)) } catch { fail(field + ':regexp') } }
  }
  return value as Localized
}

export function validateTarget(value: unknown, depth = 0): Target {
  if (!object(value) || depth > 2) fail('target')
  const allowed = new Set(['role', 'name', 'label', 'text', 'nth', 'within'])
  if (Object.keys(value).some(key => !allowed.has(key))) fail('target_field')
  const kinds = ['role', 'label', 'text'].filter(key => key in value)
  if (kinds.length !== 1) fail('target_kind')
  if ('role' in value && (typeof value.role !== 'string' || !roles.has(value.role))) fail('target_role')
  if ('name' in value) { if (!('role' in value)) fail('target_name_without_role'); localized(value.name, 'name') }
  if ('label' in value) localized(value.label, 'label')
  if ('text' in value) localized(value.text, 'text')
  if ('nth' in value && (!Number.isSafeInteger(value.nth) || (value.nth as number) < 0 || (value.nth as number) > 50)) fail('target_nth')
  if ('within' in value) validateTarget(value.within, depth + 1)
  return value as Target
}

/** Accepts a click only when it is exactly one allow-listed (kind, TR name, EN name) entry. */
export function validateClick(value: unknown): ClickTarget {
  if (!object(value)) fail('click')
  const keys = Object.keys(value).filter(key => key !== 'within').sort().join()
  if (keys !== 'name,role' && keys !== 'disclosure') fail('click_shape')
  if (keys === 'name,role' && value.role !== 'button') fail('click_role')
  const name = localized(keys === 'disclosure' ? value.disclosure : value.name, 'click_name')
  if (Object.values(name).some(text => text.startsWith('re:'))) fail('click_pattern')
  if ('within' in value) validateTarget(value.within, 1)
  const kind = keys === 'disclosure' ? 'disclosure' : 'button'
  if (!allowedClicks.some(entry => entry.kind === kind && entry.name.tr === name.tr && entry.name.en === name.en))
    fail('click_not_allowed:' + name.en)
  return value as ClickTarget
}

function validateAction(value: unknown): Action {
  if (!object(value) || Object.keys(value).join() !== 'click') fail('action_kind')
  validateClick(value.click)
  return value as Action
}

export function validateMapping(value: unknown): ScreenshotMapping {
  if (!object(value) || value.schemaVersion !== 'poseidon.help.screenshots.v1' || !object(value.guides)) fail('root')
  const committed = value.committedViewports
  if (!Array.isArray(committed) || !committed.length || committed.some(item => !(item in viewports)) || new Set(committed).size !== committed.length) fail('viewports')
  for (const [key, guide] of Object.entries(value.guides)) {
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(key) || !object(guide)) fail('guide:' + key)
    if (typeof guide.route !== 'string' || !/^\/app(?:\/[a-z0-9-]+)+(?:\?vessel=[0-9a-f-]{36})?$/.test(guide.route)) fail('route:' + key)
    if (!Array.isArray(guide.steps) || !guide.steps.length) fail('steps:' + key)
    guide.steps.forEach((step: unknown, index: number) => {
      if (!object(step) || step.step !== index + 1 || !Array.isArray(step.actions) || !Array.isArray(step.targets)) fail(`step:${key}:${index + 1}`)
      if (Object.keys(step).some(field => !['step', 'actions', 'targets', 'unavailable'].includes(field))) fail(`step_field:${key}:${index + 1}`)
      if ('unavailable' in step) {
        if (!unavailableReasons.includes(step.unavailable as never) || step.actions.length || step.targets.length) fail(`unavailable:${key}:${index + 1}`)
        return
      }
      if (!step.targets.length) fail(`targets:${key}:${index + 1}`)
      step.actions.forEach(validateAction)
      step.targets.forEach(item => validateTarget(item))
    })
  }
  return value as unknown as ScreenshotMapping
}

/** Committed output path relative to frontend/src/help/screenshots. */
export const shotName = (locale: ScreenshotLocale, key: string, step: number, viewport: ViewportName) => `${locale}/${key}/${step}-${viewport}`
