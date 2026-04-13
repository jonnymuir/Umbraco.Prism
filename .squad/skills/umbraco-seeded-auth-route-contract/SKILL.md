---
name: "umbraco-seeded-auth-route-contract"
description: "Keep localhost auth/workflow demo routes deterministic on a clean Umbraco database"
domain: "umbraco"
confidence: "high"
source: "observed"
---

## Context

Use this skill when the TestSite's real auth/workflow flow depends on seeded Umbraco content and a clean database must boot into a known content/navigation shape.

## Patterns

- Define a single seed contract for the authenticated journey (`Home`, `Dashboard`, `Get in Touch`, `My Workflows`, `Settings` mobile nav) and keep the aliases, names, and expected routes in one shared place.
- Make Development seeders idempotent and reparative: create missing nodes, restore critical workflow properties like `workflowKey`, and publish the nodes after repair.
- Resolve Razor navigation from published content discovery (`ContentAtRoot()` + `DescendantsOrSelf()`) instead of relying on root-node ordering.
- When real-app readiness matters, expose a machine-readable endpoint (for this repo: `/api/prism/downstream-demo/seed-contract-ready`) that verifies the published route contract and expected auth challenge path instead of scraping hero copy.
- Add a stable page marker on the rendered home page (`data-prism-home-ready="true"` in this repo) so the readiness gate proves the real Razor route is serving, not just that ASP.NET is listening on a port.
- Normalize published URLs before asserting them as a contract; Umbraco may emit trailing slashes even when the intended route contract is `/dashboard`, `/get-in-touch`, or `/my-workflows`.
- If a clean Aspire run uses an isolated runtime root, verify the seeded contract against that isolated database rather than the developer's standalone local Umbraco DB.
- Document the route contract anywhere the localhost Playwright/Aspire flow is described so QA and docs use the same assumptions as the seeders.

## Examples

- `src/UmbracoPrism.TestSite/TestSiteSeedContract.cs`
- `src/UmbracoPrism.TestSite/WorkflowPageSeeder.cs`
- `src/UmbracoPrism.TestSite/DemoMobileNavSeeder.cs`
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`
- `src/UmbracoPrism.TestSite/Views/Shared/Master.cshtml`
- `src/UmbracoPrism.TestSite/Views/HomePage.cshtml`
- `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`
- `src/UmbracoPrism.Client/tests/support/live-app-host.ts`

## Anti-Patterns

- Assuming the first root node with a matching alias is always the right navigation target.
- Seeding only on an empty tree when other startup seeders may populate content first.
- Letting docs describe routes that are not actually enforced by the seeders.
