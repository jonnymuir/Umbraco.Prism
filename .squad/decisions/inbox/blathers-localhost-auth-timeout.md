# Decision: Localhost auth route redirects should be issued inside Umbraco route-hijack controllers

**Date:** 2026-05-21T21:54:07.868+01:00  
**Author:** Blathers  
**Status:** Proposed  

For protected Umbraco route-hijack pages in the TestSite slice, handle anonymous-member redirects inside the controller `Index()` path instead of relying on `[Authorize]` on the route-hijack controller type.

## Why

- The localhost-auth Playwright lane was failing before login because anonymous readiness probes against `/dashboard`, `/my-workflows`, and workflow pages were intermittently hitting `No UmbracoRouteValues` 500s or timing out instead of receiving the expected `/auth/login?ReturnUrl=...` redirect.
- The unstable seam sits at the intersection of ASP.NET auth filters and Umbraco route-hijacking. Issuing the redirect from the controller after the Umbraco route has resolved keeps the behaviour deterministic for both browser navigation and CI warmup probes.

## Consequences

1. `MemberDashboardController`, `WorkflowHubController`, and `PrismWorkflowPageController` should build the login redirect from the current request path and query string.
2. Route-level auth regression checks should assert the concrete 302 `Location` header on anonymous requests, not just that the route is "protected".
3. Avoid reintroducing `[Authorize]` directly on these route-hijack controllers unless the Umbraco route-values timing issue is explicitly re-tested.
