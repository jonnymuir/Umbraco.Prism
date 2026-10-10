# UmbracoPrism.MockBusinessApp

A minimal, separate downstream application, not a business-app simulator that Prism/Wayfinder
hosts or drives. It exists for two narrow, real reasons:

1. **Proves Prism's own Bearer-token identity propagation**: `GET /api/backoffice/me`
   validates the caller's token, resolves their Prism tenant, and returns their role from this
   app's own member directory (`PrismBusinessApp:Members` below). TestSite's dashboard "Call
   Mock Business App API" demo exercises this live.
2. **A real, separate business application that journeys call as the signed-in member**: member
   profile and contributions-file validation. Wayfinder.Umbraco-hosted service blueprints call out to
   it through their own `ISupportSystemClient`, attaching the member's own bearer token (see
   `MockBusinessAppProfileClient` and `MockBusinessAppContributionsClient` in `UmbracoPrism.TestSite`,
   and `docs/walkthroughs/authenticated-business-app-call.md`).

It is also the reference for **how to secure a business API that Prism members call**, so it is
written to be copied. See "What to copy" below.

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

Every route needs a valid bearer token. There is no anonymous route outside Development.

| Route | Purpose |
|---|---|
| `GET /api/backoffice/me` | Resolves the caller's tenant and role from `PrismBusinessApp:Members`; proves auth propagation. |
| `GET /api/backoffice/profile` | The caller's own member record, for their tenant. `registered: false` if they are not a member. |
| `PUT /api/backoffice/profile` | Updates the caller's own contact details. Validated here; 422 with a reason if refused. |
| `POST /api/backoffice/members` | Registers the caller as a member of their own tenant, after they have created an account at the identity provider. 201 when created, 200 if they already were; 403 with a reason if their email is unverified or the tenant has not opened registration. |
| `POST /contributions/submissions` | Uploads a contributions CSV (max 1 MB). Owned by the caller. |
| `GET /contributions/submissions/{id}` and `/file` | Status and annotated result. Only the submitter can read them; anyone else gets 404. |
| `GET /debug/auth` | **Development only, not mapped elsewhere.** Anonymous by design: it diagnoses why bearer validation fails. |

## What to copy

Each of these is a decision you would otherwise have to rediscover. Where a test pins it down, it is named.

1. **Deny by default.** `AddAuthorizationBuilder().SetFallbackPolicy(...RequireAuthenticatedUser())` in
   `Program.cs`, so a route you forget to protect fails closed. Anonymous access is an explicit
   `AllowAnonymous()` with a reason, as in `Diagnostics.cs`. *Tested: `MockBusinessAppHostSecurityTests`
   boots the real app and checks every route, so a route added later is covered automatically.*
2. **Validate the token properly.** `AddPrismAuthentication` pins the issuer to a configured tenant,
   resolves signing keys from that tenant's own authority, and checks lifetime and audience. Set
   **`Audience`** on each OIDC tenant (see below).
3. **Acting for the caller, read from the token only.** `CallerIdentity.From(user, config)` gives the
   tenant and email from validated claims. No handler takes a tenant or user from the body, query or
   headers. *Tested: a write that names another member is ignored; the same person under two tenants has
   two separate records.*
4. **Object-level authorization.** Contributions submissions carry an `OwnerKey` (tenant plus email) and
   `ContributionsStore.Get(id, ownerKey)` returns nothing for anyone else, indistinguishable from an
   unknown id. Do not rely on ids being unguessable. *Tested.*
5. **No server-side request forgery.** A caller can never supply a URL the server then calls. This app
   used to accept a `callbackUrl` and POST to it from an anonymous route, which was exploitable; that
   whole feature was removed. If you need a callback, the receiver owns its address in its own
   configuration.
6. **TLS only.** Outside Development a plain-HTTP request is refused (400), not redirected, because a
   bearer token sent over HTTP is already exposed. Terminate TLS in front of the app and configure
   forwarded headers for your proxy. *Tested.*
7. **Bound everything.** Request bodies are capped (Kestrel 2 MB, uploads 1 MB enforced in the handler,
   because `[RequestSizeLimit]` does not apply to minimal APIs), stores are capped, and requests are rate
   limited per caller (or per address when there is no valid token). *Tested: oversize upload, store cap.*
8. **Do not cache or sniff per-caller responses.** `Cache-Control: no-store`, `X-Content-Type-Options:
   nosniff`. *Tested.*
9. **Diagnostics do not ship.** `/debug/auth` is only mapped in Development; in a deployed environment
   the route does not exist. *Tested.*
10. **No secrets in source.** Real tenant ids, client ids and member emails go in the gitignored
    `appsettings.Local.json`.
11. **Identify people by `sub` and a verified email, never by `preferred_username`.** `preferred_username`
    is the account's login name, which a person chooses when they register, so one can be set to someone
    else's email address. `CallerIdentity` reads the `email` claim for a tenant with its own identity
    provider, and counts it only when `email_verified` is true. A pre-provisioned member is matched by that
    verified email; a self-registered member is matched by `sub`, so a later email change at the provider
    cannot hand their record to someone else. *Tested: a login name that looks like another member's email
    grants nothing; an unverified claim to a member's email cannot read or edit their record.*
12. **Registration is opt-in per tenant, and the token decides everything.** `POST /api/backoffice/members`
    works only for tenants listed in `PrismBusinessApp:SelfRegistration:Tenants` (closed by default), only
    for a verified email, and takes the tenant, identity and role from the token and the server, never the
    body. It is safe to repeat and never overwrites an existing member. *Tested: unverified email, closed
    tenant, Entra tenant, a body that names a tenant or role, repeat calls.*
13. **Key stored data by an id the business owns, not by email.** Profiles are keyed by tenant plus the
    member's back-office id, so a record never follows an email address from one person to another.

### `Audience`: tokens minted for this API

By default (`Audience` unset) the generic-OIDC rule accepts a token if its `aud` or its `azp` is the
tenant's `ClientId`. That keeps Keycloak working with no extra setup, but it means any token issued to the
web client is accepted here, including its ID token. For any API reachable beyond a development machine,
set `Audience` on the tenant and have the identity provider add it to access tokens:

```json
{ "OidcAuthority": "https://idp.example/realms/prod", "ClientId": "prism-client", "Audience": "prism-business-app" }
```

With `Audience` set, only a token whose `aud` contains it is accepted: `azp` is ignored and an ID token
(audience: the client) is rejected. In Keycloak, add an **Audience** mapper to the client with the custom
audience set and "Add to access token" on, "Add to ID token" off; `keycloak/realm-export.json` shows it.
This repo's Keycloak tenant is configured this way. Entra tenants already issue a per-API audience, so
the setting is not used for them.

> **Local Keycloak keeps its database between runs** (`artifacts/aspire/keycloak-data`) and only imports
> `keycloak/realm-export.json` when the realm does not exist yet. If your local realm predates the audience
> mapper, signed-in members' tokens will not carry the audience and every business-app call fails, which the
> journey shows as "We could not send this to the service". Either delete `artifacts/aspire/keycloak-data`
> and restart (the realm is re-imported from the file), or add the mapper to the running realm: in the
> Keycloak admin console, Clients, `prism-client`, Client scopes, `prism-client-dedicated`, Add mapper,
> By configuration, **Audience**, Included Custom Audience `prism-business-app`, Add to access token on,
> Add to ID token off. CI starts fresh, so it is unaffected.
>
> The same applies to **registration**: `registrationAllowed`, email-as-username, `verifyEmail`, the
> `VERIFY_EMAIL` required action and the SMTP server are all in the realm file, so an existing local
> realm needs `artifacts/aspire/keycloak-data` cleared to pick them up. The stack also runs Keycloak
> 26.1 or later, because registration links use `prompt=create`, which 26.0 ignores.

### Registration and email verification

A person registers at the identity provider (Prism's `/auth/register` sends the browser there with
`prompt=create`), which holds the password; this app never sees one. Keycloak will not issue them a token
until they have verified their email, using the `VERIFY_EMAIL` required action. In the realm file
`verifyEmail: true` on its own does nothing: **the required action must also be registered**, or a new user
signs in with `email_verified: false`. This app does not rely on that configuration. It checks
`email_verified` itself before creating a membership, so a provider that is misconfigured, or a different
provider, fails closed. The local stack runs Mailpit (http://localhost:8025) so the verification email can be
read.

### The calling side

`MemberBearerProvider` (in `UmbracoPrism.TestSite`) is the one place a client gets the member's token: it
refuses without a signed-in request, refuses to send it to anything but an HTTPS address, and its HTTP
clients do not follow redirects. Copy it with this app, not instead of it.
