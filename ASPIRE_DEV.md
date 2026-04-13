# Umbraco Prism — Aspire Dev Environment

Press-play local development with Keycloak OIDC authentication.

## Prerequisites (One-Time Setup)

- **.NET 10 SDK** — install the current .NET 10 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Trust the .NET dev certificate** — run `dotnet dev-certs https --trust` (one-time setup)
- **Docker Desktop** — must be running ([Download](https://www.docker.com/products/docker-desktop/))
- **Node.js 20+** — for frontend assets ([Download](https://nodejs.org/))
- **Frontend dependencies:** `cd src/UmbracoPrism.Client && npm install`

## Quick Start

```bash
# From the repository root
dotnet run --project src/UmbracoPrism.AppHost
```

In VS Code, use **C#: Aspire (Full Stack)**. Its pre-launch task now checks for the .NET 10 SDK and Docker first. This repo uses the Aspire AppHost SDK and NuGet packages, so you do **not** need to run `dotnet workload install aspire`.

This launches:
- **Aspire Dashboard** at `https://localhost:17214` (telemetry, logs, resources)
- **Keycloak** at `https://localhost:8443` (OIDC provider with TLS proxy)
- **TestSite** at `https://localhost:44345` and `http://localhost:9250`

## What Gets Configured

- **Keycloak realm:** `prism-dev`
- **Client ID:** `prism-client`
- **Client secret:** `prism-dev-secret`
- **Demo user:** `demo@prism.local` / `password`
- **Keycloak admin:** `admin` / `admin`

The realm configuration is imported from `keycloak/realm-export.json`.
The AppHost includes a lightweight HTTPS reverse proxy (`UmbracoPrism.KeycloakProxy`) that terminates TLS at `https://localhost:8443` and forwards requests to Keycloak's HTTP endpoint on port 8080. The proxy uses the .NET development certificate that is already trusted on most dev machines (via `dotnet dev-certs https --trust`). This setup ensures Safari/WebKit-compatible authentication flows that require secure cookies while keeping Keycloak's own configuration simple.
For localhost auth, the Keycloak client is pinned to those two TestSite URLs because Keycloak does not accept `localhost:*` port wildcards for redirect URI validation.

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

**`CliPath` / `DashboardPath` validation error on startup:**
- Pull the latest repo changes so the AppHost project includes the Aspire AppHost SDK
- Ensure the .NET 10 SDK is installed and Docker Desktop is running
- Rerun **C#: Aspire (Full Stack)**; no separate Aspire workload install is required

**Keycloak not starting:**
- Check the Aspire dashboard for Keycloak logs
- Verify the realm export path is correct: `../../keycloak/realm-export.json` (relative to AppHost project)
- On Apple Silicon Macs, the AppHost now adds `JAVA_OPTS_APPEND=-XX:UseSVE=0` for the Keycloak container to avoid the known OpenJDK 21 `SIGILL` crash during JVM startup on affected ARM64 Docker environments

**TestSite can't reach Keycloak:**
- Ensure the `OidcAuthority` uses the AppHost-provided `KEYCLOAK_URL` (currently `https://localhost:8443` under Aspire, or `https://localhost:8443` when running TestSite standalone)
- Verify the .NET dev certificate is trusted: `dotnet dev-certs https --trust`
- If Keycloak shows `Invalid parameter: redirect_uri`, recreate the local Keycloak realm/container so it re-imports the exact localhost redirect URIs from `keycloak/realm-export.json`

**Token validation fails:**
- Check that the tenant hostname matches the request (e.g., `localhost:5000` not `127.0.0.1:5000`)
- Verify the `OidcAuthority`, `OidcClientId`, and `OidcClientSecret` match the Keycloak realm configuration
