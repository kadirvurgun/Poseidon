import { test } from '@playwright/test'
import { registerCases } from './cases.ts'
import { runSignedCase } from '../lab/signed-case.ts'
import { login } from '../lab/journeys.ts'

for (const entry of registerCases) {
  test.describe(entry.name, () => {
    test(entry.name, async ({ page }) => runSignedCase(page, entry, login))
  })
}
