import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  testIgnore: ['**/localhost-auth-session.spec.ts', '**/workflow-gds-journey.spec.ts'],
  timeout: 30_000,
  retries: 1,
  expect: {
    toHaveScreenshot: {
      pathTemplate: '{testDir}/__screenshots__{/projectName}/{platform}/{testFilePath}/{arg}{ext}'
    }
  },
  use: {
    baseURL: 'http://127.0.0.1:6006',
    trace: 'on-first-retry'
  },
  webServer: {
    command: 'npm run storybook -- --quiet',
    url: 'http://127.0.0.1:6006',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000
  }
});
