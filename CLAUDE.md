# Umbraco Prism — Project Guide for Claude Code

## What this is

Umbraco Prism is an Umbraco v17 package that adds multi-tenant OIDC authentication, runtime branding, and native mobile app generation. The repo is a mono-repo containing the package itself plus a full demo stack (TestSite, MockBusinessApp, Keycloak) orchestrated via .NET Aspire.

The GDS-style service blueprint engine and its Umbraco-hosted implementation have been extracted as **Wayfinder**, a standalone product now living in its own repos — [`jonnymuir/Wayfinder`](https://github.com/jonnymuir/Wayfinder) (`Wayfinder`, `Wayfinder.Engine`/`.Api`/`.Mcp`, `Wayfinder.Editor`, `Wayfinder.Tests`) and [`jonnymuir/Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco), published to nuget.org — the only publish/restore target for either repo; there is no GitHub Packages feed. `UmbracoPrism.Core` no longer ships any service-blueprint rendering/storage/authoring feature at all — that entire "CMS Service Blueprint" surface (backoffice controller, single-queue validator, MCP wrapper, backoffice TS) was removed once it became fully redundant with `Wayfinder.Umbraco`'s own "Blueprints" entry under Umbraco's built-in Settings section (Advanced group) — no custom top-level section, matching Umbraco's own Webhook management package's own placement convention. Core is purely tenancy/branding/mobile now; its own "Tenants" screen lives in the same place (Settings → Advanced), not a custom "Prism" section either. `UmbracoPrism.TestSite` is the reference consumer: it installs `Wayfinder.Umbraco` directly (its own `PackageReference`, not through Core) and owns its own small demo-queue implementation (`PublicVisitorQueue`, identity resolution, file upload/download controllers) the same way any other host would. This is a breaking change to the published `UmbracoPrism` package's feature set — a host that used the old CMS Service Blueprint feature must now install `Wayfinder.Umbraco` itself and own the equivalent host-side wiring (see `UmbracoPrism.TestSite` for the reference pattern).

Building this repo from source restores against nuget.org only (`NuGet.config`) — no token or secondary feed needed. The compiled service blueprint editor web component no longer has any local copy in this repo at all — `Wayfinder.Editor` and its own TS source now live entirely in the `Wayfinder` repo, consumed here purely as a transitive dependency of `Wayfinder.Umbraco`.

Solo developer project. Work directly on `main` for simple fixes; use feature branches + PRs for substantive code changes.

---

## Projects

| Project | Purpose |
|---|---|
| `UmbracoPrism.Core` | The publishable Umbraco package — multi-tenant OIDC, branding, mobile, controllers/middleware/services for those. No service-design opinion of its own at all; carries no dependency on `Wayfinder`/`Wayfinder.Umbraco`. |
| `UmbracoPrism.uSync` | uSync portability for Prism's own tenant configuration only (`PrismTenantHandler`/`PrismTenantSerializer`). Service blueprint uSync portability lives entirely in `Wayfinder.Umbraco` now (`ServiceBlueprintHandler`/`ServiceBlueprintSerializer`), with no Prism dependency. |
| `UmbracoPrism.Client` | TypeScript/Lit web components — backoffice extensions (tenant management, branding, mobile), mobile shell. No longer builds any service blueprint editor bundle — that moved entirely to the `Wayfinder` repo's own `Wayfinder.Editor.Client`. |
| `UmbracoPrism.MockBusinessApp` | Demo business API — hosts `IProcessManager`, loads service blueprint seed files, serves `/mockapp/` endpoints |
| `UmbracoPrism.TestSite` | Demo Umbraco site wired to Prism Core and MockBusinessApp. Also installs `Wayfinder.Umbraco` directly and owns its own small demo-queue implementation (`PublicVisitorQueue`, `PublicServiceRequestPageController`, file upload/download controllers) — the reference pattern for any host wanting a Wayfinder.Umbraco-backed service blueprint page. |
| `UmbracoPrism.AppHost` | .NET Aspire orchestrator for local dev (Keycloak + TestSite + MockBusinessApp) |
| `UmbracoPrism.KeycloakProxy` | YARP reverse proxy for Keycloak in Aspire |
| `UmbracoPrism.ServiceDefaults` | Shared Aspire service defaults |
| `UmbracoPrism.Core.Tests` | XUnit unit/integration test suite |

`Wayfinder`, `Wayfinder.Engine`/`.Api`/`.Mcp`, `Wayfinder.Editor`, and `Wayfinder.Umbraco` are no
longer part of this repo — see [`jonnymuir/Wayfinder`](https://github.com/jonnymuir/Wayfinder) and
[`jonnymuir/Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco) if you need
to work on the service blueprint engine itself. `UmbracoPrism.Core` has **zero** dependency on
any Wayfinder package — not even for generic queue plumbing; the old `AddPrismProcessManager()`/
`BusinessAppProcessManagerClient` (an HTTP proxy to a remote "Business App" treated as the
authoritative workflow engine) was removed outright once `UmbracoPrism.MockBusinessApp` narrowed
to a genuine downstream support system and Wayfinder.Umbraco itself became authoritative,
in-process, natively multi-queue (front-stage + backstage in one host, no separate business app
needed — see that repo's own `docs/guides/work-allocation.md`/`team-assignment.md`).
`UmbracoPrism.TestSite` is the *only* project in this repo with a `PackageReference` to any
Wayfinder package, for its own demo-queue implementation.

---

## Build and test commands

### .NET

```bash
# Build everything
dotnet build UmbracoPrism.sln

# Run unit tests
dotnet test src/UmbracoPrism.Core.Tests/

# Run unit tests (Release, full filter)
dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests

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
npm run test-storybook:ci

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

### Service Blueprint model

The canonical service blueprint contract is `ServiceBlueprint` (C#) / `AuthoredServiceBlueprint` (TypeScript). Key fields:

- **`queues`** — named work queues (e.g. `web-user`, `admin`). Each stage belongs to one queue via `queueKey`.
- **`stages`** — service blueprint stages, each owning their own `routes` array (replacing the old flat `transitions` array).
- **`gateways`** — first-class Split/Join gateway nodes. Stage routes must target a gateway; gateway routes may target stages or other gateways.
- **`initialStage`** — key of the starting stage.
- **`requestPolicy`** — `"single"` (one active service request per user, resumed on every visit), `"multiple"` (a new instance every visit), or `"prompt"` (an active instance triggers an `instance_picker` response instead of the form).

Stage routes must always point to a gateway, never directly to another stage. `ValidateGatewayRouting()` enforces this at save time.

**"Stage" vs "touchpoint":** a stage is the generic graph node — the thing that happens at one point in a blueprint, regardless of who or what is acting. "Touchpoint" is a narrower service-design term for a point of *customer contact*, so it doesn't fit every stage (a hypothetical future automated/system queue would have stages with no touchpoint at all). Queue `displayName`s may still say "touchpoints" where that's accurate — e.g. `web-user`/`business-user` queues are named "... touchpoints" in the demo seeds, because those queues genuinely are all customer- or staff-facing contact points — but the type name and field names are `Stage`/`stageKey` everywhere, not `Touchpoint`.

**Response states:** `"render"` (show this step), `"defer"` (wait), `"complete"`, `"error"`.

Persistence differs by authoring surface. `Wayfinder.Umbraco`'s own "Blueprints" entry under Settings (its `ServiceBlueprintAuthoringController`) is DB-backed — saves go through `UmbracoServiceBlueprintStore` to a real `wayfinderServiceBlueprint` table in the Umbraco content database, survive restarts, and are uSync-portable via `ServiceBlueprintHandler`/`ServiceBlueprintSerializer` — all of this carries no Prism dependency at all; `UmbracoPrism.Core` has no backoffice service-blueprint feature of its own anymore. `UmbracoPrism.TestSite`'s own demo queue (`PublicVisitorQueue`) adds only the single-queue constraint and tenant-scoping on top, enforced by `Wayfinder.Umbraco`'s own `SingleQueueStructuralValidator`, not by Prism. `MockBusinessApp`'s own demo/business blueprint authoring surface (used by the AI-ready MCP/REST toolkit against the reference app) is memory-only by design — POSTing writes to an in-memory store only, and a restart reloads from `service-blueprints/*.json`. Don't assume one behavior applies to the other.

### Queue model

Host apps decide what queues exist and who can access them:
- `TestSite` exposes `"web-user"` queue — applicant-facing stages.
- `MockBusinessApp` exposes `"admin"` queue — reviewer/admin stages.

The shared runtime does NOT enforce queue-level access control — that's the host's responsibility.

### Service Blueprint Editor (TypeScript)

No longer part of this repo at all — the editor's TS source (`wayfinder-service-blueprint-editor.ts`, `wayfinder-service-blueprint-graph.ts`, `wayfinder-service-blueprint-editor-shell.ts`, etc.) lives in the `Wayfinder` repo's own `Wayfinder.Editor.Client`, compiled into the published `Wayfinder.Editor` NuGet package. `Wayfinder.Umbraco`'s own "Blueprints" entry under Settings consumes that package directly; this repo has no local copy to work on. See [`jonnymuir/Wayfinder`](https://github.com/jonnymuir/Wayfinder) if you need to change the editor itself (canvas layout, lane bands, gateway nodes, route edges).

### Seed files

`src/UmbracoPrism.MockBusinessApp/service-blueprints/` — demo service blueprints:
- `payment-demo.json` — two-queue, Split+Join gateways, payment flow
- `planning.json` — single-queue, linear applicant flow
- `planning-notification.json` — planning notification variant
- `community-enquiry.json` — two-queue applicant/reviewer with approval loop
- `information-request.json` — two-queue, SLA-driven review flow
- `money-modeller.json` — fully declarative pension modeller: calculations block + live components (sliders, stat-group, chart, showWhen), recalculate self-loop, two-queue quote-request fan-out

### Declarative calculations & live stages (Money Modeller pattern)

Service Blueprints may carry a `calculations` block (tables + fields + series) — the
single source of any business maths. It is a total expression language (arithmetic,
comparisons, boolean logic, `if/min/max/clamp/abs/floor/round/pow/lookup`; no eval, no
loops, no side effects) with decimal semantics, evaluated by two conformant runtimes:

- C#: `Wayfinder/Services/Calculations` — authoritative. The generic engine
  evaluates the block on every render (typed scope built by `CalculationScopeBuilder`
  from the definition's own input components + defaults; hosts supply `source: "service"`
  inputs via the `ResolveServiceInputs` engine hook).
- TypeScript: `src/UmbracoPrism.Client/src/calculations/calculation-engine.ts` —
  indicative; the generic `prism-live-form` runtime re-evaluates the same definitions
  between POSTs.

The shared conformance suite is `Wayfinder/calculation-fixtures/calculation-golden.json`,
executed by `CalculationGoldenTests` (C#) and `npm run test:calc` (TS). Change either
evaluator only alongside those fixtures. Do NOT hand-write business maths in host services
or client components — put it in the definition's `calculations` block (or declare a
`service` field when it lives in an external system of record).

The UI is equally declarative: stages compose generic components only. Calculated fields
bind by name (`stat-group` items, `summary-list` rows, `chart` components bound to a
series); any component may declare `showWhen` (an expression) for live visibility; input
components declare `default` values that seed both the form and the calculation scope, or
`defaultFrom` to suggest a *calculation-scope* value (a calculated field, or a `source:
"service"` field — dotted paths like `member.tier` resolve too) instead of a static literal,
while a saved value is always absent — a genuine overridable default, never a lock, and it
falls back to `default` when the name doesn't resolve (e.g. an anonymous visitor with no
member data); calculated fields may declare `format` ("gbp"). The server renders everything (works
without JavaScript; the Recalculate self-loop re-renders authoritatively) and
`prism-live-form` (`src/UmbracoPrism.Client/src/live-form/`) upgrades the page in place —
it contains no domain knowledge and no layout. There are no bespoke per-blueprint client
components.

### AI-ready service blueprint authoring

Service blueprint authoring is exposed to AI agents (Claude Code or any MCP client) the same way
the editor is exposed to humans: as a toolkit a host app wires into its own pipeline, not
as AI built into Prism itself. `ServiceBlueprintAuthoringService`/`IServiceBlueprintSourceStore`
(`Wayfinder.Engine`) are the reusable core; `Wayfinder.Engine.Api`
(`MapServiceBlueprintAuthoringApi()`) and `Wayfinder.Engine.Mcp`
(`MapServiceBlueprintAuthoringMcp()`) map the same list/read/validate/save/simulate
operations as REST and MCP-over-HTTP respectively — both call the service in-process, so
a save reaches a host's live engine immediately. `MockBusinessApp` is the reference
implementation (`Program.cs`); see the
[AI-Ready Service Blueprint Authoring guide](docs/guides/ai-service-blueprint-authoring.md) for the full
integrator recipe. MCP hosting is HTTP-only by design — a stdio MCP server would be a
separate spawned process with no access to a host's live state, and Aspire can't manage
a stdio server as a background resource (nothing would drive its stdin).

### Authentication

OIDC via Keycloak. Stateless token handling; per-tenant JWKS validation; nonce hard-fail on mismatch. `IProcessManager` takes `ActorProfile` (derived from the authenticated user's claims) for queue-aware routing.

---

## Key conventions

### Testing

- **Behavioural contracts**: tests assert what the user sees, not implementation details.
- **Semantic selectors**: `getByRole`, `getByLabel`, `getByText`, `aria-*`, `data-prism-*` attributes. Never CSS classes or web component tag names.
- **Date inputs**: target sub-fields by generated IDs — `{fieldKey}-day`, `{fieldKey}-month`, `{fieldKey}-year`.
- **Error assertions**: always check both `[role="alert"]` error summary AND field-level errors.
- C# tests: XUnit + Moq + FluentAssertions. No database mocks — integration tests use a real test host.
- Playwright configs: `playwright.config.ts` (Storybook tests) and `playwright.localhost-auth.config.ts` (live Aspire stack tests, run separately).

### Branch policy

Feature branches + PRs for substantive changes. Branch naming: `{type}/{issue-number}-{kebab-slug}` or descriptive. Direct commits to `main` for trivial fixes only.

### Commit conventions

Always use [Conventional Commits](https://www.conventionalcommits.org/) format. The `/release` skill reads these to infer the semver bump automatically — so the signal must be accurate:

| Prefix | Semver impact | Use for |
|---|---|---|
| `feat:` | **minor** | New user-facing capability |
| `feat!:` or body contains `BREAKING CHANGE:` | **major** | Anything that breaks existing integrations or APIs |
| `fix:` | patch | Bug fixes |
| `perf:` | patch | Performance improvements |
| `refactor:` | patch | Internal restructuring, no behaviour change |
| `test:` | patch | Test additions/changes only |
| `chore:` | patch | Tooling, build, deps — no user impact |
| `docs:` | patch | Documentation only |

When a commit introduces a breaking change, add a `BREAKING CHANGE: <description>` line in the commit body explaining what breaks and how to migrate. This is what the release skill reads to trigger a major bump.

### Code style

- No speculative abstractions — solve the problem at hand.
- No comments unless the *why* is genuinely non-obvious.
- C#: idiomatic .NET 10 / Umbraco v17 patterns. Use `IContentTypeService`, `IDataTypeService` etc. from DI — don't re-register.
- TypeScript: Lit web components. Use `data-prism-*` attributes to expose semantic hooks for Playwright.

---

## Demo credentials (local)

| What | Username | Password |
|---|---|---|
| TestSite (Keycloak SSO) | `demo@prism.local` | `password` |
| Umbraco backoffice | `admin@prism.local` | `PrismLocal!12345` |
| Keycloak admin | `admin` | `admin` |
