import { defineConfig, devices } from '@playwright/test';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));

export default defineConfig({
  testDir: './specs',
  outputDir: fileURLToPath(new URL('./test-results/', import.meta.url)),
  fullyParallel: true,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  timeout: 30_000,
  reporter: [['list'], ['html', { open: 'never', outputFolder: fileURLToPath(new URL('./playwright-report/', import.meta.url)) }]],
  use: {
    baseURL: 'http://127.0.0.1:4173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile-chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true } },
  ],
  webServer: [
    {
      command: 'dotnet run --project backend/Poseidon.Api --configuration Release --no-build --no-launch-profile --urls http://127.0.0.1:5080',
      cwd: root,
      url: 'http://127.0.0.1:5080/health/live',
      env: { ASPNETCORE_ENVIRONMENT: 'Development' },
      reuseExistingServer: false,
      timeout: 60_000,
    },
    {
      command: 'npm run preview -- --host 127.0.0.1 --port 4173 --strictPort',
      cwd: fileURLToPath(new URL('../../frontend/', import.meta.url)),
      url: 'http://127.0.0.1:4173',
      reuseExistingServer: false,
      timeout: 60_000,
    },
  ],
});
