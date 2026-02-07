import type { TestRunnerConfig } from '@storybook/test-runner';
import { getStoryContext } from '@storybook/test-runner';
import { checkA11y, configureAxe, injectAxe } from 'axe-playwright';

const config: TestRunnerConfig = {
  async preRender(page) {
    await injectAxe(page);
  },
  async postRender(page, context) {
    const storyContext = await getStoryContext(page, context);

    if (storyContext.parameters?.a11y?.disable) {
      return;
    }

    await page.evaluate(() => {
      if (!window.__PRISM_A11Y__) {
        window.__PRISM_A11Y__ = { running: false };
      }
    });

    await configureAxe(page, {
      runOnly: {
        type: 'tag',
        values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']
      },
      ...(storyContext.parameters?.a11y?.config ?? {})
    });

    await page.waitForFunction(() => !window.__PRISM_A11Y__?.running);
    await page.evaluate(() => {
      if (window.__PRISM_A11Y__) {
        window.__PRISM_A11Y__.running = true;
      }
    });

    try {
      await checkA11y(page, '#storybook-root', {
        detailedReport: true,
        detailedReportOptions: { html: true }
      });
    } finally {
      await page.evaluate(() => {
        if (window.__PRISM_A11Y__) {
          window.__PRISM_A11Y__.running = false;
        }
      });
    }
  }
};

export default config;
