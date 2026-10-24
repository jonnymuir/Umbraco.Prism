# Walkthroughs

Screenshot-driven tours of the reference site (`UmbracoPrism.TestSite`), from what a member sees
through to running a tenant and shipping a mobile app. Each one shows real pages and explains
what Prism is doing behind the scenes. Start the stack first: see
[Try it now](../../README.md#try-it-now-no-install-required) or
[Quick start (local)](../../README.md#quick-start-local).

## Member journeys

The citizen journeys and caseworker queues on the reference site are
[Wayfinder.Umbraco](https://github.com/jonnymuir/Wayfinder.Umbraco) Block Grid blocks on ordinary
Prism pages, not Prism features. These walkthroughs show the two working together.

- **[Home entry](home-entry.md)**: how a visitor first meets the site, signs in, and reaches
  the dashboard and the service journeys.
- **[Bulk data review](bulk-data-review.md)**: a contributions team submits a monthly file, sees
  only the rows that need attention, corrects them in place and resubmits, with the Mock Business
  App as the validating system.
- **[Calling a business app as the signed-in member](authenticated-business-app-call.md)**: an
  "update my details" journey that reads and writes the member's record in a business API with
  the member's own token, scoped to their tenant, and the security checks that pin it down.
- **[Registering a member](member-registration.md)**: a person creates an account at the
  identity provider, verifies their email, and a journey creates their membership, without
  letting anyone claim someone else's.

## Running Prism

- **[Creating a tenant](creating-a-tenant.md)**: add a tenant in the backoffice (hostname,
  identity provider, branding), and how the site picks it up without a restart.
- **[Design system](design-system.md)**: the Storybook components, the `@property` + `@prism` CSS
  annotations, and how a tenant's tokens reach the live CSS.

## Mobile

- **[Building a mobile app](building-a-mobile-app.md)**: generate a Capacitor iOS/Android app
  from a tenant, with biometric sign-in and deep links. For push notifications, see
  [Notifications](../notifications.md) and [Push notification setup](../PUSH_SETUP.md).

## Where the service blueprints live

The reference site's runtime blueprints are in `src/UmbracoPrism.TestSite/service-blueprints/`,
hosted by Wayfinder.Umbraco, all YAML. The YAML files in `src/UmbracoPrism.MockBusinessApp/service-blueprints/`
are test fixtures that `UmbracoPrism.Core.Tests` reads from disk, not live services. To learn the
blueprint format or the editor, use
[Wayfinder's guides](https://github.com/jonnymuir/Wayfinder/blob/main/docs/guides/README.md).
