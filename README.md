# Umbraco Prism
**Splitting one web site into many. Single App, Many Brands.**

## Overview
Umbraco Prism is a multi-tenancy extension for Umbraco (v14+) designed to allow a single Umbraco instance to serve hundreds of distinct client portals. It resolves branding, identity, and content context at runtime based on the incoming domain name.

## What problem does it solve
You are a third party service provider and you have a portal offering with very similar functionality regardless of the actual organisation you are hosting the service for. 

You want each organisation to appear as its own web portal to the client and their members, but you don't want the overhead of looking after either multiple Umbraco instances or multiple root nodes.

## Core Objectives
1.  **Single Tree, Multiple Brands:** Maintain one content tree; apply branding layers (Themes) dynamically.
2.  **Zero-Code Onboarding:** Add new tenants/domains via the Backoffice, not config files.
3.  **Strict Isolation:** Ensure data and branding never leak between tenants.
4.  **Modern Stack:** Built for the new Umbraco Backoffice (Lit/TypeScript) and .NET Core Middleware.

## Architecture

### 1. The Runtime (Middleware)
* **PrismTenantMiddleware:** Intercepts requests, resolves `HttpContext.Request.Host` against the Tenant Cache.
* **IPrismContext:** A scoped service injected into Controllers/Views containing the current `Tenant` and `Theme`.
* **Asset Injection:** Dynamically injects CSS Custom Properties (`:root { --primary: #x }`) into the layout.

### 2. The Backoffice (Management)
* **Prism Dashboard:** A custom section to manage Tenants and Domains.
* **Theme Editor:** A UI to define colors, fonts, and assets, serialized to JSON.

### 3. Data Storage
* Custom NPoco tables (`prsmTenants`, `prsmDomains`) to avoid Content Node overhead.