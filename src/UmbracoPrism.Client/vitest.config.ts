import { defineConfig } from 'vitest/config';
import { playwright } from '@vitest/browser-playwright';
import { storybookTest } from '@storybook/addon-vitest/vitest-plugin';

// The Storybook interaction + a11y gate, run as Vitest browser-mode tests. Replaces the former
// `@storybook/test-runner` lane (`test-storybook:ci:all`), which is incompatible with
// Storybook 10's config loader. Each *.stories.ts play function runs as a test; the a11y addon
// (see .storybook/preview.ts `a11y.test: 'error'`) fails the run on any WCAG A/AA violation.
// Storybook 10.3+ applies the preview decorators + a11y annotations automatically, so no setup file.
export default defineConfig({
  plugins: [storybookTest({ configDir: '.storybook' })],
  test: {
    name: 'storybook',
    // Browser-mode story tests are more timing-sensitive than the former jest-based
    // @storybook/test-runner (which retried internally); one retry absorbs the odd
    // layout/ResizeObserver race without masking a real regression.
    retry: 1,
    browser: {
      enabled: true,
      provider: playwright(),
      headless: true,
      // Same three engines the former `test-storybook:ci:all` lane covered.
      instances: [{ browser: 'chromium' }, { browser: 'firefox' }, { browser: 'webkit' }]
    }
  }
});
