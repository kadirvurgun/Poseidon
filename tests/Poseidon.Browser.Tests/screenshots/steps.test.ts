import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { assertNonMutating, shotName, validateMapping, validateTarget } from './steps.ts'

// USER-GUIDE-01 UG-2: closed step-mapping schema; a screenshot run can never click a mutating control.
const committed = JSON.parse(readFileSync(resolve(import.meta.dirname, '../../../frontend/src/help/screenshots/steps.json'), 'utf8'))
const clone = () => structuredClone(committed)

test('the committed mapping is valid and only marks steps unavailable for an explicit reason', () => {
  const mapping = validateMapping(committed)
  assert.deepEqual(Object.keys(mapping.guides).sort(), ['companies', 'today', 'tug-job-board', 'user-admin'])
  for (const guide of Object.values(mapping.guides))
    for (const step of guide.steps) if (step.unavailable) assert.equal(step.unavailable, 'write_permission_required')
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

test('a preparation click on a saving, approving, signing or lifecycle control is rejected (D5)', () => {
  for (const name of ['Save', 'Kaydet', 'Create membership', 'Üyelik oluştur', 'Suspend', 'Askıya al', 'Revoke membership', 'Approve', 'Sign out', 'Submit'])
    assert.throws(() => assertNonMutating({ role: 'button', name: { tr: name, en: name } }), /mutating_action/)
  for (const name of ['Why?', 'Neden?', 'Synthetic Operator A', 'Open job', 'Apply filters', 'Edit grant'])
    assertNonMutating({ role: 'button', name: { tr: name, en: name } })
  const saving = clone(); saving.guides.today.steps[0].actions = [{ click: { role: 'button', name: { tr: 'Kaydet', en: 'Save' } } }]
  assert.throws(() => validateMapping(saving), /mutating_action/)
  const email = clone(); email.guides.today.steps[0].actions = [{ fill: { role: 'searchbox' }, value: 'someone@example.invalid' }]
  assert.throws(() => validateMapping(email), /fill_value/)
})

test('output names are locale/key/step-viewport', () => {
  assert.equal(shotName('tr', 'today', 4, 'desktop'), 'tr/today/4-desktop')
})
