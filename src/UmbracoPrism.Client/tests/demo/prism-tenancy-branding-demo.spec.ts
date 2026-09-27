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
// records one video per Page, so as long as nothing ever opens a second page, later acts are just
// later timestamps in the same file, not separate clips needing to be stitched together.
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

// Not a CI test, a demo-recording tool. Run with `npm run demo:record:tenancy-branding` (see
// tests/demo/README.md for the full operator setup: warm the Aspire stack first, off-camera).
// Mirrors Demo 4 of docs/demos/service-design-meetup-talk.md: tenancy and branding, then closing
// the loop on the same juggling-licence world Wayfinder.Umbraco's own demos run.

const adminCredentials = { username: 'admin@prism.local', password: 'PrismLocal!12345' };
const seededTenantRowName = /Local Dev/;
const originalPrimaryColour = '#1d70b8';
const demoPrimaryColour = '#7b1fa2';
const newTenantName = 'Meetup Demo';
const newTenantHostname = 'meetup-demo.local';

test.describe.serial('Umbraco.Prism tenancy and branding demo', () => {
  let page: Page;

  test.beforeAll(async ({ browser }) => {
    const recordingSize = { width: 1920, height: 1080 };
    const context = await browser.newContext({
      viewport: recordingSize,
      recordVideo: { dir: footageDir, size: recordingSize },
      ignoreHTTPSErrors: true
    });
    page = await context.newPage();
    // Delete Tenant is a native browser confirm() dialog, not an in-page element. Playwright
    // auto-dismisses native dialogs unless a handler is registered, confirmed live: without this,
    // clicking Delete silently does nothing and the row stays put.
    page.on('dialog', d => d.accept());
    startNarrationTimeline();
  });

  test.afterAll(async () => {
    // Revert the shared seeded tenant back to its original look and remove anything created
    // during the take, regardless of pass/fail, so a rehearsal pass never leaves the one tenant
    // every other demo also relies on permanently rebranded or cluttered with throwaway rows.
    try {
      await page.goto('/umbraco/section/settings/workspace/prism-tenant-root');
      await page.waitForTimeout(1_000);

      const newTenantDelete = page.getByRole('row', { name: new RegExp(newTenantName) }).getByRole('button', { name: 'Delete' });
      if (await newTenantDelete.count() > 0) {
        await newTenantDelete.click();
        await page.waitForTimeout(1_000);
      }

      await page.getByRole('row', { name: seededTenantRowName }).getByRole('button', { name: 'Edit' }).click();
      await page.waitForTimeout(800);
      await page.getByRole('button', { name: 'More' }).click();
      await page.waitForTimeout(500);
      await page.getByRole('tab', { name: 'prism-colors.css' }).click();
      await page.waitForTimeout(800);
      const primaryField = page.getByLabel('Primary Brand Colour', { exact: true }).first();
      if ((await primaryField.inputValue().catch(() => '')) !== originalPrimaryColour) {
        await primaryField.fill(originalPrimaryColour);
        await page.getByRole('button', { name: 'Update Tenant' }).click();
        await page.waitForTimeout(1_000);
      }
    } catch (err) {
      console.error(`Cleanup pass failed, the seeded tenant may need a manual revert: ${err instanceof Error ? err.message : String(err)}`);
    }

    const video = page?.video();
    await page?.close();
    if (video) {
      const finalPath = path.join(footageDir, 'prism-tenancy-branding-demo.webm');
      await video.saveAs(finalPath);
      await video.delete();
      tryConvertToMp4(finalPath);
      writeFileSync(
        path.join(footageDir, 'prism-tenancy-branding-narration-timeline.json'),
        JSON.stringify(getNarrationTimeline(), null, 2)
      );
    }
  });

  test('Cold open, introduce the demo', async () => {
    await showSlate(page, {
      eyebrow: 'UMBRACO PRISM',
      title: 'One instance, a spectrum of brands',
      body:
        'Tenancy and branding are the two things Prism adds on top of the same Wayfinder.Umbraco ' +
        'contract we just watched work on its own. One new tenant, a live rebrand, then the same ' +
        'juggling-licence journey, running inside it.',
      holdMs: 11_000
    });
    await clearSlate(page);
  });

  test('Act 1, a new tenant, live', async () => {
    await beat(page, 'setup', "This is Prism's own backoffice, the same Umbraco backoffice, nothing bespoke beyond Settings.");
    await page.goto('/umbraco/login');
    await humanType(page, page.getByLabel(/email/i), adminCredentials.username);
    await humanType(page, page.locator('#password-input'), adminCredentials.password);
    await humanClick(page, page.locator('button[type="submit"]').first());
    await page.waitForURL(url => !url.pathname.includes('/login'), { timeout: 30_000 });
    await page.waitForTimeout(1_000);

    await beat(page, 'intent', 'Settings holds a Tenants entry, same placement convention as Blueprints. One row already, the seeded local tenant.');
    await page.goto('/umbraco/section/settings/workspace/prism-tenant-root');
    await page.waitForTimeout(1_200);
    await expect(page.getByRole('row', { name: seededTenantRowName })).toBeVisible({ timeout: 15_000 });

    await beat(page, 'intent', "Let's add a second, fully independent branded portal, live.");
    await humanClick(page, page.getByRole('button', { name: 'Add New Tenant' }));
    await page.waitForTimeout(800);
    await humanType(page, page.getByLabel('Tenant Name'), newTenantName);
    await humanType(page, page.getByLabel('Hostname'), newTenantHostname);

    await humanClick(page, page.getByRole('tab', { name: 'Identity' }));
    await page.waitForTimeout(500);
    await humanType(page, page.getByLabel('OIDC Authority'), 'https://localhost:8443/realms/prism-dev');
    await humanType(page, page.getByLabel('OIDC Client ID'), 'prism-client');
    await humanType(page, page.getByLabel('OIDC Key Vault Secret Name'), 'meetup-demo-oidc-secret');

    await beat(page, 'note', "Its own OIDC identity provider, the same local realm, a real working login, not a stub.", { position: 'top' });
    await humanClick(page, page.getByRole('button', { name: 'Create Tenant' }));
    await page.waitForTimeout(1_500);
    await expect(page.getByRole('row', { name: new RegExp(newTenantName) })).toBeVisible({ timeout: 15_000 });

    await beat(
      page,
      'recap',
      "That's a second fully independent branded portal, its own identity provider config, on the " +
        'one Umbraco instance already running.'
    );
  });

  test('Act 2, live rebrand', async () => {
    await beat(page, 'setup', "Now let's rebrand the seeded tenant, live.", { position: 'top' });
    await page.goto('/umbraco/section/settings/workspace/prism-tenant-root');
    await page.waitForTimeout(1_000);
    await humanClick(page, page.getByRole('row', { name: seededTenantRowName }).getByRole('button', { name: 'Edit' }));
    await page.waitForTimeout(800);

    await beat(page, 'intent', "This isn't a theme picker with six presets. It's the actual CSS custom properties, introspected into a labelled settings form.");
    await humanClick(page, page.getByRole('button', { name: 'More' }));
    await page.waitForTimeout(500);
    await humanClick(page, page.getByRole('tab', { name: 'prism-colors.css' }));
    await page.waitForTimeout(800);

    await humanType(page, page.getByLabel('Primary Brand Colour', { exact: true }).first(), demoPrimaryColour);
    await humanClick(page, page.getByRole('button', { name: 'Update Tenant' }));
    await page.waitForTimeout(1_500);

    await beat(page, 'note', 'Switching to the front end, no redeploy.');
    await page.goto('/');
    await page.waitForTimeout(1_000);
    await expect(page.locator('body')).toHaveCSS('--prism-primary', demoPrimaryColour, { timeout: 10_000 }).catch(() => {});

    await beat(
      page,
      'recap',
      "Every visual property on this page is driven by that same custom property. Instant rebrand, no redeploy."
    );
  });

  test('Act 3, close the loop', async () => {
    await beat(
      page,
      'setup',
      'Tenancy and branding from Prism. The service journey itself from Wayfinder.Umbraco, running in the one site.',
      { position: 'top' }
    );
    await page.goto('/');
    await page.waitForTimeout(800);
    await humanClick(page, page.getByRole('link', { name: 'Apply for a juggling licence' }));
    await page.waitForLoadState('networkidle', { timeout: 15_000 }).catch(() => {});
    await expect(page.getByRole('heading', { name: 'Apply for a juggling licence' })).toBeVisible({ timeout: 15_000 });

    await beat(
      page,
      'recap',
      'Wired by the same three-delegate pattern we saw from the other side, ten minutes ago. ' +
        'Different repo, same seam.'
    );
  });

  test('Closing slate', async () => {
    await showSlate(page, {
      eyebrow: 'UMBRACO PRISM',
      title: 'One person, two hats, four demos',
      body:
        'Every single time, the same person opened Program.cs and then closed it and opened a ' +
        "browser instead. That's not a coincidence of how these three packages were built. It's " +
        'the point of building them this way.'
    });
  });
});
