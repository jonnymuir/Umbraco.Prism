# Contributing to Umbraco.Prism

Umbraco.Prism is a professional, versioned package distributed via the Umbraco Marketplace. Contributions are welcome but follow a structured review process.

## Before You Start

- **Prism is actively maintained** by Jonny Muir 
- **Biometric and security-sensitive code** requires additional scrutiny, expect thorough review.
- **Marketplace impact:** Changes affect CMS instances. Backward compatibility matters.

## Reporting Bugs

1. **Use GitHub Issues.** Check existing issues first, search by keywords.
2. **Include reproduction steps** and your environment:
   - Umbraco version
   - .NET version
   - Browser/device (especially for mobile/biometric issues)
   - Exact error message or unexpected behavior
3. **Security issues?** Do NOT open a public issue. Email security concerns to the maintainer privately instead.

## Submitting Pull Requests

1. **Fork and branch** from `main`. Use the naming convention: `feature/your-feature` or `fix/bug-description`.
2. **Keep commits focused.** One feature or fix per PR. Avoid unrelated changes.
3. **Run tests locally:**
   - Core tests: `npm run test`
   - Storybook: `npm run storybook`
   - Full build: `npm run build`
4. **Update CHANGELOG.md** if your change affects users:
   - Add an entry under a new `[Unreleased]` section
   - Use categories: New Features, Bug Fixes & Improvements, Chores
5. **Write a clear PR title and description:**
   - Link the related issue (e.g., "Closes #42")
   - Explain *why* the change is needed, not just *what* changed
6. **Expect review.** Maintainer will provide feedback or request changes before merging.

## Code Standards

- **C# / .NET:** Follow existing patterns in the codebase. No new abstractions without team discussion.
- **TypeScript:** Strict mode required. Use the existing Storybook/Web Components patterns.
- **Biometric/Security:** Comments required for non-obvious logic. Test on real devices if possible.
- **No style-only PRs.** Focus on substance.

## Development Setup

See README.md "Setup & Development" for environment setup, dev tunnels, and testing.

## Questions?

Open a GitHub Issue with `[Question]` in the title, or check existing issues for answers.

Thanks for contributing to Umbraco.Prism. 🙏
