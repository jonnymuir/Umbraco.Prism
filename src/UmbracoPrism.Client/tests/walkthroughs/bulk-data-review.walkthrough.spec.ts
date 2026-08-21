// Executable counterpart of docs/walkthroughs/bulk-data-review.md. See .claude/skills/walkthroughs-as-executable-specs/SKILL.md.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { test, expect } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { step, signInAsCaseworker } from './support/walkthrough';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const appHost = new LiveAppHost();

// Row 1 and row 2 are clean. Row 3 ("Cara Delgado", NJF-003) has a genuine Mock Business App
// error (an unrecognised tier). Row 4 ("Dev Patel", NJF-004) has a contribution outside the
// expected band for its tier — a warning, not an error. Real server-side validation on a real
// second app (ContributionsValidation.cs), not a scripted fixture.
const contributionsCsvPath = path.join(__dirname, 'fixtures', 'njf-contributions-sample.csv');
const contributionsCsv = readFileSync(contributionsCsvPath, 'utf8');

test.describe('Bulk data review walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  // A single test in this file, and MockBusinessApp is purely in-memory with no persistence, so
  // the fresh process appHost.start() just spawned above is already a clean starting point — no
  // separate reset call needed. If a second test is ever added here that needs isolation from
  // this one, tear down and restart the whole stack between them (see LiveAppHost.restart())
  // rather than adding a targeted reset endpoint back — matches how the Umbraco side of this
  // demo gets a clean slate too (PRISM_TESTSITE_RESET_RUNTIME re-seeds TestSite's own database).

  test('NJF caseworker submits a contributions file, corrects the flagged row, resubmits, and accepts with a warning on record', async ({ page }) => {
    await signInAsCaseworker(page);

    await page.goto('/submit-contributions-file');
    await step(page, '01-submit.png', {
      url: /\/submit-contributions-file/,
      heading: 'Submit contributions file'
    }, 'bulk-data-review');

    await page.getByLabel('Contributions file').setInputFiles({
      name: 'contributions.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(contributionsCsv)
    });
    await page.getByRole('button', { name: 'Submit' }).click();

    // PRG lands on the automation Join gateway's own wait screen directly.
    const instanceUrl = page.url();
    await expect(page.getByText('Mock Business App is processing the contributions file.')).toBeVisible();

    // The submission must stay reachable from the caseworker queue while it's out with Mock
    // Business App, flagged Waiting — for anyone who navigates away and comes back later. njf-upload
    // is assign-to-initiator (see NjfContributionsTeam's own remarks — one queue, not two, so a
    // resubmission's own Join arrival always lands back in the same queue the original submission's
    // did), so the item stays owned by this same caseworker all the way through review with no
    // separate pickup step anywhere.
    await page.goto('/caseworker-queue');
    const queueRow = page.locator('tr', { hasText: 'Submit a contributions file' });
    await expect(queueRow.getByText('Waiting')).toBeVisible();

    // Real batch processing on a genuinely separate app — the review stage's own
    // bulk-dataset-ingest action only fires once the join actually releases.
    await page.goto(instanceUrl);
    await expect(page.getByRole('heading', { name: 'Review contributions file' })).toBeVisible({ timeout: 20_000 });

    // The bulk-data-review card UI is entirely client-fetched — wait for the real content, not
    // the server-rendered loading skeleton it replaces.
    const attentionCard = page.locator('.wayfinder-bulk-review__card', { hasText: 'NJF-003' });
    await expect(attentionCard).toBeVisible({ timeout: 10_000 });
    await expect(attentionCard.getByText(/Unrecognised tier/)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Accept and finish' })).toHaveCount(0);

    await step(page, '02-error-and-warning-cards.png', {
      url: /\/submit-contributions-file/,
      heading: 'Review contributions file'
    }, 'bulk-data-review');

    // No "Save" button — a correction autosaves (debounced) once you stop typing.
    await attentionCard.getByLabel('Membership tier').fill('Recreational');
    await expect(attentionCard.getByText('Pending resubmission')).toBeVisible();

    // A genuine loop: this re-fires the same Split gateway, materializing the just-corrected
    // dataset (not the original upload) back to Mock Business App for real revalidation.
    await page.getByRole('button', { name: 'Resubmit corrected file' }).click();
    await expect(page.getByText('Mock Business App is processing the contributions file.')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Review contributions file' })).toBeVisible({ timeout: 20_000 });

    // Dev Patel's row (NJF-004) is still flagged with a warning — errors are gone, so "Accept and
    // finish" is reachable, but it leads to an explicit confirmation first (see below).
    await expect(page.getByRole('button', { name: 'Accept and finish' })).toBeVisible({ timeout: 10_000 });

    await page.getByRole('button', { name: 'Accept and finish' }).click();

    // A warning still on record means this doesn't finish straight away.
    await expect(page.getByRole('heading', { name: 'Confirm before finishing' })).toBeVisible();
    await step(page, '03-confirm-before-finishing.png', {
      url: /\/submit-contributions-file/,
      heading: 'Confirm before finishing'
    }, 'bulk-data-review');

    await page.getByRole('button', { name: 'Yes, accept with warnings' }).click();

    // Terminal confirmation — the instance drops off the caseworker queue entirely.
    await expect(page.getByRole('heading', { name: 'Contributions file accepted' })).toBeVisible();
    expect(page.url()).toBe(instanceUrl);
  });
});
