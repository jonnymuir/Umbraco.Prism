# Contributing to Umbraco Prism

Umbraco Prism is a versioned package on NuGet and the Umbraco Marketplace, maintained by Jonny
Muir. Contributions are welcome through pull requests and are reviewed before merging.
Security-sensitive code (authentication, tenant isolation, biometric sign-in, secrets) gets
particular scrutiny.

## Reporting bugs

1. **Search the existing [issues](https://github.com/jonnymuir/Umbraco.Prism/issues) first.**
2. **Open an issue** with steps to reproduce and your environment: Umbraco version, .NET version,
   identity provider, and the browser or device for mobile and biometric problems. Include the
   exact error message.
3. **Found a security problem?** Don't open a public issue. Report it privately as described in
   [SECURITY.md](SECURITY.md).

Questions are welcome as issues too; start the title with `[Question]`.

## Submitting a pull request

1. **Fork the repo and branch from `main`.** Name the branch `{type}/{short-description}`, using a
   Conventional Commits type: `feat/tenant-export`, `fix/biometric-banner-overlap`.
2. **Keep it focused**: one feature or fix per pull request.
3. **Write [Conventional Commits](https://www.conventionalcommits.org/).** The changelog is written
   from them at release time, so make each subject understandable to someone using the package.
   A breaking change needs a `BREAKING CHANGE:` line in the body that says how to upgrade.
4. **Run the checks** that CI runs. See the [development guide](docs/development.md) for setup:

   ```bash
   # .NET build and tests
   dotnet build UmbracoPrism.slnx -c Release
   dotnet test UmbracoPrism.slnx -c Release --no-build \
     --filter "FullyQualifiedName~UmbracoPrism.Core.Tests|FullyQualifiedName~UmbracoPrism.Core.IntegrationTests"

   # Client: type check and bundle, then the Storybook component tests
   cd src/UmbracoPrism.Client
   npm ci
   npm run build
   npm run test:playwright
   cd ../..

   # The Marketplace readme is generated from README.md, and doc links must resolve
   npm run check:marketplace
   node scripts/check-doc-links.mjs
   ```

5. **Describe the pull request**: link the issue (`Closes #42`) and explain *why* the change is
   needed, not just what changed. A change to a security boundary needs a test that fails if the
   boundary regresses.

`main` is protected: every change, however small, goes through a pull request with passing
checks.

## Code standards

[`CLAUDE.md`](CLAUDE.md) has the full conventions. In short:

- **C#**: idiomatic .NET 10 and Umbraco 17. Follow the existing patterns; no speculative
  abstractions.
- **TypeScript**: strict mode, Lit web components, `data-prism-*` attributes as test hooks.
- **Tests** check behaviour through public seams: xUnit, Moq and AwesomeAssertions for C#, and
  Playwright with semantic selectors (roles, labels, text) for the UI.
- **Comments** only where the reason isn't obvious from the code.
- **Docs** describe the current behaviour, in plain English, with no em dashes.

Thanks for contributing.
