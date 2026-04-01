import type { Preview } from '@storybook/web-components';
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
    actions: { argTypesRegex: '^on[A-Z].*' },
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/
      }
    }
  }
};

export default preview;
