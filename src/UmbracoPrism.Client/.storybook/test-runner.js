import { getStoryContext } from '@storybook/test-runner';
import { checkA11y, configureAxe, injectAxe } from 'axe-playwright';

let a11yQueue = Promise.resolve();

/** @type {import('@storybook/test-runner').TestRunnerConfig} */
const config = {
  async preVisit(page) {
    await injectAxe(page);
  },
  async postVisit(page, context) {
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

    a11yQueue = a11yQueue.then(async () => {
      await page.waitForFunction(() => !window.__PRISM_A11Y__?.running);
      await page.evaluate(() => {
        if (window.__PRISM_A11Y__) {
          window.__PRISM_A11Y__.running = true;
        }
      });
      let retries = 3;
      while (retries > 0) {
        try {
          await checkA11y(page, '#storybook-root', {
            detailedReport: true,
            detailedReportOptions: { html: true }
          });
          break;
        } catch (err) {
          if (err && err.message && err.message.includes('Axe is already running')) {
            retries--;
            await new Promise(r => setTimeout(r, 500));
            continue;
          }
          throw err;
        }
      }
      await page.evaluate(() => {
        if (window.__PRISM_A11Y__) {
          window.__PRISM_A11Y__.running = false;
        }
      });
    });

    await a11yQueue;
  }
};

export default config;
