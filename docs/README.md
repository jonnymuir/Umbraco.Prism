# Umbraco Prism documentation

Every current document, grouped by what you're trying to do. New to Prism? Start with the
[main README](../README.md), then [Adding Prism to your Umbraco site](umbraco-setup.md).

Prism is tenancy, branding and mobile for Umbraco. Service journeys (citizen forms, caseworker
queues, the service blueprint editor) are not part of Prism: they come from
[Wayfinder](https://github.com/jonnymuir/Wayfinder) and
[Wayfinder.Umbraco](https://github.com/jonnymuir/Wayfinder.Umbraco), which the reference site
installs.

## Set up Prism in your site

- **[Adding Prism to your Umbraco site](umbraco-setup.md)**: install, the content Prism creates,
  and your first tenant.
- **[Creating a tenant](walkthroughs/creating-a-tenant.md)**: hostname, identity provider and
  branding, step by step.
- **[Managing OIDC client secrets](secret-management.md)**: how tenants reference secrets in a
  vault instead of storing them.
- **[Deployment security](DEPLOYMENT_SECURITY.md)**: deployment boundaries, environment
  configuration and pre-flight checks.
- **[Cloudflare maintenance pages](cloudflare-maintenance.md)**: branded error and maintenance
  pages at the edge when the site is down.

## Branding

- **[Branding design system](branding-design-system.md)**: the `@property` + `@prism` CSS
  annotation format, and how tenant overrides reach the live site.
- **[GOV.UK Frontend token bridge](guides/govuk-token-bridge.md)**: GOV.UK components in a
  tenant's brand colours.
- **[Design system walkthrough](walkthroughs/design-system.md)**: tokens from the backoffice to
  live CSS, with the Storybook components.

## Mobile apps and notifications

- **[Building a mobile app](walkthroughs/building-a-mobile-app.md)**: generate a Capacitor
  iOS/Android app from a tenant.
- **[Biometric setup](biometric-setup.md)**: signing and encryption keys for biometric sign-in.
- **[Notifications](notifications.md)**: how Prism registers devices and sends push
  notifications.
- **[Push notification setup](PUSH_SETUP.md)**: Firebase, and what the generated app does.
- **[Mobile app CI/CD pipeline](guides/mobile-app-ci-cd-pipeline.md)**: build, sign and upload
  the app to TestFlight from GitHub Actions.

## Build on Prism

- **[Extending Prism for your business domain](guides/extending-prism.md)**: your own endpoints
  and notifications inside Prism's tenancy, with the reference site's record shop as the example.
- **[Walkthroughs](walkthroughs/README.md)**: guided tours of the reference site, including
  calling a business API as the signed-in member and registering a member.

## Work on this repo

- **[Development guide](development.md)**: prerequisites, architecture, testing and packaging.
  Start here if you're contributing.
- **[Aspire local stack](ASPIRE_DEV.md)**: what `UmbracoPrism.AppHost` runs and how to debug it.
- **[GitHub Codespaces](CODESPACES.md)**: the zero-install demo environment.
- **[Diagnosing downstream API timeouts](MANUAL_DIAGNOSIS_FLOW.md)**: a step-by-step runbook for
  the Mock Business App call in Codespaces.
- **[CONTRIBUTING](../CONTRIBUTING.md)** and **[SECURITY](../SECURITY.md)**: how to contribute and
  how to report a vulnerability.
- **[CHANGELOG](../CHANGELOG.md)**: what changed in each release.

## Research, demos and app-store pages

- **[Proof-of-possession hardware binding](proof-of-possession-analysis.md)**: a feasibility
  analysis for binding mobile tokens to device hardware. Research, not a description of current
  behaviour.
- **[Meetup talk demo script](demos/service-design-meetup-talk.md)** and the
  [recorded licence-transfer demo](demos/licence-transfer-mcp-walkthrough.md) (historical).
- **[`Design/`](../Design/)**, at the repo root: internal design notes and research (identity,
  branding, mobile navigation, biometric sign-in, a penetration-test checklist, and the field
  recording service). Working documents, not user documentation.
- **[Prism reference app](reference-app/README.md)**: the internal-testing mobile build, with its
  [privacy policy](reference-app/privacy-policy.md) and
  [account deletion](reference-app/delete-account.md) pages for the app stores.

## Archive

[`archive/`](archive/README.md) keeps superseded documents for history: the service-blueprint
guides from before Wayfinder was extracted, and the original notifications design. None of them
describes the current code.
