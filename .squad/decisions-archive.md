# Decisions Archive

Historical decisions older than 30 days. Kept for reference.

---

## 📌 2025-07-22: uui-input Accessibility Label Pattern (Isabelle)

Every `uui-input` element must have a `label` attribute, regardless of whether a visible `<label>` element already wraps or precedes it. The UUI component library requires the attribute on the element itself for its internal accessibility wiring.

- **Dynamic fields** (`_renderDynamicField`): use `label=${variable.label}` (in scope from `BrandingMetadata` variable object).
- **Table loop inputs** (`_renderStaticBrandingContent`): use template literals for uniqueness, e.g. `"${variable.name} (desktop override)"`.

Visible labels do not satisfy the UUI component's internal label requirement. Omitting the `label` attribute causes console noise and screen-reader issues.

---

## 📌 2025-07-15: Test Philosophy — Behavioural Contracts (Tangy)

Tests are **behavioural contracts** — they express what the product should *do* from a user/product-owner perspective, not *how* it does it. Tests must remain green after any refactor that preserves observable behaviour.

**Key principles:**

1. **Prefer semantic selectors over structural selectors.** `data-variable="--color-primary"` expresses intent. `uui-table-row:first-of-type` expresses position and breaks if rows are reordered.

2. **Wait for visible state before querying shadow DOM.** Always add `await expect(...).toBeVisible()` before any `evaluate` that depends on async-rendered content.

3. **Follow named-ID patterns** for stable assertions (`#mobile-app-name`, `#mobile-app-id`) with real semantic values.

Additional fixes made alongside: `_fetchBrandingMetadata` fixed with `Promise.race` + 500ms timeout so fetch fires in test environments; duplicate-ID bug fixed by extracting `_renderStaticBrandingContent` from `_renderStaticBrandingTab`.

---
## 📌 2026-04-12: Keycloak localhost redirect_uri convention (Blathers)

**Session Log:** `.squad/log/2026-04-12T07-28-02Z-redirect-uri.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-redirect-uri.md`

### Blathers — Keycloak localhost redirect URI convention

**Context**

The Aspire/TestSite localhost OIDC sign-in flow redirects to Keycloak with callback URLs derived from `src/UmbracoPrism.TestSite/Properties/launchSettings.json`:

- `https://localhost:44345/signin-oidc`
- `http://localhost:9250/signin-oidc`

Although Keycloak 26 accepted `http://localhost:*` and `https://localhost:*` patterns in the imported client JSON, live authorize requests failed with `PRISM-DEV: Invalid parameter: redirect_uri`. Investigation traced the issue to Keycloak persisting wildcard config at import time but not honoring wildcard patterns during runtime redirect URI validation.

**Decision**

Pin the local Keycloak client redirect URIs and web origins to exact TestSite launchSettings URLs instead of using `localhost:*` port wildcards.

**Why**

- Keeps localhost auth deterministic and aligned with repo-owned TestSite ports.
- Avoids relying on wildcard behavior that Keycloak accepts at config import but does not honor during redirect URI validation.
- Ensures the local auth flow is predictable and maintainable.

**Standing Effect**

- When local OIDC clients target the TestSite, keep Keycloak redirect URIs synchronized with exact launchSettings URLs.
- If TestSite localhost ports change in repo config (e.g., `launchSettings.json`), update `keycloak/realm-export.json` redirect URIs and web origins to match.
- Document such changes in ASPIRE_DEV.md and orchestration logs.

**Documentation Impact:**
- ASPIRE_DEV.md: Updated with localhost redirect URI convention note
- keycloak/realm-export.json: Updated to use exact localhost URLs instead of wildcards

---


## 📌 2026-04-12: Aspire TestSite launch profile selection (Brewster)

**Session Log:** `.squad/log/2026-04-12T07:12:41Z-testsite-url.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/brewster-testsite-url.md`

### Brewster — Aspire TestSite launch profile selection

**Context:** `UmbracoPrism.AppHost` launches `UmbracoPrism.TestSite` through Aspire. The AppHost runs under the `https` launch profile, and Aspire tries to use a service launch profile with the same name before falling back to the first profile in the service's `launchSettings.json`.

`UmbracoPrism.TestSite` came from the Umbraco template and its project launch profile is named `Umbraco.Web.UI`, while the first profile is `IIS Express`. That meant Aspire did not pick the profile containing the TestSite `applicationUrl`, so the dashboard showed the resource as running without an advertised URL.

**Decision:** Pin the TestSite launch profile explicitly in AppHost:

```csharp
builder.AddProject("testsite", "../UmbracoPrism.TestSite/UmbracoPrism.TestSite.csproj", launchProfileName: "Umbraco.Web.UI")
```

**Why:** Keeps the Umbraco project's own launch settings intact. Makes Aspire parse the correct `applicationUrl` values for the TestSite. Produces predictable dashboard behavior even though the Umbraco template does not use Aspire's conventional `https` profile name.

**Standing Effect:** When an Umbraco-based project in this repo uses a nonstandard launch profile name, AppHost should select it explicitly rather than relying on Aspire's default launch-profile matching.

---

## 📌 2026-03-22: trycloudflare Redirect URI Rotation Safety (Blathers)

**Session Log:** `.squad/log/2026-03-22-trycloudflare-uri-rotation-and-az-login.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-trycloudflare-uri-rotation.md`

### Blathers — trycloudflare Redirect URI Rotation Safety

**Decision:** Adopt safe rotation behavior for trycloudflare Prism callback URIs in `scripts/dev/start-trycloudflare.sh`.

**Conventions:**
- Preserve all non-trycloudflare redirect URIs unchanged.
- Before adding current tunnel callback URI, remove stale `*.trycloudflare.com/signin-oidc` entries.
- Ensure the current tunnel callback URI exists exactly once in final redirect URI set.
- Print a concise summary count of stale trycloudflare callback entries removed.

**Why:** Prevent redirect URI sprawl in Entra app registrations used for local development while limiting mutation scope to ephemeral trycloudflare callback entries only.

**Documentation Impact:** README local tunnel guidance documents automatic trycloudflare callback rotation and local auth guidance recommends `az login --allow-no-subscriptions` for tenant-selection scenarios.

## 📌 2026-03-22: Tunnel Input Clarity Convention (Blathers)

**Session Log:** `.squad/log/2026-03-22-tunnel-input-clarity.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-tunnel-input-clarity.md`

### Blathers — Entra Client ID + Tenant Selector Clarity

**Decision:** Standardize local tunnel helper input terminology and selector behavior in `scripts/dev/start-trycloudflare.sh`.

**Conventions:**
- Use `ENTRA_APP_CLIENT_ID` as canonical input/config key and wording (Entra Application (Client) ID).
- Keep one-way legacy compatibility: if `ENTRA_APP_OBJECT_ID` exists and `ENTRA_APP_CLIENT_ID` is missing, load legacy value for the run and persist only `ENTRA_APP_CLIENT_ID` on save.
- Accept tenant selector by either tenant name or numeric database id; resolve to canonical `TENANT_ID` before database mutation.
- Fail closed when tenant name has no match or multiple matches; require numeric id for disambiguation.
- Include resolved tenant id and tenant name in completion summary output.

**Why:** Reduce operator ambiguity around Entra identifiers and avoid accidental tenant mutation by allowing deterministic name-or-id selection with explicit duplicate handling.

**Documentation Impact:** README local tunnel guidance now explains Application (Client) ID expectations, tenant selector behavior, and legacy key compatibility.

## 📌 2026-03-22: Cloudflared Local Dev Automation + Security Guardrails (Blathers + Copper)

**Session Log:** `.squad/log/2026-03-22-cloudflared-dev-tooling.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-cloudflare-dev-tooling.md`
- `.squad/decisions/inbox/copper-cloudflare-script-security.md`

### Blathers — Local Tunnel Dev Tooling Convention

**Decision:** Standardize on `scripts/dev/start-trycloudflare.sh` for temporary public callback setup when running Prism tenant auth locally.

**Convention:**
- Use repo-root `.prism_tunnel.conf` for script inputs and enforce file mode `600`.
- Derive redirect URI as `<tunnel-url>/umbraco/oauth_complete`.
- Update local SQLite tenant hostname (`prismTenants.hostname`) for an operator-selected numeric tenant id.
- Manage cloudflared lifecycle and cleanup via script traps.
- Enforce dependency checks, numeric tenant id validation, hostname validation, startup timeout handling, and minimal sensitive output.

**Why:** Reduce manual drift between Entra redirect configuration and Prism tenant hostname while keeping local auth setup repeatable and safer by default.

---

### Copper — Security Guardrails for trycloudflare Helper

**Decision:** Add fail-closed input and hostname guardrails to the helper script and document explicit dev-only security boundaries in README.

**Guardrails Adopted:**
- Validate `LOCAL_PORT` is within `1-65535`.
- Validate `ENTRA_APP_OBJECT_ID` format as GUID.
- Accept and persist hostnames only under `*.trycloudflare.com`.
- Keep config permission hardening and cleanup behavior.
- Emit explicit warning that script mutates Entra redirect URIs and local tenant hostname for local development only.
- Document least-privilege Azure permissions and local/test DB targeting guidance.

**Why:** Prevent accidental hostname substitution and malformed mutation inputs, and make blast radius assumptions explicit for local operators.

**Follow-up Candidates:**
- Optional parameterized SQLite invocation mode for defense-in-depth.
- Optional explicit confirmation prompt before Entra redirect URI mutation.

## 📌 2026-03-22: Docs + Security Sprint Round 1 (Celeste + Copper)

**Session Log:** `.squad/log/2026-03-22-docs-security-sprint-round1.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/celeste-xml-doc-baseline.md`
- `.squad/decisions/inbox/copper-cia-hardening-round1.md`

### Celeste — XML Documentation Baseline

**Decision:** Establish a low-risk XML documentation baseline across high-impact `UmbracoPrism.Core` public/protected API surfaces, prioritizing Auth, Services, Middleware, and boundary models/interfaces.

**Conventions:**
- Document public/protected classes, interfaces, methods, and properties in scope.
- Use concise summaries with behavior-accurate wording and no implied guarantees.
- Add `param`/`returns` details when request, tenant, or security context matters.
- Favor security-aware wording on tenant/auth/secret boundaries.
- Avoid noisy docs on private/internal details unless required for comprehension.

**Why:** Improve IntelliSense, onboarding clarity, and integration safety on core runtime surfaces without introducing feature-risk refactors.

**Validation:** `dotnet build UmbracoPrism.sln` and `dotnet test src/UmbracoPrism.Core.Tests/UmbracoPrism.Core.Tests.csproj -c Release` both passed.

---

### Copper — CIA Hardening Round 1

**Decision:** Apply fail-closed tenant isolation hardening in token/cookie and downstream JWT validation paths.

**Implemented Rules:**
- `PrismContext.GetAuthorizationHeaderAsync` only returns bearer tokens when principal `tid` matches `CurrentTenant.EntraTenantId`.
- `PrismContext.RefreshTokenAsync` enforces the same tenant match before any refresh call.
- Refresh fails closed when required tenant OIDC config (`EntraTenantId`, `EntraClientId`, `SecretKeyName`) or resolved secret is missing.
- `PrismAuthExtensions` issuer validation requires exact URI host/path binding to token `tid` plus configured tenant allow-list membership.
- `PrismAuthExtensions` audience validation requires `aud` to match the configured client ID for the same token `tid`.
- Signing-key resolution is denied for unconfigured tenant IDs.

**Why:** Strengthen confidentiality and integrity boundaries by preventing cross-tenant token reuse and permissive issuer/audience acceptance.

**Regression Coverage Added:**
- Principal tenant mismatch blocks bearer header and refresh.
- Issuer host mismatch rejected even when tenant appears in path.
- Cross-tenant audience rejected; same-tenant audience accepted.

**Follow-up Risk:** Token refresh circuit breaker scope remains app-wide; per-tenant breaker partitioning remains a recommended next slice.

**Validation:** Build and test suite passed for this hardening round.

## 📌 2026-03-22: Team Expansion + Security Directive Captured (Scribe)

**Session Log:** `.squad/log/2026-03-22-team-expansion-docs-security.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/copilot-directive-20260322-201034.md`

### Team Expansion Decision

**Decision:** Add two specialist members to the active roster:
- **Celeste** as Documentation Engineer
- **Copper** as Security Engineer

**Why:** Current delivery needs explicit ownership for documentation quality and security-hardening depth alongside implementation velocity.

### Security Directive (Jonny Muir via Copilot)

**Directive:** Security is critical across confidentiality, integrity, and availability. There must be no cross-tenant authentication leakage and no tenant data leakage. OAuth implementation must preserve tenant-safe behavior and avoid single-tenancy cache assumptions (including MSAL-style patterns).

**Team Implication:** Treat tenant isolation as a hard invariant for auth flows, cache boundaries, and runtime data access.

---

## 📌 2026-03-22: P0 Implementation Round 1 — Async OIDC Cache, Token Resilience, Auth Model Split

**Session Log:** `.squad/log/2026-03-22-p0-implementation-round1.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-issue2-impl.md`
- `.squad/decisions/inbox/blathers-issue3-impl.md`
- `.squad/decisions/inbox/tom-nook-auth-split.md`

### Issue #2 — Async-warmed signing-key cache (Blathers)

**Decision:** Introduce `IPrismSigningKeyCache` (singleton, `ConcurrentDictionary`, 12h TTL) and pre-warm it from `PrismTenantMiddleware.InvokeAsync` immediately after tenant resolution. The synchronous `IssuerSigningKeyResolver` reads from cache only — zero network I/O on the hot path.

**Why:** `IssuerSigningKeyResolver` is a synchronous delegate and cannot be made async without changing the token validation infrastructure. Pre-warming in the first async request gate is the only non-blocking option.

**Deferred:** `PrismAuthExtensions.AddPrismAuthentication` (downstream API JWT validation) retains the sync-blocking pattern; only blocks cold-start first-request. Address in a future slice.

**Build/Tests:** ✅ 14/14

---

### Issue #3 — Token refresh resilience (Blathers)

**Decision:** `IPrismTokenRefreshService` / `PrismTokenRefreshService` singleton wraps all token-endpoint HTTP calls in a Polly 8.6.6 pipeline: **CircuitBreaker (outer) → Retry (inner) → HTTP call**.

**Why (CB outer, Retry inner):** Circuit breaker samples one outcome per fully-exhausted retry sequence. If circuit is open, short-circuits immediately without invoking Retry or HTTP. `ShouldHandle` triggers on 5xx, `HttpRequestException`, `TaskCanceledException` only — 4xx is not retried (invalid token; retry would not help). Token strings are never logged.

**Known limitation:** Circuit breaker is shared app-wide; per-tenant circuit breakers are a recommended follow-up issue.

**Build/Tests:** ✅ 19/19 (5 new)

---

### Issue #4 — Entra-first auth model + split into #8, #9, #10 (Tom Nook)

**Decision:** Entra token claims are the single source of truth for all Prism authorization decisions. `PrismAdminHandler` migrates from Umbraco local group membership to Entra claim evaluation in three sequenced child issues.

**Child issues:**

| GH Issue | Title | Owner | Gate |
|----------|-------|-------|------|
| #8 | Auth compatibility mode (Entra claim + Umbraco fallback) | squad:tom nook | None |
| #9 | Auth policy test suite | squad:blathers | After #8 shape finalized |
| #10 | Auth fallback removal (breaking change) | squad:tom nook | #8 deployed + #9 CI-green + one release cycle |

**Safety guardrails:**
- #8 default config is backwards-compatible (`GroupAliases` continues to work).
- Warning log on every Umbraco fallback activation.
- `StrictEntraMode: true` without `EntraAdminClaimValues` → `InvalidOperationException` on startup.
- #10 shipping gate written into the issue body — not reliant on process memory.

---

## 📌 2026-03-22: Ralph Kickoff Round – P0 Architecture Issues #2, #3, #4 (Blathers + Tom Nook)

**Session Log:** `.squad/log/2026-03-22-ralph-kickoff-p0.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-p0-kickoff.md`
- `.squad/decisions/inbox/tom-nook-auth-model-kickoff.md`

### Issue #2 & #3 – P0 Auth Hardening (Blathers)

**Decision:** Execute in two sequential first PRs.

1. **Issue #2 first PR:** Remove sync-blocking OIDC metadata calls from request-path key resolvers; introduce tenant-scoped async-warmed signing key cache.
2. **Issue #3 first PR:** Add retry with exponential backoff plus per-tenant circuit breaker to token refresh path; cover resilience behavior with focused unit tests before broader refactor.

**Why:** #2 reduces immediate request-path contention risk and removes known sync bottlenecks. #3 touches correctness-sensitive token lifecycle behavior and must ship with tests to avoid auth regressions. Sequencing avoids mixing two high-risk auth changes into one PR.

**Guardrails:** Preserve tenant isolation semantics and issuer/audience correctness. Keep first PR scopes narrow; no policy model changes in these kickoff PRs.

### Issue #4 – Standardize Authorization Model (Tom Nook)

**Decision:** Adopt Entra token claims as the single source of truth for Prism authorization decisions.

**Why:** Current authorization is split — tenant isolation uses Entra `tid` claim (`PrismTenantHandler`); admin authorization uses Umbraco backoffice local group aliases (`PrismAdminHandler`). This split can drift when Entra and Umbraco group memberships are out of sync, creating unpredictable effective permissions.

**Target Model:**
- Keep Umbraco backoffice access policy for entry to management UI/API surface.
- Standardize Prism-specific authorization (`PrismAdmins`, tenant-aware checks) on Entra claims.
- One claim-driven model for both admin and tenant decisions with explicit configuration.

**First Implementation Slice:**
1. Introduce authorization options for Entra admin claim evaluation (claim type + allowed values + compatibility toggle).
2. Update `PrismAdminHandler` to evaluate Entra claims first with optional temporary fallback to Umbraco groups.
3. Keep `PrismTenantHandler` Entra-claim based; add tests for mismatch/missing scenarios.
4. Add policy tests for `PrismAdmins` and tenant isolation paths.

**Safety & Migration:** Start in compatibility mode (Entra-first, optional Umbraco fallback); emit warning logs when fallback fires; fail fast on startup if strict Entra mode is enabled without configured claim values.

**Follow-up Split (recommended):**
1. Core implementation + compatibility mode + tests.
2. Migration hardening: diagnostics/telemetry and strict-mode rollout guidance.
3. Optional cleanup: remove legacy Umbraco-group fallback after adoption window.

---

## 📌 2026-03-22: Architecture Review Complete (Tom Nook)

**Session Log:** `.squad/log/2026-03-22-architecture-review.md`

**Scope:** Core services, middleware, identity, persistence, frontend integration

**Key Findings:**
- ✅ Stateless OIDC architecture is elegant and scales horizontally
- 🔴 P0 Risks: Blocking async in OIDC config; token refresh without retry; authorization inconsistency (Entra vs. Umbraco groups)
- 🟠 Scaling concerns: Tenant cache 30-min TTL; CSS scan on cold start; 1K tenant ceiling
- 🟡 OIDC metadata cache never invalidates; mobile bundle missing validation + rate limits

**Decision Inbox (3 items):**
1. Extract TokenRefreshService with Polly retry/circuit breaker (P0) → Blathers
2. Standardize authorization on Entra groups (P0) → Blathers
3. Document tenant rejection policy (P0) → Tom Nook

**Handoff:** Isabelle (branding UI), Blathers (token resilience + P1 cache/security), Tangy (edge case tests)

---

## 📌 2026-03-22: Ralph Triage Complete (Tom Nook)

**Session Log:** `.squad/log/2026-03-22-ralph-triage.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/tom-nook-architecture-review.md`
- `.squad/decisions/inbox/tom-nook-ralph-triage.md`

**Outcome:**
- Ralph triage completed for issues #2 through #7.
- Each issue now has one primary `squad:*` owner label.
- Domain labels were preserved (`architecture`, `security`, `performance`, `testing`).
- Triage inbox label `squad` was kept unchanged.

**Primary Owners:**
- #2 -> `squad:blathers`
- #3 -> `squad:blathers`
- #4 -> `squad:tom nook`
- #5 -> `squad:blathers`
- #6 -> `squad:isabelle`
- #7 -> `squad:tangy`

**Scope Notes:**
- #4 is expected to split into architecture decision and implementation rollout if needed.
- #6 may split if optimization work proves backend-dominant.
- #7 is expected to split into child issues after reliability test planning.

---

## 📌 2026-03-22: Squad initialized (Animal Crossing cast)

**Team roster hired:**
- Tom Nook: Lead (architect, scope, code review)
- Isabelle: Frontend Dev (Web Components, Storybook, UI)
- Blathers: Backend Dev (C# APIs, services, auth, database)
- Tangy: Tester (testing strategy, edge cases, quality)
- @copilot: Coding Agent (async issue work)
- Scribe: Session Logger (memories, decisions, logs)

**Universe:** Animal Crossing (character names drawn from Nook family empire, Isabelle's assistant role, Blathers' curator expertise, Tangy's cranky attention to detail)

**Casting policy:** One universe per assignment, persistent names, no re-casting. Stored in `.squad/casting/` (policy.json, registry.json, history.json).

---

## 📌 2026-03-24: Authorization Planes Decision (Jonny Muir)

**Decision:** Treat Prism member tenant isolation and Prism backoffice admin authorization as two different identity planes by design.

**Policy:**
- Member plane (tenant-facing websites) remains Entra claim-based and tenant-isolated.
- Backoffice plane (shared Umbraco admin surface) remains controlled by Umbraco backoffice groups via `PrismAdmins`/`GroupAliases` unless a future requirement explicitly mandates unification.

**Why:** This deployment model intentionally supports multiple member tenants on one shared Umbraco backoffice. Unifying both planes under one model is not required for current product behavior and can introduce unnecessary migration risk.

**Issue impact:**
- GitHub issue #4 was closed as **not planned** with this rationale.
- Any future unification proposal must start from a new issue with explicit deployment constraints and migration justification.

---

## 📌 2026-03-24: Follow-through on Authorization Planes Decision

**Decision:** Close child unification issues and preserve only architecture-aligned follow-up work.

**Issue actions:**
- Closed as **not planned**: #8, #9, #10 (all tied to Entra-first backoffice admin unification path).
- Opened replacement issue: #11 (**Auth: Policy test suite for two-plane authorization model**) to retain needed test coverage without changing the chosen architecture.

**Why:** #8/#9/#10 were implementation slices for the rejected unification direction. Test coverage remains valuable, so it was re-scoped into #11 for the accepted two-plane model.

---

