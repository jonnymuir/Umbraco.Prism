<div align="center">
<img src="assets/logo-horizontal-lockup.svg" width="500" alt="Umbraco Prism Logo">
<h3>One source. A spectrum of brands.</h3>
</div>

# Umbraco Prism

## Overview

Umbraco Prism is a multi-tenancy extension for Umbraco (v17+) designed to allow a single Umbraco instance to serve hundreds of distinct client portals. It resolves branding, identity, and content context at runtime based on the incoming domain name.

## What problem does it solve

You are a third-party service provider offering a portal with consistent functionality across different organizations. You want each organization to appear as its own branded web portal to their members, but without the overhead of managing multiple Umbraco instances, multiple root nodes, or a bloated local Member database.

## Core Objectives

1. **Single Tree, Multiple Brands:** Maintain one content tree; apply branding layers (Themes) dynamically.
2. **Zero-Code Onboarding:** Add new tenants/domains via the Backoffice, not config files.
3. **Strict Isolation:** Ensure data and branding never leak between tenants.
4. **Stateless Identity:** No local Member records are stored. Identity is deferred to Entra ID, keeping the Umbraco database clean and highly scalable.
5. **Vaulted Security:** Sensitive credentials (OIDC Secrets) are pulled securely from Azure Key Vault at runtime.

---

## Architecture

### 1. The Runtime (Middleware)

* **PrismTenantMiddleware:** Intercepts requests and resolves the hostname against the Tenant Cache.
* **IPrismContext:** A scoped service containing the current `Tenant` and `Theme` data.
* **Asset Injection:** Dynamically injects CSS Custom Properties (`:root { --tenant-primary: #x }`) into the layout.

### 2. The Identity Engine (Stateless OIDC)

* **AddPrismAuthentication():** A service extension that configures OIDC and Cookie authentication with a single line of code.
* **PrismOidcConfiguration:** A dynamic options provider that reconstructs OIDC settings (Authority, ClientId) per request based on the active Tenant.
* **IPrismUserContext:** A scoped service providing high-performance access to the current user's claims (Name, Email, Entra Tenant ID) and the associated Prism Tenant details.
* **SecretVaultService:** Uses `Azure.Identity` to fetch Client Secrets from Azure Key Vault, utilizing Managed Identity in production and CLI login during development.

---

## Integration & Usage

### 1. Registration

To enable Prism identity in your project, simply add the extension method to your `Program.cs`:

```csharp
// Program.cs
builder.Services.AddPrismAuthentication();

```

### 2. Accessing User Data

Since Prism is stateless, you do not use `MemberManager`. Instead, inject `IPrismUserContext` into your Views or Controllers to access the authenticated user's details and their current tenant context.

```cshtml
@inject IPrismUserContext PrismUser

@if (PrismUser.IsAuthenticated)
{
    <h1>Welcome back, @PrismUser.Name</h1>
    <p>You are logged into the @PrismUser.CurrentTenant?.Name portal.</p>
}

```

---

## Setup & Development

### Local Authentication Walkthrough

#### Phase 1: Azure Setup

1. **Entra ID:** Create an **App Registration** for your test tenant. Set the Redirect URI to `https://localhost:[PORT]/auth/signin-oidc`.
2. **Key Vault:** Create an Azure Key Vault and add a secret (e.g., `tenant-a-secret`) containing your Entra Client Secret.
3. **Permissions:** Ensure your Azure account has the **Key Vault Secrets User** role.

#### Phase 2: Local Configuration

1. **App Settings:** Add your Vault URI to `appsettings.json`:

```json
{
  "Prism": { "VaultUri": "https://your-vault.vault.azure.net/" }
}

```

2. **CLI Auth:** Run `az login` in your terminal to allow the `SecretVaultService` to access Azure during local development.

#### Phase 3: Tenant Onboarding

1. Navigate to the **Prism Dashboard** in the Umbraco Backoffice.
2. Create a Tenant with the following Identity mapping:

* **Hostname:** `localhost:[PORT]`
* **Entra Tenant ID:** Your Azure Directory ID.
* **Entra Client ID:** Your App Registration ID.
* **Secret Key Name:** `tenant-a-secret`.

---

## Technical Stack

* **Umbraco:** v17.0+
* **Framework:** .NET 10.0
* **Security:** Azure Key Vault, Managed Identity, Stateless OIDC
* **Frontend:** Lit (Backoffice), Razor (Website)