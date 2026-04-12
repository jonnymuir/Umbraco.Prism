<div align="center">
<img src="assets/logo-horizontal-lockup.svg" width="500" alt="Umbraco Prism Logo">
<h3>One source. A spectrum of brands.</h3>
</div>

# Umbraco Prism

```bash
dotnet add package UmbracoPrism
```

One Umbraco instance. Multiple branded portals. Native mobile app included.

Multi-tenant website branding and identity at runtime. Add a mobile app with one click.

---

## Try the Demo — No Azure Required

Get from clone to running in five minutes. No Azure account needed.

**One-time setup:**
- Docker Desktop running ([Download](https://www.docker.com/products/docker-desktop/))
- `.NET Aspire workload:` `dotnet workload install aspire`
- `Node.js 20+` ([Download](https://nodejs.org/))
- Frontend dependencies: `cd src/UmbracoPrism.Client && npm install`

**Start the full stack:**
```bash
dotnet run --project src/UmbracoPrism.AppHost
```

Then:
1. Open the Aspire dashboard at `https://localhost:17214`
2. Click the TestSite URL → log in with `demo@prism.local` / `password`
3. Browse **My Workflows** to see the demo workflow in action
4. The MockBusinessApp runs alongside — it accepts the same demo credentials and powers the workflow engine

**Optional:** Explore the Keycloak admin at `http://localhost:8080/admin` (`admin` / `admin`).

> For detailed setup, troubleshooting, and architecture: See [ASPIRE_DEV.md](ASPIRE_DEV.md).

---

## What You Get

### Multi-Tenant Web — One Instance, Hundreds of Brands

Serve distinct branded portals from one Umbraco instance. Runtime branding, domain resolution, tenant isolation.

<div align="center">
<img src="screenshots/testsite.png" width="400" alt="Branded portal example">
<img src="screenshots/backoffice2.png" width="400" alt="Backoffice branding editor">
</div>

**Web features:**
- Domain-based tenant resolution — each client gets their own hostname
- Live branding editor — CSS variables update without deploy
- **Branding as a Design System** — annotated CSS variables become labeled form fields, grouped into sections (Colors, Typography, Components), with type-aware editors (color pickers, sliders, text inputs)
  
  ```css
  @property --prism-primary {
    syntax: '<color>';
    inherits: true;
    initial-value: #4f46e5;
  }
  
  :root {
    /* @prism section: Brand Colours | label: Primary Brand Colour | description: Main brand colour used for buttons and links */
    --prism-primary: #4f46e5;
  }
  ```
  
  → [Branding Design System →](docs/branding-design-system.md)

- Per-tenant OIDC — Entra ID integration, zero local Members
- Downstream auth — propagate tenant identity to internal APIs
- Tenant isolation — authorization policies enforce data boundaries

→ [Umbraco Setup Guide](docs/umbraco-setup.md)

### Produce Mobile — Generate Apps from Backoffice

Turn tenant settings into iOS/Android apps. No complex native coding, just click **Produce Mobile**.

<div align="center">
<img src="screenshots/example-IOS.png" width="300" alt="iOS app with tenant branding">
</div>

**Mobile features:**
- Biometric login (Face ID, fingerprint) — skip OIDC on return
- Push notifications (FCM/APNs) — content or API triggered
- Offline-ready layouts with safe-area handling
- Tenant branding at runtime (colors, logo, splash)

Run in simulator:

```bash
npm run bootstrap:ios
```

→ [Mobile Setup](docs/PUSH_SETUP.md) | [Biometric Auth](docs/biometric-setup.md)

---

## Quick Start

### 1. Install

```bash
dotnet add package UmbracoPrism
```

Prism registers automatically via `PrismComposer` — no manual service registration needed.

### 2. Configure

Add to `appsettings.json`:

```json
{
  "Prism": {
    "VaultUri": "https://your-keyvault.vault.azure.net/"
  }
}
```

For local dev without Azure Key Vault, see [Local Authentication Walkthrough](#local-authentication-walkthrough).

### 3. Run

```bash
dotnet run
```

Prism auto-creates document types (`homePage`, `memberDashboard`) on first startup.

### 4. Add Your First Tenant

In backoffice:
1. **Settings → Prism Dashboard**
2. Add tenant (hostname, Entra ID settings, branding)
3. Visit the hostname — see branded portal

→ [Full Setup Guide](docs/umbraco-setup.md)

---

## How It Works

**Multi-tenancy at runtime:** Middleware resolves hostname to tenant. One content tree serves hundreds of portals.

**Stateless auth:** No local Members. Identity deferred to Entra ID. Secrets in Azure Key Vault.

**Mobile generation:** Tenant settings → iOS/Android app. Run in simulator immediately.

**Downstream auth:** Pass tenant identity to internal APIs without shared state.

---

## Features

**Multi-tenant web:**
- Domain-based tenant resolution
- Live CSS variable branding
- Per-tenant Entra ID (OIDC)
- Tenant isolation policies
- Downstream API auth

**Mobile:**
- iOS/Android generation from backoffice
- Biometric login (Face ID, fingerprint)
- Push notifications (FCM/APNs)
- Offline-ready layouts

**Infrastructure:**
- Azure Key Vault secrets at runtime
- Zero local Member records
- Managed Identity support
- Admin-only backoffice policies

→ [Full Documentation](docs/)

---

## Documentation

| Guide | Description |
|---|---|
| [Umbraco Setup](docs/umbraco-setup.md) | Install Prism, configure tenants, seed content |
| [Biometric Setup](docs/biometric-setup.md) | Generate signing/encryption keys for mobile biometric auth |
| [Push Notifications](docs/PUSH_SETUP.md) | Configure FCM (Android) and APNs (iOS) for push |
| [Notifications Design](docs/notifications-design.md) | Push notification architecture and API reference |
| **Design Docs** | |
| [Notifications Architecture](docs/design/notifications-architecture.md) | Internal design: notification system layers |
| [Notifications Backend](docs/design/notifications-backend.md) | Internal design: backend API and service layer |
| [Notifications Mobile](docs/design/notifications-mobile.md) | Internal design: Capacitor plugin integration |
| [Notifications Umbraco](docs/design/notifications-umbraco-demo.md) | Internal design: Umbraco content hooks and demo site |

→ [Full Documentation Index](docs/)

---

## Architecture

**Runtime layer:**
* `PrismTenantMiddleware` — resolves hostname to tenant
* `IPrismContext` — scoped service with tenant/theme data

**Identity layer:**
* Dynamic OIDC — swaps `ClientId`, `Authority`, `Issuer` per tenant
* `IPrismUserContext` — current user claims and tenant
* `SecretVaultService` — Azure Key Vault (Managed Identity in prod, Azure CLI local)
* Downstream flow — propagate tenant identity to APIs

→ [Architecture Docs](docs/)

---

## Prerequisites

- **.NET 10.0** ([Download](https://dotnet.microsoft.com/download))
- **Node.js 20+** ([Download](https://nodejs.org/))
- **Docker Desktop** — for local demo with Aspire ([Download](https://www.docker.com/products/docker-desktop/))
- **.NET Aspire workload** — for local dev: `dotnet workload install aspire` (one-time)
- **Azure Key Vault** (production) or local dev without vault (see setup guide)
- **Entra ID** (for authentication)

> **Client dependencies:** Run before first build:
> ```bash
> cd src/UmbracoPrism.Client && npm install
> ```

---

## Setup & Development

### Local Dev Tunnel (Mobile Testing)

For testing Entra sign-in on mobile devices, use `scripts/dev/start-trycloudflare.sh`:

```bash
bash scripts/dev/start-trycloudflare.sh
```

Automates:
- Cloudflare tunnel for `https://localhost:<port>`
- Entra redirect URI update
- Prism tenant hostname sync
- Cleanup on exit

**Security:** Dev use only. Mutates Entra app and local database.

→ [Full tunnel docs in README section below](#quick-start-phone-auth-via-cloudflare-tunnel-no-lan-ip-dependency)

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

Optionally, install the **Playwright Test extension** for a convenient Testing view UI to run Playwright tests. Tests are in [src/UmbracoPrism.Client/tests](src/UmbracoPrism.Client/tests). You can also run `npm run test:playwright:ui` for the interactive runner without the extension.

**Headless multi-browser + WCAG checks (recommended):**

```bash
cd src/UmbracoPrism.Client
npm run test-storybook:all
```

**CI usage (GitHub Actions):**

The workflow in [.github/workflows/ci-tests.yml](.github/workflows/ci-tests.yml) runs the following:

```bash
cd src/UmbracoPrism.Client
npm ci
npx playwright install --with-deps
npm run test-storybook:ci:all
```

### Core Tests (UmbracoPrism.Core)

```bash
dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests
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

See [umbraco-marketplace.json](umbraco-marketplace.json) for the listing metadata (icon, screenshots, tags, description).

**Accessibility (WCAG) checks:**

Storybook test runner runs axe checks (WCAG 2.0/2.1 A/AA) via
[src/UmbracoPrism.Client/.storybook/test-runner.ts](src/UmbracoPrism.Client/.storybook/test-runner.ts).

To opt out for a specific story, set `parameters: { a11y: { disable: true } }` in your `.stories.ts` file:

```typescript
export const MyStory = {
  render: (args) => <MyComponent {...args} />,
  parameters: {
    a11y: { disable: true }  // Disables WCAG checks for this story
  }
};
```

### Local Authentication Walkthrough

#### 1. Azure Setup

**Entra ID:** Create App Registration. Redirect URI: `https://localhost:[PORT]/signin-oidc`.

**Key Vault:** Add secret (e.g., `tenant-a-secret`) with Client Secret.

**Permissions:** Grant **Key Vault Secrets User** to your identity.

#### 2. Local Auth

```bash
az login --allow-no-subscriptions
```

Allows `SecretVaultService` to access Key Vault in local dev.

#### 3. Tenant Setup

In **Prism Dashboard** (backoffice):
- **Hostname:** `localhost:[PORT]`
- **Entra Tenant ID:** Directory ID
- **Entra Client ID:** App Registration ID
- **Secret Key Name:** `tenant-a-secret`

#### 4. Downstream API Auth

If your Prism frontend needs to call a secure backend (e.g., a "Member Dashboard" API), Prism can flow the current tenant’s identity and access token to that downstream system.

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

---

### Sample Projects

**`UmbracoPrism.TestSite`** — Reference Umbraco v17 site. Shows OIDC setup, tenant branding, downstream API calls. Pre-configured tenant definitions for local auth.

**`UmbracoPrism.MockBackOffice`** — Minimal API. Shows `AddPrismAuthentication` and multi-tenant data isolation.

→ See [Local Authentication Walkthrough](#local-authentication-walkthrough)

---

## Stack

* **Umbraco:** v17.0+
* **.NET:** 10.0
* **Auth:** Stateless OIDC (Entra), Azure Key Vault, Managed Identity
* **Mobile:** Capacitor, TypeScript, Storybook

---

## Phone Auth via Cloudflare Tunnel

For Entra sign-in on mobile, use HTTPS tunnel (Entra requires `https://` or `http://localhost` only).

### No Domain (Temporary URL)

```bash
brew install cloudflared
cloudflared tunnel --url https://localhost:44345
```

Or use helper:

```bash
bash scripts/dev/start-trycloudflare.sh
```

Add redirect URI in Entra:
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