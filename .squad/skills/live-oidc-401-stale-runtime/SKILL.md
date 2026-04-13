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

## Anti-Patterns

- Assuming a persistent 401 after a fix always means the code change was incomplete.
- Resetting SQLite or re-importing Keycloak before proving whether the same token works on a fresh current build.
- Relying only on unit tests when the suspected issue is a stale long-running local process.
