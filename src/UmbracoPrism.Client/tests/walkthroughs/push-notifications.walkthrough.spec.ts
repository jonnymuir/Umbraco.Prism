// Executable counterpart of docs/walkthroughs/push-notifications.md. See .claude/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';

const appHost = new LiveAppHost();

test.describe('Push notifications walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  // Browser permission prompts and OS notification toasts cannot be automated.
  //
  // TODO (manual captures required):
  //   02-browser-permission.png            — OS/browser permission prompt (cannot be scripted)
  //   03-backoffice-send-notification.png  — backoffice → Announcements → publish
  //   04-browser-notification.png          — OS notification toast (cannot be scripted)
  test.skip(true, 'Manual capture only — see SKILL.md R6');
  test('happy path: push notifications', async () => {
    // Browser permission prompts and OS notification toasts — manual capture only.
  });
});
