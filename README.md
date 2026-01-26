<div align="center">
<img src="assets/logo-horizontal-lockup.svg" width="500" alt="Umbraco Prism Logo">
<h3>One source. A spectrum of brands.</h3>
</div>

# Umbraco Prism

## Overview
Umbraco Prism is a multi-tenancy extension for Umbraco (v17+) designed to allow a single Umbraco instance to serve hundreds of distinct client portals. It resolves branding, identity, and content context at runtime based on the incoming domain name.

## What problem does it solve
You are a service provider offering a portal with consistent functionality across different organizations. You want each organization to appear as its own branded web portal, but without the overhead of managing multiple root nodes or a bloated local Member database.

## Core Objectives
1. **Single Tree, Multiple Brands:** Maintain one content tree; apply branding layers dynamically.
2. **Configuration-Driven Auth:** Authentication is activated globally by providing a Vault URI; tenants are then managed via the Backoffice.
3. **Strict Isolation:** Ensure data and branding never leak between tenants.
4. **Stateless Identity:** No local Member records. Identity is deferred to Entra ID (CIAM), keeping the database clean and scalable.
5. **Vaulted Security:** Sensitive OIDC Secrets are pulled securely from Azure Key Vault at runtime.

---

## Architecture

### 1. The Runtime (Middleware)
* **PrismTenantMiddleware:** Intercepts requests and resolves the hostname against the Tenant Cache.
* **IPrismContext:** A scoped service containing the current `Tenant` and `Theme` data.

### 2. The Identity Engine (Stateless OIDC)
* **Dynamic Configuration:** Prism controls the OIDC pipeline per request, swapping `ClientId`, `Authority`, and `Issuer` keys based on the resolved tenant.
* **IPrismUserContext:** High-performance access to the current user's claims and their associated Prism Tenant details.
* **SecretVaultService:** Uses `Azure.Identity` to fetch Client Secrets from Azure Key Vault, utilizing Managed Identity in production and CLI login during development.

---

## Integration & Usage

### 1. Enabling Authentication
Authentication is active by default once a Vault URI is detected in your configuration. In your `appsettings.json`, simply provide your Azure Key Vault address:

```json
{
  "Prism": { 
    "VaultUri": "[https://your-vault.vault.azure.net/](https://your-vault.vault.azure.net/)" 
  }
}
```

### 2. Diagnostic & Debugging (Tag Helper)

To quickly visualize the active tenant, user identity, and system health, use the built-in diagnostic Tag Helper.

First, register the Tag Helper in your `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, UmbracoPrism.Core
```

Then, drop the tag into any Razor view (e.g., your Master Template or Home Page):

```html
<prism-debug />
```

### 3. Accessing User Data

Since Prism is stateless, you do not use `MemberManager`. Instead, inject `IPrismUserContext` to access details:

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

1. **Entra ID:** Create an **App Registration** (CIAM recommended). Set the Redirect URI to `https://localhost:[PORT]/signin-oidc`.
2. **Key Vault:** Create an Azure Key Vault and add a secret (e.g., `tenant-b-secret`) containing the Client Secret.
3. **Permissions:** Ensure your identity (or App Service) has the **Key Vault Secrets User** role.

#### Phase 2: Local Auth

Run `az login` in your terminal to allow the `SecretVaultService` to access Azure during local development.

#### Phase 3: Tenant Onboarding

1. Navigate to the **Prism Dashboard** in the Umbraco Backoffice.
2. Create a Tenant with the following Identity mapping:
* **Hostname:** `localhost:[PORT]`
* **Entra Tenant ID:** Your Directory (tenant) ID.
* **Entra Client ID:** Your App Registration ID.
* **Secret Key Name:** `tenant-a-secret`.

---

## Technical Stack

* **Umbraco:** v17.0+
* **Framework:** .NET 10.0
* **Security:** Azure Key Vault, Managed Identity, Stateless OIDC (CIAM)
* **Frontend:** Lit (Backoffice), Razor (Website)