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

// Not a CI test, a demo-recording tool. Run with `npm run demo:record:member-registration` (see
// tests/demo/README.md: warm the Aspire stack first, off-camera). UI-only, no AI agent, so there is
// no long unattended wait. Walks docs/walkthroughs/member-registration.md.

const mailpitOrigin = 'http://localhost:8025';
// Nothing about a brand-new person exists anywhere until they register, so each take makes its own.
const unique = Date.now().toString(36);
const newcomer = { email: `robin.${unique}@prism.local`, password: 'Passw0rd!reg', first: 'Robin', last: 'Reed' };

// TestSite's dev-only "Demo PrismMobile UserAgent" toggle sits over the corner of every page and
// has nothing to do with this story, so dismiss it off-camera whenever a page loads.
async function dismissDevWidget(page: Page): Promise<void> {
  const dismiss = page.getByRole('button', { name: 'Dismiss' });
  if (await dismiss.isVisible({ timeout: 1_500 }).catch(() => false)) {
    await dismiss.click();
  }
}

// Read off-camera: the on-camera step is opening the message in Mailpit, this just finds the link in it.
async function verificationLinkFor(email: string): Promise<string> {
  for (let attempt = 0; attempt < 30; attempt++) {
    const found = await fetch(`${mailpitOrigin}/api/v1/search?query=${encodeURIComponent(`to:${email}`)}`);
    const { messages } = (await found.json()) as { messages: { ID: string }[] };
    if (messages.length > 0) {
      const message = (await (await fetch(`${mailpitOrigin}/api/v1/message/${messages[0].ID}`)).json()) as { Text: string };
      const link = message.Text.match(/https?:\/\/\S+action-token\S+/);
      if (link) return link[0];
    }
    await new Promise(resolve => setTimeout(resolve, 1_000));
  }
  throw new Error(`No verification email arrived for ${email}`);
}

test.describe.serial('Registering a member: the identity provider registers, the business app creates the membership', () => {
  let page: Page;

  test.beforeAll(async ({ browser }) => {
    const recordingSize = { width: 1920, height: 1080 };
    const context = await browser.newContext({
      viewport: recordingSize,
      recordVideo: { dir: footageDir, size: recordingSize },
      ignoreHTTPSErrors: true
    });
    page = await context.newPage();
    await fetch(`${mailpitOrigin}/api/v1/messages`, { method: 'DELETE' });
    startNarrationTimeline();
  });

  test.afterAll(async () => {
    const video = page?.video();
    await page?.close();
    if (video) {
      const finalPath = path.join(footageDir, 'member-registration-demo.webm');
      await video.saveAs(finalPath);
      await video.delete();
      tryConvertToMp4(finalPath);
      writeFileSync(
        path.join(footageDir, 'member-registration-narration-timeline.json'),
        JSON.stringify(getNarrationTimeline(), null, 2)
      );
    }
  });

  test('Cold open', async () => {
    await showSlate(page, {
      eyebrow: 'UMBRACO PRISM',
      title: 'Registering a member',
      body:
        'A new person wants to join. Their login must live with the identity provider, and their ' +
        "membership must live in the organisation's business system. How do the two get created, and " +
        'how does the business system know the membership belongs to the person who just registered, ' +
        'and not to someone who typed in their email address?',
      holdMs: 14_000
    });
    await clearSlate(page);
  });

  test('Act 1, the identity provider registers them', async () => {
    await beat(page, 'setup', 'Prism sends the browser to the tenant\'s own identity provider, asking for its registration page. Here, a local Keycloak realm.');
    await page.goto('/auth/register?returnUrl=/register-as-a-member');
    await expect(page.locator('#firstName')).toBeVisible({ timeout: 120_000 });
    await beat(page, 'intent', 'The password is entered here and stays here. Neither Prism nor the business app ever sees it.');

    await humanType(page, page.locator('#firstName'), newcomer.first);
    await humanType(page, page.locator('#lastName'), newcomer.last);
    await humanType(page, page.locator('#email'), newcomer.email);
    await humanType(page, page.locator('#password'), newcomer.password);
    await humanType(page, page.locator('#password-confirm'), newcomer.password);
    await humanClick(page, page.getByRole('button', { name: /register/i }));

    await expect(page.getByText(/verify your email address/i).first()).toBeVisible({ timeout: 60_000 });
    await beat(page, 'recap', 'The account exists, but they cannot go any further until they prove they control this address.');
  });

  test('Act 2, they verify their email', async () => {
    await beat(page, 'setup', 'Keycloak has sent a message. In this stack, a local mail catcher receives it.');
    const link = await verificationLinkFor(newcomer.email);
    await page.goto(mailpitOrigin);
    await expect(page.getByText(newcomer.email).first()).toBeVisible({ timeout: 30_000 });
    await beat(page, 'note', 'The business app will refuse to create a membership for an address the provider has not verified. Otherwise anyone could register as somebody else.');

    await page.goto(link);
    await expect(page).toHaveURL(/\/register-as-a-member\/?/, { timeout: 120_000 });
    await dismissDevWidget(page);
    await beat(page, 'recap', 'Following the link finishes the sign-in, and Keycloak sends them straight back to the journey, signed in as the new person.');
  });

  test('Act 3, the journey creates their membership', async () => {
    await beat(page, 'setup', 'Register as a member is a Wayfinder service blueprint. Its first system stage asks the business app, as this person, whether they already have a membership.');
    await humanClick(page, page.getByRole('button', { name: /continue/i }));
    await expect(page.getByLabel('Your name')).toBeVisible({ timeout: 60_000 });
    await beat(page, 'intent', 'They do not. So the form asks only what they are telling us about themselves. There is no field for the organisation or a role.');

    await humanType(page, page.getByLabel('Your name'), 'Robin Reed');
    await humanType(page, page.getByLabel('Telephone number'), '01632 960 123');
    await humanClick(page, page.getByLabel('phone', { exact: true }));
    await humanClick(page, page.getByRole('button', { name: /continue/i }));
    await humanClick(page, page.getByRole('button', { name: /submit/i }));

    await expect(page.getByText(/MBR-[0-9A-F]{8}/).first()).toBeVisible({ timeout: 60_000 });
    await beat(
      page,
      'recap',
      'The membership id is the business app\'s. It created the membership from this person\'s own token: who they are, and which tenant they join, came from the verified token, never from the form.'
    );
  });

  test('Act 4, the new membership works', async () => {
    await beat(page, 'setup', 'The same member can now use the rest of the service. Update my details looks them up in the business app.');
    await page.goto('/update-my-details?action=start-new');
    await dismissDevWidget(page);
    await humanClick(page, page.getByRole('button', { name: /continue/i }));

    await expect(page.getByText(newcomer.email).first()).toBeVisible({ timeout: 60_000 });
    await beat(page, 'recap', 'Found, with the name and contact details they just gave. Their record is held against a membership id, not their email address.');
  });

  test('Closing slate', async () => {
    await showSlate(page, {
      eyebrow: 'UMBRACO PRISM',
      title: 'The token decides who they are',
      body:
        'The identity provider registered the person and proved their email. The business app took ' +
        'their identity and tenant from the verified token, matched them by a stable id, and ignored ' +
        'anything in the form that tried to say otherwise. Tests pin that down: an unverified email, ' +
        'a login name chosen to look like someone else, and a closed tenant all fail to register.'
    });
  });
});
