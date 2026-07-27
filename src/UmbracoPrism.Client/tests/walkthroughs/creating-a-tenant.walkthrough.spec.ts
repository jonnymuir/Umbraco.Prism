// Executable counterpart of docs/walkthroughs/creating-a-tenant.md. See .claude/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';
import { resetServiceBlueprints } from './support/walkthrough';

const appHost = new LiveAppHost();

test.describe('Creating a tenant walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  test.beforeEach(async ({ request }) => {
    await resetServiceBlueprints(request);
  });

  // All steps require backoffice login — they cannot be driven by the demo-user
  // OIDC session. Manual captures are required for all steps.
  //
  // TODO (manual captures required):
  //   01-backoffice-login.png       — navigate to /umbraco, screenshot the login screen
  //   02-prism-dashboard.png        — Settings → Prism Dashboard, tenant list
  //   03-new-tenant-modal.png       — click "Add tenant", screenshot the modal
  //   04-branding-tab.png           — modal → Branding tab
  //   05-tenant2-homepage.png       — browser at tenant2.localhost after tenant created
  test.skip(true, 'Manual capture only — see SKILL.md R6');
  test('happy path: creating a tenant', async () => {
    // All steps require backoffice login — no automatable surface.
  });
});
