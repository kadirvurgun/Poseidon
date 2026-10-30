// Uses an existing private Development server and existing synthetic records.
// No draft writes. See docs/RESPONSIVE_QA.md for runtime and environment options.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
(async () => {
  const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || '/usr/bin/chromium', args: ['--no-sandbox'] });
  const results = [];
  try {
    for (const [width, height] of [[390, 844], [768, 1024], [1440, 900]]) {
      for (const lang of ['tr', 'en']) {
        const page = await browser.newPage({ viewport: { width, height } });
        page.on('dialog', dialog => dialog.accept());
        let writes = 0;
        page.on('request', request => {
          if (request.method() === 'POST' && /\/api\/v1\/(vessels|certificates)/.test(request.url())) writes++;
        });
        const t = (tr, en) => lang === 'tr' ? tr : en;
        const button = name => page.getByRole('button', { name, exact: true });
        const tabTo = async locator => {
          for (let i = 0; i < 80; i++) {
            await page.keyboard.press('Tab');
            if (await locator.evaluate(e => e === document.activeElement)) return;
          }
          throw new Error('Keyboard target not reached');
        };
        const noOverflow = async () => assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
        const focusBounds = async () => page.locator('.table-wrap').evaluate(w => {
          const e = document.activeElement, r = e.getBoundingClientRect(), c = w.getBoundingClientRect();
          const style = getComputedStyle(e);
          return { left: r.left, right: r.right, clipLeft: c.left, clipRight: c.right, scroll: w.scrollLeft, visible: r.left - 5 >= c.left && r.right + 5 <= c.right, focusVisible: e.matches(':focus-visible'), outline: style.outlineWidth, shadow: style.boxShadow };
        });
        await page.goto(process.env.POSEIDON_WEB_URL || 'http://127.0.0.1:5173');
        await page.keyboard.press('Tab');
        assert.equal(await page.locator(':focus').textContent(), 'İçeriğe geç');
        await page.keyboard.press('Enter');
        assert.equal(await page.locator(':focus').getAttribute('id'), 'main');
        await noOverflow();
        if (lang === 'en') await button('Switch to English').click();
        await button(t('Sentetik oturum aç', 'Open synthetic session')).click();
        await button(t('Aç', 'Open')).first().waitFor();
        await tabTo(button(t('Aç', 'Open')).first());
        const vesselFocus = await focusBounds();
        assert(vesselFocus.visible && vesselFocus.focusVisible, JSON.stringify(vesselFocus));
        await noOverflow();
        await page.keyboard.press('Enter');
        await page.locator('#name').waitFor();
        const existing = await page.locator('#name').inputValue();
        await button(t('Switch to English', 'Türkçeye geç')).click();
        assert.equal(await page.locator('#name').inputValue(), existing);
        await button(t('Türkçeye geç', 'Switch to English')).click();
        await button(t('Listeye dön', 'Back to list')).click();
        await tabTo(button(t('Yeni taslak', 'New draft')));
        await page.keyboard.press('Enter');
        await page.locator('#name').fill('Responsive synthetic unsaved');
        await page.locator('#name').focus();
        await page.keyboard.press('Tab');
        assert.equal(await page.locator(':focus').getAttribute('id'), 'imo_no');
        await page.keyboard.type('123');
        await tabTo(button(t('Taslağı kaydet', 'Save draft')));
        await page.keyboard.press('Enter');
        assert.equal(await page.locator(':focus').getAttribute('id'), 'imo_no');
        assert.equal(await page.locator('#imo_no').getAttribute('aria-describedby'), 'imo_no-error');
        assert.equal(await page.locator('#imo_no-error').textContent(), t('IMO tam 7 rakam olmalıdır.', 'IMO must contain exactly 7 digits.'));
        await button(t('Switch to English', 'Türkçeye geç')).click();
        assert.equal(await page.locator('#imo_no-error').textContent(), t('IMO must contain exactly 7 digits.', 'IMO tam 7 rakam olmalıdır.'));
        assert.equal(await page.locator('#name').inputValue(), 'Responsive synthetic unsaved');
        assert.equal(await page.locator('#imo_no').inputValue(), '123');
        await button(t('Türkçeye geç', 'Switch to English')).click();
        await noOverflow();
        await button(t('Sertifika taslakları', 'Certificate drafts')).click();
        await button(t('Aç', 'Open')).first().waitFor();
        await tabTo(button(t('Aç', 'Open')).first());
        const certificateFocus = await focusBounds();
        assert(certificateFocus.visible && certificateFocus.focusVisible, JSON.stringify(certificateFocus));
        await noOverflow();
        await page.keyboard.press('Enter');
        await page.locator('#issue').waitFor();
        await button(t('Listeye dön', 'Back to list')).click();
        await button(t('Yeni taslak', 'New draft')).click();
        await page.locator('#issue').fill('2026-10-27');
        await page.locator('#expiry').fill('2026-10-01');
        await tabTo(button(t('Taslağı kaydet', 'Save draft')));
        await page.keyboard.press('Enter');
        assert.equal(await page.locator(':focus').getAttribute('id'), 'vessel_id');
        assert.equal(await page.locator('#vessel_id').getAttribute('aria-describedby'), 'vessel_id-error');
        await button(t('Switch to English', 'Türkçeye geç')).click();
        assert.equal(await page.locator('#expiry-error').textContent(), t('Expiry cannot precede issue date.', 'Bitiş tarihi düzenlenme tarihinden önce olamaz.'));
        assert.equal(await page.locator('#issue').inputValue(), '2026-10-27');
        await button(t('Türkçeye geç', 'Switch to English')).click();
        for (const id of ['issue', 'expiry']) assert.equal(await page.locator('#' + id).evaluate(e => getComputedStyle(e).colorScheme), 'dark');
        await noOverflow();
        if (process.env.QA_SCREENSHOT_DIR) {
          await page.locator('#issue').scrollIntoViewIfNeeded();
          await page.screenshot({ path: `${process.env.QA_SCREENSHOT_DIR}/${width}-${lang}.png` });
        }
        assert.equal(writes, 0);
        results.push({ width, height, lang, result: 'PASS', vesselFocus, certificateFocus, draftWrites: writes });
        await page.close();
      }
    }
    const report = JSON.stringify({ chromium: browser.version(), results }, null, 2);
    if (process.env.QA_RESULT_FILE) fs.writeFileSync(process.env.QA_RESULT_FILE, report);
    console.log(report);
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
