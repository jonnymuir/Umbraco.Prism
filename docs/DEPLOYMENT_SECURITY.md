# Deployment Security Guide

This guide covers deployment boundaries, environment configuration, and pre-flight checks for Umbraco.Prism. This guide describes how Prism is designed to be deployed. It is not a guarantee of security, and you remain responsible for your deployment: see [Security responsibility and assurance](security/responsibility.md).

---

## Service Deployment Boundaries

### UmbracoPrism.TestSite: the reference host

The TestSite is the reference host for Prism. It also runs prismreference.com, but it is a demonstration, not a hardened template, so review its configuration before copying it:
- **Authentication:** Stateless OIDC identity per request
- **Tenant Isolation:** Properly enforced across all requests
- **Dev-Only Code Paths:** None; no conditional logic that enables dangerous features in Development
- **Safe to Deploy:** Yes, with proper environment configuration (see checklist below)

### ❌ UmbracoPrism.MockBusinessApp: DO NOT DEPLOY TO PRODUCTION

This is a demo and development-only application. It **must never** be deployed to production.

**Why it's unsafe:**
- **No Authentication:** Admin service desk endpoints have zero auth checks
- **In-Memory State:** All state is lost on restart; no persistence
- **No Audit Logging:** Changes to service requests are not logged
- **Unrestricted Admin Access:** Anyone with network access can:
  - View the full service desk dashboard (`GET /admin/service-desk`)
  - Advance service requests (`POST /admin/service-desk/{instanceId}/advance`)
  - Reset a single service request (`POST /admin/service-desk/{instanceId}/reset`)
  - Delete all service requests (`POST /admin/service-desk/reset-all`)

**Mitigation:** All unauthenticated admin endpoints return `404 Not Found` in non-Development mode. However, **do not rely on this for safety**: simply do not deploy MockBusinessApp.

### Wayfinder

The backchannel URL feature (described below) only activates if explicitly configured via environment variable.

---

## Environment Variable Hygiene

These variables must **NEVER** be set in production:

### ⚠️ **KEYCLOAK_BACKCHANNEL_URL**

**What it does:**
- Provides an internal container-to-container URL for the Keycloak signing key fetch
- Used only in Codespaces development environments

**Why it's dangerous in production:**
- Setting this in production **bypasses HTTPS enforcement** on the signing key metadata fetch
- This violates security hardening and should only exist in Development
- **The app now throws at startup if this variable is set outside Development mode**

**Correct behavior:**
- Unset in production ✅
- Set to internal Aspire URL in Codespaces ✅

### ⚠️ **Prism__AllowDemoTenants** and **Prism__AllowInternalAuthorities**

**What they do:**
- Prism's demo behaviours are on only in Development: the `localhost` and Codespaces demo tenants,
  inline client secrets for them, and an identity provider on a loopback address or plain `http`.
- `Prism__AllowDemoTenants=true` switches those on in another environment. Leave it unset in
  production.
- `Prism__AllowInternalAuthorities=true` lets a tenant's OIDC authority be a private, loopback or
  link-local address, for an identity provider that really is on an internal network. Without it,
  an authority must be an `https` URL on a public host, with no credentials, query or fragment.
  Set it only if you need it.

**Tenant checks.** Every tenant identity is validated when it is saved (the management API, uSync
and `Prism:SeedTenant`) and again when it is resolved at runtime. A tenant that fails loses its
sign-in configuration and the failure is logged, so a bad row cannot make the server request an
unintended address.

**Tenant tokens.** A `{{TOKEN}}` in a tenant field resolves from configuration, except names that
look like credentials or framework settings (`SECRET`, `PASSWORD`, `KEY`, `TOKEN`,
`CONNECTIONSTRING`, `ASPNETCORE_`, `UMBRACO_` and similar), which never resolve. To allow only your
own tokens, set `Prism:TenantTokens:AllowedPrefixes` to a list such as `["TENANT_"]`.

### ⚠️ **CODESPACE_NAME**

**What it is:**
- Signal that the app is running inside GitHub Codespaces
- Automatically set by the Codespaces environment

**Why it matters:**
- If set, Codespaces-specific configuration is activated
- Development features may be enabled that are unsafe in production
- Codespaces should only be used for local development

### ⚠️ **ASPIRE_ALLOW_UNSECURED_TRANSPORT**

**What it does:**
- Disables transport security checks in the Aspire orchestration layer
- Allows HTTP connections where HTTPS is normally required

**Why it's dangerous:**
- Undermines all TLS/HTTPS protections
- Only valid for local development

**Action:**
- Never set in production ❌

---

## Member session lifetime

A member's session cookie slides for 8 hours of inactivity, ends after 7 days whatever the
activity, and is re-confirmed with the identity provider every 15 minutes using the member's
stored refresh token. A member removed or disabled at the provider (the provider answers
`invalid_grant`) is signed out at the next check. A provider that cannot be reached does not sign
anyone out; the check is retried within minutes. A session with no refresh token has only the two
time limits. Tune them in `Prism:Session`:

```json
{ "Prism": { "Session": { "SlidingExpirationMinutes": 480, "AbsoluteMaxLifetimeMinutes": 10080, "RevalidationIntervalMinutes": 15 } } }
```

Set `RevalidationIntervalMinutes` to `0` to turn the provider check off. Ending a session at an
interactive sign-out also ends it at the provider through the provider's end-session endpoint.

## Where member sessions are kept

A signed-in member's tokens never reach the browser. The `__Host-prism.sid` cookie carries a random
reference, and the session itself (claims, access, ID and refresh tokens) sits on the server,
protected with Data Protection. The cookie is `Secure`, `HttpOnly`, `Lax`, host-only and has path
`/`, which browsers enforce for the `__Host-` prefix. A session that expires or is deleted from the
store ends at once, even though the cookie is still well formed.

By default sessions live in the Umbraco database, in the `prismSessions` table that Prism creates
on first run. Nothing needs setting up, every instance sharing the database shares the sessions,
and an hourly job deletes expired rows. To keep them in Redis (or any other `IDistributedCache`)
instead, register that cache in the host and switch the storage:

```csharp
builder.Services.AddStackExchangeRedisCache(options => options.Configuration = "redis:6379");
```

```json
{ "Prism": { "Session": { "Storage": "DistributedCache" } } }
```

Redis expires entries itself, so there is nothing to clean up. Without a shared cache registered,
`DistributedCache` keeps sessions in memory, which signs everyone out at a restart and does not
work across instances, so use it only with Redis or another shared cache. A host with a different
store implements `IPrismSessionStorage` (save, load, remove, remove expired) and registers it after
Prism.

## Rate limiting and the key ring

**Sign-in endpoints.** Prism limits `/auth/login`, `/auth/register`, `/signin-oidc`,
`/signout-callback-oidc` and the mobile handover (`/umbraco/prism/mobile/auth/*`) to 60 requests a
minute per address and answers `429` beyond that. Change it with
`Prism:RateLimiting:SignInRequestsPerMinute` (`0` turns it off). The limit is per address and per
process. Behind a reverse proxy the address is the proxy's unless you configure forwarded headers
for it, and with several instances each keeps its own count. A host that sets its own
`RateLimiterOptions.GlobalLimiter` replaces this one.

**Biometric exchange.** A token that fails too often is locked for the failure window, then starts
afresh. Idle entries are discarded and tracking is capped, so unique garbage cannot grow memory.

**Data Protection.** The member cookie, the mobile handover ticket and antiforgery tokens are only
as strong as the Data Protection key ring. Outside Development Prism logs a warning at startup when
the host has not chosen where keys are stored (`PersistKeysTo...`) and how they are protected
(`ProtectKeysWith...`), and refuses to start. Set `Prism:DataProtection:RequireConfiguredKeys` to
`false` to log a warning and start anyway; do that only for a host whose keys you accept are
unprotected. With several instances, keys must be shared and protected, or sessions will fail
intermittently.

## Hostnames, secrets and identity

**Unknown hostnames.** By default a request whose hostname matches no tenant falls through to the
host's own site. On a host that serves tenants only, set `Prism:RejectUnknownHosts` to `true` and
such requests are answered `421 Misdirected Request` before anything else runs.

**Secret cache.** A client secret read from Key Vault is cached for 60 minutes
(`Prism:Secrets:CacheMinutes`), so a rotated secret reaches Prism within that time. A failed lookup
is never cached as a value: it is not retried for 15 seconds, then tried again.

**Stable member identifier.** `ClaimsPrincipal.GetSubject()` returns `{tid}:{oid}` for an Entra
member and `{issuer}|{sub}` for another provider. It does not change when the member's email or
sign-in name does, so key ownership, rosters and notification routing on it, and keep email as a
contact detail.

**Mobile sign-in codes** are redeemed once. A host with more than one instance must register a
shared `IDistributedCache` (the default is per instance) or a code issued by one instance cannot be
redeemed on another.

## Admin Endpoint Risk: MockBusinessApp

The MockBusinessApp service desk admin panel is completely unauthenticated in its current form. These endpoints are now safe from accidental exposure because they return `404` in non-Development environments:

| Endpoint | Method | Risk |
|----------|--------|------|
| `/admin/service-desk` | GET | Full service desk dashboard with live JSON editor |
| `/admin/service-desk/{instanceId}/advance` | POST | Advance a service request without permission |
| `/admin/service-desk/{instanceId}/reset` | POST | Reset a single service request |
| `/admin/service-desk/reset-all` | POST | Delete all service requests |

**Status in Production Mode:** All return `404 Not Found`

**Still:** Do not deploy MockBusinessApp to any production environment, even with these safeguards in place. They are a safety net, not a license to deploy.

---

## How KEYCLOAK_BACKCHANNEL_URL Works

For Codespaces developers who want to understand the architecture:

### The Problem
In Codespaces, GitHub's forwarded-port proxy blocks unauthenticated server-side HTTP requests. When the .NET backend needs to fetch Keycloak signing keys, it cannot reach the external Keycloak URL through the proxy.

### The Solution
`KEYCLOAK_BACKCHANNEL_URL` provides an internal Aspire container URL as an alternative metadata fetch endpoint:
- Signing key fetch uses the backchannel URL (internal, proxy-safe)
- Issuer validation still uses `OidcAuthority` (external, user-facing)
- Token `iss` claims must still match the external `OidcAuthority` exactly

### Security Design
This is the **correct security model**:
- ✅ Metadata fetch endpoint ≠ trust anchor
- ✅ Token validation still trusts the external issuer
- ✅ Token `iss` claim mismatch = rejected token
- ✅ No bypass of HTTPS enforcement (except for backchannel fetch)

### For Developers
You do not need to set `KEYCLOAK_BACKCHANNEL_URL` manually. The AppHost automatically injects it when `CODESPACE_NAME` is detected. Just work as normal, Codespaces development is fully supported.

---

## Codespaces Development

Umbraco.Prism supports local development in GitHub Codespaces:
- The AppHost automatically detects `CODESPACE_NAME`
- `KEYCLOAK_BACKCHANNEL_URL` is automatically configured to an internal Aspire container URL
- No manual setup of backchannel URLs is required
- All development features work as expected

Codespaces is ideal for:
- Testing multi-tenant scenarios
- Rapid iteration without local .NET SDK setup
- Collaborative debugging sessions

---

## Production Deployment Checklist

Before deploying to any production environment, verify all of these:

- [ ] **Only deploying UmbracoPrism.TestSite** (never MockBusinessApp)
- [ ] **KEYCLOAK_BACKCHANNEL_URL is not set**
- [ ] **CODESPACE_NAME is not set**
- [ ] **ASPIRE_ALLOW_UNSECURED_TRANSPORT is not set**
- [ ] **Keycloak is accessible over HTTPS** (or securely via your network)
- [ ] **OidcAuthority is configured to the correct external Keycloak realm URL**
- [ ] **All tenant-specific OIDC ClientIds are registered** in your Keycloak realm
- [ ] **Network policies restrict access to admin endpoints** (defense in depth)
- [ ] **Deployment account has no access to MockBusinessApp source** (if applicable)

---

## Questions or Issues?

- **Security questions:** Contact Copper (Security Engineering)
- **Deployment issues:** Contact Blathers (Backend Services)
- **Documentation gaps:** Contact Celeste (Documentation)
