#!/usr/bin/env node
import { chromium } from 'playwright';
import { mkdir } from 'fs/promises';
import { resolve } from 'path';

const BASE_URL = 'https://localhost:44345';
const CREDS = { username: 'demo@prism.local', password: 'password' };
const KEYCLOAK_URL = 'https://localhost:8443';

async function signIn(page) {
  console.log('Navigating to home page...');
  await page.goto(BASE_URL);
  
  console.log('Clicking Sign In link...');
  await page.getByRole('link', { name: 'Sign In' }).click();
  
  console.log('Waiting for Keycloak login page...');
  await page.locator('#username').waitFor({ timeout: 120000 });
  
  console.log('Filling credentials...');
  await page.locator('#username').fill(CREDS.username);
  await page.locator('#password').fill(CREDS.password);
  
  console.log('Submitting login...');
  await Promise.all([
    page.waitForURL(url => url.origin === BASE_URL && url.pathname !== '/signin-oidc', { timeout: 120000 }),
    page.locator('#kc-login').click()
  ]);
  
  console.log('Navigating back to home...');
  await page.goto(BASE_URL);
  await page.getByRole('link', { name: 'Go to Dashboard' }).waitFor();
  console.log('Signed in successfully!\n');
}

async function captureCommunityEnquiry(page, outputDir) {
  console.log('=== Community Enquiry Workflow ===');
  await mkdir(outputDir, { recursive: true });
  
  await page.goto(`${BASE_URL}/get-in-touch`);
  
  // Wait for page to load - try multiple headings
  try {
    await page.getByRole('heading', { name: 'Tell us about your enquiry' }).waitFor({ timeout: 10000 });
  } catch (e) {
    console.log('  Looking for alternative heading...');
    const headings = await page.locator('h1,h2').allTextContents();
    console.log('  Found headings:', headings);
    
    // Wait for form to be visible anyway
    await page.getByLabel('Full name').waitFor({ timeout: 10000 });
  }
  
  // 01 - Initial form
  console.log('  Capturing: 01-initial-form.png');
  await page.screenshot({ path: `${outputDir}/01-initial-form.png`, fullPage: true });
  
  // Fill About You section (skip readonly fields that are pre-populated from auth)
  try {
    const fullNameReadonly = await page.getByLabel('Full name').evaluate(el => el.hasAttribute('readonly'));
    if (!fullNameReadonly) {
      await page.getByLabel('Full name').fill('Jane Doe');
    }
  } catch (e) {
    console.log('  Skipping full name (readonly or not found)');
  }
  
  try {
    const emailReadonly = await page.getByLabel('Email address').evaluate(el => el.hasAttribute('readonly'));
    if (!emailReadonly) {
      await page.getByLabel('Email address').fill('jane.doe@example.com');
    }
  } catch (e) {
    console.log('  Skipping email (readonly or not found)');
  }
  await page.getByLabel('Organisation (optional)').fill('Acme Corp');
  await page.locator('select#your-role').selectOption('Developer');
  
  // Select "Other" to trigger conditional reveal
  await page.getByRole('radio', { name: 'Other' }).check();
  await page.getByRole('textbox', { name: /specify.*enquiry/i }).waitFor({ timeout: 5000 });
  
  // 02 - Conditional reveal
  console.log('  Capturing: 02-conditional-reveal.png');
  await page.screenshot({ path: `${outputDir}/02-conditional-reveal.png`, fullPage: true });
  
  // Fill the conditional field
  await page.getByRole('textbox', { name: /specify.*enquiry/i }).fill('Partnership enquiry');
  
  // Switch to General enquiry and complete form
  await page.getByRole('radio', { name: 'General enquiry' }).check();
  await page.getByLabel('Tell us more').fill('I would like to learn more about Prism integration options for our Umbraco site.');
  await page.getByRole('checkbox', { name: 'Umbraco CMS' }).check();
  await page.getByRole('checkbox', { name: '.NET Development' }).check();
  
  // 03 - Form filled
  console.log('  Capturing: 03-form-filled.png');
  await page.screenshot({ path: `${outputDir}/03-form-filled.png`, fullPage: true });
  
  // Submit
  await page.getByRole('button', { name: 'Submit' }).click();
  await page.getByRole('heading', { name: 'Your enquiry is with us' }).waitFor();
  
  // 04 - Under review state
  console.log('  Capturing: 04-under-review.png');
  await page.screenshot({ path: `${outputDir}/04-under-review.png`, fullPage: true });
  
  console.log('  ✓ Community enquiry screenshots captured\n');
}

async function capturePaymentDemo(page, outputDir) {
  console.log('=== Payment Demo Workflow ===');
  await mkdir(outputDir, { recursive: true });
  
  // Reset workflow instances
  await page.request.delete(`${BASE_URL}/api/test/reset`, { ignoreHTTPSErrors: true });
  
  await page.goto(`${BASE_URL}/payment-demo`);
  await page.getByRole('heading', { name: 'Enter Payment Details' }).waitFor();
  
  // 01 - Initial form
  console.log('  Capturing: 01-initial-form.png');
  await page.screenshot({ path: `${outputDir}/01-initial-form.png`, fullPage: true });
  
  // Fill payment details
  await page.getByLabel('Cardholder name').fill('Jane Doe');
  await page.getByLabel('Amount (£)').fill('42.50');
  
  // 02 - Form filled
  console.log('  Capturing: 02-form-filled.png');
  await page.screenshot({ path: `${outputDir}/02-form-filled.png`, fullPage: true });
  
  // Submit
  await page.getByRole('button', { name: 'Submit' }).click();
  await page.getByRole('heading', { name: 'Processing Your Payment' }).waitFor();
  
  // 03 - Processing (Waiting component)
  console.log('  Capturing: 03-processing-payment.png');
  await page.screenshot({ path: `${outputDir}/03-processing-payment.png`, fullPage: true });
  
  console.log('  ✓ Payment demo screenshots captured\n');
}

async function capturePlanningNotification(page, outputDir) {
  console.log('=== Planning Notification Workflow ===');
  await mkdir(outputDir, { recursive: true });
  
  // Reset workflow instances
  await page.request.delete(`${BASE_URL}/api/test/reset`, { ignoreHTTPSErrors: true });
  
  await page.goto(`${BASE_URL}/apply-for-planning-permission`);
  await page.getByRole('heading', { name: 'Describe your project' }).waitFor();
  
  // State 1: Project details
  console.log('  Capturing: 01-project-details.png');
  await page.screenshot({ path: `${outputDir}/01-project-details.png`, fullPage: true });
  
  await page.getByLabel('Project name').fill('Loft conversion');
  await page.getByLabel('Describe the proposed works').fill('Converting existing loft space into habitable bedroom with dormer window');
  await page.getByLabel('Property address').fill('456 Oak Avenue\nWoodlands\nWD3 4EF');
  await page.getByRole('button', { name: 'Continue' }).click();
  
  // State 2: Work type
  await page.getByRole('heading', { name: 'Type of work' }).waitFor();
  console.log('  Capturing: 02-work-type.png');
  await page.screenshot({ path: `${outputDir}/02-work-type.png`, fullPage: true });
  
  // Select "Other" to show conditional textarea
  await page.getByRole('radio', { name: 'Other' }).check();
  await page.getByLabel('Describe the type of work').waitFor();
  
  console.log('  Capturing: 03-work-type-conditional.png');
  await page.screenshot({ path: `${outputDir}/03-work-type-conditional.png`, fullPage: true });
  
  await page.getByLabel('Describe the type of work').fill('Listed building restoration with specialist masonry');
  
  // Switch to Extension
  await page.getByRole('radio', { name: 'Extension or alteration' }).check();
  await page.getByRole('radio', { name: 'Yes' }).first().check();
  await page.getByRole('button', { name: 'Continue' }).click();
  
  // State 3: Timeline & cost
  await page.getByRole('heading', { name: 'Timeline and cost' }).waitFor();
  console.log('  Capturing: 04-timeline-cost.png');
  await page.screenshot({ path: `${outputDir}/04-timeline-cost.png`, fullPage: true });
  
  await page.locator('#proposedStartDate-day').fill('1');
  await page.locator('#proposedStartDate-month').fill('9');
  await page.locator('#proposedStartDate-year').fill('2025');
  await page.getByLabel('Estimated duration in weeks').fill('16');
  await page.getByLabel('Estimated cost of works').fill('35000.75');
  await page.getByRole('button', { name: 'Continue' }).click();
  
  // State 4: Affected parties
  await page.getByRole('heading', { name: 'Affected parties' }).waitFor();
  console.log('  Capturing: 05-affected-parties.png');
  await page.screenshot({ path: `${outputDir}/05-affected-parties.png`, fullPage: true });
  
  await page.getByRole('checkbox', { name: 'Neighbouring properties' }).check();
  await page.getByRole('checkbox', { name: 'Conservation area' }).check();
  await page.getByRole('radio', { name: 'Yes' }).last().check();
  await page.getByRole('button', { name: 'Continue' }).click();
  
  // State 5: Check answers (Summary List)
  await page.getByRole('heading', { name: 'Check your answers' }).waitFor();
  console.log('  Capturing: 06-check-answers.png');
  await page.screenshot({ path: `${outputDir}/06-check-answers.png`, fullPage: true });
  
  // Submit
  await page.getByRole('button', { name: 'Submit' }).click();
  await page.locator('.govuk-panel--confirmation').waitFor();
  
  // State 6: Complete (Panel component)
  console.log('  Capturing: 07-complete.png');
  await page.screenshot({ path: `${outputDir}/07-complete.png`, fullPage: true });
  
  console.log('  ✓ Planning notification screenshots captured\n');
}

async function captureInformationRequest(page, outputDir) {
  console.log('=== Information Request Workflow ===');
  await mkdir(outputDir, { recursive: true });
  
  // Reset workflow instances
  await page.request.delete(`${BASE_URL}/api/test/reset`, { ignoreHTTPSErrors: true });
  
  await page.goto(`${BASE_URL}/request-information`);
  await page.getByRole('heading', { name: 'Tell us about yourself' }).waitFor();
  
  // 01 - Initial form
  console.log('  Capturing: 01-initial-form.png');
  await page.screenshot({ path: `${outputDir}/01-initial-form.png`, fullPage: true });
  
  // Fill Your details section
  await page.getByLabel('First name').fill('Jane');
  await page.getByLabel('Last name').fill('Smith');
  await page.locator('#dateOfBirth-day').fill('12');
  await page.locator('#dateOfBirth-month').fill('3');
  await page.locator('#dateOfBirth-year').fill('1985');
  await page.getByLabel('Email address').fill('jane.smith@example.com');
  
  // Fill Your request section
  await page.locator('select#requestType').selectOption('Data subject access request');
  await page.getByLabel('Tell us more about your request').fill('I would like to request a copy of all personal data you hold about me, in accordance with GDPR Article 15.');
  await page.getByRole('radio', { name: 'Urgent (2 working days)' }).check();
  
  // 02 - Form filled
  console.log('  Capturing: 02-form-filled.png');
  await page.screenshot({ path: `${outputDir}/02-form-filled.png`, fullPage: true });
  
  // Submit
  await page.getByRole('button', { name: 'Submit' }).click();
  await page.getByRole('heading', { name: 'Your request is being reviewed' }).waitFor();
  
  // 03 - Under review state
  console.log('  Capturing: 03-under-review.png');
  await page.screenshot({ path: `${outputDir}/03-under-review.png`, fullPage: true });
  
  console.log('  ✓ Information request screenshots captured\n');
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ 
    ignoreHTTPSErrors: true,
    viewport: { width: 1280, height: 1024 }
  });
  const page = await context.newPage();
  
  try {
    await signIn(page);
    
    const baseDir = resolve(process.cwd(), 'docs/images/walkthroughs');
    
    await captureCommunityEnquiry(page, `${baseDir}/community-enquiry`);
    await capturePaymentDemo(page, `${baseDir}/payment-demo`);
    await capturePlanningNotification(page, `${baseDir}/planning-notification`);
    await captureInformationRequest(page, `${baseDir}/information-request`);
    
    console.log('✓ All screenshots captured successfully!');
  } catch (error) {
    console.error('Error capturing screenshots:', error);
    process.exit(1);
  } finally {
    await browser.close();
  }
})();
