import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  testIgnore: [
    // Live-stack tests: require the Aspire stack (Docker + Keycloak + TestSite).
    // Run these with: npm run test:playwright:localhost-auth
    '**/localhost-auth-session.spec.ts',
    '**/service-blueprint-gds-journey.spec.ts',
    '**/walkthroughs/**',
    '**/four-service-blueprint-contract.spec.ts',
    '**/govuk-frontend-bootstrap.spec.ts',
    '**/demo/**',
  ],
  timeout: 30_000,
  retries: 1,
  expect: {
    toHaveScreenshot: {
      pathTemplate: '{testDir}/__screenshots__{/projectName}/{testFilePath}/{arg}{ext}'
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
