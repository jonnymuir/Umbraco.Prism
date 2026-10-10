import { test, expect, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  beat, showSlate, clearSlate, startNarrationTimeline, getNarrationTimeline,
  humanClick, humanType
} from 'wayfinder-demo-recording-kit';

// One shared Page across every act (see narrated-single-take-demo-recording skill). Playwright
// records one video per Page, so later acts are just later timestamps in the same file.
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const footageDir = path.join(__dirname, '..', '..', 'demo-footage');
mkdirSync(footageDir, { recursive: true });

function tryConvertToMp4(webmPath: string): void {
  const mp4Path = webmPath.replace(/\.webm$/, '.mp4');
  try {
    execFileSync(
      'ffmpeg',
      ['-y', '-i', webmPath, '-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-c:a', 'aac', mp4Path],
      { stdio: 'ignore' }
    );
    console.log(`Also wrote ${mp4Path}.`);
  } catch {
    console.log('ffmpeg not found on PATH, skipping the .mp4 convenience copy. The .webm is the real output.');
  }
}

// Not a CI test, a demo-recording tool. Run with `npm run demo:record:update-my-details` (see
// tests/demo/README.md: warm the Aspire stack first, off-camera). UI-only, no AI agent, so there is
// no long unattended wait. Walks docs/walkthroughs/authenticated-business-app-call.md.

const member = { username: 'demo@prism.local', password: 'password' };
const notAMember = { username: 'njf-caseworker@prism.local', password: 'password' };
// A finished case persists for the member (requestPolicy single), so always start a fresh one.
const journeyUrl = '/update-my-details?action=start-new';

// TestSite's dev-only "Demo PrismMobile UserAgent" toggle sits over the corner of every page and
// has nothing to do with this story, so dismiss it off-camera whenever a page loads.
async function dismissDevWidget(page: Page): Promise<void> {
  const dismiss = page.getByRole('button', { name: 'Dismiss' });
  if (await dismiss.isVisible({ timeout: 1_500 }).catch(() => false)) {
    await dismiss.click();
  }
}

async function signIn(page: Page, credentials: { username: string; password: string }): Promise<void> {
  // A navigation started while a redirect from the previous page is still in flight is aborted, so retry once.
  await page.goto('/').catch(async () => {
    await page.waitForLoadState('load');
    await page.goto('/');
  });
  await humanClick(page, page.getByRole('link', { name: 'Sign In' }));
  await expect(page.locator('#username')).toBeVisible({ timeout: 120_000 });
  await humanType(page, page.locator('#username'), credentials.username);
  await humanType(page, page.locator('#password'), credentials.password);
  await Promise.all([
    page.waitForURL(url => url.origin === 'https://localhost:44345' && url.pathname !== '/signin-oidc', { timeout: 120_000 }),
    humanClick(page, page.locator('#kc-login'))
  ]);
  await expect(page.getByRole('button', { name: 'Sign Out' }).first()).toBeVisible({ timeout: 30_000 });
  await dismissDevWidget(page);
}

async function signOut(page: Page): Promise<void> {
  await page.goto('/');
  await humanClick(page, page.getByRole('button', { name: 'Sign Out' }).first());
  const logoutConfirm = page.locator('#kc-logout');
  if (await logoutConfirm.isVisible({ timeout: 5_000 }).catch(() => false)) {
    await logoutConfirm.click();
  }
  await page.waitForURL(url => url.origin === 'https://localhost:44345' && url.pathname === '/', { timeout: 120_000 });
  await page.waitForLoadState('load');
  await expect(page.getByRole('link', { name: 'Sign In' })).toBeVisible({ timeout: 30_000 });
}

test.describe.serial('Update my details: calling a business app as the signed-in member', () => {
  let page: Page;

  test.beforeAll(async ({ browser }) => {
    const recordingSize = { width: 1920, height: 1080 };
    const context = await browser.newContext({
      viewport: recordingSize,
      recordVideo: { dir: footageDir, size: recordingSize },
      ignoreHTTPSErrors: true
    });
    page = await context.newPage();
    startNarrationTimeline();
  });

  test.afterAll(async () => {
    const video = page?.video();
    await page?.close();
    if (video) {
      const finalPath = path.join(footageDir, 'update-my-details-demo.webm');
      await video.saveAs(finalPath);
      await video.delete();
      tryConvertToMp4(finalPath);
      writeFileSync(
        path.join(footageDir, 'update-my-details-narration-timeline.json'),
        JSON.stringify(getNarrationTimeline(), null, 2)
      );
    }
  });

  test('Cold open', async () => {
    await showSlate(page, {
      eyebrow: 'UMBRACO PRISM',
      title: 'Calling a business app as the member',
      body:
        'A member signs in to a tenant and fills in a front-stage journey. The journey needs the ' +
        "organisation's business system to answer for them. How does that system know who is " +
        'asking, and which tenant they belong to, without the journey ever handling a credential?',
      holdMs: 12_000
    });
    await clearSlate(page);
  });

  test('Act 1, sign in as a member of the tenant', async () => {
    await beat(page, 'setup', 'Prism signs members in through the tenant\'s own identity provider. Here, a local Keycloak realm.');
    await signIn(page, member);
    await beat(page, 'recap', 'Signed in. Prism now holds this member\'s tokens in an encrypted cookie. They are never shown to the journey.');
  });

  test('Act 2, the business app answers for this member', async () => {
    await beat(page, 'setup', 'Update my details is a Wayfinder service blueprint. Its first system stage reads the member\'s record from the business app.');
    await page.goto(journeyUrl);
    await expect(page.getByRole('button', { name: /continue/i })).toBeVisible({ timeout: 30_000 });
    await dismissDevWidget(page);

    await beat(page, 'intent', 'The stage runs inside this request, so the host attaches this member\'s own bearer token to the call.');
    await humanClick(page, page.getByRole('button', { name: /continue/i }));

    await expect(page.getByText('Prism Demo (Keycloak)').first()).toBeVisible({ timeout: 60_000 });
    await beat(
      page,
      'note',
      'Name, email, organisation and role all came from the business app, scoped to this tenant by the token. The form is prefilled from the same answer.'
    );
  });

  test('Act 3, the business app can refuse', async () => {
    await beat(page, 'setup', 'The business app validates every change itself. Let\'s give it a telephone number that is not one.');
    // Prefilled from the business app on any run after the first, so clear it rather than append.
    await page.getByLabel('Telephone number').fill('');
    await humanType(page, page.getByLabel('Telephone number'), 'not a number');
    await humanClick(page, page.getByRole('button', { name: /continue/i }));
    await expect(page.getByRole('button', { name: /submit/i })).toBeVisible({ timeout: 30_000 });
    await humanClick(page, page.getByRole('button', { name: /submit/i }));

    await expect(page.getByText(/correct format/i).first()).toBeVisible({ timeout: 60_000 });
    await beat(page, 'recap', 'A refusal is a route, not an error page. The member sees the business app\'s own reason and can go straight back to the form.');
  });

  test('Act 4, a change it accepts', async () => {
    await humanClick(page, page.getByRole('button', { name: /continue/i }));
    const phone = page.getByLabel('Telephone number');
    await expect(phone).toBeVisible({ timeout: 30_000 });
    await phone.fill('');
    await humanType(page, phone, '01632 960 001');
    await beat(page, 'intent', 'A valid number this time.');
    await humanClick(page, page.getByRole('button', { name: /continue/i }));
    await humanClick(page, page.getByRole('button', { name: /submit/i }));

    await expect(page.getByText(/UPD-[0-9A-F]{8}/).first()).toBeVisible({ timeout: 60_000 });
    await beat(page, 'recap', 'Written with the same member\'s token. The reference on screen is the business app\'s, not Prism\'s.');
  });

  test('Act 5, signed in is not the same as a member', async () => {
    await beat(page, 'setup', 'Now someone who can sign in to this tenant but has no member record in the business system.');
    await signOut(page);
    await signIn(page, notAMember);
    await page.goto(journeyUrl);
    await dismissDevWidget(page);
    await humanClick(page, page.getByRole('button', { name: /continue/i }));

    await expect(page.getByText(/could not find your membership/i).first()).toBeVisible({ timeout: 60_000 });
    await beat(
      page,
      'recap',
      'Same journey, same code, different token. The business app looked up this person for this tenant, and found nobody.'
    );
  });

  test('Closing slate', async () => {
    await showSlate(page, {
      eyebrow: 'UMBRACO PRISM',
      title: 'The journey never held a credential',
      body:
        'The member\'s own token was attached to the outgoing call and nowhere else. The business app ' +
        'read the member and the tenant from it. Tests pin that down: no token means no call, ' +
        'and one tenant\'s record is never visible under another.'
    });
  });
});
