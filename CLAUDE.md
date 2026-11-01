# Umbraco Prism: Project Guide for Claude Code

## What this is

Umbraco Prism is an Umbraco v17 package that adds multi-tenant OIDC authentication, runtime branding, and native mobile app generation. The repo is a mono-repo containing the package itself plus a full demo stack (TestSite, MockBusinessApp, Keycloak) orchestrated via .NET Aspire.

The GDS-style service blueprint engine and its Umbraco-hosted implementation have been extracted as **Wayfinder**, a standalone product now living in its own repos, [`jonnymuir/Wayfinder`](https://github.com/jonnymuir/Wayfinder) (`Wayfinder`, `Wayfinder.Engine`/`.Api`/`.Mcp`, `Wayfinder.Editor`, `Wayfinder.Tests`) and [`jonnymuir/Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco), published to nuget.org, the only publish/restore target for either repo; there is no GitHub Packages feed. `UmbracoPrism.Core` no longer ships any service-blueprint rendering/storage/authoring feature at all, that entire "CMS Service Blueprint" surface (backoffice controller, single-queue validator, MCP wrapper, backoffice TS) was removed once it became fully redundant with `Wayfinder.Umbraco`'s own "Blueprints" entry under Umbraco's built-in Settings section (Advanced group), no custom top-level section, matching Umbraco's own Webhook management package's own placement convention. Core is purely tenancy/branding/mobile now; its own "Tenants" screen lives in the same place (Settings → Advanced), not a custom "Prism" section either. `UmbracoPrism.TestSite` is the reference consumer: it installs `Wayfinder.Umbraco` directly (its own `PackageReference`, not through Core) and owns its own small demo-queue implementation (`PublicVisitorQueue`, identity resolution, file upload/download controllers) the same way any other host would. This is a breaking change to the published `UmbracoPrism` package's feature set, a host that used the old CMS Service Blueprint feature must now install `Wayfinder.Umbraco` itself and own the equivalent host-side wiring (see `UmbracoPrism.TestSite` for the reference pattern).

Building this repo from source restores against nuget.org only (`NuGet.config`), no token or secondary feed needed. The compiled service blueprint editor web component no longer has any local copy in this repo at all, `Wayfinder.Editor` and its own TS source now live entirely in the `Wayfinder` repo, consumed here purely as a transitive dependency of `Wayfinder.Umbraco`.

Solo developer project, but every change reaches `main` through a pull request. `main` is protected (required status checks, no admin bypass) and stays green at all times.

---

## Projects

| Project | Purpose |
|---|---|
| `UmbracoPrism.Core` | The publishable Umbraco package, multi-tenant OIDC, branding, mobile, controllers/middleware/services for those. Ships the starter branding design system (`wwwroot/branding/*.css`, served at `/App_Plugins/UmbracoPrism/branding/` and read from the assembly by `BrandingService`), including the GOV.UK token bridge; TestSite uses it like any other host. No service-design opinion of its own at all; carries no dependency on `Wayfinder`/`Wayfinder.Umbraco`. |
| `UmbracoPrism.uSync` | Published as `UmbracoPrism.uSync`: uSync portability for Prism's own tenants and page access policies (`PrismTenantHandler`, `PrismPageAccessPolicyHandler`). Service blueprint uSync portability lives entirely in `Wayfinder.Umbraco` now (`ServiceBlueprintHandler`/`ServiceBlueprintSerializer`), with no Prism dependency. |
| `UmbracoPrism.Automate` | Published as `UmbracoPrism.Automate`: the "Send Prism push notification" Umbraco Automate action. |
| `UmbracoPrism.Client` | TypeScript/Lit web components, backoffice extensions (tenant management, branding, mobile), mobile shell. No longer builds any service blueprint editor bundle, that moved entirely to the `Wayfinder` repo's own `Wayfinder.Editor.Client`. |
| `UmbracoPrism.MockBusinessApp` | A separate downstream business API that journeys call as the signed-in member (member records, contributions-file validation) and that proves Prism's bearer-token identity propagation. Hosts no Wayfinder engine. Its `service-blueprints/*.yaml` are test fixtures read by `UmbracoPrism.Core.Tests`. |
| `UmbracoPrism.TestSite` | Demo Umbraco site wired to Prism Core and MockBusinessApp. Also installs `Wayfinder.Umbraco` directly and owns its own small demo-queue implementation (`PublicVisitorQueue`, `PublicServiceRequestPageController`, file upload/download controllers), the reference pattern for any host wanting a Wayfinder.Umbraco-backed service blueprint page. |
| `UmbracoPrism.AppHost` | .NET Aspire orchestrator for local dev (Keycloak + TestSite + MockBusinessApp) |
| `UmbracoPrism.KeycloakProxy` | YARP reverse proxy for Keycloak in Aspire |
| `UmbracoPrism.ServiceDefaults` | Shared Aspire service defaults |
| `UmbracoPrism.Core.Tests` | xUnit unit/integration test suite. References `Wayfinder`, `Wayfinder.Engine` and `Wayfinder.Umbraco` to test the reference site's blueprints and demo queue. |

Three packages are published, together and with one version (`<Version>` in
`Directory.Build.props`): `UmbracoPrism` (from `UmbracoPrism.Core`), `UmbracoPrism.Automate` and
`UmbracoPrism.uSync`. The two add-ons depend on `UmbracoPrism`, so a site that doesn't use
Automate or uSync never takes those dependencies. Each has its own `README.md`, which is its
NuGet readme.

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

# Performance tripwire (k6) against a running stack; nightly in perf.yml
k6 run -e BASE_URL=https://localhost:44345 tests/perf/smoke.js

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

- **The reference site's runtime blueprints**, YAML files in `src/UmbracoPrism.TestSite/service-blueprints/`,
  seeded into Wayfinder.Umbraco's database-backed store. Their queues are `public-visitor` (the
  citizen side), `njf-upload` (the contributions team), `web-user`/`business-user` (the money
  modeller), and `automation`/`identification` (system lanes).
- **`PublicVisitorQueue`** in TestSite: the single-queue constraint and tenant scoping for the
  public journeys, enforced through Wayfinder.Umbraco's `SingleQueueStructuralValidator`.
- **Wayfinder's MCP authoring endpoint**, wired in TestSite's `Program.cs` at
  `/wayfinder/service-blueprint-authoring/mcp`, with sign-in through the backoffice. The Blueprints
  screen's **Connect a design partner** panel shows how to connect.
- **Access control is the host's job.** Wayfinder.Umbraco calls TestSite's `ResolveAccessProfile`
  (asynchronous since Wayfinder.Umbraco 3.0) to decide which queues a person can see and act on.
- **Test fixtures** in `src/UmbracoPrism.MockBusinessApp/service-blueprints/`
  (`community-enquiry`, `information-request`, `money-modeller`, `payment-demo`,
  `planning-notification`), read from disk by `UmbracoPrism.Core.Tests`. Validated copies of the
  same examples, used by the guides, live in Wayfinder's `docs/examples/`.
- **Agent-facing guidance for site builders** lives in Wayfinder.Umbraco
  (`docs/building-a-site.md`, the `wayfinder-agents` template) and Wayfinder
  (`docs/guides/building-with-wayfinder.md`, the MCP server's instructions and prompts). Prism's
  README points to it; keep that pointer, and don't write a second copy here.
- **Business maths** belongs in a blueprint's `calculations` block, never in a host service or a
  client component. Prism ships no calculation or live-form runtime of its own; Wayfinder's
  `wayfinder-calculations.js` and `wayfinder-live-form.js` are the only ones.

### Authentication

Per-tenant OIDC against any compliant provider (Keycloak locally, Entra ID and others in production). Stateless token handling; per-tenant JWKS validation; nonce hard-fail on mismatch. Wayfinder journeys get an `ActorProfile` derived from the signed-in member's claims through TestSite's `ResolveAccessProfile`.

---

## Releasing

`UmbracoPrism`, `UmbracoPrism.Automate` and `UmbracoPrism.uSync` release together at one version.
Run `/release` (from the engineering-standards plugin):

- **Version files:** `<Version>` in `Directory.Build.props` and `"version"` in
  `src/UmbracoPrism.Client/package.json`, kept in sync.
- **Pre-release checks:** `npm run check:marketplace`, then
  `dotnet test UmbracoPrism.slnx -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests`.
- **The `v{version}` tag** triggers `package-release.yml`, which builds, packs and publishes the
  three packages.

## Key conventions

Engineering standards (security, simplicity, testing, change flow, docs, writing style) are in
[jonnymuir/engineering-standards](https://github.com/jonnymuir/engineering-standards/blob/main/STANDARDS.md).
Follow them. This section adds only what is specific to this repo.

### Testing

- **Semantic hooks:** components expose `data-prism-*` attributes for Playwright.
- **Date inputs:** target sub-fields by generated IDs, `{fieldKey}-day`, `{fieldKey}-month`,
  `{fieldKey}-year`.
- C# tests: xUnit + Moq + AwesomeAssertions. No database mocks; integration tests use a real
  test host.
- Playwright configs: `playwright.config.ts` (Storybook tests) and
  `playwright.localhost-auth.config.ts` (live Aspire stack tests, run separately).

### Keep things small

The build treats warnings as errors with the `latest-recommended` analyzers and the CA1502/CA1506
complexity budgets (`CodeMetricsConfig.txt`), and `node scripts/check-policies.mjs` (CI) enforces
the size budgets. Code that predates them is baselined: `.policy/size-baseline.json`, the
complexity baseline in `.editorconfig`, and the `.editorconfig` logging baseline (files whose log
calls predate `[LoggerMessage]`). All three may only shrink; new code meets the budgets.

### Documentation

- `README.md` is the source; `MARKETPLACE.md` is generated from it (`npm run generate:marketplace`)
  and CI fails if it is stale.
- Service-design documentation belongs in Wayfinder or Wayfinder.Umbraco, not here.

### Code style

- C#: idiomatic .NET 10 / Umbraco v17 patterns. Use `IContentTypeService`, `IDataTypeService` etc.
  from DI, don't re-register.
- TypeScript: Lit web components.

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
(no card, no icon; see that view's own comment). The game itself (a deterministic puzzle game,
not a Prism feature) has been extracted to its own repo,
[`jonnymuir/Hedgehogger`](https://github.com/jonnymuir/Hedgehogger). This repo has zero
dependency on it, carries no copy of its code, and that repo's own deploy pipeline keeps
`/games/hedgehogger/` populated on prismreference.com's VPS independently of this repo's own
`deploy-prism-reference.yml`. The link itself never needs to change when the game does.
