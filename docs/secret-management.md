# Managing OIDC Client Secrets in Prism

**Audience:** DevOps, SRE, and administrators configuring production tenants.

Prism uses a **provider/reference secret model** for confidential OIDC clients. Production tenants store only a secret reference in Prism; the runtime resolves the actual value through `ISecretVaultService`. The repo-owned localhost Keycloak tenant is the only supported inline-secret exception.

---

## Overview

This model:
- keeps raw secrets out of management responses
- centralizes rotation in Azure Key Vault
- preserves the fresh-clone localhost Keycloak demo
- fails closed when a generic OIDC confidential client has no resolvable secret

---

## Secret Paths

### 1. Entra ID tenants

- Store the app secret in Azure Key Vault
- Set `SecretKeyName` to the vault secret name
- Runtime resolves it with `GetSecretAsync(tenant.SecretKeyName)`

### 2. Generic OIDC tenants (production/staging)

- Store the client secret in Azure Key Vault
- Set:
  - `OidcClientSecretProvider = "azure-key-vault"`
  - `OidcClientSecretReference = "<vault-secret-name>"`
- Runtime resolves it with `ResolveSecretAsync(provider, reference)`
- Never use inline secrets for these tenants

### 3. Repo-owned localhost Keycloak demo

- Applies only to the seeded localhost tenant:
  - `Hostname = "localhost"`
  - `OidcAuthority = "https://localhost:8443/realms/prism-dev"`
  - `OidcClientId = "prism-client"`
- This path may use:
  - `OidcClientSecretProvider = "inline"`
  - `OidcClientSecretReference = "prism-dev-secret"`
- Older seeded rows may still use the legacy `OidcClientSecret` column; `TenantService` maps that to the inline provider model at runtime

---

## Management API Contract

The management API is intentionally non-secret-bearing. The tenant management endpoints (GET /api/prism/tenants, POST, PUT) never expose secret values or reference names.

### Responses

Tenant responses expose:
- `OidcClientSecretProvider`, the provider name (e.g., `"azure-key-vault"`)
- `HasOidcClientSecret`, boolean indicating whether a secret is configured

Tenant responses do **not** expose:
- raw OIDC client secret values
- `OidcClientSecretReference`, the actual vault secret name

### Requests

POST /api/prism/tenants (create) and PUT /api/prism/tenants/{id} (update) accept:
- `OidcClientSecretProvider`, the provider name (string)
- `OidcClientSecretReference`, the vault secret name (string)
- `ResetOidcClientSecret`, flag to clear secret configuration (boolean)

Updates preserve the existing secret configuration if `OidcClientSecretReference` is not supplied and `ResetOidcClientSecret` is false.

---

## Backoffice Behavior

The backoffice tenant editor integrates with the management API contract:

- **On edit load:** The `OidcClientSecretReference` field displays empty. This is intentional, the UI never reveals what the secret reference is.
- **Existing secret indicator:** The UI shows a label or badge indicating a secret exists (via `HasOidcClientSecret`) without echoing the reference name.
- **Clear or replace:** Sending `ResetOidcClientSecret = true` clears the configuration. Updating the reference field with a new value replaces it.
- **Preserve on blank update:** If you omit the reference field during an update, the existing configuration is preserved.

This aligns with the security posture: admins confirm **that** a secret is configured without seeing **what** it is.

---

## Production Setup Checklist

1. Create the tenant with:
   - `OidcAuthority`
   - `OidcClientId`
2. Store the client secret in Azure Key Vault
3. Save the generic OIDC secret reference via:
   - `OidcClientSecretProvider = "azure-key-vault"`
   - `OidcClientSecretReference = "<vault-secret-name>"`
4. Verify sign-in succeeds
5. Rotate the value in Key Vault when needed

---

## Example Tenant Request

Create a production generic OIDC tenant with vault-backed secret (POST /api/prism/tenants):

```json
{
  "name": "Acme Production",
  "hostname": "acme.example.com",
  "oidcAuthority": "https://auth.acme.com/realms/acme",
  "oidcClientId": "acme-prism-prod",
  "oidcClientSecretProvider": "azure-key-vault",
  "oidcClientSecretReference": "prism-acme-oidc-secret"
}
```

The response will confirm the secret is configured via `HasOidcClientSecret: true` and `OidcClientSecretProvider: "azure-key-vault"`, but will not return the reference name or any secret value.

---

## FAQ

**Can production generic OIDC tenants use inline secrets?**  
No. Prism rejects inline generic OIDC secrets outside the seeded localhost demo path.

**What happens if a generic OIDC tenant has no resolvable secret?**  
Token exchange fails closed.

**Does the API return secret references?**  
No. It returns provider metadata plus `HasOidcClientSecret`.

**Does the localhost demo still need Key Vault?**  
No. The repo-owned Keycloak tenant remains self-contained for local development.

---

## See Also

- [ASPIRE_DEV.md](ASPIRE_DEV.md)
- [README.md](../README.md)
- [Umbraco Setup](umbraco-setup.md)
