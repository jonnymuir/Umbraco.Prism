# Decision: Wrap IDocumentRetriever for full backchannel JWKS coverage

**Date:** 2026-05-02
**Author:** Blathers (Backend Dev)
**Branch:** `fix/codespaces-401-downstream-auth`
**Commit:** `4a47acc`

## Context

Copper's `e0e8ee3` routed the refresh-token grant through `KEYCLOAK_BACKCHANNEL_URL`.
Once a refreshed bearer token reaches MockBusinessApp, JWT validation triggers a JWKS
fetch. The metadata URL was already rewritten in `PrismAuthExtensions.ResolveSigningKeys`,
but `OpenIdConnectConfigurationRetriever` follows `jwks_uri` from the discovery doc —
which Keycloak emits as the public Codespace URL (`KC_HOSTNAME`). That second call hit
the GitHub port-forwarding proxy → `HTTP 401 / www-authenticate: tunnel`.

## Decision

Introduce `BackchannelRewritingDocumentRetriever` (private sealed class in
`PrismSigningKeyCache`) that wraps `IDocumentRetriever` and rewrites any URL whose
origin matches the public Keycloak origin to the internal backchannel base before
delegating. Wire it into the generic `WarmAsync` overload when:

- `KEYCLOAK_BACKCHANNEL_URL` is set, AND
- `ASPNETCORE_ENVIRONMENT == Development`

Production path (no env var, or non-Development) uses the existing injectable factory
unchanged — zero behaviour change for production.

## Alternatives Considered

1. **Post-process `OpenIdConnectConfiguration.JwksUri`** — not possible; the property
   is read from the fetched document and the JWKS GET has already fired by the time
   `GetConfigurationAsync` returns.

2. **Pass public+backchannel bases into `WarmAsync` signature** — would require changing
   the `IPrismSigningKeyCache` interface and all callers. Deemed too invasive for a
   targeted Codespaces fix.

3. **Change `_configurationManagerFactory` signature** — would break the internal test
   constructor seam without meaningful benefit. The retriever wrapper is a more targeted
   interception point.

## Security Constraints Upheld

- No `RequireHttpsMetadata = false`
- No `ValidateIssuer = false` / `ValidateAudience = false`
- No `ServerCertificateCustomValidationCallback => true`
- `normalizedKey` (public OidcAuthority URL) remains the issuer trust anchor
- Existing fail-loud check at `MockBusinessApp/Program.cs:38-41` untouched

## Impact

- **Files changed:** 1 (`src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs`)
- **Tests:** 631 passed, 0 failed (no regressions)
- **Build:** 0 errors
