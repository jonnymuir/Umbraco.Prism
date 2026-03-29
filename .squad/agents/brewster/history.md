# Brewster — History

## Project Context

**Project:** Umbraco.Prism — a multi-tenant web and mobile authentication package for Umbraco v17+. It provides:
- Automatic tenant resolution from hostname
- Entra ID (Azure CIAM) OIDC authentication via `PrismMemberCookie` scheme
- Tenant-scoped branding via CSS variables
- Biometric authentication for Capacitor mobile apps
- A MockBackOffice project demonstrating downstream credential propagation

**Stack:** .NET 10, Umbraco v17.2.2, SQLite, Capacitor/Ionic mobile

**User:** Jonny Muir

## Key Architecture Facts

- Auth scheme: `PrismMemberCookie` (Entra ID OIDC, custom). NOT Umbraco member groups.
- `IPrismContext.CurrentTenant` resolves the current tenant from the request hostname.
- `PrismContext.GetAuthorizationHeaderAsync()` returns a Bearer token for downstream API calls.
- MockBackOffice runs on `localhost:5163` — validates the Prism Bearer token at `/api/backoffice/me`.
- Test site auto-generated Umbraco models are in `src/UmbracoPrism.TestSite/umbraco/models/` — do not hand-edit.
- The old `HomePage.cshtml` had a working `CallBackOfficeAsync()` demo that was removed during a test site overhaul (commit `40834e8`) — it needs to be restored.
- A plain MVC `MemberDashboardController` was introduced in `40834e8` — this is NOT Umbraco-idiomatic and needs to be replaced with a proper `RenderController` route-hijacking approach.

## Learnings

- **`[ModelType]` attribute does not exist in Umbraco v17.** Route hijacking is purely by naming convention: a controller named `{DocumentTypeAlias}Controller` inheriting `RenderController` is auto-discovered. No attribute needed.
- **Hand-authored `PublishedContentModel` subclasses** can be placed in `src/UmbracoPrism.TestSite/umbraco/models/` alongside the auto-generated ones. Use the same `Umbraco.Cms.Web.Common.PublishedModels` namespace. Skip the `new` keyword on constants that don't hide a base member.
- **`CallBackOfficeAsync()` downstream demo** belongs in the dashboard view (not the homepage), since the dashboard is the authenticated area. Trigger via `?callApi=true` query string; show MockBackOffice start hint on network failure.
- **`@inject IPrismContext PrismContext`** is wired in `_ViewImports.cshtml` — available in all TestSite views without explicit declaration in each view.

---

## Session: 2026-03-29 — MemberDashboard Rework

**Status:** Completed  
**Test outcome:** 0 errors, 165 tests passed.

**Completed work:**
- Replaced `MemberDashboardController` (plain MVC) with `RenderController` route hijacking.
- Hand-authored `MemberDashboard.cs` content model in `umbraco/models/`.
- Updated `Index.cshtml` to use `@inherits UmbracoViewPage<MemberDashboard>`.
- Restored `CallBackOfficeAsync()` downstream API call demo, triggered by `?callApi=true`.

**Outcome:** Test site is now Umbraco-idiomatic. MemberDashboard is routable as a native document type view. Auth and downstream demo flows are functional.
