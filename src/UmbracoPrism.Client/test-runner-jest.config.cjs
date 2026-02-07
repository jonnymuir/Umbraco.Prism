const { getJestConfig } = require('@storybook/test-runner');

/** @type {import('@jest/types').Config.InitialOptions} */
const testRunnerConfig = getJestConfig();
const swcJestPath = require.resolve('@swc/jest', { paths: [__dirname] });

module.exports = {
  ...testRunnerConfig,
  transform: {
    ...testRunnerConfig.transform,
    '^.+\\.[jt]sx?$': [
      swcJestPath,
      {
        jsc: {
          target: 'es2022'
        },
        module: {
          type: 'commonjs'
        }
      }
    ]
  }
};
