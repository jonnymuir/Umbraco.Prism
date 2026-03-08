<div align="center">
<img src="assets/logo-horizontal-lockup.svg" width="500" alt="Umbraco Prism Logo">
<h3>One source. A spectrum of brands.</h3>
</div>

# Umbraco Prism

## Overview
Umbraco Prism is a multi-tenancy extension for Umbraco (v17+) designed to allow a single Umbraco instance to serve hundreds of distinct client portals. It resolves branding, identity, and content context at runtime based on the incoming domain name.

## What problem does it solve
You are a service provider offering a portal with consistent functionality across different organizations. You want each organization to appear as its own branded web portal, but without the overhead of managing multiple root nodes or a bloated local Member database.

## Core Objectives
1. **Single Tree, Multiple Brands:** Maintain one content tree; apply branding layers dynamically.
2. **Configuration-Driven Auth:** Authentication is activated globally by providing a Vault URI; tenants are then managed via the Backoffice.
3. **Strict Isolation:** Ensure data and branding never leak between tenants.
4. **Stateless Identity:** No local Member records. Identity is deferred to Entra ID (CIAM), keeping the database clean and scalable.
5. **Vaulted Security:** Sensitive OIDC Secrets are pulled securely from Azure Key Vault at runtime.

## Overview screenshots

### Easy backoffice setup

Create and edit tenants in the back office.

![Shows how tenant editing appears in the umbraco back office](backoffice.png)

An editable realtime design system using css variables.

![Shows how altering branding works](backoffice2.png)

See changes in real time on your site without any recompilation.

![Test site with overrides](testsite-overrides.png)

Compared to without overrides.

![Test site without overrides](testsite.png)


### Simple to debug

![Shows how debug looks on your site](debug-info.png)

### Flow down to your down stream services

![Example of umbraco calling a downstream service](downstream.png)

---

## Architecture

### 1. The Runtime (Middleware)
* **PrismTenantMiddleware:** Intercepts requests and resolves the hostname against the Tenant Cache.
* **IPrismContext:** A scoped service containing the current `Tenant` and `Theme` data.

### 2. The Identity Engine (Stateless OIDC)
* **Dynamic Configuration:** Prism controls the OIDC pipeline per request, swapping `ClientId`, `Authority`, and `Issuer` keys based on the resolved tenant.
* **IPrismUserContext:** High-performance access to the current user's claims and their associated Prism Tenant details.
* **SecretVaultService:** Uses `Azure.Identity` to fetch Client Secrets from Azure Key Vault, utilizing Managed Identity in production and CLI login during development.
* **Downstream Identity Flow:** Prism supports secure token propagation to internal APIs or Back Office systems. It validates and resolves the tenant identity on the receiving end without requiring complex shared-state logic.

---

## Integration & Usage

### 1. Enabling Authentication
Authentication is active by default once a Vault URI is detected in your configuration. In your `appsettings.json`, simply provide your Azure Key Vault address:

```json
{
  "Prism": { 
    "VaultUri": "[https://your-vault.vault.azure.net/](https://your-vault.vault.azure.net/)" 
  }
}
```

### 2. Diagnostic & Debugging (Tag Helper)

To quickly visualize the active tenant, user identity, and system health, use the built-in diagnostic Tag Helper.

First, register the Tag Helper in your `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, UmbracoPrism.Core
```

Then, drop the tag into any Razor view (e.g., your Master Template or Home Page):

```html
<prism-debug />
```

### 3. Mobile Workflow (Backoffice → Emulator)

Prism includes a first-pass **Produce Mobile** workflow in the tenant editor to generate a Capacitor starter app with minimal manual setup.

#### A) Configure in Backoffice

Open a tenant, then use the **Produce Mobile** tab and provide:

- App Name
- App ID (reverse-domain format, e.g. `com.example.portal`)
- Version (e.g. `1.0.0`)
- Start URL (absolute URL)
- User Agent Marker (default: `PrismMobile`)
- Icon URL (recommended 1024x1024)
- Splash URL (optional)
- Startup Error Title / Message
- Startup Error Background / Text colors
- Show technical diagnostics toggle

Built-in helpers include app-id suggestion, tenant-based defaults, inline validation, and icon/splash previews.

Click **Generate & Download App Bundle**.

#### B) What the bundle contains

- `capacitor.config.ts` with your tenant values
- `package.json` with Capacitor scripts/dependencies
- `www/index.html` local startup bootstrap with remote reachability check + branded fallback screen
- `www/mobile-overrides.css` as a mobile styling starter
- `scripts/doctor-mobile.sh` environment diagnostics
- `scripts/bootstrap-ios.sh` / `scripts/bootstrap-android.sh` one-command emulator bootstrap
- `AGENT_PROMPT.md` handoff instructions for coding agents
- `resources/mobile-assets.json` with icon/splash values
- Generated `README.md` with commands

Management API endpoint used by the Backoffice tab:

- `POST /umbraco/management/api/v1/prism/tenants/{id}/produce-mobile`

#### C) Run on emulators

From the extracted bundle:

```bash
npm install
npm run doctor
```

#### Prerequisites (required once per machine)

- Node.js 20+ and npm
- Xcode (for iOS)
- CocoaPods (for iOS)
- Android Studio + Android SDK (for Android)

On macOS, install CocoaPods if needed:

```bash
brew install cocoapods
```

Verify:

```bash
pod --version
```

**iOS (macOS required):**

One-command bootstrap:

```bash
npm run bootstrap:ios
```

Or manually add platform, sync and open:

```bash
npx cap add ios
npx cap sync ios
npx cap open ios
```

Then in Xcode:

1. Select a simulator (or device).
2. Set Team/Bundle Signing.
3. Press Run.

**Android:**

One-command bootstrap:

```bash
npm run bootstrap:android
```

Or manually add platform, sync and open:

```bash
npx cap add android
npx cap sync android
npx cap open android
```

Then in Android Studio:

1. Create/select emulator (AVD).
2. Sync Gradle.
3. Press Run.

#### Common setup errors

- `[error] CocoaPods is not installed.`
  - Install with `brew install cocoapods`, then rerun `npx cap add ios`.
- `[error] ios platform has not been added yet.`
  - Run `npx cap add ios` before `npx cap sync ios` / `npx cap open ios`.
- `[error] android platform has not been added yet.`
  - Run `npx cap add android` before `npx cap sync android` / `npx cap open android`.

#### iOS localhost HTTPS cert trust (`NSURLErrorDomain -1202`)

If your Start URL is `https://localhost:<port>`, iOS simulator can show a blank screen until the cert is trusted.

The generated app bundle includes a helper script:

```bash
bash scripts/trust-ios-localhost-cert.sh
npx cap run ios
```

Notes:

- Keep your local HTTPS site running while executing the script.
- Ensure a simulator is booted first.
- For physical devices, prefer LAN/tunnel/public HTTPS, or install/trust your local CA profile on-device.

### 4. Mobile Runtime Behavior & Styling

Prism applies mobile behavior with these rules:

1. Base tenant overrides are injected first.
2. Mobile overrides are injected second (so mobile values can intentionally win).
3. Mobile request detection supports user-agent marker, query flag, cookie, and platform header.
4. Produced mobile bundles append `?prismMobile=1` on startup navigation for reliable server-side mobile detection.
5. Prism persists this marker as a cookie, so mobile mode continues across subsequent navigation in the same session.

When Start URL cannot be reached at app launch, the generated app shows your configured fallback screen and optional diagnostics to help with debugging (for example, localhost or unreachable host issues).

For local/demo simulation, use:

```html
<prism-mobile-user-agent-demo />
```

Optional query flags:

- `?prismMobile=1` enable mobile simulation
- `?prismMobile=0` disable mobile simulation

To style app-like UI, add a runtime class when mobile UA is detected:

```html
<script>
  (function() {
    if (navigator.userAgent.includes('PrismMobile')) {
      document.documentElement.classList.add('prism-mobile');
    }
  })();
</script>
```

Example CSS:

```css
.prism-mobile .desktop-nav { display: none; }
.prism-mobile .app-shell-footer { display: flex; }
```

### 5. Store Readiness (App Store / Play Store)

Prism can generate the starter shell, but store submission still needs platform-specific release work:

**Required for both stores**

- Production app icon/splash asset set
- Signed release build configuration
- Privacy policy URL and support details
- App metadata (name, description, screenshots)
- Functional test pass on real devices

**Apple App Store (iOS)**

- Apple Developer account
- Unique bundle id + provisioning profiles
- Archive and upload via Xcode
- App Store Connect listing + review submission

**Google Play (Android)**

- Google Play Console account
- Unique application id + signed AAB
- Store listing + content rating + data safety form
- Internal/closed testing, then production rollout

### 6. Accessing User Data

Since Prism is stateless, you do not use `MemberManager`. Instead, inject `IPrismUserContext` to access details:

```cshtml
@inject IPrismUserContext PrismUser

@if (PrismUser.IsAuthenticated)
{
    <h1>Welcome back, @PrismUser.Name</h1>
    <p>You are logged into the @PrismUser.CurrentTenant?.Name portal.</p>
}
```

### 7. Prism Admins Policy (Backoffice Safety)

Tenant management is powerful (it can change domains, secrets, and branding), so Prism restricts these endpoints to a dedicated admin policy. By default, only users in the **admin** group can create, update, or delete tenants.

**Why:** ensures only trusted backoffice users can manage tenant identity and branding settings.

**How:** configure which backoffice user groups are allowed by setting group aliases in `appsettings.json`:

```json
{
  "Prism": {
    "AdminGroups": {
      "GroupAliases": ["admin", "prism-admins"]
    }
  }
}
```

Use Umbraco's User Groups to grant access (Settings → Users → Groups). Anyone not in these groups can still access the backoffice, but cannot modify tenants via the Prism management API.

---

## Setup & Development

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

**VS Code:**

Install the Playwright Test extension to run Playwright tests in the Testing view. Tests are in [src/UmbracoPrism.Client/tests](src/UmbracoPrism.Client/tests). You can also run `npm run test:playwright:ui` for the interactive runner.

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

**VS Code:**

Install the .NET Test Explorer extension to run the Core tests in the Testing view.

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
To opt out for a specific story, set `parameters: { a11y: { disable: true } }`.

### Local Authentication Walkthrough

#### Phase 1: Azure Setup

1. **Entra ID:** Create an **App Registration** (CIAM recommended). Set the Redirect URI to `https://localhost:[PORT]/signin-oidc`.
2. **Key Vault:** Create an Azure Key Vault and add a secret (e.g., `tenant-b-secret`) containing the Client Secret.
3. **Permissions:** Ensure your identity (or App Service) has the **Key Vault Secrets User** role.

#### Phase 2: Local Auth

Run `az login` in your terminal to allow the `SecretVaultService` to access Azure during local development.

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

To see a full end-to-end implementation of the multi-tenant identity flow, refer to the following projects in this repository:

* **`UmbracoPrism.TestSite`**: A reference Umbraco v17 implementation showing how to configure the OIDC pipeline and call secure downstream services.
* **`UmbracoPrism.MockBackOffice`**: A standalone minimal API project that demonstrates the use of `AddPrismAuthentication` and the `PrismTenantResolver` to isolate data across hundreds of tenants.

---

## Technical Stack

* **Umbraco:** v17.0+
* **Framework:** .NET 10.0
* **Security:** Azure Key Vault, Managed Identity, Stateless OIDC (CIAM), **Multi-tenant JWT Bearer validation**