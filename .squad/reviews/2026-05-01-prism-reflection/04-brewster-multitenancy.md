# Multi-tenancy & editor review — Brewster

_Reviewed: 2026-05-01T08:57:29+01:00 · Model: claude-sonnet-4.5_

---

## Verdict

Prism's multi-tenancy is architecturally sound and genuinely first-class: host-based resolution, per-request scoping, immediate activation without restarts, and a clean CSS-variable branding pipeline. For developers, the onboarding path is coherent and well-documented. The honest friction lives at the seam between Umbraco's single shared content tree and the promise of per-tenant content isolation — Prism delivers workflow and branding isolation but explicitly punts on content isolation ("not covered in this walkthrough," `docs/walkthroughs/creating-a-tenant.md` line 181). For content editors in the Umbraco backoffice, Prism installs invisibly and adds real value, but the backoffice surface — a custom Settings section — is thin, and the document-type model exposes none of the tenant concept to the editor's day-to-day authoring experience. Editors author in a single undifferentiated content tree; there is no in-tree signal that a page belongs to a tenant.

---

## The new-tenant onboarding journey

**Developer's path today (realistic assessment):**

1. Stand up Keycloak or Entra, register a realm/client.
2. Navigate to **Settings → Prism Dashboard** in the Umbraco backoffice.
3. Click **Add tenant**, fill hostname, OIDC authority, client ID.
4. Point Key Vault secret reference at the client secret (`OidcClientSecretProvider = "azure-key-vault"`, `OidcClientSecretReference = "My-Tenant-Secret"` — `src/UmbracoPrism.Core/Controllers/TenantManagementController.cs` line 369).
5. Add `tenant.hostname` to `/etc/hosts` or DNS.
6. Verify with incognito window.

That's a remarkably short path. The walkthrough (`docs/walkthroughs/creating-a-tenant.md`) is clear, honest about local dev DNS friction, and executable as a Playwright spec. The 30-minute runtime cache (`src/UmbracoPrism.Core/Services/TenantService.cs` line 92) means a mis-typed hostname persists for half an hour — no feedback loop in the backoffice.

**What the content team gets out of the box:** Branding CSS variables applied to every HTML response (`src/UmbracoPrism.Core/Middleware/PrismBrandingMiddleware.cs`), a themed login flow, and workflow isolation by `{tenantId}:{userId}:{workflowKey}`. That is genuinely useful from day one.

**Where they hit friction:**

- No content-tree separation. All tenants see and publish the same Umbraco content nodes. A content editor for Tenant A can accidentally publish a page that appears on Tenant B's domain with no warning. The walkthrough acknowledges this candidly but offers no mitigation path.
- The `MemberDashboardController` hardcodes `/auth/login?returnUrl=/dashboard` (`src/UmbracoPrism.Core/Controllers/MemberDashboardController.cs` line 42) — bypassing `CurrentTemplate()` — meaning the fallback URL is not tenant-hostname-relative. On a second tenant this redirect sends the user to the same `/dashboard` path but the auth challenge may resolve to the wrong tenant's OIDC authority depending on host header state.
- `homePage.cshtml` inherits `UmbracoViewPage` (untyped, line 2) rather than the strongly-typed `UmbracoViewPage<HomePage>` that Brewster's charter requires. Inline `@Html.Raw(imageryCss)` (`src/UmbracoPrism.TestSite/Views/homePage.cshtml` line 41) is a CSP violation logged but not yet patched (SEC-PT2-007/008 open).

---

## The editor's day-to-day (with Prism installed vs vanilla Umbraco)

**With Prism:**

- Backoffice login, content tree, document types, and publishing workflow are 100% vanilla Umbraco. No friction introduced.
- One extra section appears: **Settings → Prism Dashboard**. A non-developer editor landing there will encounter: tenant hostnames, OIDC authority URLs, Key Vault secret references, and a CSS-variable branding editor. The branding editor is genuinely excellent — colour pickers, font selectors, live labels from `@prism` annotations (`docs/branding-design-system.md`). The OIDC fields are completely opaque to a non-technical editor.
- The dashboard reads `GET /umbraco/api/prism/tenants` (Prism Management API), which is correctly gated behind `[Authorize(Policy = "PrismAdmins")]` (`src/UmbracoPrism.Core/Controllers/TenantManagementController.cs` line 20). An editor without the PrismAdmins group will not see tenant management at all — but they also get no explanation of why the section is empty.
- Zero impact on the content creation flow: no custom property editors, no tenant selectors on nodes, no tenant-scoped media library. The editor authors for all tenants simultaneously without knowing it.

**Net verdict:** Prism makes the backoffice neither simpler nor more complex for pure content editors. It is invisible — which is its best quality and also its biggest limitation (see isolation honesty audit below).

---

## Tenant isolation honesty audit

**What Prism genuinely isolates (Rams #6: Honest):**

| Surface | Isolated? | Evidence |
|---|---|---|
| Authentication | ✅ | Per-tenant OIDC authority; JWT validated against per-tenant JWKS (`src/UmbracoPrism.Core/Services/TenantService.cs` line 33) |
| Branding (web) | ✅ | CSS declarations injected per-tenant by `PrismBrandingMiddleware` |
| Branding (mobile) | ✅ | `MobileBrandingOverrides` separate column; Capacitor bundle generated per-tenant |
| Workflow instances | ✅ | Instance key includes `tenantId` (documented in walkthrough line 177) |
| Session cookies | ✅ | Cookies scoped to request host; `SameSite=Lax` (`PrismComposer.cs` line 128) |
| Email notifications | ⚠️ | `PrismNotificationService` — not audited here; branding token availability unconfirmed |

**What Prism does NOT isolate (and the docs admit it):**

- **Content tree:** Every Umbraco node is visible from every tenant. `creating-a-tenant.md` line 181: "Umbraco serves the same content tree to all tenants." This is the single biggest honesty gap. The product promises "its own … user base" but a user who authenticates on Tenant A can navigate content authored for Tenant B if they know the URL, because the content is not gated by tenant in the Umbraco pipeline.
- **Media library:** Shared across all tenants; tenant A's editors can see tenant B's uploaded images.
- **Umbraco members:** Prism members are Entra/Keycloak identities — not Umbraco members — so Umbraco member groups provide no isolation. This is architecturally consistent but undocumented for operators.
- **Cache timing window:** `TenantService` caches for 30 minutes with no per-tenant TTL control. A deleted tenant continues to resolve (from cache) for up to 30 minutes — this is not documented anywhere visible to operators.

---

## Per-tenant branding flow (does it reach all surfaces — web, email, mobile?)

**Web:** ✅ Full. `PrismBrandingMiddleware` buffers HTML responses and injects `<style>` blocks with CSS variable overrides. Served immediately on every request, no deploy needed. Backed by `BrandingCssDeclarations` pre-built at cache-load time (`src/UmbracoPrism.Core/Services/TenantService.cs` line 81).

**Mobile app (Capacitor):** ✅ Functional. `MobileBrandingOverrides` is a separate column (`src/UmbracoPrism.Core/Persistence/PrismTenantSchema.cs` line 66); `TenantManagementController.ProduceMobileBundle` bakes overrides into the downloadable Capacitor zip (`src/UmbracoPrism.Core/Controllers/TenantManagementController.cs` line 187). The mobile branding editor tab in the backoffice is the correct access point.

**Email notifications:** ⚠️ Unclear. `PrismNotificationService` (`src/UmbracoPrism.Core/Services/PrismNotificationService.cs`) is present but not audited in this pass. The service is scoped per request (`PrismComposer.cs` line 48), so it has access to `IPrismContext.CurrentTenant`. Whether it injects tenant branding into outbound email templates is unverified and undocumented. This is a gap in the branding story for any operator expecting end-to-end branded communication.

**Push notifications:** ⚠️ `PrismVinylNotificationController` exists. No evidence of per-tenant push payload customisation reviewed.

The CSS-variable-to-backoffice-form pipeline is one of Prism's most elegant features and is genuinely well-executed. The gap is that "branding" in the backoffice currently means colours/fonts/imagery — not email templates, notification payloads, or subject lines. Operators may reasonably expect broader reach.

---

## Rams scorecard (10 principles)

| # | Principle | Score | Finding |
|---|---|---|---|
| 1 | **Innovative** | ✅ | CSS-annotation-driven tenant editor is genuinely novel; backoffice-managed multi-tenancy without code changes is a strong differentiator |
| 2 | **Useful** | ✅ | Workflow isolation, per-tenant OIDC, and live branding solve real platform problems |
| 3 | **Aesthetic** | ⚠️ | The branding editor is clean; the OIDC/secret-provider fields in the tenant form are developer-facing and aesthetically hostile to non-technical editors |
| 4 | **Understandable** | ⚠️ | Developer onboarding is clear; editor mental model is weak — no in-tree tenant signal means editors cannot understand what they are publishing to |
| 5 | **Unobtrusive** | ✅ | Prism is invisible to content editors who don't need tenant management; the middleware pipeline adds zero perceptible latency on cache hits |
| 6 | **Honest** | ⚠️ | Content isolation is promised in the product overview ("isolated context … its own … user base") but is not enforced by the Umbraco content tree. Walkthrough is honest; the product pitch is not |
| 7 | **Long-lasting** | ✅ | Umbraco Management API, Lit/UUI web components, EF-free NPoco persistence — all v17-idiomatic and forward-compatible |
| 8 | **Thorough** | ⚠️ | DataProtection keys on-disk without encryption-at-rest; 30-minute deleted-tenant cache gap; email branding unresolved |
| 9 | **Environmentally friendly** | ✅ | Single shared deployment for N tenants; no per-tenant infra provisioning |
| 10 | **As little design as possible** | ⚠️ | `PrismTenant` carries both Entra-specific fields (`EntraTenantId`, `EntraClientId`, `SecretKeyName`) and generic OIDC fields — two overlapping auth models in one model increase cognitive load for any operator reading the tenant record |

---

## Three improvements (prioritised, with file paths)

### 1. ❗ Content isolation — make the boundary visible to editors

**Priority:** High. The single most dishonest gap in the current system.

**What:** Add an optional `TenantTag` property to the Umbraco content tree (a text property on the root document type, or a custom property editor). `PrismTenantMiddleware` already sets `IPrismContext.CurrentTenant`; a content filter registered via `IPublishedContentFilter` or Umbraco's route request handler could filter `ContentAtRoot()` to only return nodes matching the current tenant tag. Even without filtering, surfacing the current tenant name in the Umbraco backoffice header (via the v17 backoffice extension manifest in `src/UmbracoPrism.Client/`) would make the boundary visible to editors.

**Files to touch:**
- `src/UmbracoPrism.Core/PrismContentTypeSeeder.cs` — add `tenantTag` property to `homePage`/root types
- `src/UmbracoPrism.Client/src/backoffice/` — add tenant context indicator to backoffice header extension
- `docs/walkthroughs/creating-a-tenant.md` — update isolation table

### 2. ⚠️ Fix the hardcoded `/dashboard` redirect in `MemberDashboardController`

**Priority:** Medium. Breaks on multi-tenant setups where Umbraco content nodes are at different URLs per tenant.

**What:** Replace the hardcoded `Redirect("/auth/login?returnUrl=/dashboard")` with a content-tree lookup using `TestSiteSeedContract.FindPublishedByAlias()` — the same pattern `memberDashboard.cshtml` already uses (lines 10–12). This makes the redirect tenant-hostname-agnostic and Umbraco-idiomatic.

**Files to touch:**
- `src/UmbracoPrism.Core/Controllers/MemberDashboardController.cs` line 42

### 3. ⚠️ Document and surface the tenant cache TTL for operators

**Priority:** Medium. The 30-minute cache on a deleted or updated tenant is an invisible operational trap.

**What:** Expose `ITenantService.GetCacheMetrics()` in the Prism Dashboard UI (cache hit/miss/invalidation counters already exist at `src/UmbracoPrism.Core/Services/TenantService.cs` lines 132–137). Add an explicit note in `docs/walkthroughs/creating-a-tenant.md` (Part 6 — delete tenant row) warning that cache eviction takes up to 30 minutes unless manually invalidated. Consider exposing a `POST /umbraco/api/prism/tenants/{id}/invalidate-cache` endpoint so operators can flush immediately after a delete.

**Files to touch:**
- `src/UmbracoPrism.Core/Controllers/TenantManagementController.cs` — add cache-invalidate endpoint
- `docs/walkthroughs/creating-a-tenant.md` — operator warning in Part 6
- `src/UmbracoPrism.Client/src/backoffice/` — cache metrics panel in Prism Dashboard
