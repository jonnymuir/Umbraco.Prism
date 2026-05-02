# Codespaces 401 — Security diagnosis (Copper)

**Diagnosis only — no code changes. Generated 2026-05-02T09:24:57+01:00.**
**Bedrock rule honoured throughout: no shortcut weakens token validation, issuer trust, or tenant isolation. All remediation candidates preserve the security boundary.**

---

## TL;DR (most likely root cause)

The 401 the dashboard surfaces is **almost certainly produced by `DownstreamDemoController.Get` itself** (`src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs:62-63`), not by `MockBusinessApp`. The controller does cookie auth (works — the dashboard rendered) and then asks `IPrismContext.GetAuthorizationHeaderAsync()` for a bearer header. That call returns `null` because `IsPrincipalBoundToCurrentTenant` rejects the principal — and that rejection happens because `PrismContext.CurrentTenant` is `null` (or has a different `OidcAuthority` than the principal's `iss` claim). The most plausible reason `CurrentTenant` is `null` on this Codespace is that the `prismTenants` row for hostname `upgraded-bassoon-4g5v5r9vghq5p6-44345.app.github.dev` was never seeded — `DemoTenantSeeder.BuildCodespaceTestSiteHostname()` only fires when `CODESPACE_NAME` is visible to the TestSite process, and there are several plausible failure modes (env not inherited by the Aspire-launched dotnet, seeder ran before `CODESPACE_NAME` exported, an existing localhost-only DB from a pre-Codespaces run). UI summary text "Your Prism session is no longer valid…" is generated client-side purely from `statusCode === 401` (`memberDashboard.cshtml:218-220`), so the wording does not distinguish between this 401 and a real downstream 401 — but the failure mode that fits "fresh Codespace, login worked, dashboard rendered, button 401s" most cleanly is the tenant-binding rejection in `PrismContext`.

## Hypotheses (ranked)

### H1 — `CurrentTenant` not resolved for the Codespaces hostname → controller returns 401 itself  *(likelihood ≈ 55%)*

**Evidence:**
- `PrismTenantMiddleware.InvokeAsync` (`src/UmbracoPrism.Core/Middleware/PrismTenantMiddleware.cs:26-50`) resolves tenant by `Request.Host.Host` and logs **"Unknown tenant domain: {Host}"** when the row is missing. The pasted log even contains a prior instance of that warning ("Unknown tenant domain: localhost").
- `PrismContext.GetAuthorizationHeaderAsync` (`src/UmbracoPrism.Core/Models/PrismContext.cs:54-58`) calls `IsPrincipalBoundToCurrentTenant(...)` which returns `false` when `CurrentTenant == null` — so `LastAuthorizationFailureReason = "tenant-mismatch"` and the method returns `null`.
- `DownstreamDemoController.Get` (`Controllers/DownstreamDemoController.cs:61-63`) returns `Unauthorized()` whenever the auth header is null. That's the 401 the JS sees.
- `DemoTenantSeeder.BuildCodespaceTestSiteHostname` (`src/UmbracoPrism.TestSite/DemoTenantSeeder.cs:69-75`) requires `CODESPACE_NAME` to be set on the **TestSite** process. AppHost reads it (line 7), but the TestSite child process only inherits it if the AppHost shell had it exported — `on-start.sh` launches `nohup dotnet run --project src/UmbracoPrism.AppHost` and relies on inheritance.
- TestSite Program.cs override (`Program.cs:35-46`) rewrites `Request.Host` from `localhost:44345` to the public host *before* the request hits `PrismTenantMiddleware`. So the host the middleware sees is the Codespaces hostname; if no row exists for it, resolution silently fails.

**What to check next:**
- `SELECT Id, Hostname, OidcAuthority FROM prismTenants;` against the running TestSite DB.
- TestSite logs around startup for `DEMO SEEDER: Created tenant 'Local Dev (Keycloak) (Codespaces)'`.
- `printenv CODESPACE_NAME` from inside the running TestSite process (e.g. via `/debug/auth` analogue, or `proc/<pid>/environ`).

### H2 — JWT issuer mismatch on `MockBusinessApp` → real downstream 401  *(likelihood ≈ 25%)*

**Evidence:**
- AppHost overrides `PrismBusinessApp__Tenants__2__OidcAuthority` (`AppHost/Program.cs:87`) to `https://{name}-8443.app.github.dev/realms/prism-dev`. Token's `iss` is set by Keycloak's `KC_HOSTNAME` (line 61) — same string. **Should** match.
- But `IssuerValidator` does case-insensitive equality on the *exact* authority including path (`Shared/Extensions/PrismAuthExtensions.cs:119-126`). Trailing-slash, casing, or env-var-binding misordering (e.g. tenants array reordered, so index 2 isn't PRISM-DEMO any more) would make this fail closed. Healthy — this is the bedrock-respecting failure mode.
- Failure log path is also explicit: the `OnAuthenticationFailed` console writer (`PrismAuthExtensions.cs:62-66`) prints `token.iss` vs configured authorities. If Jonny captures MockBusinessApp's stdout on the failing call, this hypothesis is confirmable in one log line.

**What to check next:**
- MockBusinessApp stdout immediately after pressing the button.
- Hit `MockBusinessApp /debug/auth` (`Program.cs:190-227`) — it prints the resolved tenants and probes the backchannel.

### H3 — TestSite → MockBusinessApp HTTPS call cannot establish trust  *(likelihood ≈ 8%)*

**Evidence:**
- `dotnet dev-certs https --trust` is best-effort on Linux (`on-create.sh` runs it with `|| true`).
- `WorkflowApiBaseUrl = https://localhost:7245` is hardcoded in AppHost (`AppHost/Program.cs:32`); HTTPS handshake to a self-signed dev cert often fails server-side on Codespaces.
- BUT: the controller catches `HttpRequestException` and returns `Ok(...)` with `statusCode=0, statusText="Network Error"` — UI would then say "We could not reach the Mock Business App", **not** "session no longer valid". So this is unlikely as the *current* failure shape, though it could surface once H1/H2 are fixed.

### H4 — Cookie not sent / cookie auth fails on the API call  *(likelihood ≈ 7%)*

**Evidence:**
- The dashboard fetch includes `credentials: 'include'` (`memberDashboard.cshtml:298`). Same-origin (44345 ↔ 44345) so SameSite=Lax is fine.
- If the cookie *were* missing, `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` would 401 the request before the controller body runs. Functionally indistinguishable from H1 from the UI side; but H1 is the strictly more likely scenario because dashboard rendered (cookie exists) and the GET is same-origin.

### H5 — Token actually expired and refresh blocked by Codespaces  *(likelihood ≈ 5%)*

**Evidence:**
- `PrismContext.RefreshTokenAsync` (`PrismContext.cs:97-200`) hits `OidcAuthority/protocol/openid-connect/token` directly — that endpoint is the **public Keycloak URL**, not the backchannel. In Codespaces, server-side calls to the public `*.app.github.dev` URL are blocked by the GitHub forwarded-port proxy (this is the exact reason `KEYCLOAK_BACKCHANNEL_URL` exists for metadata fetches). **Token *refresh* is NOT routed through the backchannel** — only signing-key fetch is. So a token-refresh attempt in Codespaces would fail.
- However, the user just signed in, so the access token shouldn't be expired yet. This bites later in the session, not immediately.

## What we'd need to confirm

| Need | Where Jonny can pull it |
|---|---|
| MockBusinessApp `[PRISM AUTH FAILED]` block printed (or absent) on the failing click | Aspire dashboard → MockBusinessApp logs |
| `prismTenants` row for the Codespace hostname | `dotnet ef`/SQL against the runtime DB OR seeder log line `DEMO SEEDER: Created tenant '... (Codespaces)'` |
| TestSite's `LastAuthorizationFailureReason` for the failing call | Add `?include-failure-reason=1` debug, or already exposed at `/api/prism/downstream-demo/session-contract` (`DownstreamDemoController.cs:172`) — call this endpoint right after the failure |
| The token's `iss` and `aud` claims | `/debug/auth` on MockBusinessApp + the OnAuthenticationFailed log; or paste the access token into jwt.io |
| `CODESPACE_NAME` visible in TestSite process | `cat /proc/$(pgrep -f UmbracoPrism.TestSite)/environ \| tr '\0' '\n' \| grep CODESPACE` |

The single most informative artefact: **the response body of `/api/prism/downstream-demo/session-contract`** taken *immediately* after the 401. That endpoint's `downstream.failureReason` field is exactly `LastAuthorizationFailureReason` — it will say `tenant-mismatch`, `missing-cookie-principal`, `token-expired`, or `refresh-failed`, which collapses the hypothesis space to one.

## Remediation principles (security-bedrock-respecting)

1. **Tenant resolution must remain authoritative.** Fix by *seeding the correct row*, never by relaxing `IsPrincipalBoundToCurrentTenant` to "skip if tenant is null". The strict tenant binding in `PrismContext.cs:101-103` is a deliberate cross-tenant isolation control (CIA — Confidentiality).
2. **Issuer & audience validation stay strict.** If H2 lands, the fix is to align config with the actual issuer string (env var override correctness, trailing-slash hygiene), not to flip `ValidateIssuer/ValidateAudience` to `false` on `MockBusinessApp`. The `IssuerValidator` at `PrismAuthExtensions.cs:81-127` is the correct guard.
3. **Backchannel URL stays scoped to Codespaces and to metadata fetches only.** Existing pattern is correct (Copper review 2026-04-21 in history). Do **not** widen it to refresh-token grants without a paired plan: the refresh endpoint must remain HTTPS to the public Keycloak URL OR have an equally hardened internal channel; never silently downgrade to plaintext HTTP on prod-shaped builds.
4. **`KEYCLOAK_BACKCHANNEL_URL` guard at `MockBusinessApp/Program.cs:38-41` is non-negotiable.** Any fix must keep that production fail-loud check in place.
5. **HTTPS dev-cert friction (H3) is solved by configuring the inter-service `HttpClient`, not by `ServerCertificateCustomValidationCallback = (_,_,_,_) => true`.** Acceptable patterns: trust the dev cert chain on Codespaces (`update-ca-certificates`), or call the HTTP endpoint over loopback **and** keep authority/issuer trust unchanged. Disabling cert validation is forbidden under the bedrock rule.

## What we MUST NOT do (the tempting shortcuts)

- ❌ Set `RequireHttpsMetadata = false` globally to "make Codespaces work". The metadata path is already hardened via the scoped backchannel — extending laxity beyond that is a regression.
- ❌ Set `ValidateIssuer = false` or `ValidateAudience = false` on `MockBusinessApp` JwtBearer. Removes cross-tenant token-replay protection. The Entra-path issuer regex (`PrismAuthExtensions.cs:103-109`) and the OIDC strict-equality (`121-124`) are anti-confused-deputy controls.
- ❌ Disable `IsPrincipalBoundToCurrentTenant`. It's the gate that prevents tenant A's cookie from minting a bearer for tenant B's API.
- ❌ Whitelist `*.app.github.dev` as a trusted issuer family. Issuer trust must be *exact-string*, never glob/suffix.
- ❌ Bypass `HttpClient` cert validation in `prism-downstream-demo`. If TLS to localhost:7245 is the blocker, the fix is cert trust at the OS/runtime layer, not ignoring chain validation.
- ❌ Add a `Development`-only "skip tenant binding" branch. Once such branches exist they're cargo-culted into staging. Bedrock = no environment-specific auth shortcuts.

## Where Blathers should look (handoff)

Blathers — backend agent — please:

1. **Confirm or refute H1 first** by inspecting the running `prismTenants` table and TestSite startup log for the `DEMO SEEDER` line for the Codespace hostname. If the row is missing, the `DemoTenantSeeder` invocation path is the bug surface (`DemoTenantSeeder.cs:33-67`) — env-var visibility, ordering against `UmbracoApplicationStartedNotification`, or a stale DB carried over from a non-Codespaces run.
2. **If H1 is clean**, hit `/api/prism/downstream-demo/session-contract` and read `downstream.failureReason`. That single field collapses to the next hypothesis. If `tenant-mismatch` despite a row existing, the principal's `iss` doesn't match the seeded tenant's `OidcAuthority` (look at `IsGenericOidcPrincipalBoundToCurrentTenant` in `PrismContext.cs:233-260`).
3. **If H2**, capture MockBusinessApp's `[PRISM AUTH FAILED]` block — it reveals `token.iss` vs configured authorities deterministically.
4. **Cross-check** that the env-var-bound config index `PrismBusinessApp__Tenants__2__OidcAuthority` is still the PRISM-DEMO tenant (`MockBusinessApp/appsettings.json` ordering — currently index 2 = PRISM-DEMO, but any reorder breaks the AppHost override silently).
5. **Out of scope for the fix but worth noting**: refresh-token grant in `PrismContext.RefreshTokenAsync` does not use the backchannel URL — once a session lasts past the access-token TTL in Codespaces, refresh will fail. That's a separate bedrock-aligned hardening item (route to the same internal HTTP endpoint the backchannel uses, with `iss` validation untouched), not something to bundle into this fix.

— Copper
