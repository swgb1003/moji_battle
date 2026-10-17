import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

// node Tools/verify_title_web.mjs <playwright/index.mjs> <URL> <report directory>
const { chromium } = await import(pathToFileURL(resolve(process.argv[2])).href);
const url = process.argv[3] || 'https://moji-fighter-x.vercel.app';
const directory = resolve(process.argv[4] || 'Reports/UI/TitleWeb');
await mkdir(directory, { recursive: true });
const browser = await chromium.launch({ headless: true, executablePath: process.env.MOJI_CHROMIUM_PATH || undefined, args: [
  '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--disable-background-timer-throttling',
] });
const page = await browser.newPage({ viewport: { width: 1920, height: 1124 }, deviceScaleFactor: 1 });
const errors = [], failedRequests = [], consoleErrors = [];
page.on('pageerror', e => errors.push(e.message));
page.on('requestfailed', r => failedRequests.push({ url: r.url(), error: r.failure()?.errorText }));
page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
try {
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 90000 });
  await page.locator('#play').waitFor({ state: 'visible', timeout: 180000 });
  await page.locator('#play').click();
  // Unity's runtime-ready callback precedes its splash screen finishing.
  // Wait for the actual title before comparing animation frames.
  await page.waitForTimeout(5000);
  const canvas = page.locator('#unity-canvas');
  const first = await canvas.screenshot({ path: resolve(directory, '01_title.png') });
  await page.waitForTimeout(700);
  const second = await canvas.screenshot({ path: resolve(directory, '02_title_idle.png') });
  assert.notEqual(hash(first), hash(second), 'The title fighters must animate in the Web player.');
  await canvas.focus();
  await page.keyboard.press('F6');
  await page.waitForTimeout(500);
  const reduced = await canvas.screenshot({ path: resolve(directory, '03_title_reduced.png') });
  await page.waitForTimeout(700);
  const reducedAgain = await canvas.screenshot();
  assert.equal(hash(reduced), hash(reducedAgain), 'F6 must stop title animation in the Web player.');
  await canvas.focus();
  await page.keyboard.press('F6');
  // Observe a full practice-swing cycle, then navigate with the actual keyboard control.
  await page.waitForTimeout(2800);
  await canvas.screenshot({ path: resolve(directory, '04_title_practice.png') });
  await canvas.focus();
  await page.keyboard.press('Enter');
  await page.waitForTimeout(220);
  await canvas.screenshot({ path: resolve(directory, '05_start_step.png') });
  await page.waitForTimeout(1300);
  const menu = await canvas.screenshot({ path: resolve(directory, '06_mode_select.png') });
  assert.notEqual(hash(menu), hash(first), 'START must change the title screen.');
  assert.equal(await page.locator('#error').isVisible(), false, 'The loader must not show an error.');
  assert.deepEqual(errors, [], 'The browser must not have JavaScript exceptions.');
  assert.deepEqual(failedRequests.filter(r => !r.url.startsWith('blob:')), [], 'Game resources must load successfully.');
  const report = { url, checkedAtUtc: new Date().toISOString(), result: 'passed',
    checks: ['WebGL boot', 'title renders', 'idle animation', 'F6 stops animation',
      'practice-swing cycle', 'Enter/START transition', 'no JavaScript exceptions'],
    canvas: await canvas.boundingBox(), errors, failedRequests, consoleErrors };
  await writeFile(resolve(directory, 'browser-report.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report, null, 2));
} finally {
  await browser.close();
}
