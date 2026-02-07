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

    await configureAxe(page, {
      runOnly: {
        type: 'tag',
        values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']
      },
      ...(storyContext.parameters?.a11y?.config ?? {})
    });

    await checkA11y(page, '#storybook-root', {
      detailedReport: true,
      detailedReportOptions: { html: true }
    });
  }
};

export default config;
