<div align="center">
<img src="assets/logo-horizontal-lockup.svg" width="500" alt="Umbraco Prism Logo">
<h3>One source. A spectrum of brands.</h3>
</div>

# Umbraco Prism

```bash
dotnet add package UmbracoPrism
```

One Umbraco instance. Multiple branded portals, each with its own OIDC identity and runtime
branding. Add a native mobile app with one click.

---

## How it fits together

```mermaid
graph LR
  PRISM["Umbraco Prism<br/>multi-tenant OIDC, branding, mobile<br/>(this repo)"] -- "reference site installs" --> WFU["Wayfinder.Umbraco<br/>Umbraco host"]
  WFU -- "depends on" --> WF["Wayfinder<br/>framework-agnostic engine"]
```

Each arrow points from a project to the one it builds on. The `UmbracoPrism` package itself has
no Wayfinder dependency and no service-design features: it's tenancy, branding and mobile.
`UmbracoPrism.TestSite` (the reference site in this repo) installs
[`Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco) for its citizen-journey
and caseworker-queue pages, exactly the way any other Prism-hosted site would.

## Try it Now: No Install Required

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/jonnymuir/Umbraco.Prism)

Click the button to spin up the full stack in a browser — no local setup, no Docker, no .NET
install. First boot takes ~3 minutes; watch the terminal for the ready banner and URLs.

| What | Username | Password |
|------|----------|----------|
| TestSite, plain member (Keycloak SSO) | `demo@prism.local` | `password` |
| TestSite, NJF caseworker (Keycloak SSO) | `njf-caseworker@prism.local` | `password` |
| Umbraco backoffice (`/umbraco`) | `admin@prism.local` | `PrismLocal!12345` |
| Keycloak admin console | `admin` | `admin` |

Once it's up, follow the **[Bulk Data Review walkthrough](docs/walkthroughs/bulk-data-review.md)**
to see a real service-design journey end to end — a caseworker queue backed by a real downstream
validation service, waiting states, in-place correction, all composed from
[`Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco)'s own Block Grid blocks
with no hand-coded route.

> **When you're done:** stop or delete the Codespace at [github.com/codespaces](https://github.com/codespaces) to free quota.

## Quick Start (local)

```bash
git clone https://github.com/jonnymuir/Umbraco.Prism.git && cd Umbraco.Prism
cd src/UmbracoPrism.Client && npm install && cd ../..
dotnet run --project src/UmbracoPrism.AppHost
```

Needs .NET 10 SDK, Docker Desktop running, and Node 20+. Open the Aspire dashboard
(`https://localhost:17214`), click through to TestSite, and sign in with either demo persona
above. See [docs/development.md](docs/development.md) for the full prerequisites list and
troubleshooting, and [docs/ASPIRE_DEV.md](docs/ASPIRE_DEV.md) for what the Aspire stack wires up.

**Installing the published package into your own site?** See the [full setup guide](docs/umbraco-setup.md):
`dotnet add package UmbracoPrism`, add a `Prism:VaultUri` (or use the local Keycloak demo
exception; see [Secret Management](docs/secret-management.md)), then add your first tenant from
**Settings → Advanced → Tenants**. Prism needs Umbraco 17 and .NET 10.

Two optional add-ons, released alongside it with the same version:

| Package | Adds |
|---|---|
| [`UmbracoPrism.uSync`](src/UmbracoPrism.uSync/README.md) | uSync export and import of tenants and page access policies, so they move between environments with the rest of your site. |
| [`UmbracoPrism.Automate`](src/UmbracoPrism.Automate/README.md) | A "Send Prism push notification" action for Umbraco Automate. |

![The reference site's home page for a signed-in member, branded for its local Keycloak tenant](docs/images/walkthroughs/home-entry/02-signed-in-hero.png)

## What you get

- **Multi-tenant web**: domain-based tenant resolution, one content tree serving every branded
  portal, live branding editor (CSS custom properties become a labelled, type-aware settings
  form — see [Branding Design System](docs/branding-design-system.md)).
- **Stateless OIDC identity**: any OIDC-compliant provider (Entra ID, Keycloak, ...) per tenant,
  zero local Members, tenant isolation enforced by authorization policy, downstream identity
  propagation to your own APIs.
- **Produce Mobile**: turn tenant settings into an iOS/Android app — biometric login, push
  notifications, offline-ready layouts, tenant branding at runtime. See [Mobile Setup](docs/PUSH_SETUP.md) /
  [Biometric Auth](docs/biometric-setup.md).
- **Push notifications**: `IPrismNotificationService` sends to generated mobile apps through
  Firebase, to one member, a genre's subscribers, or a whole tenant. Notify on content publish
  from configuration alone, from an Umbraco Automate action, or from your own code. See
  [Notifications](docs/notifications.md).
- **Page access policies**: require sign-in for any page and limit it to named tenants, from
  **Settings → Advanced → Page access**.
- **Secure-by-default secrets**: production tenants resolve confidential-client secrets through
  Key Vault; nothing raw ever appears in a management API response. See
  [Secret Management](docs/secret-management.md).

## Documentation

**[Full documentation index →](docs/README.md)** — setup, branding, mobile, notifications, and
design docs.

- **[Development Guide](docs/development.md)**: prerequisites, architecture, testing
  (Storybook/Playwright/xUnit), packaging, the local-auth walkthrough, and the Cloudflare tunnel
  setup for phone testing. Start here if you're contributing to this repo.
- **[Walkthroughs](docs/walkthroughs/README.md)**: guided tours of the reference site, starting
  with [Home Entry](docs/walkthroughs/home-entry.md) and
  [Bulk Data Review](docs/walkthroughs/bulk-data-review.md).
- **Service design itself** (the engine, the calculation language, the visual editor) lives in
  [`jonnymuir/Wayfinder`](https://github.com/jonnymuir/Wayfinder) and
  [`jonnymuir/Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco); see their own
  READMEs, not this one.
- **[Changelog](CHANGELOG.md)**: what changed in each release, and how to upgrade.

## Stack

.NET 10 · Umbraco 17+ · Stateless OIDC (any compliant provider) · Azure Key Vault · Capacitor/TypeScript/Storybook for mobile.

## Contributing and security

See [CONTRIBUTING.md](CONTRIBUTING.md). To report a vulnerability, use
[GitHub's private reporting](https://github.com/jonnymuir/Umbraco.Prism/security/advisories/new)
as described in [SECURITY.md](SECURITY.md), not a public issue.

## License

MIT. See [LICENSE](LICENSE).
