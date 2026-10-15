# Umbraco Prism: Project Guide for Claude Code

## What this is

Umbraco Prism is an Umbraco v17 package that adds multi-tenant OIDC authentication, runtime branding, and native mobile app generation. The repo is a mono-repo containing the package itself plus a full demo stack (TestSite, MockBusinessApp, Keycloak) orchestrated via .NET Aspire.

The GDS-style service blueprint engine and its Umbraco-hosted implementation have been extracted as **Wayfinder**, a standalone product now living in its own repos, [`jonnymuir/Wayfinder`](https://github.com/jonnymuir/Wayfinder) (`Wayfinder`, `Wayfinder.Engine`/`.Api`/`.Mcp`, `Wayfinder.Editor`, `Wayfinder.Tests`) and [`jonnymuir/Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco), published to nuget.org, the only publish/restore target for either repo; there is no GitHub Packages feed. `UmbracoPrism.Core` no longer ships any service-blueprint rendering/storage/authoring feature at all, that entire "CMS Service Blueprint" surface (backoffice controller, single-queue validator, MCP wrapper, backoffice TS) was removed once it became fully redundant with `Wayfinder.Umbraco`'s own "Blueprints" entry under Umbraco's built-in Settings section (Advanced group), no custom top-level section, matching Umbraco's own Webhook management package's own placement convention. Core is purely tenancy/branding/mobile now; its own "Tenants" screen lives in the same place (Settings → Advanced), not a custom "Prism" section either. `UmbracoPrism.TestSite` is the reference consumer: it installs `Wayfinder.Umbraco` directly (its own `PackageReference`, not through Core) and owns its own small demo-queue implementation (`PublicVisitorQueue`, identity resolution, file upload/download controllers) the same way any other host would. This is a breaking change to the published `UmbracoPrism` package's feature set, a host that used the old CMS Service Blueprint feature must now install `Wayfinder.Umbraco` itself and own the equivalent host-side wiring (see `UmbracoPrism.TestSite` for the reference pattern).

Building this repo from source restores against nuget.org only (`NuGet.config`), no token or secondary feed needed. The compiled service blueprint editor web component no longer has any local copy in this repo at all, `Wayfinder.Editor` and its own TS source now live entirely in the `Wayfinder` repo, consumed here purely as a transitive dependency of `Wayfinder.Umbraco`.

Solo developer project, but every change reaches `main` through a pull request. `main` is protected — required status checks, no admin bypass — and stays green at all times. No direct pushes.

---

## Projects

| Project | Purpose |
|---|---|
| `UmbracoPrism.Core` | The publishable Umbraco package, multi-tenant OIDC, branding, mobile, controllers/middleware/services for those. No service-design opinion of its own at all; carries no dependency on `Wayfinder`/`Wayfinder.Umbraco`. |
| `UmbracoPrism.uSync` | uSync portability for Prism's own tenant configuration only (`PrismTenantHandler`/`PrismTenantSerializer`). Service blueprint uSync portability lives entirely in `Wayfinder.Umbraco` now (`ServiceBlueprintHandler`/`ServiceBlueprintSerializer`), with no Prism dependency. |
| `UmbracoPrism.Client` | TypeScript/Lit web components, backoffice extensions (tenant management, branding, mobile), mobile shell. No longer builds any service blueprint editor bundle, that moved entirely to the `Wayfinder` repo's own `Wayfinder.Editor.Client`. |
| `UmbracoPrism.MockBusinessApp` | A separate downstream business API that journeys call as the signed-in member (member records, contributions-file validation) and that proves Prism's bearer-token identity propagation. Hosts no Wayfinder engine. Its `service-blueprints/*.json` are test fixtures read by `UmbracoPrism.Core.Tests`. |
| `UmbracoPrism.TestSite` | Demo Umbraco site wired to Prism Core and MockBusinessApp. Also installs `Wayfinder.Umbraco` directly and owns its own small demo-queue implementation (`PublicVisitorQueue`, `PublicServiceRequestPageController`, file upload/download controllers), the reference pattern for any host wanting a Wayfinder.Umbraco-backed service blueprint page. |
| `UmbracoPrism.AppHost` | .NET Aspire orchestrator for local dev (Keycloak + TestSite + MockBusinessApp) |
| `UmbracoPrism.KeycloakProxy` | YARP reverse proxy for Keycloak in Aspire |
| `UmbracoPrism.ServiceDefaults` | Shared Aspire service defaults |
| `UmbracoPrism.Core.Tests` | xUnit unit/integration test suite. References `Wayfinder`, `Wayfinder.Engine` and `Wayfinder.Umbraco` to test the reference site's blueprints and demo queue. |

Only `UmbracoPrism.Core` is packed and published (as the `UmbracoPrism` NuGet package).
`UmbracoPrism.uSync` and `UmbracoPrism.Automate` (the "Send Prism push notification" Automate
action) are consumed by the reference site as project references and do not ship.

`Wayfinder`, `Wayfinder.Engine`/`.Api`/`.Mcp`, `Wayfinder.Editor`, and `Wayfinder.Umbraco` are no
longer part of this repo, see [`jonnymuir/Wayfinder`](https://github.com/jonnymuir/Wayfinder) and
[`jonnymuir/Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco) if you need
to work on the service blueprint engine itself. `UmbracoPrism.Core` has **zero** dependency on
any Wayfinder package, not even for generic queue plumbing; the old `AddPrismProcessManager()`/
`BusinessAppProcessManagerClient` (an HTTP proxy to a remote "Business App" treated as the
authoritative workflow engine) was removed outright once `UmbracoPrism.MockBusinessApp` narrowed
to a genuine downstream support system and Wayfinder.Umbraco itself became authoritative,
in-process, natively multi-queue (front-stage + backstage in one host, no separate business app
needed, see that repo's own `docs/guides/work-allocation.md`/`team-assignment.md`).
`UmbracoPrism.TestSite` (and `UmbracoPrism.Core.Tests`, which tests it) are the only projects in
this repo with a `PackageReference` to any Wayfinder package.

---

## Build and test commands

### .NET

```bash
# Build everything
dotnet build UmbracoPrism.slnx

# Run unit tests
dotnet test src/UmbracoPrism.Core.Tests/

# Run unit tests (Release, full filter)
dotnet test UmbracoPrism.slnx -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests

# Vulnerability scan
dotnet list src/UmbracoPrism.Core/UmbracoPrism.Core.csproj package --vulnerable --include-transitive
```

### TypeScript / Lit client

```bash
cd src/UmbracoPrism.Client

# Build (TypeScript check + Vite bundle)
npm run build

# Run Playwright tests (Storybook starts automatically via webServer config)
node node_modules/.bin/playwright test --reporter=line

# Run single Playwright test
node node_modules/.bin/playwright test tests/prism-create-tenant-modal.spec.ts -g "test name"

# Storybook dev server
npm run storybook

# Storybook tests (accessibility + interaction)
npm run test-storybook

# Live stack E2E tests (requires Aspire running)
npm run test:playwright:localhost-auth
```

### Aspire (full dev stack)

```bash
# Validate prerequisites first
node scripts/validate-aspire-prereqs.mjs

# Start via VS Code launch config: "C#: Aspire (Full Stack)"
# Or: dotnet run --project src/UmbracoPrism.AppHost
```

---

## Architecture essentials

### Service blueprints (owned by Wayfinder)

This repo owns no service-design code. The `ServiceBlueprint` model, the engine, the calculation
language, the editor and the MCP/REST authoring toolkit are all in
[`jonnymuir/Wayfinder`](https://github.com/jonnymuir/Wayfinder); the Umbraco store, Block Grid
blocks and backoffice **Settings → Advanced → Blueprints** screen are in
[`jonnymuir/Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco). Read their
`CLAUDE.md` and `docs/` before changing anything blueprint-shaped, and never copy their code here.

What this repo does have:

- **The reference site's runtime blueprints**, in `src/UmbracoPrism.TestSite/service-blueprints/`,
  seeded into Wayfinder.Umbraco's database-backed store. Their queues are `public-visitor` (the
  citizen side), `njf-upload` (the contributions team), `web-user`/`business-user` (the money
  modeller), and `automation`/`identification` (system lanes).
- **`PublicVisitorQueue`** in TestSite: the single-queue constraint and tenant scoping for the
  public journeys, enforced through Wayfinder.Umbraco's `SingleQueueStructuralValidator`.
- **Access control is the host's job.** Wayfinder.Umbraco calls TestSite's `ResolveAccessProfile`
  (asynchronous since Wayfinder.Umbraco 3.0) to decide which queues a person can see and act on.
- **Test fixtures** in `src/UmbracoPrism.MockBusinessApp/service-blueprints/`
  (`community-enquiry`, `information-request`, `money-modeller`, `payment-demo`,
  `planning-notification`), read from disk by `UmbracoPrism.Core.Tests`. Validated copies of the
  same examples, used by the guides, live in Wayfinder's `docs/examples/`.
- **Business maths** belongs in a blueprint's `calculations` block, never in a host service or a
  client component. Prism ships no calculation or live-form runtime of its own; Wayfinder's
  `wayfinder-calculations.js` and `wayfinder-live-form.js` are the only ones.

### Authentication

Per-tenant OIDC against any compliant provider (Keycloak locally, Entra ID and others in production). Stateless token handling; per-tenant JWKS validation; nonce hard-fail on mismatch. Wayfinder journeys get an `ActorProfile` derived from the signed-in member's claims through TestSite's `ResolveAccessProfile`.

---

## Key conventions

### Testing

- **Behavioural contracts**: tests assert what the user sees, not implementation details.
- **Semantic selectors**: `getByRole`, `getByLabel`, `getByText`, `aria-*`, `data-prism-*` attributes. Never CSS classes or web component tag names.
- **Date inputs**: target sub-fields by generated IDs, `{fieldKey}-day`, `{fieldKey}-month`, `{fieldKey}-year`.
- **Error assertions**: always check both `[role="alert"]` error summary AND field-level errors.
- C# tests: XUnit + Moq + AwesomeAssertions. No database mocks, integration tests use a real test host.
- Playwright configs: `playwright.config.ts` (Storybook tests) and `playwright.localhost-auth.config.ts` (live Aspire stack tests, run separately).

### Documentation

- `docs/README.md` indexes every current document; add new docs there. `docs/archive/` holds
  superseded docs; never link to it from current documentation.
- `README.md` is the source; `MARKETPLACE.md` is generated from it (`npm run generate:marketplace`)
  and CI fails if it is stale.
- `CHANGELOG.md` is written at release time by the `/release` command, from the Conventional
  Commit history. So write commit subjects a package user can understand, and give a breaking
  change a `BREAKING CHANGE:` body line that says how to upgrade.
- `node scripts/check-doc-links.mjs` (CI) fails on a broken relative link or heading anchor.
- Service-design documentation belongs in Wayfinder or Wayfinder.Umbraco, not here.

### Branch policy

Every change goes through a feature branch + PR. Branch naming: `{type}/{issue-number}-{kebab-slug}` or descriptive. `main` is protected (required checks `security / security-all` and `ci-tests-all`, branch must be current before merge, no admin bypass) — there are no direct commits to `main`, however trivial. An emergency merge means deliberately, and visibly, disabling protection.

### Commit conventions

Always use [Conventional Commits](https://www.conventionalcommits.org/) format. The `/release` skill reads these to infer the semver bump automatically, so the signal must be accurate:

| Prefix | Semver impact | Use for |
|---|---|---|
| `feat:` | **minor** | New user-facing capability |
| `feat!:` or body contains `BREAKING CHANGE:` | **major** | Anything that breaks existing integrations or APIs |
| `fix:` | patch | Bug fixes |
| `perf:` | patch | Performance improvements |
| `refactor:` | patch | Internal restructuring, no behaviour change |
| `test:` | patch | Test additions/changes only |
| `chore:` | patch | Tooling, build, deps, no user impact |
| `docs:` | patch | Documentation only |

When a commit introduces a breaking change, add a `BREAKING CHANGE: <description>` line in the commit body explaining what breaks and how to migrate. This is what the release skill reads to trigger a major bump.

### Code style

- No speculative abstractions, solve the problem at hand.
- No comments unless the *why* is genuinely non-obvious.
- C#: idiomatic .NET 10 / Umbraco v17 patterns. Use `IContentTypeService`, `IDataTypeService` etc. from DI, don't re-register.
- TypeScript: Lit web components. Use `data-prism-*` attributes to expose semantic hooks for Playwright.

### Writing style

No em dashes anywhere, prose, docs, code comments, commit messages. Use a comma or a full stop
instead, whichever fits. Emojis are fine sparingly and only where they add real signposting (e.g.
a short role label), never as decoration.

### Documentation style

Docs, guides, and demo scripts (`docs/**`) describe **current state**, not the history of how
that state was reached. Never write "X originally had Y", "discovered while...", "verified live",
"checked and corrected from an earlier draft". A reader coming to the doc cold has no context
for the "before" and doesn't need it; state the current behaviour directly, as if it had always
been true. That narrative belongs in the commit message or PR description that introduced the
change, not in the doc itself. Applies equally to a narrated `docs/demos/` script, someone else
will read it cold too, not just the person who wrote it.

---

## Demo credentials (local)

| What | Username | Password |
|---|---|---|
| TestSite (Keycloak SSO) | `demo@prism.local` | `password` |
| Vinyl Vault broadcast admin (`vinyl-admin` realm role) | `vinyl-admin@prism.local` | `password` |
| Umbraco backoffice | `admin@prism.local` | `PrismLocal!12345` |
| Keycloak admin | `admin` | `admin` |

---

## Easter eggs

`homePage.cshtml` links to `/games/hedgehogger/index.html`, a small deliberately-undersold 🦔 link
(no card, no icon — see that view's own comment). The game itself (a deterministic puzzle game,
not a Prism feature) has been extracted to its own repo,
[`jonnymuir/Hedgehogger`](https://github.com/jonnymuir/Hedgehogger) — this repo has zero
dependency on it, carries no copy of its code, and that repo's own deploy pipeline keeps
`/games/hedgehogger/` populated on prismreference.com's VPS independently of this repo's own
`deploy-prism-reference.yml`. The link itself never needs to change when the game does.
