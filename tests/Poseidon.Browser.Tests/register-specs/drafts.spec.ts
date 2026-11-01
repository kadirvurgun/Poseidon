import { test } from '@playwright/test'
import { registerCases } from './cases.ts'

for (const entry of registerCases) {
  test.describe(entry.name, () => {
    if (entry.restricted) test.use({ storageState: process.env.POSEIDON_REGISTER_RESTRICTED_STATE })
    test(entry.name, async ({ page }) => entry.run(page))
  })
}
