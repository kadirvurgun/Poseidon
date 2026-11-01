import { chromium } from '@playwright/test';
import { writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { managerJourney, restrictedJourney } from './journeys.ts';

// Direct Playwright library runner intentionally avoids test-runner automatic
// error-context DOM snapshots, which can include filled credentials/redirects
// even when trace, video and screenshot are off. No raw errors leave memory.
const counts = { status: 'running', passed: 0, failed: 0, failureLines: [] as number[] };
const persist = () => writeFileSync(resolve(process.env.POSEIDON_LAB_PRIVATE!, 'browser-counts.json'), JSON.stringify(counts));
persist();
let browser;
try {
  browser = await chromium.launch();
  for (const journey of [managerJourney, restrictedJourney]) {
    const context = await browser.newContext({ baseURL: 'https://localhost:5443', ignoreHTTPSErrors: true });
    const page = await context.newPage();
    page.setDefaultTimeout(15_000);
    page.setDefaultNavigationTimeout(30_000);
    try {
      await journey(page);
      counts.passed++;
    } catch (error) {
      counts.failed++;
      const line = error instanceof Error ? error.stack?.match(/journeys\.ts:(\d+):/) : null;
      if (line) counts.failureLines.push(Number(line[1]));
    } finally {
      await context.close();
      persist();
    }
  }
} catch {
  counts.failed++;
} finally {
  await browser?.close();
  counts.status = counts.failed === 0 && counts.passed === 2 ? 'passed' : 'failed';
  persist();
}
process.exitCode = counts.status === 'passed' ? 0 : 1;
