# Decision: DefaultAuthenticateScheme must not depend on Prism:VaultUri

**Date:** 2026-04-30  
**Author:** Brewster (Umbraco Platform Specialist)  
**Commit:** 42b85e5

## Context

`PrismComposer` was gating `DefaultAuthenticateScheme = "PrismMemberCookie"` on
`isAuthEnabled = !string.IsNullOrEmpty(builder.Config["Prism:VaultUri"])`.

Security commit `b6336fd` correctly removed `Prism:VaultUri` from `appsettings.json`
(it is a deployment secret, not a source-controlled value). This silently made
`isAuthEnabled = false`, which meant the three auth defaults
(`DefaultAuthenticateScheme`, `DefaultSignInScheme`, `DefaultChallengeScheme`)
were never registered with ASP.NET Core.

**Symptom:** After Keycloak sign-in the browser received `PrismMemberCookie` and
sent it on all subsequent requests. Route-hijacking controllers with
`[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` (e.g. `/dashboard`)
continued to work because they name the scheme explicitly. But the home page
view, which uses `Context.User.Identity.IsAuthenticated` under the default
authentication pipeline, always showed the signed-out state. The Playwright
test saw `/dashboard` → 200 then `/` → 200 (signed out), and timed out waiting
for "Go to Dashboard".

**Root cause confirmed via Playwright network trace:** cookie was sent on both
requests; the server-side issue was that `UseAuthentication()` on the home-page
request used Umbraco's fallback default scheme (not `PrismMemberCookie`), so the
cookie was never decrypted and `User.Identity` was anonymous.

## Decision

**Auth scheme defaults are unconditional.** The vault URI is an optional
secret-provider detail (Azure Key Vault for production; inline secrets for local
dev/CI). Its presence must not gate authentication setup.

Remove the `isAuthEnabled` flag and always call:

```csharp
options.DefaultAuthenticateScheme = "PrismMemberCookie";
options.DefaultSignInScheme = "PrismMemberCookie";
options.DefaultChallengeScheme = "PrismEntraID";
```

## Guidance for future work

- Do not tie authentication enablement to the presence of any secret or
  infrastructure URI in config.
- If Prism auth ever needs to be feature-flagged (e.g. opt-in per environment),
  introduce a dedicated `Prism:AuthEnabled` boolean, defaulting to `true`.
- `Prism:VaultUri` belongs in `appsettings.Local.json` (gitignored) for local
  dev and as a CI/CD secret for production deployments.
