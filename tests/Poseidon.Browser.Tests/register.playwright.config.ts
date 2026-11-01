import { defineConfig, devices } from '@playwright/test'

// Authenticated execution uses lab/register-run.ts: the pinned test runner writes
// error-context DOM/error files even with trace/screenshot/video disabled.
if (process.env.POSEIDON_REGISTER_DIRECT !== '1') throw new Error('Use the private oidc_lab.py register_browser driver.')
const baseURL = process.env.POSEIDON_REGISTER_URL
if (!baseURL || new URL(baseURL).protocol !== 'https:') throw new Error('A real HTTPS register host is required.')
if (!process.env.POSEIDON_REGISTER_WRITER_STATE || !process.env.POSEIDON_REGISTER_RESTRICTED_STATE) throw new Error('Existing private writer and restricted session states are required.')
export default defineConfig({
  testDir: './register-specs', fullyParallel: false, workers: 1, retries: 0, forbidOnly: true,
  timeout: 45000, reporter: 'list',
  use: { baseURL, storageState: process.env.POSEIDON_REGISTER_WRITER_STATE, trace: 'off', screenshot: 'off', video: 'off' },
  // These options alone do not disable automatic error context in the test runner.
  captureGitInfo: { commit: false, diff: false },
  projects: [
    { name: 'firefox-desktop', use: { ...devices['Desktop Firefox'] } },
    // Firefox has no isMobile emulation: actual narrow responsive layout, no touch claim.
    { name: 'firefox-narrow-responsive', use: { ...devices['Desktop Firefox'], viewport: { width: 390, height: 844 } } },
  ],
})
