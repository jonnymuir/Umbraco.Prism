# Development Guide

Detailed setup, testing, packaging, and local-auth instructions for contributors working on
Umbraco Prism itself. For installing the published package into your own site, see the
[main README](../README.md)'s Quick Start instead.

## Prerequisites

- **.NET 10.0** ([Download](https://dotnet.microsoft.com/download))
- **Node.js 20+** ([Download](https://nodejs.org/))
- **Docker Desktop**: for local demo with Aspire ([Download](https://www.docker.com/products/docker-desktop/))
- **Azure Key Vault** (production) or local dev without vault (see [Local Authentication Walkthrough](#local-authentication-walkthrough))
- **OIDC Provider**: any OIDC-compliant system (Keycloak included for local dev; Entra ID or others for production)

> **Client dependencies:** Run before first build:
> ```bash
> cd src/UmbracoPrism.Client && npm install
> ```

## Architecture

**Prism Core provides:**

**Runtime layer:**
* `PrismTenantMiddleware`, resolves hostname to tenant
* `IPrismContext`, scoped service with tenant/theme data

**Identity layer:**
* Dynamic OIDC, swaps `ClientId`, `Authority`, `Issuer` per tenant
* `IPrismUserContext`, current user claims and tenant
* `SecretVaultService`, Azure Key Vault (Managed Identity in prod, Azure CLI local)
* Downstream flow, propagate tenant identity to APIs

**Notification layer:**
* `IPrismNotificationService`, Generic notifications API (send to members, subscribers, or broadcast)
* `PrismContentPublishedHandler`, Config-driven event handler for Umbraco publish events
* Subscription persistence, Database schema for managing notification preferences
* Rate limiting, Automatic throttling to prevent spam

**Secret Management:**
* **Entra ID tenants (production):** Secrets stored in Azure Key Vault, referenced by `SecretKeyName`
* **Generic OIDC tenants (production):** Secrets stored in Azure Key Vault, referenced by `OidcClientSecretProvider = "azure-key-vault"` plus `OidcClientSecretReference`
* **Local dev demo (Keycloak):** Repo-owned secret uses `OidcClientSecretProvider = "inline"` only for the seeded `localhost` tenant path
* **Management API/UI:** Responses expose `HasOidcClientSecret` and `OidcClientSecretProvider`, never the raw secret or reference value
* All confidential-client flows fail closed if a secret cannot be resolved at runtime

**Your application extends Prism with:**
* Business-specific notification handlers (see `PrismVinylNotificationController` in TestSite)
* Service blueprint endpoints and state machines (via `Wayfinder.Umbraco` — see the main README)
* Domain models and validation logic
* Custom API routes for your business processes

→ [Secret Management Guide](secret-management.md) | [Architecture Docs](README.md)

## Setup & Development

### Local Dev Tunnel (Mobile Testing)

For testing OIDC sign-in on mobile devices with an external OIDC provider, use `scripts/dev/start-trycloudflare.sh`:

```bash
bash scripts/dev/start-trycloudflare.sh
```

Automates:
- Cloudflare tunnel for `https://localhost:<port>`
- OIDC redirect URI update (for your provider)
- Prism tenant hostname sync
- Cleanup on exit

**Security:** Dev use only. Mutates your OIDC provider app config and local database.

→ See [Phone Auth via Cloudflare Tunnel](#phone-auth-via-cloudflare-tunnel) below for the full tunnel setup (temporary URL or stable custom domain).

### Storybook Tests (UmbracoPrism.Client)

Storybook is used for component-driven tests with the Storybook test runner + Playwright.

**Local usage:**

```bash
cd src/UmbracoPrism.Client
npm install
npm run storybook
```

In a second terminal:

```bash
cd src/UmbracoPrism.Client
npm run test-storybook
```

**VS Code (Optional):**

Optionally, install the **Playwright Test extension** for a convenient Testing view UI to run Playwright tests. Tests are in [src/UmbracoPrism.Client/tests](../src/UmbracoPrism.Client/tests). You can also run `npm run test:playwright:ui` for the interactive runner without the extension.

**Headless multi-browser + WCAG checks (recommended):**

```bash
cd src/UmbracoPrism.Client
npm run test-storybook:all
```

**CI usage (GitHub Actions):**

The workflow in [.github/workflows/ci-tests.yml](../.github/workflows/ci-tests.yml) runs the following:

```bash
cd src/UmbracoPrism.Client
npm ci
npx playwright install --with-deps
npm run test-storybook:ci:all
```

### Localhost auth/session Playwright regressions

These behavioural-contract tests run against the real Aspire stack rather than Storybook. The suite validates Aspire prerequisites, boots its own `UmbracoPrism.AppHost` session, waits for the dashboard plus seeded app resources to be ready, then signs into the seeded Keycloak demo user and restarts the whole localhost stack mid-run to verify session continuity.

**Before running:**

- Docker Desktop must be running
- `dotnet dev-certs https --trust` must already be done
- The default Aspire ports (`17214`, `44345`, `7245`, `8443`) must be free because the suite owns the stack lifecycle and will not attach to an existing or partial stack

```bash
cd src/UmbracoPrism.Client
npm run test:playwright:localhost-auth
```

The suite uses the seeded demo identity from `keycloak/realm-export.json`: `demo@prism.local` / `password`.

**Stable seeded content contract:** on a clean TestSite database, Development startup deterministically repairs the Umbraco nodes the localhost auth/service request flows use, `Home` (`/`), `Dashboard` (`/dashboard`), and the two `wayfinderServicePage` nodes carrying Wayfinder.Umbraco's own Block Grid stage/worklist blocks: `Apply for a juggling licence` (`/apply-for-a-juggling-licence`), `Submit contributions file` (`/submit-contributions-file`), and `Caseworker queue` (`/caseworker-queue`), plus the `Settings` node's mobile nav entries for all of these. The Razor views resolve those destinations from published content, so route lookup does not depend on root-node ordering.

### Core Tests (UmbracoPrism.Core)

```bash
dotnet test UmbracoPrism.slnx -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests
```

### Dependency Vulnerability Check

Run a transitive package vulnerability scan for the Core project:

```bash
dotnet list src/UmbracoPrism.Core/UmbracoPrism.Core.csproj package --vulnerable --include-transitive
```

If vulnerabilities are reported, prefer upgrading the direct package first. For transitive-only issues, add a top-level package reference in the relevant `.csproj` to force a patched version.

**VS Code (Optional):**

Optionally, install the **.NET Test Explorer extension** for a convenient Testing view UI to run the Core tests. Tests can also be run from the command line using `dotnet test`.

### Packaging & Marketplace

**Build the backoffice assets:**

```bash
cd src/UmbracoPrism.Client
npm install
npm run build
```

**Pack the NuGet package:**

```bash
dotnet pack src/UmbracoPrism.Core/UmbracoPrism.Core.csproj -c Release -o artifacts
```

**Marketplace metadata:**

See [umbraco-marketplace.json](../umbraco-marketplace.json) for the listing metadata (icon, screenshots, tags, description).

**Accessibility (WCAG) checks:**

Storybook test runner runs axe checks (WCAG 2.0/2.1 A/AA) via
[src/UmbracoPrism.Client/.storybook/test-runner.ts](../src/UmbracoPrism.Client/.storybook/test-runner.ts).

To opt out for a specific story, set `parameters: { a11y: { disable: true } }` in your `.stories.ts` file:

```typescript
export const MyStory = {
  render: (args) => <MyComponent {...args} />,
  parameters: {
    a11y: { disable: true }  // Disables WCAG checks for this story
  }
};
```

## Local Authentication Walkthrough

### 1. Choose Your OIDC Provider

**Option A: Quick Start with Keycloak (Included)**
- No setup needed; the local Keycloak is already running on `https://localhost:8443` with the seeded demo realm
- Use for immediate testing without external OIDC provider configuration
- User: `demo@prism.local` / password: `password`

**Option B: Production-Style Setup (Entra ID, Generic OIDC, etc.)**
- Create App Registration in your OIDC provider
- Redirect URI: `https://localhost:[PORT]/signin-oidc`
- Note the **Client ID** and **Authority URL** from your provider

### 2. Local Auth (Azure Key Vault)

If using an external OIDC provider and storing secrets in Key Vault:

```bash
az login --allow-no-subscriptions
```

Allows `SecretVaultService` to access Key Vault in local dev.

**Key Vault Setup:**
- Add secret (e.g., `tenant-a-secret`) with your OIDC Client Secret
- Grant **Key Vault Secrets User** to your identity

### 3. Tenant Setup

In **Prism Dashboard** (backoffice):

**For Keycloak (local dev):**
- **Hostname:** `localhost:[PORT]`
- **OIDC Authority:** `https://localhost:8443/realms/prism-dev`
- **OIDC Client ID:** `prism-client`
- **Secret Provider:** `inline` (only for demo; leave as is)

**For External OIDC Provider (Entra ID, generic OIDC, etc.):**
- **Hostname:** `localhost:[PORT]` (or your domain)
- **OIDC Authority:** Authority URL from your provider
- **OIDC Client ID:** Client ID from your provider
- **Secret Provider:** `azure-key-vault`
- **Secret Reference:** `tenant-a-secret` (or your vault secret name)

The dashboard does not round-trip raw OIDC client secrets through edit responses; production updates are reference-based, and only the seeded localhost Keycloak demo exposes an inline replace field.

### 4. Downstream API Auth

If your Prism frontend needs to call a secure backend (e.g., a "Member Dashboard" API), Prism can flow the current tenant's identity and access token to that downstream system.

#### 1. Backend API: Enabling Prism Auth

In your downstream ASP.NET Core API, register the Prism authentication handler. This allows the API to accept multi-tenant tokens from any CIAM tenant registered in your system.

```csharp
// In your API's Program.cs
builder.Services.AddPrismAuthentication(builder.Configuration);
```

#### 2. Backend API: Resolving the Tenant

Use the Prism identity extensions to resolve which brand the user belongs to. This ensures data isolation at the API level.

```csharp
app.MapGet("/api/backoffice/me", (IConfiguration config, ClaimsPrincipal user) =>
{
    // Resolves the tenant from config (default) or a custom resolver
    var tenant = user.GetPrismTenant(PrismResolvers.FromConfig(config));

    if (tenant == null) return Results.Unauthorized();

    return Results.Ok(new {
        Brand = tenant.DisplayName,
        Code = tenant.Code
    });
}).RequireAuthorization();
```

#### 3. Frontend: Calling the API

From your Umbraco site, use `IPrismContext` to automatically generate the correct Authorization header containing the user's `access_token`.

```csharp
public async Task<string> GetMemberDataAsync()
{
    using var client = new HttpClient();
    // Automatically handles token extraction and refresh logic
    client.DefaultRequestHeaders.Authorization = await PrismContext.GetAuthorizationHeaderAsync();

    return await client.GetStringAsync("https://your-api.com/api/backoffice/me");
}
```

## Sample Projects

**`UmbracoPrism.TestSite`**, Reference Umbraco v17 application. Shows a complete example of extending Prism for a business domain (vinyl record store). Includes:
- OIDC setup and tenant branding
- Custom notification handler for "back-in-stock" alerts
- Wayfinder.Umbraco's Block Grid-composed service design blocks, in use on real content (a citizen journey and a caseworker worklist, see [Bulk Data Review Walkthrough](walkthroughs/bulk-data-review.md))
- Pre-configured tenant definitions for local development

Use this as a template for building your own application on top of Prism Core.

**`UmbracoPrism.MockBusinessApp`**, A real, separate downstream application, not a business-app simulator Prism hosts or drives. Two narrow jobs: proving Prism's own Bearer-token identity propagation (`GET /api/backoffice/me`), and hosting a generic downstream support-system reference implementation the contributions-file demo calls out to for real per-row validation. Demonstrates `AddPrismAuthentication` and multi-tenant data isolation for backend services. See that project's own README for the full endpoint list.

## Phone Auth via Cloudflare Tunnel

For OIDC sign-in on mobile devices with an external provider, use HTTPS tunnel (most OIDC providers require `https://` or `http://localhost` only).

### No Domain (Temporary URL)

```bash
brew install cloudflared
cloudflared tunnel --url https://localhost:44345
```

Or use helper:

```bash
bash scripts/dev/start-trycloudflare.sh
```

Add redirect URI in your OIDC provider:
```
https://<random>.trycloudflare.com/signin-oidc
```

Helper script auto-rotates stale trycloudflare URIs.

### Stable Hostname (Custom Domain)

```bash
cloudflared tunnel login
cloudflared tunnel create prism-dev
cloudflared tunnel route dns prism-dev prism-dev.<your-domain>
```

Create `~/.cloudflared/config.yml`:

```yml
tunnel: <tunnel-id>
credentials-file: /Users/<you>/.cloudflared/<tunnel-id>.json

ingress:
  - hostname: prism-dev.<your-domain>
    service: https://localhost:44345
    originRequest:
      noTLSVerify: true
      httpHostHeader: localhost:44345
  - service: http_status:404
```

Run:

```bash
cloudflared tunnel run prism-dev
```

Redirect URI:
```
https://prism-dev.<your-domain>/signin-oidc
```
