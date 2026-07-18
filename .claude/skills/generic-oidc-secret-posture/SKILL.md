---
name: "generic-oidc-secret-posture"
description: "How to keep generic OIDC confidential-client secrets vault-backed in production while preserving repo-owned local demos"
domain: "authentication"
confidence: "high"
source: "earned"
---

## Context

Use this when Prism supports non-Entra OIDC providers such as Keycloak or Auth0 and a tenant needs a confidential client secret. The challenge is balancing production secret hygiene with the desire for a fresh-clone localhost demo that works immediately.

## Patterns

### Treat generic OIDC client secrets like Entra secrets in production

- If the tenant is a real production or staging tenant, do not persist the raw OIDC client secret in the tenant row.
- Persist a reference/alias instead, and resolve the actual secret through a vault or secret-provider abstraction at runtime.
- In Prism's current contract, that means `OidcClientSecretProvider = "azure-key-vault"` plus `OidcClientSecretReference = "<secret-name>"`.

### Keep inline secrets limited to repo-owned local demos

- A hardcoded secret is acceptable only when the identity provider, client, and credential are all demo-only and shipped explicitly for local development.
- Keep that exception isolated to the local seed path and realm export, not the normal tenant-management path used for real tenants.
- In Prism's current contract, `inline` is valid only for the seeded localhost Keycloak tenant created by `DemoTenantSeeder`.

### Avoid management-API secret echo

- Do not return confidential secret values from tenant listing/edit APIs once a reference-based model exists.
- If an edit form needs to preserve a secret, use explicit replace/reset behavior instead of rehydrating the stored value back into the UI.

### Test for provider metadata, not secret material

- Regression tests should assert on `HasOidcClientSecret`, provider names, and preserve/clear behaviors rather than checking for secret literals.
- UI tests should confirm secret inputs stay blank on edit while still indicating that a secret is configured.

### Separate developer convenience from production posture

- Local press-play flows can use demo credentials, local env vars, or dev-only seed data.
- Production guidance should require a vault/provider-backed secret source and fail closed when a confidential client is configured without one.

### Keep the public management contract stable while refactoring secret storage

- If the UI or management DTO still uses an existing field such as `SecretKeyName` for the admin-entered generic OIDC vault alias, translate that field server-side into the new provider/reference storage model instead of failing the request.
- Do not expose the internal provider/reference persistence details back through list/detail responses just because the backend now stores them separately.

### Fail closed on confidential-client secret resolution

- If a generic OIDC tenant uses authorization-code redemption or refresh flows as a confidential client, runtime code must require a resolvable secret source before attempting token exchange.
- Do not silently fall back to empty-string client secrets, nullable secret values, or "best effort" token redemption for non-demo tenants.
- The only acceptable inline-secret bypass is the explicitly marked localhost/demo seed path.

### Remove secret echo from administrative surfaces

- Tenant list/detail/edit APIs should return metadata or secret-reference state, not the stored secret value.
- Update semantics should distinguish between "leave current secret reference as-is", "replace reference", and "clear reference" rather than round-tripping secret material through the UI payload.

### Make edit forms replace-only when secrets are hidden

- If an edit API stops echoing generic OIDC secret material (or even safe references), keep the admin field blank and label it as a replace-only action.
- Pair blank replace fields with an explicit reset affordance and a current-state hint such as "Azure Key Vault reference configured" or "localhost demo inline secret configured".
- Reserve inline replace inputs for the repo-owned localhost demo path; production/admin flows should steer users toward Key Vault references first.

## Examples

- `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs` and `src/UmbracoPrism.Core/Models/PrismContext.cs` both resolve generic OIDC secrets through `ISecretVaultService.ResolveSecretAsync(provider, reference)` and fail closed when resolution is missing.
- `src/UmbracoPrism.Core/Controllers/TenantManagementController.cs` writes `OidcClientSecretProvider`/`OidcClientSecretReference`, returns `HasOidcClientSecret`, and rejects inline secrets on the normal management path.
- `src/UmbracoPrism.TestSite/DemoTenantSeeder.cs` is the explicit localhost exception: it seeds `OidcClientSecretProvider = "inline"` with the repo-owned Keycloak secret so fresh clones still work immediately.

## Anti-Patterns

- Storing production generic OIDC client secrets directly in `prismTenants`.
- Returning stored secrets in list/edit APIs and hydrating them back into admin forms.
- Redeeming generic OIDC authorization codes with `tenant.OidcClientSecret ?? string.Empty` or any other soft-fail secret fallback.
- Requiring Azure Key Vault specifically for local development when a repo-owned demo secret already satisfies the dev scenario.
