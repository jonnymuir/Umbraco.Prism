# Walkthrough — Creating a New Tenant

A step-by-step guide to adding a new tenant in the Umbraco backoffice, binding it to a hostname, configuring its OIDC authority, and verifying that the Prism middleware routes requests to it correctly.

> **Prerequisites:** The Prism stack is running. See [Codespaces](../../README.md#try-it-now--no-install-required) or [local setup](../../README.md#try-the-demo--local-setup). You should also be familiar with the core concepts in [docs/umbraco-setup.md](../umbraco-setup.md).

---

## Overview

Umbraco.Prism is a **multi-tenant** system. Each tenant is an isolated context — its own hostname, OIDC authority, branding, and user base — but all tenants share a single Umbraco installation and a single code deployment. Tenants are managed entirely through the Umbraco backoffice; no code changes or restarts are needed to add one.

This walkthrough adds a second local tenant (`tenant2.localhost`) alongside the existing `localhost` demo tenant.

---

## Part 1: Log Into the Umbraco Backoffice

### Step 1: Navigate to the Backoffice

1. Open `https://localhost:44345/umbraco` in your browser.
   - In Codespaces: your forwarded URL + `/umbraco`.

<!-- TODO: capture 01-backoffice-login.png via backoffice login screen -->
<!-- pending capture -->

2. Enter the admin credentials:
   - **Username:** `admin@prism.local`
   - **Password:** `PrismLocal!12345`

3. Click **Log in**.

   You land on the Umbraco backoffice dashboard.

4. 💡 **What's happening:** Umbraco's own authentication (separate from the Prism OIDC flow used by end users) verifies your credentials against the Umbraco member database. This is the standard Umbraco backoffice login — Prism does not modify it.

---

## Part 2: Navigate to the Prism Dashboard

### Step 2: Open Settings → Prism Dashboard

1. In the left navigation, click **Settings**.

2. Under the Settings section, find and click **Prism Dashboard**.

<!-- TODO: capture 02-prism-dashboard.png via Settings → Prism Dashboard -->
<!-- pending capture -->

   You see a list of currently configured tenants. The demo stack has one: `localhost`.

3. 💡 **What's happening:** The Prism Dashboard is a custom Umbraco backoffice section registered by `PrismComposer` using the Umbraco Extension Registry. It is implemented as a Lit web component (`prism-dashboard`) in `src/UmbracoPrism.Client/src/backoffice/`. The list you see is fetched from `GET /umbraco/api/prism/tenants`.

---

## Part 3: Create the New Tenant

### Step 3: Open the "New Tenant" Form

1. Click **Add tenant** (or the **+** button in the tenant list).

   A modal dialog opens with the tenant creation form.

<!-- TODO: capture 03-new-tenant-modal.png via Prism Dashboard → Add tenant modal -->
<!-- pending capture -->

2. ✅ **What you're about to fill in:**
   - **Tenant name** — a human-readable label (internal only, not shown to end users).
   - **Host binding** — the hostname Prism will match against incoming requests.
   - **OIDC authority** — the Keycloak (or Entra) issuer URL for this tenant's identity provider.
   - **Branding** — colours, fonts, and imagery (can be configured after creation).

### Step 4: Fill In the Host Binding

1. In the **Tenant name** field, type `Tenant 2`.

2. In the **Host** field, type `tenant2.localhost`.

   💡 **What's happening:** This value is stored in the `prismTenants` Umbraco database table. When a request arrives at the TestSite, `PrismTenantMiddleware` calls `ITenantService.GetByDomainAsync(host)` — which does a case-insensitive match against all registered hosts. If a match is found, the tenant is set in `IPrismContext.CurrentTenant` for the duration of that request.

   The middleware uses the `Host` header (not the request path), so subdomain-per-tenant and domain-per-tenant topologies both work without any routing configuration.

3. ✅ **What you can do with host bindings:**
   - Use a subdomain: `mycouncil.prism.gov.uk`
   - Use a custom domain: `portal.acmecorp.com`
   - Use a port (local dev): `localhost:5001`
   - Wildcards are **not** supported — each binding is an exact-match string.

### Step 5: Configure the OIDC Authority

1. In the **OIDC Authority** field, enter the Keycloak realm URL for this tenant:

   ```
   https://localhost:8443/realms/prism-dev
   ```

   For a production tenant pointing at a different Keycloak realm:
   ```
   https://your-keycloak.example.com/realms/your-realm
   ```

   For an Entra ID (Azure AD) tenant:
   ```
   https://login.microsoftonline.com/{your-tenant-id}/v2.0
   ```

2. In the **OIDC Client ID** field, enter the client ID registered in your identity provider (e.g., `prism-client`).

3. 💡 **What's happening:** The authority URL is the OpenID Connect discovery endpoint root. Prism appends `/.well-known/openid-configuration` to fetch issuer metadata, including the `jwks_uri` used to validate JWT signatures. The signing key cache (`IPrismSigningKeyCache`) is pre-warmed by `PrismTenantMiddleware` on the first request for each tenant.

### Step 6: Configure Branding

1. Click the **Branding** tab in the tenant form.

2. You see a design-system-style editor with sections: **Brand Colours**, **Typography**, **Imagery**, **Components**, **Layout**.

3. For now, accept the defaults and click **Save tenant**.

<!-- TODO: capture 04-branding-tab.png via New Tenant modal → Branding tab -->
<!-- pending capture -->

4. 💡 **What's happening:** The branding editor reads CSS variable metadata from `GET /umbraco/api/prism/branding/metadata` — the same endpoint described in [Branding Design System](../branding-design-system.md). Each variable annotated with `/* @prism section: ... | label: ... */` appears as a typed form field. Changes are saved per-tenant and served as a tenant-specific CSS override on the frontend.

5. ✅ **For a deep dive on branding:** See the [Design System walkthrough](design-system.md) for how tokens flow from the backoffice through to CSS variables consumed by web components.

---

## Part 4: How the Middleware Picks Up the New Tenant

After saving, the new tenant is immediately active — no restart required. Here is what happens on the next request to `https://tenant2.localhost:44345`:

```
Browser → TestSite
  Host: tenant2.localhost
  ↓
PrismTenantMiddleware.InvokeAsync()
  ITenantService.GetByDomainAsync("tenant2.localhost")
    → Queries prismTenants table (EF Core, cached per request)
    → Returns: { TenantId: "...", Host: "tenant2.localhost", OidcAuthority: "...", ... }
  IPrismContext.CurrentTenant = tenant
  IPrismSigningKeyCache.WarmAsync(tenant.EntraTenantId)
  ↓
Next middleware (auth, routing, Umbraco pipeline)
```

If no tenant matches the host, `CurrentTenant` is `null` and a `LogWarning` is emitted. Pages that require tenant context (e.g., the dashboard, workflow pages) will return a 404 or redirect to an error page.

💡 **What's happening:** `ITenantService` is scoped per request and queries the Umbraco database. The result is cached in `IPrismContext` (also scoped per request), so the database is only hit once per request even if multiple middleware or services read the current tenant.

---

## Part 5: Verify with a Fresh Browser Session

### Step 7: Test the New Tenant

1. Open a **new private/incognito browser window** (to ensure no existing session cookies carry over).

2. Navigate to `https://tenant2.localhost:44345`.

   > **Local dev note:** You may need to add `tenant2.localhost` to your `/etc/hosts` file if your OS doesn't resolve it automatically:
   > ```
   > 127.0.0.1  tenant2.localhost
   > ```

3. You should see the homepage rendered with the **Tenant 2** branding (default branding if you accepted the defaults).

<!-- TODO: capture 05-tenant2-homepage.png via browser at tenant2.localhost -->
<!-- pending capture -->

4. ✅ **What you can verify:**

   | Check | How |
   |---|---|
   | Tenant resolved correctly | Open browser DevTools → Network → reload → find any API call; its response headers should include `X-Prism-Tenant: Tenant 2` (if you added this debug header in your implementation) |
   | Branding applied | Check that the page colours match what you configured |
   | OIDC login works | Click **Sign In** — you should be redirected to the correct Keycloak realm |
   | Workflow isolation | Start a workflow as `demo@prism.local` on `tenant2.localhost` — the workflow instance should be isolated from the same user's instance on `localhost` (they share a user ID but have different tenant IDs, so the engine keys instances as `{tenantId}:{userId}:{workflowKey}`) |

5. 💡 **Tenant isolation in practice:**
   - **Content:** Umbraco serves the same content tree to all tenants. Tenant-specific content can be achieved by creating tenant-specific Umbraco content nodes (not covered in this walkthrough).
   - **Workflow instances:** Fully isolated by `tenantId` in the instance key.
   - **Branding:** Each tenant has its own CSS variable overrides, served at `/branding/tenant/{tenantId}/overrides.css`.
   - **OIDC tokens:** Each tenant's tokens are issued by a different authority and validated using that authority's public keys.

---

## Part 6: What the Backoffice User Controls

Once a tenant exists, an Umbraco editor can update it at any time from **Settings → Prism Dashboard → [Tenant name]**:

| Setting | Effect |
|---|---|
| **Host** | Changes which hostname routes to this tenant. Takes effect immediately on next request. |
| **OIDC Authority** | Changes the identity provider. Existing sessions remain valid until they expire. |
| **OIDC Client ID** | Updates the client ID sent to the identity provider during token validation. |
| **Branding** | Live-updates the CSS variables served to the tenant's frontend. No deploy needed. |
| **Delete tenant** | Removes the host binding. In-flight requests complete normally; subsequent requests to that host return a 404 (no tenant matched). |

---

## Related Resources

- [Umbraco Setup Guide](../umbraco-setup.md) — full installation walkthrough
- [Design System](design-system.md) — deep dive on branding tokens
- [Branding Design System](../branding-design-system.md) — annotating CSS variables for the tenant editor

---

[← Back to Walkthroughs](README.md)

---

**Executable spec:** This walkthrough is executed on every PR by [`creating-a-tenant.walkthrough.spec.ts`](../../src/UmbracoPrism.Client/tests/walkthroughs/creating-a-tenant.walkthrough.spec.ts). Screenshots above regenerate via the [`Capture Walkthrough Screenshots`](../../.github/workflows/capture-screenshots.yml) workflow (manual dispatch). See [`walkthroughs-as-executable-specs`](../../.squad/skills/walkthroughs-as-executable-specs/SKILL.md) for the policy.
