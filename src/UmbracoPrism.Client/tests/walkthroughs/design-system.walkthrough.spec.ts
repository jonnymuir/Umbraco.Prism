// Executable counterpart of docs/walkthroughs/design-system.md. See .squad/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { resetWorkflows } from './support/walkthrough';

const appHost = new LiveAppHost();

test.describe('Design system walkthrough', () => {
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

  // Storybook pages are automatable. Backoffice branding editor steps require
  // backoffice login and are flagged as manual captures.
  //
  // TODO (manual captures required):
  //   04-branding-editor.png           — backoffice → Prism Dashboard → localhost → Branding tab
  //   05-branding-updated-frontend.png — TestSite after changing --prism-primary in branding editor
  test.skip(true, 'Manual capture only — see SKILL.md R6');
  test('happy path: design system overview', async () => {
    // Storybook pages and backoffice branding editor — manual capture required for backoffice steps.
  });
});
