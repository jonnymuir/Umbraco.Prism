// Executable counterpart of docs/walkthroughs/authoring-a-workflow.md. See .claude/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { signIn, resetWorkflows } from './support/walkthrough';

const appHost = new LiveAppHost();

test.describe('Authoring a workflow walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test.beforeEach(async ({ request }) => {
    await resetWorkflows(request);
  });

  // Most of this walkthrough covers source files and JSON — not interactive browser
  // pages — so the automatable surface is limited to the final verification step
  // (the seeded leave-request workflow rendered in the TestSite).
  //
  // TODO (manual captures required for backoffice steps):
  //   01-backoffice-workflow-key.png — backoffice → Content → new Workflow Page → Workflow Key field
  //
  // The leave-request workflow seed must exist in workflow-seeds/ before running.
  test.skip(true, 'Manual capture only — see SKILL.md R6');
  test('happy path: authoring a workflow', async ({ page }) => {
    await signIn(page);
    // Attempt to capture the seeded leave-request workflow — only works if the seed exists.
    await page.goto('/leave-request');
    await page.getByRole('heading', { name: /leave|request annual leave/i }).waitFor({ timeout: 10_000 });
  });
});
