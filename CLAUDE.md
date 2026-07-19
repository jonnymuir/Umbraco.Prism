# Umbraco Prism — Project Guide for Claude Code

## What this is

Umbraco Prism is an Umbraco v17 package that adds multi-tenant OIDC authentication, runtime branding, a GDS-style workflow engine with a visual editor, and native mobile app generation. The repo is a mono-repo containing the package itself plus a full demo stack (TestSite, MockBusinessApp, Keycloak) orchestrated via .NET Aspire.

Solo developer project. Work directly on `main` for simple fixes; use feature branches + PRs for substantive code changes.

---

## Projects

| Project | Purpose |
|---|---|
| `UmbracoPrism.Core` | The publishable Umbraco package — controllers, middleware, auth, services, tag helpers, views |
| `UmbracoPrism.Shared` | Shared models used by both Core and demo apps — `WorkflowDefinitionFile`, `WorkflowResponseEnvelope`, etc. |
| `UmbracoPrism.WorkflowRuntime` | Workflow state-machine engine — queue routing, gateway evaluation, instance persistence |
| `UmbracoPrism.WorkflowRuntime.Api` | REST toolkit (`MapPrismWorkflowAuthoringApi()`) exposing workflow authoring — list/read/validate/save/simulate — over HTTP for any host |
| `UmbracoPrism.WorkflowRuntime.Mcp` | MCP-over-HTTP toolkit (`MapPrismWorkflowAuthoringMcp()`) — the same authoring surface as MCP tools for AI agents |
| `UmbracoPrism.WorkflowEditor` | Razor Class Library hosting the compiled workflow editor web component as a static web asset |
| `UmbracoPrism.Client` | TypeScript/Lit web components — workflow editor, backoffice extensions, mobile shell |
| `UmbracoPrism.MockBusinessApp` | Demo business API — hosts `IWorkflowRuntimeEngine`, loads workflow seed files, serves `/mockapp/` endpoints |
| `UmbracoPrism.TestSite` | Demo Umbraco site wired to Prism Core and MockBusinessApp |
| `UmbracoPrism.AppHost` | .NET Aspire orchestrator for local dev (Keycloak + TestSite + MockBusinessApp) |
| `UmbracoPrism.KeycloakProxy` | YARP reverse proxy for Keycloak in Aspire |
| `UmbracoPrism.ServiceDefaults` | Shared Aspire service defaults |
| `UmbracoPrism.Core.Tests` | XUnit unit/integration test suite |

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

# Build (TypeScript check + Vite bundle for both main and workflow editor)
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

### Workflow model

The canonical workflow contract is `WorkflowDefinitionFile` (C#) / `AuthoredWorkflow` (TypeScript). Key fields:

- **`queues`** — named work queues (e.g. `web-user`, `admin`). Each stage belongs to one queue via `queueName`.
- **`states`** — workflow stages, each owning their own `routes` array (replacing the old flat `transitions` array).
- **`gateways`** — first-class Split/Join gateway nodes. State routes must target a gateway; gateway routes may target states or other gateways.
- **`initialState`** — key of the starting state.
- **`instancePolicy`** — `"single"` (one active instance per user) or `"multiple"`.

State routes must always point to a gateway, never directly to another state. `ValidateGatewayRouting()` enforces this at save time.

**Response states:** `"render"` (show this step), `"defer"` (wait), `"complete"`, `"error"`.

Workflow saves are **memory-only** — POSTing to the editor writes to the in-memory store only; a restart reloads from seed files. This is intentional during the current demo/dev phase.

### Queue model

Host apps decide what queues exist and who can access them:
- `TestSite` exposes `"web-user"` queue — applicant-facing stages.
- `MockBusinessApp` exposes `"admin"` queue — reviewer/admin stages.

The shared runtime does NOT enforce queue-level access control — that's the host's responsibility.

### Workflow editor (TypeScript)

Web components in `src/UmbracoPrism.Client/src/workflow-editor/`:
- `prism-workflow-editor.ts` — main editor component
- `prism-workflow-graph.ts` — canvas with lane bands, stage nodes, gateway nodes, route edges. Uses longest-path Kahn's algorithm for Y-rank; backward edges (Join loop-backs) are detected and removed from the ranking graph before layout.
- `prism-workflow-editor-shell.ts` — standalone shell for embedding the editor
- `types.ts` — canonical TypeScript types; includes compatibility getters for older `lane`/`transition` naming

The graph layout uses `data-prism-lane` on stage button elements (stages are absolutely-positioned siblings of lane bands, not DOM children).

### Seed files

`src/UmbracoPrism.MockBusinessApp/workflow-seeds/` — demo workflows:
- `payment-demo.json` — two-queue, Split+Join gateways, payment flow
- `planning.json` — single-queue, linear applicant flow
- `planning-notification.json` — planning notification variant
- `community-enquiry.json` — two-queue applicant/reviewer with approval loop
- `information-request.json` — two-queue, SLA-driven review flow
- `money-modeller.json` — fully declarative pension modeller: calculations block + live components (sliders, stat-group, chart, showWhen), recalculate self-loop, two-queue quote-request fan-out

### Declarative calculations & live stages (Money Modeller pattern)

Workflow definitions may carry a `calculations` block (tables + fields + series) — the
single source of any business maths. It is a total expression language (arithmetic,
comparisons, boolean logic, `if/min/max/clamp/abs/floor/round/pow/lookup`; no eval, no
loops, no side effects) with decimal semantics, evaluated by two conformant runtimes:

- C#: `UmbracoPrism.Shared/Services/Calculations` — authoritative. The generic engine
  evaluates the block on every render (typed scope built by `CalculationScopeBuilder`
  from the definition's own input components + defaults; hosts supply `source: "service"`
  inputs via the `ResolveServiceInputs` engine hook).
- TypeScript: `src/UmbracoPrism.Client/src/calculations/calculation-engine.ts` —
  indicative; the generic `prism-live-form` runtime re-evaluates the same definitions
  between POSTs.

The shared conformance suite is `UmbracoPrism.Shared/calculation-fixtures/calculation-golden.json`,
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
it contains no domain knowledge and no layout. There are no bespoke per-workflow client
components.

### AI-ready workflow authoring

Workflow authoring is exposed to AI agents (Claude Code or any MCP client) the same way
the editor is exposed to humans: as a toolkit a host app wires into its own pipeline, not
as AI built into Prism itself. `WorkflowAuthoringService`/`IWorkflowSourceStore`
(`UmbracoPrism.WorkflowRuntime`) are the reusable core; `UmbracoPrism.WorkflowRuntime.Api`
(`MapPrismWorkflowAuthoringApi()`) and `UmbracoPrism.WorkflowRuntime.Mcp`
(`MapPrismWorkflowAuthoringMcp()`) map the same list/read/validate/save/simulate
operations as REST and MCP-over-HTTP respectively — both call the service in-process, so
a save reaches a host's live engine immediately. `MockBusinessApp` is the reference
implementation (`Program.cs`); see the
[AI-Ready Workflow Authoring guide](docs/guides/ai-workflow-authoring.md) for the full
integrator recipe. MCP hosting is HTTP-only by design — a stdio MCP server would be a
separate spawned process with no access to a host's live state, and Aspire can't manage
a stdio server as a background resource (nothing would drive its stdin).

### Authentication

OIDC via Keycloak. Stateless token handling; per-tenant JWKS validation; nonce hard-fail on mismatch. `IWorkflowRuntimeEngine` takes `WorkflowAccessProfile` (derived from the authenticated user's claims) for queue-aware routing.

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
