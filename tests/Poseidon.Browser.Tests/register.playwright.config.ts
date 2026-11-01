import { defineConfig, devices } from '@playwright/test'

// Authored only. Coordinator supplies existing real sessions; this task provisions no accounts.
const baseURL = process.env.POSEIDON_REGISTER_URL
if (!baseURL || new URL(baseURL).protocol !== 'https:') throw new Error('A real HTTPS register host is required.')
if (!process.env.POSEIDON_REGISTER_WRITER_STATE || !process.env.POSEIDON_REGISTER_RESTRICTED_STATE) throw new Error('Existing private writer and restricted session states are required.')
export default defineConfig({
  testDir: './register-specs', fullyParallel: false, workers: 1, retries: 0, forbidOnly: true,
  timeout: 45000, reporter: 'list',
  use: { baseURL, storageState: process.env.POSEIDON_REGISTER_WRITER_STATE, trace: 'off', screenshot: 'off', video: 'off' },
  // Avoid automatic DOM error context: authenticated DOM/storage may contain private context.
  captureGitInfo: { commit: false, diff: false },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile', use: { ...devices['Desktop Chrome'], viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true } },
  ],
})
