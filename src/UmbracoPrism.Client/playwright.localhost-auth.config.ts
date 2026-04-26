import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  testMatch: /(localhost-auth-session|workflow-gds-journey|workflow-all-demos)\.spec\.ts/,
  fullyParallel: false,
  workers: 1,
  timeout: 12 * 60_000,
  use: {
    baseURL: 'https://localhost:44345',
    ignoreHTTPSErrors: true,
    trace: 'retain-on-failure'
  }
});
