<div align="center">
<img src="assets/logo-horizontal-lockup.svg" width="500" alt="Umbraco Prism Logo">
<h3>One source. A spectrum of brands.</h3>
</div>

# Umbraco Prism

```bash
dotnet add package UmbracoPrism
```

One Umbraco instance. Hundreds of branded client portals. Native mobile apps with one click.

---

## What You Get

### Generate Native Mobile Apps from the Backoffice

Turn tenant settings into a production-ready iOS/Android app. No Xcode project setup. No Gradle config. Just click **Produce Mobile** in the backoffice.

![iOS app with tenant branding applied](example-IOS.png)

Run in simulator with one command:

```bash
npm run bootstrap:ios
```

**Mobile-first features:**
- Biometric login (fingerprint, Face ID) — skip OIDC on return visits
- Push notifications (FCM/APNs) — content-triggered or API-triggered
- Offline-ready with safe-area mobile layouts
- Tenant branding applied at runtime (colors, logo, splash screen)

→ [Mobile Setup Guide](docs/PUSH_SETUP.md) | [Biometric Auth Setup](docs/biometric-setup.md)

### Live Backoffice Branding Editor

Design your tenant's look in real time. No build step. No deploy.

![Backoffice tenant branding editor](backoffice2.png)

CSS variables update instantly across web and mobile:

![Test site with branding overrides](testsite-overrides.png)

→ [Umbraco Setup Guide](docs/umbraco-setup.md)

### Multi-Tenant Identity Without the Mess

One Umbraco site. Hundreds of clients. Each gets their own:
- Domain name
- Entra ID tenant
- Branding theme
- Member portal

No local Member database. Identity deferred to Entra (CIAM). Secrets pulled from Azure Key Vault at runtime.

![Downstream credential flow example](downstream.png)

→ [Architecture Overview](#architecture)

---

## Quick Start

### 1. Install the Package

```bash
dotnet add package UmbracoPrism
```

### 2. Register Services

In `Program.cs`:

```csharp
builder.Services.AddPrism(builder.Configuration);
```

### 3. Run Your Site

```bash
dotnet run
```

Prism auto-creates document types (`homePage`, `memberDashboard`) on first startup.

### 4. Configure Your First Tenant

In Umbraco backoffice:
1. Go to **Settings → Prism Dashboard**
2. Add a tenant (name, hostname, Entra settings)
3. Visit your site — see the branded homepage

→ **Full guide:** [Umbraco Setup](docs/umbraco-setup.md)

---

## What It Does

**Multi-tenancy at runtime:** One content tree serves hundreds of client portals. Branding, identity, and content context resolve by hostname.

**Stateless authentication:** No local Member records. Identity deferred to Entra ID. Secrets in Azure Key Vault.

**Mobile app generation:** Generate iOS/Android apps from backoffice tenant settings. Run in emulator immediately.

**Downstream credential flow:** Pass tenant identity to internal APIs or microservices. No shared-state logic.

---

## Features

- ✅ **Multi-tenant branding** — CSS variables update per hostname
- ✅ **Entra ID authentication** — OIDC with dynamic tenant config
- ✅ **Mobile app generation** — iOS/Android from backoffice
- ✅ **Biometric login** — fingerprint/Face ID for mobile
- ✅ **Push notifications** — content-triggered or API-triggered
- ✅ **Azure Key Vault integration** — secrets pulled at runtime
- ✅ **Downstream API auth** — propagate tenant identity to microservices
- ✅ **Zero local Members** — identity stays in Entra
- ✅ **Backoffice admin policy** — restrict tenant management to admins

→ See [docs/](docs/) for detailed guides on each feature.

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

Two layers work together:

### The Runtime (Middleware)
* **PrismTenantMiddleware** — resolves hostname to tenant
* **IPrismContext** — scoped service with current tenant/theme data

### The Identity Engine (Stateless OIDC)
* **Dynamic configuration** — OIDC pipeline swaps `ClientId`, `Authority`, `Issuer` per tenant
* **IPrismUserContext** — access current user claims and tenant details
* **SecretVaultService** — fetches secrets from Azure Key Vault (Managed Identity in production, Azure CLI in local dev)
* **Downstream flow** — propagate tenant identity to internal APIs without shared state

→ [Full Architecture Guide](docs/)

---

## Prerequisites

Before you begin:

- **.NET 10.0** ([Download](https://dotnet.microsoft.com/download))
- **Node.js 20+** ([Download](https://nodejs.org/))
- **Azure Key Vault** (production only)
- **Entra ID (Azure AD)** (for authentication)

> **Important:** Install client dependencies before first build:
> ```bash
> cd src/UmbracoPrism.Client && npm install
> ```

---

## Setup & Development

### Local Dev Tunnel Automation (trycloudflare + Entra + Prism DB)

Use `scripts/dev/start-trycloudflare.sh` to automate local development setup when your local Umbraco site needs a public HTTPS callback.

Purpose:

- Start a Cloudflare quick tunnel for `https://localhost:<port>`
- Update your Entra app redirect URI to `<tunnel-url>/signin-oidc` (Prism auth callback path)
- Remove stale `*.trycloudflare.com/signin-oidc` redirect URIs before adding the current tunnel callback URI
- Update the selected Prism tenant hostname in SQLite (`prismTenants.hostname`)

Security notes:

- Development use only. Do not run this script for production or shared environments.
- The script changes Entra redirect URI configuration for the selected Entra Application (Client) ID. Use a dedicated dev Entra app registration.
- The script writes a new hostname into your local Prism tenant record. Point it at a local/test database only.
- Treat `.prism_tunnel.conf` as sensitive operational metadata and do not commit it.
- Use least-privilege Azure access: identity running `az` should only manage the intended app registration.

Prerequisites:

- `cloudflared`
- `az` (Azure CLI) with an authenticated session (`az login --allow-no-subscriptions` recommended when your dev Entra tenant has no active Azure subscription)
- `sqlite3`
- `grep` and `sed` (available by default on macOS)

Run:

```bash
bash scripts/dev/start-trycloudflare.sh
```

On first run, you will be prompted for:

- `LOCAL_PORT` (default `44345`)
- `ENTRA_APP_CLIENT_ID` (Entra Application (Client) ID GUID)
- `TENANT_SELECTOR` (tenant name or numeric id; numeric id is the internal DB `prismTenants.id`)
- `DB_PATH` (default `src/UmbracoPrism.TestSite/umbraco/Data/Umbraco.sqlite.db`)

Tenant selector behavior:

- If you provide a tenant name, the script resolves it to the canonical `TENANT_ID` before updating the database.
- If no row matches that name, the script fails with a helpful message.
- If multiple rows share that name, the script fails and prints matching ids so you can retry with a numeric id.
- Summary output shows both tenant id and tenant name so you can confirm the updated record.

Redirect URI rotation behavior:

- Non-trycloudflare redirect URIs are preserved as-is.
- Stale `*.trycloudflare.com/signin-oidc` entries are pruned. This prevents redirect URI sprawl accumulating in Entra over repeated dev sessions.
- The current tunnel callback URI is ensured exactly once.
- Script output includes a concise prune summary with the number of stale trycloudflare callback entries removed.

Config storage:

- Saved in `.prism_tunnel.conf` at repo root
- Starter template is committed at `.prism_tunnel.conf.example`
- Script enforces permissions `600` (owner read/write only)
- Backward compatible: if legacy `ENTRA_APP_OBJECT_ID` exists and `ENTRA_APP_CLIENT_ID` is missing, the script reads the legacy value once and then saves only `ENTRA_APP_CLIENT_ID` going forward.

Stop and cleanup:

- Press `Ctrl+C`
- Script creates tunnel temp logs by trying writable directories in order:
  `artifacts/logs/trycloudflared`, `${TMPDIR:-/tmp}/prism-trycloudflared-logs`, `/tmp/prism-trycloudflared-logs`, then `$HOME/.cache/prism-trycloudflared-logs` (when `HOME` is set)
- The script stops `cloudflared` and removes its temporary log file automatically

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

#### Phase 1: Azure Setup

1. **Entra ID:** Create an **App Registration** (CIAM recommended). Set the Redirect URI to `https://localhost:[PORT]/signin-oidc`.
2. **Key Vault:** Create an Azure Key Vault and add a secret (e.g., `tenant-b-secret`) containing the Client Secret.
3. **Permissions:** Ensure your identity (or App Service) has the **Key Vault Secrets User** role.

#### Phase 2: Local Auth

Run `az login --allow-no-subscriptions` in your terminal to allow the `SecretVaultService` to access Azure during local development, especially when you need to select the correct Entra tenant but do not have an active Azure subscription in that directory.

#### Phase 3: Tenant Onboarding

1. Navigate to the **Prism Dashboard** in the Umbraco Backoffice.
2. Create a Tenant with the following Identity mapping:
* **Hostname:** `localhost:[PORT]`
* **Entra Tenant ID:** Your Directory (tenant) ID.
* **Entra Client ID:** Your App Registration ID.
* **Secret Key Name:** `tenant-a-secret`.


#### Phase 4: Downstream API Authentication

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

The repository includes two reference projects that demonstrate end-to-end multi-tenant authentication and configuration:

* **`UmbracoPrism.TestSite`**: A reference Umbraco v17 implementation showing how to configure the OIDC pipeline with Prism, set up tenant-specific branding, and call secure downstream services. Use this when setting up local Entra authentication — it includes pre-configured tenant definitions.
* **`UmbracoPrism.MockBackOffice`**: A standalone minimal API project that demonstrates how to use `AddPrismAuthentication` and the `PrismTenantResolver` to isolate data across hundreds of tenants on the backend.

These projects ship pre-configured OIDC settings and are referenced in the "[Local Authentication Walkthrough](#local-authentication-walkthrough)" section below.

---

## Technical Stack

* **Umbraco:** v17.0+
* **Framework:** .NET 10.0
* **Security:** Azure Key Vault, Managed Identity, Stateless OIDC (CIAM), **Multi-tenant JWT Bearer validation**

---

## Quick Start: Phone Auth via Cloudflare Tunnel (No LAN IP Dependency)

If you need Entra sign-in on a phone, avoid using `http://192.168.x.x` redirect URIs.
Entra requires redirect URIs to be `https://...` (or `http://localhost` only), so a tunnel is the easiest dev-safe approach.

### Do I need a domain?

No. You can start with a temporary Cloudflare URL (`*.trycloudflare.com`) and no custom domain.

- **No domain yet (fastest):** use `cloudflared tunnel --url https://localhost:44345`.
- **Have a domain later (stable):** create a named tunnel + DNS record for a fixed hostname.

### Option A — No domain (temporary URL)

1. Install cloudflared:

```bash
brew install cloudflared
```

2. Start your local app on HTTPS localhost:

```bash
https://localhost:44345
```

3. Start temporary tunnel:

```bash
cloudflared tunnel --url https://localhost:44345
```

Or use the helper script (prints the exact Entra redirect URI):

```bash
bash scripts/dev/start-trycloudflare.sh
```

4. Copy the generated `https://<random>.trycloudflare.com` URL.

5. In Entra App Registration, add redirect URI:

```text
https://<random>.trycloudflare.com/signin-oidc
```

6. Use the same tunnel URL as your mobile Start URL.

> Note: This URL changes each run. If you use `scripts/dev/start-trycloudflare.sh`, the script rotates trycloudflare `/signin-oidc` redirect URIs automatically and keeps only the current callback entry plus non-trycloudflare entries.

### Option B — Stable hostname (custom domain)

If you have a domain in Cloudflare, set up a named tunnel once and keep a fixed URL.

1. Authenticate cloudflared:

```bash
cloudflared tunnel login
```

2. Create tunnel:

```bash
cloudflared tunnel create prism-dev
```

3. Route DNS hostname:

```bash
cloudflared tunnel route dns prism-dev prism-dev.<your-domain>
```

4. Create `~/.cloudflared/config.yml`:

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

5. Run tunnel:

```bash
cloudflared tunnel run prism-dev
```

6. In Entra, use:

```text
https://prism-dev.<your-domain>/signin-oidc
```