# Copilot Instructions for Umbraco Prism

## Quick Start

**Umbraco Prism** is a multi-tenancy package for Umbraco v17+ with dynamic branding, stateless OIDC identity, and a **Produce Mobile** feature that generates native-shell app starters from Backoffice settings.

**Prerequisites:** .NET 10.0.x, Node.js 22.17.1

## Build & Test

### .NET Core

```bash
# Build (Debug)
dotnet build UmbracoPrism.sln

# Build (Release)
dotnet build UmbracoPrism.sln -c Release

# Run all Core tests
dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests

# Run single Core test
dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests.TestClass.TestMethod

# Vulnerability scan
dotnet list src/UmbracoPrism.Core/UmbracoPrism.Core.csproj package --vulnerable --include-transitive

# Pack NuGet
dotnet pack src/UmbracoPrism.Core/UmbracoPrism.Core.csproj -c Release -o artifacts
```

### Client (Web Components)

```bash
cd src/UmbracoPrism.Client

# Install dependencies
npm install

# Build static assets
npm run build

# Storybook (local development)
npm run storybook

# Storybook tests (single browser)
npm run test-storybook

# Storybook tests (all browsers + WCAG checks)
npm run test-storybook:all

# Storybook tests (CI mode: auto-start server, all browsers)
npm run test-storybook:ci:all

# Playwright tests (direct, interactive UI)
npm run test:playwright:ui

# Run single Playwright test by name
npx playwright test tests/prism-create-tenant-modal.spec.ts -g "Create modal tabs switch and content has height"
```

Note: No linting configured. Accessibility checks run via Storybook's axe integration (WCAG 2.0/2.1).

## Architecture

### Middleware & Runtime
- **PrismTenantMiddleware:** Resolves hostname → Tenant Cache
- **PrismBrandingMiddleware:** Injects CSS variable overrides
- **IPrismContext** (scoped): Holds current tenant & theme per-request

### Identity (Stateless OIDC)
- **Dynamic OIDC:** Tenant-specific ClientId, Authority swapped per request via `PrismOidcConfiguration`
- **IPrismUserContext:** High-performance access to user claims + tenant
- **SecretVaultService:** Fetches OIDC secrets from Azure Key Vault (Managed Identity in prod, CLI in dev)
- **PrismTokenService:** Token extraction & refresh
- **Downstream Auth:** Secure token propagation to internal APIs via `AddPrismAuthentication` on receiving end

### Persistence
Located in `src/UmbracoPrism.Core/Persistence/`:
- **PrismMigrationPlan:** Defines migrations (AddIdentityColumns, AddMobileAppConfigColumn, etc.)
- **PrismTenantSchema:** Database schema for tenants, branding, mobile settings
- **TenantService:** CRUD for tenants
- **BrandingService:** Dynamic branding overrides
- **MobileBundleService:** Generates Capacitor bundles with tenant settings

### Services
Located in `src/UmbracoPrism.Core/Services/`:
- `TenantService` – Tenant CRUD & domain resolution
- `BrandingService` – CSS variable management
- `MobileBundleService` – App bundle generation (iOS/Android)
- `SecretVaultService` – Azure Key Vault integration

### Authorization
Located in `src/UmbracoPrism.Core/Auth/`:
- **PrismAdminHandler / PrismAdminRequirement:** Restricts tenant management to configured admin groups (default: `["admin"]`)
- **PrismTenantHandler / PrismTenantRequirement:** Ensures authenticated users within tenant context

### Backoffice (Web Components)
Located in `src/UmbracoPrism.Client/`:
- **Storybook** for component-driven development and accessibility (axe WCAG 2.0/2.1)
- **Playwright** for end-to-end tests
- Static assets deployed to `App_Plugins/UmbracoPrism` (configured in Core.csproj `StaticWebAssetBasePath`)

## Key Conventions

### C# Organization
- **Middleware/** – Request pipeline handlers
- **Services/** – Business logic (tenant, branding, mobile, vault)
- **Models/** – Data models (Tenant, Theme, MobileAppConfig)
- **Persistence/** – Database schema and migrations
- **Auth/** – Authorization handlers and policies
- **Controllers/** – Management API endpoints
- **TagHelpers/** – Razor helpers (e.g., `<prism-debug />`)

### Naming
- **Interfaces:** `IPrismXxx` (e.g., `IPrismContext`)
- **Services:** `XxxService` (e.g., `TenantService`)
- **Middleware:** `PrismXxxMiddleware` (e.g., `PrismTenantMiddleware`)
- **Models:** `PrismXxx` (e.g., `PrismTenant`)

### Configuration
- **Settings:** `appsettings.json` under `"Prism"` section
- **Key settings:**
  - `Prism.VaultUri` – Azure Key Vault URI (triggers auth activation)
  - `Prism.AdminGroups.GroupAliases` – Admin user groups (default: `["admin"]`)

### Testing
- **Core:** XUnit, Moq, FluentAssertions
- **Client:** Playwright with Storybook test runner, axe for accessibility
- **Test project:** `UmbracoPrism.Core.Tests`

### Database & Migrations
- Migrations applied automatically by `PrismMigrationHandler` on startup
- Schema: TenantId, DomainName, ClientId, Secret key ref, Branding (JSON), MobileAppConfig (JSON), MobileBrandingOverrides (JSON)
- No local Member records (stateless; identity via Entra ID/CIAM)

### Mobile Feature
- **Produce Mobile Tab:** Backoffice UI → Capacitor bundle download
- **Generated bundle:** `capacitor.config.ts`, `package.json`, `www/mobile-overrides.css`, bootstrap scripts, `AGENT_PROMPT.md`
- **Mobile detection:** Query flag (`?prismMobile=1`), user-agent marker (`PrismMobile`), or cookie
- **Safe-area support:** CSS class `prism-mobile` for notched devices

### Security & Secrets
- **OIDC Secrets:** Stored in Azure Key Vault; fetched at runtime via `SecretVaultService`
- **Local Dev:** Use `az login` to authenticate with Azure
- **Production:** Managed Identity (App Service)
- **No hardcoded secrets:** All CIAM credentials retrieved dynamically per tenant


