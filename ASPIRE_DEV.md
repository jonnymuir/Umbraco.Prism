# Umbraco Prism — Aspire Dev Environment

Press-play local development with Keycloak OIDC authentication.

## Prerequisites (One-Time Setup)

- **Docker Desktop** — must be running ([Download](https://www.docker.com/products/docker-desktop/))
- **.NET Aspire workload:** `dotnet workload install aspire`
- **Node.js 20+** — for frontend assets ([Download](https://nodejs.org/))
- **Frontend dependencies:** `cd src/UmbracoPrism.Client && npm install`

## Quick Start

```bash
# From the repository root
dotnet run --project src/UmbracoPrism.AppHost
```

This launches:
- **Aspire Dashboard** at `https://localhost:17214` (telemetry, logs, resources)
- **Keycloak** at `http://localhost:8080` (OIDC provider)
- **TestSite** at assigned port (check Aspire dashboard for URL)

## What Gets Configured

- **Keycloak realm:** `prism-dev`
- **Client ID:** `prism-client`
- **Client secret:** `prism-dev-secret`
- **Demo user:** `demo@prism.local` / `password`
- **Keycloak admin:** `admin` / `admin`

The realm configuration is imported from `keycloak/realm-export.json`.

## Localhost Tenant (Auto-Seeded)

When TestSite starts in Development mode, a `DemoTenantSeeder` automatically creates the localhost tenant pointing at Keycloak — no manual database setup required. The seeder is idempotent: if a localhost tenant already exists, it is left untouched.

## Entra Tenants (Existing Behavior)

Tenants with `EntraTenantId` set continue to use Entra ID authentication via Azure Key Vault. The OIDC columns are optional — if `OidcAuthority` is null, the system falls back to constructing the Entra authority from `EntraTenantId`.

## Architecture

### New Columns

- **`OidcAuthority`** (nullable string): Full OIDC authority URL (e.g., `http://localhost:8080/realms/prism-dev`). When set, overrides Entra-specific authority construction.
- **`OidcClientId`** (nullable string): OIDC client ID for generic providers.
- **`OidcClientSecret`** (nullable string): OIDC client secret. For local dev only — production should use environment variables.

### PrismOidcConfiguration Fallback Logic

```csharp
if (!string.IsNullOrEmpty(tenant.OidcAuthority))
{
    // Use generic OIDC provider (Keycloak, Okta, etc.)
    authority = tenant.OidcAuthority;
    clientId = tenant.OidcClientId;
    secret = tenant.OidcClientSecret;
}
else if (!string.IsNullOrEmpty(tenant.EntraTenantId))
{
    // Use Entra ID (existing path with Key Vault)
    authority = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/v2.0";
    clientId = tenant.EntraClientId;
    secret = await vault.GetSecretAsync(tenant.SecretKeyName);
}
```

## Projects

- **`UmbracoPrism.AppHost`** — Aspire orchestrator (dev-only, not for production)
- **`UmbracoPrism.ServiceDefaults`** — Shared Aspire extensions (OpenTelemetry, health checks, service discovery)

## Migration

The `AddOidcAuthorityColumns` migration adds the new columns to the `prismTenants` table. It runs automatically on TestSite startup.

## Troubleshooting

**Keycloak not starting:**
- Check the Aspire dashboard for Keycloak logs
- Verify the realm export path is correct: `../../keycloak/realm-export.json` (relative to AppHost project)

**TestSite can't reach Keycloak:**
- Ensure the `OidcAuthority` uses `http://localhost:8080` (not `host.docker.internal` — we're running locally, not in containers)

**Token validation fails:**
- Check that the tenant hostname matches the request (e.g., `localhost:5000` not `127.0.0.1:5000`)
- Verify the `OidcAuthority`, `OidcClientId`, and `OidcClientSecret` match the Keycloak realm configuration
