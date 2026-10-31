import { expect, test } from '@playwright/test';

// OPS-DASH-UI-01: browser UI contract with synthetic ops metadata.
// Existing connectivity specs continue to exercise the real .NET API.
test('shows working account separately from unknown quota, in both languages', async ({ page }) => {
  const stamp = new Date().toISOString();
  const reset = Math.floor(Date.now() / 1000) + 3600;
  await page.route('**/ops/status', route => route.fulfill({ json: {
    schemaVersion: 1, generatedAt: stamp,
    quota: { timestamp: stamp, next_check: new Date(Date.now() + 3600000).toISOString(), selected: 'kadir-plus', profiles: {
      'esra-pro': { state: 'busy', ready: false, lastKnown: { windows: [{ usedPercent: 18, windowDurationMins: 10080, resetsAt: reset }], observedAt: stamp } },
      'kadir-plus': { state: 'known', ready: true, windows: [{ usedPercent: 40, windowDurationMins: 300, resetsAt: reset }] },
    } },
    coordinator: { enabled: true, status: 'running', modelCalls: 1, maxCalls: 8, maxJobs: 4, deadline: reset, jobs: [] },
    activities: [{ id: 'runtime', profile: 'esra-pro', taskId: 'OPS-RUNTIME', role: 'development', status: 'running', startedAt: stamp, model: 'gpt-6.1-sol', effort: 'medium' }],
    modelPolicy: { automaticEscalation: false, roles: { routine: { model: 'gpt-6-luna', effort: 'medium' } } },
    events: [], alarms: [],
    project: { completionPercent: 0.82, acceptedStoryPoints: 8, totalStoryPoints: 970, acceptedTasks: 2, totalTasks: 121, stage: 'S0' },
  } }));
  await page.goto('/agents');
  const esra = page.getByRole('region', { name: 'Esra Pro' });
  await expect(esra.getByText('Çalışıyor', { exact: true })).toBeVisible();
  await expect(esra.getByText('Görev sürüyor · kota ölçümü görev bitince yenilenecek')).toBeVisible();
  await expect(esra.getByText('82% kalan')).toBeVisible();
  await expect(esra.getByText(/gpt-6.1-sol/)).toBeVisible();
  await expect(esra.getByText('Yeni göreve uygun')).toHaveCount(0);
  await expect(page.getByText('gpt-6-luna', { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.getByRole('button', { name: 'EN', exact: true }).click();
  await expect(esra.getByText('Working', { exact: true })).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('lang', 'en');
});

test('failed ops service displays retry without claiming account readiness', async ({ page }) => {
  await page.route('**/ops/status', route => route.fulfill({ status: 503, body: '{}' }));
  await page.goto('/agents');
  await expect(page.getByRole('status')).toContainText('Servise ulaşılamadı');
  await expect(page.getByRole('button', { name: 'Tekrar dene' })).toBeVisible();
  await expect(page.getByText('Yeni göreve uygun')).toHaveCount(0);
});

test('quota without reset timestamp stays visible without an invented timer', async ({ page }) => {
  const stamp = new Date().toISOString();
  const profile = { state: 'known', ready: true, windows: [{ usedPercent: 20, windowDurationMins: 10080 }] };
  await page.route('**/ops/status', route => route.fulfill({ json: {
    schemaVersion: 1, generatedAt: stamp,
    quota: { timestamp: stamp, next_check: null, selected: 'esra-pro', profiles: { 'esra-pro': profile, 'kadir-plus': profile } },
    coordinator: { enabled: false, status: 'monitoring', modelCalls: 0, maxCalls: 8, maxJobs: 4, deadline: null, jobs: [] },
    activities: [], events: [], alarms: [],
    project: { completionPercent: 0.82, acceptedStoryPoints: 8, totalStoryPoints: 970, acceptedTasks: 2, totalTasks: 121, stage: 'S0' },
  } }));
  await page.goto('/agents');
  const esra = page.getByRole('region', { name: 'Esra Pro' });
  await expect(esra.getByText('80% kalan')).toBeVisible();
  await expect(esra.getByText('Sıfırlanma zamanı bilinmiyor')).toBeVisible();
  await expect(esra.getByText(/Sıfırlanma:/)).toHaveCount(0);
  await expect(page.getByRole('status')).toContainText('Canlı servis bağlantısı');
});
