# SKILL: Proxy-Aware Rate Limiting with ForwardedHeadersMiddleware

**Author:** Blathers (Backend Dev)  
**Date:** 2026-04-30  
**Context:** SEC-007 remediation, Umbraco.Prism  

---

## Problem

Rate limiters that partition by `HttpContext.Connection.RemoteIpAddress` are ineffective behind reverse proxies (nginx, Azure Front Door, AWS ALB). All requests share the proxy's IP — one bucket for all clients.

## Idiomatic ASP.NET Core Solution

Register `ForwardedHeadersMiddleware` to rewrite `RemoteIpAddress` from `X-Forwarded-For` **before** any IP-sensitive middleware runs. Rate-limiting code then reads `RemoteIpAddress` as normal — no header-parsing in business logic.

### 1. Service registration (Composer / Program.cs)

```csharp
using Microsoft.AspNetCore.HttpOverrides;

services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Dev/permissive default — restrict in production (see below)
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
```

### 2. Middleware placement (must run first)

```csharp
app.UseForwardedHeaders();      // rewrites RemoteIpAddress — MUST be first
app.UseAuthentication();
app.UseAuthorization();
// ... other middleware
```

For Umbraco.Prism, this is wired inside the `UmbracoPipelineFilter` pre-pipeline callback in `PrismComposer.cs`.

### 3. Rate-limiter partition key

```csharp
// In controller or service — reads the middleware-rewritten value
string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
```

**Never** read `X-Forwarded-For` headers directly in business logic. The middleware is the single point of trust.

---

## Production Hardening (Required Before Launch)

Clearing `KnownProxies`/`KnownNetworks` trusts any `X-Forwarded-For` header, including attacker-crafted ones. Lock down in production:

```csharp
options.KnownNetworks.Clear();
options.KnownProxies.Clear();
// Add your actual load balancer IPs / CIDRs:
options.KnownProxies.Add(IPAddress.Parse("203.0.113.1"));      // your LB external IP
options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8)); // internal CIDR
```

With known proxies configured, only the innermost `X-Forwarded-For` value (the real client) from a trusted proxy is used. Client-supplied `X-Forwarded-For` from unknown IPs is ignored.

---

## Unit Testing Contract

Test the partition-key isolation at the service level:

```csharp
// Two distinct IPs produce independent rate-limit buckets
var (limitedA, _) = svc.CheckIpLimit("1.2.3.4");   // real client
var (limitedB, _) = svc.CheckIpLimit("9.9.9.9");   // spoofed header IP (different bucket)

// Exhaust A's budget — B is unaffected
for (var i = 1; i < limit; i++) svc.CheckIpLimit("1.2.3.4");
svc.CheckIpLimit("1.2.3.4").IsLimited.Should().BeTrue();
svc.CheckIpLimit("9.9.9.9").IsLimited.Should().BeFalse("independent bucket");
```

This confirms the rate-limiter uses per-client partitioning, not a shared proxy-IP bucket.

---

## References

- [ASP.NET Core ForwardedHeaders docs](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer)
- Umbraco.Prism `PrismComposer.cs` — SEC-007 implementation (commit `44c476f`)
- `Phase1SecurityRegressionTests.BiometricRateLimit_PartitionKey_UsesRemoteIpAddress_NotRawForwardedForHeader`
