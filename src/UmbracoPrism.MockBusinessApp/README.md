# UmbracoPrism.MockBusinessApp

A minimal downstream business-app simulator used in local development and integration testing.

## Configuration

`appsettings.json` contains **placeholder values only** — no real tenant IDs, client IDs, or email addresses. This is intentional (SEC-010 — information disclosure prevention).

Real values go in a **gitignored local override** (`appsettings.Local.json`).

### First-run setup

1. Create `src/UmbracoPrism.MockBusinessApp/appsettings.Local.json` (already gitignored by root `.gitignore`).
2. Populate it with your real values:

```json
{
  "PrismBusinessApp": {
    "Tenants": [
      {
        "EntraTenantId": "<your-real-entra-tenant-id>",
        "ClientId": "<your-real-client-id>",
        "Code": "ALPHA-CORP",
        "DisplayName": "Alpha Corporation"
      }
    ],
    "Members": [
      {
        "Email": "your.real@email.com",
        "TenantCode": "ALPHA-CORP",
        "BackOfficeId": "MEMBER-001",
        "Role": "Admin"
      }
    ]
  }
}
```

3. The app reads `appsettings.Local.json` at startup (if present). Values in the local override take priority over `appsettings.json`.

> **Never commit `appsettings.Local.json`** — it is excluded by `.gitignore`. If you accidentally add real IDs to `appsettings.json`, revert them immediately.

## Pattern

This mirrors the secrets management pattern used by `UmbracoPrism.TestSite` (see `src/UmbracoPrism.TestSite/README.md`). `appsettings.Local.json` is the canonical mechanism for local dev overrides across this solution.
