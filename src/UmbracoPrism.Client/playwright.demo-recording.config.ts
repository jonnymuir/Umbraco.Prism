import { defineConfig } from '@playwright/test';

// Not a test config — a recording tool. Deliberately excluded from CI: no npm script or
// workflow references this file, and the spec's filename doesn't match
// playwright.localhost-auth.config.ts's testMatch, so it can never run there either.
export default defineConfig({
  testDir: './tests/demo',
  testMatch: /garden-waste-demo\.spec\.ts/,
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
    video: 'on',
    trace: 'off'
  }
});
