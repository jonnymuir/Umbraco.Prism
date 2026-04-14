# Keycloak Local Development Realm

This directory contains the Keycloak realm configuration for local development testing of generic OIDC flows.

## Realm Export

**File:** `realm-export.json`

**Purpose:** Configures a `prism-dev` realm with a confidential client `prism-client` for localhost testing.

## Client Configuration

**Client ID:** `prism-client`  
**Client Secret:** `prism-dev-secret` (inline secret, only for localhost demo)

### Scopes

The client is configured with the following scopes:

**Default Scopes:**
- `openid` (implicit)
- `profile`
- `email`
- `roles`
- `acr`
- `web-origins`

**Optional Scopes:**
- `address`
- `phone`
- `microprofile-jwt`

### Redirect URIs

- `http://localhost:9250/signin-oidc`
- `https://localhost:44345/signin-oidc`
- `http://localhost:9250/signout-callback-oidc`
- `https://localhost:44345/signout-callback-oidc`

## Important Notes

1. **Refresh tokens:** Keycloak provides session-bound refresh tokens by default with the standard `openid profile` scopes. `offline_access` is intentionally not configured for this demo client because it would trigger offline token mode, which requires additional client permissions.

2. **Inline secret:** The client secret is stored directly in the realm export for convenience. This is acceptable for the localhost demo tenant only. Production tenants must use Azure Key Vault.

3. **Realm import:** When running Keycloak locally, import this realm via the admin console or using the `-Dkeycloak.import` flag.

## Testing

To test OIDC flows against this realm, ensure:
- Keycloak is running and accessible at the configured authority URL
- The `localhost` tenant in the database points to this realm's authority and client ID
- The test site is running on one of the configured redirect URIs
