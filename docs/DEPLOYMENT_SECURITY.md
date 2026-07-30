# Deployment Security Guide

This guide covers deployment boundaries, environment configuration, and pre-flight checks for Umbraco.Prism. All details below reflect the current security architecture verified in the latest review.

---

## Service Deployment Boundaries

### ✅ UmbracoPrism.TestSite — Production-Safe

The TestSite is designed and tested for production use:
- **Authentication:** Stateless OIDC identity per request
- **Tenant Isolation:** Properly enforced across all requests
- **Dev-Only Code Paths:** None; no conditional logic that enables dangerous features in Development
- **Safe to Deploy:** Yes, with proper environment configuration (see checklist below)

### ❌ UmbracoPrism.MockBusinessApp — DO NOT DEPLOY TO PRODUCTION

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

**Mitigation:** All unauthenticated admin endpoints return `404 Not Found` in non-Development mode. However, **do not rely on this for safety**—simply do not deploy MockBusinessApp.

### ✅ Wayfinder — Production-Safe

Shared libraries are production-safe. The backchannel URL feature (described below) only activates if explicitly configured via environment variable.

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
You do not need to set `KEYCLOAK_BACKCHANNEL_URL` manually. The AppHost automatically injects it when `CODESPACE_NAME` is detected. Just work as normal—Codespaces development is fully supported.

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
