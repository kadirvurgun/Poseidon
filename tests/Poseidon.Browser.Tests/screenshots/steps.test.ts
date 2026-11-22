import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { allowedClicks, shotName, unavailableReasons, validateClick, validateMapping, validateTarget } from './steps.ts'

// USER-GUIDE-01 UG-2: closed step-mapping schema; a screenshot run can only perform allow-listed clicks.
const committed = JSON.parse(readFileSync(resolve(import.meta.dirname, '../../../frontend/src/help/screenshots/steps.json'), 'utf8'))
const clone = () => structuredClone(committed)
const button = (tr: string, en: string = tr) => ({ role: 'button', name: { tr, en } })
const withClick = (click: unknown) => { const mapping = clone(); mapping.guides.today.steps[0].actions = [{ click }]; return mapping }

test('the committed mapping is valid and only marks steps unavailable for an explicit reason', () => {
  const mapping = validateMapping(committed)
  assert.deepEqual(Object.keys(mapping.guides).sort(), ['audit', 'certificate-command-center', 'companies', 'departure-signoff', 'fleet',
    'noon-report', 'safety-events', 'today', 'tug-command', 'tug-daily-log', 'tug-job-board', 'user-admin', 'work-order-center'])
  assert.deepEqual([...mapping.committedViewports].sort(), ['desktop', 'mobile'])
  for (const guide of Object.values(mapping.guides))
    for (const step of guide.steps) if (step.unavailable) assert.ok(unavailableReasons.includes(step.unavailable), step.unavailable)
  // Steps that need a write, triage, review or decision grant stay explicitly unavailable (never staged).
  const reason = (key: string, step: number) => mapping.guides[key].steps[step - 1].unavailable
  assert.equal(reason('departure-signoff', 8), 'write_permission_required')
  for (const step of [2, 3, 4, 5, 6, 7, 8, 9]) assert.equal(reason('safety-events', step), 'write_permission_required')
  assert.equal(reason('audit', 4), 'record_selection_not_allowlisted')
})

test('a step route is an app path like the guide route and is not allowed on an unavailable step', () => {
  const ok = clone(); ok.guides.today.steps[0].route = '/app/today'
  validateMapping(ok)
  for (const route of ['https://example.invalid/app/today', '/app/today?id=1', '/app/today#x', '/api/v1/workspace', '/app/Today']) {
    const bad = clone(); bad.guides.today.steps[0].route = route
    assert.throws(() => validateMapping(bad), /route:today:1/, route)
  }
  const hidden = clone(); hidden.guides['tug-job-board'].steps[5].route = '/app/tug-job-board'
  assert.throws(() => validateMapping(hidden), /unavailable/)
  const reason = clone(); reason.guides['tug-job-board'].steps[5].unavailable = 'not_needed'
  assert.throws(() => validateMapping(reason), /unavailable/)
})

test('rejects CSS selectors, unknown fields, missing locales and non-sequential steps', () => {
  assert.throws(() => validateTarget({ css: '.button' }))
  assert.throws(() => validateTarget({ role: 'button', name: { en: 'Refresh' } }))
  assert.throws(() => validateTarget({ role: 'marquee' }))
  assert.throws(() => validateTarget({ role: 'button', label: { tr: 'a', en: 'b' } }))
  assert.throws(() => validateTarget({ text: { tr: 're:(', en: 'x' } }))
  const gap = clone(); gap.guides.today.steps[1].step = 3
  assert.throws(() => validateMapping(gap))
  const route = clone(); route.guides.today.route = 'https://example.invalid/app/today'
  assert.throws(() => validateMapping(route))
  const hidden = clone(); hidden.guides.today.steps[0].unavailable = 'write_permission_required'
  assert.throws(() => validateMapping(hidden), /unavailable/)
  const viewport = clone(); viewport.committedViewports = ['tablet']
  assert.throws(() => validateMapping(viewport))
})

test('every allow-listed click is accepted with its exact names, optionally scoped (QA N1)', () => {
  for (const entry of allowedClicks) {
    const click = entry.kind === 'button' ? { role: 'button', name: entry.name } : { disclosure: entry.name }
    validateClick(click)
    validateClick({ ...click, within: { role: 'article', nth: 0 } })
    validateMapping(withClick(click))
  }
})

test('clicks outside the allow-list are rejected, including destructive words in both languages (QA N1)', () => {
  const words = ['Save', 'Kaydet', 'Create membership', 'Üyelik oluştur', 'Suspend', 'Askıya al', 'Revoke membership', 'Approve', 'Sign out',
    'Submit', 'Grant', 'Yetki ver', 'Confirm', 'Onayla', 'Assign', 'Ata', 'Remove', 'Kaldır', 'Reject', 'Reddet', 'Add', 'Ekle', 'Add member',
    'İmzala', 'İptal', 'İçe aktar', 'Çıkış', 'Çıkış yap', 'Delete', 'Sil', 'Record', 'Kaydet', 'Record Master decision', 'Release', 'Hold', 'Deny',
    'Record triage', 'Report an event', 'Save draft', 'New draft', 'Release work order', 'Record verification', 'Filter', 'Refresh']
  for (const word of words) {
    assert.throws(() => validateClick(button(word)), /click_not_allowed/, word)
    assert.throws(() => validateMapping(withClick(button(word))), /click_not_allowed/, word)
  }
})

test('casing, Turkish dotted/dotless I and partial names never match an allowed click (QA N1)', () => {
  const variants = [button('NEDEN?', 'WHY?'), button('neden?', 'why?'), button('Neden', 'Why'), button('Neden? ', 'Why? '),
    button('HESAP ADD41584', 'ACCOUNT ADD41584'), button('Hesap add4158', 'Account add4158'), button('hesap add41584', 'account add41584'),
    button('SYNTHETİC OPERATOR A', 'SYNTHETIC OPERATOR A'), button('Synthetıc Operator A', 'Synthetic Operator A'),
    { disclosure: { tr: 'NEDEN / SONRAKİ ADIM', en: 'WHY / NEXT ACTION' } }, { disclosure: { tr: 'neden / sonraki adim', en: 'Why / next action' } },
    // A correct name in one locale does not rescue a wrong name in the other.
    button('Neden?', 'Save'), button('Kaydet', 'Why?')]
  for (const click of variants) assert.throws(() => validateClick(click), /click_not_allowed/, JSON.stringify(click))
})

test('unnamed, nth-only, pattern, wrong-role and other action kinds are refused (QA N1)', () => {
  assert.throws(() => validateClick({ role: 'button' }), /click_shape/)
  assert.throws(() => validateClick({ role: 'button', nth: 0 }), /click_shape/)
  assert.throws(() => validateClick({ role: 'button', name: { tr: 'Neden?', en: 'Why?' }, nth: 0 }), /click_shape/)
  assert.throws(() => validateClick({ role: 'button', name: { tr: 're:.*', en: 're:.*' } }), /click_pattern/)
  assert.throws(() => validateClick({ role: 'button', name: { tr: 're:^Neden', en: 're:^Why' } }), /click_pattern/)
  assert.throws(() => validateClick({ role: 'link', name: { tr: 'Neden?', en: 'Why?' } }), /click_role/)
  assert.throws(() => validateClick({ text: { tr: 'Neden?', en: 'Why?' } }), /click_shape/)
  assert.throws(() => validateClick({ label: { tr: 'Neden?', en: 'Why?' } }), /click_shape/)
  assert.throws(() => validateClick({ disclosure: { tr: 'Neden?', en: 'Why?' } }), /click_not_allowed/)
  assert.throws(() => validateClick({ role: 'button', name: { tr: 'Neden?', en: 'Why?' }, within: { css: 'main' } }), /target_field/)
  for (const action of [{ fill: { role: 'searchbox' }, value: 'x' }, { press: { role: 'searchbox' }, key: 'Enter' }, { click: button('Neden?', 'Why?'), extra: 1 }]) {
    const mapping = clone(); mapping.guides.today.steps[0].actions = [action]
    assert.throws(() => validateMapping(mapping), /action_kind/)
  }
})

test('output names are locale/key/step-viewport', () => {
  assert.equal(shotName('tr', 'today', 4, 'desktop'), 'tr/today/4-desktop')
})
