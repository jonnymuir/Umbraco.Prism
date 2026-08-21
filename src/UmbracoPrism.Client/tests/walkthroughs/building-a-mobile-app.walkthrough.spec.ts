// Executable counterpart of docs/walkthroughs/building-a-mobile-app.md. See .claude/skills/walkthroughs-as-executable-specs/SKILL.md.
import { test } from '@playwright/test';
import { LiveAppHost } from '../support/live-app-host';

const appHost = new LiveAppHost();

test.describe('Building a mobile app walkthrough', () => {
  test.describe.configure({ mode: 'serial' });
  test.setTimeout(12 * 60_000);

  test.beforeAll(async () => {
    await appHost.start();
  });

  test.afterAll(async () => {
    await appHost.stop();
  });

  // All mobile captures require a physical device or emulator — they cannot be
  // scripted via Playwright running against the web stack.
  //
  // TODO (manual captures required):
  //   01-biometric-enroll.png             — iOS/Android device: biometric enrollment prompt
  //   02-backoffice-biometric-setting.png — backoffice → Prism Dashboard → Biometric Auth toggle
  //   03-mobile-nav.png                   — Storybook: Prism Mobile Nav story, OR physical device
  //   04-ios-app-running.png              — Physical iOS device or Xcode simulator
  //   05-android-app-running.png          — Android emulator or physical device
  test.skip(true, 'Manual capture only — see SKILL.md R6');
  test('happy path: building a mobile app', async () => {
    // All captures require a physical device or emulator — manual capture only.
  });
});
