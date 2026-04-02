import tsParser from '@typescript-eslint/parser';

export default [
  {
    files: ['src/mobile/**/*.ts'],
    languageOptions: {
      parser: tsParser,
    },
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@umbraco-cms/backoffice', '@umbraco-cms/backoffice/*'],
              message:
                'Mobile boundary violation: @umbraco-cms/backoffice must not be imported in src/mobile/. Keep this bundle lean — it loads on every member-facing page view.',
            },
          ],
        },
      ],
    },
  },
];
