# Umbraco Prism

**One Umbraco instance. Multiple branded portals. Native mobile app included.**

Multi-tenant website branding and identity at runtime. Add a mobile app with one click.

```bash
dotnet add package UmbracoPrism
```

---

## Try it Now — No Install Required

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/jonnymuir/Umbraco.Prism)

Click the button to spin up the full Umbraco Prism stack in a browser — no local setup, no Docker, no .NET install. GitHub handles everything. The Codespace is completely throwaway when you're done.

**The stack starts automatically** — watch the terminal at the bottom of your screen. It polls until Keycloak, the Aspire Dashboard, and the TestSite are all ready (first boot: ~3 minutes), then prints the URLs and credentials. When the Aspire Dashboard port is detected VS Code opens it in your browser automatically.

1. Wait for the terminal to print **🎉 Umbraco Prism is ready!**
2. Click the TestSite URL → log in with `demo@prism.local` / `password` (Keycloak SSO)
3. Browse **My Workflows** to see the demo workflow in action

**Credentials at a glance:**

| What | Username | Password |
|------|----------|----------|
| TestSite (Keycloak SSO) | `demo@prism.local` | `password` |
| Umbraco backoffice (`/umbraco`) | `admin@prism.local` | `PrismLocal!12345` |
| Keycloak admin console | `admin` | `admin` |

> **When you're done:** go to [github.com/codespaces](https://github.com/codespaces), find your Codespace, and click **Stop** (or **Delete** to free quota immediately). Stopping halts billing; the Codespace resumes from where you left off.

---

## 🚀 Interactive Walkthrough — "Apply for Planning Permission"

Once your stack is running, follow the step-by-step guide to complete the demo workflow — with explanations of what Umbraco.Prism and the Umbraco backoffice are doing at each stage.

→ **[Full walkthrough: docs/walkthroughs/planning-notification.md](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/walkthroughs/planning-notification.md)**

The walkthrough covers:
- Logging in via Keycloak SSO and what the token exchange looks like
- Walking through each GDS form step (project details, work type, timeline & cost, affected parties)
- The check-answers review screen and how field values are aggregated
- Submitting and seeing the confirmation
- Behind the scenes: workflow definition files, field groups, the workflow engine, and how Umbraco renders it all
- Exploring further: editing definitions, watching engine logs in Aspire, testing with multiple browsers

---

## Try the Demo — Local Setup

Get from clone to running in five minutes. No Azure account needed.

**One-time setup:**
- `.NET 10 SDK` ([Download](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Trust the .NET dev certificate** — run `dotnet dev-certs https --trust`
- Docker Desktop running ([Download](https://www.docker.com/products/docker-desktop/))
- `Node.js 20+` ([Download](https://nodejs.org/))
- Frontend dependencies: `cd src/UmbracoPrism.Client && npm install`

**Start the full stack:**
```bash
dotnet run --project src/UmbracoPrism.AppHost
```

Then:
1. Open the Aspire dashboard at `https://localhost:17214`
2. Click the TestSite URL → log in with `demo@prism.local` / `password`
3. Browse **My Workflows** to see the demo workflow in action
4. The MockBusinessApp runs alongside at `https://localhost:7245` — it accepts the same demo credentials and powers the workflow engine

**Optional:** Explore Keycloak admin at `https://localhost:8443/admin` (`admin` / `admin`).

**Why this matters for local dev:**
- The local Keycloak uses standard OIDC code-flow scopes — no offline tokens needed for a fresh clone.
- Prism preserves the `id_token` in the session, enabling logout callbacks to Keycloak with the required `id_token_hint`.
- MockBusinessApp trusts the browser-facing Keycloak authority (`https://localhost:8443`), so the workflow dashboard validates bearer tokens against the public issuer, not the internal container URL (`http://localhost:8080`).
- Aspire runtime state lives under `artifacts/aspire/testsite-runtime/` — the demo and Playwright suite never mutate the standalone TestSite database at `src/UmbracoPrism.TestSite/umbraco/Data/`.

→ For detailed setup, troubleshooting, and architecture: See [ASPIRE_DEV.md](https://github.com/jonnymuir/Umbraco.Prism/blob/main/ASPIRE_DEV.md).

---

## What You Get

### Multi-Tenant Web — One Instance, Hundreds of Brands

Serve distinct branded portals from one Umbraco instance. Runtime branding, domain resolution, tenant isolation.

**Screenshots:** [See on GitHub](https://github.com/jonnymuir/Umbraco.Prism#what-you-get)

**Web features:**
- Domain-based tenant resolution — each client gets their own hostname
- Live branding editor — CSS variables update without deploy
- **Branding as a Design System** — annotated CSS variables become labeled form fields, grouped into sections (Colors, Typography, Components), with type-aware editors (color pickers, sliders, text inputs)
- Per-tenant OIDC — Entra ID integration, zero local Members
- Downstream auth — propagate tenant identity to internal APIs
- Tenant isolation — authorization policies enforce data boundaries

→ [Umbraco Setup Guide](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/umbraco-setup.md)

### Produce Mobile — Generate Apps from Backoffice

Turn tenant settings into iOS/Android apps. No complex native coding, just click **Produce Mobile**.

**Mobile features:**
- Biometric login (Face ID, fingerprint) — skip OIDC on return
- Push notifications (FCM/APNs) — content or API triggered
- Offline-ready layouts with safe-area handling
- Tenant branding at runtime (colors, logo, splash)

Run in simulator:

```bash
npm run bootstrap:ios
```

→ [Mobile Setup](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/PUSH_SETUP.md) | [Biometric Auth](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/biometric-setup.md)

---

## Quick Start

### 1. Install

```bash
dotnet add package UmbracoPrism
```

Prism registers automatically via `PrismComposer` — no manual service registration needed.

### 2. Configure

Add to `appsettings.json`:

```json
{
  "Prism": {
    "VaultUri": "https://your-keyvault.vault.azure.net/"
  }
}
```

For local dev without Azure Key Vault, see [Local Authentication Walkthrough](#local-authentication-walkthrough).

### 3. Run

```bash
dotnet run
```

Prism auto-creates document types (`homePage`, `memberDashboard`) on first startup.

### 4. Add Your First Tenant

In backoffice:
1. **Settings → Prism Dashboard**
2. Add tenant (hostname, identity settings, branding)
   - **Entra tenants:** enter the vault secret name in `SecretKeyName`
   - **Generic OIDC tenants:** enter OIDC authority and client ID, then provide the Key Vault secret name as the `OidcClientSecretReference` with provider `azure-key-vault`; the localhost Keycloak demo is the only inline-secret exception
3. Visit the hostname — see branded portal

→ [Full Setup Guide](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/umbraco-setup.md)

---

## How It Works

**Multi-tenancy at runtime:** Middleware resolves hostname to tenant. One content tree serves hundreds of portals.

**Stateless auth:** No local Members. Identity deferred to OIDC providers (Entra ID or generic OIDC). Confidential client secrets resolve through Key Vault or the repo-owned localhost demo exception.

**Secure-by-default secrets:** Production tenants use vault-backed secret references, never raw values in management responses. The localhost Keycloak demo is the only inline-secret path, and runtime rejects inline generic OIDC secrets anywhere else.

**Mobile generation:** Tenant settings → iOS/Android app. Run in simulator immediately.

**Downstream auth:** Pass tenant identity to internal APIs without shared state.

---

## Features

**Multi-tenant web:**
- Domain-based tenant resolution
- Live CSS variable branding
- Per-tenant Entra ID (OIDC)
- Tenant isolation policies
- Downstream API auth

**Mobile:**
- iOS/Android generation from backoffice
- Biometric login (Face ID, fingerprint)
- Push notifications (FCM/APNs)
- Offline-ready layouts

**Infrastructure:**
- Azure Key Vault secrets at runtime
- Zero local Member records
- Managed Identity support
- Admin-only backoffice policies

→ [Full Documentation](https://github.com/jonnymuir/Umbraco.Prism/tree/main/docs)

---

## Documentation

| Guide | Description |
|---|---|
| [Workflow Walkthrough](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/walkthroughs/planning-notification.md) | Step-by-step demo of the planning permission workflow — what you see and what's happening behind the scenes |
| [Secret Management](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/secret-management.md) | Configure OIDC client secrets for production tenants, understand local dev demo |
| [Umbraco Setup](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/umbraco-setup.md) | Install Prism, configure tenants, seed content |
| [Biometric Setup](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/biometric-setup.md) | Generate signing/encryption keys for mobile biometric auth |
| [Push Notifications](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/PUSH_SETUP.md) | Configure FCM (Android) and APNs (iOS) for push |
| [Notifications Design](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/notifications-design.md) | Push notification architecture and API reference |
| **Design Docs** | |
| [Notifications Architecture](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/design/notifications-architecture.md) | Internal design: notification system layers |
| [Notifications Backend](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/design/notifications-backend.md) | Internal design: backend API and service layer |
| [Notifications Mobile](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/design/notifications-mobile.md) | Internal design: Capacitor plugin integration |
| [Notifications Umbraco](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/design/notifications-umbraco-demo.md) | Internal design: Umbraco content hooks and demo site |

→ [Full Documentation Index](https://github.com/jonnymuir/Umbraco.Prism/tree/main/docs)

---

## Architecture

**Runtime layer:**
* `PrismTenantMiddleware` — resolves hostname to tenant
* `IPrismContext` — scoped service with tenant/theme data

**Identity layer:**
* Dynamic OIDC — swaps `ClientId`, `Authority`, `Issuer` per tenant
* `IPrismUserContext` — current user claims and tenant
* `SecretVaultService` — Azure Key Vault (Managed Identity in prod, Azure CLI local)
* Downstream flow — propagate tenant identity to APIs

**Secret Management:**
* **Entra ID tenants (production):** Secrets stored in Azure Key Vault, referenced by `SecretKeyName`
* **Generic OIDC tenants (production):** Secrets stored in Azure Key Vault, referenced by `OidcClientSecretProvider = "azure-key-vault"` plus `OidcClientSecretReference`
* **Local dev demo (Keycloak):** Repo-owned secret uses `OidcClientSecretProvider = "inline"` only for the seeded `localhost` tenant path
* **Management API/UI:** Responses expose `HasOidcClientSecret` and `OidcClientSecretProvider`, never the raw secret or reference value
* All confidential-client flows fail closed if a secret cannot be resolved at runtime

→ [Secret Management Guide](https://github.com/jonnymuir/Umbraco.Prism/blob/main/docs/secret-management.md) | [Architecture Docs](https://github.com/jonnymuir/Umbraco.Prism/tree/main/docs)

---

## Prerequisites

- **.NET 10.0** ([Download](https://dotnet.microsoft.com/download))
- **Node.js 20+** ([Download](https://nodejs.org/))
- **Docker Desktop** — for local demo with Aspire ([Download](https://www.docker.com/products/docker-desktop/))
- **Azure Key Vault** (production) or local dev without vault (see setup guide)
- **Entra ID** (for authentication)

> **Client dependencies:** Run before first build:
> ```bash
> cd src/UmbracoPrism.Client && npm install
> ```

---

## Stack

* **Umbraco:** v17.0+
* **.NET:** 10.0
* **Auth:** Stateless OIDC (Entra), Azure Key Vault, Managed Identity
* **Mobile:** Capacitor, TypeScript, Storybook

---

## Learn More

**Full documentation and setup guides:** [github.com/jonnymuir/Umbraco.Prism](https://github.com/jonnymuir/Umbraco.Prism)

This marketplace listing uses a plain-text-friendly version of the full README. Screenshots, videos, and advanced configuration are available in the complete GitHub documentation.
