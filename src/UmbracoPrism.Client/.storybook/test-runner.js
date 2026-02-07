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

    await configureAxe(page, {
      runOnly: {
        type: 'tag',
        values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']
      },
      ...(storyContext.parameters?.a11y?.config ?? {})
    });

    a11yQueue = a11yQueue.then(() =>
      checkA11y(page, '#storybook-root', {
        detailedReport: true,
        detailedReportOptions: { html: true }
      })
    );

    await a11yQueue;
  }
};

export default config;
