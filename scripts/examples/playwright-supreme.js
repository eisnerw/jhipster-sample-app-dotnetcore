// Playwright example: login, open Supreme, filter by quoted name, expand detail
// Emits PNG and JSON artifacts in the current working directory.
// Config via env: BASE_URL, USERNAME, PASSWORD, QUERY

const { chromium } = require('playwright');

async function main() {
  const BASE_URL = process.env.BASE_URL || 'http://localhost:5000';
  const USERNAME = process.env.USERNAME || 'admin';
  const PASSWORD = process.env.PASSWORD || 'admin';
  const QUERY = process.env.QUERY || '"Mark R. Freeman"';

  const browser = await chromium.launch({ args: ['--no-sandbox'] });
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await ctx.newPage();

  const shot = (name) => page.screenshot({ path: name, fullPage: true }).catch(() => {});

  async function login() {
    await page.goto(BASE_URL, { waitUntil: 'domcontentloaded' });
    try { await page.waitForLoadState('networkidle', { timeout: 8000 }); } catch {}
    if (await page.getByText(/Sign out/i).count()) { await shot('home.png'); return; }
    // direct login route if menu not present
    if (!(await page.locator('input[name="username"],#username').count())) {
      await page.goto(BASE_URL + '/login').catch(() => {});
    }
    await shot('home.png');
    await page.fill('input[name="username"],#username', USERNAME);
    await page.fill('input[name="password"],#password', PASSWORD);
    await shot('login.png');
    await page.click('button[type="submit"], button:has-text("Sign in")');
    try { await page.waitForSelector('text=Sign out', { timeout: 10000 }); } catch {}
    await shot('after-login.png');
  }

  async function openSupreme() {
    const entities = page.getByRole('link', { name: /Entities/i }).first();
    if (await entities.count()) await entities.click().catch(() => {});
    const sup = page.getByRole('link', { name: /Supreme/i }).first();
    if (await sup.count()) await sup.click().catch(() => {});
    if (!/\/supreme/i.test(page.url())) await page.goto(BASE_URL + '/supreme').catch(() => {});
    await page.waitForTimeout(500);
    await shot('supreme.png');
  }

  async function selectAdvocatesIfPresent() {
    for (const sel of await page.$$('select')) {
      const labels = await sel.$$eval('option', os => os.map(o => o.textContent.trim()));
      const label = labels.find(l => /Advocates/i.test(l));
      if (label) { await sel.selectOption({ label }); break; }
    }
  }

  async function filterByQuery() {
    // Try to find the Category input first
    let input = null;
    const label = await page.getByText(/^Category$/i).first();
    if (await label.count()) {
      const lh = await label.elementHandle();
      const container = await lh.evaluateHandle(el => el.closest('div,section,form') || el.parentElement);
      input = await container.asElement().$('input,textarea,[contenteditable=true]');
    }
    if (!input) input = await page.locator('input[name=category], input[placeholder*="category" i]').first().elementHandle().catch(() => null);
    if (!input) input = await page.getByRole('textbox').first().elementHandle().catch(() => null);
    if (!input) throw new Error('category-input-not-found');

    await input.focus();
    await page.keyboard.down('Control').catch(() => {});
    await page.keyboard.press('KeyA').catch(() => {});
    await page.keyboard.up('Control').catch(() => {});
    await page.keyboard.press('Backspace').catch(() => {});
    await page.keyboard.type(QUERY, { delay: 8 });
    await shot('typed.png');

    // Apply via check icon near the input if present; else Enter
    const container = await input.evaluateHandle(el => el.closest('div,section,form') || el.parentElement);
    let applied = false;
    for (const sel of ['i.pi-check','i.fa-check','button:has(i.pi-check)','button:has(i.fa-check)','[title*="Apply" i]']) {
      const el = await container.asElement().$(sel);
      if (el) { await el.click({ force: true }).catch(() => {}); applied = true; break; }
    }
    if (!applied) await page.keyboard.press('Enter').catch(() => {});

    // Wait for a single row if possible
    let rows = 0; let tries = 25;
    while (tries--) {
      rows = await page.locator('tbody tr').count().catch(() => 0);
      if (rows === 1) break;
      await page.waitForTimeout(200);
    }
    await shot('filtered.png');
  }

  async function expandDetailAndExtract() {
    const row = page.locator('tbody tr').first();
    if ((await page.locator('tbody tr').count()) < 1) return { ok: false };
    const toggler = row.locator('.p-row-toggler, td:last-child button, button:has(i.pi-chevron-right), button:has(i.pi-plus), button:has(i.fa-chevron-right), button:has(i.fa-plus)').first();
    if (await toggler.count()) await toggler.click({ force: true }).catch(() => {});
    else await row.locator('td').last().click({ force: true }).catch(() => {});
    await page.waitForTimeout(1000);

    const next = row.locator('xpath=following-sibling::tr[1]').first();
    let iframeHandle = null;
    if (await next.count()) iframeHandle = await next.evaluateHandle(tr => tr.querySelector('iframe'));
    if (!iframeHandle) iframeHandle = await page.$('iframe');
    if (!iframeHandle) { await shot('detail.png'); return { ok: false } };
    const frame = await iframeHandle.contentFrame();
    if (!frame) { await shot('detail.png'); return { ok: false } };

    try { await frame.waitForSelector('text=Mark R. Freeman', { timeout: 8000 }); } catch {}
    const info = await frame.evaluate(() => {
      const body = document.body ? (document.body.innerText || '') : '';
      const name = 'Mark R. Freeman';
      const idx = body.indexOf(name);
      const snippet = idx >= 0 ? body.slice(Math.max(0, idx - 250), idx + 250) : '';
      const roleMatch = snippet.match(/(for|on behalf of) the\s+([A-Za-z ]{3,35})/i);
      const role = roleMatch ? roleMatch[2].trim() : null;
      return { snippet, role };
    });
    await shot('detail.png');
    const fs = require('fs');
    fs.writeFileSync('detail.json', JSON.stringify(info, null, 2));
    return { ok: true };
  }

  try {
    await login();
    await openSupreme();
    await selectAdvocatesIfPresent();
    await filterByQuery();
    await expandDetailAndExtract();
  } finally {
    await browser.close();
  }
}

main().catch(err => { console.error(err); process.exit(1); });

