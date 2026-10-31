import { defineConfig, devices } from '@playwright/test';
import { fileURLToPath } from 'node:url';

function port(name: string, fallback: number): number {
  const value = process.env[name];
  if (value === undefined) return fallback;
  if (!/^[0-9]+$/.test(value) || Number(value) < 1024 || Number(value) > 65535) {
    throw new Error(`Invalid ${name}`);
  }
  return Number(value);
}
const apiPort = port('POSEIDON_API_PORT', 5080);
const uiPort = port('POSEIDON_UI_PORT', 4173);
if (apiPort === uiPort) throw new Error('API and UI ports must differ');
const apiUrl = `http://127.0.0.1:${apiPort}`;
const uiUrl = `http://127.0.0.1:${uiPort}`;

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
    baseURL: uiUrl,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile-chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true } },
  ],
  webServer: [
    {
      command: `dotnet run --project backend/Poseidon.Api --configuration Release --no-build --no-launch-profile --urls ${apiUrl}`,
      cwd: root,
      url: `${apiUrl}/health/live`,
      env: { ASPNETCORE_ENVIRONMENT: 'Development' },
      reuseExistingServer: false,
      timeout: 60_000,
    },
    {
      command: `npm run preview -- --host 127.0.0.1 --port ${uiPort} --strictPort`,
      cwd: fileURLToPath(new URL('../../frontend/', import.meta.url)),
      url: uiUrl,
      reuseExistingServer: false,
      timeout: 60_000,
    },
  ],
});
