import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  testMatch: /(localhost-auth-session|service-blueprint-gds-journey|four-service-blueprint-contract|walkthroughs\/.*\.walkthrough)\.spec\.ts/,
  globalSetup: './tests/support/aspire-prereqs-setup.ts',
  fullyParallel: false,
  workers: 1,
  timeout: 12 * 60_000,
  expect: { timeout: 30_000 },
  use: {
    baseURL: 'https://localhost:44345',
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure'
  }
});
