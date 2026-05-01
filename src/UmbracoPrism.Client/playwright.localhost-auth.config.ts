import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  testMatch: /(localhost-auth-session|workflow-gds-journey|walkthroughs\/.*\.walkthrough)\.spec\.ts/,
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
