---
name: "live-oidc-401-stale-runtime"
description: "Differentiate a stale local runtime from a real repo auth bug when a downstream OIDC API keeps returning 401"
domain: "authentication"
confidence: "high"
source: "earned"
---

## Context

Use this when a live local environment still returns `401 Unauthorized` from a downstream API even after auth fixes already landed in the repo. This is especially useful for Aspire-managed local stacks where an older child process can keep serving stale JWT validation settings.

## Patterns

### Reproduce with a real token, not just static code review

- Drive a real login flow against the running stack and capture a fresh access token from the configured OIDC provider.
- Call the failing downstream endpoint both through the app's normal path and directly with that bearer token.

### Compare the live advertised instance with a fresh current build

- Start the current app from the worktree on a different local port.
- Reuse the exact same token against the fresh instance.
- If the live port returns `401 invalid_token` but the fresh current build returns `200`, the repo code is already fixed and the running stack is stale.

### Prefer the smallest operational reset

- Restart the affected downstream app resource first.
- Only escalate to full-stack restart if the resource is AppHost-managed or cannot be restarted in isolation.
- Avoid unnecessary DB wipes, realm re-imports, or forced sign-outs when the same token already proves the current code path works.

## Examples

- Live failing path: `https://localhost:44345/api/prism/downstream-demo` -> `https://localhost:7245/api/backoffice/me`
- Fresh comparison instance: `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=https://localhost:9345 dotnet run --project src/UmbracoPrism.MockBusinessApp --no-launch-profile`
- Relevant files: `src/UmbracoPrism.MockBusinessApp/Program.cs`, `src/UmbracoPrism.MockBusinessApp/appsettings.json`, `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`

## Runtime Restart Detection

### Detect pre-restart sessions using IssuedUtc timestamp

- When a protected session should survive an app restart (for example, using `offline_access` refresh tokens), add runtime restart detection to force token refresh on first use after restart.
- Store `AuthenticationProperties.IssuedUtc = DateTimeOffset.UtcNow` when creating the session cookie.
- On each downstream API call, compare `IssuedUtc` against a process-startup timestamp (`ProcessStartedUtc = DateTimeOffset.UtcNow` at class initialization).
- If `IssuedUtc < ProcessStartedUtc`, force a token refresh even if the access token hasn't expired yet.

### Omit scope parameter from refresh calls when using offline_access tokens

- When the initial login requested `offline_access`, the refresh token is already bound to those scopes.
- For Keycloak and similar providers, restating scopes in the refresh call (especially without `offline_access`) can cause rejection.
- The correct pattern: return `null` from `GetRefreshScope()` for tenants using `offline_access`, and skip adding the `scope` parameter to the refresh form data.

## Anti-Patterns

- Assuming a persistent 401 after a fix always means the code change was incomplete.
- Resetting SQLite or re-importing Keycloak before proving whether the same token works on a fresh current build.
- Relying only on unit tests when the suspected issue is a stale long-running local process.
- Adding the same `scope` value to refresh calls that was used in the initial authorization request when using `offline_access` tokens.
