Here is the comprehensive design document for your repository. This outlines the architecture we discussed: a secure, vault-backed, multi-tenant identity system.

You can copy the block below directly into a `IDENTITY_ARCHITECTURE.md` file in your repo.

---

# Multi-Tenant Identity Architecture (Vaulted Pattern)

## Overview

This document outlines the architectural pattern for handling multi-tenant identity within the `Umbraco.Prism` project. The system allows a single Umbraco instance to serve multiple member websites (Tenants), each with distinct branding and identity configurations using Microsoft Entra ID (formerly Azure AD).

### Core Principles

1. **Isolation:** Identity providers (Entra ID) are configured per-tenant to ensure data and branding separation.
2. **Zero-Knowledge Database:** Sensitive secrets (Client Secrets) are **never** stored in the application database. They are referenced by name and retrieved securely from Azure Key Vault at runtime.
3. **Dynamic Resolution:** Authentication schemes are not hardcoded in `Startup.cs` but are constructed dynamically based on the incoming request hostname.

---

## 1. System Architecture

The following diagram illustrates the relationship between the Consumer (Browser), the Prism Backoffice (Control Plane), and the Azure Infrastructure.

```mermaid
graph TD
    subgraph "Azure Infrastructure"
        AKV[Azure Key Vault]
        EntraA[Entra ID - Tenant A]
        EntraB[Entra ID - Tenant B]
    end

    subgraph "Umbraco Host"
        Middleware[Tenant Resolution Middleware]
        PrismDB[(Prism SQLite DB)]
        AuthService[Dynamic Auth Service]
    end

    User((User / Browser)) -->|Requests tenantA.com| Middleware
    Middleware -->|Lookup Hostname| PrismDB
    PrismDB -->|Return Tenant Config & Key Name| Middleware
    Middleware -->|Request Secret| AuthService
    AuthService -->|Get Secret by Name| AKV
    AKV -.->|Return Secret Value| AuthService
    AuthService -->|Build Auth Scheme| Middleware
    Middleware -->|Redirect to Login| EntraA

```

---

## 2. Authentication Sequence Flow

This sequence diagram details the precise "handshake" that occurs when a user clicks "Log In" on a specific tenant's site.

```mermaid
sequenceDiagram
    autonumber
    participant User as Browser
    participant Umb as Umbraco (Prism)
    participant DB as Prism Database
    participant AKV as Azure Key Vault
    participant Entra as Microsoft Entra ID

    Note over User, Umb: User visits https://tenant-a.com/login

    User->>Umb: GET /login
    Umb->>DB: Query Tenant by Hostname ("tenant-a.com")
    DB-->>Umb: Return Config (ClientID, Authority, SecretKeyName)
    
    Note right of Umb: Secret is missing. Fetch from Vault.

    Umb->>AKV: GetSecretAsync("TenantA-ClientSecret")
    AKV-->>Umb: Return "super-secret-value"

    Note right of Umb: Construct OIDC Challenge dynamically

    Umb-->>User: Redirect to Entra ID (Authorize Endpoint)
    User->>Entra: Authenticate (Username/Password)
    Entra-->>User: Redirect to /signin-oidc with Code
    User->>Umb: POST /signin-oidc (Auth Code)

    Umb->>Entra: Exchange Code for Token (using Secret from Vault)
    Entra-->>Umb: ID Token + Access Token

    Umb->>DB: Find/Create Member (Map "sub" to Member)
    Umb-->>User: Set Session Cookie & Redirect to Home

```

---

## 3. Data Schema Extensions

To support this logic, the `Tenant` entity in the Umbraco database requires specific configuration fields.

```mermaid
erDiagram
    TENANT {
        int Id PK
        string Name "Friendly Name"
        string Hostname "Routing Key (e.g. tenant-a.com)"
        string ThemeColor "Branding"
        string EntraTenantId "Azure Directory ID"
        string EntraClientId "App Registration ID"
        string SecretKeyName "Pointer to Key Vault Secret"
        string CallbackPath "Optional override"
    }

```

---

## 4. Security & Vault Integration

Instead of storing the actual connection string or secret, we use the **Managed Identity** of the hosting environment.

| Component | Responsibility | Access Method |
| --- | --- | --- |
| **Prism DB** | Stores the *Name* of the secret (e.g., `Prism-Secret-TenantA`) | SQLite Connection |
| **Umbraco App** | Requests the secret value using its Identity | `DefaultAzureCredential` |
| **Key Vault** | Validates the App Identity and returns the secret | Azure RBAC |

---
