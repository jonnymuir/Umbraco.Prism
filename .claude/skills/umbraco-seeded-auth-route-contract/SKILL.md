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
- Make the harness wait on that machine-readable contract before it ever drives authored child-page journeys; the home-page marker is only a smoke signal that Razor is serving, not the authoritative route-readiness signal.
- Normalize published URLs before asserting them as a contract; Umbraco may emit trailing slashes even when the intended route contract is `/dashboard`, `/get-in-touch`, or `/my-workflows`.
- Treat a transient published URL of `/` for non-home seeded pages as "route not converged yet", not as a trustworthy navigation target. Fall back to the authored contract route (`/dashboard`, `/get-in-touch`, `/my-workflows`) so cold-start auth CTAs and nav links do not bounce members back to Home.
- In this repo, that transient `/` happens because Razor resolves links from `ContentAtRoot()` + `content.Url()` while startup seeders are still publishing into a fresh runtime database; a node can be visible in the published tree before Umbraco's hierarchical route cache has finished computing its final child path.
- Classify that `/` collapse as a dev-startup artifact of seeded fresh-runtime bootstrapping, not as the normal steady-state Umbraco behavior other features should expect once the route contract is ready.
- Keep that convergence quirk inside the readiness layer, not the behavioural assertions: once the contract probe reports ready, browser tests should demand the normal authored URLs and auth challenges, never "either `/` or the real child route".
- If a clean Aspire run uses an isolated runtime root, verify the seeded contract against that isolated database rather than the developer's standalone local Umbraco DB.
- Document the route contract anywhere the localhost Playwright/Aspire flow is described so QA and docs use the same assumptions as the seeders.
- In browser tests, prefer entering authored member pages by clicking the content-resolved CTA or nav link that points there, while separately asserting the link `href` matches the seeded contract (for example, `Go to Dashboard` → `/dashboard`).
- When debugging redirect chains, separate the protected-page challenge from the OIDC callback redirect target: in this repo `/dashboard` should 302 to `/auth/login?ReturnUrl=%2Fdashboard`, while `/signin-oidc` redirects to whatever `AuthenticationProperties.RedirectUri` was set to. If login was initiated from the home-page `Sign In` CTA, that fallback target is `/`, so Playwright may show a 302 to `/` even though the dashboard route itself is correct.
- On public TestSite pages that funnel members straight into protected content, generate the login CTA from the same content-resolved member URL (`/auth/login?returnUrl={dashboardUrl}`) so the first successful sign-in lands on the authored destination instead of bouncing back to `/`.
- Do not persist that one-off OIDC `RedirectUri` in the long-lived `PrismMemberCookie`; use it for the immediate `/signin-oidc` response, then clear it before writing the cookie so later protected requests like `/my-workflows` are not hijacked back to the previous login target.

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
