# Decision: Register IWorkflowContentSanitizer in MockBusinessApp

**Date:** 2025-07-11  
**Author:** Blathers (Backend Dev)  
**Branch:** fix/ci-green  

## Context

The `localhost-auth-playwright` CI lane was failing with all Playwright specs timing out
at the 5-minute readiness deadline. Logs showed MockBusinessApp (`https://localhost:7245/api/backoffice/me`)
accepting TCP connections (DCP held the port) but never returning an HTTP response — every probe
timed out after 5 000 ms consistently across all three spec-file runs.

## Root Cause

SEC-003 added `IWorkflowContentSanitizer` as a constructor dependency to `BusinessAppWorkflowEngine`
(which runs in MockBusinessApp). The registration for this interface lives in
`UmbracoPrism.Core/Extensions/WorkflowBuilderExtensions.cs`, which is only called by TestSite
through the Umbraco pipeline (`AddPrismWorkflowEngine()`).

MockBusinessApp only references `UmbracoPrism.Shared` and registers `BusinessAppWorkflowEngine`
directly — it never calls `AddPrismWorkflowEngine()`. This left `IWorkflowContentSanitizer`
unregistered in MockBusinessApp's DI container. At startup, the generic host tried to instantiate
`WorkflowTuiService → BusinessAppWorkflowEngine → IWorkflowContentSanitizer`, threw
`InvalidOperationException`, and the app crashed. Aspire DCP kept the port bound (accepting TCP),
but with no live process behind it all HTTP requests hung until the 5 000 ms probe timeout.

## Decision

Register a `file`-scoped `PassthroughSanitizer` directly in MockBusinessApp's `Program.cs`.
MockBusinessApp serves controlled developer-authored seed content (no user-supplied HTML), so a
passthrough implementation is appropriate and carries no XSS risk.

The real GDS allowlist sanitizer (`WorkflowContentSanitizer`, Ganss.Xss-backed) continues to be
registered exclusively in TestSite/Core. MockBusinessApp remains a test-double app and does not
need the full security policy.

## Alternatives Considered

- **Add `NoOpWorkflowContentSanitizer` to Shared** — rejected; the interface is already in Shared
  and adding the no-op there would encourage misuse. Better to keep it explicit per-app.
- **Reference `UmbracoPrism.Core` from MockBusinessApp** — rejected; introduces a layering violation
  (mock app referencing the full Umbraco integration layer for a utility class).

## Impact

- MockBusinessApp now starts successfully in CI and responds to the readiness probe with HTTP 401
  (unauthenticated), unblocking all three Playwright spec files.
- No behaviour change for TestSite; the real sanitizer registration is untouched.
- Build succeeds, 601 Core unit tests pass.

---

# Finding: WorkflowPageSeeder Race Condition (CI run 25171755261)

**Date:** 2026-04-30  
**Author:** Blathers (Backend Dev)  
**Branch:** fix/ci-green  

## Context

After the PassthroughSanitizer fix, the host no longer crashes. CI run 25171755261 (PR #38) revealed
a deeper seed-time bug: the TestSite seed-contract endpoint returned HTTP 503 with `home.matchesExpected: true`
and `dashboard.matchesExpected: true` but all 5 workflow pages (`workflowPage`, `workflowHub`,
`planningWorkflowPage`, `paymentDemoPage`, `informationRequestPage`) as `published: false, url: "/"`.

The `localhost-auth-playwright` lane also showed:
- `/my-workflows` → HTTP 404 (no published content at URL)
- `/apply-for-planning-permission` → HTTP 500 `No UmbracoRouteValues feature was found in the
  HttpContext` (content saved/draft but not published, causing Umbraco's `UmbracoPageController` to
  crash on a missing route-values feature it expects to always be set for published content)

## Root Cause

Umbraco dispatches `INotificationAsyncHandler<UmbracoApplicationStartedNotification>` handlers
**concurrently** (Task.WhenAll). Both `PrismContentTypeSeeder` (Core) and `WorkflowPageSeeder`
(TestSite) subscribe to the same notification and start simultaneously.

`PrismContentTypeSeeder.HandleAsync` creates content types in order:
1. `homePage` — created first
2. `memberDashboard` — created second
3. `workflowDemoPage` — created third
4. `workflowPage` — created fourth
5. `workflowHub` — created fifth

`WorkflowPageSeeder` starts at the same moment. By the time it reaches `EnsureCommunityEnquiryPage()`,
`workflowPage` doesn't exist yet → `contentTypeService.Get("workflowPage")` returns null → early return
→ no workflow pages seeded. Home and dashboard pass because their content types are created first by
`PrismContentTypeSeeder` and are already available by the time `WorkflowPageSeeder` checks them.

This was **pre-existing** and masked by the DI crash. It's a race condition on a fresh CI database;
on local dev the types often exist from a previous run (SQLite DB persists).

## Decision

Make `WorkflowPageSeeder.HandleAsync` properly async and add a `WaitForContentTypeAsync` helper that
polls `contentTypeService.Get(alias)` every 500 ms for up to 90 seconds before proceeding to seed
workflow-page content. This is:

- Defensive against the concurrent dispatch behaviour regardless of Umbraco version
- Idempotent — if types are already present the first poll returns immediately (zero extra latency)
- Safe — 90 s timeout is generous for CI; production/staging never runs this seeder (dev-only guard)

## Impact

- All 5 workflow pages now get published on fresh CI databases even under concurrent handler dispatch.
- `workflowPage` doc type availability is polled rather than assumed.
- Build succeeds, 601 Core unit tests pass.
