# UmbracoPrism.MockBusinessApp

A minimal, separate downstream application, not a business-app simulator that Prism/Wayfinder
hosts or drives. It exists for two narrow, real reasons:

1. **Proves Prism's own Bearer-token identity propagation**: `GET /api/backoffice/me`
   validates the caller's token, resolves their Prism tenant, and returns their role from this
   app's own member directory (`PrismBusinessApp:Members` below). TestSite's dashboard "Call
   Mock Business App API" demo exercises this live.
2. **A real, separate downstream support system**: `SupportSystemEndpoints` (`POST /submissions`,
   `GET /submissions/{id}`, `GET /queue`, `POST /queue/{id}/decide`) mirrors the exact shape the
   core Wayfinder repo's own `SafetyNetUnderwriting` reference implementation uses (see
   `docs/guides/support-systems.md` there). Wayfinder.Umbraco-hosted service blueprints call out
   to it via their own `ISupportSystemClient`, see `MockBusinessAppContributionsClient` in
   `UmbracoPrism.TestSite`.

Service design itself, citizen journeys, caseworker worklists, blueprint authoring, is entirely
Wayfinder.Umbraco's job now, hosted in-process by `UmbracoPrism.TestSite`. This app owns none of
that; it has no engine, no blueprint store, and no editor of its own.

## Configuration

`appsettings.json` contains **placeholder values only**: no real tenant IDs, client IDs, or email addresses. This is intentional (SEC-010, information disclosure prevention).

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

> **Never commit `appsettings.Local.json`**, it is excluded by `.gitignore`. If you accidentally add real IDs to `appsettings.json`, revert them immediately.

## Pattern

This mirrors the secrets management pattern used by `UmbracoPrism.TestSite` (see `src/UmbracoPrism.TestSite/README.md`). `appsettings.Local.json` is the canonical mechanism for local dev overrides across this solution.

## Endpoints

| Route | Auth | Purpose |
|---|---|---|
| `GET /api/backoffice/me` | Bearer token (Prism) | Resolves the caller's tenant + role from `PrismBusinessApp:Members`; proves auth propagation. |
| `POST /submissions` | none | Accepts a support-system submission (arbitrary JSON fields + an optional `callbackUrl`). |
| `GET /submissions/{id}` | none | Polls a submission's decision status. |
| `GET /queue` | none | Plain-HTML staff queue, approve/reject pending submissions by hand. |
| `POST /queue/{id}/decide` | none | Records a decision; fires the submission's `callbackUrl` webhook if one was given. |
| `GET /debug/auth` (dev only) | none | Diagnostics for the OIDC/backchannel wiring, see the endpoint's own code for what it reports. |

The support-system endpoints are deliberately unauthenticated, a real downstream system's own
auth model is its business, not something Wayfinder or Prism prescribes (mirrors
`SafetyNetUnderwriting`'s own reference-app posture in the core Wayfinder repo).
