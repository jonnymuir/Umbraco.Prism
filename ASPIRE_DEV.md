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
- **Keycloak proxy** at `https://localhost:8443` (browser-facing OIDC endpoint shown in Aspire)
- **TestSite** at `https://localhost:44345` and `http://localhost:9250`
- **MockBusinessApp** at `https://localhost:7245` and `http://localhost:5163`

## What Gets Configured

- **Keycloak realm:** `prism-dev`
- **Client ID:** `prism-client`
- **Client secret:** `prism-dev-secret`
- **Demo user:** `demo@prism.local` / `password`
- **Keycloak admin:** `admin` / `admin`

The realm configuration is imported from `keycloak/realm-export.json`.
The AppHost includes a lightweight HTTPS reverse proxy (`UmbracoPrism.KeycloakProxy`) that terminates TLS at `https://localhost:8443` and forwards requests to Keycloak's HTTP endpoint on port 8080. The proxy uses the .NET development certificate that is already trusted on most dev machines (via `dotnet dev-certs https --trust`). This setup ensures Safari/WebKit-compatible authentication flows that require secure cookies while keeping Keycloak's own configuration simple.
The MockBusinessApp now defaults to its `https` launch profile, so both the workflow engine and the dashboard's downstream demo call the same trusted localhost origin (`https://localhost:7245`) that Aspire advertises.
For localhost auth, the Keycloak client is pinned to the TestSite sign-in and sign-out callback URLs because Keycloak does not accept `localhost:*` port wildcards for redirect URI validation.
The local Prism login flow intentionally requests standard OIDC scopes (`openid profile`) from Keycloak instead of `offline_access`. Keycloak still issues a normal session refresh token for code flow, and fresh clones do not need extra offline-token grants on the user or client.

## Localhost Tenant (Auto-Seeded)

When TestSite starts in Development mode, a `DemoTenantSeeder` automatically creates the localhost tenant pointing at Keycloak — no manual database setup required. The seeder is idempotent: if a localhost tenant already exists, it is left untouched.

The seeded tenant is the only supported inline-secret exception:
- `Hostname = "localhost"`
- `OidcAuthority = "https://localhost:8443/realms/prism-dev"`
- `OidcClientId = "prism-client"`
- `OidcClientSecretProvider = "inline"`
- `OidcClientSecretReference = "prism-dev-secret"` (repo-owned demo marker)

At runtime, Prism allows inline generic OIDC secrets only for that repo-owned localhost Keycloak path. Any other generic OIDC tenant must resolve through a managed provider such as Azure Key Vault, otherwise token exchange fails closed.

## Entra Tenants (Existing Behavior)

Tenants with `EntraTenantId` set continue to use Entra ID authentication via Azure Key Vault. The generic OIDC columns are optional — if `OidcAuthority` is null, the system falls back to constructing the Entra authority from `EntraTenantId`.

## Architecture

### Secret Model

- **`OidcAuthority`** (nullable string): Full OIDC authority URL (e.g., `https://localhost:8443/realms/prism-dev`). When set, overrides Entra-specific authority construction.
- **`OidcClientId`** (nullable string): OIDC client ID for generic providers.
- **`OidcClientSecretProvider`** (nullable string): Canonical secret provider name. Prism currently supports `azure-key-vault` for normal tenants and `inline` only for the repo-owned localhost demo.
- **`OidcClientSecretReference`** (nullable string): Provider-specific alias/reference. For Key Vault this is the secret name; for the localhost demo it is the repo-owned inline secret.
- **`OidcClientSecret`** (legacy nullable string): Back-compat storage for older demo rows. `TenantService` maps it onto the new provider/reference model as `inline`.

### PrismOidcConfiguration Resolution Logic

```csharp
if (!string.IsNullOrEmpty(tenant.OidcAuthority))
{
    authority = $"{tenant.OidcAuthority}/protocol/openid-connect/token";
    clientId = tenant.OidcClientId;
    secret = await PrismOidcConfiguration.ResolveClientSecretAsync(tenant, vault);
}
else if (!string.IsNullOrEmpty(tenant.EntraTenantId))
{
    authority = $"https://{tenant.EntraTenantId}.ciamlogin.com/{tenant.EntraTenantId}/oauth2/v2.0/token";
    clientId = tenant.EntraClientId;
    secret = await vault.GetSecretAsync(tenant.SecretKeyName);
}
```

### Management API / Backoffice

- Tenant list/edit responses expose `OidcClientSecretProvider` and `HasOidcClientSecret`, never the raw secret value or the reference name.
- **Request Contract:** POST/PUT accept `OidcClientSecretProvider` (string, e.g., `"azure-key-vault"`) and `OidcClientSecretReference` (string, the vault secret name). Setting `ResetOidcClientSecret = true` clears the stored secret configuration.
- **Security:** Inline secrets are rejected on the normal management path unless the tenant matches the repo-owned localhost demo identity.
- **Backend:** The backoffice edit form shows whether a secret exists but does not echo the reference. Updating a tenant without filling the reference field preserves the stored configuration.

## Projects

- **`UmbracoPrism.AppHost`** — Aspire orchestrator (dev-only, not for production)
- **`UmbracoPrism.ServiceDefaults`** — Shared Aspire extensions (OpenTelemetry, health checks, service discovery)

## Migration

The OIDC migrations add the authority/client columns and the provider/reference columns to the `prismTenants` table. They run automatically on TestSite startup.

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

**Dashboard workflow or downstream API demo can't reach MockBusinessApp:**
- Ensure `PrismBusinessApp:WorkflowApiBaseUrl` resolves to `https://localhost:7245`
- If running the Business App outside Aspire, `dotnet run --project src/UmbracoPrism.MockBusinessApp` now uses the HTTPS launch profile by default
- Verify the .NET dev certificate is trusted: `dotnet dev-certs https --trust`
- Keep the MockBusinessApp's trusted OIDC issuer aligned with the browser-facing Keycloak proxy (`https://localhost:8443/realms/prism-dev`), not Keycloak's internal container URL on port 8080
- If `/api/backoffice/me` still returns a bare `401 Unauthorized` after pulling the latest auth fixes, fully restart the running MockBusinessApp resource (or restart the Aspire AppHost session). A stale process can keep the old JWT validation settings even though the repo now accepts the same Keycloak token without any database reset, realm re-import, or sign-in flow changes.

**Token validation fails:**
- Check that the tenant hostname matches the request (e.g., `localhost:5000` not `127.0.0.1:5000`)
- Verify the `OidcAuthority`, `OidcClientId`, and generic secret provider/reference match the Keycloak realm configuration
- For the localhost demo tenant, the generic secret should resolve through the inline demo path only for the seeded `localhost` tenant

**`Offline tokens not allowed for the user or client`:**
- The local Keycloak demo is expected to work without enabling offline tokens
- Pull the latest repo changes so Prism no longer requests `offline_access` for the generic Keycloak tenant during the localhost login flow

**Keycloak shows `Missing parameters: id_token_hint` on logout:**
- Pull the latest repo changes so Prism persists the OIDC `id_token` in the member cookie and sends it back on RP-initiated logout
- Recreate the local Keycloak realm/container if needed so it re-imports the registered `signout-callback-oidc` redirect URIs from `keycloak/realm-export.json`
