import type { Preview } from '@storybook/web-components-vite';
import { html } from 'lit';
import '@umbraco-ui/uui-css/dist/custom-properties.css';
import '@umbraco-ui/uui-css/dist/uui-css.css';
import '@umbraco-ui/uui-css/dist/uui-font.css';
import '@umbraco-ui/uui-css/dist/uui-text.css';
import '@umbraco-ui/uui-icon';
import '@umbraco-ui/uui-icon-registry-essential';
import '../src/backoffice/index.css';

const preview: Preview = {
  decorators: [
    (story) => html`
      <uui-icon-registry-essential>
        ${story()}
      </uui-icon-registry-essential>
    `
  ],
  parameters: {
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/
      }
    },
    a11y: {
      // Fail the Vitest run on any violation — the equivalent of the old
      // .storybook/test-runner.js postVisit hook throwing out of axe-playwright's checkA11y.
      test: 'error',
      // Same rule scope that test-runner.js configured: WCAG 2.0 / 2.1, levels A and AA only.
      options: {
        runOnly: {
          type: 'tag',
          values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']
        }
      }
    }
  }
};

export default preview;
