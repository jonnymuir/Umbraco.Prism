import type { StorybookConfig } from '@storybook/web-components-vite';

const config: StorybookConfig = {
  stories: ['../src/**/*.stories.@(ts|tsx)'],
  addons: [
    '@storybook/addon-essentials',
    '@storybook/addon-interactions',
    '@storybook/addon-a11y'
  ],
  framework: {
    name: '@storybook/web-components-vite',
    options: {}
  },
  docs: {
    autodocs: 'tag'
  },
  viteFinal: async (config) => {
    config.optimizeDeps ??= {};
    config.optimizeDeps.include = Array.from(
      new Set([
        ...(config.optimizeDeps.include ?? []),
        '@umbraco-ui/uui-icon',
        '@umbraco-ui/uui-icon-registry-essential',
        '@umbraco-ui/uui-css'
      ])
    );
    return config;
  }
};

export default config;
