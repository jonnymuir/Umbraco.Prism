# Skill: Backchannel Rewrite Testing

**Domain:** Auth transport rewrites / JWT security regression tests  
**Author:** Tangy  
**First applied:** `fix/codespaces-401-downstream-auth` (commit `ba14053`)

---

## Problem Pattern

When a Development-only environment variable (`KEYCLOAK_BACKCHANNEL_URL`) gates a transport rewrite (changing *where* an HTTP call goes), tests must prove:

1. The rewrite fires **only when both gates are open** (env var + `ASPNETCORE_ENVIRONMENT=Development`).
2. The rewrite does **not** fire in production (env var absent OR non-Development environment).
3. JWT issuer/audience validation remains **unchanged** regardless of transport path.

---

## Test Infrastructure

### Refresh-token rewrite (PrismContext)

Drive via `GetAuthorizationHeaderAsync` with a session that has **only a `refresh_token`** (no `access_token`). This forces the refresh path every time.

Mock `IPrismTokenRefreshService` with `Callback` on `RefreshAsync(string endpoint, ...)` to capture the URL used.

```csharp
var mockRefreshService = new Mock<IPrismTokenRefreshService>();
string? capturedEndpoint = null;
mockRefreshService
    .Setup(s => s.RefreshAsync(It.IsAny<string>(), ...))
    .Callback<string, ...>((ep, ...) => capturedEndpoint = ep)
    .ReturnsAsync(...);
```

### JWKS fetch rewrite (PrismAuthExtensions.ResolveSigningKeys)

1. Register a mock `IPrismSigningKeyCache` **before** `AddPrismAuthentication` (which uses `TryAddSingleton` — won't override an already-registered service).
2. Mock `GetSnapshot` to return `IsExpired: true, ContainsRequestedKey: false` → triggers `WarmAsync`.
3. Capture `metadataAddress` via `Callback` on `WarmAsync(string tenantKey, string metadataAddress, ...)`.

```csharp
var mockCache = new Mock<IPrismSigningKeyCache>();
string? capturedMetadataAddress = null;
mockCache
    .Setup(c => c.GetSnapshot(It.IsAny<string>()))
    .Returns(new SigningKeySnapshot([], DateTimeOffset.MinValue, false, false));
mockCache
    .Setup(c => c.WarmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
    .Callback<string, string, bool, string?, CancellationToken>((_, addr, _, _, _) => capturedMetadataAddress = addr)
    .Returns(Task.CompletedTask);

// Register BEFORE AddPrismAuthentication:
services.AddSingleton(mockCache.Object);
services.AddPrismAuthentication(config);

var sp = services.BuildServiceProvider();
var options = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("prism-backoffice");
var resolver = options.TokenValidationParameters.IssuerSigningKeyResolver!;

// Invoke resolver to trigger WarmAsync:
resolver(string.Empty, token, token.Header.Kid, options.TokenValidationParameters);
```

### BackOfficeTenant config binding

`BackOfficeTenant` is a **positional record** — all config keys must match property names exactly:

```csharp
var config = new Dictionary<string, string?>
{
    ["PrismBusinessApp:Tenants:0:EntraTenantId"] = "",
    ["PrismBusinessApp:Tenants:0:ClientId"] = "prism-client",
    ["PrismBusinessApp:Tenants:0:Code"] = "prism-dev",
    ["PrismBusinessApp:Tenants:0:DisplayName"] = "Prism Dev",
    ["PrismBusinessApp:Tenants:0:OidcAuthority"] = "https://..."
};
```

Missing any of the four non-optional fields causes config binding to produce null/empty tenants → `ResolveSigningKeys` returns early without calling `WarmAsync`.

---

## Environment Variable Isolation

Tests that set process-wide env vars (`Environment.SetEnvironmentVariable`) **must** be serialised with any test that reads those env vars in the code under test.

Use an xUnit `[CollectionDefinition]` + `[Collection]` to prevent parallel execution:

```csharp
// EnvVarSensitiveTestCollection.cs
[CollectionDefinition(Name)]
public sealed class EnvVarSensitiveTestCollection : ICollectionFixture<EnvVarSensitiveTestCollection>
{
    public const string Name = "EnvVarSensitive";
}

// On each affected test class:
[Collection(EnvVarSensitiveTestCollection.Name)]
public class BackchannelRewriteTests { ... }

[Collection(EnvVarSensitiveTestCollection.Name)]
public class PrismSigningKeyCacheTests { ... }
```

Use a `TempEnvVar` / `TempEnvironmentVariable` disposable guard so env vars are always restored even on test failure.

---

## Path from Test Binary to Source

```
AppContext.BaseDirectory = src/UmbracoPrism.Core.Tests/bin/Release/net10.0/
```

5× `../` to reach the solution root:
```
net10.0 → Release → bin → UmbracoPrism.Core.Tests → src → (solution root)
```

Then append `src/UmbracoPrism.MockBusinessApp/Program.cs`.

---

## Gotchas

### Serialising env-var-mutating tests is necessary but NOT sufficient

Putting env-var-mutating tests into `EnvVarSensitiveTestCollection` prevents them from running
in parallel with *each other*. It does **not** automatically protect *other* test classes that
merely *read* those same env vars — unless those readers are also in the same collection.

**The rule:** every test class that reads `KEYCLOAK_BACKCHANNEL_URL` or
`ASPNETCORE_ENVIRONMENT` — directly, or transitively through production code under test
(e.g. `PrismContext.GetAuthorizationHeaderAsync`, `PrismOidcConfiguration` callbacks) —
must either:

1. Join `EnvVarSensitiveTestCollection` (add `[Collection(EnvVarSensitiveTestCollection.Name)]`), **AND**
2. Add a defensive snapshot/restore in its constructor/`Dispose()` so that even within a
   serialised sequence, a test that crashes mid-mutation cannot leave env vars dirty for the
   next test.

**Why local CPUs hide the race:** On a developer's machine, xUnit's default thread count
matches available cores. Most machines finish the mutating tests and restore the env vars
*before* the reader tests are scheduled. On a CI box running multiple agent jobs, or with
fewer threads, the scheduler can interleave them, exposing the leak.

**Belt-and-braces pattern for reader classes (don't mutate, but still vulnerable):**

```csharp
[Collection(EnvVarSensitiveTestCollection.Name)]
public class LocalhostGenericOidcRegressionTests : IDisposable
{
    private readonly string? _savedBackchannelUrl;
    private readonly string? _savedAspNetCoreEnv;

    public LocalhostGenericOidcRegressionTests()
    {
        _savedBackchannelUrl = Environment.GetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL");
        _savedAspNetCoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("KEYCLOAK_BACKCHANNEL_URL", _savedBackchannelUrl);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _savedAspNetCoreEnv);
    }
}
```

This snapshot/restore in a *reader* ensures that if any test in the serialised sequence leaves
env vars dirty (e.g. due to an unhandled exception in a mutator), subsequent reader tests
still see a clean slate.

---

## Security Checklist for Backchannel Rewrites

When reviewing a new backchannel transport rewrite, verify:

- [ ] **Dual-gate**: rewrite only fires when BOTH env var AND `ASPNETCORE_ENVIRONMENT=Development` are set.
- [ ] **Issuer validation unchanged**: `TokenValidationParameters.ValidateIssuer = true` and issuer is validated against configured `OidcAuthority`, not the transport URL.
- [ ] **Audience validation unchanged**: `ValidateAudience = true`.
- [ ] **Fail-loud guard in production app**: `MockBusinessApp/Program.cs` (or equivalent) throws `InvalidOperationException` if env var is set outside Development.
- [ ] **Tests cover all four cases**: rewrite active, env var absent, non-Development, and issuer rejection with rewrite active.
