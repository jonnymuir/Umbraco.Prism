# SKILL: Config Presence as Feature Flag — Anti-Pattern

**Domain:** Architecture / Configuration Design  
**Author:** Brewster  
**Date:** 2026-04-30

---

## Summary

Anti-pattern: using the presence of an optional configuration value (especially environment secrets or infrastructure URIs) as a feature flag for foundational subsystems. This pattern is fragile and creates silent failures when secrets are removed or refactored.

---

## The Problem

```csharp
// ❌ BAD: Auth gated on vault URI presence
bool isAuthEnabled = !string.IsNullOrEmpty(config["Prism:VaultUri"]);
if (isAuthEnabled)
{
    options.DefaultAuthenticateScheme = "PrismMemberCookie";
}
```

When a security patch removes `Prism:VaultUri` from tracked config (correct: it's a secret), the authentication defaults silently stop registering. The application still boots. Explicitly `[Authorize(Scheme = "...")]` controllers work (they name the scheme). But views using default authentication (`User.IsAuthenticated`) show signed-out state — the default scheme never decrypts the cookie.

**Why this is dangerous:**
- Config presence is meant to encode *infrastructure details* (which vault provider, which key, where to connect)
- Auth capability is *foundational*, not conditional
- Removing a secret (correct security practice) silently disables auth (incorrect consequence)
- Symptoms appear at the HTTP layer, not at startup (no early warning)

---

## When This Happens

- Feature is "optional" in some environments (e.g., auth via external OIDC in production, but required in development)
- Config value is infrastructure-specific (vault URI, key vault name, provider endpoint)
- Code was built with the assumption "if the value is present, the feature is enabled"
- Someone refactors config management (extract secrets from source, use environment-specific files)
- Result: feature silently disables

---

## The Fix

### 1. Separate Concerns: Capability from Infrastructure

```csharp
// ✅ GOOD: Auth is always enabled; vault is optional infrastructure
var vaultUri = config["Prism:VaultUri"];  // Optional secret provider detail

options.DefaultAuthenticateScheme = "PrismMemberCookie";
options.DefaultSignInScheme = "PrismMemberCookie";
options.DefaultChallengeScheme = "PrismEntraID";

// Conditionally register secret provider, not auth capability
if (!string.IsNullOrEmpty(vaultUri))
{
    services.AddKeyVaultSecretProvider(vaultUri);
}
```

### 2. Explicit Feature Flags (If Truly Optional)

If a feature genuinely needs to be toggled (rare for foundational subsystems), use a dedicated, intentional flag:

```csharp
// ✅ GOOD: Explicit feature flag, independent of secrets
var authEnabled = bool.Parse(config["Prism:AuthEnabled"] ?? "true");  // Defaults to true

if (authEnabled)
{
    options.DefaultAuthenticateScheme = "PrismMemberCookie";
    // ...
}
```

This makes the intent clear and doesn't couple to infrastructure config.

---

## Umbraco Context

`PrismComposer` in the Umbraco.Prism codebase had this bug:

```csharp
var vaultUri = builder.Config["Prism:VaultUri"];
bool isAuthEnabled = !string.IsNullOrEmpty(vaultUri);
if (isAuthEnabled)
{
    options.DefaultAuthenticateScheme = "PrismMemberCookie";
    options.DefaultSignInScheme = "PrismMemberCookie";
    options.DefaultChallengeScheme = "PrismEntraID";
}
```

Security commit `b6336fd` correctly removed `Prism:VaultUri` from tracked `appsettings.json` (it's a deployment secret). This silently set `isAuthEnabled = false`. Home page views began showing "Sign In" even for authenticated users (Umbraco's fallback scheme didn't decrypt `PrismMemberCookie`). Only route-hijacking controllers with explicit scheme worked.

Fixed by removing the gate:

```csharp
options.DefaultAuthenticateScheme = "PrismMemberCookie";
options.DefaultSignInScheme = "PrismMemberCookie";
options.DefaultChallengeScheme = "PrismEntraID";
// Vault URI is now just infrastructure configuration, not a feature flag
```

---

## Checklist

When reviewing code for this anti-pattern:

- [ ] Are foundational subsystems (auth, logging, error handling) gated on optional config presence?
- [ ] Would removing that config value (security refactor, secret extraction) silently disable the subsystem?
- [ ] Could the subsystem function without that specific config value (using defaults, fallbacks, or environment-specific files)?
- [ ] Is the config value infrastructure-specific (URI, endpoint, provider name) rather than a capability toggle?

If any answer is yes, refactor to decouple capability from infrastructure.

---

## Related Patterns

- **Explicit feature flags** (e.g., `Prism:AuthEnabled` boolean) — use for genuinely optional behavior
- **Graceful defaults** — subsystems should boot with sensible defaults, not fail silently
- **Environment-specific configuration** — secrets in `appsettings.Local.json`, infrastructure URIs in deployment-time config; neither drives capability

---

## References

- Commit `42b85e5` (Umbraco.Prism): Removed `isAuthEnabled` gate, made auth scheme registration unconditional
- PR #38 decision log: `.squad/decisions.md` — "DefaultAuthenticateScheme Must Not Depend on Prism:VaultUri"
