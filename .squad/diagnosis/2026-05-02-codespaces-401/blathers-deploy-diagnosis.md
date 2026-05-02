# Codespaces 401 — Deploy delta diagnosis (Blathers)

## TL;DR

The `KEYCLOAK_BACKCHANNEL_URL` fix is **half-applied** for MockBusinessApp JWT validation. It patches the OpenID Configuration discovery URL but not the `jwks_uri` that `OpenIdConnectConfigurationRetriever` follows from that document. In Codespaces, Keycloak's discovery document (returned even from `http://localhost:8080`) contains a `jwks_uri` pointing to the **public** Codespace URL (`https://{name}-8443.app.github.dev/…`). That outbound HTTPS fetch from within the Codespace is blocked by the GitHub forwarded-port proxy (the same class of failure the backchannel URL was introduced to fix). No signing keys → MockBusinessApp JWT validation fails → 401.

---

## What's running where (local vs Codespaces — concrete service inventory)

| Service | Local | Codespaces |
|---|---|---|
| **Keycloak** | Docker container, `http://localhost:8080`, proxied via YARP at `https://localhost:8443` | Same Docker container, same ports. Port 8080 `private`; port 8443 forwarded `public` |
| **keycloak-proxy** (YARP) | `https://localhost:8443` | `https://{name}-8443.app.github.dev` (Codespaces forwards port 8443, `public`) |
| **MockBusinessApp** | `https://localhost:7245` / `http://localhost:5163` | Same localhost addresses. Port 7245 forwarded `public` |
| **TestSite** | `https://localhost:44345` | `https://{name}-44345.app.github.dev`, port 44345 `public` |
| **Aspire Dashboard** | `https://localhost:17214` | `http://localhost:15135` (HTTP, anonymous via `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`) |

All four services **are running** in Codespaces — the `on-start.sh` health checks confirm MockBusinessApp on 7245 is reachable (a 401 response satisfies `curl -w "%{http_code}" … -lt 500`). Keycloak is real (not stubbed). Port 8080 is private/internal.

**Codespaces-specific env vars set by AppHost** (`src/UmbracoPrism.AppHost/Program.cs`):

| Variable | Target | Value |
|---|---|---|
| `PrismBusinessApp__Tenants__2__OidcAuthority` | MockBusinessApp | `https://{name}-8443.app.github.dev/realms/prism-dev` |
| `KEYCLOAK_URL` | TestSite | `https://{name}-8443.app.github.dev` |
| `TESTSITE_PUBLIC_URL` | TestSite | `https://{name}-44345.app.github.dev` |
| `KEYCLOAK_BACKCHANNEL_URL` | TestSite + MockBusinessApp | `keycloak.GetEndpoint("http")` → `http://localhost:8080` |
| `PrismBusinessApp__WorkflowApiBaseUrl` | TestSite | **`https://localhost:7245`** — hardcoded, never Codespace-aware (AppHost line 31 + 110) |

---

## The downstream-demo call path (frontend → API, with URLs and token attachment points)

1. **Browser → TestSite**: `fetch('/api/prism/downstream-demo', { credentials: 'include' })` — relative URL, always hits the TestSite host. Source: `src/UmbracoPrism.TestSite/Views/memberDashboard.cshtml:175`.

2. **TestSite: `DownstreamDemoController.Get()`** (`src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`):
   - Line 42: `IsDemoEnabled()` — returns true if `environment.IsDevelopment()` (from launch profile `ASPNETCORE_ENVIRONMENT=Development`) **OR** `Prism:EnableDownstreamDemo=true`.
   - Line 61: `prismContext.GetAuthorizationHeaderAsync()` — retrieves Bearer token from the member's OIDC cookie; force-refreshes via Keycloak's token endpoint (backchannel) if expired.
   - Line 65: `BuildTargetUrl(null)` → reads `PrismBusinessApp:WorkflowApiBaseUrl` = `"https://localhost:7245"` → target = `"https://localhost:7245/api/backoffice/me"`.

3. **TestSite → MockBusinessApp**: Named client `"prism-downstream-demo"` (no special SSL config — relies on dotnet dev-certs trust) calls `https://localhost:7245/api/backoffice/me` with `Authorization: Bearer <token>`.

4. **MockBusinessApp: JWT Bearer validation** (`src/UmbracoPrism.MockBusinessApp/Program.cs` + `src/UmbracoPrism.Core/Extensions/PrismAuthExtensions.cs`):
   - `IssuerSigningKeyResolver` → `PrismAuthExtensions.ResolveSigningKeys` (Shared/Extensions, line ~228)
   - Looks up tenant by `token.iss` vs `OidcAuthority`; in Codespaces both = `https://{name}-8443.app.github.dev/realms/prism-dev` ✓
   - Builds `metadataAddress` from `KEYCLOAK_BACKCHANNEL_URL`:
     ```
     http://localhost:8080/realms/prism-dev/.well-known/openid-configuration
     ```
   - Calls `PrismSigningKeyCache.WarmAsync(cacheKey, metadataAddress, …)` (`src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs:111`).
   - Inside WarmAsync: `ConfigurationManager<OpenIdConnectConfiguration>` + `OpenIdConnectConfigurationRetriever` → fetches discovery doc from `http://localhost:8080/…` (OK) then **follows `jwks_uri`** from that document.

5. **The gap**: Keycloak's discovery document — even when fetched from `http://localhost:8080` — contains ALL URLs using `KC_HOSTNAME` (`{name}-8443.app.github.dev`). So:
   ```
   jwks_uri: "https://{name}-8443.app.github.dev/realms/prism-dev/protocol/openid-connect/certs"
   ```
   `OpenIdConnectConfigurationRetriever` calls the same `HttpDocumentRetriever` to GET this URL. That outbound call exits the Codespace through GitHub's proxy — the same proxy the existing backchannel comment (`AppHost/Program.cs:120-130`) identifies as blocking unauthenticated server-side requests. If it fails: `config.SigningKeys` is empty or an exception is thrown → `SecurityTokenSignatureKeyNotFoundException` → 401.

---

## Hypotheses ranked by likelihood

### 1. ★★★★★ JWKS fetch from external Codespace URL is blocked (backchannel gap)

`KEYCLOAK_BACKCHANNEL_URL` substitution in `PrismAuthExtensions.ResolveSigningKeys` (`Shared/Extensions/PrismAuthExtensions.cs:228-235`) replaces the **discovery document** URL with the backchannel address. But `OpenIdConnectConfigurationRetriever` then fetches `jwks_uri` from the same (external) URL found in the document. This second HTTP call is NOT rerouted through the backchannel. It exits through GitHub's port-forwarding proxy, which blocks unauthenticated server-side calls — the documented failure mode `AppHost/Program.cs:120-123` was written to solve for the discovery fetch, but the JWKS fetch is still exposed.

### 2. ★★★☆☆ `prism-downstream-demo` HttpClient cannot reach `https://localhost:7245` (SSL trust)

On Ubuntu 24.04, `dotnet dev-certs https --trust` (run in `on-create.sh`) may not add the dev cert to .NET's own trust store reliably. If `https://localhost:7245` is untrusted, the `"prism-downstream-demo"` client throws `HttpRequestException`. This would surface as `statusCode: 0 / "Network Error"` in the UI, **not** 401 — making it a secondary concern. But worth confirming.

### 3. ★★☆☆☆ `IsDemoEnabled()` returns false (ASPNETCORE_ENVIRONMENT not propagated)

If Aspire fails to propagate `ASPNETCORE_ENVIRONMENT=Development` from the `"Umbraco.Web.UI"` launch profile to the TestSite process, `environment.IsDevelopment()` returns false and `Prism:EnableDownstreamDemo` (unset in Codespaces appsettings) defaults to false → 403. The JS maps both 401 and 403 to "Your Prism session is no longer valid." Aspire does read launch profile env vars, so this is unlikely but unconfirmed.

### 4. ★★☆☆☆ Token refresh fails before MockBusinessApp is reached

If the Bearer token has expired and the OIDC refresh call from the TestSite fails (e.g., backchannel URL resolves to a slow/timing-sensitive path), `GetAuthorizationHeaderAsync()` returns `null` → controller returns HTTP 401 directly (never calls MockBusinessApp). Less likely if sign-in just completed.

---

## What we'd need to confirm (specific logs / config dumps / curl probes)

1. **JWKS fetch status** — Inside the Codespace terminal:
   ```bash
   # Does the JWKS URL resolve from within the Codespace?
   curl -sI "https://${CODESPACE_NAME}-8443.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN}/realms/prism-dev/protocol/openid-connect/certs"
   ```
   A redirect to a login page (302) or 401 confirms the proxy is blocking. A 200 with JWKS JSON confirms it's reachable.

2. **MockBusinessApp console output** — the `OnAuthenticationFailed` handler in `PrismAuthExtensions.cs:27-68` logs `[PRISM AUTH FAILED]` with exception type, token issuer, and configured authorities. Run:
   ```bash
   grep "PRISM AUTH FAILED" /tmp/prism-apphost.log
   ```
   This gives the exact exception (e.g., `SecurityTokenSignatureKeyNotFoundException` vs `SecurityTokenInvalidIssuerException`).

3. **IsDemoEnabled confirmation** — call the unauthenticated endpoint:
   ```bash
   curl -s "https://${CODESPACE_NAME}-44345.app.github.dev/api/prism/downstream-demo/session-contract"
   ```
   A 403 means the endpoint is disabled; a 200 with JSON means it's enabled.

4. **Signing key cache hit** — add temporary logging in `ResolveSigningKeys` to emit `backchannelBase` and `metadataAddress` values, confirming the backchannel substitution is applied.

5. **Dev cert trust for localhost** — from within the Codespace:
   ```bash
   dotnet dev-certs https --check
   curl -s -o /dev/null -w "%{http_code}" https://localhost:7245
   ```

---

## Suggested fix space (respecting the security bedrock)

**For Hypothesis 1 (primary):** The `PrismSigningKeyCache.WarmAsync` generic OIDC overload (`Shared/Services/PrismSigningKeyCache.cs:111`) must also rewrite the `jwks_uri` through the backchannel, not just the initial metadata address. Options:

- Pass the backchannel base URL into `WarmAsync` and substitute `jwks_uri` before the JWKS fetch (requires a custom `IDocumentRetriever` wrapper that rewrites Keycloak-origin URLs to the backchannel base).
- Or use a single internal `ConfigurationRetriever` that, given both the public authority base and the backchannel base, rewrites ALL Keycloak-origin HTTPS URLs to their HTTP backchannel equivalents before making any HTTP call.

**Security constraint (bedrock):** This rewriting must ONLY apply when `KEYCLOAK_BACKCHANNEL_URL` is set AND the environment is Development. The existing guard in `MockBusinessApp/Program.cs:38-41` enforces this at startup. Any fix must preserve that guard — no exceptions for "just Codespaces" at the production security boundary.

**For Hypothesis 2 (secondary):** Confirm `dotnet dev-certs https --trust` effectiveness on Ubuntu 24.04 in the Codespace; if needed, configure the `"prism-downstream-demo"` named HttpClient to use `DangerousAcceptAnyServerCertificateValidator` only when `IsDevelopment()` — or switch the target URL to the HTTP endpoint (`http://localhost:5163`) in Development.
