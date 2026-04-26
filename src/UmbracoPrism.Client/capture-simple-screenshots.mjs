#!/usr/bin/env node
import { chromium } from 'playwright';
import { mkdir } from 'fs/promises';

const BASE_URL = 'https://localhost:44345';
const CREDS = { username: 'demo@prism.local', password: 'password' };

const workflows = [
  { key: 'community-enquiry', path: '/get-in-touch', name: 'Community Enquiry' },
  { key: 'payment-demo', path: '/payment-demo', name: 'Payment Demo' },
  { key: 'planning-notification', path: '/planning-notification', name: 'Planning Notification' },
  { key: 'information-request', path: '/request-information', name: 'Information Request' }
];

async function signIn(page) {
  console.log('Signing in...');
  await page.goto(BASE_URL);
  await page.getByRole('link', { name: 'Sign In' }).click();
  await page.locator('#username').waitFor({ timeout: 60000 });
  await page.locator('#username').fill(CREDS.username);
  await page.locator('#password').fill(CREDS.password);
  await Promise.all([
    page.waitForURL(url => url.origin === BASE_URL && url.pathname !== '/signin-oidc', { timeout: 60000 }),
    page.locator('#kc-login').click()
  ]);
  await page.goto(BASE_URL);
  await page.getByRole('link', { name: 'Go to Dashboard' }).waitFor({ timeout: 30000 });
  console.log('Signed in!\n');
}

async function captureWorkflow(page, workflow) {
  console.log(`=== ${workflow.name} ===`);
  const outputDir = `../../docs/images/walkthroughs/${workflow.key}`;
  await mkdir(outputDir, { recursive: true });
  
  await page.goto(`${BASE_URL}${workflow.path}`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(2000); // Allow any JS to settle
  
  // Take initial screenshot
  console.log(`  Capturing: 01-initial.png`);
  await page.screenshot({ 
    path: `${outputDir}/01-initial.png`, 
    fullPage: true 
  });
  
  console.log(`  ✓ ${workflow.name} captured\n`);
}

async function main() {
  const browser = await chromium.launch({ 
    headless: true,
    ignoreHTTPSErrors: true
  });
  
  const context = await browser.newContext({
    viewport: { width: 1280, height: 1024 },
    ignoreHTTPSErrors: true
  });
  
  const page = await context.newPage();
  
  try {
    await signIn(page);
    
    for (const workflow of workflows) {
      await captureWorkflow(page, workflow);
    }
    
    console.log('✅ All screenshots captured successfully!');
  } catch (error) {
    console.error('Error capturing screenshots:', error);
    process.exit(1);
  } finally {
    await browser.close();
  }
}

main();
