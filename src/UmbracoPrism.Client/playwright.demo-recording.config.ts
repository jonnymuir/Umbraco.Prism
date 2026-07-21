import { defineConfig } from '@playwright/test';

// Not a test config — a recording tool. Deliberately excluded from CI: no npm script or
// workflow references this file, and no demo spec's filename matches
// playwright.localhost-auth.config.ts's testMatch, so they can never run there either.
export default defineConfig({
  testDir: './tests/demo',
  testMatch: /(garden-waste|licence-transfer)-demo\.spec\.ts/,
  globalSetup: './tests/demo/support/demo-prereqs-setup.ts',
  fullyParallel: false,
  workers: 1,
  // Per-test default — deliberately not the 20-minute ceiling Act 3 needs for a real agent call;
  // that test overrides its own timeout via test.setTimeout() so other acts fail fast on a stuck
  // selector instead of burning the whole budget.
  timeout: 5 * 60_000,
  expect: { timeout: 30_000 },
  use: {
    baseURL: 'https://localhost:44345',
    ignoreHTTPSErrors: true,
    // Headless Chromium throttles rendering on a backgrounded tab — a real take confirmed this
    // can visually freeze the recorded video on one frame (screenshots showed the same page for
    // 50+ seconds while narration correctly cycled through later captions) even though the
    // underlying automation kept working correctly the whole time. Setting this here, not just
    // relying on remembering a `--headed` CLI flag, is what actually fixes it for every future
    // take of any demo using this config.
    headless: false,
    // The spec never destructures Playwright's built-in `page` fixture — it creates and records
    // its own single page in beforeAll (see garden-waste-demo.spec.ts) so every act shares one
    // continuous video instead of one-per-test. `use.video` would be a no-op here either way.
    trace: 'off'
  }
});
