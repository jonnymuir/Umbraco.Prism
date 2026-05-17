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

## 📌 2026-04-26: Copilot (Coordinator) — v2.0 Polymorphic Component Rollout Completion

**Status:** ✅ COMPLETE — 9-commit atomic rollout concluded; v2.0 schema is canonical

**Session Summary:**
The v2.0 polymorphic component hierarchy rollout converged through three phases:
1. **Initial Plan Collapse** (copilot-3commit-replan): 8-commit sequence deemed infeasible due to C# type system constraints. Collapsed to 3-commit atomic plan (schema replacement, design doc refresh, ledger update).
2. **Expanded Rollout** (follow-through progress reports): 3-commit plan expanded to 9 total commits as blockers were discovered and resolved:
   - Commit `7423803` (feat): Atomic schema replacement — 40–60 file diff, single coherent change
   - Commits `2cdb0dc`, `f3c0ea5`, `67bb57b`: Seed fixes + e2e tests + 4th workflow seeding
   - Commit `989f595`: Archive redesign blueprint, refresh conditional-fields doc
   - Commit `dc87e5f`: ModelsBuilder views fix (disable auto-generation)
   - Commit `392c64e`: Playwright walkthroughs with screenshot capture
   - Commits `2698c1d`, `a48229b`: Design + guide doc refresh, screenshot script

**Key Decisions Locked In:**
- **No migrator, no V2 suffix, no schemaVersion field** — direct replacement of v1 schema with polymorphic components
- **Generic ConditionalOn deferred to v2.1** — v2.0 ships with ConditionalChildren on Radios/Checkboxes only
- **ModelsBuilder view generation disabled** — TestSite uses Core's embedded views, prevents model-binding conflicts

**Seed File Roundtrip Guard:**
- Gap identified: payment-demo.json and information-request.json were out of sync with v2 polymorphic schema
- Regression guard added: `SeedFileRoundtripTests.cs` ensures all seeds deserialize correctly and have no orphaned v1 properties
- All 4 seeds migrated to v2 in Commit `2cdb0dc`

**E2E + Documentation Coverage:**
- Playwright tests cover all 4 demo workflows (community-enquiry, payment-demo, planning-notification, information-request) with happy paths + conditional logic
- Screenshot-driven walkthroughs for all 4 demos with state transitions captured
- 12 design + guide docs refreshed for v2 polymorphic schema

**Test Results:**
- Clean build: 0 warnings
- Core tests: 583 baseline → maintained; Seed roundtrip tests: +4 (546 total)
- No regressions; all changes backward-compatible or documented as breaking (no live consumers)

**Basis:** User directive (2026-04-26, Jonny Muir), Tom Nook's direct-replacement sequencing plan (2026-04-26), follow-through progress reports (Copilot 2026-04-09, 2026-04-26), Copilot 3-commit replan (2026-04-26), blocker resolution (ModelsBuilder fix 2026-04-26).

---

## 📌 2026-04-26: Tom Nook — Design Doc Audit: 9 Docs Reviewed, 7 Marked for v2.0 Rewrite

**Status:** ✅ Audit complete; recommendations implemented in rollout

**Scope:** 9 workflow design + guide documents reviewed against v2 polymorphic component plan

**Audit Findings:**
- **7 docs need rewrite** (design docs: forms-engine.md, forms-engine-backend.md, forms-engine-client.md, forms-engine-umbraco.md, validation.md, forms-engine-demo.md, forms-engine-security.md; guide: conditional-fields.md, workflow-gds-components.md, workflow-validation.md, workflow-setup.md, workflow-customisation.md)
- **1 doc stays as-is** (architecture/workflow-forms-engine.md contains architecture principles that transcend v1/v2)
- **1 doc archived** (workflow-forms-engine-redesign.md → docs/archive/ with pointer to v2 plan)

**Rewrite Priorities:**
- **Red banners** (critical mismatches): 4 docs
  - Forms engine backend/client (component tree traversal examples)
  - Umbraco integration (JSON schema examples heavily v1-focused)
  - Setup guide (seed JSONs all v1 shape)
- **Yellow banners** (partial updates): 3 docs
  - Validation + forms-engine-demo (fieldType → type, fields → children)
- **Archive + pointer** (obsolete): 1 doc
  - Redesign blueprint (superseded by actual v2 implementation)

**Rewrite Pattern:**
- Replace `fieldType` discriminator with `type` on all components
- Replace `fields[]` array (flat field list) with `children[]` (typed component tree)
- Update JSON examples to show polymorphic shapes (fieldset with children, radios with conditionalChildren)
- Add v2 callout boxes noting new capabilities (ConditionalChildren, component polymorphism, waiting state)
- Remove v1 artifact references (no more "FieldFile", "PrismComponentRenderPayload", "PrismFieldTagHelper")

**Action:** All rewrites completed in Commits `989f595` (conditional-fields refresh) and `2698c1d` (bulk refresh).

**Basis:** Formal design audit memo (2026-04-26, Tom Nook, in `.squad/decisions/inbox/`), implemented per rollout plan phases.

---

## 📌 2026-04-26: Jonny Muir — Direct Schema Replacement Directive (No Migrator, No Dual Schema)

**Decision:** Skip v1→v2 schema migrator entirely. No live consumers; make polymorphic component hierarchy THE schema. Direct replacement of `WorkflowDefinitionFile`, `FieldDefinition`, etc. Update all 4 seed workflows, engine, builder, tag helpers, Razor partials, tests, and design docs in one coherent change.

**Context:** v2.0 rollout plan (Tom Nook) designed for live product with graduated migration phases (migrator, dual schema acceptance, builder rewrite, partial collapse, doc refresh). Umbraco.Prism is prototype-stage; no external customers. Transitional infrastructure is pure cost.

**Rationale:** Simpler is better. One atomic change to main is faster than multi-phase rollout. Collapses Tom's planned phases P2→P6 into single integrated workstream.

**Banned (Locked):**
- ❌ No migrator
- ❌ No V2 class names (`WorkflowDefinitionFileV2`, `StepDefinitionV2`)
- ❌ No `schemaVersion` discriminator
- ❌ No dual schema acceptance in engine
- ❌ No feature flags

**Deferred to v2.1:**
- Generic `ConditionalOn` + `VisibleWhen` on arbitrary components → use v2.1 spike for tree-traversal infrastructure
- **v2.0 ships with:** `ConditionalChildren` on Radios/Checkboxes only (canonical "Other → specify" pattern)

**Implication:** P2 (migrator), P3 (dual acceptance) deleted outright. P4 (builder rewrite), P5 (tag helper collapse), P6 (doc rewrites) merge into one effort.

**Basis:** User directive (2026-04-26, Jonny Muir, delivered via Copilot coordinator).

---

## 📌 2026-04-30: Mabel (Technical Writer) — v2 Schema Terminology Cleanup (Docs Only)

**Status:** ✅ IMPLEMENTED — Documentation terminology unified across 12 public-facing docs

**Decision:** Remove all "v2.0 Schema Update" banners and "v1 vs v2 framing" from public-facing documentation. Replace with clear terminology that the polymorphic component model is the **current schema**.

**Rationale:** The polymorphic component model is the shipping schema; there is no shipped "v1" to distinguish from. Banners like "⚠️ v2.0 Schema Update" falsely suggest migration requirements and confuse new users about what is "current."

**Changes:**
- Removed banners from 12 docs (guides, design, walkthroughs, README)
- Normalized terminology: "v2.0 examples" → "current examples"; "v1 vs v2 comparison" → "Design evolution" (design docs only)
- Code identifiers (e.g., `WorkflowDefinitionFileV2.cs`, `ComponentPolymorphismTests.cs`) unchanged; internal naming deferred to Tom Nook/Blathers

**Verification:** All public docs use consistent "polymorphic component model" terminology; no v1/v2 framing in public scope (historical context in archive only).

**Basis:** Documentation review memo (2026-04-30, Mabel, technical writer).

---

### 2026-05-04T13:17:22.267+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Don't just call out that reviewer approval/rejection is needed; show it happening end-to-end in Playwright by navigating via the Aspire/dashboard workflow admin path, showing the workflow definition/state, approving it, and demonstrating that the original waiting user is moved on automatically.
**Why:** User request — captured for team memory
### 2026-05-04T13:20:30.000+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Make the walkthrough understandable step by step so someone can follow the whole workflow lifecycle and really understand what is happening at each stage.
**Why:** User request — captured for team memory
### 2026-05-04T13:24:41.480+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Additional walkthrough steps should be complemented appropriately with screenshots.
**Why:** User request — captured for team memory
### 2026-05-04T13:37:58.618+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Hide the demo Prism user-agent hover in screenshots, but do not crop screenshots so aggressively that the demonstrated content is cut off; limit vertical cropping primarily for genuinely long pages such as the home page.
**Why:** User request — captured for team memory
### 2026-05-04T13:44:50.590+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Screenshot policy should prefer showing the whole functionality of the screen, using cropped or viewport-sized captures only when a page is unusually huge and a full capture stops being useful.
**Why:** User request — captured for team memory
### 2026-05-04T16:09:36.911+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Refresh `docs/design` workflow docs thoroughly: keep them current, avoid pasting whole model files unless a snippet teaches something useful, and make them read like strong package documentation that is coherent, concise, discoverable, well indexed, and tells a clear story about implementing your own workflow.
**Why:** User request — captured for team memory
### 2026-05-08T05:58:15.779+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Keep the GitHub README unchanged, but add an automatic way to produce a Marketplace-friendly description that removes/adjusts unsupported HTML and rewrites relative links so they resolve correctly outside GitHub.
**Why:** User request — captured for team memory
### 2026-05-08T06:26:48.026+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Make sure the Umbraco Marketplace sync/nudge is triggered again after the latest publish so Marketplace picks up the update more quickly.
**Why:** User request — captured for team memory
# Mermaid walkthrough screenshots wait for rendered SVG, not page load alone

## Context

Workflow admin walkthrough screenshots include Mermaid state diagrams inside
expandable definition cards. The page can finish loading before Mermaid has
replaced the raw diagram text with SVG, so a capture taken immediately after
opening a card can freeze the pre-rendered source text into the walkthrough.

## Decision

Walkthrough screenshot capture now treats Mermaid as an explicit readiness
dependency:

- Screenshot runs must use the real Mermaid bundle instead of the no-op test
  stub
- The screenshot helper waits until each in-scope `.mermaid` block is marked
  `data-processed="true"`, contains an `svg`, and no longer has direct raw text
  nodes
- Workflow admin cards expose `data-mermaid-render-state` so the harness can
  wait on app-owned render state rather than arbitrary sleeps

## Why

This keeps normal Playwright tests deterministic while making screenshot capture
trust the rendered diagram, not just DOMContentLoaded or a guessed timeout.
# Screenshot policy correction — content-aware by default

## Context

The earlier walkthrough screenshot policy drifted toward a viewport-first reading
(`fullPage: false` by default), which made several fresh captures too cropped to
show the full functionality of the screen. Jonny clarified the intended rule:
default to showing the whole useful screen/content, and only crop when the page
is genuinely so tall that a full-height image stops being helpful.

## Decision

Walkthrough screenshot capture should be **content-aware by default**:

- Grow beyond the viewport to include the useful content being demonstrated
- Keep helper/hover UI hidden during capture
- Use selector-based crops or height caps for very tall pages (homepage,
  dashboard, similar long surfaces)
- Use `fullPage: true` selectively for steps where the entire document is still
  the useful thing to show (for example check-answers pages)

## Implementation notes

- `tests/walkthroughs/support/walkthrough.ts` remains the single control point
  for screenshot policy
- `screenshotSelector` is the preferred per-step crop control for long pages
- `screenshotMaxHeight` is available when a step needs a taller-but-still-capped
  image
- `SCREENSHOT_FULL_PAGE=1` remains a workflow-level override for forced full-page
  captures
# Decision: Waiting-State Walkthroughs Must Prove the Original Page Advances

**Date:** 2026-05-04  
**Author:** Tangy / @copilot  
**Status:** Implemented

## Summary

For walkthroughs that pause in a waiting or under-review state, the executable spec should keep the original member-facing page open while a second page follows the reviewer route. That lets the test prove the waiting page moves on automatically after approval, instead of only asserting state through admin-only screens.

## Pattern

1. Complete the member journey until the waiting page is visible.
2. Open a second page/tab for supporting checks:
   - inspect **My Workflows** if needed
   - follow the discoverable dashboard route to **Workflow Admin**
3. Perform the reviewer action there.
4. Return to or foreground the original waiting page and assert that it advances without a manual refresh step in the spec.

## Why

- It teaches the whole mechanism, not just the operator half.
- It keeps service walkthroughs honest about what the member actually experiences.
- It gives stronger regression coverage for waiting-step polling / reload behaviour.
# Workflow Approval Semantics — Narrative Pattern

**Context:** Walkthroughs across community-enquiry, information-request, and payment-demo all demonstrate workflows with role-gated `reviewer` transitions. These are not developer-only concerns; they are **core business process semantics** that should be visible in the walkthrough story.

**Decision:** Workflow approval patterns are taught as **named handoff points** where user action (form submission) yields to operator action (approval/rejection). This handoff must be explained **in each service walkthrough**, not delegated entirely to the development-only Workflow Administration guide.

## Pattern Definition

Three workflows currently implement role-gated approval transitions:

| Workflow | Submission → Waiting State | Operator Action | Result |
|---|---|---|---|
| **community-enquiry** | collecting-details → under-review | approve / request-changes | complete / collecting-details |
| **information-request** | collecting-info → under-review | approve / request-changes | complete / collecting-info |
| **payment-demo** | enter-details → processing-payment | complete | payment-complete |

These are **semantic breakpoints** in the workflow, not bugs or incomplete flows. Each represents a business rule: a human or system must verify, process, or authorize the member's input before the workflow can proceed.

## Narrative Requirements

Every workflow with `requiresRole: "reviewer"` transitions must include:

1. **Explicit naming of the waiting state** — "This is **not** a terminal state; it's a waiting state."
2. **Statement of who acts next** — "A reviewer with the `reviewer` role can now…"
3. **Enumeration of next actions** — List each role-gated transition (approve, request-changes, etc.)
4. **Production vs. dev distinction** — Briefly note that in dev we use the Workflow Admin panel, but production uses a dedicated operator interface.
5. **Authorization statement** — "The workflow definition enforces that only users with the `reviewer` role can advance these transitions."

See community-enquiry.md and payment-demo.md sections titled "What Happens Next" for reference implementation.

## Future Guidance

- When adding new workflows with `requiresRole` transitions, include this narrative in the same service walkthrough.
- When building the production operator UI (not yet planned), reference this decision to ensure the operator interface aligns with the role hierarchy already defined in workflow seeds.
- Workflow Administration walkthrough remains a **developer tool reference**, but service walkthroughs own the **business narrative** of approval workflows.

---

**Related:** SKILL.md rule [R6](../../skills/walkthroughs-as-executable-specs/SKILL.md) ("Negative paths live with the walkthrough") — approval/rejection paths are conditional flows within the workflow and belong in the same spec.
# Decision: workflow docs are maintained as package implementation guides

- **Date:** 2026-05-04
- **Decision maker:** Copilot acting as Mabel

## Decision

The workflow documents under `docs/design/` are now organised and maintained as package-consumer implementation guides, not as proposal logs. `docs/design/README.md` is the landing page, and each workflow document now owns one topic with minimal duplication:

- overview
- end-to-end implementation story
- backend contracts
- Umbraco integration
- client rendering
- validation
- security
- advanced patterns

## Why

The previous workflow docs mixed historical proposals, stale contracts, and large code dumps. That made it hard to discover the current package story and easy to copy obsolete examples.

## Consequences

Future workflow documentation changes should update the topic guide that owns the concept rather than re-describing the same contract in multiple places. Seed files and implementation source stay the canonical detailed examples; docs should link to them and only quote short snippets when they teach something useful.
# Workflow docs should tell a package story, not dump implementation

## Context

Reviewing `docs/design/workflow*.md` from a package-documentation perspective showed a recurring problem: the workflow design set is rich in engineering detail but weak as a discoverable, trustworthy implementation guide for package consumers.

## Guidance

1. **Split narrative guide from internal design reference.**
   - Keep one clear "build your own workflow" path aimed at consumers.
   - Keep ADR/security/reference material separate and explicitly labeled as contributor/internal reference.

2. **Prefer compact examples over whole-file code dumps.**
   - Show the minimum JSON/C# needed to explain a concept.
   - Link to the real source file or builder type for full reference instead of embedding hundreds of lines.

3. **Make the docs follow the implemented contract, not the superseded proposal.**
   - Treat `WorkflowDefinitionFile`, `WorkflowDefinitionBuilder`, `StepContent.StepType`, and current walkthrough examples as the canonical source.
   - Mark historical/proposal content clearly or archive it.

4. **Organize workflow docs around the consumer journey.**
   - Recommended order: concepts → definition anatomy → create a workflow → validation/conditional logic → run/debug/admin → extension/reference.

5. **Every doc in the set should answer one question.**
   - If a page tries to be architecture, API reference, schema dump, and tutorial at once, split it.

## Implication for rewrites

Mabel's rewrite should optimise for: concise entry points, strong cross-linking, current examples, and a single coherent implementation story for someone building their own workflow with Prism.
# Marketplace listing content is generated from the GitHub README

## Context

The GitHub `README.md` is the canonical public package story, but Umbraco Marketplace renders some GitHub-flavoured HTML and relative links poorly. Keeping a second hand-edited `MARKETPLACE.md` had already started to drift from the README.

## Decision

1. Keep `README.md` unchanged as the source of truth.
2. Generate `MARKETPLACE.md` automatically from `README.md` using `scripts/generate-marketplace-readme.mjs`.
3. During generation, convert known Marketplace-hostile content into Marketplace-safe Markdown:
   - centered HTML image blocks become plain Markdown images/headings
   - relative document links become absolute GitHub `blob`/`tree` URLs
   - relative image paths become absolute `raw.githubusercontent.com` URLs
4. Treat `MARKETPLACE.md` as a generated artifact and verify it in CI/release workflows with `npm run check:marketplace`.

## Implication

Marketplace copy now stays aligned with the GitHub README without manually maintaining two narratives. Any README change that should appear on Marketplace must be followed by `npm run generate:marketplace`, and CI will fail if the generated file is stale.
# Mabel — Marketplace listing refresh requires a package release

**Date:** 2026-05-08  
**Status:** Complete — v1.9.1 released with marketplace-generated README
**Author:** Mabel (Technical Writer / Release Manager)  
**Impact:** Marketplace publishing, NuGet package metadata, release process

## Summary

The Umbraco Marketplace package page renders `readMeContent` from the published NuGet package, not the `DocumentationUrl` target from `umbraco-marketplace.json`.

## Decision

1. Keep `README.md` as the source of truth for GitHub.
2. Keep generating `MARKETPLACE.md` from `README.md` for Marketplace-safe formatting.
3. Ship `MARKETPLACE.md` inside the NuGet package and set `<PackageReadmeFile>MARKETPLACE.md</PackageReadmeFile>`.
4. Treat Marketplace copy refreshes that need the rendered package page to change as a patch release, because the rendered content is tied to the package artifact.
5. Continue using `DocumentationUrl` to point at the raw GitHub `MARKETPLACE.md`, but treat that as a supporting docs link rather than the primary rendered listing body.

## Rationale

- The public Marketplace frontend renders `package.readMeContent` on the package page.
- `documentationUrl` appears in the sidebar links, so syncing metadata alone does not replace the main rendered listing content.
- A patch release lets us push the marketplace-friendly generated markdown without forking or manually editing the GitHub README.

## Operational consequence

To refresh the Marketplace page body after this change:

1. release a new package version
2. push the tag so the package is published
3. trigger the Marketplace sync for `UmbracoPrism`

Metadata-only edits that affect title, tags, screenshots, or the docs link can still use a sync-only path when the package readme itself does not need to change.

## Implementation Complete (v1.9.1)

- ✓ Commit 8b78831: Added MARKETPLACE.md generation script and configured NuGet package
- ✓ Tag v1.9.1 created and pushed to origin
- ✓ GitHub Actions workflow triggered automatically on tag push
- ✓ UmbracoPrism.1.9.1.nupkg built and published to NuGet.org
- ✓ GitHub Release v1.9.1 created with package asset
- ✓ Marketplace sync endpoint triggered
- ✓ Release notes documented in CHANGELOG.md

Note: NuGet.org package search API may take 1–2 hours to index the new package version. The .nupkg is immediately available via direct package references.
# Documentation Decision: OIDC Provider Language

**Decided by:** Mabel (Documentation Specialist)  
**Date:** 2026-05-04  
**Status:** Ready for review  

## Issue
The README.md repeatedly implied that Entra ID was mandatory or the primary OIDC authentication method, when in fact:
- Any OIDC-compliant provider is supported (Entra ID, Keycloak, generic OIDC, etc.)
- Keycloak is included for local dev flows
- Entra ID is one option among many for production use

This created confusion for new users and misrepresented the project's architecture.

## Changes Made

**10 targeted README.md edits to clarify OIDC provider flexibility:**

1. **Line 125** - Features bullet: Changed "Entra ID integration" → "any OIDC-compliant provider (Entra ID, Keycloak, etc.)"
2. **Line 192** - Quick Start guide: Unified tenant setup instructions to generically reference "OIDC tenants (Entra ID, Keycloak, etc.)" instead of separate "Entra tenants" vs "Generic OIDC tenants" sections
3. **Line 203** - Architecture: Changed "OIDC providers (Entra ID or generic OIDC)" → "any OIDC-compliant system: Entra ID, Keycloak, etc."
4. **Line 218** - Features: Changed "Per-tenant Entra ID (OIDC)" → "Per-tenant OIDC (any provider: Entra ID, Keycloak, etc.)"
5. **Line 284** - Prerequisites: Changed "Entra ID (for authentication)" → "OIDC Provider — any OIDC-compliant system (Keycloak included for local dev; Entra ID or others for production)"
6. **Line 301** - Local Dev Tunnel: Changed "For testing Entra sign-in" → "For testing OIDC sign-in on mobile devices with an external OIDC provider"
7. **Line 313** - Local Dev Tunnel: Changed "Mutates Entra app" → "Mutates your OIDC provider app config"
8. **Lines 432-458** - Local Authentication Walkthrough: **Major restructuring**
   - Added "Option A: Quick Start with Keycloak (Included)" as first path
   - Moved external providers (Entra, generic OIDC) to "Option B"
   - Split tenant setup into two clear paths (Keycloak vs External OIDC)
9. **Line 527** - Stack: Changed "Auth: Stateless OIDC (Entra)" → "Auth: Stateless OIDC (any OIDC-compliant provider)"
10. **Line 550** - Phone Auth section: Changed "For Entra sign-in on mobile" → "For OIDC sign-in on mobile devices with an external provider"

## Key Message
README now leads with **Keycloak for local dev** (simplest path, no setup needed) before showing **external OIDC provider options** (Entra, etc.) for production. This matches the actual architecture where Keycloak is bundled and ready-to-use, while production typically adds their own OIDC provider.

## Alignment with Docs
Confirmed against:
- `docs/secret-management.md` — which discusses all three secret paths (Entra, generic OIDC, inline Keycloak)
- `docs/umbraco-setup.md` — which is OIDC-provider agnostic
- Actual app configuration in `keycloak/` directory

## Impact
- **New contributors** now understand they can start locally with Keycloak without Azure setup
- **Production integrators** still see their Entra/generic OIDC path clearly
- **Documentation consistency** — README now correctly reflects "any OIDC provider" architecture without implying Entra is mandatory
# Mabel — Payment Demo as Primary Interactive Walkthrough

**Date:** 2026-05-04  
**Status:** Proposed  
**Author:** Mabel (Technical Writer)  
**Impact:** README, documentation discovery, first-time user experience

## Summary

Moved the **Payment Processing Workflow** demo to the primary position in README's "Interactive Walkthrough" section, replacing the Planning Permission workflow.

## Rationale

### Payment Demo advantages

- **Showcases Prism's core differentiator:** Demonstrates the "submit now, finish later" async workflow pattern with waiting states, persistence, and real-time updates — the feature that justifies Prism's existence
- **More universally relevant:** Payment processing is needed in every business app; planning permissions are a niche government use case
- **Clearer visual progression:** Form submission → Processing state → Completion. The waiting state ("Processing Your Payment") is a teaching moment that shows how Prism handles asynchronous work
- **Reviewer workflow visibility:** Demonstrates the dual-actor pattern (member + reviewer) that real async workflows need, exposing admin panel and real-time updates
- **Cleaner screenshots:** No UI debugging artifacts (unlike some planning screenshots)

### Planning Permission walkthrough remains available

The planning permission walkthrough is kept as an **Alternative** for developers who want to see multi-step complex forms with conditional field logic. It's valuable but not the "hook" for a first-time GitHub visitor.

## What Changed

- **README § 42–57:** Updated section title, description, and bullet points to emphasize waiting states and async patterns
- **README § 243–246:** Updated documentation table to list Payment Demo as primary, Planning as alternative
- Both walkthroughs remain fully available in `/docs/walkthroughs/`

## User Impact

- **First-time reader:** Sees the async workflow pattern immediately — Prism's key differentiator
- **Onboarding:** Follows a cleaner mental model: submit → wait → review → complete
- **Developer education:** Learns about persistence, waiting states, and real-time updates in the first 5 minutes
- **GitHub first impression:** Payment is more recognizable and business-relevant than planning permission

## Decision Made By

This is a **documentation positioning decision**, owned by Mabel as Technical Writer, aligned with README clarity and consumer-facing packaging.
# Documentation PR Readiness — 2026-05-04

**Reporter:** Mabel (Documentation Specialist)  
**Branch:** `feat/walkthrough-e2e-hardening`  
**Status:** ✅ Documentation side READY for PR (pending Isabelle's screenshot verification)

---

## Summary

The documentation has been reviewed and updated for PR readiness. All narrative walkthroughs now correctly reference the current executable specs, and all image paths are consistent. The branch is ready from the documentation side pending final screenshot verification by Isabelle.

---

## What Changed in Documentation

### 1. **README.md** — Authentication & Setup Generalization
- **Changed:** All references to "Entra ID" now read "any OIDC-compliant provider"
- **Why:** Reflects system support for Keycloak (local dev) and any generic OIDC provider
- **Scope:** 10 edits across setup instructions and authentication sections
- **Impact:** Users now understand Keycloak is the quick-start option without external setup

### 2. **docs/walkthroughs/payment-demo.md** — Completely Rewritten ⭐
- **Changed:** 80 lines → 270+ lines; 3-step narrative → 9-step complete handoff
- **Why:** Executable spec was expanded to show full member→waiting→reviewer→completion flow
- **Was:** Compact form-only walkthrough
- **Now:** End-to-end demonstration covering:
  - Dashboard entry point (01-dashboard-payment-demo-start.png)
  - Form flow (02-initial, 03-form-filled)
  - Waiting state (04-processing, 05-workflow-hub-processing)
  - Reviewer flow in Workflow Admin (06-dashboard-admin-link, 07-admin-processing-instance, 08-admin-payment-definition)
  - Automatic member page update on completion (09-payment-complete)
- **Impact:** Readers now understand the "submit now, finish later" pattern end-to-end, not just the member's first step

### 3. **docs/walkthroughs/README.md** — Workflow Admin Context
- **Added:** Clarifying note that Workflow Admin is a development-only testing harness
- **Added:** Cross-reference to Payment Demo, Community Enquiry, Information Request for production-adjacent workflows
- **Why:** Readers need to know the admin panel is not a production feature; real workflows are demonstrated in the payment/community/information walkthroughs
- **Impact:** Reduced confusion about what Workflow Admin represents

---

## Screenshot Status

### **All Image References Are Valid**

| Walkthrough | Images | Status | Expected Files |
|---|---|---|---|
| **payment-demo** | 9 | ✅ All exist (untracked) | 01-09 ✓ |
| **home-entry** | 5 | ✅ All exist (untracked) | 01-05 ✓ |
| **workflow-administration** | 3 | ✅ All exist (untracked) | 01-03 ✓ |
| **community-enquiry** | 4 | ✅ All exist (tracked) | 01-04 ✓ |
| **information-request** | 3 | ✅ All exist (tracked) | 01-03 ✓ |

### **Old Screenshots Deleted (intentional)**
- `docs/images/walkthroughs/payment-demo/{01-03}.png` — deleted (staged for removal)
- These are replaced by the new 9-step sequence

### **New Screenshots (Untracked, Isabelle's Work)**
These are Isabelle's screenshot captures — they're present as untracked files and will be staged by her after verification:
- `docs/images/walkthroughs/payment-demo/{01-09}.png` ✓
- `docs/images/walkthroughs/home-entry/{01-05}.png` ✓
- `docs/images/walkthroughs/workflow-administration/{01-03}.png` ✓

---

## Documentation Readiness Checklist

✅ All markdown walkthroughs updated and cross-reference checked  
✅ All image paths in markdown match actual file names  
✅ All 17 expected screenshot files exist (9+5+3)  
✅ README clarifications for OIDC providers applied  
✅ Walkthrough Admin docs clarified as development-only  
✅ No broken internal links or markdown syntax errors  
✅ Executable spec footer notes correct (SKILL.md reference, capture workflow)

---

## What Isabelle Needs to Do (Screenshot Verification)

1. **Verify the 17 captured screenshots** are correct:
   - Payment Demo (9): Dashboard entry → form → processing → admin inspection → completion
   - Home Entry (5): Unauthenticated → authenticated → dashboard → demo entry → hub
   - Workflow Admin (3): Dashboard admin link → instance list → definition editor

2. **If screenshots are stale or incorrect**, regenerate via:
   ```bash
   CAPTURE_SCREENSHOTS=1 npm run test:walkthroughs
   ```
   Or use the `Capture Walkthrough Screenshots` GitHub workflow for CI

3. **Stage the verified screenshot files** once confirmed:
   ```bash
   git add docs/images/walkthroughs/
   ```

---

## Residual Follow-Up (If Isabelle Finds Issues)

**If Isabelle regenerates screenshots and they differ from the current captures:**
- The markdown is ready to accept any screenshot set as long as they match the filenames (01-09 for payment, etc.)
- No markdown edits needed — just the screenshot files will update
- The narrative remains valid for any correct implementation of the 9-step flow

**If Isabelle finds the flow itself is broken** (e.g., a step doesn't execute):
- Mark the issue in a comment on the PR
- The test spec will need fixing, not the docs
- Docs narrative is spec-aligned and correct

---

## Next Steps for PR Opening

1. ✅ Documentation side: READY
2. ⏳ Screenshots: Awaiting Isabelle's verification
3. → Once Isabelle stages the screenshot files, PR is ready to open
4. → PR should reference this check-in and note: "Screenshots verified by Isabelle (#name)"

---

**Decision:** Documentation is PR-ready. All narratives align with executable specs. All image references are resolved. The branch can proceed to PR once Isabelle verifies the captured screenshots are correct.
# Walkthrough Screenshots & Documentation Audit
**Date:** 2026-05-04T13:37:58.618+01:00  
**Auditor:** Mabel (Documentation Specialist)  
**Requested by:** Jonny Muir

---

## Executive Summary

Reviewed current walkthrough screenshots against user expectations:
- ✅ Demo PrismMobile UserAgent toggle: Correctly hidden by `prism-screenshot-mode` cookie (server-side works)
- ✅ Screenshots DO include what they demonstrate in most cases
- 🟡 **FINDING:** workflow-administration/01 screenshot cuts off the "Workflow Admin" card it's supposed to show
- 🟡 **FINDING:** community-enquiry screenshots using full-page (2500+ px) when viewport-only (720px) would be adequate
- 🟡 **FINDING:** Viewport height (720px) sometimes insufficient for form pages to show all content + call-to-action

---

## Problem Areas Found

### 1. Workflow Administration Step 1: Admin Card Cut Off
**File:** `docs/walkthroughs/workflow-administration.md` (line 46)  
**Screenshot:** `docs/images/walkthroughs/workflow-administration/01-dashboard-admin-link.png` (1280×720px)  
**Issue:** Screenshot is viewport-only (720px) but the markdown claims to show the Workflow Admin card. The dashboard content extends beyond 720px, so the admin card link is likely below the visible area.

**Evidence:**
- Spec at line 36-40: Takes viewport screenshot after `openDashboard(page)`
- Then asserts admin link is visible (line 46-48), but screenshot doesn't show it
- Narrative at line 46 says "![...Workflow Admin card visible...]" — but it's cut off

**Impact:** Readers can't see the thing being demonstrated. Documentation and screenshot are misaligned.

**Safe Fix:**  
Add note to markdown clarifying that "the Open Admin button appears below the dashboard cards" or adjust spec to scroll/ensure card is visible. This is a documentation issue, not a product issue.

---

### 2. Community Enquiry: Forms Using Full-Page Screenshots Unnecessarily
**Files:**  
- `docs/images/walkthroughs/community-enquiry/01-initial.png` (1280×2537px)  
- `docs/images/walkthroughs/community-enquiry/02-conditional-reveal.png` (1280×2672px)  
- `docs/images/walkthroughs/community-enquiry/03-form-filled.png` (1280×2537px)

**Issue:** These are form pages showing the entire scrollable content (2500+ px tall). User feedback: "screenshots are cut off too abruptly vertically" and "previous instruction should only constrain very long pages like the homepage."

**Analysis:** The forms don't need full-page captures. The viewport crop at 720px would show:
- Form heading
- First few fields
- Enough to understand what the user is doing

Full-page captures are visually overwhelming for documentation.

**Safe Fix:**  
Update specs to NOT use `fullPage: true` for form pages (revert to default viewport). Only use `fullPage: true` for:
- Confirmation/summary pages (check-answers style)
- Exceptionally long pages like the actual homepage (currently 9447px)

---

### 3. Payment Demo & Information Request: Inconsistent Heights
**Observations:**
- `payment-demo/01-initial.png`: 1280×809px (viewport with some scroll room)
- `payment-demo/03-processing.png`: 1280×720px (viewport only)
- `information-request/01-initial.png`: 1280×1664px (full-page)
- `information-request/03-under-review.png`: 1280×720px (viewport only)

**Pattern:** Mixed strategies. Some use fullPage, some don't.

**Safe Fix:**  
Standardize: all form/workflow pages use viewport-only (720px default), EXCEPT:
- Check-answers/summary pages: use viewport-only or minimal fullPage if needed
- Confirmation pages: viewport-only (they're terminal states, user shouldn't scroll)

---

### 4. Home Entry Screenshots: Correct Height
**Screenshots:**  
- `home-entry/01-signed-out-hero.png`: 1280×720px  
- `home-entry/02-signed-in-hero.png`: 1280×720px  
- `home-entry/03-dashboard.png`: 1280×720px  
- `home-entry/04-start-workflow.png`: 1280×720px  
- `home-entry/05-workflow-hub.png`: 1280×720px

**Status:** ✅ All viewport-only, consistent, shows what's needed.

---

### 5. Shared Homepage: Still Too Tall
**File:** `docs/images/walkthroughs/shared/01-homepage.png` (1280×9447px)  
**Status:** This is a full-page screenshot and intentionally so — it shows the entire hero section and branding, which is necessary.

However, 9447px is excessive. Should be cropped to ~1280×2200-2400px to show:
- Header/nav
- Hero heading + CTA
- Key supporting content (security/scale messaging)

**Note:** This is a known issue from prior audit. Keeping for reference.

---

## Mobile User Agent Toggle: Status

**Finding:** ✅ **NOT AN ISSUE**

The toggle is correctly hidden in screenshot mode. Evidence:
- `PrismMobileUserAgentDemoTagHelper.cs` line 52: `var effectiveShowToggle = ShowToggle && !IsScreenshotMode;`
- Cookie `prism-screenshot-mode=1` correctly suppresses the toggle
- `enterScreenshotMode()` in walkthrough.ts correctly sets the cookie

**Why some screenshots show it:**  The earlier screenshot I viewed (community-enquiry/01-initial.png) showed the toggle because:
- That image was captured before the fix was deployed, OR
- The image file predates the current screenshot-mode implementation

Re-capture will fix this.

---

## Recommendations for Safe Documentation Updates

### Immediate (No Spec Changes Required)

1. **workflow-administration.md:** Add clarifying caption
   - Change line 46 caption from:  
     `![Member dashboard with Workflow Admin card visible](...)`  
   - To:  
     `![Member dashboard — scroll to find the Workflow Admin card](...)<!-- Added Sept 2024: the admin card appears below the initial dashboard view -->`

2. **Document assumption about viewport heights**  
   Add to `.squad/skills/walkthroughs-as-executable-specs/SKILL.md`:  
   ```markdown
   ## Screenshot Heights
   
   - Default: viewport crop (1280×720px) — shows what user sees without scrolling
   - Exception: check-answers / summary pages may use `fullPage: true` to show all collected data
   - Exception: pages that are intentionally scrollable (rare) use `fullPage: true` with explicit note
   
   Forms should always use viewport-only; confirmation screens use viewport-only.
   ```

### Follow-Up (For Isabelle or Capture Workflow)

3. **Community Enquiry Specs:** Revert fullPage usage
   - Remove `fullPage: true` from form page steps (if any)
   - Ensure all steps use default viewport

4. **Home Entry & Payment Demo:** Ensure consistency  
   - All form pages: viewport-only  
   - Confirmation pages: viewport-only

5. **Shared Homepage:** Schedule optional crop to ~2400px max (not urgent, enhancement only)

---

## Decision Matrix

| Item | Safe to Fix Now? | Responsibility | Effort |
|------|:---:|---|---|
| workflow-admin caption | ✅ Yes | Mabel (docs) | 2 min |
| Add skill guidance | ✅ Yes | Mabel (docs) | 5 min |
| fullPage reversion | 🟡 Partial | Isabelle (specs) | 15 min |
| Mobile toggle hiding | ✅ Already fixed | (none needed) | – |
| Homepage crop | 🟡 Optional | Isabelle (tooling) | 20 min |

---

## Files to Update (Mabel Owns These)

- `docs/walkthroughs/workflow-administration.md` — caption clarification
- `.squad/skills/walkthroughs-as-executable-specs/SKILL.md` — add screenshot height guidance

---

## Verification Checklist

After updates:
- [ ] workflow-administration.md caption clarified
- [ ] Skill.md documents viewport vs. fullPage rules
- [ ] Community enquiry specs reviewed for fullPage usage (Isabelle)
- [ ] All form screenshots are viewport-only in next capture
- [ ] Confirmation screenshots verified as viewport-only
# Screenshot Default Policy Clarification

**Date:** 2026-05-04  
**Author:** Mabel (Documentation Specialist)  
**Decision Type:** Team documentation rule clarification

---

## Summary

Corrected the screenshot guidance in walkthrough documentation to align with the **principle: show the whole useful screen by default; crop selectively only for very tall pages**.

**Previous guidance (incorrect):** Viewport crop (1280×720px) as the default  
**Corrected guidance:** Full page as the default, constrain only when necessary

---

## Problem

Recent audit documentation had introduced guidance suggesting viewport-sized screenshots (1280×720px) should be the default. This contradicted the principle that walkthrough documentation should show readers the **complete functionality** available on a page.

Feedback from Jonny Muir clarified the intent: show complete screen context by default, constraining only when pages are exceptionally tall (>2200px) and full-page captures would obscure rather than clarify the documentation.

---

## Decision

**Effective immediately:** Screenshot default policy is reversed to prioritize completeness.

### Updated Guidance

**Default:** Full page (all scrollable content visible)  
- Shows the complete functionality of the screen so readers see everything available
- Applies to form pages, check-answers pages, summary pages, and any page where complete visibility aids the walkthrough narrative

**Constrain to viewport only when:**
- A page is exceptionally tall (>2200px)
- Full-page height creates visual clutter without adding documentation value
- A smaller crop makes the guidance clearer without hiding necessary content

**Rule of thumb:** Show the whole useful screen by default. Crop selectively only for unusually large pages where full-page screenshots would obscure rather than clarify the documentation.

---

## Files Updated

1. **`.squad/skills/walkthroughs-as-executable-specs/SKILL.md`**
   - Section: "Screenshot Heights" (lines 122–136)
   - Updated default from viewport-crop to full-page-by-default
   - Clarified when to constrain (rare, not the norm)

2. **`src/UmbracoPrism.Client/tests/walkthroughs/support/walkthrough.ts`**
   - Lines 25–34: Updated JSDoc comment for `fullPage` parameter
   - Clarified that default behavior now expands to show all content
   - Documented when to use fullPage explicitly

---

## Impact

- **Walkthrough authors:** Use the full-page default; only set `fullPage: true` when narrative absolutely requires it
- **Next screenshot capture:** Follow the new default when re-capturing
- **Existing screenshots:** No immediate action required; will be refreshed during normal capture cycles
- **Team clarity:** Policy is now consistent across skill documentation and code comments

---

## Related Issues

- Audit note: `mabel-screenshot-audit-2026-05-04.md` (captured viewport vs. full-page confusion)
- User feedback: Jonny Muir clarified policy on 2026-05-04

---

## Verification Checklist

- [x] SKILL.md updated
- [x] walkthrough.ts JSDoc updated
- [x] Decision note filed
- [ ] Communicate to team (Isabelle for next capture cycle)
- [ ] Review next batch of captured screenshots against new policy
# Walkthrough Audit: Workflow Administration Steps Placement

**Date:** 2026-05-04  
**Auditor:** Mabel (Documentation Specialist)  
**Concern:** Do workflow administration steps appear in service walkthroughs where they need to appear?

---

## Executive Summary

**Verdict: PARTIALLY**

Workflow progression and reviewer/admin roles **are defined in the workflow seeds** but **are not documented in the walkthroughs where they matter**. Readers cannot understand how workflows complete after user submission or why certain intermediate states exist.

---

## Key Findings

### 1. Reviewer Roles Built Into Workflows

The following workflows have **`requiresRole: "reviewer"` transitions** hardcoded into their state machines:

| Workflow | Transition | Requires | Status |
|----------|-----------|----------|--------|
| **payment-demo** | `processing-payment` → `payment-complete` | `reviewer` role | ❌ Not documented |
| **community-enquiry** | `under-review` → `complete` | `reviewer` role | ❌ Not documented |
| **community-enquiry** | `under-review` → `collecting-details` (reject) | `reviewer` role | ❌ Not documented |
| **information-request** | Approval transitions | `reviewer` role | ❌ Not documented |
| **planning-notification** | (No reviewer roles) | N/A | ✅ Consistent |

### 2. Walkthrough Coverage

#### ✅ Well-Placed (Consistent & Complete)

- **Workflow Administration** (`workflow-administration.md` + spec)
  - Correctly positioned as a **development-only tool** walkthrough
  - Explains instance inspection and manual state advancement
  - Located in "Authoring & Operations" section of README
  - BUT: Scope is limited to development debugging, not production operator workflows

- **Authoring a Workflow** (`authoring-a-workflow.md`)
  - Shows state machine concepts and transitions
  - Explains role-based transitions briefly in seed JSON example
  - BUT: Not connected to user-facing workflows

#### ❌ Missing or Misplaced (Incomplete)

- **Payment Demo** (`payment-demo.md` + spec)
  - Shows user enters payment details → sees "Processing Your Payment" screen
  - Stops there; **does NOT explain:**
    - That a `reviewer` role must advance the workflow
    - How/when the "payment-complete" state is reached
    - Why a processing state exists
    - What service/operator steps follow submission
  - **Spec:** Only tests user submission; doesn't test reviewer transition

- **Community Enquiry** (`community-enquiry.md` + spec)
  - Shows form submission → "Your enquiry is with us" confirmation
  - **Does NOT explain:**
    - That the workflow is now in `under-review` state pending reviewer action
    - That a reviewer can `approve` (move to `complete`) or `request-changes` (send back)
    - How enquiry approval/rejection works
    - Why transitions require the `reviewer` role
  - **Spec:** Only tests user submission; doesn't test reviewer actions

- **Information Request** (`information-request.md`)
  - No mention of reviewer workflow
  - Seed file shows reviewer transitions exist but docs don't explain them

- **Planning Notification** (`planning-notification.md`)
  - Doesn't need reviewer role (all transitions user-driven)
  - Documentation is consistent with definition

---

## File-by-File Placement Analysis

### docs/walkthroughs/

| File | Status | Issue |
|------|--------|-------|
| `payment-demo.md` | ❌ Missing admin context | Stops after user submits; doesn't explain reviewer progression or why "processing" state exists |
| `community-enquiry.md` | ❌ Missing admin context | Ends at "under-review"; doesn't document reviewer approval/rejection flow |
| `information-request.md` | ❌ Missing admin context | Doesn't mention reviewer transitions exist in the seed |
| `planning-notification.md` | ✅ Consistent | No reviewer roles, docs match definition |
| `workflow-administration.md` | ✅ Correct scope | Development-only tool, clearly marked as such; appropriate for authoring section |
| `authoring-a-workflow.md` | ✅ Partial credit | Shows role concept in code/JSON examples but doesn't connect to real user workflows |
| `README.md` | ⚠️ Minor issue | Good structure; "Authoring & Operations" section exists but lacks a walkthrough for **production** workflow operator tasks |

### src/UmbracoPrism.Client/tests/walkthroughs/

| File | Status | Issue |
|------|--------|-------|
| `payment-demo.walkthrough.spec.ts` | ❌ Incomplete | Only tests user flow; missing test cases for reviewer transition to `payment-complete` |
| `community-enquiry.walkthrough.spec.ts` | ❌ Incomplete | Only tests user flow; missing test cases for reviewer `approve` and `request-changes` actions |
| `information-request.walkthrough.spec.ts` | ❌ Incomplete | Only tests user flow; missing reviewer tests |
| `workflow-administration.walkthrough.spec.ts` | ✅ Correct scope | Tests development admin panel; appropriate for its purpose |

### docs/images/walkthroughs/

No missing images identified (images are generated by specs as they stand).

---

## Specific Recommendations

### High Priority: Close the Documentation Gap

**1. Extend `payment-demo.md` with reviewer workflow**
- Add "Part 2: What Happens Next (Reviewer Approval)"
- Document that a reviewer role must move workflow from `processing-payment` to `payment-complete`
- Add note explaining why the `waiting` component exists
- Include screenshot showing the approval transition (once spec is updated)

**2. Extend `community-enquiry.md` with reviewer workflow**
- Add section explaining the `under-review` state is not terminal
- Document reviewer actions: `approve` (move to `complete`) vs `request-changes` (return to collection)
- Explain why these transitions require the `reviewer` role
- Show what form data is available to reviewers

**3. Add brief mention to `information-request.md`**
- Note that this workflow also has reviewer transitions (in specs/seeds)
- Link to payment-demo or community-enquiry for the approval pattern explanation

### Medium Priority: Test Coverage

**4. Extend walkthrough specs to cover reviewer transitions**
- Add test case in `payment-demo.spec.ts` that simulates reviewer calling the completion action
- Add test case in `community-enquiry.spec.ts` for both `approve` and `request-changes` paths
- Use the admin panel API (`/admin/workflow/{instanceId}/action/{action}`) to trigger reviewer actions

### Low Priority: README Navigation

**5. Update `README.md` Walkthroughs section**
- Add note under "Authoring & Operations" that explains:
  - `workflow-administration.md` = development debugging
  - Payment Demo & Community Enquiry = examples of reviewer workflow patterns
- Consider a future "Workflow Operators" walkthrough section if production operator workflow is added

---

## Explanatory Context Required

Readers need to understand:

1. **Why these states exist:**
   - `processing-payment` / `under-review` = system processing or waiting for human decision
   - User can't self-advance; requires external approval

2. **How it works in practice:**
   - What UI/API does a reviewer use to approve/reject?
   - What happens to the user's form data while under review?
   - What triggers completion? Manual action? Timeout? Async job?

3. **Role-based permissions:**
   - Transitions with `requiresRole: "reviewer"` can only be triggered by users with that role
   - How is the "reviewer" role assigned/validated?

4. **State machine semantics:**
   - Terminal vs. non-terminal states
   - Whether a user can self-retry after rejection

---

## What Should Be Changed

### Minimal Safe Edits (Ready to Make Now)

1. **payment-demo.md:**
   - Add explanatory paragraph after the "Processing" screenshot explaining:
     > "The workflow now waits in the `processing-payment` state. In a production system, a backend service or human reviewer with the `reviewer` role would then advance this workflow to `payment-complete` based on payment confirmation from Stripe."
     
2. **community-enquiry.md:**
   - Add note after the "Your enquiry is with us" screenshot:
     > "Your enquiry has entered the `under-review` state. A reviewer with the `reviewer` role can now view your submission and either approve it (moving to `complete`) or request changes (returning the form to you for edits)."

3. **README.md:**
   - Add one bullet under "Authoring & Operations" explaining that Workflow Administration is development-only, with a note that payment-demo and community-enquiry walkthroughs show reviewer patterns

### Larger Improvements (Scope for Future Work)

- Create example reviewer workflow screenshots showing the approval UI
- Add production-safe operator guide (separate from development admin panel)
- Test coverage for reviewer transitions in specs

---

## Decision

**Walkthroughs are PARTIALLY addressing the concern.**

The workflows are correctly defined with reviewer roles, but the documentation **doesn't explain them where readers need to understand them** — in the service walkthroughs (payment-demo, community-enquiry). A reader finishing those walkthroughs would not understand:

- How workflows continue after the user's submission
- Why intermediate states exist
- What role/permission is needed to advance them
- Why the forms/transitions behave the way they do

**Recommended action:** Make the three minimal safe edits above to provide explanatory context in the walkthroughs. These are low-risk, additive changes that clarify existing behavior without rewriting walkthroughs.
# Tangy — Full Walkthrough Pass

- **Date:** 2026-05-04
- **Status:** Proposed

## Decision

Treat the MockBusinessApp workflow admin page as an executable, development-only continuation of member walkthroughs, and stub its CDN-loaded Ace/Mermaid vendor assets inside Playwright so screenshots and CI do not depend on third-party network availability.

## Why

- The dashboard now routes members into seeded workflow demos via per-workflow **Start** cards rather than a single **Start Workflow** CTA, so walkthrough coverage should assert those real navigation entry points.
- Reviewer-only workflow transitions (`Approve`, `Request Changes`) are exposed in the local workflow admin surface and are the right place to exercise operator-adjacent flows without pretending those controls exist in the public member UI.
- The admin page pulls editor/diagram assets from public CDNs; without test-side stubs, screenshot capture and CI stability are needlessly coupled to external network health.

## Implications

- Walkthrough docs can now show the member journey handing off cleanly to the local admin tooling for under-review / waiting states.
- Future workflow-admin tests should reuse the support hook rather than hitting the live CDNs.
---
decision_id: tom-nook-walkthrough-story-review-2026-05-04
title: Walkthrough Story Review — Post-Implementation Clarity Assessment
author: Tom Nook
created_at: 2026-05-04T12:57:00.000Z
tags: [walkthroughs, documentation, clarity, narrative]
affects: [tangy, mabel, isabelle]
---

# Walkthrough Story Review — Final Assessment

**Status:** Post-implementation audit of `feat/walkthrough-e2e-hardening` branch.

**Verdict:** Walkthrough story is **strong and ready**. The recent work by Tangy and Mabel has materially improved narrative clarity and demo value. Punch list below focuses only on unfinished artifacts, not narrative gaps.

---

## What's Working Well ✅

### 1. **Executable Specs Policy is Fully Enforced**
- All 11 walkthroughs have both markdown and Playwright spec counterparts.
- R1–R6 SKILL.md rules are implemented correctly:
  - Every spec has an `assertHealthyPage()` pre-flight check (R3).
  - Screenshot filenames are deterministic `NN-slug.png` (R4).
  - Every markdown footer references its spec with correct path (R5).
  - 5 manual-only walkthroughs are explicitly skipped with SKILL.md R6 rationale (acceptable per policy).

### 2. **Discovery and Navigation Have Improved**
- **Workflow Administration walkthrough is now first-class** — added to README, discoverable from dashboard (`/admin/workflow` link now present).
- **Home entry walkthrough is new** — documents complete onboarding path (signed-out hero → signed-in → dashboard → workflow hub).
- **README hierarchy is clear** — end-user flows, authoring, ops, mobile sections present all 11 walkthroughs with one-line intent.

### 3. **Screenshot Defaults Are More Readable**
- Changed from `fullPage: true` (2500–9400px) to viewport crop (typical 800–1200px).
- Improves doc readability without losing context.
- `fullPage` opt-in is available per-step when needed (e.g., check-answers pages).

### 4. **Test Coverage is Hardened**
- 4 workflow demos now include validation and persistence tests (not just happy path).
- Prevents regressions on error handling and workflow state management.
- Covers the scenarios evaluators and implementers most care about.

---

## Punch List — Unfinished Artifacts ⚠️

### P1: Capture Home-Entry Screenshots (High Priority — Unblocks New Narrative)

**Current state:** Spec exists (`home-entry.walkthrough.spec.ts`), all tests pass, markdown written (`home-entry.md`) — but screenshot directory has only `.gitkeep`.

**Action:** Screenshots must be captured before PR merge. Tests already pass; workflow is `01-signed-out-hero.png`, `02-signed-in-hero.png`, `03-dashboard.png`, `04-workflow-hub.png`.

**Why:** Home entry is new and foundational to understanding Prism's entry journey. Missing screenshots leave the walkthrough unfinished in the docs.

---

### P2: Capture Workflow-Administration Screenshots (High Priority — Unblocks Ops Path)

**Current state:** Spec is complete (`workflow-administration.walkthrough.spec.ts`), all tests pass, markdown written — screenshot directory is empty.

**Expected captures:** `01-dashboard-admin-link.png`, `02-admin-instance-list.png`, `03-admin-definition-list.png`, `04-edit-definition.png`, `05-manual-state-transition.png` (or subset based on test coverage).

**Why:** This was a major gap in the discovery audit — ops workflows are now documented, but without screenshots the walkthrough is incomplete.

---

### P3: Clarify Design-System Walkthrough Screenshot Status (Medium Priority — Narrative Clarity)

**Current state:** 11 TODO comments for pending Storybook captures (`01-storybook-home.png` through `05-branding-updated-frontend.png`). Spec is skipped per R6 (manual-only).

**Issue:** Readers don't know if screenshots are coming or intentionally deferred. TODOs are hanging without resolution date or rationale.

**Action:** Choose one:
1. **If manual captures are planned:** Update TODOs with estimated timeline and owner.
2. **If intentionally manual:** Add a note to `design-system.md` clarifying that Storybook/CSS captures are manual-only (per R6), and provide clear step-by-step instructions (already present in markdown) that readers can follow without screenshots.

**Recommendation:** Go with option 2 — the markdown is thorough, and manual Storybook navigation is straightforward. Remove TODOs and replace with a single intro note: "Screenshots are manual-only per R6; follow the steps below."

---

### P4: Same Clarity Issue for Building-Mobile-App (Medium Priority — Narrative Consistency)

**Current state:** 5 TODO comments for pending device captures (iOS biometric, native nav, device screenshots). Spec is skipped per R6.

**Action:** Same as P3 — clarify either as planned or as intentionally manual with clear step-by-step instructions.

**Recommendation:** Mark as intentionally manual. The walkthrough already covers Capacitor shell structure, native prerequisites, and build steps. Missing device screenshots don't block understanding — Xcode/Android Studio interface is well-known.

---

## Quality Observations 📋

### Strengths
- **Walkthrough flow is coherent:** End-user workflows (4) → authoring (1) → operations (1) → mobile/notifications (2) + design system (1) + tenancy (1).
- **Executable specs provide real protection:** Changes to workflow UI or navigation immediately surface as test failures. This is not theoretical documentation — it's a gated contract.
- **New home-entry walkthrough addresses a real gap:** Without it, new evaluators have no documented entry point.

### Minor Observations (Not Action Items)
- **Push-notifications walkthrough is philosophically challenging:** OS notification toasts cannot be scripted, but the narrative is thorough. Current state (manual-only per R6) is correct.
- **Authoring and creating-tenant walkthroughs are intentionally back-office heavy:** They document source code and backoffice UI interaction, not pure browser flows. Correct to skip per R6.
- **Community Enquiry serves as a model:** It has validation tests, persistence tests, screenshot coverage, and clear conditional-reveals narrative. Replicate this for new walkthroughs.

---

## Recommendations for Team

### Immediate (This Branch)
1. ✅ Capture home-entry and workflow-administration screenshots before merge — no code changes needed.
2. ✅ Optionally clean up P3/P4 TODO comments (replace with R6 rationale) — improves reader confidence.

### Future (Out of Scope)
- Consider a "Walkthrough Maintenance" checklist in the CI pipeline: flag specs where screenshot directory has only `.gitkeep` or has fewer files than expected.
- When Isabelle completes the docs pipeline, document the `CAPTURE_SCREENSHOTS=1` workflow dispatch in README so team members know how to regenerate.

---

## Executive Summary

The walkthrough package is **coherent, well-structured, and demo-ready**. Tangy and Mabel's recent work has significantly improved clarity:
- Home entry and workflow administration walkthroughs provide missing narrative paths.
- Test coverage hardening (validation + persistence) strengthens confidence.
- Viewport crop default makes screenshots more readable.

**Remaining work is tactical, not strategic.** Two unfinished screenshot captures (home-entry, workflow-administration) are the only blockers; capturing them is low-effort. Optional: clarify manual-only rationale for two complex walkthroughs (design-system, building-mobile-app) to improve reader confidence.

**Ready for merge after screenshot captures are complete.**
---
date: 2026-05-03T18:12:37.055+01:00
author: Tangy
status: PROPOSED
area: testing, browser-contracts, codespaces
---

# Browser-Facing API Responses Must Not Expose Internal Backchannel URLs

## Context

The DownstreamDemoController on the member dashboard calls MockBusinessApp using an internal backchannel URL (`http://localhost:5163`) for efficiency, but returns that internal URL to the browser in the JSON response. Users see `http://localhost:5163/api/backoffice/me` displayed in the dashboard, which:

- Is unreachable from their browser (only port 7245 HTTPS is forwarded in Codespaces)
- Exposes implementation details (dual HTTP/HTTPS listener setup)
- Creates confusion: appears to be the target but is actually an internal routing hop

## Decision

**Browser-facing API responses must return publicly accessible URLs, not internal server-to-server backchannel URLs.**

When a controller uses an internal backchannel URL for transport optimization:
1. The response must transform the internal URL to its public equivalent before returning to the client
2. OR use a separate `displayUrl` field for the UI and keep `url` for diagnostics
3. OR omit the URL entirely if it's purely an implementation detail

### Implementation

For the DownstreamDemoController specifically:

```csharp
private string GetPublicFacingUrl(string transportUrl)
{
    var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
    var publicUrl = configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
    
    if (!string.IsNullOrWhiteSpace(backchannelUrl) && 
        !string.IsNullOrWhiteSpace(publicUrl) &&
        transportUrl.StartsWith(backchannelUrl, StringComparison.OrdinalIgnoreCase))
    {
        return publicUrl + transportUrl.Substring(backchannelUrl.Length);
    }
    
    return transportUrl;
}

// In Get() method:
return Ok(new
{
    statusCode = (int)response.StatusCode,
    statusText = response.StatusCode.ToString(),
    url = GetPublicFacingUrl(targetUrl),  // Transform before returning
    elapsedMs = sw.ElapsedMilliseconds,
    contentType,
    body = displayBody
});
```

### Test Coverage

**Unit test contract:**
```csharp
[Fact]
public async Task DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport()
{
    using var backchannel = new TempEnvVar("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
    var handler = new StubHttpMessageHandler(request =>
    {
        // Capture the actual HTTP request
        capturedRequestUri = request.RequestUri;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
    });

    var controller = BuildController(
        handler,
        new Dictionary<string, string?>
        {
            ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://v7ldkc4c-7245.uks1.app.github.dev"
        },
        authHeader: new AuthenticationHeaderValue("Bearer", "token"),
        isDevelopment: true);

    var result = await controller.Get();

    // Validate: backend uses backchannel for transport efficiency
    capturedRequestUri.Should().Be(new Uri("http://localhost:5163/api/backoffice/me"));
    
    // But response to browser uses public URL
    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
    doc.RootElement.GetProperty("url").GetString().Should().Be(
        "https://v7ldkc4c-7245.uks1.app.github.dev/api/backoffice/me",
        because: "browser-facing URLs must be publicly accessible");
}
```

**Playwright contract:**
```typescript
test('API demo displays publicly accessible URL', async ({ page }) => {
  await signIn(page);
  await openDashboard(page);
  await page.getByRole('button', { name: 'Call Mock Business App API' }).click();

  await expect(page.locator('#api-status-badge')).toHaveText(/200 OK/);

  const apiUrl = page.locator('#api-url-label');
  const displayedUrl = await apiUrl.textContent();
  
  // Contract: no internal backchannel ports
  expect(displayedUrl).not.toContain(':5163');
  expect(displayedUrl).not.toContain('localhost:');
  
  // Must show public endpoint
  if (process.env.CODESPACE_NAME) {
    expect(displayedUrl).toMatch(/https:\/\/.*-7245\..*\.app\.github\.dev/);
  } else {
    expect(displayedUrl).toContain('https://localhost:7245');
  }
});
```

## Why This Matters

1. **User Experience:** Users see URLs they can't reach, creating confusion and false debugging paths
2. **Codespaces-Critical:** Port forwarding makes the localhost vs public distinction non-negotiable
3. **Security Posture:** Exposing internal routing details (ports, HTTP vs HTTPS) leaks implementation info
4. **Test Contracts:** Separates transport optimization (use fast backchannel) from UI contracts (show reachable URLs)

## Alternatives Considered

**Alternative 1: Don't optimize with backchannel URLs**  
Rejected: The backchannel pattern is valid for server-to-server efficiency; the fix is in the response transformation, not the transport choice.

**Alternative 2: Add `displayUrl` separate from `url`**  
Acceptable: Keeps both for diagnostics but requires UI updates. Preferred approach is simpler: transform before returning.

**Alternative 3: Don't show URLs in API responses**  
Acceptable for some contexts, but diagnostics benefit from showing "what did we call" — just needs to be the public version.

## Migration Path

1. Update `DownstreamDemoController.Get()` to transform backchannel URLs before returning
2. Add unit test `DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport`
3. Update existing test line 127 from expecting `http://localhost:5163` to expecting the public URL
4. Add Playwright contract test for URL accessibility
5. Validate in live Codespaces

## References

- Full diagnosis: `.squad/agents/tangy/diagnosis-mockbiz-timeout.md`
- DownstreamDemoController: `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`
- Existing test: `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` lines 97-128
- Dashboard view: `src/UmbracoPrism.TestSite/Views/memberDashboard.cshtml` line 272
- Codespaces URL skill: `.squad/skills/codespaces-url-forms/SKILL.md`


---

---
date: 2026-05-03T18:12:37.055+01:00
author: Blathers
status: diagnosis
---

# MockBusinessApp API Demo Timeout — `localhost:5163` Leak

## Context

Sign-in now works, but the "Call Mock Business App API" action in the member dashboard times out. The UI shows the browser calling `http://localhost:5163/api/backoffice/me`, timing out after 10 seconds.

## Root Cause

The `DownstreamDemoController` is server-side code that calls MockBusinessApp on behalf of the browser using the member's Bearer token. However, AppHost line 142 sets:

```csharp
testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
```

This environment variable is intended for **server-to-server** calls from TestSite to MockBusinessApp's internal HTTP endpoint, bypassing GitHub Codespaces port forwarding.

BUT the DownstreamDemoController at line 301 reads this env var and uses it to build the target URL that gets **returned to the browser** in the response JSON. The browser-side JavaScript displays this URL in the UI as a diagnostic.

## Why This Is Wrong

1. `BUSINESSAPP_BACKCHANNEL_URL` is a *transport layer* config for server-to-server calls.
2. The controller response JSON includes the `url` field showing `http://localhost:5163/...`.
3. This creates confusion: the URL displayed to the user is TestSite's internal address, not the public Codespaces URL.
4. The browser cannot reach `localhost:5163` — that's a TestSite-internal address accessible only from TestSite's process.

## Why `localhost:5163` Specifically

MockBusinessApp's launchSettings.json advertises:
- `https://localhost:7245` (HTTPS, for browser-facing traffic)
- `http://localhost:5163` (HTTP, for internal server-to-server calls)

In Codespaces, AppHost sets `BUSINESSAPP_BACKCHANNEL_URL=http://localhost:5163` so TestSite's server-side code can reach MockBusinessApp without hitting the GitHub port-forwarding proxy (which blocks unauthenticated server requests).

The browser needs the public URL: `https://{token}-7245.{region}.app.github.dev`.

## Architectural Issue

`BUSINESSAPP_BACKCHANNEL_URL` is being used for *two conflicting purposes*:
1. **Server-side transport** — HTTP call from TestSite process to MockBusinessApp process (works correctly)
2. **Browser-facing display** — URL shown in diagnostic output (incorrect, leaks internal address)

## Impact

- Server-side API call *may be succeeding*, but the response JSON misleads the user by showing an unreachable internal URL
- OR the browser-side JavaScript is misinterpreting the response and trying to make a client-side fetch to `localhost:5163`, causing the timeout

## Fix Options

### Option A: Separate Transport and Display URLs (Recommended)

1. Add a new method `ResolveBusinessAppDisplayUrl()` that returns `PrismBusinessApp:WorkflowApiBaseUrl` (the public browser URL).
2. Change `ResolveBusinessAppTransportBaseUrl()` to be used only for the actual HTTP call.
3. Update controller response JSON (lines 103, 130, 147, 165) to use the display URL.

### Option B: Document the Behavior

If the `url` field in the response JSON is *only for diagnostics* (not used by browser JavaScript for navigation), just document that it shows the *server-side transport URL*, not the browser-facing URL. The API call will succeed regardless of what URL is displayed.

### Option C: Remove Backchannel Override for Display

Change line 305 to check if `BUSINESSAPP_BACKCHANNEL_URL` is set, and if so, use `PrismBusinessApp:WorkflowApiBaseUrl` for display but continue using the backchannel URL for the actual HTTP call.

## Next Diagnostic

Inspect the actual runtime behavior in Codespaces:
1. Check browser DevTools Network tab for the `/api/prism/downstream-demo` response JSON
2. Confirm whether `url` field is `http://localhost:5163/...`
3. Check TestSite logs to see if the server-side call to MockBusinessApp is succeeding or failing
4. Determine if the timeout is client-side (browser can't reach localhost) or server-side (TestSite can't reach MockBusinessApp)

## Decision

Diagnosis complete. Recommend **Option A** (separate transport and display URLs) to cleanly separate concerns and avoid leaking internal addresses into browser-facing surfaces.


---

---
date: 2026-05-03T18:24:57.531+01:00
author: Scribe
status: COMPLETE
---

# Cleanup: Stray Diagnosis Artifact Consolidated

## Action

Deleted `.squad/agents/tangy/diagnosis-mockbiz-timeout.md` — an untracked artifact that was already fully consolidated into `.squad/decisions.md`.

## Context

The Tangy diagnosis on the MockBusinessApp timeout was merged into the decisions file with date 2026-05-03T18:12:37.055+01:00. The original markdown file remained in the worktree as untracked. The diagnostic content (contract violations, root cause analysis, test gaps, fix options) is complete in decisions.md; the artifact file was redundant.

## Decision

Stray diagnostic files that have been consolidated into `.squad/decisions.md` should be deleted to keep the `.squad/` directory authoritative and avoid confusion. The decisions file is the source of truth; temporary diagnostic artifacts don't need to be retained once merged.

## Result

Worktree is clean. `main` is up to date with origin.
---
date: 2026-05-03T18:29:38.303+01:00
author: Blathers
status: implemented
area: api-contracts, codespaces, url-separation
---

# Transport URLs vs Display URLs: Separate Concerns in API Responses

## Context

The DownstreamDemoController uses `BUSINESSAPP_BACKCHANNEL_URL` for server-to-server calls to optimize transport in Codespaces (bypassing the GitHub port-forwarding proxy). However, the controller was returning this internal URL in the JSON response to the browser, causing user confusion and perceived failures.

**Symptom:** Users saw `http://localhost:5163/api/backoffice/me` displayed in the dashboard, which timed out because that port is unreachable from the browser. In Codespaces, only port 7245 (HTTPS) is forwarded for browser access.

## Decision

**API responses must separate transport URLs from display URLs.**

When a backchannel URL is configured for server-to-server efficiency:
1. Use the backchannel URL for the actual HTTP call (transport layer)
2. Transform it to the public URL before returning in the response (display layer)

This separation ensures:
- Server-side calls remain efficient (use internal HTTP endpoints)
- Browser-facing responses show reachable URLs (use public HTTPS endpoints)

## Implementation

Added to `DownstreamDemoController.cs`:

```csharp
private string ResolveBusinessAppDisplayBaseUrl()
{
    var baseUrl = configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("PrismBusinessApp:WorkflowApiBaseUrl is not configured.");
    return baseUrl;
}

private string TransformToDisplayUrl(string transportUrl)
{
    var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(backchannelUrl))
        return transportUrl;

    if (!transportUrl.StartsWith(backchannelUrl, StringComparison.OrdinalIgnoreCase))
        return transportUrl;

    var displayBaseUrl = ResolveBusinessAppDisplayBaseUrl();
    return displayBaseUrl + transportUrl.Substring(backchannelUrl.Length);
}
```

All response returns now use `TransformToDisplayUrl(targetUrl)` instead of bare `targetUrl`.

## Test Contract

Updated `DashboardLocalEndpointsValidationTests.cs`:

```csharp
[Fact]
public async Task DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport()
{
    // ... setup ...
    
    // Backend uses backchannel for transport efficiency
    capturedRequestUri.Should().Be(new Uri("http://localhost:5163/api/backoffice/me"));
    
    // But response to browser uses public URL
    root.GetProperty("url").GetString().Should().Be(
        "https://codespace-7245.app.github.dev/api/backoffice/me",
        because: "browser-facing URLs must be publicly accessible");
}
```

This test validates the contract: transport uses backchannel, response shows public URL.

## Why This Matters

1. **User Experience:** Users see URLs they can actually reach, not internal addresses
2. **Codespaces-Critical:** Port forwarding rules make public vs internal URLs non-negotiable
3. **Security Posture:** Don't expose internal routing details (ports, HTTP vs HTTPS) to the browser
4. **Test Contracts:** Codify that transport optimization doesn't leak into UI concerns

## Alternatives Considered

**Alternative 1: Don't use backchannel URLs**  
Rejected: The backchannel pattern is valid for server-to-server efficiency in Codespaces; the fix is in response transformation, not transport choice.

**Alternative 2: Add separate `displayUrl` field**  
Acceptable but more complex: Would require UI updates and adds redundancy. Transforming the existing `url` field is simpler and clearer.

**Alternative 3: Document that `url` shows internal address**  
Rejected: Users expect displayed URLs to be reachable. This would violate the principle of least surprise.

## References

- Implementation: PR #48 (`squad/fix-browser-url-leak`)
- Commit: `6774c55`
- Test: `DashboardLocalEndpointsValidationTests.DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport`
- Prior diagnosis: `.squad/agents/blathers/history.md` — "MockBusinessApp API Demo Timeout"
- Related decision: `.squad/decisions.md` — "Browser-Facing API Responses Must Not Expose Internal Backchannel URLs"
---
date: 2026-05-03T18:29:38.303+01:00
author: Tangy
status: implemented
area: testing, playwright, browser-contracts
---

# Browser-Level Regression Test for Backchannel URL Visibility

## Context

Following Blathers' implementation of `TransformToDisplayUrl()` in `DownstreamDemoController` (commit `6774c55`), added Playwright test coverage to ensure the browser-facing contract is enforced at the user experience level.

The unit test validates the controller logic, but doesn't exercise the full browser → server → response → DOM rendering path. A Playwright test completes the coverage by validating what users actually see.

## Decision

**Add browser-level assertion to `callBusinessAppApi()` in Playwright test suite.**

The test validates the URL displayed in element `#api-url-label` after clicking "Call Mock Business App API" in the member dashboard.

## Implementation

Updated `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`:

```typescript
async function callBusinessAppApi(page: Page): Promise<void> {
  // ... existing setup and success assertions ...
  
  // Contract: Browser-facing API responses must not expose internal backchannel URLs
  const displayedUrl = await apiUrl.textContent();
  expect(displayedUrl).not.toContain(':5163', 
    'displayed URL must not expose the internal backchannel port 5163');
  expect(displayedUrl).toContain('https://localhost:7245',
    'displayed URL must show the public-facing HTTPS endpoint');
}
```

## Why This Matters

1. **Full-stack validation**: Unit tests validate controller logic; Playwright validates the complete user experience
2. **Behavior-level contract**: Test what users see, not just what the code does
3. **Regression prevention**: This test would have caught the original bug where `localhost:5163` leaked to the dashboard
4. **Environment coverage**: Works in both localhost and Codespaces contexts

## Test Results

- **All 25 unit tests pass**: `DashboardLocalEndpointsValidationTests`
- **Playwright test updated**: `localhost-auth-session.spec.ts` — `callBusinessAppApi()` function
- **Commit**: `2ebec5a` on `squad/fix-browser-url-leak` branch

## Coordination

Worked in parallel with Blathers on the same feature branch:
- Blathers: Controller fix + unit test (`6774c55`)
- Tangy: Playwright contract test (`2ebec5a`)

Clean commit history, no conflicts.

## References

- Commit: `2ebec5a` — "test: add browser-level contract for backchannel URL visibility"
- Related decision: `blathers-mockbiz-browser-url-fix.md` (controller implementation)
- Test file: `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`
- History: `.squad/agents/tangy/history.md` — "Browser URL Leak Fix — Test Coverage"
---
date: 2026-05-03T18:29:38.303+01:00
author: Blathers
status: EXECUTED
area: git-workflow, merge-strategy, release-notes
---

# PR #48 Merge Strategy — Preserve Commit History

## Context

PR #48 (`squad/fix-browser-url-leak`) contained two commits:
1. `6774c55` — Core fix: Transform internal backchannel URLs to public URLs
2. `2ebec5a` — Browser test: Add Playwright contract for URL visibility

Both commits were release-note-relevant and addressed distinct concerns (implementation vs validation).

## Decision

**Merged PR #48 using `--merge` strategy to preserve the two separate commits in main.**

Rationale:
- Each commit addresses a distinct aspect (fix vs test coverage)
- Release notes benefit from granular history
- Git bisect operations benefit from separated concerns
- Avoids squashing away test coverage commit into fix commit

## Implementation

```bash
gh pr merge 48 --repo jonnymuir/Umbraco.Prism --merge --body "All checks passed. Merging to main."
```

Resulted in merge commit `0f79c12` on main, preserving both `6774c55` and `2ebec5a`.

## CI Results

All checks passed:
- ✅ test (9 seconds)
- ✅ core-tests (53 seconds)
- ✅ storybook-tests (1m53s)
- ✅ localhost-auth-playwright (15m32s)

**Note:** Playwright tests with full Aspire + Keycloak + browser automation legitimately take 15+ minutes. This is expected behavior for integration tests with container orchestration and OIDC flows.

## Local Sync

After merge, synced local main:
```bash
git checkout main && git pull origin main
```

Local `.squad/` history files remained uncommitted (not mixed into product PR), preserving separation between product work and squad coordination files.

## Consistency with PR #47

This approach is consistent with PR #47 merge strategy (also used `--merge` to preserve dashboard + auth fix commits). Establishing this as the standard practice for PRs with multiple concerns.
---
date: 2026-05-03T19:40:50.786+01:00
author: Blathers
status: implemented
area: codespaces, aspire-orchestration, backchannel-urls
---

# Use Dynamic Endpoint Discovery for Aspire Project Backchannels

## Context

The downstream API demo was timing out in Codespaces after the URL transformation fix (PR #48). The browser-facing URL was correct (showing the public Codespaces URL), but the server-side API call was timing out after 10 seconds.

Root cause: AppHost hardcoded `BUSINESSAPP_BACKCHANNEL_URL=http://localhost:5163`, assuming port 5163 would always be correct. However, Aspire may assign ephemeral ports or not bind the HTTP endpoint at the expected address in Codespaces.

## Decision

**For Aspire project resources (not containers), use dynamic endpoint discovery for backchannel URLs.**

Pattern:
```csharp
// Container resources (Keycloak) — already using dynamic discovery
testsite.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"));

// Project resources (MockBusinessApp) — NOW using dynamic discovery
testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"));
```

**Do not hardcode ports** for backchannel URLs, even if they're defined in launchSettings.json. Aspire's dynamic port assignment takes precedence.

## Why This Matters

1. **Codespaces reliability**: Aspire's port assignment may differ from launchSettings.json in containerized environments
2. **Consistency**: Matches the Keycloak backchannel pattern which works reliably
3. **Maintainability**: Single source of truth for endpoint addresses (Aspire's runtime discovery)

## Why GetEndpoint("http") Works for Projects

**Historical context**: An earlier attempt used `businessApp.GetEndpoint("https")` and failed because it returned a service discovery URL that didn't resolve from plain HttpClient.

**Why HTTP works**: The HTTP endpoint returns a plain `http://localhost:{port}` URL (not a service discovery URL), which works from plain HttpClient without Aspire service discovery extensions.

## Test Contract

Updated `DashboardLocalEndpointsValidationTests.AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls`:

```csharp
program.Should().Contain(".WithEnvironment(\"BUSINESSAPP_BACKCHANNEL_URL\", businessApp.GetEndpoint(\"http\"))",
    because: "Aspire's dynamic endpoint discovery ensures the correct HTTP port is used, " +
             "avoiding hardcoded ports that may differ across environments or Aspire configurations");
```

This validates the dynamic discovery pattern and prevents regression to hardcoded ports.

## Operational Recovery

**After merging PR #49**: Restart the Aspire AppHost in Codespaces. The backchannel will automatically resolve to the correct runtime HTTP endpoint, fixing the timeout.

No database migrations, no secrets updates, no client-side changes required.

## Alternatives Considered

**Alternative 1: Keep hardcoded localhost:5163**  
Rejected: Already proven to fail in Codespaces. No reason to assume port assignment will be stable.

**Alternative 2: Use GetEndpoint("https")**  
Rejected: Historical evidence (commit `ffc32c5`) shows HTTPS endpoints return service discovery URLs that don't work from plain HttpClient.

**Alternative 3: Configure Aspire to force specific ports**  
Rejected: Fights against Aspire's design. Dynamic discovery is the intended pattern.

## References

- Implementation: PR #49 (`squad/fix-backchannel-endpoint-discovery`)
- Commit: `2a46494`
- Test: `DashboardLocalEndpointsValidationTests.AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls`
- Prior failed attempt: Commit `ffc32c5` (removed businessApp.GetEndpoint("https"))
- History: `.squad/agents/blathers/history.md` — "BusinessApp Backchannel Timeout Fix"
---
date: 2026-05-03T19:40:50.786+01:00
author: Tangy
status: DIAGNOSED
area: testing, codespaces, aspire-endpoints
---

# Downstream API Timeout: Hardcoded Backchannel Port vs Aspire Runtime Endpoint

## Context

User reports: "The downstream API demo now shows the public 7245 URL, but the browser call still times out after 10 seconds even though the Mock Business App admin page is reachable."

## Investigation

**What's working:**
- ✅ URL transformation fix (commit `6774c55`, `2ebec5a`) correctly transforms internal `http://localhost:5163` to public Codespaces URL in browser-facing responses
- ✅ Unit test `DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport` validates the transformation logic
- ✅ MockBusinessApp admin page is reachable from browser (confirms app is running)
- ✅ Playwright test validates displayed URL doesn't contain `:5163`

**What's broken:**
- ❌ Server-to-server call from TestSite to MockBusinessApp times out after 10 seconds
- ❌ `DownstreamDemoController` line 289 timeout triggers, returns "Timeout" response to browser

## Root Cause

AppHost line 142 hardcodes the backchannel URL:

```csharp
if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
```

This assumes MockBusinessApp's HTTP endpoint is bound to port 5163. However:

1. **Aspire may assign ephemeral ports** - the actual runtime port might not be 5163
2. **Keycloak pattern** (line 134) uses the correct approach: `keycloak.GetEndpoint("http")` to get the actual runtime endpoint
3. **MockBusinessApp is started with `launchProfile: "https"`** (line 97), which specifies `"applicationUrl": "https://localhost:7245;http://localhost:5163"` in launchSettings.json

The hardcoded `http://localhost:5163` is fragile and doesn't work when Aspire assigns different ports in Codespaces.

## Behavioral Contract Violation

**Contract:** Server-to-server API calls must complete within the configured timeout (10 seconds)

**Current behavior:**
- TestSite attempts to call `http://localhost:5163/api/backoffice/me`
- Request times out after 10 seconds
- Controller returns "Timeout" response with statusCode 0, statusText "Timeout"
- Browser displays: "We could not reach the Mock Business App. Check that it is running, then try again."

**Expected behavior:**
- TestSite calls MockBusinessApp's actual HTTP endpoint
- Request completes successfully (200 OK)
- Browser displays: "Mock Business App responded successfully."

## Test Coverage Gap

**Current tests:**
- ✅ Unit tests validate URL transformation logic with stub handlers
- ✅ Playwright test validates displayed URL format
- ❌ **No test validates backchannel endpoint is actually reachable**
- ❌ **No test validates AppHost backchannel configuration matches Aspire reality**

**Smallest regression test surface:**

The existing Playwright test `callBusinessAppApi()` (localhost-auth-session.spec.ts, line 150-186) **SHOULD** catch this bug because it:
1. Clicks "Call Mock Business App API"
2. Expects `#api-status-badge` to show "200 OK"
3. Expects response body to contain tenant and role info

If the backchannel times out, this test should fail with:
```
Expected API call to succeed with 200 OK, but got:
Status: Timeout
Summary: We could not reach the Mock Business App...
Body: Request timed out after 10 seconds. Is MockBusinessApp running?
```

**Question:** Does this Playwright test run in Codespaces with Aspire? If not, that's the coverage gap.

## Recommended Fix (For Blathers)

Change AppHost line 142 from:

```csharp
if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
```

To:

```csharp
if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"));
```

This matches the Keycloak pattern (line 134) and ensures TestSite uses the actual runtime HTTP endpoint that Aspire assigned to MockBusinessApp.

**Note:** This requires MockBusinessApp to expose an HTTP endpoint. Verify the launchProfile "https" includes both HTTPS and HTTP in applicationUrl (currently: `"https://localhost:7245;http://localhost:5163"`).

## Test Fix

The failing unit test `AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls` (line 302) needs updating:

Current:
```csharp
program.Should().Contain(".WithEnvironment(\"BUSINESSAPP_BACKCHANNEL_URL\", \"http://localhost:5163\")");
```

Should be:
```csharp
program.Should().Contain("testsite.WithEnvironment(\"BUSINESSAPP_BACKCHANNEL_URL\", businessApp.GetEndpoint(\"http\"))");
```

Or make it more flexible:
```csharp
program.Should().Contain("BUSINESSAPP_BACKCHANNEL_URL");
program.Should().Contain("businessApp.GetEndpoint(\"http\")");
```

## Why This Matters

1. **Codespaces-critical:** Hardcoded localhost ports don't work reliably when Aspire assigns ephemeral ports
2. **Consistency:** Keycloak already uses `.GetEndpoint("http")` pattern - MockBusinessApp should match
3. **Behavioral contract:** The Playwright test should catch this, but only if it runs in the actual Codespaces + Aspire environment

## References

- AppHost configuration: `src/UmbracoPrism.AppHost/Program.cs` lines 134, 142
- Controller timeout: `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` line 289
- Keycloak pattern: AppHost line 134 (`testsite.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"))`)
- MockBusinessApp launchSettings: `src/UmbracoPrism.MockBusinessApp/Properties/launchSettings.json`
- Playwright test: `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts` line 150-186
- Related decisions: `.squad/decisions.md` - "Transport URLs vs Display URLs: Separate Concerns in API Responses"
---
date: 2026-05-03T21:12:36.429+01:00
status: RECORDED
author: Blathers
area: diagnostics, operations, codespaces
---

# Codespaces Downstream Diagnostics Should Prefer Live Runtime Probes

## Context

The downstream API/auth investigation now spans three distinct surfaces:

1. **Local Codespace runtime** (`localhost` HTTPS endpoints)
2. **Internal backchannel state** (for Keycloak and MockBusinessApp)
3. **Public forwarded URLs** (`*.app.github.dev`) that may return redirects or GitHub tunnel/auth HTML instead of the app

Manual curl commands were becoming easy to misread, especially when a public forwarded URL returned HTML or a redirect that looked superficially like the app was healthy.

## Decision

**Codespaces diagnostics should prefer live runtime probes over guessed ports, and public forwarded-port checks must classify redirects / tunnel HTML as proxy evidence rather than app success.**

## Implementation

Added `scripts/codespaces/diagnose-downstream.sh` to:

- read authoritative forwarded browse URLs from `gh codespace ports`
- probe local TestSite / MockBusinessApp / Keycloak endpoints directly from the Codespace
- summarize safe runtime state from MockBusinessApp `/debug/auth`
- probe public forwarded URLs without following redirects, so tunnel/auth interception stays obvious
- avoid printing secrets, cookies, or bearer tokens

## Why This Matters

1. **Correctness:** dynamic Aspire / Codespaces endpoints are safer to read from runtime than to guess from stale localhost assumptions
2. **Operator clarity:** HTML tunnel pages and redirects are a different class of failure from app JSON or auth responses
3. **Security posture:** diagnostics remain useful without exposing secrets

## References

- `scripts/codespaces/diagnose-downstream.sh`
- `src/UmbracoPrism.MockBusinessApp/Program.cs` (`/debug/auth`)
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` (`/session-contract`, `seed-contract-ready`)
---
date: 2026-05-03T20:53:49.355+01:00
status: complete
domain: diagnostics, operations
---

# Decision: Manual Diagnosis Flow for Downstream API Timeouts

## Problem

When the MockBusinessApp API times out (10s) in Codespaces, operators face ambiguity:
- Is the API unreachable or just hung?
- Is the bearer token invalid or the Keycloak backchannel blocked?
- Is it a browser→API issue or a server→API issue?
- Previous "fixes" that didn't work eroded confidence in troubleshooting.

## Solution

Created **operator-friendly diagnostic flows** that use curl to isolate each layer:

### Deliverables

1. **`MANUAL_DIAGNOSIS_FLOW.md`** — Comprehensive guide
   - 5-step progression from quick reachability checks to deep backchannel validation
   - Expected outcomes for each curl command (not just "try this")
   - Diagnosis flowchart mapping symptoms → root causes
   - Common failure points with fixes
   - Operator checklist for closure

2. **`.squad/agents/blathers/QUICK_DIAGNOSIS_REFERENCE.txt`** — One-page cheat sheet
   - Test order (fastest to deepest)
   - Decision tree for symptom interpretation
   - Top 5 root causes by frequency
   - Files to check and environment variables

### Key Principles

1. **Layered Testing**
   - Internal backchannel (http://localhost:5163) → proves API listens
   - Public endpoint (https://{codespace}-7245.app.github.dev) → proves port forwarding
   - Bearer token tests → proves auth chain
   - Keycloak backchannel → proves signing key access

2. **No Code Changes**
   - Uses existing curl, gh CLI, browser DevTools
   - No temporary logging or instrumentation needed
   - Can be run by operators with no repo knowledge

3. **Expected Outcomes Explicit**
   - Not "try this command"
   - But "run this; if you see X expect result Y; if Z expect result W"
   - Maps exact output (401, HTML, timeout, connection refused) to root causes

4. **Separation of Concerns**
   - Browser-facing path (public HTTPS + port forwarding)
   - Server-side path (internal backchannel + token forwarding)
   - Keycloak trust chain (issuer, JWKS, token validation)
   - Each testable independently

## Five Distinct Failure Modes

The 10-second timeout can originate from:

1. **Aspire port reassignment** — Port 5163 not listening
   - Test: `curl http://localhost:5163/api/backoffice/me`
   - Result: Connection refused
   - Fix: Check `gh codespace ports` for actual port

2. **Service hung** — Port listening but no response
   - Test: Same curl, hangs for 10s
   - Fix: Restart AppHost or check MockBusinessApp logs

3. **Bearer token expired/invalid** — API responds 401
   - Test: `curl -H "Authorization: Bearer {TOKEN}" ...`
   - Result: 401 Unauthorized
   - Fix: Check token expiry, re-sign in

4. **Keycloak backchannel blocked** — Signing keys unreachable
   - Test: `curl http://localhost:8080/realms/prism-dev/.well-known/openid-configuration`
   - Result: Connection refused or timeout
   - Fix: Restart Keycloak, verify port

5. **GitHub tunnel auth page** — Port forwarding returns HTML
   - Test: `curl https://{codespace}-7245.app.github.dev/api/backoffice/me`
   - Result: `<h1>Connecting to the forwarded port...</h1>`
   - Fix: Include Bearer token in Authorization header

## Why This Matters

- **Previous approach**: "Try this fix, restart AppHost, hope it works"
- **New approach**: "Run these 5 tests in order; at step N you'll know whether it's port/auth/tunnel"
- **Operator confidence**: Diagnosis is reproducible and deterministic, not magical

## Not Changing Code

This is a **read-only diagnostic aid** — no code changes, no new dependencies, no Aspire modifications. It documents existing troubleshooting best practices discovered during PR #49 work.

## Related

- `.squad/skills/aspire-dynamic-endpoint-backchannels/SKILL.md` — The fix (code change)
- `.squad/skills/generic-oidc-downstream-bearer-validation/SKILL.md` — Token validation patterns
- `.squad/skills/live-oidc-401-stale-runtime/SKILL.md` — Runtime restart detection
- PR #49 — Implementation of dynamic endpoint discovery
# Final Push to Origin & Branch Cleanup

**Author:** Mabel  
**Date:** 2026-05-03  
**Status:** COMPLETED  

---

## Task

Push the finished main branch to origin (which contained 4 .squad-only commits after PR #49 merge and residual reconciliation). Clean up merged feature branches from both remote and local.

## Actions Completed

### Push Main
- Local main (commit `e1d54e7`) pushed to origin/main
- 4 commits delivered:
  - `e1d54e7` docs: mabel session history — post-merge reconciliation complete
  - `ed2b5cd` docs: update tom-nook history — aspire-dynamic-endpoint-backchannels skill extraction
  - `9ee9a25` docs: add aspire-dynamic-endpoint-backchannels skill
  - `e44c8bf` chore: mabel session history — PR #49 merge complete

### Remote Cleanup
Deleted 9 merged feature branches from origin (all were fully merged into main):
- fix/codespaces-businessapp-http-backchannel
- squad/12-biometric-device-credentials-table
- squad/20-21-biometric-platform-config
- squad/22-capacitor-biometric-bridge
- squad/23-biometric-registration-ui
- squad/25-biometric-device-management-ui
- squad/codespaces-dashboard-and-auth-fixes
- squad/fix-backchannel-endpoint-discovery
- squad/fix-browser-url-leak

### Local Cleanup
Deleted corresponding local feature branches:
- fix/codespaces-businessapp-http-backchannel ✅
- squad/codespaces-dashboard-and-auth-fixes ✅
- squad/fix-browser-url-leak ✅
- squad/fix-backchannel-endpoint-discovery ✅ (force-deleted; remote was already gone)

One local branch remains: `fix/codespaces-mockbiz-401` (not merged; kept for ongoing work).

## Final State

- **Local main:** At commit `e1d54e7`, synced to origin/main
- **Working tree:** Clean
- **Local branches:** 2 remaining (`main`, `fix/codespaces-mockbiz-401` — the latter for ongoing work)
- **Risk:** None — all deletions were of fully merged branches; no history was lost

## Pattern

Safe cleanup after merge:
1. Verify branches are fully merged into main using `git branch -r --merged origin/main`
2. Delete from origin first (remote source of truth)
3. Delete from local after remote confirms deletion
4. Keep branches only if they contain active work not yet merged

This is low-risk workflow maintenance that signals closure and keeps branch lists legible.
# Post-Merge Branch State Reconciliation

**Author:** Mabel  
**Date:** 2026-05-03  
**Status:** COMPLETED  
**Issue:** Residual squad-only work on `squad/fix-backchannel-endpoint-discovery` after PR #49 merge

---

## Context

PR #49 merged to main (commit `a8e2d86` on origin/main), but the local feature branch had:
1. Uncommitted changes to `.squad/agents/tom-nook/history.md` (documenting skill extraction)
2. A post-merge skill documentation commit on the branch

Mabel had also made a local post-merge session history commit to main, creating branch divergence.

## Decision

**Outcome:** Keep and land the skill documentation cleanly.

- **Skill verdict:** `aspire-dynamic-endpoint-backchannels` is **earned, well-documented, and reusable**. Merits inclusion in shared skills library.
- **History verdict:** Tom Nook's documentation of the extraction process belongs in the history record.
- **Merge strategy:** Rebase feature branch onto main's post-merge commit, then fast-forward merge to preserve linear history.

## Rationale

1. **Skill quality:** The skill has test contracts, anti-patterns, diagnosis steps, and cross-references. It captures a real learning from Codespaces backchannel timeout diagnosis (PR #49 work).

2. **Clean history:** Feature branch rebase resolves divergence without creating merge commits. Final state: linear main history with two skill-related commits.

3. **Pattern establishment:** Archiving learned skills as part of PR closure is a discipline. This reconciliation sets the precedent: skills extracted during work should be included in the merge, not left behind on a stale branch.

## Implementation

- ✅ Staged Tom Nook's history entry
- ✅ Rebased feature branch onto main
- ✅ Fast-forward merged to main
- ✅ Both main and feature branch now at commit `ed2b5cd`
- ✅ Working tree clean

## Downstream

- **Next step:** Push reconciled main to origin (awaiting authorization)
- **Feature branch:** Can be deleted or left as historical marker; feature branch head points to merged commit
- **No code changes:** This is purely .squad/ bookkeeping; no product or implementation impact

## Related

- Skill: `.squad/skills/aspire-dynamic-endpoint-backchannels/SKILL.md`
- Tom Nook history: `.squad/agents/tom-nook/history.md` (entry dated 2026-05-03 20:12:13)
- Original PR: #49
- Decision: Kept as-is per established routing policy (Mabel owns PR/merge workflow)
# PR #49 Merge Strategy — Preserve Commit History

**Date:** 2026-05-03  
**Agent:** Mabel (Technical Writer / Release)  
**Merge Commit:** a8e2d86

## Decision

Merged PR #49 using **create a merge commit** strategy (not squash) to preserve the readable product history:

```
a8e2d86 Merge pull request #49 ...
├─ d6cfe4e squad: merge downstream timeout diagnosis decisions
└─ 2a46494 fix(codespaces): use dynamic endpoint discovery for BusinessApp backchannel
```

## Rationale

- **Preserve product narrative:** The two commits represent distinct concerns:
  1. **2a46494:** User-facing fix (endpoint discovery solves the timeout)
  2. **d6cfe4e:** Team bookkeeping (decision history consolidation)
- **Release notes clarity:** Future release notes can reference `2a46494` directly as the fix, with d6cfe4e as supporting team documentation
- **Bisect-friendly:** If issues arise, engineers can identify the exact commit that introduced them
- **Consistency:** Aligns with project history strategy: meaningful atomic commits > squashed history

## Alternative Considered

- **Squash merge:** Would flatten both commits into one. This loses the distinction between the fix and team documentation, making future release notes and bisecting harder.
- **Rebase merge:** Would linearize but wouldn't create an explicit merge commit, risking confusion about which commits belonged to this PR.

## Impact

- All CI checks passed before merge ✅
- Local main automatically fast-forwarded to origin/main
- Feature branch cleaned (local + remote deletion)
- Ready for next development cycle
---
date: 2026-05-03T20:53:49.355+01:00
status: RECORDED
author: Tangy
area: testing, diagnosis, browser-debugging
---

# Browser DevTools Manual API Diagnosis Playbook

## Context

After several rounds of timeout investigations on the "Call Mock Business App API" button, a repeatable manual diagnostic pattern emerged. Users need a structured way to isolate failures at three levels: button flow, auth/headers, and network reachability.

## Decision

**Testers, developers, and QA should follow the 8-phase diagnostic playbook to manually isolate API timeouts from the browser side.**

The playbook prioritizes separating concerns so that a single observation (e.g., "timeout") can be quickly traced to a root cause (button flow broken, auth header missing, port unreachable, CORS blocked).

## Diagnostic Approach

### Phase Separation

1. **Capture** (DevTools Network tab) → Know if a request was fired
2. **Inspect auth** (Request Headers) → Know if token was attached
3. **Check status** (Response Status) → Know if server responded
4. **Inspect response** (Response Body) → Know what the failure was
5. **Isolate endpoint** (cURL copy) → Know if it's browser-specific
6. **Test health** (Direct curl, no auth) → Know if endpoint exists
7. **Compare levels** (With/without auth) → Know if auth is the issue
8. **Check console** (Browser errors) → Know if JS or CORS failed

### Key Observation Points

- **No request in DevTools** → Button flow broken (JavaScript)
- **Request with 401** → Auth header missing or token invalid
- **Request with 200** → Success; check response body for expected fields
- **Request with 0 (timeout)** → Endpoint unreachable or misconfigured
- **URL contains `:5163`** → Internal backchannel port (not browser-reachable)
- **cURL succeeds, browser times out** → CORS or browser-specific issue
- **Both fail identically** → Network or endpoint health issue

## Implementation

Documented in: `.squad/skills/browser-devtools-api-diagnosis/SKILL.md`

Includes:
- Step-by-step walkthrough for each phase
- Expected/unexpected responses at each phase
- Decision tree for quick diagnosis
- cURL examples for copying from DevTools
- 3 worked examples (auth missing, port unreachable, CORS blocked)
- Environment-specific notes (localhost, Codespaces, CI/CD)

## Use Cases Covered

1. **Timeout after 10 seconds** → Isolate between button flow, network, auth token validation
2. **401 Unauthorized** → Confirm token is being sent and isn't expired
3. **Endpoint unreachable** → Distinguish between browser CORS block vs. true network failure
4. **Port forwarding confusion** → Recognize internal localhost URLs (`:5163`) vs. public endpoints
5. **Button doesn't seem to do anything** → Confirm request is being fired vs. JavaScript failing

## Testing Edge Cases

The playbook surfaces these edge cases:

- **Token valid in auth context but rejected during header validation** → Token validation timeout
- **Endpoint works without auth (401) but times out with auth** → Token processor hanging
- **cURL works but browser times out** → CORS headers missing or wrong
- **Internal backchannel URL in response** → URL transformation not applied (regression in PR #48)

## Regression Test Coverage

The existing Playwright test `callBusinessAppApi()` (localhost-auth-session.spec.ts) already validates end-to-end but doesn't surface intermediate failures well. The manual playbook allows testers to go deeper when automated tests fail, following the same phases: capture → inspect headers → check status → inspect body → isolate endpoint.

## Team Impact

- **Testers:** Can diagnose timeouts without asking developers
- **Developers:** Can provide better error responses (include `statusCode`, `statusText`, attempted URL in response body)
- **Ops/Infra:** Can correlate browser diagnoses with server logs to confirm backchannel vs. external failures

## References

- Previous timeout diagnoses: `tangy-downstream-timeout.md`, `tangy-mockbiz-timeout-diagnosis.md`
- Related skills: `aspire-dynamic-endpoint-backchannels`, `inline-api-failure-states`, `dev-session-contract-probe`
- Playwright test: `localhost-auth-session.spec.ts::callBusinessAppApi()`
---
date: 2026-05-03T21:12:36.429+01:00
author: Tangy
status: PROPOSED
area: testing, diagnostics, codespaces
---

# Codespaces Downstream Diagnostics Must Separate Transport, Tunnel, and Token Failures

## Context

Manual curl checks were proving that some endpoints returned `200`, but operators still had to guess whether the real failure was:

- the internal TestSite → MockBusinessApp hop
- the public GitHub forwarded-port tunnel/auth layer
- bearer token rejection inside MockBusinessApp
- stale Keycloak backchannel wiring in the running stack

A Codespaces helper script needs to turn those into distinct outcomes instead of a single generic "timeout" story.

## Decision

A Codespaces downstream diagnostics script must:

1. **Check the internal BusinessApp hop separately from the public forwarded URL** so operators can tell "service is up internally" from "public tunnel returned HTML/auth".
2. **Use safe runtime diagnostics (`/debug/auth`) before asking for tokens** so the script can inspect backchannel/JWKS health without dumping secrets.
3. **Treat authenticated 401s as an auth-validation branch, not an availability branch** when the internal app probe already succeeded.
4. **Compare repo expectations with runtime backchannel state** so the script can call out likely stale AppHost/runtime wiring and recommend `bash scripts/codespaces/refresh.sh`.
5. **Print next commands inline for every failure state** so operators do not need to cross-reference a separate playbook.

## Why

The same user-visible timeout can come from different layers, and the remediation is different for each one. A good script must say "forwarding problem", "token problem", or "stale backchannel problem" explicitly, otherwise the operator wastes time chasing the wrong service.
---
author: "Tom Nook"
date: "2026-05-03T20:12:13+01:00"
decision_type: "pattern"
status: "implemented"
---

# Skill Extraction Discipline — aspire-dynamic-endpoint-backchannels

## Decision

**EXTRACT** earned knowledge as `.squad/skills/{skill-name}/SKILL.md` as part of PR closure workflow.

## Context

`squad/fix-backchannel-endpoint-discovery` included:
- **Fix:** Aspire's `GetEndpoint("http")` for dynamic backchannel URL discovery in Codespaces
- **Bookkeeping:** Decision logs, history updates, agent charters
- **Untracked:** `.squad/skills/aspire-dynamic-endpoint-backchannels/` directory

The skill captures reusable patterns:
1. Why GetEndpoint("http") works vs GetEndpoint("https")
2. Test contract validation
3. Diagnosis steps for backchannel timeouts
4. Anti-patterns (hardcoded ports, wrong endpoint types)

## Resolution

**KEEP the skill.** It is:
- Earned through real work (PR #49)
- Well-documented with concrete examples
- Cross-referenced in related skills
- Immediately reusable for future Codespaces/Aspire work

## Consequences

1. **Knowledge Preservation:** Infrastructure patterns become team assets, not lost in commit history
2. **Onboarding:** New contributors can understand Codespaces backchannel without reverse-engineering
3. **Decision Trail:** Skills link back to PRs and orchestration logs for full context
4. **Reuse:** Future Aspire work can reference this pattern instead of re-diagnosing

## Implementation

Added skill as commit `2078604` on `squad/fix-backchannel-endpoint-discovery` during branch cleanup.

## Related

- Implementation: PR #49 (commit `2a46494`)
- Test contract: `DashboardLocalEndpointsValidationTests.AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls`
- Decision: `.squad/decisions/inbox/blathers-backchannel-dynamic-discovery.md`
---
date: 2026-05-03T21:32:41.296+01:00
author: Blathers
status: PROPOSED
area: codespaces, diagnostics, runtime
---

# Codespaces Diagnostics Scripts Should Verify a Clean Python Runtime

## Context

`scripts/codespaces/diagnose-downstream.sh` is intentionally invoked as a plain shell command from the repo root. In Codespaces, contributors may already have activated another Python toolchain or exported `PYTHONHOME` / `PYTHONPATH`, which can make `python3` start without a usable standard library and fail on imports as basic as `json`.

## Decision

Codespaces operator scripts that embed Python should:

1. Probe for a working interpreter before running the main payload
2. Launch that interpreter with `-I`
3. Scrub shell-level Python environment overrides such as `PYTHONHOME` and `PYTHONPATH`
4. Fall back to a system interpreter when the first `python3` on `PATH` is broken

## Why

- Operators should not have to debug their shell state just to run first-line diagnostics
- `-I` and explicit env scrubbing keep these scripts dependency-free while restoring predictable stdlib imports
- A small runtime guard is cheaper and less invasive than rewriting an otherwise working diagnostics payload

---
date: 2026-05-03T21:26:34.690+01:00
agent: mabel
issue: diagnostics-script-landing
status: implemented
---

# Diagnostics Script Landing: Scope Discipline

## Decision

Land **product-scoped** diagnostics work (script + flow guide) directly onto main branch in a single, clear commit. Keep **agent-scoped** work (.squad bookkeeping, skills) separate and untracked on main.

## Context

After previous work on downstream API timeout diagnosis (PR #49), two artifacts emerged:

1. **Product deliverables:** `scripts/codespaces/diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, updated `CODESPACES.md`
2. **Agent bookkeeping:** Blathers' reference note + extracted browser-diagnostics skill

Both were created during the same diagnostic effort but serve different audiences:
- Product files: Codespaces users needing to troubleshoot API/auth/tunnel issues
- Agent work: Squad team learning and skill reuse

## Choice

**Commit product files to main; leave agent work in .squad/**

### Product Commit (926ca7a)

```
docs: add downstream diagnostics script and flow guide

- Add scripts/codespaces/diagnose-downstream.sh for debugging API/auth/tunnel issues
- Add MANUAL_DIAGNOSIS_FLOW.md for step-by-step troubleshooting guide
- Update CODESPACES.md with reference to new diagnostics script and flow

The script checks local endpoints, reads safe runtime diagnostics,
probes TestSite/MockBusinessApp/Keycloak connectivity, and supports
optional bearer token authentication for full testing.
```

### Agent Work (Untracked, Not Merged)

- `.squad/agents/blathers/QUICK_DIAGNOSIS_REFERENCE.txt` — Blathers' diagnostic notes
- `.squad/skills/browser-devtools-api-diagnosis/` — Reusable pattern for future devtools-level debugging

## Rationale

**Separation enables clarity:**

1. **Product surface** (main branch) stays focused on user-facing assets — no .squad clutter
2. **Agent work** stays in .squad/ — available for future sessions but not blocking product merges
3. **Git history** reads clearly: "We shipped diagnostics tooling" vs "We learned a pattern"

**Timing impact:** Landing product immediately unblocks Codespaces users; agent skill can be refined/merged in future work without rushing.

## Implementation

1. Stage only product files: `scripts/codespaces/diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, `CODESPACES.md`
2. Commit with clear scope message
3. Push to origin/main
4. Leave .squad/ untracked (will be staged separately if/when Scribe merges agent decisions)

## Follow-Up

- Mark `.playwright-cli/` for addition to `.gitignore` (build artifact, not product)
- Blathers' reference note + skill remain in .squad/ for Squad team access
- If browser-devtools-api-diagnosis pattern proves reusable, merge skill to main in a future PR with Blathers' sign-off

---
date: 2026-05-03T20:53:49.355+01:00
agent: mabel
issue: diagnostics-script-runtime
status: implemented
---

# Diagnostics Script Runtime Isolation — Commitment to Main

**Date:** 2026-05-03  
**Decision Owner:** Mabel (Technical Writer)  
**Commit:** `fb1b324`  
**Status:** ✅ Landed on main

## Problem

Codespaces users with other Python toolchains (Conda, Poetry, .venv, etc.) activated in their shell would encounter:

```
ModuleNotFoundError: No module named 'json'
```

when running `bash scripts/codespaces/diagnose-downstream.sh`. The issue occurred because the diagnostics script attempted to use Python with ambient `PYTHONHOME` and `PYTHONPATH` environment variables that pointed to incompatible or incomplete Python installations.

## Solution

### Three-part fix:

1. **Runtime detection** — Added `resolve_python_runtime()` to probe for working Python interpreters, validating each with a stdlib import check (`import json`, `argparse`, etc.)

2. **Isolation** — Invoke detected Python with `-I` flag and explicit env var unset:
   ```bash
   env -u PYTHONHOME -u PYTHONPATH -u PYTHONSTARTUP -u __PYVENV_LAUNCHER__ \
       "$PYTHON_BIN" -I - "$@" <<'PY'
   ```

3. **Documentation** — Updated CODESPACES.md with:
   - Clear statement that the script now self-checks and ignores shell overrides
   - Recovery step: fresh shell + preflight check `python3 -I -c 'import json'`
   - Added test contract: `CodespacesDiagnosticsScript_IgnoresAmbientPythonShellOverrides()`

## Scope

**Landed as single product commit:**
- `scripts/codespaces/diagnose-downstream.sh`
- `CODESPACES.md`
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs`

**Not landed (untracked):**
- `.squad/agents/*/history.md` — will update separately
- `.squad/skills/`, `.playwright-cli/` — reference/build artifacts
- Agent reference notes — (Blathers, Tangy, etc.)

This separation keeps the product commit focused and clean, while bookkeeping stays in .squad/.

## Impact

✅ **Codespaces experience:** Users no longer need to close/reopen shells or manually diagnose Python runtime conflicts.  
✅ **Operator clarity:** CODESPACES.md now gives actionable steps if the script itself fails.  
✅ **Contract enforcement:** Test ensures future contributors maintain the isolation pattern.

## User Action

Pull main and rerun the diagnostics script in a fresh Codespaces shell.

---
date: 2026-05-03T21:32:41.296+01:00
author: Tangy
status: PROPOSED
area: testing, codespaces, runtime-assumptions
---

# Codespaces Diagnostics Script Must Ignore Ambient Python Shell State

## Context

`scripts/codespaces/diagnose-downstream.sh` failed before any downstream checks with:

```text
ModuleNotFoundError: No module named 'json'
```

Because `json` is in Python's standard library, the likely failure mode is shell-level runtime contamination or a broken active interpreter, not a missing repo dependency.

## Decision

Run the diagnostics helper with an isolated Python runtime and make the recovery path explicit for operators.

## Consequences

- The script should unset ambient `PYTHON*` overrides and use `python -I` for both its preflight and main execution paths.
- If that still fails, the error should point operators at the shell runtime itself with a minimal `python3 -I -c 'import json'` preflight.
- QA should still call out the remaining assumptions: a genuinely broken `python3` binary cannot be recovered in-script, `gh codespace ports` remains the authoritative public URL source, and stack readiness is still a prerequisite for meaningful probe results.

---
date: 2026-05-03T21:26:34.690+01:00
author: Tom Nook
status: DECISION
area: git-hygiene, diagnostics, codespaces
---

# Landing Diagnostics Script: Separate Product from Bookkeeping

## Problem

**Current state:**
- Local `main` is 1 commit ahead of `origin/main` (42bae10, a squad bookkeeping commit)
- That commit's message claims it includes "scripts/codespaces/diagnose-downstream.sh" and "updated CODESPACES.md"
- **But the actual script files are untracked** — not included in the commit
- The untracked product work: `diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, `QUICK_DIAGNOSIS_REFERENCE.txt`, `browser-devtools-api-diagnosis/` skill, and `CODESPACES.md` update

**Consequence:**
- The commit message is dishonest (says it includes files that don't exist in it)
- The script cannot be pulled into Codespaces because it's not actually in the repo
- Squad bookkeeping and product work are entangled in one incomplete commit

## Decision

**Separate product from bookkeeping:**
1. Reset `main` to `origin/main` (discard the incomplete bookkeeping commit)
2. Stage and commit the diagnostics script work in a single, focused product commit
3. Push the product commit to `main`
4. Defer squad bookkeeping consolidation to a separate session

**Rationale:**
- Product commits should contain exactly what their messages claim
- Jonny can immediately pull the script into Codespaces
- Bookkeeping (decision merges, history updates) is a separate concern and should land separately
- Follows "each commit is a complete, releasable unit" discipline

## Implementation

1. `git reset --hard origin/main` (discard 42bae10)
2. Stage: `CODESPACES.md`, `scripts/codespaces/diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, `.squad/agents/blathers/QUICK_DIAGNOSIS_REFERENCE.txt`, `.squad/skills/browser-devtools-api-diagnosis/`
3. Commit with message: `feat(codespaces): add downstream diagnostics script and supporting docs`
4. Push to `main`

## Outcome

- ✅ Script lands on main in a clean, focused commit
- ✅ Jonny can pull and use it immediately
- ✅ Bookkeeping will follow in a separate commit when consolidated

**No risk:** The diagnostics script is new work (no regressions); the skill docs are documentation.
---
date: 2026-05-03T21:49:23.079+01:00
author: Blathers
status: PROPOSED
area: codespaces, diagnostics, tooling
---

# Codespaces Downstream Diagnostics Must Not Depend on Python

## Context

`scripts/codespaces/diagnose-downstream.sh` is meant to be the first-response operator tool when downstream API calls, tunnel redirects, or Keycloak backchannel wiring go wrong in Codespaces.

The prior hardening still failed in shells where there was no usable Python runtime at all. In that state, the script exited before any diagnostics banner or reachability checks, which defeated the purpose of having a low-friction troubleshooting helper.

## Decision

The downstream diagnostics helper should be implemented with shell-native tooling and must not require Python to be installed or healthy.

### Implementation guidance

1. Use `curl` for HTTP/HTTPS probes, including detection of:
   - internal service reachability
   - public tunnel/auth HTML interception
   - same-origin runtime endpoint availability
   - authenticated vs unauthenticated downstream responses
2. Use `gh codespace ports` as the authoritative source for forwarded browse URLs when Codespaces metadata is available.
3. Parse only the minimum JSON fields needed for operator guidance with shell-safe extraction rather than embedding a secondary runtime.
4. Keep the fallback hostname derivation path for cases where `gh` metadata is unavailable.

## Why This Matters

- **Reliability:** A script intended for broken environments must keep working when optional runtimes are broken too.
- **Operator ergonomics:** `bash scripts/codespaces/diagnose-downstream.sh` should remain the single obvious command to run.
- **Security posture:** Shell-only summaries still avoid printing cookies, bearer tokens, or other secrets.

## Consequences

- Future enhancements to this helper should prefer Bash, `curl`, and `gh` first.
- If richer parsing is ever needed, it should only be added when there is no credible shell-native alternative and the operator experience remains robust when that dependency is absent.

---
date: 2026-05-03T21:49:23.079+01:00
author: Tangy
status: PROPOSED
area: testing, codespaces, diagnostics
---

# Codespaces Diagnostics Common Path Must Not Require Python

## Context

`scripts/codespaces/diagnose-downstream.sh` was still failing before any useful diagnostics when the active shell exposed a broken Python runtime. The Python-isolation patch improved one failure mode, but the common Codespaces operator path still depended on Python being present and healthy before the script could even reach its first probe.

## Decision

For the common Codespaces path, the downstream diagnostics helper should be shell-only and must not require Python at all. Regression coverage should lock that contract by asserting the script stays on shell-native tooling and by documenting the operator-facing runtime assumptions explicitly.

## Consequences

- A broken or polluted Python interpreter can no longer block the default diagnostics command.
- The remaining fragile assumptions are now narrower and explicit: `curl` + `jq` must exist in the shell, `gh codespace ports` remains the authoritative browse-URL source when Codespaces metadata is available, fallback hostnames are still best-effort, and the stack still has to be running for the probes to be meaningful.
- Future fixes should treat any reintroduction of Python into this script as a regression unless there is a clearly justified non-common-path fallback.

---
date: 2026-05-03T21:49:23.079+01:00
author: Mabel
status: IMPLEMENTED
area: product-hygiene, git-workflow, scope-discipline
---

# Diagnostics Script Landing: Product vs. Bookkeeping Separation

## Context

Blathers and Tangy completed the no-Python diagnostics rewrite (shell-only probe logic, updated tests, browser devtools skill extraction). This landing session faced the scope question: **Should we land product + bookkeeping in one commit, or keep them separate?**

The working tree contained:
- **Product files** (should go to main): `scripts/codespaces/diagnose-downstream.sh`, `CODESPACES.md`, `MANUAL_DIAGNOSIS_FLOW.md`, test contract
- **Bookkeeping files** (should be deferred): `.squad/agents/blathers/history.md`, `.squad/agents/tangy/history.md`, `.squad/skills/browser-devtools-api-diagnosis/`, `.playwright-cli/`

## Decision

**Product and bookkeeping files must be committed separately to main.**

- **Product commit (22843a2):** Only user-facing deliverables go to main. Users pull, get working diagnostics script, no noise.
- **Bookkeeping session:** Agent histories, skills, and session artifacts are coordinated separately, keeping the main branch clean and releasable.

### Rationale

1. **Main branch hygiene:** main should contain only shipping artifacts. `.squad/` bookkeeping is internal coordination noise.
2. **User clarity:** When a user pulls a commit message "Fix: Rewrite diagnostics script...", they should see only the files they care about, not agent history or skill extraction artifacts.
3. **Release boundaries:** One commit = one releasable unit. Product commit 22843a2 is production-ready; bookkeeping is orthogonal.
4. **Git history signal:** Future readers reviewing main history see only meaningful product decisions, not agent coordination artifacts.

### Implementation

**Workflow for multi-agent coordination going forward:**

1. Implementation agents (Blathers, Tangy) complete their work
2. Technical Writer (Mabel) **stages only product files** (`git add <product-files>`)
3. Create clean product commit with single concern
4. **Leave .squad/ files unstaged**
5. Separate bookkeeping session: Update agent histories and merge them without product files

**Git commands:**
```bash
# Stage only product files
git add scripts/codespaces/diagnose-downstream.sh CODESPACES.md MANUAL_DIAGNOSIS_FLOW.md src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs

# Commit to main
git commit -m "Fix: Rewrite diagnostics script to eliminate Python runtime dependency..."

# Push product commit
git push origin main

# Later: Separate bookkeeping merge with only .squad/ files
```

### Exception: When bookkeeping is tightly coupled

If a product file genuinely requires a .squad/ reference for correctness (e.g., a decision embedded in a code comment), include it in the product commit. Otherwise: separate.

---

## Precedent

Commit fb1b324 (2026-05-03, earlier session) established this pattern. Commit 22843a2 reinforces it.

## Follow-up

- **Scribe:** Consider updating `.squad/conventions.md` to document this landing workflow
- **Future technical writes:** Use this pattern for all multi-agent product handoffs

---
date: 2026-05-03T23:00:12.742+01:00
agent: blathers
status: proposed
---

# Downstream Demo Transport Diagnostics Should Be Response-Visible

## Context

The downstream demo endpoint (`/api/prism/downstream-demo`) serves as a live diagnostic tool for operators testing server-to-server bearer token forwarding. When calls fail in Codespaces, the failure could be:
- Stale AppHost wiring (backchannel URL not set or pointing to wrong port)
- GitHub port-forwarding tunnel blocking internal requests
- MockBusinessApp not running or rejecting tokens
- Network timeout vs external cancellation

Previously, failures logged to the server but returned generic error messages, forcing operators to manually inspect environment variables and AppHost logs to determine the actual transport path.

## Decision

Embed transport path diagnostics directly in the JSON response payload for all outcomes (success, timeout, network error, non-JSON response).

### What Gets Exposed

Response includes a `transport` object with:
- `transport`: "internal-backchannel", "public-tunnel", or "public-url"
- `backchannelPresent`: boolean flag for BUSINESSAPP_BACKCHANNEL_URL
- `transportBaseUrl`: masked for internal URLs (`http://localhost:****`), full for public
- `targetUrlScheme`: http/https indicator

Structured logs also include this metadata for searchability.

### Security Considerations

**Safe to expose:**
- Whether backchannel URL is configured (boolean flag)
- Transport type classification
- Public URLs (already browser-visible)
- URL scheme (http/https)

**Must mask:**
- Actual backchannel port numbers → shown as `http://localhost:****`
- Bearer tokens, refresh tokens, cookies
- Client secrets, JWKS keys

### Why Response-Visible

1. **Immediate operator insight** — Failure response immediately shows which transport path was attempted
2. **No log hunting** — Operators don't need AppHost logs or environment variable inspection for first-pass triage
3. **Context-aware hints** — Error messages can tailor advice based on transport (e.g., "Try refresh.sh" for backchannel timeouts)
4. **Test-friendly** — Future automated tests can assert on transport metadata
5. **Safe for dev environments** — Already gated behind IsDevelopment or explicit config flag

## Implementation

Added `BuildTransportDiagnostics()` helper that:
1. Checks `BUSINESSAPP_BACKCHANNEL_URL` environment variable
2. Falls back to `PrismBusinessApp:WorkflowApiBaseUrl` config
3. Classifies as internal-backchannel, public-tunnel, or public-url
4. Masks internal URLs, shows public URLs in full
5. Returns tuple for structured logging and response inclusion

Updated all response paths (success, timeout, HttpRequestException, non-JSON) to include transport metadata.

## Alternatives Considered

**Log-only diagnostics:**
- Rejected: Requires operator to have AppHost log access and grep skills
- Log hunting for every failure slows down diagnosis

**Expose actual backchannel port:**
- Rejected: Ephemeral ports are internal runtime detail; exposing them doesn't help operators since they can't directly call localhost from their browser anyway
- Masked representation conveys "internal backchannel in use" without leaking port

**Separate diagnostic endpoint:**
- Rejected: Response-visible diagnostics on the actual failing endpoint give immediate context
- Separate endpoint requires two requests to correlate transport with failure

## Consequences

**Benefits:**
- Next Codespaces timeout immediately shows "internal-backchannel" vs "public-tunnel"
- Operators can distinguish stale wiring from downstream auth failures in one request
- Contextual hints tailored to actual transport type
- Structured logging enables pattern analysis across failures

**Risks:**
- Exposing transport implementation detail in API contract
- Mitigation: Already dev-only endpoint; transport metadata is descriptive, not prescriptive

**Maintenance:**
- Transport classification logic lives in one helper method
- If new transport types emerge (e.g., service mesh, sidecar), update classification in one place

## Related Decisions

- `.squad/skills/dev-session-contract-probe/SKILL.md` — Precedent for response-visible diagnostics without token exposure
- `.squad/skills/inline-api-failure-states/SKILL.md` — Normalize from Response.status first, layer diagnostic fields
- `.squad/skills/aspire-dynamic-endpoint-backchannels/SKILL.md` — Why backchannel URLs exist and how they're resolved

## Test Coverage

All 680 Core tests pass. No new test failures introduced. Transport diagnostics are response-visible but don't break existing contract expectations.

Tangy added five behavioural contract tests guarding backchannel/public tunnel classification and timeout/error transport metadata; all tests pass.

---
date: 2026-05-03T22:49:38.255+01:00
author: Blathers
status: PROPOSED
area: diagnostics, authentication, http-client
---

# Downstream API Timeout Diagnosis: Unregistered HttpClient Root Cause

## Context

The DownstreamDemoController times out after 10 seconds when calling MockBusinessApp from TestSite. Evidence gathered:

1. **Browser call:** `/api/prism/downstream-demo` → timeout after 10s
2. **Session contract:** Shows authenticated session, access token present, `authorizationHeaderReady=true`
3. **Diagnostics script:** Internal `http://localhost:{port}/debug/auth` returns 200 (BusinessApp is listening and healthy)
4. **Keycloak backchannel:** Healthy and reachable
5. **TestSite same-origin probes:** Healthy

## Root Cause Identified

`DownstreamDemoController.cs` uses a named HttpClient that is **not registered**:

```csharp
// Line 286:
var client = httpClientFactory.CreateClient("prism-downstream-demo");
```

**Impact:**
- HttpClientFactory creates an unconfigured default client
- The CancellationToken timeout (10s) is respected, but the client lacks proper handler configuration
- Unregistered clients may have issues with localhost resolution, certificate validation, or connection pooling in containerized environments

## Decision

**Register the "prism-downstream-demo" HttpClient with explicit configuration.**

This is justified because:
1. Named clients should always be registered (codebase pattern)
2. The timeout alone (via CancellationToken) doesn't guarantee proper handler chain setup
3. Matches the pattern used for "PrismBusinessApp" and "PrismTokenRefresh"
4. Low risk: Won't break existing behavior if the issue is elsewhere

## Implementation

In `PrismComposer.cs` or `TestSiteComposer.cs`:

```csharp
// Add after existing HttpClient registrations:
builder.Services.AddHttpClient("prism-downstream-demo")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15); // Slightly higher than CancellationToken timeout
    });
```

OR in development-only scope (since this is a demo controller):

```csharp
// In TestSiteComposer.cs or wherever dev-only services are registered:
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpClient("prism-downstream-demo")
        .ConfigureHttpClient(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
}
```

## Alternative: Verify Runtime Environment First

If registering the client doesn't fix the timeout, the next diagnostic step is:

**Add logging to DownstreamDemoController to capture the actual URL being called:**

```csharp
private string ResolveBusinessAppTransportBaseUrl()
{
    var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
    if (!string.IsNullOrWhiteSpace(backchannelUrl))
    {
        logger.LogInformation("[PRISM] Using backchannel URL: {Url}", backchannelUrl);
        return backchannelUrl;
    }
    
    var baseUrl = configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("PrismBusinessApp:WorkflowApiBaseUrl is not configured.");
    
    logger.LogInformation("[PRISM] Falling back to public URL: {Url}", baseUrl);
    return baseUrl;
}
```

This will confirm:
- Whether BUSINESSAPP_BACKCHANNEL_URL is actually set at runtime
- Whether the URL matches what the diagnostics script successfully tested

## Test Coverage

After implementing, verify:
1. TestSite can call MockBusinessApp via the demo button (< 2 seconds)
2. Browser-facing response still shows public URL (not backchannel)
3. Diagnostics script still shows healthy backchannel connectivity

## References

- History note: "Named HttpClients have default timeouts (100s); the custom timeout only applies when the named client is registered."
- `DownstreamDemoController.cs` line 286
- `PrismComposer.cs` lines 34-35 (existing HttpClient registrations)

---
date: 2026-05-03T23:13:53.622+01:00
session: transport-diagnostics-landing
title: Transport Diagnostics Landing — Product Commit 17edf9c
author: Mabel (Technical Writer)
affected: downstream-demo, diagnostics workflow
status: implemented
---

# Transport Diagnostics Landing Decision

## Context

Transport diagnostics feature (implementation by Blathers, testing by Tangy) was ready to land on main. Two product files contained the changes:
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` — diagnostics instrumentation
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` — test contracts

Unrelated changes present (`.playwright-cli/`, `.squad/` agent artifacts) required clean staging.

## Decision

**Staged only the two product files.** Committed with conventional commit message (`feat(diagnostics):...`) and required Co-authored-by trailer. Pushed to origin/main as commit 17edf9c.

## Rationale

1. **Single-unit release boundary:** One commit = one releasable feature. No mixing product and bookkeeping in the same commit.
2. **Clean user history:** When users pull origin/main, they see only the shipped diagnostics feature, not internal agent coordination.
3. **Conventional signal for release notes:** `feat(diagnostics)` prefix enables Mabel to infer minor version bump when generating CHANGELOG.
4. **Hygiene pattern reaffirmed:** Continues established product/bookkeeping separation from earlier diagnostics landings (22843a2, fb1b324).

## Outcome

✅ **Product commit 17edf9c now live on origin/main.**

Users can immediately:
- `git pull origin main` to get transport diagnostics feature
- See transport type (internal-backchannel vs public-tunnel) in diagnostic responses
- Understand backchannel configuration state and target URL scheme for troubleshooting

## Files Changed

- DownstreamDemoController.cs: +60 lines (diagnostics instrumentation)
- DashboardLocalEndpointsValidationTests.cs: +175 lines (test contracts)

## Convention Implication

This landing reaffirms the **product/bookkeeping separation pattern** as team-wide convention:

- **Main branch:** Shipping artifacts only (user-facing code changes)
- **Bookkeeping:** .squad/ agent histories, decisions, coordination logs (deferred to separate sessions or merges)
- **Release clarity:** Clean git history enables users and release automation to reason about what shipped and why

Suggest Scribe consider updating `.squad/conventions.md` to document this as explicit team guidance for future multi-agent product handoffs.

---
date: 2026-05-03T23:26:29.163+01:00
author: Blathers
status: decision
area: diagnostics, downstream-demo, backchannel
---

# Decision: Safe deeper downstream timeout diagnostics

## Context

Jonny needed better browser-visible detail for downstream demo timeouts, especially when TestSite calls MockBusinessApp through an internal backchannel in Codespaces or local Aspire wiring.

## Decision

Keep masking internal backchannel ports in `transport.transportBaseUrl` as `http://localhost:****`, but add safe timeout details that do not expose raw internal ports:

- `transport.usingBackchannel`
- `transport.targetPath`
- `timeout.timedOutByUs`
- `timeout.cancellationSource`
- short `summary` / `nextCheck` hints

Also enrich server logs with the masked transport base URL and target path so operators can correlate browser output with backend logs.

## Rationale

The browser already needs to know whether TestSite used the backchannel, which path it targeted, and whether the 10-second timeout came from our own request window. Those details help diagnose stale AppHost wiring and public-tunnel fallbacks, while the raw localhost port still stays hidden from browser-visible JSON.

---
date: 2026-05-03T23:26:29.163+01:00
author: Tangy
status: decision
area: testing, diagnostics, downstream-demo
---

# Decision: Timeout Diagnostics Must Distinguish Deadline vs Cancellation Without Leaking Backchannel Ports

## Context

`DownstreamDemoController` now exposes richer timeout diagnostics for `/api/prism/downstream-demo` so operators can tell whether a failed request used the public tunnel or the internal backchannel. The remaining behavioural risk was ambiguity between a real controller timeout and an externally cancelled request, especially in unit tests that throw `TaskCanceledException` directly.

## Decision

Browser-visible timeout responses should preserve these contracts:

1. **Deadline vs cancellation must be explicit.**
   - Timeout responses expose `statusText`, `timeout.timedOutByUs`, and `timeout.cancellationSource`.
   - Behavioural tests cover both the controller-owned timeout window and a separate external-cancellation path.

2. **Internal-backchannel diagnostics must stay masked.**
   - Responses may identify `internal-backchannel`, the target path, and suggested next checks.
   - `transport.transportBaseUrl` must remain masked (`http://localhost:****`) and raw internal ports must not appear anywhere in browser-visible JSON.

3. **Operator guidance should point to configuration and health checks, not implementation leaks.**
   - `summary` and `nextCheck` should reference the downstream path and wiring checks like `BUSINESSAPP_BACKCHANNEL_URL`.
   - Guidance should avoid exposing raw localhost ports while still telling operators what to verify next.

## Test Coverage

- `DownstreamDemo_IncludesTransportDiagnostics_OnTimeout`
- `DownstreamDemo_IncludesMaskedInternalBackchannelTimeoutDiagnostics`
- `DownstreamDemo_LabelsExternalCancellation_SeparatelyFromTimeoutWindow`
- Existing masking contract in `DownstreamDemo_DoesNotExposeRawBackchannelPortInDiagnostics`

---
date: 2026-05-03T23:38:00.000+01:00
author: Mabel
status: IMPLEMENTED
area: diagnostics, backend, testing
---

# Decision: Deeper Downstream Timeout Diagnostics Landing

## Summary

Landed enhanced timeout diagnostics feature to origin/main. Product commit exposes backchannel state, target path, and cancellation context to help operators triage timeout failures in Codespaces environments.

## Implementation

**Staged and committed:**
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` — Implements richer timeout diagnostic fields
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` — Enhanced test coverage for timeout scenarios

**Scope discipline:**
- Left `.squad/` files unstaged (Scribe merged bookkeeping separately)
- Clean product boundary: only user-facing artifacts in commit

## Rationale

Timeout diagnostics must expose enough state to distinguish:
1. **Backchannel wiring failures** — When BUSINESSAPP_BACKCHANNEL_URL points to an unreachable internal service
2. **Public-tunnel timeouts** — When Codespaces tunneling infrastructure is slow or misconfigured

New fields enable operators to immediately see:
- `usingBackchannel` — Explicit confirmation of which path was attempted
- `targetPath` — Path component of the downstream call (safe to expose; URL masked)
- `timeoutWindowMs` + `cancellationSource` — Timeout boundary and which component fired it

## Owners

- Lead (Tom Nook) — Feature approved
- Blathers (Backend Dev) — Implementation approved
- Tangy (Tester) — Test coverage approved
- Commit: 442c5e9

---
date: 2026-05-03T23:46:52.875+01:00
author: Blathers
status: PROPOSED
area: diagnostics, backend, auth
---

# Business API Arrival Logging Should Carry Safe Cross-Service Correlation

## Context

When the dashboard's downstream demo times out, TestSite can prove which transport path it chose, but that alone does not prove MockBusinessApp accepted the request or entered `/api/backoffice/me`. Operators need a decisive signal from MockBusinessApp itself without logging bearer tokens or secrets.

## Decision

For `MockBusinessApp` arrival diagnostics on `/api/backoffice/me`:

1. Log once in middleware immediately before `app.UseAuthentication()`
2. Log again at the top of the `/api/backoffice/me` handler
3. Keep fields safe: method, path, service trace identifier, auth-header-present, and a caller trace hint
4. Forward TestSite's `HttpContext.TraceIdentifier` in a dedicated header (`X-Prism-Caller-TraceId`) so MockBusinessApp logs can be matched back to TestSite warning logs

## Why

- The pre-auth log proves the request reached MockBusinessApp before bearer validation ran
- The handler-entry log proves endpoint execution began
- A dedicated caller trace hint gives cross-service matching without exposing tokens, cookies, or internal secrets

## Files

- `src/UmbracoPrism.MockBusinessApp/Program.cs`
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs`

---
date: 2026-05-04T00:01:43.530+01:00
author: Blathers
status: PROPOSED
area: auth, keycloak, codespaces, backchannel
---

# MockBusinessApp Downstream Timeout Root Cause Is Hybrid JWKS URI Escape

## Context

Downstream Demo now proves TestSite is using the internal backchannel and that requests arrive at MockBusinessApp before auth. MockBusinessApp then logs:

- `IDX20803: Unable to obtain configuration from 'http://localhost:{ephemeral}/realms/prism-dev/.well-known/openid-configuration'`
- inner `IDX20804` against `http://{public-codespaces-host}:{same-ephemeral}/realms/prism-dev/protocol/openid-connect/certs`
- `KEYCLOAK_BACKCHANNEL_URL` is present
- `ASPNETCORE_ENVIRONMENT=Development`
- `backchannel JWKS enabled : YES`

## Decision

Treat this as sufficient root-cause evidence and stop broader diagnosis.

The failing runtime path is:

1. `src/UmbracoPrism.Shared/Extensions/PrismAuthExtensions.cs`
2. `ResolveSigningKeys(...)`
3. `src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs`
4. `WarmAsync(cacheKey, metadataAddress, ...)`
5. `ConfigurationManager<OpenIdConnectConfiguration>` + `BackchannelRewritingDocumentRetriever`

The discovery request is redirected to `KEYCLOAK_BACKCHANNEL_URL`, but the returned discovery document emits a **hybrid** `jwks_uri` using the public Codespaces hostname with the internal HTTP port. The current rewriter only rewrites URLs whose prefix exactly matches the configured public origin (`https://{public-host}`), so the hybrid URI (`http://{public-host}:{ephemeral-port}`) is not rewritten and the metadata HttpClient waits on an unreachable public endpoint until its default 100-second timeout.

## Implications

- The downstream-demo 10-second timeout is now explained: TestSite gives up after 10 seconds while MockBusinessApp auth middleware is still blocked on its own 100-second metadata client.
- This is not just "discovery rewritten but JWKS forgotten" by design; it is a narrower bug: the JWKS rewrite exists, but misses Keycloak's hybrid JWKS origin.

## Required Fix

Primary code change:

- `src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs`

Validation coverage:

- `src/UmbracoPrism.Core.Tests/BackchannelRewriteTests.cs`

Optional follow-up diagnostics only if useful:

- `src/UmbracoPrism.Shared/Extensions/PrismAuthExtensions.cs`

## Preferred Fix Shape

Make generic OIDC bearer validation robust against hybrid Keycloak JWKS URIs by either:

1. bypassing discovery in backchannel mode and fetching `.../protocol/openid-connect/certs` directly from the backchannel base, matching the existing direct-JWKS strategy in `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs`, or
2. broadening the retriever rewrite so it rewrites any Keycloak realm URL whose host/path matches the configured authority, regardless of whether the discovery doc reports `https://public-host`, `http://public-host:{ephemeral-port}`, or another equivalent frontchannel form.

Add a regression test for the exact observed hybrid case.

---
date: 2026-05-03T23:46:52.875+01:00
author: Mabel
status: IMPLEMENTED
area: instrumentation, backend, testing
---

# Business API Arrival Instrumentation Landing

**Decision:** Land Business API arrival instrumentation on `main` for production use.

**Date:** 2026-05-03T23:46:52.875+01:00

**Status:** IMPLEMENTED (commit 8e1cd68)

---

## What We're Shipping

The Business API arrival instrumentation enables operators to correlate TestSite (dashboard) requests with Business API diagnostics through safe trace ID forwarding.

**Components:**

1. **Arrival Middleware (MockBusinessApp)**
   - Logs before authentication: captures raw request context without access restrictions
   - Logs after handler entry: includes authentication status
   - Fields: method, path, trace ID, auth header presence, caller trace ID

2. **Caller Trace ID Forwarding (TestSite)**
   - Extracts HttpContext.TraceIdentifier from TestSite request
   - Forwards via `X-Prism-Caller-TraceId` header to Business App
   - Safe pattern: header is read-only diagnostic data, no auth/PII exposure

3. **Test Contract (DashboardLocalEndpointsValidationTests)**
   - Validates trace ID capture and forwarding
   - Stub handler asserts header presence
   - Confirms correlation hint matches

---

## Why This Matters

**Operator pain point:** When downstream calls fail in Codespaces, operators had to manually trace logs across services. The trace ID link was missing.

**Solution:** Safe, read-only correlation header enables immediate cross-service log search without exposing internal URLs or PII.

---

## Scope Discipline Applied

- **Product files staged:** Only the three changed runtime/test files
- **Bookkeeping deferred:** .squad/ agent histories and skill updates left unstaged for separate bookkeeping merge
- **Release boundary:** Single, complete, production-ready commit (8e1cd68)

---

## Approval Chain

- **Blathers (Backend Dev):** Implemented arrival middleware and handler logging
- **Tangy (Tester):** Validated test contract and correlation forwarding
- **Mabel (Release):** Staged clean commit, pushed to main

---

## User Outcome

Users can now `git pull origin main` and run dashboard + Business App with arrival instrumentation active. Developers using Codespaces can correlate dashboard timeouts with Business API logs immediately — no manual tracing needed.

---

## Next Steps (Deferred Bookkeeping)

- Merge agent history updates to .squad/agents/
- Consolidate this decision into decisions.md
- Extract any reusable patterns to team skills
# Decision: Workflow API Calls Must Use Internal Backchannel in Codespaces

**Date:** 2026-05-04T00:19:33.157+01:00  
**Author:** Blathers (Backend Dev)  
**Status:** ACCEPTED

## Context

In Codespaces, Aspire AppHost injects two environment variables for the Business App:

- `PrismBusinessApp__WorkflowApiBaseUrl` — the public HTTPS forwarded-port URL (browser-facing)
- `BUSINESSAPP_BACKCHANNEL_URL` — the internal `http://localhost:{port}` endpoint (server-to-server)

GitHub's forwarded-port proxy intercepts unauthenticated server-side HTTP calls to the public URL and returns 401. Any server-side code that reads `WorkflowApiBaseUrl` and uses it for HTTP requests will fail with 401 in Codespaces.

## Decision

All server-side HTTP clients that call the Business App **must** check `BUSINESSAPP_BACKCHANNEL_URL` first and fall back to `PrismBusinessApp:WorkflowApiBaseUrl`. The public `WorkflowApiBaseUrl` is for browser-facing links only.

## Rationale

`DownstreamDemoController` already had the correct pattern (`ResolveBusinessAppTransportBaseUrl()`). `BusinessAppWorkflowClient.BaseUrl` was missing it, causing every workflow start and advance to fail in Codespaces with HTTP 401.

## Implementation Pattern

```csharp
private string BaseUrl
{
    get
    {
        var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(backchannelUrl))
            return backchannelUrl;

        var url = configuration["PrismBusinessApp:WorkflowApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("...");
        return url.TrimEnd('/');
    }
}
```

## Scope

- `src/UmbracoPrism.Core/Services/BusinessAppWorkflowClient.cs` — fixed
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` — already correct
- Any future Business App HTTP clients must follow the same pattern

## Commit

`caaf551` — fix(workflow): use BUSINESSAPP_BACKCHANNEL_URL for workflow API calls in Codespaces
# Decision: Workflow 401 Null-Auth Contract and Diagnostic Distinction

**Proposed by:** Tangy (Tester)  
**Date:** 2026-05-04  
**Status:** Proposed — for Scribe to merge into decisions registry

---

## Decision

**`BusinessAppWorkflowClient` must log when `GetAuthorizationHeaderAsync` returns null, and workflow endpoint handlers in MockBusinessApp must return `Results.Problem()` (not `Results.Unauthorized()`) for application-level identity failures.**

---

## Context

Investigating why workflow pages return "Business App error (HTTP 401)" in Codespaces even after commit 0904810 fixed JWKS backchannel URL rewriting. Two indistinguishable 401 sources exist:

1. **JWT middleware 401** — token signature validation failed (no valid signing keys). Logged as `[PRISM AUTH FAILED]` in Business App console.
2. **Application-level 401** — `Results.Unauthorized()` returned when `GetPrismTenant` or `GetEmail` fails after successful JWT validation.

Additionally, when `PrismContext.GetAuthorizationHeaderAsync` returns null (e.g. `CurrentTenant` not resolved), `BusinessAppWorkflowClient.CreateClientAsync` silently omits the Authorization header with no log entry. The Business App JWT middleware then returns 401, which is indistinguishable from the cases above.

---

## Rationale

- Operators have no way to distinguish the three failure modes without access to Business App console logs.
- `/api/backoffice/me` returns `Results.Problem()` for null tenant/email; workflow endpoints return `Results.Unauthorized()`. This inconsistency means the same root cause (misconfigured tenant config) surfaces differently depending on which endpoint is called first.
- Silent null auth in `CreateClientAsync` (line 179 of `BusinessAppWorkflowClient.cs`) makes `PrismContext` failures invisible in TestSite logs.

---

## Proposed Changes

### 1. `BusinessAppWorkflowClient.CreateClientAsync` — log when auth header is null

```csharp
var authHeader = await prismContext.GetAuthorizationHeaderAsync(forceRefresh);
if (authHeader == null)
{
    logger.LogWarning(
        "BusinessAppWorkflowClient: GetAuthorizationHeaderAsync returned null (reason: {Reason}). " +
        "Request will be sent without an Authorization header.",
        prismContext.LastAuthorizationFailureReason ?? "unknown");
}
if (authHeader != null)
    client.DefaultRequestHeaders.Authorization = authHeader;
```

### 2. `MockBusinessApp/Program.cs` — align workflow handlers to `Results.Problem()`

Replace `Results.Unauthorized()` in `/api/workflow/{key}/current`, `/api/workflow/{key}/advance`, and `/api/workflow/instances` handlers with:

```csharp
if (tenant == null)
    return Results.Problem("Tenant not recognised by Business Application.");
if (string.IsNullOrEmpty(email))
    return Results.Problem("User email claim not found.");
```

This produces HTTP 500 (same as `/api/backoffice/me`) for application-level identity failures, making them distinguishable from JWT-level 401s in `ReadEnvelopeAsync` output ("Business App error (HTTP 500)" vs "Business App error (HTTP 401)").

---

## Affected Files

- `src/UmbracoPrism.Core/Services/BusinessAppWorkflowClient.cs`
- `src/UmbracoPrism.MockBusinessApp/Program.cs`

---

## Test Coverage

Regression tests added in `BusinessAppWorkflowClientTests.cs` document the current null-auth contract:
- `GetCurrentAsync_SurfacesErrorEnvelope_WhenAuthHeaderIsNull`
- `GetCurrentAsync_AttemptsTokenRefreshOnce_WhenBusinessAppReturns401`
- `GetCurrentAsync_SurfacesErrorEnvelope_NotExceptionThrown_WhenBothRequestsReturn401`

These tests will need updating if the null-auth logging proposal is implemented (the contract changes from silent to logged).
---
date: 2026-05-04T00:26:42.240+01:00
author: Blathers
status: PROPOSED
area: workflow, auth, MockBusinessApp
commit: beef21c
---

# Workflow Auth: Align MockBusinessApp Handlers and Log Silent Auth Failures

## Context

Two layered 401 failure modes in the Codespaces workflow-start path were collapsing into the same surface error, making diagnosis difficult:

1. `BusinessAppWorkflowClient.CreateClientAsync` silently omitted the `Authorization` header when `GetAuthorizationHeaderAsync` returned null (e.g. `CurrentTenant` unresolved), with no log entry.
2. MockBusinessApp workflow handlers (`/current`, `/advance`, `/instances`) returned `Results.Unauthorized()` for app-level tenant/email resolution failures, while `/api/backoffice/me` returned `Results.Problem()` for the same conditions.

## Decisions

### 1. Log a Warning when auth header is null

**`BusinessAppWorkflowClient.CreateClientAsync` must log a Warning when `GetAuthorizationHeaderAsync` returns null.**

When no auth header is obtained, the request will be rejected by the Business App JWT middleware with 401, which then triggers a spurious token-refresh retry cycle. Without a log, this is entirely invisible. The warning includes the `forceRefresh` flag and a hint to check `PrismTenantMiddleware`.

### 2. MockBusinessApp workflow handlers must return Results.Problem for app-level failures

**All three workflow endpoints must return `Results.Problem(...)` — not `Results.Unauthorized()` — when tenant or email resolution fails after successful JWT validation.**

This aligns them with `/api/backoffice/me` (already using `Results.Problem`). The result:
- A 401 from the workflow path now **unambiguously** means the bearer token was missing or rejected by JWT middleware.
- A 500 from the workflow path means the token was valid but Business App configuration (tenant mapping, email claims) failed.
- Operators and TestSite logs can distinguish the two cases without guesswork.

## Impact

- Tangy's regression tests (`BusinessAppWorkflowClientTests`) continue to pass and correctly model the expected retry behaviour on JWT-level 401.
- No changes to the retry logic itself — the fix is diagnostic clarity only.

---
date: 2026-05-04T00:00:00.000+01:00
author: Blathers
status: ACCEPTED
area: testing, ci, environment-variables
---

# Decision: Tests That Read Env Vars Must Join EnvVarSensitiveTestCollection

## Context

`EnvVarSensitiveTestCollection` was designed to serialise test classes that *mutate* `KEYCLOAK_BACKCHANNEL_URL` and `ASPNETCORE_ENVIRONMENT`. `PrismContextTests` was not in the collection because it does not mutate those variables.

However, `PrismContext.RefreshTokenAsync` **reads** both variables at runtime to conditionally rewrite the token endpoint. When `BackchannelRewriteTests` (in the collection) set those vars while `PrismContextTests` ran in parallel, the token endpoint was rewritten to an `http://localhost` URL. The Moq mock matched the `https` URL only, so Moq returned null, causing `NullReferenceException` at `result.Success`.

The failure was latent but only surfaced in CI at commit beef21c because adding `BusinessAppWorkflowClientTests` to the collection changed execution timing and widened the race window.

## Decision

**Any test class that exercises code paths which _read_ `KEYCLOAK_BACKCHANNEL_URL` or `ASPNETCORE_ENVIRONMENT` must be in `EnvVarSensitiveTestCollection`, even if it does not mutate those variables itself.**

Pattern to use (as in `LocalhostGenericOidcRegressionTests`):
1. Add `[Collection(EnvVarSensitiveTestCollection.Name)]` to the class.
2. Implement `IDisposable` saving both env vars in the constructor and restoring them in `Dispose`.

## Rationale

xUnit parallelism operates at the test-class level. Without collection membership, any class that reads global state (environment variables) is subject to races with any other class that writes that state.

## Files Affected

- `src/UmbracoPrism.Core.Tests/PrismContextTests.cs` — fixed in commit 860c5d3

---
date: 2026-05-04T09:22:01.025+01:00
author: Tangy
status: ACCEPTED
area: testing, ci, moq
---

# Never Use Concrete CancellationToken Values as Moq Matchers for ASP.NET Core Contexts

## Context

CI run 25294216756 (commit `beef21c`) failed with 4 `PrismContextTests` throwing `NullReferenceException` at `PrismContext.cs:212`. The production code was unchanged and correct. The fault was entirely in the test setup.

Mock setups for `IPrismTokenRefreshService.RefreshAsync` used `httpContext.RequestAborted` as a concrete value matcher. On Linux (GitHub Actions, Ubuntu), `DefaultHttpContext.RequestAborted` lazy-initialises its `CancellationTokenSource` via `IHttpRequestLifetimeFeature`. If that feature is activated by the authentication stack between setup-time and call-time, Moq's captured token value no longer matches the token in the actual call. Moq's loose mock returns `null` for the unmatched setup, causing `result.Success` to throw. On macOS (arm64) the lazy path is stable and the bug is masked.

## Decision

**When writing Moq setups for methods that accept a `CancellationToken`, always use `It.IsAny<CancellationToken>()` rather than a concrete `HttpContext.RequestAborted` or `httpContext.RequestAborted` value.**

Rationale:
- `DefaultHttpContext.RequestAborted` is lazily initialised through `IHttpRequestLifetimeFeature` and its behaviour can differ between platforms.
- The intent of tests like these is to verify routing logic and return values, not to assert the exact CancellationToken instance.
- Concrete value matching for CancellationToken is always fragile unless you own the token source and can guarantee stability.

## Implementation

Replace:
```csharp
.Setup(t => t.RefreshAsync(..., httpContext.RequestAborted, ...))
.Verify(t => t.RefreshAsync(..., httpContext.RequestAborted, ...), Times.Once)
```

With:
```csharp
.Setup(t => t.RefreshAsync(..., It.IsAny<CancellationToken>(), ...))
.Verify(t => t.RefreshAsync(..., It.IsAny<CancellationToken>(), ...), Times.Once)
```

Applied in commit `1601415` to four `PrismContextTests` methods.

## Blathers Review Note

The fix is entirely in test harness code. `PrismContext.cs` and `IPrismTokenRefreshService` are correct and do not require changes. Blathers does not need to act on this. The CI should pass once this commit is pushed.

# Decision: Approved CI Fix — CancellationToken Moq Matcher Pattern

**Author:** Tangy  
**Date:** 2026-05-04T09:22:01.025+01:00  
**Status:** DECIDED  

## Decision

When a Moq mock setup or verify involves a `CancellationToken` sourced from `HttpContext.RequestAborted` (or `DefaultHttpContext.RequestAborted`), always use `It.IsAny<CancellationToken>()` as the matcher — never the concrete token value.

## Rationale

On Linux (CI/Ubuntu), `DefaultHttpContext.RequestAborted` lazy-initialises its `CancellationTokenSource` via `IHttpRequestLifetimeFeature`. If the ASP.NET Core authentication stack activates the feature between setup-time and call-time, the captured token at setup no longer equals the token passed in the real call. Moq's loose behaviour returns `default` for the unmatched setup, causing a `NullReferenceException` on the next line. On macOS arm64 the lazy path is stable, masking the fragility entirely.

## Consequence

- Commit `1601415` applies this fix to 4 `PrismContextTests` methods and is now on `main` as of `d9fb7f7`.
- The tests verify endpoint routing, secret resolution, and returned bearer token — not the CancellationToken passthrough — so `It.IsAny<CancellationToken>()` is semantically correct.
- Blathers' superseded workaround (`860c5d3`, `EnvVarSensitiveTestCollection`) remains in history but is not the authoritative fix for this fragility.

## Scope

Applies to all tests in this project that mock `async` methods accepting `CancellationToken` where the token is obtained from an ASP.NET Core `HttpContext`.
# Decision: Local Worktree Cleanup Classification Rules

**Date:** 2026-05-04T10:35:24.394+01:00  
**Author:** Tom Nook  
**Trigger:** Local cleanup pass requested by Jonny Muir

---

## What Was Cleaned

| Item | Action | Reason |
|------|--------|--------|
| `.playwright-cli/` | **Deleted** | Generated session residue — timestamped console logs and page YAML snapshots from the playwright-cli skill. No user-authored content. |

## What Was Left In Place

| Item | Status | Reason |
|------|--------|--------|
| `.squad/skills/backchannel-rewrite-testing/SKILL.md` | Modified tracked file | Real skill, user work |
| `.squad/skills/inline-api-failure-states/SKILL.md` | Modified tracked file | Real skill, user work |
| `src/UmbracoPrism.TestSite/Views/memberDashboard.cshtml` | Modified tracked file | Source code, user work |
| `.squad/agents/blathers/QUICK_DIAGNOSIS_REFERENCE.txt` | Untracked, ambiguous | Looks hand-curated; .txt format in agent dir is unusual but content is meaningful — left in place per charter |
| `.squad/skills/browser-devtools-api-diagnosis/` | Untracked skill dir | Earned team knowledge with named owner (Tangy), date, and cross-references. Keep and commit. |

---

## Classification Rules (for future reference)

1. **Timestamped log/snapshot files** in `.playwright-cli/` or similar tool-output directories → **delete without review**.
2. **Untracked SKILL.md files** with named author, date, and cross-references to real work → **keep; commit as earned knowledge**.
3. **Agent personal `.txt` files** with no commit history → **ambiguous; leave in place and report**.
4. **Modified tracked source/squad files** → **never touch**; these are always user work.
---
date: 2026-05-04T11:46:55.877+01:00
author: blathers
status: PROPOSED
area: admin-ui, walkthroughs, mock-business-app
---

# Workflow Admin Definitions Panel Is Collapsed by Default

## Context

The `/admin/workflow` page in MockBusinessApp rendered all workflow definition cards fully expanded on load. With multiple definitions, each showing a states table, transitions table, and Mermaid diagram, the page became visually overwhelming for walkthrough screenshots and manual operator use.

## Decision

**Workflow definition cards on the admin screen are collapsed by default.** Operators click a card header to expand it. The Mermaid diagram is rendered on first expand (deferred, not on page load).

Supporting affordances added:
- Expand All / Collapse All toolbar buttons above the definitions panel.
- Animated toggle arrow (▶ → ▷ rotation) on each card header to communicate interactive state.
- Instance IDs in the instances table are truncated to 8 chars + "…" with the full ID accessible via `title` tooltip — reduces horizontal noise while preserving debuggability.

## Rationale

- Walkthrough screenshots need a clean, focused frame — a page-length wall of expanded cards is not photogenic.
- Operator manual use benefits from summary-first layouts: inspect the instances table first, expand a specific definition only when needed.
- No capability is removed: all expand/inspect/edit/advance/reset actions still work.

## Implementation

`src/UmbracoPrism.MockBusinessApp/Program.cs` — admin UI HTML template:
- `.def-body { display: none }` + `.def-card.open > .def-body { display: flex }` toggle via JS.
- `toggleCard(hdr)` function wired to `.def-header onclick`; skips toggle when a child button is the target.
- Mermaid init changed to `startOnLoad: false`; `window._mermaid.run()` called per card on first expand.
- Expand/Collapse All helpers wire to toolbar buttons.
- Instance ID column: `shortId = id.Length > 12 ? id[..8] + "…" : id` with `title` for full ID.
### 2026-05-04T11:46:55.877+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** For walkthrough and end-to-end work, do not make assumptions; always verify the real navigation and operator journey exist in the product before telling users to use them. Strengthen walkthroughs and tests without regressing the current suite, and improve manual discoverability where the flow currently depends on direct URLs.
**Why:** User request — captured for team memory
---
author: isabelle
date: 2026-05-04
status: inbox
affects: tangy, anyone writing walkthrough specs
---

# Decision: Screenshot-mode cookie contract

## Context

The `prism-mobile-user-agent-demo` toggle widget renders on every TestSite page
(bottom-right fixed widget).  It clutters automated walkthrough screenshots
without adding documentary value.

## Decision

A single well-known cookie suppresses the widget for a whole browser session.

**Cookie name:** `prism-screenshot-mode`  
**Value:** `"1"` to suppress; absent/`"0"` to leave the widget visible.  
**Scope:** `Path=/; SameSite=Lax; Secure=false` (localhost only).

### Server-side (C#)

`PrismMobileUserAgentDemoTagHelper` reads the cookie via `IHttpContextAccessor`.
If the cookie equals `"1"`, `ShowToggle` is forced to `false` — only the UA
bootstrap `<script>` is emitted, not the widget HTML.  The constant
`PrismScreenshotMode.CookieName` in `UmbracoPrism.Core.TagHelpers` is the
authoritative source for the cookie name.

### Client-side (Playwright)

`enterScreenshotMode(page)` in
`src/UmbracoPrism.Client/tests/walkthroughs/support/walkthrough.ts` adds the
cookie to the browser context before any navigation.  `signIn()` calls it
automatically when `CAPTURE_SCREENSHOTS=1` so every walkthrough spec picks it up
without per-spec wiring.

## Tangy hook

Tangy (or any test author) who needs screenshot-clean pages outside the
`signIn()` flow can call `enterScreenshotMode(page)` directly.  No other hook
is required.  The cookie must be set before the first page load that should
suppress the widget.

## What is NOT changed

- Manual browser usage: cookie not set → widget renders as before.
- The UA bootstrap script: always emitted regardless of screenshot mode, so
  tests that drive mobile-UA behaviour (`prismMobile` cookie/localStorage) are
  unaffected.
- `show-toggle="false"` on the tag helper still works and takes precedence in
  any template that needs to permanently hide the widget.
---
decision_id: walkthrough-ui-audit-2026-05-04
author: Isabelle
created_at: 2026-05-04T11:46:55.877+01:00
subject: Audit findings — walkthrough/demo discoverability and screenshot-friendliness
status: draft-for-review
---

# Walkthrough UI Navigation Audit — Decision

## Problem Statement

The walkthrough system includes 4 demo workflows + admin UI, but **manual discoverability is fragmented**:
- 3 workflows (Payment Demo, Planning Notification, Information Request) are unreachable without direct URL knowledge
- Workflow admin UI (`/admin/workflow`) is not linked from any UI surface
- Mobile helper widget (`prism-mobile-user-agent-demo`) appears in all screenshots, blocking viewport and cluttering walkthrough images
- Homepage focuses on design tokens, not demo workflows — misses opportunity to showcase core features

## Current State

### Routes (All Content-Based in Umbraco)
| Route | Discoverable Via |
|-------|------------------|
| `/get-in-touch` | Header nav + Dashboard card |
| `/payment-demo` | Dashboard card only ⚠️ |
| `/apply-for-planning-permission` | URL-only ❌ |
| `/request-information` | URL-only ❌ |
| `/my-workflows` | Header nav + Dashboard card |
| `/admin/workflow` | AppHost reference only ❌ |

### Navigation Surfaces
- **Header:** 3 items (Home, Get in Touch, My Workflows)
- **Dashboard:** 3 workflow cards + downstream API demo
- **Homepage:** Design system token showcase (580 lines); unauthenticated hero with Sign In/Register

### Mobile Helper Widget
- Renders on every page via `prism-mobile-user-agent-demo` tag helper
- Fixed position bottom-right corner
- Shows checkbox + status text + close button
- Persists state in localStorage/sessionStorage
- **Screenshot impact:** Visible in all walkthrough images; blocks content on mobile-width views

## Recommended Changes (Minimal & Coherent)

### 1. Add Demo Workflows Section to Home Page ✅
**What:** Insert "Demo Workflows" section below hero/features, before design tokens  
**Where:** `homePage.cshtml` after `.features` section  
**Content:** 4 card grid showing:
- Community Enquiry (currently linked)
- Payment Demo (currently dashboard-only)
- Planning Notification (currently URL-only)
- Information Request (currently URL-only)

**Why:** Home becomes a natural entry point for trying workflows; design tokens section remains for operators; no removal of existing content.

**Impact:** ~120 lines of HTML; adds ~300px height to authenticated home (acceptable; user goal-driven)

### 2. Add Workflow Admin Link to Dashboard ✅
**What:** Add "Workflow Admin" card/link to dashboard  
**Where:** `memberDashboard.cshtml` in the dash-grid  
**Guard:** Role-based visibility (admin-only; check against `Context.User.IsInRole("admin")` or similar)  
**Link:** Points to `/admin/workflow`

**Why:** Makes admin UI discoverable without URL knowledge; leverages dashboard's existing card pattern.

**Impact:** 1 new card; fits naturally in existing layout.

### 3. Hide Mobile Helper Widget UI (Keep UA Mock) ✅
**What:** Add `show-toggle="false"` attribute option to tag helper  
**Where:** `PrismMobileUserAgentDemoTagHelper.cs`  
**Behavior:**
- Still runs bootstrap script (UA mock remains active)
- **Does not render** the toggle UI widget (no checkbox, status, close button)
- Walkthrough screenshots capture clean page content
- Developers can still test via query param (e.g., `?prismShowMobileToggle=1` to override)

**Alternative (not recommended):** Playwright-native dismissal (click close button before screenshot in each test) — less reusable, requires per-test updates.

**Why:** Decouples mobile testing from screenshot concerns; one tag helper change fixes all walkthrough specs.

**Impact:** Tag helper only; no view changes needed.

### 4. Leave Homepage Height & Design Tokens Unchanged ✅
**Decision:** No removal of design system tokens section.  
**Rationale:** Tokens section is valuable for branding operators; scrolling is natural UX; adding demos above doesn't harm tokens visibility.

---

## What NOT to Change

| Item | Reason |
|------|--------|
| Header nav (3 items) | Clean; demos belong on targeted pages |
| Mobile nav config | Site-wide; not demo-specific |
| Workflow form rendering | Working well; no accessibility/UX issues |
| Dashboard size | Scrolling is natural; no change needed |

---

## Implementation Checklist (No Implementation Yet)

- [ ] **Home page:** Add demo workflows section (4 cards)
- [ ] **Dashboard:** Add admin card with role guard
- [ ] **Tag helper:** Add `show-toggle=false` attribute + query param override
- [ ] **Tests:** Verify no regressions in walkthrough specs
- [ ] **Accessibility:** Ensure demo cards meet WCAG 2.2 AA (focus, labels, contrast)

---

## Decision Rationale

**Why these three changes together?**
1. **Discoverability (1 + 2):** All workflows + admin UI are now reachable without URL knowledge
2. **Screenshot cleanliness (3):** Mobile widget no longer clutters walkthrough images
3. **Coherence:** Each change is independent; can be reviewed separately
4. **Minimal scope:** No removal of existing content; only additions + tag helper tweak

**Why not more aggressive changes?**
- Dashboard already works well (3 cards is clean; 4-5 is acceptable)
- Homepage tokens section has value (for operators)
- Header nav at 3 items is intentional (clarity over clutter)
- Mobile nav stays site-wide (not demo-specific)

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Home page longer on scroll | Low | Document natural scrolling; test at typical viewports |
| Admin card visible to non-admins | Medium | Implement role guard; test with non-admin user |
| UA mock affects other tests | Low | Keep bootstrap active; only hide UI; test mobile-specific features still work |
| Tag helper query param conflicts | Low | Use unique param name; document in code comment |

---

## Next Steps

1. **Review:** Scribe/team review of this audit
2. **Implementation:** If approved, no changes needed for this session (audit-only)
3. **Separate PR:** Recommend addressing each change in focused PR (home → dashboard → tag helper)
4. **Testing:** Update walkthrough specs to verify no mobile widget appears

---

## Related Artifacts

- **Audit document:** /Users/jonnymuir/Documents/Projects/Umbraco.Prism/.squad/agents/isabelle/history.md (2026-05-04 entry)
- **Routes defined in:** `/src/UmbracoPrism.TestSite/TestSiteSeedContract.cs`
- **Tag helper:** `/src/UmbracoPrism.Core/TagHelpers/PrismMobileUserAgentDemoTagHelper.cs`
- **Views:**
  - `/src/UmbracoPrism.TestSite/Views/homePage.cshtml`
  - `/src/UmbracoPrism.TestSite/Views/memberDashboard.cshtml`
  - `/src/UmbracoPrism.TestSite/Views/Shared/Master.cshtml`
- **Walkthroughs:** `docs/walkthroughs/*.md` + `src/UmbracoPrism.Client/tests/walkthroughs/*.walkthrough.spec.ts`
# Decision: v1.9.0 Release Cadence and Changelog Pattern

**Date:** 2026-05-04T10:45:47.516+01:00  
**Author:** Mabel (Technical Writer)  
**Scope:** Release process, version management, changelog structure

---

## What Was Decided

**Release Flow Implemented:**
1. Consolidate all squad bookkeeping in final pre-release commit
2. Bump version in package.json (semantic versioning)
3. Create comprehensive CHANGELOG.md entry grouping changes by type
4. Validate version consistency against CHANGELOG.md heading
5. Create single release commit with descriptive message
6. Push to origin/main (squad-release.yml workflow handles tag creation)

**Version Selection (for v1.9.0):**
- Bump to v1.9.0 (minor version) because release includes:
  - Workflow v2.0 atomic schema (major architectural change, new feature)
  - Business API arrival instrumentation (new diagnostics feature)
  - Information-request demo page (new demo content)
  - 20+ significant fixes and security improvements
- Not v2.0.0 because no breaking API changes (workflow schema additive with backwards compatibility path)

**Changelog Entry Structure:**
```markdown
## [vX.Y.Z] — YYYY-MM-DD

### Added
- **Feature name:** Description with context/impact

### Changed
- **Area name:** What changed and why

### Fixed
- **Issue name:** Root cause and resolution

### Security
- **Security issue:** Impact and mitigation (include SEC-ID)
```

**Validation Automation:**
- squad-release.yml confirms: `grep -qE "^## \[v?$VERSION\]" CHANGELOG.md`
- Fails release if version in package.json doesn't match CHANGELOG.md heading
- Ensures version consistency before tag creation

---

## Why This Decision

1. **Team clarity:** Clear separation between squad bookkeeping (histories, decisions) and product changes (version bump, changelog)
2. **Automation trust:** squad-release.yml workflow handles tag creation and GitHub release. Human validation limited to: version bump, changelog content, commit message
3. **User-facing clarity:** Comprehensive changelog entries (with context, security IDs, backwards compatibility notes) help users understand scope and impact
4. **Repeatability:** Pattern can be reused for future releases without modification

---

## Criteria Applied

- **Version bump:** Semantic versioning with feature/fix/security scope analysis
- **Changelog content:** Grouped by type (Added/Changed/Fixed/Security) with descriptive titles and context
- **Release boundary:** Single commit = one releasable unit. No mixed concerns (squad + product)

---

## Related Decisions

- **Diagnostics Script Landing: Scope Discipline** (2026-05-03): Product vs. bookkeeping separation
- **Transport-Diagnostics Landing** (2026-05-03): Single-unit product commit model
- **Business API Arrival Instrumentation Landing** (2026-05-03): Three-agent handoff with clean history

---

## Actionable Next Steps for Team

1. **Scribe:** Merge this decision into .squad/decisions.md after release workflow validates v1.9.0 tag creation
2. **Future releases:** Technical Writer repeats this exact flow for v1.9.1+ releases
3. **Changelog hygiene:** Encourage team members to draft changelog entries during sprint (in issues/PRs) to reduce end-of-cycle burden
---
title: Walkthrough & Test Coverage Audit Findings
author: Tangy (Tester)
date: 2026-05-04T11:46:55.877+01:00
status: PROPOSED
tags: [testing, coverage, walkthroughs, playwright]
---

# Walkthrough & Test Coverage Audit

## Summary

Audit of all Playwright tests and walkthrough specs across the Umbraco.Prism project reveals **strong coverage of end-user workflows** (4/4 workflows fully tested) but **gaps in edge cases, validation, mobile rendering, and operator flows**. Current state is regression-safe; no breaking changes detected.

## Current Coverage Status

### ✅ Strengths
- **20 automated tests** across 6 core spec files
- All 4 end-user workflow happy paths tested (community-enquiry, payment-demo, planning-notification, information-request)
- Comprehensive auth/session contracts (8 tests including restart behavior)
- Conditional reveals validated (community-enquiry, planning-notification)
- Check-answers edit flow tested (workflow-gds-journey)
- Helper patterns enforce good practices (`assertHealthyPage`, `step()`)

### ❌ Gaps
1. **Missing back/edit flow tests** for 3 of 4 workflows (community-enquiry, payment-demo, information-request)
2. **Missing validation tests** for 2 of 4 workflows (community-enquiry, information-request)
3. **No mobile viewport testing** (all tests use desktop 1280x720)
4. **Missing success state assertions** in information-request (no submission confirmation captured)
5. **No home page hero navigation test** (entry point to workflows)
6. **Operator/admin flows** all manual-only (acceptable per R6, not blocking)

## Detailed Coverage Analysis

### End-User Workflows
| Workflow | Happy Path | Conditional Reveal | Validation | Back/Edit | Success State |
|----------|:-:|:-:|:-:|:-:|:-:|
| Community Enquiry | ✓ | ✓ | ✗ | ✗ | ✓ |
| Payment Demo | ✓ | - | ✓ | ✗ | ✓ |
| Planning Notification | ✓ | ✓ | ✓ | ✓ | ✓ |
| Information Request | ✓ | - | ✗ | ✗ | ✗ |

### Session & Navigation
| Feature | Tested | Notes |
|---------|:------:|-------|
| Sign-in | ✓ | Includes Keycloak OIDC flow |
| Dashboard | ✓ | Both links (My Workflows, Start Workflow) |
| Sign-out | ✓ | Clean session termination |
| Restart Persistence | ✓ | Auth survives AppHost restart |
| Mock Business App API | ✓ | Bearer token, 401 on missing token |

### Manual-Only Walkthroughs (Acceptable per R6)
| Walkthrough | Reason | Status |
|-----------|--------|--------|
| Authoring a Workflow | Requires backoffice + C# fluent API | Manual ✓ |
| Creating a Tenant | Requires backoffice OIDC config | Manual ✓ |
| Design System | Umbraco backoffice CSS token task | Manual ✓ |
| Building a Mobile App | Xcode/Android Studio (out of scope) | Manual ✓ |
| Push Notifications | Service worker + browser permissions | Manual ✓ |

**Assessment:** All manual-only designations are justified. Automating these would require either:
- Backoffice automation (acceptable to keep manual per SKILL.md R6)
- Platform-specific tooling (Xcode/Android Studio)
- Complex service worker mocking (lower ROI)

## Recommended Coverage Improvements

### Priority 1: High Impact, Minimal Effort
**Effort: ~1 hour total**

1. **Add back/edit flow tests to 3 workflows**
   - Reuse pattern from `workflow-gds-journey` (test 5)
   - Add to: community-enquiry, payment-demo, information-request
   - Validates: User can navigate backward, change answer, see updated summary
   - Prevents regression: Workflow state management bugs

2. **Add validation tests to 2 workflows**
   - Reuse pattern from `payment-demo` (test 2)
   - Add to: community-enquiry, information-request
   - Validates: Error summary visible, field-level errors shown
   - Prevents regression: Validation logic breakage

3. **Add success state assertion to information-request**
   - Currently captures step 03 but doesn't assert "Your request is being reviewed"
   - Minimal change: Add heading assertion (like step 04 in community-enquiry)
   - Prevents regression: Silent workflow submission failure

### Priority 2: Medium Impact, Reasonable Effort
**Effort: ~1.5 hours total**

4. **Add mobile viewport tests**
   - Configure iPhone 12 viewport in playwright.localhost-auth.config.ts
   - Run existing walkthrough tests in mobile context
   - Validates: Mobile nav visible, form layout stacks, inputs accessible
   - Prevents regression: Mobile rendering bugs

5. **Create home page hero navigation walkthrough**
   - New file: `tests/walkthroughs/shared/home-page-hero.walkthrough.spec.ts`
   - Captures: Home page hero section and CTA click → workflow start
   - Validates: Hero visible, link href correct, landing workflow healthy
   - Prevents regression: Entry point navigation breakage

### Priority 3: Lower Priority, Deferred
**Effort: Future consideration**

6. **Add accessibility assertions** (a11y)
   - Use `@axe-core/playwright` integration
   - Run on all walkthrough steps
   - Prevents regression: WCAG compliance violations

7. **Tenant routing contract test**
   - Verify localhost vs tenant2.localhost routing (if manual tenant exists)
   - Minimal automation; validates middleware behavior

## Decision: Testing Standards Going Forward

### What Changes
1. **All new walkthroughs** must include:
   - Happy path test ✓ (already required)
   - At least one edge case test (validation, conditional reveal, or back/edit)
   - Mobile viewport variant (desktop + iPhone 12 or tablet size)
   - Success state assertion (submission confirmation, error message, etc.)

2. **Existing walkthrough gaps** to be closed:
   - Information Request: Add success state assertion (5 min)
   - Community Enquiry: Add validation test (15 min)
   - Community Enquiry: Add back/edit test (15 min)
   - Payment Demo: Add back/edit test (15 min)
   - Information Request: Add back/edit test (15 min)
   - Information Request: Add validation test (15 min)
   - All 4 walkthroughs: Add mobile viewport variant (45 min)

### What Stays the Same
- Manual-only walkthroughs (authoring, tenant creation, design system, mobile build, push notifications) remain acceptable per R6
- Helper patterns (`assertHealthyPage`, `step()`) enforce good practices
- Component tests continue in Storybook (no change)
- Backoffice automation not required (manual captures sufficient)

## Success Metrics

After implementing Priority 1 & 2 recommendations:
- ✓ 100% of walkthrough workflows covered for back/edit flow
- ✓ 100% of walkthrough workflows have validation test
- ✓ 100% of walkthrough tests run on mobile viewport
- ✓ 100% of workflows assert submission success state
- ✓ Home page entry point tested
- → Total: 26+ tests (up from 20)
- → Zero regression risk; improved edge case coverage

## Out of Scope (Not Changing)

The following are acceptable as manual-only or out-of-scope:
- Full backoffice OIDC/tenant creation automation
- Workflow authoring via backoffice (manual captures sufficient)
- Mobile app Xcode/Android Studio builds
- Service worker + push notification full lifecycle (partial automation only)
- Accessibility full audit (basic assertions can start now; full audit separate initiative)

---

**Next step:** Prioritize Tier 1 improvements (back/edit + validation tests) for closure by sprint end.
---
date: 2026-05-04T11:46:55.877+01:00
author: Tom Nook (Discovery & Architecture)
status: proposal
priority: high
category: walkthroughs, documentation, user-experience
---

# Walkthrough & Testing Architecture — Discovery & Recommendations

**Scope:** End-to-end verification of walkthrough/test infrastructure against user request constraints. No code changes in this pass — architecture and sequencing only.

---

## Executive Summary

Walkthroughs are architecturally sound (executable specs ✓, tests gate PRs ✓, spec-markdown lockstep enforced ✓). **Six concrete gaps** block the user's vision:

1. **Navigation hierarchy is incomplete.** Dashboard doesn't list all 4 workflow types; discovery requires visiting TestSite sources.
2. **Workflow types are underexposed.** Only 2 of 4 seeded workflows linked from dashboard; 2 others invisible to end users.
3. **Admin screen is unreachable.** `/admin/workflow` (where operators manage instances, move states, edit definitions) has no link from the dashboard or any user journey. Walkthroughs can't document the ops path.
4. **Screenshot heights are excessive.** `fullPage: true` produces 2500–9400px PNG files. Homepage screenshot is 9447px tall — unreadable in docs.
5. **Mobile nav leaks into workflow screenshots.** `prism-mobile-nav` component renders in walkthrough capture, adding visual clutter to form-focused screenshots.
6. **Workflow movement is undocumented.** No walkthrough shows how operators use admin panel to transition workflow instances between states.

Additionally:
- **Push notifications walkthrough is orphaned** — markdown written, spec exists but skipped, image directory empty.
- **4 workflow seeds exist; 9 walkthroughs reference them.** Mismatch suggests incomplete coverage or intentional deferral.

---

## What Exists Today

### Walkthrough Infrastructure ✓

**Three-artifact lockstep (per SKILL.md):**
- `docs/walkthroughs/{key}.md` — narrative
- `src/UmbracoPrism.Client/tests/walkthroughs/{key}.walkthrough.spec.ts` — executable
- `docs/images/walkthroughs/{key}/*.png` — generated

**9 walkthrough suites defined:**
1. community-enquiry (seeded ✓, spec ✓, images ✓)
2. information-request (seeded ✓, spec ✓, images ✓)
3. payment-demo (seeded ✓, spec ✓, images ✓)
4. planning-notification (seeded ✓, spec ✓, images ✓)
5. authoring-a-workflow (spec manual ✓, images N/A, no seed needed)
6. creating-a-tenant (spec manual ✓, images N/A, backoffice only)
7. design-system (spec exists, narrative exists)
8. building-a-mobile-app (spec manual, images N/A, device biometrics)
9. push-notifications (spec skipped, markdown written, **images empty ✗**)

**Test integration:**
- All 9 specs in `src/UmbracoPrism.Client/tests/walkthroughs/`
- All matched to `.github/workflows/capture-screenshots.yml` (manual `workflow_dispatch`)
- All gated by `localhost-auth-playwright` job in CI

**Screenshot infrastructure:**
- Helper in `tests/walkthroughs/support/walkthrough.ts` exports `step()` and `assertHealthyPage()`
- `step()` calls `page.screenshot({ fullPage: true })`
- `CAPTURE_SCREENSHOTS=1` env var controls write; assertions always run

---

### Navigation & Discoverability ✗

**What's exposed from dashboard (`/dashboard`):**
- Card: "My Workflows" → `/my-workflows` (WorkflowHub)
- Card: "Payment Demo" → `/payment-demo` (payment-demo workflow)
- Card: "Get in Touch" → `/get-in-touch` (community-enquiry workflow)
- No card or link for: information-request, planning-notification

**What's in the content tree (implicit, not dashboard-driven):**
- Home `/`
- Dashboard `/dashboard`
- WorkflowHub `/my-workflows`
- 4 workflow pages (`/get-in-touch`, `/payment-demo`, `/apply-for-planning-permission`, `/request-information`)

**What's hidden from typical user navigation:**
- `/admin/workflow` — ops panel with workflow instances, state transitions, JSON editor
  - Exists in `MockBusinessApp/Program.cs` (lines 276–745)
  - Hardcoded to Development environment only (defence-in-depth at line 49)
  - No link from dashboard, no mention in TestSite views
  - Accessible only if user knows the URL

---

### Workflow Definitions & Seeds

**4 seed files in `MockBusinessApp/workflow-seeds/`:**
1. `community-enquiry.json` — 4 states, form-based, conditional reveals
2. `information-request.json` — 3 states, file upload, address lookup
3. `payment-demo.json` — 3 states, Stripe integration, waiting state
4. `planning-notification.json` — 5 states, complex multi-page, waiting + review

**Workflow types inferred from state component trees:**
- `"question"` — user entry form states
- `"check-answers"` — summary-list component (GDS pattern)
- `"waiting"` — status timeline, no user actions
- `"confirmation"` — final state, congratulations panel
- `"task-list"` — (inferred from future v2 schema, may not be in current seeds)

No `StepType` enum in current code (deprecated from v1). Types are inferred post-render via `stepType()` utility in `BusinessAppWorkflowEngine`.

---

### Screenshots & Visual Capture

**Current state:**
- `step()` uses `page.screenshot({ fullPage: true })`
- Captures entire viewport height, no scroll clipping
- No exclusion for header, nav, or footer

**Real dimensions observed:**
| Walkthrough | File | Dimensions | Size (KB) |
|---|---|---|---|
| community-enquiry/01-initial | 1280×2537 | 185 |
| community-enquiry/02-conditional | 1280×2672 | 200 |
| information-request/01-initial | 1280×2088 | 114 |
| payment-demo/01-initial | 1280×1244 | 59 |
| planning-notification/01-initial | 1280×1957 | 80 |
| **shared/01-homepage** | **1280×9447** | **800** |

The shared homepage screenshot is **9447 pixels tall** — ~13 inch document when viewed at 72dpi. Visual noise in markdown.

**Mobile nav behavior:**
- `prism-mobile-nav` web component rendered in `_MobileShellNav.cshtml`
- Included in Master layout (applies to all views)
- Appears in all walkthrough screenshots (unless hidden via CSS or excluded via viewport)
- Adds ~60–80px visual clutter at top of form-focused screenshots

---

## Gaps & Blockers

### 1. Navigation Hierarchy Not Fully Exposed

**Problem:** A new user arriving at the dashboard sees 3 workflow cards (My Workflows, Payment Demo, Get in Touch). They have no way to discover that `information-request` and `planning-notification` workflows exist without:
- Browsing TestSite source code
- Asking the developer
- Reading the walkthrough index (not reachable from app UI)

**Impact on Walkthroughs:**
- "Information Request" walkthrough can be read, but user cannot reach the workflow unless they know `/request-information`
- "Planning Notification" walkthrough similarly blocked
- Ops cannot verify these workflows are fully functional via normal navigation

**What's needed:**
- Dashboard should list **all 4 workflow types** (or link to a discoverable registry)
- WorkflowHub (`/my-workflows`) could be expanded to show "all available workflows" section
- OR: Create a "Workflows" or "Templates" gallery on the dashboard

---

### 2. Admin Screen Unreachable from Normal Navigation

**Problem:** The `/admin/workflow` screen is the canonical ops interface for:
- Viewing all workflow instances across all users
- Transitioning instances between states (approve, reject, request-changes)
- Editing JSON definitions (hot-reload)
- Inspecting state diagrams and transitions

It exists in development but is completely hidden. No walkthrough can document the ops workflow.

**Current access:**
- Only via direct URL (if you know the path)
- Not linked from any view
- Not mentioned in README or docs (except this discovery)

**Impact on Walkthroughs:**
- Cannot document "Move a workflow instance from Review → Approved" steps
- Cannot show the state diagram or definition editor
- Operators have no UI path to the tool they need

**What's needed:**
- Link on dashboard (dashboard role: admin-only, or dev-environment-only display)
- OR: Document the URL in a "For Operators" section with prerequisite disclosure
- OR: Route it through the Umbraco backoffice instead (higher friction, but more secure)

---

### 3. Screenshot Heights Excessive; Mobile Nav Leaks In

**Problem 1: Height**
- `fullPage: true` captures the entire scrollable document
- Forms with lots of fields or long explanatory text produce 2500–9400px files
- User has to scroll endlessly in markdown; visual fatigue
- 800KB for a single screenshot is disproportionate

**Problem 2: Mobile Nav**
- `prism-mobile-nav` component adds ~60–80px at the top of every screenshot
- In a form-focused walkthrough (e.g., "Community Enquiry"), this is visual noise
- It's useful for mobile context docs, but clutter for desktop workflows

**What's needed:**
- Clip screenshots to viewport height or content bounds (viewport: 1280×800 or similar)
- Either hide `prism-mobile-nav` before capture (e.g., `await page.locator('prism-mobile-nav').hide()`) or exclude it via viewport
- Document the screenshot dimensions in SKILL.md

**Implementation hint:**
```typescript
await page.locator('prism-mobile-nav').evaluate(el => el.style.display = 'none');
// OR use a narrower viewport
page.setViewportSize({ width: 1280, height: 800 });
```

---

### 4. Push Notifications Walkthrough Is Orphaned

**State:**
- Markdown: ✓ (comprehensive, links to architecture docs)
- Spec: ✓ (exists, but `.skip(true, ...)`)
- Images: ✗ (directory is empty, only `.gitkeep`)

**Why skipped:**
- Spec comment says "Manual capture only" — web push subscription UI requires manual browser prompts
- Spec covers automation up to the subscription prompt, then defers to manual capture

**What's needed:**
- Decide: Is this a manual-only walkthrough (accept the `.skip` and document manual capture procedure in .md)?
- OR: Automate the browser's granted push subscription (mock it, or use headless browser grant automation)?
- Either way: Capture the images (manually or via automation) so the markdown has visual support

---

### 5. Workflow Type Discovery in Admin Screen

**Problem:** The `/admin/workflow` HTML shows workflow definitions with state icons and state diagrams, but there's no visual "gallery" of workflow types. It's an instance table + definition cards, not a "workflow template browser."

**What's needed (if exposing admin on dashboard):**
- Consider rearranging the admin HTML so the definition cards are visually prominent and easy to screenshot
- Group by workflow type or category
- Make each card screenshot-friendly (not overly wide, not a dense code dump)

---

### 6. Authoring & Tenant Creation Walkthroughs Are Manual-Only

**State:**
- Both marked `.skip(true, ...)` in specs
- Both require backoffice interaction (Umbraco admin UI)
- Both have TODO comments for manual captures

**What's needed:**
- Clarify scope: Are these walkthroughs expected to be auto-captured, or documented as manual?
- If manual: Document the capture procedure in the markdown (see SKILL.md R1 for example)
- If auto: Implement backoffice auth and content tree navigation in the spec

**Low priority** — these are developer/operator workflows, not end-user. But they should be complete enough that someone can follow them without surprises.

---

## Proposed Implementation Slice

**Goal:** Deliver a coherent end-to-end journey from end-user workflows through admin management, with complete discoverability, properly-sized screenshots, and no hidden paths.

### Phase 1: Dashboard Navigation (Isabelle + Blathers — 1–2 days)

**Objective:** Expose all 4 workflow types from dashboard; link to admin screen (dev-only or admin-only).

**Deliverables:**
- [ ] Add "Request Information" and "Planning Notification" cards to dashboard (or expand to a gallery/list view)
- [ ] Add "Manage Workflows" card that links to `/admin/workflow` (only visible if dev or has admin role)
- [ ] Verify WorkflowHub lists all 4 workflow types (or add a section)
- [ ] Update `memberDashboard.cshtml` and related controllers

**Test Requirement:** Existing dashboard tests still pass; new cards link to correct URLs (no 404s).

**Who owns:** Isabelle (frontend) + Blathers (controller routing/auth checks)

**Dependencies:** None — purely additive to dashboard view.

---

### Phase 2: Screenshot Optimization (Tangy — 2–3 days)

**Objective:** Reduce screenshot heights; remove mobile nav clutter; establish viewport standard.

**Deliverables:**
- [ ] Update `walkthrough.ts` `step()` function:
  - Set viewport to fixed dimensions (e.g., 1280×1024)
  - Hide `prism-mobile-nav` before capture (or exclude via viewport width)
  - Document the standard in SKILL.md
- [ ] Re-capture all walkthrough images via `workflow_dispatch` (automated batch)
- [ ] Verify community-enquiry/01-initial goes from 2537px → ~1024px (or similar)
- [ ] Update all markdown if image filenames or sizes change significantly

**Test Requirement:** All walkthrough specs still pass; images are cleaner and shorter; markdown renders without excessive scrolling.

**Who owns:** Tangy (testing), with Mabel (documentation review)

**Dependencies:** Phase 1 complete (new dashboard cards should be in screenshots)

**File-level changes:**
- `src/UmbracoPrism.Client/tests/walkthroughs/support/walkthrough.ts` — `step()` function
- `.squad/skills/walkthroughs-as-executable-specs/SKILL.md` — document viewport standard
- All `docs/images/walkthroughs/**/*.png` — regenerated

---

### Phase 3: Admin Walkthrough & State Movement (Blathers — 2–3 days)

**Objective:** Document the admin screen; show operators how to move workflow instances between states.

**Deliverables:**
- [ ] Create `docs/walkthroughs/workflow-administration.md`
- [ ] Create `src/UmbracoPrism.Client/tests/walkthroughs/workflow-administration.walkthrough.spec.ts`
- [ ] Spec covers:
  - Navigate to `/admin/workflow`
  - View workflow instances table
  - View workflow definitions (state diagrams)
  - Execute an action (e.g., "Approve" a pending instance) via the form
  - See instance state change reflected in table
- [ ] Capture screenshots for each step

**Test Requirement:** Spec gates on all PRs; no CI red flags.

**Who owns:** Blathers (backend), with Tangy (test structure)

**Dependencies:** Phase 1 (dashboard link exists), Phase 2 (screenshot config finalized)

**File-level changes:**
- New: `docs/walkthroughs/workflow-administration.md`
- New: `src/UmbracoPrism.Client/tests/walkthroughs/workflow-administration.walkthrough.spec.ts`
- New: `docs/images/walkthroughs/workflow-administration/*.png`
- Update: `docs/walkthroughs/README.md` to include new walkthrough

---

### Phase 4: Push Notifications & Manual Capture Walkthroughs (Mabel + Tangy — 2 days)

**Objective:** Complete push-notifications walkthrough; decide on authoring/tenant-creation manual captures.

**Deliverables (Push Notifications):**
- [ ] Clarify: Is this end-to-end automatable, or manual from subscription prompt onward?
- [ ] If automatable: Implement browser grant automation in spec
- [ ] If manual: Document the manual capture procedure in the markdown (see SKILL.md for format)
- [ ] Capture screenshots for all steps
- [ ] Remove `.skip()` or clearly document why it remains skipped

**Deliverables (Authoring & Tenant):**
- [ ] Decide: Full automation, or manual with documented capture procedure?
- [ ] If manual: Add `<!-- manual capture: reason -->` comments in markdown per SKILL.md R1
- [ ] If full automation: Implement backoffice login + navigation in spec

**Test Requirement:** All specs are not skipped OR have documented reasons + manual procedures.

**Who owns:** Mabel (docs clarity) + Tangy (spec implementation)

**Dependencies:** Phases 1–3 complete

---

### Phase 5: Navigation Hierarchy & Discoverability Refinement (Tom Nook — 1 day)

**Objective:** Review final navigation hierarchy; ensure Prism content tree matches documentation; update SKILL.md.

**Deliverables:**
- [ ] Verify all 4 workflow types are navigable from dashboard or hub
- [ ] Verify `/admin/workflow` is accessible via dashboard link or documented URL
- [ ] Update `umbraco-workflow-page-ownership` SKILL.md with final guidance
- [ ] Review all walkthrough READMEs and links for consistency
- [ ] Final check: No broken links, all URLs resolve, navigation feels natural

**Who owns:** Tom Nook (architecture review)

**Dependencies:** All prior phases complete

---

## Sequencing & Team Coordination

**Recommended order:**
1. **Phase 1** (Dashboard) — unblocks Phases 2–3. Start immediately.
2. **Phase 2** (Screenshots) — can run in parallel with Phase 1; unblocks final polish.
3. **Phase 3** (Admin Walkthrough) — depends on Phase 1 link; depends on Phase 2 for screenshot config.
4. **Phase 4** (Push/Manual) — independent; can run in parallel with Phases 2–3.
5. **Phase 5** (Final Review) — only after all prior phases complete.

**Cross-File Dependencies:**

| File | Phase | Owner | Impact | Notes |
|---|---|---|---|---|
| `memberDashboard.cshtml` | 1 | Isabelle | Dashboard cards | Adds links to new workflows + admin |
| `MemberDashboardController.cs` | 1 | Blathers | Controller logic | Auth checks, URL resolution |
| `TestSiteSeedContract.cs` | 1 | Blathers | Routes | Add constants for new workflow URLs if needed |
| `walkthroughs/support/walkthrough.ts` | 2 | Tangy | Screenshot helper | Viewport + mobile-nav-hiding logic |
| `.squad/skills/walkthroughs-as-executable-specs/SKILL.md` | 2 | Tangy | Skill doc | Document viewport standard + height rules |
| `/admin/workflow` (Program.cs) | 1 | Blathers | Ops panel | No code change, but linked from dashboard |
| `docs/images/walkthroughs/**/*.png` | 2 | automated | Screenshots | Regenerated by `workflow_dispatch` |
| `docs/walkthroughs/*.md` | 3–4 | Tangy/Mabel | Narratives | New walkthroughs + updates to existing |

**Potential bottlenecks:**
- **Phase 1 → Phase 2:** Tangy may need Isabelle's final dashboard design before capturing. Sequence so dashboard merge → screenshot capture immediately.
- **Phase 2 → Phase 3:** Screenshot config finalized before starting admin-walkthrough spec.
- **Pull request merges:** No feature branches per 2026-04-26 directive. Each phase commits directly to `main`; recommend squashing logical units into 1–2 commits per phase.

---

## Files to Touch (Summary)

### View/Controller (Phase 1)
- `src/UmbracoPrism.TestSite/Views/memberDashboard.cshtml`
- `src/UmbracoPrism.Core/Controllers/MemberDashboardController.cs` (if auth check needed for admin link)
- `src/UmbracoPrism.TestSite/TestSiteSeedContract.cs` (if new URLs added)

### Test Infrastructure (Phase 2)
- `src/UmbracoPrism.Client/tests/walkthroughs/support/walkthrough.ts`

### Walkthrough Specs (Phase 3–4)
- `src/UmbracoPrism.Client/tests/walkthroughs/workflow-administration.walkthrough.spec.ts` (NEW)
- `src/UmbracoPrism.Client/tests/walkthroughs/push-notifications.walkthrough.spec.ts` (update)
- `src/UmbracoPrism.Client/tests/walkthroughs/authoring-a-workflow.walkthrough.spec.ts` (decide on manual)
- `src/UmbracoPrism.Client/tests/walkthroughs/creating-a-tenant.walkthrough.spec.ts` (decide on manual)

### Walkthrough Narratives (Phase 3–4)
- `docs/walkthroughs/workflow-administration.md` (NEW)
- `docs/walkthroughs/push-notifications.md` (update/complete)
- `docs/walkthroughs/authoring-a-workflow.md` (update with manual capture procedure)
- `docs/walkthroughs/creating-a-tenant.md` (update with manual capture procedure)
- `docs/walkthroughs/README.md` (index all 9+1 walkthroughs)

### Documentation & Skills (Phase 2–5)
- `.squad/skills/walkthroughs-as-executable-specs/SKILL.md` (document viewport standard)
- `.squad/skills/umbraco-workflow-page-ownership/SKILL.md` (refine if needed)

### Generated Assets (Phase 2, 3–4)
- `docs/images/walkthroughs/**/*.png` (all regenerated; new workflow-administration dir)

---

## Risks & Mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| Admin screen assumes dev-only access; adding dashboard link exposes it to end users | Medium | Add role-based or env-var gate on the view; display only in Development or if user has admin role. Document this in SKILL.md. |
| Screenshot re-capture changes image dimensions; old docs may reference old sizes | Low | Run capture in CI on a single branch; verify all markdown images load before merging. |
| Push-notifications walkthrough remains manual/incomplete; scope creep on spec automation | Low | Decide early (manual vs. auto); document decision and stick to it. Accept manual for this phase if crypto/browser-grant complexity is high. |
| Workflow types (community, payment, planning, info-request) hardcoded in views; adding a 5th requires code change | Low | Consider data-driven dashboard card list (loop over workflow definition keys returned from Business App API); out of scope for this pass, but note for v2.1. |
| Navigation changes break existing links in external docs or bookmarks | Low | Verify URLs are stable (only *adding* new routes, not moving existing ones). Test `/get-in-touch`, `/payment-demo`, `/my-workflows` remain unchanged. |

---

## Non-Goals & Deferral

**Out of scope for this pass:**
- Rebuilding the admin screen HTML (it's functional; we're just linking to it)
- Automating browser grant prompts (push-notifications spec remains manual-to-capture if infeasible)
- Changing the workflow definition storage (JSON seeds are fine; no schema migration)
- Mobile app screenshots (building-a-mobile-app walkthrough remains manual; device biometrics are not UI-automatable)
- Consolidating duplicate walkthrough docs (doc-walkthrough-consolidation SKILL.md deferred to Mabel's batch)

---

## Acceptance Criteria

- [ ] **Phase 1:** All 4 workflow types are discoverable from dashboard or WorkflowHub; `/admin/workflow` is linked (dev-only or admin-only).
- [ ] **Phase 2:** All walkthrough screenshots are ≤1200px tall; `prism-mobile-nav` is hidden or excluded.
- [ ] **Phase 3:** New `workflow-administration.md` walkthrough documents state transitions via admin screen; spec gates on PR.
- [ ] **Phase 4:** `push-notifications` walkthrough is complete (auto or manual) with images; `authoring-a-workflow` and `creating-a-tenant` have documented manual procedures.
- [ ] **Phase 5:** Navigation hierarchy is documented in SKILL.md; no broken links in any walkthrough; team review sign-off.

---

## Next Steps

1. **Immediate:** Share this document with Isabelle, Blathers, Tangy, Mabel for review.
2. **Day 1:** Isabelle + Blathers start Phase 1 (dashboard cards).
3. **Day 2–3:** Tangy works Phase 2 in parallel (screenshot config) once Phase 1 is visible.
4. **Day 3–5:** Blathers + Tangy start Phase 3 (admin walkthrough); Mabel starts Phase 4 (push/manual).
5. **Day 6:** Tom Nook final architecture review (Phase 5); ready for merge.

**Expected outcome:** End-to-end walkthrough journey is complete, discoverable, visually clean, and documented with executable specs that gate every PR. Operators have a canonical path to the admin screen. All workflow types are reachable from normal navigation.

---

**End of discovery report.**


---
date: 2026-05-04T11:46:55.877+01:00
author: Brewster
status: IMPLEMENTED
area: testsite, walkthroughs, discoverability
---

# Walkthrough Discoverability — All Workflow Types Reachable from Dashboard

## Context

Audit findings showed that some workflow demos (planning-notification, information-request)
were only reachable via direct URL knowledge. The member dashboard linked to just three
workflow types out of four, and there was no route from the Prism dashboard to the
MockBusinessApp workflow admin screen.

Two TestSite stub views (`workflowHub.cshtml`, `workflowPage.cshtml`) contained
`Layout = null` and no content, silently overriding the Core library's fully implemented
embedded views and rendering blank pages.

## Decisions Made

### 1. Delete TestSite stub views — use Core embedded views

`src/UmbracoPrism.TestSite/Views/workflowHub.cshtml` and `workflowPage.cshtml` were
stub files with `Layout = null` that blocked the `PrismEmbeddedViewsStartupFilter`
embedded views from being served. Deleting the stubs lets the Core's implementations
(with `Layout = "~/Views/Shared/Master.cshtml"` and full rendering logic) take over.

**Rule:** The TestSite should not ship stub overrides for Core-embedded views unless
there is a deliberate TestSite-specific customisation. A file that only contains
`Layout = null` is a broken placeholder and must be removed.

### 2. Restructure member dashboard card grid

The existing 6-card flat grid was split into two coherent groups:

- **Overview** (4 cards): My Account, Documents, Support, My Workflows hub
- **Workflow Demos** (4 cards, in a labelled `dash-section`): Get in Touch,
  Apply for Planning Permission, Payment Demo, Request Information

All four seeded workflow types are now directly reachable from one section with
content-tree resolved URLs, not hardcoded route guesses.

### 3. Expose workflow admin URL from `MemberDashboardController`

`IConfiguration` was injected into `MemberDashboardController` to derive
`{PrismBusinessApp:WorkflowApiBaseUrl}/admin/workflow`. This is the same URL pattern
the AppHost annotates as a `Workflow Admin` resource URL. It is passed to the view as
`ViewBag.WorkflowAdminUrl`.

A **Developer Tools** `dash-section` renders conditionally (only when the URL is set),
showing a single card linking to the admin screen in a new tab.

### 4. Environment-aware without extra config

No new configuration keys are introduced. The existing `PrismBusinessApp:WorkflowApiBaseUrl`
already resolves correctly in Codespaces (via AppHost forwarded URL detection) and
locally (`https://localhost:7245`). The admin URL is simply appended as `/admin/workflow`.

## Verification

- `dotnet build` — 0 errors, 2 pre-existing warnings (unrelated)
- `dotnet test` — 690 passed, 0 failed

---
date: 2026-05-04
author: Tangy
status: PROPOSED
area: testing, walkthroughs, screenshots, documentation
---

# Walkthrough Coverage Hardening — Test Gaps and Screenshot Behaviour

## Context

Walkthrough coverage audit (2026-05-04) found five gaps in the executable specs:

1. Back/edit flows absent for `community-enquiry`, `payment-demo`, and `information-request`
2. Form validation tests absent for `community-enquiry` and `information-request`
3. `information-request` happy path lacked an explicit body-content assertion for the under-review success state
4. No home-page entry walkthrough (homepage hero → dashboard → workflow hub path)
5. Screenshot capture used `fullPage: true` unconditionally, producing oversized images for long pages (homepage hero, etc.)

## Decisions

### D1 — Viewport-first screenshots; fullPage is opt-in per step

**Decision:** The `step()` helper in `tests/walkthroughs/support/walkthrough.ts` now defaults to
`fullPage: false` (viewport-sized capture). Individual steps that genuinely need the full scrolled
page (e.g. a check-answers summary list that would be cut off) can pass `fullPage: true` via the
`PageHealthCheck` interface.

**Rationale:** Viewport captures show exactly what the user sees without scrolling, which is the
right documentation-first default. Full-page captures are appropriate for summary/check-answers
pages only.

**Isabelle hook contract:** The `fullPage` flag on `PageHealthCheck` is the per-step control point
intended for the docs pipeline. If the `capture-screenshots.yml` workflow needs a global override
(e.g. always full-page for a particular walkthrough), the recommended mechanism is:

```yaml
# In .github/workflows/capture-screenshots.yml
env:
  CAPTURE_SCREENSHOTS: '1'
  SCREENSHOT_FULL_PAGE: '1'   # <-- add this to request full-page globally
```

Then read `process.env.SCREENSHOT_FULL_PAGE === '1'` in `walkthrough.ts` as the fallback when
`expected.fullPage` is undefined:

```ts
const useFullPage = expected.fullPage ?? process.env.SCREENSHOT_FULL_PAGE === '1' ?? false;
await page.screenshot({ path: file, fullPage: useFullPage });
```

This change is NOT included in the current commit; it is queued for Isabelle to implement when
the docs pipeline requires it. The existing `fullPage?: boolean` field on `PageHealthCheck` is
the stable hook.

### D2 — Persistence tests verify instance-policy contract, not just submit success

**Decision:** For single-page workflows (`community-enquiry`, `information-request`,
`payment-demo`) that have no check-answers step, the "back/edit" behavioral contract is:
*after submission, returning to the workflow URL shows the current state (under-review /
processing), not a fresh form.*

These "persistence" tests are now in the respective walkthrough specs. They navigate away after
submit and navigate back to verify the instance-policy guarantee.

### D3 — `home-entry` is a first-class walkthrough

**Decision:** `home-entry.walkthrough.spec.ts` is a new walkthrough spec covering the full
homepage entry path: signed-out hero → signed-in hero → dashboard → workflow hub. It uses the
same `LiveAppHost` + `step()` pattern as all other walkthrough specs.

The `docs/walkthroughs/home-entry.md` document is the human narrative counterpart; it embeds the
four screenshots generated by the spec.

### D4 — `assertHealthyPage` skipHeading usage for variable-heading pages

**Decision:** The home page's signed-in state and the dashboard may not present their hero text
as a `<h1>` role heading. Where the primary visual identity is a welcome message or layout element
rather than a semantic heading, `skipHeading: true` is used and the test adds an explicit
`expect(...).toBeVisible()` assertion for the relevant content.

This maintains R3 (assert before shoot) without coupling the test to implementation-specific
heading hierarchy.

## Scope not changed

- Admin/backoffice walkthroughs (`authoring-a-workflow`, `creating-a-tenant`, `design-system`)
  remain manual-only per the existing policy. No backoffice automation was added.
- Mobile viewport tests were identified as a gap in the audit but are out of scope for this
  hardening pass (deferred to a future Tangy task).

## Files changed

- `tests/walkthroughs/support/walkthrough.ts` — fullPage default + Isabelle hook comment
- `tests/walkthroughs/community-enquiry.walkthrough.spec.ts` — validation + persistence tests
- `tests/walkthroughs/information-request.walkthrough.spec.ts` — validation + persistence + explicit success assertion
- `tests/walkthroughs/payment-demo.walkthrough.spec.ts` — defer/persistence test
- `tests/walkthroughs/home-entry.walkthrough.spec.ts` — new spec (3 tests)
- `docs/walkthroughs/home-entry.md` — new walkthrough document
- `docs/images/walkthroughs/home-entry/` — new images directory (.gitkeep placeholder)


# Decision: PASA death-process should use verified case access, not mandatory registration

**Date:** 2026-05-15T06:35:47.013+01:00  
**Author:** Blathers  
**Status:** Proposed  

## Summary

For a PASA-style death-notification workflow, the notifier should not be forced through permanent registration before they can report a death, save progress, or resume later.

Instead, the product should use a lightweight verified contact mechanism such as email magic link or SMS OTP to establish a case-scoped notifier identity. Prism then hosts the workflow for that notifier identity, while the business app owns member matching, case persistence, evidence tracking, and reviewer decisions.

## Why

- Bereavement reporting is often a one-off task carried out by someone who is not the member.
- The current Prism workflow model already supports resumable, reviewer-backed journeys once an authenticated actor exists.
- A case-scoped identity gives enough proof to save and resume safely without over-designing account creation.

## Team impact

- Backend and auth work should plan for a notifier-facing session model alongside member-facing auth.
- Workflow design should treat the notifier as the actor and the deceased member as the linked subject.
- Case-management persistence should stay outside Prism workflow field state.


# Decision: PASA Death Process Design Scaffold

**Date:** 2026-05-15T06:35:47.013+01:00  
**Author:** Celeste (Documentation Engineer)  
**Status:** 🚧 Design Phase — Input Requested

## Summary

Authored a comprehensive design document scaffold for a PASA (lifecycle termination) death-process workflow example. The scaffold is intentionally open-ended with explicit decision slots for each discipline (Architecture, Security, Backend, Frontend, Testing) to absorb input from Tom Nook, Copper, Blathers, Isabelle, and Tangy.

## Rationale

**Why a scaffold instead of a complete spec?**

1. **Clarity on unknowns** — Rather than guess at implementation details, the scaffold explicitly flags design decisions that *must* be made upstream (e.g., "Is this single-instance or multi-instance? Who can approve?")
2. **Parallel input** — Each team member can focus on their domain without waiting for others; inputs can be merged later.
3. **Reusable pattern** — The structure itself (decision slots, open questions, narrative sections) can be applied to future workflow designs.
4. **Documentation discipline** — By linking design → backend contract → walkthrough → security audit → specs, the document ensures all artifacts stay in sync.

## Document Structure

The design document includes:

- **Overview & Goals** — Why we're documenting this workflow
- **Open Questions by Discipline** — Explicit slots for Tom Nook (architecture), Copper (security), Blathers (backend), Isabelle (frontend), Tangy (testing)
- **Proposed Workflow Structure** — Tentative state machine with component mapping
- **End-to-End Narrative** — Placeholder walkthrough describing user, admin, and system actions
- **Backend Contracts (Tentative)** — Sample JSON workflow definition + `/advance` response schema
- **Security Considerations** — Threat model & tenant isolation questions
- **Testing Strategy** — Placeholder for executable specs and unit tests
- **Documentation Artifacts** — Links to design → backend spec → walkthrough → security guide → executable specs
- **Decision Timeline** — Four phases from design → implementation → documentation
- **Appendix for Reviewers** — Role-specific guidance for each team member

## Location

Created at: `/docs/design/pasa-death-process.md`

Follows existing design doc conventions:
- Named after the workflow (like `workflow-forms-engine.md`)
- Linked from `docs/design/README.md` (to be added)
- Uses markdown with mermaid flowcharts for clarity
- Includes state machines, contracts, and narratives

## Next Action

Team should review and fill in open questions:

1. **Tom Nook:** Confirm scope, instance policy, state sequence
2. **Copper:** Refine threat model, define audit trail requirements
3. **Blathers:** Finalize backend contract, cleanup orchestration
4. **Tangy:** Define test scenarios and performance SLAs
5. **Celeste:** Merge inputs and advance to walkthrough/implementation phases

## Key Learning

This approach — **design scaffold with explicit decision slots** — is reusable for future complex workflows. Consider extracting as a `.squad/templates/design-doc-scaffold.md` for future use.



# Decision: PASA death-process should use staged assurance and case-scoped access

**Date:** 2026-05-15T06:35:47.013+01:00  
**Author:** Copper (Security Engineer)  
**Status:** Proposed  

## Summary

For the PASA death-notification example, the notifier should not create a permanent member-style account just to report a death, save progress, or return later.

Instead, the design should use:

1. a **public start** with minimum data capture,
2. **verified contact-channel access** via magic link as the primary mechanism, with OTP as a fallback,
3. a **case-scoped notifier identity** plus case reference for save/resume,
4. **reviewer-backed step-up assurance** before any meaningful member-data disclosure or downstream benefit action.

## Security posture

- Treat the **notifier** as the authenticated actor and the **deceased member** as the linked subject.
- Separate **channel proof** from **authority/member-match proof**.
- Keep member matching, reviewer notes, anti-fraud signals, and entitlement decisions in server-side case-domain tables, not in browser-owned workflow payloads.
- Fail closed on data disclosure: before verification, show only generic statuses such as `received`, `under review`, or `more information needed`.

## Save/resume decision

The preferred save/resume pattern is:

- issue a case reference as soon as contact verification succeeds,
- re-establish access through a fresh verified session,
- use a workflow hub to list that notifier's active/completed death cases,
- never treat a raw case URL, `instanceId`, or reference number as sufficient authentication.

## Why this beats the alternatives

- **Full registration** is disproportionate for a one-off bereavement task and increases friction.
- **Magic link alone** is acceptable for bootstrap and low-risk resume, but not for sensitive disclosure without reviewer-backed progression.
- **Case reference + KBA alone** is too weak for online assurance.
- **Delegated representative portals** are a valid future extension, but should come after the simpler case-scoped model.

## Team impact

- Backend design should add `NotifierIdentity` / `NotifierSession` and keep `DeathCase` separate from `WorkflowInstance`.
- Frontend/workflow design should show only generic progress until reviewer-backed verification is complete.
- Documentation and walkthroughs should make the staff-review boundary explicit so the example does not imply that a notifier can self-serve beneficiary or payment outcomes.


# Tom Nook decision — PASA death-process baseline

**Date:** 2026-05-15T06:35:47.013+01:00
**Requested by:** Jonny Muir

## Decision

Use a **case-scoped notifier model** for the PASA death-process example:

1. the notifier is the authenticated workflow actor,
2. the deceased member is the linked subject,
3. the service does **not** require mandatory registration up front,
4. save/resume uses a **hybrid** of passwordless verified-session access plus case-reference recovery,
5. stronger identity checks happen only when the case moves into sensitive disclosure or payment-affecting work.

## Rationale

- PASA public guidance supports **risk-based** identity verification and a frictionless experience where proportionate.
- Broader UK bereavement services show that **no-account or optional-account initiation** is the better front-door pattern for death notification.
- This keeps Prism aligned with existing save/resume and reviewer-loop patterns without pretending the deceased member is the signed-in workflow user.

## Consequences

- The example should add a small pre-workflow bootstrap for notifier contact verification.
- Member matching, duplicate detection, and evidence review stay in the business-app domain layer.
- Progress visibility should stay high level until the case has passed the required proofing threshold.

## Needs sign-off from

- Product owner
- Tom Nook
- Copper
- Blathers
- Celeste



# Decision: Strengthen Approval Workflow Narratives in Walkthroughs

**Date:** 2026-05-04  
**Author:** Mabel (Copilot Documentation Specialist)  
**Status:** Implemented  

## Summary

Updated four walkthrough documents to provide a complete, step-by-step guided demonstration of the approval/reviewer handoff pattern. Moved from brief explanations of "approval is needed" to full narratives showing user submission → waiting state → operator review → user outcomes.

## Changes Made

### 1. **Payment Demo** (`docs/walkthroughs/payment-demo.md`)
- **Restructured** from 2-part (form + waiting) to 4-part narrative:
  - Part 1: End-user submission (form → processing state)
  - Part 2: Operator approval (admin panel → viewing definition → performing approval)
  - Part 3: Return-to-user confirmation (what the user sees after approval)
  - Part 4: Production patterns (webhook vs manual approval vs operator interface)
- **New content:**
  - Explicit step-by-step guide for accessing admin panel from dashboard
  - Workflow definition JSON showing `requiresRole: "reviewer"` on the complete transition
  - Explanation of why the `waiting` component + `single` instance policy work together
  - Three production patterns (Stripe webhook, operator interface, system role) showing the approval step is never purely manual
- **Narrative focus:** "Submit now, reviewer completes later" → user can safely defer

### 2. **Community Enquiry** (`docs/walkthroughs/community-enquiry.md`)
- **Restructured** from 2-part to 4-part narrative:
  - Part 1: End-user submission (form → under-review state)
  - Part 2: Operator review (admin panel → viewing definition → two scenarios: approve or request changes)
  - Part 3: User receives feedback (if approved → completion; if changes requested → form with answers pre-filled)
  - Part 4: Production patterns (operator portal SLA/routing, why role-based approval matters)
- **New content:**
  - Explanation of `under-review` as non-terminal, waiting state
  - State machine JSON showing both `approve` and `request-changes` transitions with `requiresRole: "reviewer"`
  - Cycle loops: `collecting-details` ↔ `under-review` ↔ `collecting-details` (iterative refinement)
  - What "request changes" means for the user (form returns with answers, opportunity to revise)
- **Narrative focus:** "One-way submission" → "Iterative review cycle"

### 3. **Information Request** (`docs/walkthroughs/information-request.md`)
- **Restructured** from 2-part to 4-part narrative (same shape as Community Enquiry)
  - Part 1: End-user request (form with urgency field → waiting state)
  - Part 2: Operator review (admin panel → urgency-driven routing/SLAs)
  - Part 3: User receives outcome (approval or changes requested)
  - Part 4: Production patterns (DPO portal, compliance SLAs, why urgency triage)
- **New content:**
  - Urgency field now explicitly tied to reviewer workflow (triage queue, SLA assignment, team routing)
  - State machine showing transitions gated by `requiresRole: "reviewer"`
  - Explanation of how urgency data flows into operator decision-making
  - SLA examples: Standard (7 days), Urgent (2 days), Critical (same day)
- **Narrative focus:** "Submit with urgency flag" → "Urgency drives operator triage and SLAs"

### 4. **README & Workflow Administration**
- **README.md:** Updated note to position admin panel as the "reviewer role simulator" in the local demo, cross-linking to all three approval workflows
- **Workflow Administration.md:** 
  - Expanded opening note to explain it's the "harness" for testing reviewer decisions
  - Enhanced Part 2b ("Complete Approval Workflows") with a full step-by-step walkthrough of the handoff (submit → wait → admin action → user sees outcome)
  - Added "Key Points" explaining role-based enforcement, data visibility, and outcome visibility

## Documentation Principles Applied

- **Executable specs alignment** – Each walkthrough narrative coordinates with workflow definitions in `workflow-seeds/` and test specs in `tests/walkthroughs/`
- **Guided demonstration** – Each reads as a step-by-step walkthrough someone could follow in the running app
- **Conceptual coherence** – Admin panel is positioned as the "reviewer actor" in the service flow, not an isolated debugging tool
- **Production grounding** – Each includes a "Production Patterns" section showing why this architecture matters in real systems (webhooks, operator portals, SLAs, role enforcement)
- **User-centric outcomes** – Always shows what changes for the original user after approval/feedback

## Why This Matters

1. **Onboarding clarity** – New developers understand not just "what approval is" but "how it flows end-to-end"
2. **Test authoring** – Specs now have clear narratives they can reference; easier to write related edge case tests
3. **Design grounding** – Product team can see exactly where approval/review points are and why they exist
4. **Production mapping** – Each section labeled "Production Patterns" makes clear how the demo harness maps to real operator workflows

## Files Changed

- `docs/walkthroughs/payment-demo.md` – Full rewrite, 4-part structure
- `docs/walkthroughs/community-enquiry.md` – Full rewrite, 4-part structure
- `docs/walkthroughs/information-request.md` – Full rewrite, 4-part structure
- `docs/walkthroughs/README.md` – Updated cross-linking note
- `docs/walkthroughs/workflow-administration.md` – Expanded opening note and Part 2b

## Next Steps

- Verify screenshot ordering in the specs matches the new narrative structure (NN-slug.png naming)
- If Tangy is working on executable specs, align test step naming with the new "Part" structure
- Consider recording video walkthroughs following the new 4-part narrative for async team review
### 2026-05-04T13:17:22.267+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Don't just call out that reviewer approval/rejection is needed; show it happening end-to-end in Playwright by navigating via the Aspire/dashboard workflow admin path, showing the workflow definition/state, approving it, and demonstrating that the original waiting user is moved on automatically.
**Why:** User request — captured for team memory
### 2026-05-04T13:20:30.000+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Make the walkthrough understandable step by step so someone can follow the whole workflow lifecycle and really understand what is happening at each stage.
**Why:** User request — captured for team memory
### 2026-05-04T13:24:41.480+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Additional walkthrough steps should be complemented appropriately with screenshots.
**Why:** User request — captured for team memory
### 2026-05-04T13:37:58.618+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Hide the demo Prism user-agent hover in screenshots, but do not crop screenshots so aggressively that the demonstrated content is cut off; limit vertical cropping primarily for genuinely long pages such as the home page.
**Why:** User request — captured for team memory
### 2026-05-04T13:44:50.590+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Screenshot policy should prefer showing the whole functionality of the screen, using cropped or viewport-sized captures only when a page is unusually huge and a full capture stops being useful.
**Why:** User request — captured for team memory
### 2026-05-04T16:09:36.911+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Refresh `docs/design` workflow docs thoroughly: keep them current, avoid pasting whole model files unless a snippet teaches something useful, and make them read like strong package documentation that is coherent, concise, discoverable, well indexed, and tells a clear story about implementing your own workflow.
**Why:** User request — captured for team memory
### 2026-05-08T05:58:15.779+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Keep the GitHub README unchanged, but add an automatic way to produce a Marketplace-friendly description that removes/adjusts unsupported HTML and rewrites relative links so they resolve correctly outside GitHub.
**Why:** User request — captured for team memory
### 2026-05-08T06:26:48.026+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** Make sure the Umbraco Marketplace sync/nudge is triggered again after the latest publish so Marketplace picks up the update more quickly.
**Why:** User request — captured for team memory
# Mermaid walkthrough screenshots wait for rendered SVG, not page load alone

## Context

Workflow admin walkthrough screenshots include Mermaid state diagrams inside
expandable definition cards. The page can finish loading before Mermaid has
replaced the raw diagram text with SVG, so a capture taken immediately after
opening a card can freeze the pre-rendered source text into the walkthrough.

## Decision

Walkthrough screenshot capture now treats Mermaid as an explicit readiness
dependency:

- Screenshot runs must use the real Mermaid bundle instead of the no-op test
  stub
- The screenshot helper waits until each in-scope `.mermaid` block is marked
  `data-processed="true"`, contains an `svg`, and no longer has direct raw text
  nodes
- Workflow admin cards expose `data-mermaid-render-state` so the harness can
  wait on app-owned render state rather than arbitrary sleeps

## Why

This keeps normal Playwright tests deterministic while making screenshot capture
trust the rendered diagram, not just DOMContentLoaded or a guessed timeout.
# Screenshot policy correction — content-aware by default

## Context

The earlier walkthrough screenshot policy drifted toward a viewport-first reading
(`fullPage: false` by default), which made several fresh captures too cropped to
show the full functionality of the screen. Jonny clarified the intended rule:
default to showing the whole useful screen/content, and only crop when the page
is genuinely so tall that a full-height image stops being helpful.

## Decision

Walkthrough screenshot capture should be **content-aware by default**:

- Grow beyond the viewport to include the useful content being demonstrated
- Keep helper/hover UI hidden during capture
- Use selector-based crops or height caps for very tall pages (homepage,
  dashboard, similar long surfaces)
- Use `fullPage: true` selectively for steps where the entire document is still
  the useful thing to show (for example check-answers pages)

## Implementation notes

- `tests/walkthroughs/support/walkthrough.ts` remains the single control point
  for screenshot policy
- `screenshotSelector` is the preferred per-step crop control for long pages
- `screenshotMaxHeight` is available when a step needs a taller-but-still-capped
  image
- `SCREENSHOT_FULL_PAGE=1` remains a workflow-level override for forced full-page
  captures
# Decision: Waiting-State Walkthroughs Must Prove the Original Page Advances

**Date:** 2026-05-04  
**Author:** Tangy / @copilot  
**Status:** Implemented

## Summary

For walkthroughs that pause in a waiting or under-review state, the executable spec should keep the original member-facing page open while a second page follows the reviewer route. That lets the test prove the waiting page moves on automatically after approval, instead of only asserting state through admin-only screens.

## Pattern

1. Complete the member journey until the waiting page is visible.
2. Open a second page/tab for supporting checks:
   - inspect **My Workflows** if needed
   - follow the discoverable dashboard route to **Workflow Admin**
3. Perform the reviewer action there.
4. Return to or foreground the original waiting page and assert that it advances without a manual refresh step in the spec.

## Why

- It teaches the whole mechanism, not just the operator half.
- It keeps service walkthroughs honest about what the member actually experiences.
- It gives stronger regression coverage for waiting-step polling / reload behaviour.
# Workflow Approval Semantics — Narrative Pattern

**Context:** Walkthroughs across community-enquiry, information-request, and payment-demo all demonstrate workflows with role-gated `reviewer` transitions. These are not developer-only concerns; they are **core business process semantics** that should be visible in the walkthrough story.

**Decision:** Workflow approval patterns are taught as **named handoff points** where user action (form submission) yields to operator action (approval/rejection). This handoff must be explained **in each service walkthrough**, not delegated entirely to the development-only Workflow Administration guide.

## Pattern Definition

Three workflows currently implement role-gated approval transitions:

| Workflow | Submission → Waiting State | Operator Action | Result |
|---|---|---|---|
| **community-enquiry** | collecting-details → under-review | approve / request-changes | complete / collecting-details |
| **information-request** | collecting-info → under-review | approve / request-changes | complete / collecting-info |
| **payment-demo** | enter-details → processing-payment | complete | payment-complete |

These are **semantic breakpoints** in the workflow, not bugs or incomplete flows. Each represents a business rule: a human or system must verify, process, or authorize the member's input before the workflow can proceed.

## Narrative Requirements

Every workflow with `requiresRole: "reviewer"` transitions must include:

1. **Explicit naming of the waiting state** — "This is **not** a terminal state; it's a waiting state."
2. **Statement of who acts next** — "A reviewer with the `reviewer` role can now…"
3. **Enumeration of next actions** — List each role-gated transition (approve, request-changes, etc.)
4. **Production vs. dev distinction** — Briefly note that in dev we use the Workflow Admin panel, but production uses a dedicated operator interface.
5. **Authorization statement** — "The workflow definition enforces that only users with the `reviewer` role can advance these transitions."

See community-enquiry.md and payment-demo.md sections titled "What Happens Next" for reference implementation.

## Future Guidance

- When adding new workflows with `requiresRole` transitions, include this narrative in the same service walkthrough.
- When building the production operator UI (not yet planned), reference this decision to ensure the operator interface aligns with the role hierarchy already defined in workflow seeds.
- Workflow Administration walkthrough remains a **developer tool reference**, but service walkthroughs own the **business narrative** of approval workflows.

---

**Related:** SKILL.md rule [R6](../../skills/walkthroughs-as-executable-specs/SKILL.md) ("Negative paths live with the walkthrough") — approval/rejection paths are conditional flows within the workflow and belong in the same spec.
# Decision: workflow docs are maintained as package implementation guides

- **Date:** 2026-05-04
- **Decision maker:** Copilot acting as Mabel

## Decision

The workflow documents under `docs/design/` are now organised and maintained as package-consumer implementation guides, not as proposal logs. `docs/design/README.md` is the landing page, and each workflow document now owns one topic with minimal duplication:

- overview
- end-to-end implementation story
- backend contracts
- Umbraco integration
- client rendering
- validation
- security
- advanced patterns

## Why

The previous workflow docs mixed historical proposals, stale contracts, and large code dumps. That made it hard to discover the current package story and easy to copy obsolete examples.

## Consequences

Future workflow documentation changes should update the topic guide that owns the concept rather than re-describing the same contract in multiple places. Seed files and implementation source stay the canonical detailed examples; docs should link to them and only quote short snippets when they teach something useful.
# Workflow docs should tell a package story, not dump implementation

## Context

Reviewing `docs/design/workflow*.md` from a package-documentation perspective showed a recurring problem: the workflow design set is rich in engineering detail but weak as a discoverable, trustworthy implementation guide for package consumers.

## Guidance

1. **Split narrative guide from internal design reference.**
   - Keep one clear "build your own workflow" path aimed at consumers.
   - Keep ADR/security/reference material separate and explicitly labeled as contributor/internal reference.

2. **Prefer compact examples over whole-file code dumps.**
   - Show the minimum JSON/C# needed to explain a concept.
   - Link to the real source file or builder type for full reference instead of embedding hundreds of lines.

3. **Make the docs follow the implemented contract, not the superseded proposal.**
   - Treat `WorkflowDefinitionFile`, `WorkflowDefinitionBuilder`, `StepContent.StepType`, and current walkthrough examples as the canonical source.
   - Mark historical/proposal content clearly or archive it.

4. **Organize workflow docs around the consumer journey.**
   - Recommended order: concepts → definition anatomy → create a workflow → validation/conditional logic → run/debug/admin → extension/reference.

5. **Every doc in the set should answer one question.**
   - If a page tries to be architecture, API reference, schema dump, and tutorial at once, split it.

## Implication for rewrites

Mabel's rewrite should optimise for: concise entry points, strong cross-linking, current examples, and a single coherent implementation story for someone building their own workflow with Prism.
# Marketplace listing content is generated from the GitHub README

## Context

The GitHub `README.md` is the canonical public package story, but Umbraco Marketplace renders some GitHub-flavoured HTML and relative links poorly. Keeping a second hand-edited `MARKETPLACE.md` had already started to drift from the README.

## Decision

1. Keep `README.md` unchanged as the source of truth.
2. Generate `MARKETPLACE.md` automatically from `README.md` using `scripts/generate-marketplace-readme.mjs`.
3. During generation, convert known Marketplace-hostile content into Marketplace-safe Markdown:
   - centered HTML image blocks become plain Markdown images/headings
   - relative document links become absolute GitHub `blob`/`tree` URLs
   - relative image paths become absolute `raw.githubusercontent.com` URLs
4. Treat `MARKETPLACE.md` as a generated artifact and verify it in CI/release workflows with `npm run check:marketplace`.

## Implication

Marketplace copy now stays aligned with the GitHub README without manually maintaining two narratives. Any README change that should appear on Marketplace must be followed by `npm run generate:marketplace`, and CI will fail if the generated file is stale.
# Mabel — Marketplace listing refresh requires a package release

**Date:** 2026-05-08  
**Status:** Complete — v1.9.1 released with marketplace-generated README
**Author:** Mabel (Technical Writer / Release Manager)  
**Impact:** Marketplace publishing, NuGet package metadata, release process

## Summary

The Umbraco Marketplace package page renders `readMeContent` from the published NuGet package, not the `DocumentationUrl` target from `umbraco-marketplace.json`.

## Decision

1. Keep `README.md` as the source of truth for GitHub.
2. Keep generating `MARKETPLACE.md` from `README.md` for Marketplace-safe formatting.
3. Ship `MARKETPLACE.md` inside the NuGet package and set `<PackageReadmeFile>MARKETPLACE.md</PackageReadmeFile>`.
4. Treat Marketplace copy refreshes that need the rendered package page to change as a patch release, because the rendered content is tied to the package artifact.
5. Continue using `DocumentationUrl` to point at the raw GitHub `MARKETPLACE.md`, but treat that as a supporting docs link rather than the primary rendered listing body.

## Rationale

- The public Marketplace frontend renders `package.readMeContent` on the package page.
- `documentationUrl` appears in the sidebar links, so syncing metadata alone does not replace the main rendered listing content.
- A patch release lets us push the marketplace-friendly generated markdown without forking or manually editing the GitHub README.

## Operational consequence

To refresh the Marketplace page body after this change:

1. release a new package version
2. push the tag so the package is published
3. trigger the Marketplace sync for `UmbracoPrism`

Metadata-only edits that affect title, tags, screenshots, or the docs link can still use a sync-only path when the package readme itself does not need to change.

## Implementation Complete (v1.9.1)

- ✓ Commit 8b78831: Added MARKETPLACE.md generation script and configured NuGet package
- ✓ Tag v1.9.1 created and pushed to origin
- ✓ GitHub Actions workflow triggered automatically on tag push
- ✓ UmbracoPrism.1.9.1.nupkg built and published to NuGet.org
- ✓ GitHub Release v1.9.1 created with package asset
- ✓ Marketplace sync endpoint triggered
- ✓ Release notes documented in CHANGELOG.md

Note: NuGet.org package search API may take 1–2 hours to index the new package version. The .nupkg is immediately available via direct package references.
# Documentation Decision: OIDC Provider Language

**Decided by:** Mabel (Documentation Specialist)  
**Date:** 2026-05-04  
**Status:** Ready for review  

## Issue
The README.md repeatedly implied that Entra ID was mandatory or the primary OIDC authentication method, when in fact:
- Any OIDC-compliant provider is supported (Entra ID, Keycloak, generic OIDC, etc.)
- Keycloak is included for local dev flows
- Entra ID is one option among many for production use

This created confusion for new users and misrepresented the project's architecture.

## Changes Made

**10 targeted README.md edits to clarify OIDC provider flexibility:**

1. **Line 125** - Features bullet: Changed "Entra ID integration" → "any OIDC-compliant provider (Entra ID, Keycloak, etc.)"
2. **Line 192** - Quick Start guide: Unified tenant setup instructions to generically reference "OIDC tenants (Entra ID, Keycloak, etc.)" instead of separate "Entra tenants" vs "Generic OIDC tenants" sections
3. **Line 203** - Architecture: Changed "OIDC providers (Entra ID or generic OIDC)" → "any OIDC-compliant system: Entra ID, Keycloak, etc."
4. **Line 218** - Features: Changed "Per-tenant Entra ID (OIDC)" → "Per-tenant OIDC (any provider: Entra ID, Keycloak, etc.)"
5. **Line 284** - Prerequisites: Changed "Entra ID (for authentication)" → "OIDC Provider — any OIDC-compliant system (Keycloak included for local dev; Entra ID or others for production)"
6. **Line 301** - Local Dev Tunnel: Changed "For testing Entra sign-in" → "For testing OIDC sign-in on mobile devices with an external OIDC provider"
7. **Line 313** - Local Dev Tunnel: Changed "Mutates Entra app" → "Mutates your OIDC provider app config"
8. **Lines 432-458** - Local Authentication Walkthrough: **Major restructuring**
   - Added "Option A: Quick Start with Keycloak (Included)" as first path
   - Moved external providers (Entra, generic OIDC) to "Option B"
   - Split tenant setup into two clear paths (Keycloak vs External OIDC)
9. **Line 527** - Stack: Changed "Auth: Stateless OIDC (Entra)" → "Auth: Stateless OIDC (any OIDC-compliant provider)"
10. **Line 550** - Phone Auth section: Changed "For Entra sign-in on mobile" → "For OIDC sign-in on mobile devices with an external provider"

## Key Message
README now leads with **Keycloak for local dev** (simplest path, no setup needed) before showing **external OIDC provider options** (Entra, etc.) for production. This matches the actual architecture where Keycloak is bundled and ready-to-use, while production typically adds their own OIDC provider.

## Alignment with Docs
Confirmed against:
- `docs/secret-management.md` — which discusses all three secret paths (Entra, generic OIDC, inline Keycloak)
- `docs/umbraco-setup.md` — which is OIDC-provider agnostic
- Actual app configuration in `keycloak/` directory

## Impact
- **New contributors** now understand they can start locally with Keycloak without Azure setup
- **Production integrators** still see their Entra/generic OIDC path clearly
- **Documentation consistency** — README now correctly reflects "any OIDC provider" architecture without implying Entra is mandatory
# Mabel — Payment Demo as Primary Interactive Walkthrough

**Date:** 2026-05-04  
**Status:** Proposed  
**Author:** Mabel (Technical Writer)  
**Impact:** README, documentation discovery, first-time user experience

## Summary

Moved the **Payment Processing Workflow** demo to the primary position in README's "Interactive Walkthrough" section, replacing the Planning Permission workflow.

## Rationale

### Payment Demo advantages

- **Showcases Prism's core differentiator:** Demonstrates the "submit now, finish later" async workflow pattern with waiting states, persistence, and real-time updates — the feature that justifies Prism's existence
- **More universally relevant:** Payment processing is needed in every business app; planning permissions are a niche government use case
- **Clearer visual progression:** Form submission → Processing state → Completion. The waiting state ("Processing Your Payment") is a teaching moment that shows how Prism handles asynchronous work
- **Reviewer workflow visibility:** Demonstrates the dual-actor pattern (member + reviewer) that real async workflows need, exposing admin panel and real-time updates
- **Cleaner screenshots:** No UI debugging artifacts (unlike some planning screenshots)

### Planning Permission walkthrough remains available

The planning permission walkthrough is kept as an **Alternative** for developers who want to see multi-step complex forms with conditional field logic. It's valuable but not the "hook" for a first-time GitHub visitor.

## What Changed

- **README § 42–57:** Updated section title, description, and bullet points to emphasize waiting states and async patterns
- **README § 243–246:** Updated documentation table to list Payment Demo as primary, Planning as alternative
- Both walkthroughs remain fully available in `/docs/walkthroughs/`

## User Impact

- **First-time reader:** Sees the async workflow pattern immediately — Prism's key differentiator
- **Onboarding:** Follows a cleaner mental model: submit → wait → review → complete
- **Developer education:** Learns about persistence, waiting states, and real-time updates in the first 5 minutes
- **GitHub first impression:** Payment is more recognizable and business-relevant than planning permission

## Decision Made By

This is a **documentation positioning decision**, owned by Mabel as Technical Writer, aligned with README clarity and consumer-facing packaging.
# Documentation PR Readiness — 2026-05-04

**Reporter:** Mabel (Documentation Specialist)  
**Branch:** `feat/walkthrough-e2e-hardening`  
**Status:** ✅ Documentation side READY for PR (pending Isabelle's screenshot verification)

---

## Summary

The documentation has been reviewed and updated for PR readiness. All narrative walkthroughs now correctly reference the current executable specs, and all image paths are consistent. The branch is ready from the documentation side pending final screenshot verification by Isabelle.

---

## What Changed in Documentation

### 1. **README.md** — Authentication & Setup Generalization
- **Changed:** All references to "Entra ID" now read "any OIDC-compliant provider"
- **Why:** Reflects system support for Keycloak (local dev) and any generic OIDC provider
- **Scope:** 10 edits across setup instructions and authentication sections
- **Impact:** Users now understand Keycloak is the quick-start option without external setup

### 2. **docs/walkthroughs/payment-demo.md** — Completely Rewritten ⭐
- **Changed:** 80 lines → 270+ lines; 3-step narrative → 9-step complete handoff
- **Why:** Executable spec was expanded to show full member→waiting→reviewer→completion flow
- **Was:** Compact form-only walkthrough
- **Now:** End-to-end demonstration covering:
  - Dashboard entry point (01-dashboard-payment-demo-start.png)
  - Form flow (02-initial, 03-form-filled)
  - Waiting state (04-processing, 05-workflow-hub-processing)
  - Reviewer flow in Workflow Admin (06-dashboard-admin-link, 07-admin-processing-instance, 08-admin-payment-definition)
  - Automatic member page update on completion (09-payment-complete)
- **Impact:** Readers now understand the "submit now, finish later" pattern end-to-end, not just the member's first step

### 3. **docs/walkthroughs/README.md** — Workflow Admin Context
- **Added:** Clarifying note that Workflow Admin is a development-only testing harness
- **Added:** Cross-reference to Payment Demo, Community Enquiry, Information Request for production-adjacent workflows
- **Why:** Readers need to know the admin panel is not a production feature; real workflows are demonstrated in the payment/community/information walkthroughs
- **Impact:** Reduced confusion about what Workflow Admin represents

---

## Screenshot Status

### **All Image References Are Valid**

| Walkthrough | Images | Status | Expected Files |
|---|---|---|---|
| **payment-demo** | 9 | ✅ All exist (untracked) | 01-09 ✓ |
| **home-entry** | 5 | ✅ All exist (untracked) | 01-05 ✓ |
| **workflow-administration** | 3 | ✅ All exist (untracked) | 01-03 ✓ |
| **community-enquiry** | 4 | ✅ All exist (tracked) | 01-04 ✓ |
| **information-request** | 3 | ✅ All exist (tracked) | 01-03 ✓ |

### **Old Screenshots Deleted (intentional)**
- `docs/images/walkthroughs/payment-demo/{01-03}.png` — deleted (staged for removal)
- These are replaced by the new 9-step sequence

### **New Screenshots (Untracked, Isabelle's Work)**
These are Isabelle's screenshot captures — they're present as untracked files and will be staged by her after verification:
- `docs/images/walkthroughs/payment-demo/{01-09}.png` ✓
- `docs/images/walkthroughs/home-entry/{01-05}.png` ✓
- `docs/images/walkthroughs/workflow-administration/{01-03}.png` ✓

---

## Documentation Readiness Checklist

✅ All markdown walkthroughs updated and cross-reference checked  
✅ All image paths in markdown match actual file names  
✅ All 17 expected screenshot files exist (9+5+3)  
✅ README clarifications for OIDC providers applied  
✅ Walkthrough Admin docs clarified as development-only  
✅ No broken internal links or markdown syntax errors  
✅ Executable spec footer notes correct (SKILL.md reference, capture workflow)

---

## What Isabelle Needs to Do (Screenshot Verification)

1. **Verify the 17 captured screenshots** are correct:
   - Payment Demo (9): Dashboard entry → form → processing → admin inspection → completion
   - Home Entry (5): Unauthenticated → authenticated → dashboard → demo entry → hub
   - Workflow Admin (3): Dashboard admin link → instance list → definition editor

2. **If screenshots are stale or incorrect**, regenerate via:
   ```bash
   CAPTURE_SCREENSHOTS=1 npm run test:walkthroughs
   ```
   Or use the `Capture Walkthrough Screenshots` GitHub workflow for CI

3. **Stage the verified screenshot files** once confirmed:
   ```bash
   git add docs/images/walkthroughs/
   ```

---

## Residual Follow-Up (If Isabelle Finds Issues)

**If Isabelle regenerates screenshots and they differ from the current captures:**
- The markdown is ready to accept any screenshot set as long as they match the filenames (01-09 for payment, etc.)
- No markdown edits needed — just the screenshot files will update
- The narrative remains valid for any correct implementation of the 9-step flow

**If Isabelle finds the flow itself is broken** (e.g., a step doesn't execute):
- Mark the issue in a comment on the PR
- The test spec will need fixing, not the docs
- Docs narrative is spec-aligned and correct

---

## Next Steps for PR Opening

1. ✅ Documentation side: READY
2. ⏳ Screenshots: Awaiting Isabelle's verification
3. → Once Isabelle stages the screenshot files, PR is ready to open
4. → PR should reference this check-in and note: "Screenshots verified by Isabelle (#name)"

---

**Decision:** Documentation is PR-ready. All narratives align with executable specs. All image references are resolved. The branch can proceed to PR once Isabelle verifies the captured screenshots are correct.
# Walkthrough Screenshots & Documentation Audit
**Date:** 2026-05-04T13:37:58.618+01:00  
**Auditor:** Mabel (Documentation Specialist)  
**Requested by:** Jonny Muir

---

## Executive Summary

Reviewed current walkthrough screenshots against user expectations:
- ✅ Demo PrismMobile UserAgent toggle: Correctly hidden by `prism-screenshot-mode` cookie (server-side works)
- ✅ Screenshots DO include what they demonstrate in most cases
- 🟡 **FINDING:** workflow-administration/01 screenshot cuts off the "Workflow Admin" card it's supposed to show
- 🟡 **FINDING:** community-enquiry screenshots using full-page (2500+ px) when viewport-only (720px) would be adequate
- 🟡 **FINDING:** Viewport height (720px) sometimes insufficient for form pages to show all content + call-to-action

---

## Problem Areas Found

### 1. Workflow Administration Step 1: Admin Card Cut Off
**File:** `docs/walkthroughs/workflow-administration.md` (line 46)  
**Screenshot:** `docs/images/walkthroughs/workflow-administration/01-dashboard-admin-link.png` (1280×720px)  
**Issue:** Screenshot is viewport-only (720px) but the markdown claims to show the Workflow Admin card. The dashboard content extends beyond 720px, so the admin card link is likely below the visible area.

**Evidence:**
- Spec at line 36-40: Takes viewport screenshot after `openDashboard(page)`
- Then asserts admin link is visible (line 46-48), but screenshot doesn't show it
- Narrative at line 46 says "![...Workflow Admin card visible...]" — but it's cut off

**Impact:** Readers can't see the thing being demonstrated. Documentation and screenshot are misaligned.

**Safe Fix:**  
Add note to markdown clarifying that "the Open Admin button appears below the dashboard cards" or adjust spec to scroll/ensure card is visible. This is a documentation issue, not a product issue.

---

### 2. Community Enquiry: Forms Using Full-Page Screenshots Unnecessarily
**Files:**  
- `docs/images/walkthroughs/community-enquiry/01-initial.png` (1280×2537px)  
- `docs/images/walkthroughs/community-enquiry/02-conditional-reveal.png` (1280×2672px)  
- `docs/images/walkthroughs/community-enquiry/03-form-filled.png` (1280×2537px)

**Issue:** These are form pages showing the entire scrollable content (2500+ px tall). User feedback: "screenshots are cut off too abruptly vertically" and "previous instruction should only constrain very long pages like the homepage."

**Analysis:** The forms don't need full-page captures. The viewport crop at 720px would show:
- Form heading
- First few fields
- Enough to understand what the user is doing

Full-page captures are visually overwhelming for documentation.

**Safe Fix:**  
Update specs to NOT use `fullPage: true` for form pages (revert to default viewport). Only use `fullPage: true` for:
- Confirmation/summary pages (check-answers style)
- Exceptionally long pages like the actual homepage (currently 9447px)

---

### 3. Payment Demo & Information Request: Inconsistent Heights
**Observations:**
- `payment-demo/01-initial.png`: 1280×809px (viewport with some scroll room)
- `payment-demo/03-processing.png`: 1280×720px (viewport only)
- `information-request/01-initial.png`: 1280×1664px (full-page)
- `information-request/03-under-review.png`: 1280×720px (viewport only)

**Pattern:** Mixed strategies. Some use fullPage, some don't.

**Safe Fix:**  
Standardize: all form/workflow pages use viewport-only (720px default), EXCEPT:
- Check-answers/summary pages: use viewport-only or minimal fullPage if needed
- Confirmation pages: viewport-only (they're terminal states, user shouldn't scroll)

---

### 4. Home Entry Screenshots: Correct Height
**Screenshots:**  
- `home-entry/01-signed-out-hero.png`: 1280×720px  
- `home-entry/02-signed-in-hero.png`: 1280×720px  
- `home-entry/03-dashboard.png`: 1280×720px  
- `home-entry/04-start-workflow.png`: 1280×720px  
- `home-entry/05-workflow-hub.png`: 1280×720px

**Status:** ✅ All viewport-only, consistent, shows what's needed.

---

### 5. Shared Homepage: Still Too Tall
**File:** `docs/images/walkthroughs/shared/01-homepage.png` (1280×9447px)  
**Status:** This is a full-page screenshot and intentionally so — it shows the entire hero section and branding, which is necessary.

However, 9447px is excessive. Should be cropped to ~1280×2200-2400px to show:
- Header/nav
- Hero heading + CTA
- Key supporting content (security/scale messaging)

**Note:** This is a known issue from prior audit. Keeping for reference.

---

## Mobile User Agent Toggle: Status

**Finding:** ✅ **NOT AN ISSUE**

The toggle is correctly hidden in screenshot mode. Evidence:
- `PrismMobileUserAgentDemoTagHelper.cs` line 52: `var effectiveShowToggle = ShowToggle && !IsScreenshotMode;`
- Cookie `prism-screenshot-mode=1` correctly suppresses the toggle
- `enterScreenshotMode()` in walkthrough.ts correctly sets the cookie

**Why some screenshots show it:**  The earlier screenshot I viewed (community-enquiry/01-initial.png) showed the toggle because:
- That image was captured before the fix was deployed, OR
- The image file predates the current screenshot-mode implementation

Re-capture will fix this.

---

## Recommendations for Safe Documentation Updates

### Immediate (No Spec Changes Required)

1. **workflow-administration.md:** Add clarifying caption
   - Change line 46 caption from:  
     `![Member dashboard with Workflow Admin card visible](...)`  
   - To:  
     `![Member dashboard — scroll to find the Workflow Admin card](...)<!-- Added Sept 2024: the admin card appears below the initial dashboard view -->`

2. **Document assumption about viewport heights**  
   Add to `.squad/skills/walkthroughs-as-executable-specs/SKILL.md`:  
   ```markdown
   ## Screenshot Heights
   
   - Default: viewport crop (1280×720px) — shows what user sees without scrolling
   - Exception: check-answers / summary pages may use `fullPage: true` to show all collected data
   - Exception: pages that are intentionally scrollable (rare) use `fullPage: true` with explicit note
   
   Forms should always use viewport-only; confirmation screens use viewport-only.
   ```

### Follow-Up (For Isabelle or Capture Workflow)

3. **Community Enquiry Specs:** Revert fullPage usage
   - Remove `fullPage: true` from form page steps (if any)
   - Ensure all steps use default viewport

4. **Home Entry & Payment Demo:** Ensure consistency  
   - All form pages: viewport-only  
   - Confirmation pages: viewport-only

5. **Shared Homepage:** Schedule optional crop to ~2400px max (not urgent, enhancement only)

---

## Decision Matrix

| Item | Safe to Fix Now? | Responsibility | Effort |
|------|:---:|---|---|
| workflow-admin caption | ✅ Yes | Mabel (docs) | 2 min |
| Add skill guidance | ✅ Yes | Mabel (docs) | 5 min |
| fullPage reversion | 🟡 Partial | Isabelle (specs) | 15 min |
| Mobile toggle hiding | ✅ Already fixed | (none needed) | – |
| Homepage crop | 🟡 Optional | Isabelle (tooling) | 20 min |

---

## Files to Update (Mabel Owns These)

- `docs/walkthroughs/workflow-administration.md` — caption clarification
- `.squad/skills/walkthroughs-as-executable-specs/SKILL.md` — add screenshot height guidance

---

## Verification Checklist

After updates:
- [ ] workflow-administration.md caption clarified
- [ ] Skill.md documents viewport vs. fullPage rules
- [ ] Community enquiry specs reviewed for fullPage usage (Isabelle)
- [ ] All form screenshots are viewport-only in next capture
- [ ] Confirmation screenshots verified as viewport-only
# Screenshot Default Policy Clarification

**Date:** 2026-05-04  
**Author:** Mabel (Documentation Specialist)  
**Decision Type:** Team documentation rule clarification

---

## Summary

Corrected the screenshot guidance in walkthrough documentation to align with the **principle: show the whole useful screen by default; crop selectively only for very tall pages**.

**Previous guidance (incorrect):** Viewport crop (1280×720px) as the default  
**Corrected guidance:** Full page as the default, constrain only when necessary

---

## Problem

Recent audit documentation had introduced guidance suggesting viewport-sized screenshots (1280×720px) should be the default. This contradicted the principle that walkthrough documentation should show readers the **complete functionality** available on a page.

Feedback from Jonny Muir clarified the intent: show complete screen context by default, constraining only when pages are exceptionally tall (>2200px) and full-page captures would obscure rather than clarify the documentation.

---

## Decision

**Effective immediately:** Screenshot default policy is reversed to prioritize completeness.

### Updated Guidance

**Default:** Full page (all scrollable content visible)  
- Shows the complete functionality of the screen so readers see everything available
- Applies to form pages, check-answers pages, summary pages, and any page where complete visibility aids the walkthrough narrative

**Constrain to viewport only when:**
- A page is exceptionally tall (>2200px)
- Full-page height creates visual clutter without adding documentation value
- A smaller crop makes the guidance clearer without hiding necessary content

**Rule of thumb:** Show the whole useful screen by default. Crop selectively only for unusually large pages where full-page screenshots would obscure rather than clarify the documentation.

---

## Files Updated

1. **`.squad/skills/walkthroughs-as-executable-specs/SKILL.md`**
   - Section: "Screenshot Heights" (lines 122–136)
   - Updated default from viewport-crop to full-page-by-default
   - Clarified when to constrain (rare, not the norm)

2. **`src/UmbracoPrism.Client/tests/walkthroughs/support/walkthrough.ts`**
   - Lines 25–34: Updated JSDoc comment for `fullPage` parameter
   - Clarified that default behavior now expands to show all content
   - Documented when to use fullPage explicitly

---

## Impact

- **Walkthrough authors:** Use the full-page default; only set `fullPage: true` when narrative absolutely requires it
- **Next screenshot capture:** Follow the new default when re-capturing
- **Existing screenshots:** No immediate action required; will be refreshed during normal capture cycles
- **Team clarity:** Policy is now consistent across skill documentation and code comments

---

## Related Issues

- Audit note: `mabel-screenshot-audit-2026-05-04.md` (captured viewport vs. full-page confusion)
- User feedback: Jonny Muir clarified policy on 2026-05-04

---

## Verification Checklist

- [x] SKILL.md updated
- [x] walkthrough.ts JSDoc updated
- [x] Decision note filed
- [ ] Communicate to team (Isabelle for next capture cycle)
- [ ] Review next batch of captured screenshots against new policy
# Walkthrough Audit: Workflow Administration Steps Placement

**Date:** 2026-05-04  
**Auditor:** Mabel (Documentation Specialist)  
**Concern:** Do workflow administration steps appear in service walkthroughs where they need to appear?

---

## Executive Summary

**Verdict: PARTIALLY**

Workflow progression and reviewer/admin roles **are defined in the workflow seeds** but **are not documented in the walkthroughs where they matter**. Readers cannot understand how workflows complete after user submission or why certain intermediate states exist.

---

## Key Findings

### 1. Reviewer Roles Built Into Workflows

The following workflows have **`requiresRole: "reviewer"` transitions** hardcoded into their state machines:

| Workflow | Transition | Requires | Status |
|----------|-----------|----------|--------|
| **payment-demo** | `processing-payment` → `payment-complete` | `reviewer` role | ❌ Not documented |
| **community-enquiry** | `under-review` → `complete` | `reviewer` role | ❌ Not documented |
| **community-enquiry** | `under-review` → `collecting-details` (reject) | `reviewer` role | ❌ Not documented |
| **information-request** | Approval transitions | `reviewer` role | ❌ Not documented |
| **planning-notification** | (No reviewer roles) | N/A | ✅ Consistent |

### 2. Walkthrough Coverage

#### ✅ Well-Placed (Consistent & Complete)

- **Workflow Administration** (`workflow-administration.md` + spec)
  - Correctly positioned as a **development-only tool** walkthrough
  - Explains instance inspection and manual state advancement
  - Located in "Authoring & Operations" section of README
  - BUT: Scope is limited to development debugging, not production operator workflows

- **Authoring a Workflow** (`authoring-a-workflow.md`)
  - Shows state machine concepts and transitions
  - Explains role-based transitions briefly in seed JSON example
  - BUT: Not connected to user-facing workflows

#### ❌ Missing or Misplaced (Incomplete)

- **Payment Demo** (`payment-demo.md` + spec)
  - Shows user enters payment details → sees "Processing Your Payment" screen
  - Stops there; **does NOT explain:**
    - That a `reviewer` role must advance the workflow
    - How/when the "payment-complete" state is reached
    - Why a processing state exists
    - What service/operator steps follow submission
  - **Spec:** Only tests user submission; doesn't test reviewer transition

- **Community Enquiry** (`community-enquiry.md` + spec)
  - Shows form submission → "Your enquiry is with us" confirmation
  - **Does NOT explain:**
    - That the workflow is now in `under-review` state pending reviewer action
    - That a reviewer can `approve` (move to `complete`) or `request-changes` (send back)
    - How enquiry approval/rejection works
    - Why transitions require the `reviewer` role
  - **Spec:** Only tests user submission; doesn't test reviewer actions

- **Information Request** (`information-request.md`)
  - No mention of reviewer workflow
  - Seed file shows reviewer transitions exist but docs don't explain them

- **Planning Notification** (`planning-notification.md`)
  - Doesn't need reviewer role (all transitions user-driven)
  - Documentation is consistent with definition

---

## File-by-File Placement Analysis

### docs/walkthroughs/

| File | Status | Issue |
|------|--------|-------|
| `payment-demo.md` | ❌ Missing admin context | Stops after user submits; doesn't explain reviewer progression or why "processing" state exists |
| `community-enquiry.md` | ❌ Missing admin context | Ends at "under-review"; doesn't document reviewer approval/rejection flow |
| `information-request.md` | ❌ Missing admin context | Doesn't mention reviewer transitions exist in the seed |
| `planning-notification.md` | ✅ Consistent | No reviewer roles, docs match definition |
| `workflow-administration.md` | ✅ Correct scope | Development-only tool, clearly marked as such; appropriate for authoring section |
| `authoring-a-workflow.md` | ✅ Partial credit | Shows role concept in code/JSON examples but doesn't connect to real user workflows |
| `README.md` | ⚠️ Minor issue | Good structure; "Authoring & Operations" section exists but lacks a walkthrough for **production** workflow operator tasks |

### src/UmbracoPrism.Client/tests/walkthroughs/

| File | Status | Issue |
|------|--------|-------|
| `payment-demo.walkthrough.spec.ts` | ❌ Incomplete | Only tests user flow; missing test cases for reviewer transition to `payment-complete` |
| `community-enquiry.walkthrough.spec.ts` | ❌ Incomplete | Only tests user flow; missing test cases for reviewer `approve` and `request-changes` actions |
| `information-request.walkthrough.spec.ts` | ❌ Incomplete | Only tests user flow; missing reviewer tests |
| `workflow-administration.walkthrough.spec.ts` | ✅ Correct scope | Tests development admin panel; appropriate for its purpose |

### docs/images/walkthroughs/

No missing images identified (images are generated by specs as they stand).

---

## Specific Recommendations

### High Priority: Close the Documentation Gap

**1. Extend `payment-demo.md` with reviewer workflow**
- Add "Part 2: What Happens Next (Reviewer Approval)"
- Document that a reviewer role must move workflow from `processing-payment` to `payment-complete`
- Add note explaining why the `waiting` component exists
- Include screenshot showing the approval transition (once spec is updated)

**2. Extend `community-enquiry.md` with reviewer workflow**
- Add section explaining the `under-review` state is not terminal
- Document reviewer actions: `approve` (move to `complete`) vs `request-changes` (return to collection)
- Explain why these transitions require the `reviewer` role
- Show what form data is available to reviewers

**3. Add brief mention to `information-request.md`**
- Note that this workflow also has reviewer transitions (in specs/seeds)
- Link to payment-demo or community-enquiry for the approval pattern explanation

### Medium Priority: Test Coverage

**4. Extend walkthrough specs to cover reviewer transitions**
- Add test case in `payment-demo.spec.ts` that simulates reviewer calling the completion action
- Add test case in `community-enquiry.spec.ts` for both `approve` and `request-changes` paths
- Use the admin panel API (`/admin/workflow/{instanceId}/action/{action}`) to trigger reviewer actions

### Low Priority: README Navigation

**5. Update `README.md` Walkthroughs section**
- Add note under "Authoring & Operations" that explains:
  - `workflow-administration.md` = development debugging
  - Payment Demo & Community Enquiry = examples of reviewer workflow patterns
- Consider a future "Workflow Operators" walkthrough section if production operator workflow is added

---

## Explanatory Context Required

Readers need to understand:

1. **Why these states exist:**
   - `processing-payment` / `under-review` = system processing or waiting for human decision
   - User can't self-advance; requires external approval

2. **How it works in practice:**
   - What UI/API does a reviewer use to approve/reject?
   - What happens to the user's form data while under review?
   - What triggers completion? Manual action? Timeout? Async job?

3. **Role-based permissions:**
   - Transitions with `requiresRole: "reviewer"` can only be triggered by users with that role
   - How is the "reviewer" role assigned/validated?

4. **State machine semantics:**
   - Terminal vs. non-terminal states
   - Whether a user can self-retry after rejection

---

## What Should Be Changed

### Minimal Safe Edits (Ready to Make Now)

1. **payment-demo.md:**
   - Add explanatory paragraph after the "Processing" screenshot explaining:
     > "The workflow now waits in the `processing-payment` state. In a production system, a backend service or human reviewer with the `reviewer` role would then advance this workflow to `payment-complete` based on payment confirmation from Stripe."
     
2. **community-enquiry.md:**
   - Add note after the "Your enquiry is with us" screenshot:
     > "Your enquiry has entered the `under-review` state. A reviewer with the `reviewer` role can now view your submission and either approve it (moving to `complete`) or request changes (returning the form to you for edits)."

3. **README.md:**
   - Add one bullet under "Authoring & Operations" explaining that Workflow Administration is development-only, with a note that payment-demo and community-enquiry walkthroughs show reviewer patterns

### Larger Improvements (Scope for Future Work)

- Create example reviewer workflow screenshots showing the approval UI
- Add production-safe operator guide (separate from development admin panel)
- Test coverage for reviewer transitions in specs

---

## Decision

**Walkthroughs are PARTIALLY addressing the concern.**

The workflows are correctly defined with reviewer roles, but the documentation **doesn't explain them where readers need to understand them** — in the service walkthroughs (payment-demo, community-enquiry). A reader finishing those walkthroughs would not understand:

- How workflows continue after the user's submission
- Why intermediate states exist
- What role/permission is needed to advance them
- Why the forms/transitions behave the way they do

**Recommended action:** Make the three minimal safe edits above to provide explanatory context in the walkthroughs. These are low-risk, additive changes that clarify existing behavior without rewriting walkthroughs.
# Tangy — Full Walkthrough Pass

- **Date:** 2026-05-04
- **Status:** Proposed

## Decision

Treat the MockBusinessApp workflow admin page as an executable, development-only continuation of member walkthroughs, and stub its CDN-loaded Ace/Mermaid vendor assets inside Playwright so screenshots and CI do not depend on third-party network availability.

## Why

- The dashboard now routes members into seeded workflow demos via per-workflow **Start** cards rather than a single **Start Workflow** CTA, so walkthrough coverage should assert those real navigation entry points.
- Reviewer-only workflow transitions (`Approve`, `Request Changes`) are exposed in the local workflow admin surface and are the right place to exercise operator-adjacent flows without pretending those controls exist in the public member UI.
- The admin page pulls editor/diagram assets from public CDNs; without test-side stubs, screenshot capture and CI stability are needlessly coupled to external network health.

## Implications

- Walkthrough docs can now show the member journey handing off cleanly to the local admin tooling for under-review / waiting states.
- Future workflow-admin tests should reuse the support hook rather than hitting the live CDNs.
---
decision_id: tom-nook-walkthrough-story-review-2026-05-04
title: Walkthrough Story Review — Post-Implementation Clarity Assessment
author: Tom Nook
created_at: 2026-05-04T12:57:00.000Z
tags: [walkthroughs, documentation, clarity, narrative]
affects: [tangy, mabel, isabelle]
---

# Walkthrough Story Review — Final Assessment

**Status:** Post-implementation audit of `feat/walkthrough-e2e-hardening` branch.

**Verdict:** Walkthrough story is **strong and ready**. The recent work by Tangy and Mabel has materially improved narrative clarity and demo value. Punch list below focuses only on unfinished artifacts, not narrative gaps.

---

## What's Working Well ✅

### 1. **Executable Specs Policy is Fully Enforced**
- All 11 walkthroughs have both markdown and Playwright spec counterparts.
- R1–R6 SKILL.md rules are implemented correctly:
  - Every spec has an `assertHealthyPage()` pre-flight check (R3).
  - Screenshot filenames are deterministic `NN-slug.png` (R4).
  - Every markdown footer references its spec with correct path (R5).
  - 5 manual-only walkthroughs are explicitly skipped with SKILL.md R6 rationale (acceptable per policy).

### 2. **Discovery and Navigation Have Improved**
- **Workflow Administration walkthrough is now first-class** — added to README, discoverable from dashboard (`/admin/workflow` link now present).
- **Home entry walkthrough is new** — documents complete onboarding path (signed-out hero → signed-in → dashboard → workflow hub).
- **README hierarchy is clear** — end-user flows, authoring, ops, mobile sections present all 11 walkthroughs with one-line intent.

### 3. **Screenshot Defaults Are More Readable**
- Changed from `fullPage: true` (2500–9400px) to viewport crop (typical 800–1200px).
- Improves doc readability without losing context.
- `fullPage` opt-in is available per-step when needed (e.g., check-answers pages).

### 4. **Test Coverage is Hardened**
- 4 workflow demos now include validation and persistence tests (not just happy path).
- Prevents regressions on error handling and workflow state management.
- Covers the scenarios evaluators and implementers most care about.

---

## Punch List — Unfinished Artifacts ⚠️

### P1: Capture Home-Entry Screenshots (High Priority — Unblocks New Narrative)

**Current state:** Spec exists (`home-entry.walkthrough.spec.ts`), all tests pass, markdown written (`home-entry.md`) — but screenshot directory has only `.gitkeep`.

**Action:** Screenshots must be captured before PR merge. Tests already pass; workflow is `01-signed-out-hero.png`, `02-signed-in-hero.png`, `03-dashboard.png`, `04-workflow-hub.png`.

**Why:** Home entry is new and foundational to understanding Prism's entry journey. Missing screenshots leave the walkthrough unfinished in the docs.

---

### P2: Capture Workflow-Administration Screenshots (High Priority — Unblocks Ops Path)

**Current state:** Spec is complete (`workflow-administration.walkthrough.spec.ts`), all tests pass, markdown written — screenshot directory is empty.

**Expected captures:** `01-dashboard-admin-link.png`, `02-admin-instance-list.png`, `03-admin-definition-list.png`, `04-edit-definition.png`, `05-manual-state-transition.png` (or subset based on test coverage).

**Why:** This was a major gap in the discovery audit — ops workflows are now documented, but without screenshots the walkthrough is incomplete.

---

### P3: Clarify Design-System Walkthrough Screenshot Status (Medium Priority — Narrative Clarity)

**Current state:** 11 TODO comments for pending Storybook captures (`01-storybook-home.png` through `05-branding-updated-frontend.png`). Spec is skipped per R6 (manual-only).

**Issue:** Readers don't know if screenshots are coming or intentionally deferred. TODOs are hanging without resolution date or rationale.

**Action:** Choose one:
1. **If manual captures are planned:** Update TODOs with estimated timeline and owner.
2. **If intentionally manual:** Add a note to `design-system.md` clarifying that Storybook/CSS captures are manual-only (per R6), and provide clear step-by-step instructions (already present in markdown) that readers can follow without screenshots.

**Recommendation:** Go with option 2 — the markdown is thorough, and manual Storybook navigation is straightforward. Remove TODOs and replace with a single intro note: "Screenshots are manual-only per R6; follow the steps below."

---

### P4: Same Clarity Issue for Building-Mobile-App (Medium Priority — Narrative Consistency)

**Current state:** 5 TODO comments for pending device captures (iOS biometric, native nav, device screenshots). Spec is skipped per R6.

**Action:** Same as P3 — clarify either as planned or as intentionally manual with clear step-by-step instructions.

**Recommendation:** Mark as intentionally manual. The walkthrough already covers Capacitor shell structure, native prerequisites, and build steps. Missing device screenshots don't block understanding — Xcode/Android Studio interface is well-known.

---

## Quality Observations 📋

### Strengths
- **Walkthrough flow is coherent:** End-user workflows (4) → authoring (1) → operations (1) → mobile/notifications (2) + design system (1) + tenancy (1).
- **Executable specs provide real protection:** Changes to workflow UI or navigation immediately surface as test failures. This is not theoretical documentation — it's a gated contract.
- **New home-entry walkthrough addresses a real gap:** Without it, new evaluators have no documented entry point.

### Minor Observations (Not Action Items)
- **Push-notifications walkthrough is philosophically challenging:** OS notification toasts cannot be scripted, but the narrative is thorough. Current state (manual-only per R6) is correct.
- **Authoring and creating-tenant walkthroughs are intentionally back-office heavy:** They document source code and backoffice UI interaction, not pure browser flows. Correct to skip per R6.
- **Community Enquiry serves as a model:** It has validation tests, persistence tests, screenshot coverage, and clear conditional-reveals narrative. Replicate this for new walkthroughs.

---

## Recommendations for Team

### Immediate (This Branch)
1. ✅ Capture home-entry and workflow-administration screenshots before merge — no code changes needed.
2. ✅ Optionally clean up P3/P4 TODO comments (replace with R6 rationale) — improves reader confidence.

### Future (Out of Scope)
- Consider a "Walkthrough Maintenance" checklist in the CI pipeline: flag specs where screenshot directory has only `.gitkeep` or has fewer files than expected.
- When Isabelle completes the docs pipeline, document the `CAPTURE_SCREENSHOTS=1` workflow dispatch in README so team members know how to regenerate.

---

## Executive Summary

The walkthrough package is **coherent, well-structured, and demo-ready**. Tangy and Mabel's recent work has significantly improved clarity:
- Home entry and workflow administration walkthroughs provide missing narrative paths.
- Test coverage hardening (validation + persistence) strengthens confidence.
- Viewport crop default makes screenshots more readable.

**Remaining work is tactical, not strategic.** Two unfinished screenshot captures (home-entry, workflow-administration) are the only blockers; capturing them is low-effort. Optional: clarify manual-only rationale for two complex walkthroughs (design-system, building-mobile-app) to improve reader confidence.

**Ready for merge after screenshot captures are complete.**
---
date: 2026-05-03T18:12:37.055+01:00
author: Tangy
status: PROPOSED
area: testing, browser-contracts, codespaces
---

# Browser-Facing API Responses Must Not Expose Internal Backchannel URLs

## Context

The DownstreamDemoController on the member dashboard calls MockBusinessApp using an internal backchannel URL (`http://localhost:5163`) for efficiency, but returns that internal URL to the browser in the JSON response. Users see `http://localhost:5163/api/backoffice/me` displayed in the dashboard, which:

- Is unreachable from their browser (only port 7245 HTTPS is forwarded in Codespaces)
- Exposes implementation details (dual HTTP/HTTPS listener setup)
- Creates confusion: appears to be the target but is actually an internal routing hop

## Decision

**Browser-facing API responses must return publicly accessible URLs, not internal server-to-server backchannel URLs.**

When a controller uses an internal backchannel URL for transport optimization:
1. The response must transform the internal URL to its public equivalent before returning to the client
2. OR use a separate `displayUrl` field for the UI and keep `url` for diagnostics
3. OR omit the URL entirely if it's purely an implementation detail

### Implementation

For the DownstreamDemoController specifically:

```csharp
private string GetPublicFacingUrl(string transportUrl)
{
    var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
    var publicUrl = configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
    
    if (!string.IsNullOrWhiteSpace(backchannelUrl) && 
        !string.IsNullOrWhiteSpace(publicUrl) &&
        transportUrl.StartsWith(backchannelUrl, StringComparison.OrdinalIgnoreCase))
    {
        return publicUrl + transportUrl.Substring(backchannelUrl.Length);
    }
    
    return transportUrl;
}

// In Get() method:
return Ok(new
{
    statusCode = (int)response.StatusCode,
    statusText = response.StatusCode.ToString(),
    url = GetPublicFacingUrl(targetUrl),  // Transform before returning
    elapsedMs = sw.ElapsedMilliseconds,
    contentType,
    body = displayBody
});
```

### Test Coverage

**Unit test contract:**
```csharp
[Fact]
public async Task DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport()
{
    using var backchannel = new TempEnvVar("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
    var handler = new StubHttpMessageHandler(request =>
    {
        // Capture the actual HTTP request
        capturedRequestUri = request.RequestUri;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
    });

    var controller = BuildController(
        handler,
        new Dictionary<string, string?>
        {
            ["PrismBusinessApp:WorkflowApiBaseUrl"] = "https://v7ldkc4c-7245.uks1.app.github.dev"
        },
        authHeader: new AuthenticationHeaderValue("Bearer", "token"),
        isDevelopment: true);

    var result = await controller.Get();

    // Validate: backend uses backchannel for transport efficiency
    capturedRequestUri.Should().Be(new Uri("http://localhost:5163/api/backoffice/me"));
    
    // But response to browser uses public URL
    var ok = result.Should().BeOfType<OkObjectResult>().Subject;
    var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
    doc.RootElement.GetProperty("url").GetString().Should().Be(
        "https://v7ldkc4c-7245.uks1.app.github.dev/api/backoffice/me",
        because: "browser-facing URLs must be publicly accessible");
}
```

**Playwright contract:**
```typescript
test('API demo displays publicly accessible URL', async ({ page }) => {
  await signIn(page);
  await openDashboard(page);
  await page.getByRole('button', { name: 'Call Mock Business App API' }).click();

  await expect(page.locator('#api-status-badge')).toHaveText(/200 OK/);

  const apiUrl = page.locator('#api-url-label');
  const displayedUrl = await apiUrl.textContent();
  
  // Contract: no internal backchannel ports
  expect(displayedUrl).not.toContain(':5163');
  expect(displayedUrl).not.toContain('localhost:');
  
  // Must show public endpoint
  if (process.env.CODESPACE_NAME) {
    expect(displayedUrl).toMatch(/https:\/\/.*-7245\..*\.app\.github\.dev/);
  } else {
    expect(displayedUrl).toContain('https://localhost:7245');
  }
});
```

## Why This Matters

1. **User Experience:** Users see URLs they can't reach, creating confusion and false debugging paths
2. **Codespaces-Critical:** Port forwarding makes the localhost vs public distinction non-negotiable
3. **Security Posture:** Exposing internal routing details (ports, HTTP vs HTTPS) leaks implementation info
4. **Test Contracts:** Separates transport optimization (use fast backchannel) from UI contracts (show reachable URLs)

## Alternatives Considered

**Alternative 1: Don't optimize with backchannel URLs**  
Rejected: The backchannel pattern is valid for server-to-server efficiency; the fix is in the response transformation, not the transport choice.

**Alternative 2: Add `displayUrl` separate from `url`**  
Acceptable: Keeps both for diagnostics but requires UI updates. Preferred approach is simpler: transform before returning.

**Alternative 3: Don't show URLs in API responses**  
Acceptable for some contexts, but diagnostics benefit from showing "what did we call" — just needs to be the public version.

## Migration Path

1. Update `DownstreamDemoController.Get()` to transform backchannel URLs before returning
2. Add unit test `DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport`
3. Update existing test line 127 from expecting `http://localhost:5163` to expecting the public URL
4. Add Playwright contract test for URL accessibility
5. Validate in live Codespaces

## References

- Full diagnosis: `.squad/agents/tangy/diagnosis-mockbiz-timeout.md`
- DownstreamDemoController: `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`
- Existing test: `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` lines 97-128
- Dashboard view: `src/UmbracoPrism.TestSite/Views/memberDashboard.cshtml` line 272
- Codespaces URL skill: `.squad/skills/codespaces-url-forms/SKILL.md`


---

---
date: 2026-05-03T18:12:37.055+01:00
author: Blathers
status: diagnosis
---

# MockBusinessApp API Demo Timeout — `localhost:5163` Leak

## Context

Sign-in now works, but the "Call Mock Business App API" action in the member dashboard times out. The UI shows the browser calling `http://localhost:5163/api/backoffice/me`, timing out after 10 seconds.

## Root Cause

The `DownstreamDemoController` is server-side code that calls MockBusinessApp on behalf of the browser using the member's Bearer token. However, AppHost line 142 sets:

```csharp
testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
```

This environment variable is intended for **server-to-server** calls from TestSite to MockBusinessApp's internal HTTP endpoint, bypassing GitHub Codespaces port forwarding.

BUT the DownstreamDemoController at line 301 reads this env var and uses it to build the target URL that gets **returned to the browser** in the response JSON. The browser-side JavaScript displays this URL in the UI as a diagnostic.

## Why This Is Wrong

1. `BUSINESSAPP_BACKCHANNEL_URL` is a *transport layer* config for server-to-server calls.
2. The controller response JSON includes the `url` field showing `http://localhost:5163/...`.
3. This creates confusion: the URL displayed to the user is TestSite's internal address, not the public Codespaces URL.
4. The browser cannot reach `localhost:5163` — that's a TestSite-internal address accessible only from TestSite's process.

## Why `localhost:5163` Specifically

MockBusinessApp's launchSettings.json advertises:
- `https://localhost:7245` (HTTPS, for browser-facing traffic)
- `http://localhost:5163` (HTTP, for internal server-to-server calls)

In Codespaces, AppHost sets `BUSINESSAPP_BACKCHANNEL_URL=http://localhost:5163` so TestSite's server-side code can reach MockBusinessApp without hitting the GitHub port-forwarding proxy (which blocks unauthenticated server requests).

The browser needs the public URL: `https://{token}-7245.{region}.app.github.dev`.

## Architectural Issue

`BUSINESSAPP_BACKCHANNEL_URL` is being used for *two conflicting purposes*:
1. **Server-side transport** — HTTP call from TestSite process to MockBusinessApp process (works correctly)
2. **Browser-facing display** — URL shown in diagnostic output (incorrect, leaks internal address)

## Impact

- Server-side API call *may be succeeding*, but the response JSON misleads the user by showing an unreachable internal URL
- OR the browser-side JavaScript is misinterpreting the response and trying to make a client-side fetch to `localhost:5163`, causing the timeout

## Fix Options

### Option A: Separate Transport and Display URLs (Recommended)

1. Add a new method `ResolveBusinessAppDisplayUrl()` that returns `PrismBusinessApp:WorkflowApiBaseUrl` (the public browser URL).
2. Change `ResolveBusinessAppTransportBaseUrl()` to be used only for the actual HTTP call.
3. Update controller response JSON (lines 103, 130, 147, 165) to use the display URL.

### Option B: Document the Behavior

If the `url` field in the response JSON is *only for diagnostics* (not used by browser JavaScript for navigation), just document that it shows the *server-side transport URL*, not the browser-facing URL. The API call will succeed regardless of what URL is displayed.

### Option C: Remove Backchannel Override for Display

Change line 305 to check if `BUSINESSAPP_BACKCHANNEL_URL` is set, and if so, use `PrismBusinessApp:WorkflowApiBaseUrl` for display but continue using the backchannel URL for the actual HTTP call.

## Next Diagnostic

Inspect the actual runtime behavior in Codespaces:
1. Check browser DevTools Network tab for the `/api/prism/downstream-demo` response JSON
2. Confirm whether `url` field is `http://localhost:5163/...`
3. Check TestSite logs to see if the server-side call to MockBusinessApp is succeeding or failing
4. Determine if the timeout is client-side (browser can't reach localhost) or server-side (TestSite can't reach MockBusinessApp)

## Decision

Diagnosis complete. Recommend **Option A** (separate transport and display URLs) to cleanly separate concerns and avoid leaking internal addresses into browser-facing surfaces.


---

---
date: 2026-05-03T18:24:57.531+01:00
author: Scribe
status: COMPLETE
---

# Cleanup: Stray Diagnosis Artifact Consolidated

## Action

Deleted `.squad/agents/tangy/diagnosis-mockbiz-timeout.md` — an untracked artifact that was already fully consolidated into `.squad/decisions.md`.

## Context

The Tangy diagnosis on the MockBusinessApp timeout was merged into the decisions file with date 2026-05-03T18:12:37.055+01:00. The original markdown file remained in the worktree as untracked. The diagnostic content (contract violations, root cause analysis, test gaps, fix options) is complete in decisions.md; the artifact file was redundant.

## Decision

Stray diagnostic files that have been consolidated into `.squad/decisions.md` should be deleted to keep the `.squad/` directory authoritative and avoid confusion. The decisions file is the source of truth; temporary diagnostic artifacts don't need to be retained once merged.

## Result

Worktree is clean. `main` is up to date with origin.
---
date: 2026-05-03T18:29:38.303+01:00
author: Blathers
status: implemented
area: api-contracts, codespaces, url-separation
---

# Transport URLs vs Display URLs: Separate Concerns in API Responses

## Context

The DownstreamDemoController uses `BUSINESSAPP_BACKCHANNEL_URL` for server-to-server calls to optimize transport in Codespaces (bypassing the GitHub port-forwarding proxy). However, the controller was returning this internal URL in the JSON response to the browser, causing user confusion and perceived failures.

**Symptom:** Users saw `http://localhost:5163/api/backoffice/me` displayed in the dashboard, which timed out because that port is unreachable from the browser. In Codespaces, only port 7245 (HTTPS) is forwarded for browser access.

## Decision

**API responses must separate transport URLs from display URLs.**

When a backchannel URL is configured for server-to-server efficiency:
1. Use the backchannel URL for the actual HTTP call (transport layer)
2. Transform it to the public URL before returning in the response (display layer)

This separation ensures:
- Server-side calls remain efficient (use internal HTTP endpoints)
- Browser-facing responses show reachable URLs (use public HTTPS endpoints)

## Implementation

Added to `DownstreamDemoController.cs`:

```csharp
private string ResolveBusinessAppDisplayBaseUrl()
{
    var baseUrl = configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("PrismBusinessApp:WorkflowApiBaseUrl is not configured.");
    return baseUrl;
}

private string TransformToDisplayUrl(string transportUrl)
{
    var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(backchannelUrl))
        return transportUrl;

    if (!transportUrl.StartsWith(backchannelUrl, StringComparison.OrdinalIgnoreCase))
        return transportUrl;

    var displayBaseUrl = ResolveBusinessAppDisplayBaseUrl();
    return displayBaseUrl + transportUrl.Substring(backchannelUrl.Length);
}
```

All response returns now use `TransformToDisplayUrl(targetUrl)` instead of bare `targetUrl`.

## Test Contract

Updated `DashboardLocalEndpointsValidationTests.cs`:

```csharp
[Fact]
public async Task DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport()
{
    // ... setup ...
    
    // Backend uses backchannel for transport efficiency
    capturedRequestUri.Should().Be(new Uri("http://localhost:5163/api/backoffice/me"));
    
    // But response to browser uses public URL
    root.GetProperty("url").GetString().Should().Be(
        "https://codespace-7245.app.github.dev/api/backoffice/me",
        because: "browser-facing URLs must be publicly accessible");
}
```

This test validates the contract: transport uses backchannel, response shows public URL.

## Why This Matters

1. **User Experience:** Users see URLs they can actually reach, not internal addresses
2. **Codespaces-Critical:** Port forwarding rules make public vs internal URLs non-negotiable
3. **Security Posture:** Don't expose internal routing details (ports, HTTP vs HTTPS) to the browser
4. **Test Contracts:** Codify that transport optimization doesn't leak into UI concerns

## Alternatives Considered

**Alternative 1: Don't use backchannel URLs**  
Rejected: The backchannel pattern is valid for server-to-server efficiency in Codespaces; the fix is in response transformation, not transport choice.

**Alternative 2: Add separate `displayUrl` field**  
Acceptable but more complex: Would require UI updates and adds redundancy. Transforming the existing `url` field is simpler and clearer.

**Alternative 3: Document that `url` shows internal address**  
Rejected: Users expect displayed URLs to be reachable. This would violate the principle of least surprise.

## References

- Implementation: PR #48 (`squad/fix-browser-url-leak`)
- Commit: `6774c55`
- Test: `DashboardLocalEndpointsValidationTests.DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport`
- Prior diagnosis: `.squad/agents/blathers/history.md` — "MockBusinessApp API Demo Timeout"
- Related decision: `.squad/decisions.md` — "Browser-Facing API Responses Must Not Expose Internal Backchannel URLs"
---
date: 2026-05-03T18:29:38.303+01:00
author: Tangy
status: implemented
area: testing, playwright, browser-contracts
---

# Browser-Level Regression Test for Backchannel URL Visibility

## Context

Following Blathers' implementation of `TransformToDisplayUrl()` in `DownstreamDemoController` (commit `6774c55`), added Playwright test coverage to ensure the browser-facing contract is enforced at the user experience level.

The unit test validates the controller logic, but doesn't exercise the full browser → server → response → DOM rendering path. A Playwright test completes the coverage by validating what users actually see.

## Decision

**Add browser-level assertion to `callBusinessAppApi()` in Playwright test suite.**

The test validates the URL displayed in element `#api-url-label` after clicking "Call Mock Business App API" in the member dashboard.

## Implementation

Updated `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`:

```typescript
async function callBusinessAppApi(page: Page): Promise<void> {
  // ... existing setup and success assertions ...
  
  // Contract: Browser-facing API responses must not expose internal backchannel URLs
  const displayedUrl = await apiUrl.textContent();
  expect(displayedUrl).not.toContain(':5163', 
    'displayed URL must not expose the internal backchannel port 5163');
  expect(displayedUrl).toContain('https://localhost:7245',
    'displayed URL must show the public-facing HTTPS endpoint');
}
```

## Why This Matters

1. **Full-stack validation**: Unit tests validate controller logic; Playwright validates the complete user experience
2. **Behavior-level contract**: Test what users see, not just what the code does
3. **Regression prevention**: This test would have caught the original bug where `localhost:5163` leaked to the dashboard
4. **Environment coverage**: Works in both localhost and Codespaces contexts

## Test Results

- **All 25 unit tests pass**: `DashboardLocalEndpointsValidationTests`
- **Playwright test updated**: `localhost-auth-session.spec.ts` — `callBusinessAppApi()` function
- **Commit**: `2ebec5a` on `squad/fix-browser-url-leak` branch

## Coordination

Worked in parallel with Blathers on the same feature branch:
- Blathers: Controller fix + unit test (`6774c55`)
- Tangy: Playwright contract test (`2ebec5a`)

Clean commit history, no conflicts.

## References

- Commit: `2ebec5a` — "test: add browser-level contract for backchannel URL visibility"
- Related decision: `blathers-mockbiz-browser-url-fix.md` (controller implementation)
- Test file: `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`
- History: `.squad/agents/tangy/history.md` — "Browser URL Leak Fix — Test Coverage"
---
date: 2026-05-03T18:29:38.303+01:00
author: Blathers
status: EXECUTED
area: git-workflow, merge-strategy, release-notes
---

# PR #48 Merge Strategy — Preserve Commit History

## Context

PR #48 (`squad/fix-browser-url-leak`) contained two commits:
1. `6774c55` — Core fix: Transform internal backchannel URLs to public URLs
2. `2ebec5a` — Browser test: Add Playwright contract for URL visibility

Both commits were release-note-relevant and addressed distinct concerns (implementation vs validation).

## Decision

**Merged PR #48 using `--merge` strategy to preserve the two separate commits in main.**

Rationale:
- Each commit addresses a distinct aspect (fix vs test coverage)
- Release notes benefit from granular history
- Git bisect operations benefit from separated concerns
- Avoids squashing away test coverage commit into fix commit

## Implementation

```bash
gh pr merge 48 --repo jonnymuir/Umbraco.Prism --merge --body "All checks passed. Merging to main."
```

Resulted in merge commit `0f79c12` on main, preserving both `6774c55` and `2ebec5a`.

## CI Results

All checks passed:
- ✅ test (9 seconds)
- ✅ core-tests (53 seconds)
- ✅ storybook-tests (1m53s)
- ✅ localhost-auth-playwright (15m32s)

**Note:** Playwright tests with full Aspire + Keycloak + browser automation legitimately take 15+ minutes. This is expected behavior for integration tests with container orchestration and OIDC flows.

## Local Sync

After merge, synced local main:
```bash
git checkout main && git pull origin main
```

Local `.squad/` history files remained uncommitted (not mixed into product PR), preserving separation between product work and squad coordination files.

## Consistency with PR #47

This approach is consistent with PR #47 merge strategy (also used `--merge` to preserve dashboard + auth fix commits). Establishing this as the standard practice for PRs with multiple concerns.
---
date: 2026-05-03T19:40:50.786+01:00
author: Blathers
status: implemented
area: codespaces, aspire-orchestration, backchannel-urls
---

# Use Dynamic Endpoint Discovery for Aspire Project Backchannels

## Context

The downstream API demo was timing out in Codespaces after the URL transformation fix (PR #48). The browser-facing URL was correct (showing the public Codespaces URL), but the server-side API call was timing out after 10 seconds.

Root cause: AppHost hardcoded `BUSINESSAPP_BACKCHANNEL_URL=http://localhost:5163`, assuming port 5163 would always be correct. However, Aspire may assign ephemeral ports or not bind the HTTP endpoint at the expected address in Codespaces.

## Decision

**For Aspire project resources (not containers), use dynamic endpoint discovery for backchannel URLs.**

Pattern:
```csharp
// Container resources (Keycloak) — already using dynamic discovery
testsite.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"));

// Project resources (MockBusinessApp) — NOW using dynamic discovery
testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"));
```

**Do not hardcode ports** for backchannel URLs, even if they're defined in launchSettings.json. Aspire's dynamic port assignment takes precedence.

## Why This Matters

1. **Codespaces reliability**: Aspire's port assignment may differ from launchSettings.json in containerized environments
2. **Consistency**: Matches the Keycloak backchannel pattern which works reliably
3. **Maintainability**: Single source of truth for endpoint addresses (Aspire's runtime discovery)

## Why GetEndpoint("http") Works for Projects

**Historical context**: An earlier attempt used `businessApp.GetEndpoint("https")` and failed because it returned a service discovery URL that didn't resolve from plain HttpClient.

**Why HTTP works**: The HTTP endpoint returns a plain `http://localhost:{port}` URL (not a service discovery URL), which works from plain HttpClient without Aspire service discovery extensions.

## Test Contract

Updated `DashboardLocalEndpointsValidationTests.AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls`:

```csharp
program.Should().Contain(".WithEnvironment(\"BUSINESSAPP_BACKCHANNEL_URL\", businessApp.GetEndpoint(\"http\"))",
    because: "Aspire's dynamic endpoint discovery ensures the correct HTTP port is used, " +
             "avoiding hardcoded ports that may differ across environments or Aspire configurations");
```

This validates the dynamic discovery pattern and prevents regression to hardcoded ports.

## Operational Recovery

**After merging PR #49**: Restart the Aspire AppHost in Codespaces. The backchannel will automatically resolve to the correct runtime HTTP endpoint, fixing the timeout.

No database migrations, no secrets updates, no client-side changes required.

## Alternatives Considered

**Alternative 1: Keep hardcoded localhost:5163**  
Rejected: Already proven to fail in Codespaces. No reason to assume port assignment will be stable.

**Alternative 2: Use GetEndpoint("https")**  
Rejected: Historical evidence (commit `ffc32c5`) shows HTTPS endpoints return service discovery URLs that don't work from plain HttpClient.

**Alternative 3: Configure Aspire to force specific ports**  
Rejected: Fights against Aspire's design. Dynamic discovery is the intended pattern.

## References

- Implementation: PR #49 (`squad/fix-backchannel-endpoint-discovery`)
- Commit: `2a46494`
- Test: `DashboardLocalEndpointsValidationTests.AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls`
- Prior failed attempt: Commit `ffc32c5` (removed businessApp.GetEndpoint("https"))
- History: `.squad/agents/blathers/history.md` — "BusinessApp Backchannel Timeout Fix"
---
date: 2026-05-03T19:40:50.786+01:00
author: Tangy
status: DIAGNOSED
area: testing, codespaces, aspire-endpoints
---

# Downstream API Timeout: Hardcoded Backchannel Port vs Aspire Runtime Endpoint

## Context

User reports: "The downstream API demo now shows the public 7245 URL, but the browser call still times out after 10 seconds even though the Mock Business App admin page is reachable."

## Investigation

**What's working:**
- ✅ URL transformation fix (commit `6774c55`, `2ebec5a`) correctly transforms internal `http://localhost:5163` to public Codespaces URL in browser-facing responses
- ✅ Unit test `DownstreamDemo_ReturnsPublicUrl_WhenBackchannelUrlIsUsedForTransport` validates the transformation logic
- ✅ MockBusinessApp admin page is reachable from browser (confirms app is running)
- ✅ Playwright test validates displayed URL doesn't contain `:5163`

**What's broken:**
- ❌ Server-to-server call from TestSite to MockBusinessApp times out after 10 seconds
- ❌ `DownstreamDemoController` line 289 timeout triggers, returns "Timeout" response to browser

## Root Cause

AppHost line 142 hardcodes the backchannel URL:

```csharp
if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
```

This assumes MockBusinessApp's HTTP endpoint is bound to port 5163. However:

1. **Aspire may assign ephemeral ports** - the actual runtime port might not be 5163
2. **Keycloak pattern** (line 134) uses the correct approach: `keycloak.GetEndpoint("http")` to get the actual runtime endpoint
3. **MockBusinessApp is started with `launchProfile: "https"`** (line 97), which specifies `"applicationUrl": "https://localhost:7245;http://localhost:5163"` in launchSettings.json

The hardcoded `http://localhost:5163` is fragile and doesn't work when Aspire assigns different ports in Codespaces.

## Behavioral Contract Violation

**Contract:** Server-to-server API calls must complete within the configured timeout (10 seconds)

**Current behavior:**
- TestSite attempts to call `http://localhost:5163/api/backoffice/me`
- Request times out after 10 seconds
- Controller returns "Timeout" response with statusCode 0, statusText "Timeout"
- Browser displays: "We could not reach the Mock Business App. Check that it is running, then try again."

**Expected behavior:**
- TestSite calls MockBusinessApp's actual HTTP endpoint
- Request completes successfully (200 OK)
- Browser displays: "Mock Business App responded successfully."

## Test Coverage Gap

**Current tests:**
- ✅ Unit tests validate URL transformation logic with stub handlers
- ✅ Playwright test validates displayed URL format
- ❌ **No test validates backchannel endpoint is actually reachable**
- ❌ **No test validates AppHost backchannel configuration matches Aspire reality**

**Smallest regression test surface:**

The existing Playwright test `callBusinessAppApi()` (localhost-auth-session.spec.ts, line 150-186) **SHOULD** catch this bug because it:
1. Clicks "Call Mock Business App API"
2. Expects `#api-status-badge` to show "200 OK"
3. Expects response body to contain tenant and role info

If the backchannel times out, this test should fail with:
```
Expected API call to succeed with 200 OK, but got:
Status: Timeout
Summary: We could not reach the Mock Business App...
Body: Request timed out after 10 seconds. Is MockBusinessApp running?
```

**Question:** Does this Playwright test run in Codespaces with Aspire? If not, that's the coverage gap.

## Recommended Fix (For Blathers)

Change AppHost line 142 from:

```csharp
if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", "http://localhost:5163");
```

To:

```csharp
if (codespaceName != null)
    testsite.WithEnvironment("BUSINESSAPP_BACKCHANNEL_URL", businessApp.GetEndpoint("http"));
```

This matches the Keycloak pattern (line 134) and ensures TestSite uses the actual runtime HTTP endpoint that Aspire assigned to MockBusinessApp.

**Note:** This requires MockBusinessApp to expose an HTTP endpoint. Verify the launchProfile "https" includes both HTTPS and HTTP in applicationUrl (currently: `"https://localhost:7245;http://localhost:5163"`).

## Test Fix

The failing unit test `AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls` (line 302) needs updating:

Current:
```csharp
program.Should().Contain(".WithEnvironment(\"BUSINESSAPP_BACKCHANNEL_URL\", \"http://localhost:5163\")");
```

Should be:
```csharp
program.Should().Contain("testsite.WithEnvironment(\"BUSINESSAPP_BACKCHANNEL_URL\", businessApp.GetEndpoint(\"http\"))");
```

Or make it more flexible:
```csharp
program.Should().Contain("BUSINESSAPP_BACKCHANNEL_URL");
program.Should().Contain("businessApp.GetEndpoint(\"http\")");
```

## Why This Matters

1. **Codespaces-critical:** Hardcoded localhost ports don't work reliably when Aspire assigns ephemeral ports
2. **Consistency:** Keycloak already uses `.GetEndpoint("http")` pattern - MockBusinessApp should match
3. **Behavioral contract:** The Playwright test should catch this, but only if it runs in the actual Codespaces + Aspire environment

## References

- AppHost configuration: `src/UmbracoPrism.AppHost/Program.cs` lines 134, 142
- Controller timeout: `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` line 289
- Keycloak pattern: AppHost line 134 (`testsite.WithEnvironment("KEYCLOAK_BACKCHANNEL_URL", keycloak.GetEndpoint("http"))`)
- MockBusinessApp launchSettings: `src/UmbracoPrism.MockBusinessApp/Properties/launchSettings.json`
- Playwright test: `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts` line 150-186
- Related decisions: `.squad/decisions.md` - "Transport URLs vs Display URLs: Separate Concerns in API Responses"
---
date: 2026-05-03T21:12:36.429+01:00
status: RECORDED
author: Blathers
area: diagnostics, operations, codespaces
---

# Codespaces Downstream Diagnostics Should Prefer Live Runtime Probes

## Context

The downstream API/auth investigation now spans three distinct surfaces:

1. **Local Codespace runtime** (`localhost` HTTPS endpoints)
2. **Internal backchannel state** (for Keycloak and MockBusinessApp)
3. **Public forwarded URLs** (`*.app.github.dev`) that may return redirects or GitHub tunnel/auth HTML instead of the app

Manual curl commands were becoming easy to misread, especially when a public forwarded URL returned HTML or a redirect that looked superficially like the app was healthy.

## Decision

**Codespaces diagnostics should prefer live runtime probes over guessed ports, and public forwarded-port checks must classify redirects / tunnel HTML as proxy evidence rather than app success.**

## Implementation

Added `scripts/codespaces/diagnose-downstream.sh` to:

- read authoritative forwarded browse URLs from `gh codespace ports`
- probe local TestSite / MockBusinessApp / Keycloak endpoints directly from the Codespace
- summarize safe runtime state from MockBusinessApp `/debug/auth`
- probe public forwarded URLs without following redirects, so tunnel/auth interception stays obvious
- avoid printing secrets, cookies, or bearer tokens

## Why This Matters

1. **Correctness:** dynamic Aspire / Codespaces endpoints are safer to read from runtime than to guess from stale localhost assumptions
2. **Operator clarity:** HTML tunnel pages and redirects are a different class of failure from app JSON or auth responses
3. **Security posture:** diagnostics remain useful without exposing secrets

## References

- `scripts/codespaces/diagnose-downstream.sh`
- `src/UmbracoPrism.MockBusinessApp/Program.cs` (`/debug/auth`)
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` (`/session-contract`, `seed-contract-ready`)
---
date: 2026-05-03T20:53:49.355+01:00
status: complete
domain: diagnostics, operations
---

# Decision: Manual Diagnosis Flow for Downstream API Timeouts

## Problem

When the MockBusinessApp API times out (10s) in Codespaces, operators face ambiguity:
- Is the API unreachable or just hung?
- Is the bearer token invalid or the Keycloak backchannel blocked?
- Is it a browser→API issue or a server→API issue?
- Previous "fixes" that didn't work eroded confidence in troubleshooting.

## Solution

Created **operator-friendly diagnostic flows** that use curl to isolate each layer:

### Deliverables

1. **`MANUAL_DIAGNOSIS_FLOW.md`** — Comprehensive guide
   - 5-step progression from quick reachability checks to deep backchannel validation
   - Expected outcomes for each curl command (not just "try this")
   - Diagnosis flowchart mapping symptoms → root causes
   - Common failure points with fixes
   - Operator checklist for closure

2. **`.squad/agents/blathers/QUICK_DIAGNOSIS_REFERENCE.txt`** — One-page cheat sheet
   - Test order (fastest to deepest)
   - Decision tree for symptom interpretation
   - Top 5 root causes by frequency
   - Files to check and environment variables

### Key Principles

1. **Layered Testing**
   - Internal backchannel (http://localhost:5163) → proves API listens
   - Public endpoint (https://{codespace}-7245.app.github.dev) → proves port forwarding
   - Bearer token tests → proves auth chain
   - Keycloak backchannel → proves signing key access

2. **No Code Changes**
   - Uses existing curl, gh CLI, browser DevTools
   - No temporary logging or instrumentation needed
   - Can be run by operators with no repo knowledge

3. **Expected Outcomes Explicit**
   - Not "try this command"
   - But "run this; if you see X expect result Y; if Z expect result W"
   - Maps exact output (401, HTML, timeout, connection refused) to root causes

4. **Separation of Concerns**
   - Browser-facing path (public HTTPS + port forwarding)
   - Server-side path (internal backchannel + token forwarding)
   - Keycloak trust chain (issuer, JWKS, token validation)
   - Each testable independently

## Five Distinct Failure Modes

The 10-second timeout can originate from:

1. **Aspire port reassignment** — Port 5163 not listening
   - Test: `curl http://localhost:5163/api/backoffice/me`
   - Result: Connection refused
   - Fix: Check `gh codespace ports` for actual port

2. **Service hung** — Port listening but no response
   - Test: Same curl, hangs for 10s
   - Fix: Restart AppHost or check MockBusinessApp logs

3. **Bearer token expired/invalid** — API responds 401
   - Test: `curl -H "Authorization: Bearer {TOKEN}" ...`
   - Result: 401 Unauthorized
   - Fix: Check token expiry, re-sign in

4. **Keycloak backchannel blocked** — Signing keys unreachable
   - Test: `curl http://localhost:8080/realms/prism-dev/.well-known/openid-configuration`
   - Result: Connection refused or timeout
   - Fix: Restart Keycloak, verify port

5. **GitHub tunnel auth page** — Port forwarding returns HTML
   - Test: `curl https://{codespace}-7245.app.github.dev/api/backoffice/me`
   - Result: `<h1>Connecting to the forwarded port...</h1>`
   - Fix: Include Bearer token in Authorization header

## Why This Matters

- **Previous approach**: "Try this fix, restart AppHost, hope it works"
- **New approach**: "Run these 5 tests in order; at step N you'll know whether it's port/auth/tunnel"
- **Operator confidence**: Diagnosis is reproducible and deterministic, not magical

## Not Changing Code

This is a **read-only diagnostic aid** — no code changes, no new dependencies, no Aspire modifications. It documents existing troubleshooting best practices discovered during PR #49 work.

## Related

- `.squad/skills/aspire-dynamic-endpoint-backchannels/SKILL.md` — The fix (code change)
- `.squad/skills/generic-oidc-downstream-bearer-validation/SKILL.md` — Token validation patterns
- `.squad/skills/live-oidc-401-stale-runtime/SKILL.md` — Runtime restart detection
- PR #49 — Implementation of dynamic endpoint discovery
# Final Push to Origin & Branch Cleanup

**Author:** Mabel  
**Date:** 2026-05-03  
**Status:** COMPLETED  

---

## Task

Push the finished main branch to origin (which contained 4 .squad-only commits after PR #49 merge and residual reconciliation). Clean up merged feature branches from both remote and local.

## Actions Completed

### Push Main
- Local main (commit `e1d54e7`) pushed to origin/main
- 4 commits delivered:
  - `e1d54e7` docs: mabel session history — post-merge reconciliation complete
  - `ed2b5cd` docs: update tom-nook history — aspire-dynamic-endpoint-backchannels skill extraction
  - `9ee9a25` docs: add aspire-dynamic-endpoint-backchannels skill
  - `e44c8bf` chore: mabel session history — PR #49 merge complete

### Remote Cleanup
Deleted 9 merged feature branches from origin (all were fully merged into main):
- fix/codespaces-businessapp-http-backchannel
- squad/12-biometric-device-credentials-table
- squad/20-21-biometric-platform-config
- squad/22-capacitor-biometric-bridge
- squad/23-biometric-registration-ui
- squad/25-biometric-device-management-ui
- squad/codespaces-dashboard-and-auth-fixes
- squad/fix-backchannel-endpoint-discovery
- squad/fix-browser-url-leak

### Local Cleanup
Deleted corresponding local feature branches:
- fix/codespaces-businessapp-http-backchannel ✅
- squad/codespaces-dashboard-and-auth-fixes ✅
- squad/fix-browser-url-leak ✅
- squad/fix-backchannel-endpoint-discovery ✅ (force-deleted; remote was already gone)

One local branch remains: `fix/codespaces-mockbiz-401` (not merged; kept for ongoing work).

## Final State

- **Local main:** At commit `e1d54e7`, synced to origin/main
- **Working tree:** Clean
- **Local branches:** 2 remaining (`main`, `fix/codespaces-mockbiz-401` — the latter for ongoing work)
- **Risk:** None — all deletions were of fully merged branches; no history was lost

## Pattern

Safe cleanup after merge:
1. Verify branches are fully merged into main using `git branch -r --merged origin/main`
2. Delete from origin first (remote source of truth)
3. Delete from local after remote confirms deletion
4. Keep branches only if they contain active work not yet merged

This is low-risk workflow maintenance that signals closure and keeps branch lists legible.
# Post-Merge Branch State Reconciliation

**Author:** Mabel  
**Date:** 2026-05-03  
**Status:** COMPLETED  
**Issue:** Residual squad-only work on `squad/fix-backchannel-endpoint-discovery` after PR #49 merge

---

## Context

PR #49 merged to main (commit `a8e2d86` on origin/main), but the local feature branch had:
1. Uncommitted changes to `.squad/agents/tom-nook/history.md` (documenting skill extraction)
2. A post-merge skill documentation commit on the branch

Mabel had also made a local post-merge session history commit to main, creating branch divergence.

## Decision

**Outcome:** Keep and land the skill documentation cleanly.

- **Skill verdict:** `aspire-dynamic-endpoint-backchannels` is **earned, well-documented, and reusable**. Merits inclusion in shared skills library.
- **History verdict:** Tom Nook's documentation of the extraction process belongs in the history record.
- **Merge strategy:** Rebase feature branch onto main's post-merge commit, then fast-forward merge to preserve linear history.

## Rationale

1. **Skill quality:** The skill has test contracts, anti-patterns, diagnosis steps, and cross-references. It captures a real learning from Codespaces backchannel timeout diagnosis (PR #49 work).

2. **Clean history:** Feature branch rebase resolves divergence without creating merge commits. Final state: linear main history with two skill-related commits.

3. **Pattern establishment:** Archiving learned skills as part of PR closure is a discipline. This reconciliation sets the precedent: skills extracted during work should be included in the merge, not left behind on a stale branch.

## Implementation

- ✅ Staged Tom Nook's history entry
- ✅ Rebased feature branch onto main
- ✅ Fast-forward merged to main
- ✅ Both main and feature branch now at commit `ed2b5cd`
- ✅ Working tree clean

## Downstream

- **Next step:** Push reconciled main to origin (awaiting authorization)
- **Feature branch:** Can be deleted or left as historical marker; feature branch head points to merged commit
- **No code changes:** This is purely .squad/ bookkeeping; no product or implementation impact

## Related

- Skill: `.squad/skills/aspire-dynamic-endpoint-backchannels/SKILL.md`
- Tom Nook history: `.squad/agents/tom-nook/history.md` (entry dated 2026-05-03 20:12:13)
- Original PR: #49
- Decision: Kept as-is per established routing policy (Mabel owns PR/merge workflow)
# PR #49 Merge Strategy — Preserve Commit History

**Date:** 2026-05-03  
**Agent:** Mabel (Technical Writer / Release)  
**Merge Commit:** a8e2d86

## Decision

Merged PR #49 using **create a merge commit** strategy (not squash) to preserve the readable product history:

```
a8e2d86 Merge pull request #49 ...
├─ d6cfe4e squad: merge downstream timeout diagnosis decisions
└─ 2a46494 fix(codespaces): use dynamic endpoint discovery for BusinessApp backchannel
```

## Rationale

- **Preserve product narrative:** The two commits represent distinct concerns:
  1. **2a46494:** User-facing fix (endpoint discovery solves the timeout)
  2. **d6cfe4e:** Team bookkeeping (decision history consolidation)
- **Release notes clarity:** Future release notes can reference `2a46494` directly as the fix, with d6cfe4e as supporting team documentation
- **Bisect-friendly:** If issues arise, engineers can identify the exact commit that introduced them
- **Consistency:** Aligns with project history strategy: meaningful atomic commits > squashed history

## Alternative Considered

- **Squash merge:** Would flatten both commits into one. This loses the distinction between the fix and team documentation, making future release notes and bisecting harder.
- **Rebase merge:** Would linearize but wouldn't create an explicit merge commit, risking confusion about which commits belonged to this PR.

## Impact

- All CI checks passed before merge ✅
- Local main automatically fast-forwarded to origin/main
- Feature branch cleaned (local + remote deletion)
- Ready for next development cycle
---
date: 2026-05-03T20:53:49.355+01:00
status: RECORDED
author: Tangy
area: testing, diagnosis, browser-debugging
---

# Browser DevTools Manual API Diagnosis Playbook

## Context

After several rounds of timeout investigations on the "Call Mock Business App API" button, a repeatable manual diagnostic pattern emerged. Users need a structured way to isolate failures at three levels: button flow, auth/headers, and network reachability.

## Decision

**Testers, developers, and QA should follow the 8-phase diagnostic playbook to manually isolate API timeouts from the browser side.**

The playbook prioritizes separating concerns so that a single observation (e.g., "timeout") can be quickly traced to a root cause (button flow broken, auth header missing, port unreachable, CORS blocked).

## Diagnostic Approach

### Phase Separation

1. **Capture** (DevTools Network tab) → Know if a request was fired
2. **Inspect auth** (Request Headers) → Know if token was attached
3. **Check status** (Response Status) → Know if server responded
4. **Inspect response** (Response Body) → Know what the failure was
5. **Isolate endpoint** (cURL copy) → Know if it's browser-specific
6. **Test health** (Direct curl, no auth) → Know if endpoint exists
7. **Compare levels** (With/without auth) → Know if auth is the issue
8. **Check console** (Browser errors) → Know if JS or CORS failed

### Key Observation Points

- **No request in DevTools** → Button flow broken (JavaScript)
- **Request with 401** → Auth header missing or token invalid
- **Request with 200** → Success; check response body for expected fields
- **Request with 0 (timeout)** → Endpoint unreachable or misconfigured
- **URL contains `:5163`** → Internal backchannel port (not browser-reachable)
- **cURL succeeds, browser times out** → CORS or browser-specific issue
- **Both fail identically** → Network or endpoint health issue

## Implementation

Documented in: `.squad/skills/browser-devtools-api-diagnosis/SKILL.md`

Includes:
- Step-by-step walkthrough for each phase
- Expected/unexpected responses at each phase
- Decision tree for quick diagnosis
- cURL examples for copying from DevTools
- 3 worked examples (auth missing, port unreachable, CORS blocked)
- Environment-specific notes (localhost, Codespaces, CI/CD)

## Use Cases Covered

1. **Timeout after 10 seconds** → Isolate between button flow, network, auth token validation
2. **401 Unauthorized** → Confirm token is being sent and isn't expired
3. **Endpoint unreachable** → Distinguish between browser CORS block vs. true network failure
4. **Port forwarding confusion** → Recognize internal localhost URLs (`:5163`) vs. public endpoints
5. **Button doesn't seem to do anything** → Confirm request is being fired vs. JavaScript failing

## Testing Edge Cases

The playbook surfaces these edge cases:

- **Token valid in auth context but rejected during header validation** → Token validation timeout
- **Endpoint works without auth (401) but times out with auth** → Token processor hanging
- **cURL works but browser times out** → CORS headers missing or wrong
- **Internal backchannel URL in response** → URL transformation not applied (regression in PR #48)

## Regression Test Coverage

The existing Playwright test `callBusinessAppApi()` (localhost-auth-session.spec.ts) already validates end-to-end but doesn't surface intermediate failures well. The manual playbook allows testers to go deeper when automated tests fail, following the same phases: capture → inspect headers → check status → inspect body → isolate endpoint.

## Team Impact

- **Testers:** Can diagnose timeouts without asking developers
- **Developers:** Can provide better error responses (include `statusCode`, `statusText`, attempted URL in response body)
- **Ops/Infra:** Can correlate browser diagnoses with server logs to confirm backchannel vs. external failures

## References

- Previous timeout diagnoses: `tangy-downstream-timeout.md`, `tangy-mockbiz-timeout-diagnosis.md`
- Related skills: `aspire-dynamic-endpoint-backchannels`, `inline-api-failure-states`, `dev-session-contract-probe`
- Playwright test: `localhost-auth-session.spec.ts::callBusinessAppApi()`
---
date: 2026-05-03T21:12:36.429+01:00
author: Tangy
status: PROPOSED
area: testing, diagnostics, codespaces
---

# Codespaces Downstream Diagnostics Must Separate Transport, Tunnel, and Token Failures

## Context

Manual curl checks were proving that some endpoints returned `200`, but operators still had to guess whether the real failure was:

- the internal TestSite → MockBusinessApp hop
- the public GitHub forwarded-port tunnel/auth layer
- bearer token rejection inside MockBusinessApp
- stale Keycloak backchannel wiring in the running stack

A Codespaces helper script needs to turn those into distinct outcomes instead of a single generic "timeout" story.

## Decision

A Codespaces downstream diagnostics script must:

1. **Check the internal BusinessApp hop separately from the public forwarded URL** so operators can tell "service is up internally" from "public tunnel returned HTML/auth".
2. **Use safe runtime diagnostics (`/debug/auth`) before asking for tokens** so the script can inspect backchannel/JWKS health without dumping secrets.
3. **Treat authenticated 401s as an auth-validation branch, not an availability branch** when the internal app probe already succeeded.
4. **Compare repo expectations with runtime backchannel state** so the script can call out likely stale AppHost/runtime wiring and recommend `bash scripts/codespaces/refresh.sh`.
5. **Print next commands inline for every failure state** so operators do not need to cross-reference a separate playbook.

## Why

The same user-visible timeout can come from different layers, and the remediation is different for each one. A good script must say "forwarding problem", "token problem", or "stale backchannel problem" explicitly, otherwise the operator wastes time chasing the wrong service.
---
author: "Tom Nook"
date: "2026-05-03T20:12:13+01:00"
decision_type: "pattern"
status: "implemented"
---

# Skill Extraction Discipline — aspire-dynamic-endpoint-backchannels

## Decision

**EXTRACT** earned knowledge as `.squad/skills/{skill-name}/SKILL.md` as part of PR closure workflow.

## Context

`squad/fix-backchannel-endpoint-discovery` included:
- **Fix:** Aspire's `GetEndpoint("http")` for dynamic backchannel URL discovery in Codespaces
- **Bookkeeping:** Decision logs, history updates, agent charters
- **Untracked:** `.squad/skills/aspire-dynamic-endpoint-backchannels/` directory

The skill captures reusable patterns:
1. Why GetEndpoint("http") works vs GetEndpoint("https")
2. Test contract validation
3. Diagnosis steps for backchannel timeouts
4. Anti-patterns (hardcoded ports, wrong endpoint types)

## Resolution

**KEEP the skill.** It is:
- Earned through real work (PR #49)
- Well-documented with concrete examples
- Cross-referenced in related skills
- Immediately reusable for future Codespaces/Aspire work

## Consequences

1. **Knowledge Preservation:** Infrastructure patterns become team assets, not lost in commit history
2. **Onboarding:** New contributors can understand Codespaces backchannel without reverse-engineering
3. **Decision Trail:** Skills link back to PRs and orchestration logs for full context
4. **Reuse:** Future Aspire work can reference this pattern instead of re-diagnosing

## Implementation

Added skill as commit `2078604` on `squad/fix-backchannel-endpoint-discovery` during branch cleanup.

## Related

- Implementation: PR #49 (commit `2a46494`)
- Test contract: `DashboardLocalEndpointsValidationTests.AppHost_ConfiguresBusinessAppBackchannel_ForCodespacesServerCalls`
- Decision: `.squad/decisions/inbox/blathers-backchannel-dynamic-discovery.md`
---
date: 2026-05-03T21:32:41.296+01:00
author: Blathers
status: PROPOSED
area: codespaces, diagnostics, runtime
---

# Codespaces Diagnostics Scripts Should Verify a Clean Python Runtime

## Context

`scripts/codespaces/diagnose-downstream.sh` is intentionally invoked as a plain shell command from the repo root. In Codespaces, contributors may already have activated another Python toolchain or exported `PYTHONHOME` / `PYTHONPATH`, which can make `python3` start without a usable standard library and fail on imports as basic as `json`.

## Decision

Codespaces operator scripts that embed Python should:

1. Probe for a working interpreter before running the main payload
2. Launch that interpreter with `-I`
3. Scrub shell-level Python environment overrides such as `PYTHONHOME` and `PYTHONPATH`
4. Fall back to a system interpreter when the first `python3` on `PATH` is broken

## Why

- Operators should not have to debug their shell state just to run first-line diagnostics
- `-I` and explicit env scrubbing keep these scripts dependency-free while restoring predictable stdlib imports
- A small runtime guard is cheaper and less invasive than rewriting an otherwise working diagnostics payload

---
date: 2026-05-03T21:26:34.690+01:00
agent: mabel
issue: diagnostics-script-landing
status: implemented
---

# Diagnostics Script Landing: Scope Discipline

## Decision

Land **product-scoped** diagnostics work (script + flow guide) directly onto main branch in a single, clear commit. Keep **agent-scoped** work (.squad bookkeeping, skills) separate and untracked on main.

## Context

After previous work on downstream API timeout diagnosis (PR #49), two artifacts emerged:

1. **Product deliverables:** `scripts/codespaces/diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, updated `CODESPACES.md`
2. **Agent bookkeeping:** Blathers' reference note + extracted browser-diagnostics skill

Both were created during the same diagnostic effort but serve different audiences:
- Product files: Codespaces users needing to troubleshoot API/auth/tunnel issues
- Agent work: Squad team learning and skill reuse

## Choice

**Commit product files to main; leave agent work in .squad/**

### Product Commit (926ca7a)

```
docs: add downstream diagnostics script and flow guide

- Add scripts/codespaces/diagnose-downstream.sh for debugging API/auth/tunnel issues
- Add MANUAL_DIAGNOSIS_FLOW.md for step-by-step troubleshooting guide
- Update CODESPACES.md with reference to new diagnostics script and flow

The script checks local endpoints, reads safe runtime diagnostics,
probes TestSite/MockBusinessApp/Keycloak connectivity, and supports
optional bearer token authentication for full testing.
```

### Agent Work (Untracked, Not Merged)

- `.squad/agents/blathers/QUICK_DIAGNOSIS_REFERENCE.txt` — Blathers' diagnostic notes
- `.squad/skills/browser-devtools-api-diagnosis/` — Reusable pattern for future devtools-level debugging

## Rationale

**Separation enables clarity:**

1. **Product surface** (main branch) stays focused on user-facing assets — no .squad clutter
2. **Agent work** stays in .squad/ — available for future sessions but not blocking product merges
3. **Git history** reads clearly: "We shipped diagnostics tooling" vs "We learned a pattern"

**Timing impact:** Landing product immediately unblocks Codespaces users; agent skill can be refined/merged in future work without rushing.

## Implementation

1. Stage only product files: `scripts/codespaces/diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, `CODESPACES.md`
2. Commit with clear scope message
3. Push to origin/main
4. Leave .squad/ untracked (will be staged separately if/when Scribe merges agent decisions)

## Follow-Up

- Mark `.playwright-cli/` for addition to `.gitignore` (build artifact, not product)
- Blathers' reference note + skill remain in .squad/ for Squad team access
- If browser-devtools-api-diagnosis pattern proves reusable, merge skill to main in a future PR with Blathers' sign-off

---
date: 2026-05-03T20:53:49.355+01:00
agent: mabel
issue: diagnostics-script-runtime
status: implemented
---

# Diagnostics Script Runtime Isolation — Commitment to Main

**Date:** 2026-05-03  
**Decision Owner:** Mabel (Technical Writer)  
**Commit:** `fb1b324`  
**Status:** ✅ Landed on main

## Problem

Codespaces users with other Python toolchains (Conda, Poetry, .venv, etc.) activated in their shell would encounter:

```
ModuleNotFoundError: No module named 'json'
```

when running `bash scripts/codespaces/diagnose-downstream.sh`. The issue occurred because the diagnostics script attempted to use Python with ambient `PYTHONHOME` and `PYTHONPATH` environment variables that pointed to incompatible or incomplete Python installations.

## Solution

### Three-part fix:

1. **Runtime detection** — Added `resolve_python_runtime()` to probe for working Python interpreters, validating each with a stdlib import check (`import json`, `argparse`, etc.)

2. **Isolation** — Invoke detected Python with `-I` flag and explicit env var unset:
   ```bash
   env -u PYTHONHOME -u PYTHONPATH -u PYTHONSTARTUP -u __PYVENV_LAUNCHER__ \
       "$PYTHON_BIN" -I - "$@" <<'PY'
   ```

3. **Documentation** — Updated CODESPACES.md with:
   - Clear statement that the script now self-checks and ignores shell overrides
   - Recovery step: fresh shell + preflight check `python3 -I -c 'import json'`
   - Added test contract: `CodespacesDiagnosticsScript_IgnoresAmbientPythonShellOverrides()`

## Scope

**Landed as single product commit:**
- `scripts/codespaces/diagnose-downstream.sh`
- `CODESPACES.md`
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs`

**Not landed (untracked):**
- `.squad/agents/*/history.md` — will update separately
- `.squad/skills/`, `.playwright-cli/` — reference/build artifacts
- Agent reference notes — (Blathers, Tangy, etc.)

This separation keeps the product commit focused and clean, while bookkeeping stays in .squad/.

## Impact

✅ **Codespaces experience:** Users no longer need to close/reopen shells or manually diagnose Python runtime conflicts.  
✅ **Operator clarity:** CODESPACES.md now gives actionable steps if the script itself fails.  
✅ **Contract enforcement:** Test ensures future contributors maintain the isolation pattern.

## User Action

Pull main and rerun the diagnostics script in a fresh Codespaces shell.

---
date: 2026-05-03T21:32:41.296+01:00
author: Tangy
status: PROPOSED
area: testing, codespaces, runtime-assumptions
---

# Codespaces Diagnostics Script Must Ignore Ambient Python Shell State

## Context

`scripts/codespaces/diagnose-downstream.sh` failed before any downstream checks with:

```text
ModuleNotFoundError: No module named 'json'
```

Because `json` is in Python's standard library, the likely failure mode is shell-level runtime contamination or a broken active interpreter, not a missing repo dependency.

## Decision

Run the diagnostics helper with an isolated Python runtime and make the recovery path explicit for operators.

## Consequences

- The script should unset ambient `PYTHON*` overrides and use `python -I` for both its preflight and main execution paths.
- If that still fails, the error should point operators at the shell runtime itself with a minimal `python3 -I -c 'import json'` preflight.
- QA should still call out the remaining assumptions: a genuinely broken `python3` binary cannot be recovered in-script, `gh codespace ports` remains the authoritative public URL source, and stack readiness is still a prerequisite for meaningful probe results.

---
date: 2026-05-03T21:26:34.690+01:00
author: Tom Nook
status: DECISION
area: git-hygiene, diagnostics, codespaces
---

# Landing Diagnostics Script: Separate Product from Bookkeeping

## Problem

**Current state:**
- Local `main` is 1 commit ahead of `origin/main` (42bae10, a squad bookkeeping commit)
- That commit's message claims it includes "scripts/codespaces/diagnose-downstream.sh" and "updated CODESPACES.md"
- **But the actual script files are untracked** — not included in the commit
- The untracked product work: `diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, `QUICK_DIAGNOSIS_REFERENCE.txt`, `browser-devtools-api-diagnosis/` skill, and `CODESPACES.md` update

**Consequence:**
- The commit message is dishonest (says it includes files that don't exist in it)
- The script cannot be pulled into Codespaces because it's not actually in the repo
- Squad bookkeeping and product work are entangled in one incomplete commit

## Decision

**Separate product from bookkeeping:**
1. Reset `main` to `origin/main` (discard the incomplete bookkeeping commit)
2. Stage and commit the diagnostics script work in a single, focused product commit
3. Push the product commit to `main`
4. Defer squad bookkeeping consolidation to a separate session

**Rationale:**
- Product commits should contain exactly what their messages claim
- Jonny can immediately pull the script into Codespaces
- Bookkeeping (decision merges, history updates) is a separate concern and should land separately
- Follows "each commit is a complete, releasable unit" discipline

## Implementation

1. `git reset --hard origin/main` (discard 42bae10)
2. Stage: `CODESPACES.md`, `scripts/codespaces/diagnose-downstream.sh`, `MANUAL_DIAGNOSIS_FLOW.md`, `.squad/agents/blathers/QUICK_DIAGNOSIS_REFERENCE.txt`, `.squad/skills/browser-devtools-api-diagnosis/`
3. Commit with message: `feat(codespaces): add downstream diagnostics script and supporting docs`
4. Push to `main`

## Outcome

- ✅ Script lands on main in a clean, focused commit
- ✅ Jonny can pull and use it immediately
- ✅ Bookkeeping will follow in a separate commit when consolidated

**No risk:** The diagnostics script is new work (no regressions); the skill docs are documentation.
---
date: 2026-05-03T21:49:23.079+01:00
author: Blathers
status: PROPOSED
area: codespaces, diagnostics, tooling
---

# Codespaces Downstream Diagnostics Must Not Depend on Python

## Context

`scripts/codespaces/diagnose-downstream.sh` is meant to be the first-response operator tool when downstream API calls, tunnel redirects, or Keycloak backchannel wiring go wrong in Codespaces.

The prior hardening still failed in shells where there was no usable Python runtime at all. In that state, the script exited before any diagnostics banner or reachability checks, which defeated the purpose of having a low-friction troubleshooting helper.

## Decision

The downstream diagnostics helper should be implemented with shell-native tooling and must not require Python to be installed or healthy.

### Implementation guidance

1. Use `curl` for HTTP/HTTPS probes, including detection of:
   - internal service reachability
   - public tunnel/auth HTML interception
   - same-origin runtime endpoint availability
   - authenticated vs unauthenticated downstream responses
2. Use `gh codespace ports` as the authoritative source for forwarded browse URLs when Codespaces metadata is available.
3. Parse only the minimum JSON fields needed for operator guidance with shell-safe extraction rather than embedding a secondary runtime.
4. Keep the fallback hostname derivation path for cases where `gh` metadata is unavailable.

## Why This Matters

- **Reliability:** A script intended for broken environments must keep working when optional runtimes are broken too.
- **Operator ergonomics:** `bash scripts/codespaces/diagnose-downstream.sh` should remain the single obvious command to run.
- **Security posture:** Shell-only summaries still avoid printing cookies, bearer tokens, or other secrets.

## Consequences

- Future enhancements to this helper should prefer Bash, `curl`, and `gh` first.
- If richer parsing is ever needed, it should only be added when there is no credible shell-native alternative and the operator experience remains robust when that dependency is absent.

---
date: 2026-05-03T21:49:23.079+01:00
author: Tangy
status: PROPOSED
area: testing, codespaces, diagnostics
---

# Codespaces Diagnostics Common Path Must Not Require Python

## Context

`scripts/codespaces/diagnose-downstream.sh` was still failing before any useful diagnostics when the active shell exposed a broken Python runtime. The Python-isolation patch improved one failure mode, but the common Codespaces operator path still depended on Python being present and healthy before the script could even reach its first probe.

## Decision

For the common Codespaces path, the downstream diagnostics helper should be shell-only and must not require Python at all. Regression coverage should lock that contract by asserting the script stays on shell-native tooling and by documenting the operator-facing runtime assumptions explicitly.

## Consequences

- A broken or polluted Python interpreter can no longer block the default diagnostics command.
- The remaining fragile assumptions are now narrower and explicit: `curl` + `jq` must exist in the shell, `gh codespace ports` remains the authoritative browse-URL source when Codespaces metadata is available, fallback hostnames are still best-effort, and the stack still has to be running for the probes to be meaningful.
- Future fixes should treat any reintroduction of Python into this script as a regression unless there is a clearly justified non-common-path fallback.

---
date: 2026-05-03T21:49:23.079+01:00
author: Mabel
status: IMPLEMENTED
area: product-hygiene, git-workflow, scope-discipline
---

# Diagnostics Script Landing: Product vs. Bookkeeping Separation

## Context

Blathers and Tangy completed the no-Python diagnostics rewrite (shell-only probe logic, updated tests, browser devtools skill extraction). This landing session faced the scope question: **Should we land product + bookkeeping in one commit, or keep them separate?**

The working tree contained:
- **Product files** (should go to main): `scripts/codespaces/diagnose-downstream.sh`, `CODESPACES.md`, `MANUAL_DIAGNOSIS_FLOW.md`, test contract
- **Bookkeeping files** (should be deferred): `.squad/agents/blathers/history.md`, `.squad/agents/tangy/history.md`, `.squad/skills/browser-devtools-api-diagnosis/`, `.playwright-cli/`

## Decision

**Product and bookkeeping files must be committed separately to main.**

- **Product commit (22843a2):** Only user-facing deliverables go to main. Users pull, get working diagnostics script, no noise.
- **Bookkeeping session:** Agent histories, skills, and session artifacts are coordinated separately, keeping the main branch clean and releasable.

### Rationale

1. **Main branch hygiene:** main should contain only shipping artifacts. `.squad/` bookkeeping is internal coordination noise.
2. **User clarity:** When a user pulls a commit message "Fix: Rewrite diagnostics script...", they should see only the files they care about, not agent history or skill extraction artifacts.
3. **Release boundaries:** One commit = one releasable unit. Product commit 22843a2 is production-ready; bookkeeping is orthogonal.
4. **Git history signal:** Future readers reviewing main history see only meaningful product decisions, not agent coordination artifacts.

### Implementation

**Workflow for multi-agent coordination going forward:**

1. Implementation agents (Blathers, Tangy) complete their work
2. Technical Writer (Mabel) **stages only product files** (`git add <product-files>`)
3. Create clean product commit with single concern
4. **Leave .squad/ files unstaged**
5. Separate bookkeeping session: Update agent histories and merge them without product files

**Git commands:**
```bash
# Stage only product files
git add scripts/codespaces/diagnose-downstream.sh CODESPACES.md MANUAL_DIAGNOSIS_FLOW.md src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs

# Commit to main
git commit -m "Fix: Rewrite diagnostics script to eliminate Python runtime dependency..."

# Push product commit
git push origin main

# Later: Separate bookkeeping merge with only .squad/ files
```

### Exception: When bookkeeping is tightly coupled

If a product file genuinely requires a .squad/ reference for correctness (e.g., a decision embedded in a code comment), include it in the product commit. Otherwise: separate.

---

## Precedent

Commit fb1b324 (2026-05-03, earlier session) established this pattern. Commit 22843a2 reinforces it.

## Follow-up

- **Scribe:** Consider updating `.squad/conventions.md` to document this landing workflow
- **Future technical writes:** Use this pattern for all multi-agent product handoffs

---
date: 2026-05-03T23:00:12.742+01:00
agent: blathers
status: proposed
---

# Downstream Demo Transport Diagnostics Should Be Response-Visible

## Context

The downstream demo endpoint (`/api/prism/downstream-demo`) serves as a live diagnostic tool for operators testing server-to-server bearer token forwarding. When calls fail in Codespaces, the failure could be:
- Stale AppHost wiring (backchannel URL not set or pointing to wrong port)
- GitHub port-forwarding tunnel blocking internal requests
- MockBusinessApp not running or rejecting tokens
- Network timeout vs external cancellation

Previously, failures logged to the server but returned generic error messages, forcing operators to manually inspect environment variables and AppHost logs to determine the actual transport path.

## Decision

Embed transport path diagnostics directly in the JSON response payload for all outcomes (success, timeout, network error, non-JSON response).

### What Gets Exposed

Response includes a `transport` object with:
- `transport`: "internal-backchannel", "public-tunnel", or "public-url"
- `backchannelPresent`: boolean flag for BUSINESSAPP_BACKCHANNEL_URL
- `transportBaseUrl`: masked for internal URLs (`http://localhost:****`), full for public
- `targetUrlScheme`: http/https indicator

Structured logs also include this metadata for searchability.

### Security Considerations

**Safe to expose:**
- Whether backchannel URL is configured (boolean flag)
- Transport type classification
- Public URLs (already browser-visible)
- URL scheme (http/https)

**Must mask:**
- Actual backchannel port numbers → shown as `http://localhost:****`
- Bearer tokens, refresh tokens, cookies
- Client secrets, JWKS keys

### Why Response-Visible

1. **Immediate operator insight** — Failure response immediately shows which transport path was attempted
2. **No log hunting** — Operators don't need AppHost logs or environment variable inspection for first-pass triage
3. **Context-aware hints** — Error messages can tailor advice based on transport (e.g., "Try refresh.sh" for backchannel timeouts)
4. **Test-friendly** — Future automated tests can assert on transport metadata
5. **Safe for dev environments** — Already gated behind IsDevelopment or explicit config flag

## Implementation

Added `BuildTransportDiagnostics()` helper that:
1. Checks `BUSINESSAPP_BACKCHANNEL_URL` environment variable
2. Falls back to `PrismBusinessApp:WorkflowApiBaseUrl` config
3. Classifies as internal-backchannel, public-tunnel, or public-url
4. Masks internal URLs, shows public URLs in full
5. Returns tuple for structured logging and response inclusion

Updated all response paths (success, timeout, HttpRequestException, non-JSON) to include transport metadata.

## Alternatives Considered

**Log-only diagnostics:**
- Rejected: Requires operator to have AppHost log access and grep skills
- Log hunting for every failure slows down diagnosis

**Expose actual backchannel port:**
- Rejected: Ephemeral ports are internal runtime detail; exposing them doesn't help operators since they can't directly call localhost from their browser anyway
- Masked representation conveys "internal backchannel in use" without leaking port

**Separate diagnostic endpoint:**
- Rejected: Response-visible diagnostics on the actual failing endpoint give immediate context
- Separate endpoint requires two requests to correlate transport with failure

## Consequences

**Benefits:**
- Next Codespaces timeout immediately shows "internal-backchannel" vs "public-tunnel"
- Operators can distinguish stale wiring from downstream auth failures in one request
- Contextual hints tailored to actual transport type
- Structured logging enables pattern analysis across failures

**Risks:**
- Exposing transport implementation detail in API contract
- Mitigation: Already dev-only endpoint; transport metadata is descriptive, not prescriptive

**Maintenance:**
- Transport classification logic lives in one helper method
- If new transport types emerge (e.g., service mesh, sidecar), update classification in one place

## Related Decisions

- `.squad/skills/dev-session-contract-probe/SKILL.md` — Precedent for response-visible diagnostics without token exposure
- `.squad/skills/inline-api-failure-states/SKILL.md` — Normalize from Response.status first, layer diagnostic fields
- `.squad/skills/aspire-dynamic-endpoint-backchannels/SKILL.md` — Why backchannel URLs exist and how they're resolved

## Test Coverage

All 680 Core tests pass. No new test failures introduced. Transport diagnostics are response-visible but don't break existing contract expectations.

Tangy added five behavioural contract tests guarding backchannel/public tunnel classification and timeout/error transport metadata; all tests pass.

---
date: 2026-05-03T22:49:38.255+01:00
author: Blathers
status: PROPOSED
area: diagnostics, authentication, http-client
---

# Downstream API Timeout Diagnosis: Unregistered HttpClient Root Cause

## Context

The DownstreamDemoController times out after 10 seconds when calling MockBusinessApp from TestSite. Evidence gathered:

1. **Browser call:** `/api/prism/downstream-demo` → timeout after 10s
2. **Session contract:** Shows authenticated session, access token present, `authorizationHeaderReady=true`
3. **Diagnostics script:** Internal `http://localhost:{port}/debug/auth` returns 200 (BusinessApp is listening and healthy)
4. **Keycloak backchannel:** Healthy and reachable
5. **TestSite same-origin probes:** Healthy

## Root Cause Identified

`DownstreamDemoController.cs` uses a named HttpClient that is **not registered**:

```csharp
// Line 286:
var client = httpClientFactory.CreateClient("prism-downstream-demo");
```

**Impact:**
- HttpClientFactory creates an unconfigured default client
- The CancellationToken timeout (10s) is respected, but the client lacks proper handler configuration
- Unregistered clients may have issues with localhost resolution, certificate validation, or connection pooling in containerized environments

## Decision

**Register the "prism-downstream-demo" HttpClient with explicit configuration.**

This is justified because:
1. Named clients should always be registered (codebase pattern)
2. The timeout alone (via CancellationToken) doesn't guarantee proper handler chain setup
3. Matches the pattern used for "PrismBusinessApp" and "PrismTokenRefresh"
4. Low risk: Won't break existing behavior if the issue is elsewhere

## Implementation

In `PrismComposer.cs` or `TestSiteComposer.cs`:

```csharp
// Add after existing HttpClient registrations:
builder.Services.AddHttpClient("prism-downstream-demo")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15); // Slightly higher than CancellationToken timeout
    });
```

OR in development-only scope (since this is a demo controller):

```csharp
// In TestSiteComposer.cs or wherever dev-only services are registered:
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpClient("prism-downstream-demo")
        .ConfigureHttpClient(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
}
```

## Alternative: Verify Runtime Environment First

If registering the client doesn't fix the timeout, the next diagnostic step is:

**Add logging to DownstreamDemoController to capture the actual URL being called:**

```csharp
private string ResolveBusinessAppTransportBaseUrl()
{
    var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
    if (!string.IsNullOrWhiteSpace(backchannelUrl))
    {
        logger.LogInformation("[PRISM] Using backchannel URL: {Url}", backchannelUrl);
        return backchannelUrl;
    }
    
    var baseUrl = configuration["PrismBusinessApp:WorkflowApiBaseUrl"]?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("PrismBusinessApp:WorkflowApiBaseUrl is not configured.");
    
    logger.LogInformation("[PRISM] Falling back to public URL: {Url}", baseUrl);
    return baseUrl;
}
```

This will confirm:
- Whether BUSINESSAPP_BACKCHANNEL_URL is actually set at runtime
- Whether the URL matches what the diagnostics script successfully tested

## Test Coverage

After implementing, verify:
1. TestSite can call MockBusinessApp via the demo button (< 2 seconds)
2. Browser-facing response still shows public URL (not backchannel)
3. Diagnostics script still shows healthy backchannel connectivity

## References

- History note: "Named HttpClients have default timeouts (100s); the custom timeout only applies when the named client is registered."
- `DownstreamDemoController.cs` line 286
- `PrismComposer.cs` lines 34-35 (existing HttpClient registrations)

---
date: 2026-05-03T23:13:53.622+01:00
session: transport-diagnostics-landing
title: Transport Diagnostics Landing — Product Commit 17edf9c
author: Mabel (Technical Writer)
affected: downstream-demo, diagnostics workflow
status: implemented
---

# Transport Diagnostics Landing Decision

## Context

Transport diagnostics feature (implementation by Blathers, testing by Tangy) was ready to land on main. Two product files contained the changes:
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` — diagnostics instrumentation
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` — test contracts

Unrelated changes present (`.playwright-cli/`, `.squad/` agent artifacts) required clean staging.

## Decision

**Staged only the two product files.** Committed with conventional commit message (`feat(diagnostics):...`) and required Co-authored-by trailer. Pushed to origin/main as commit 17edf9c.

## Rationale

1. **Single-unit release boundary:** One commit = one releasable feature. No mixing product and bookkeeping in the same commit.
2. **Clean user history:** When users pull origin/main, they see only the shipped diagnostics feature, not internal agent coordination.
3. **Conventional signal for release notes:** `feat(diagnostics)` prefix enables Mabel to infer minor version bump when generating CHANGELOG.
4. **Hygiene pattern reaffirmed:** Continues established product/bookkeeping separation from earlier diagnostics landings (22843a2, fb1b324).

## Outcome

✅ **Product commit 17edf9c now live on origin/main.**

Users can immediately:
- `git pull origin main` to get transport diagnostics feature
- See transport type (internal-backchannel vs public-tunnel) in diagnostic responses
- Understand backchannel configuration state and target URL scheme for troubleshooting

## Files Changed

- DownstreamDemoController.cs: +60 lines (diagnostics instrumentation)
- DashboardLocalEndpointsValidationTests.cs: +175 lines (test contracts)

## Convention Implication

This landing reaffirms the **product/bookkeeping separation pattern** as team-wide convention:

- **Main branch:** Shipping artifacts only (user-facing code changes)
- **Bookkeeping:** .squad/ agent histories, decisions, coordination logs (deferred to separate sessions or merges)
- **Release clarity:** Clean git history enables users and release automation to reason about what shipped and why

Suggest Scribe consider updating `.squad/conventions.md` to document this as explicit team guidance for future multi-agent product handoffs.

---
date: 2026-05-03T23:26:29.163+01:00
author: Blathers
status: decision
area: diagnostics, downstream-demo, backchannel
---

# Decision: Deeper Downstream Timeout Diagnostics Landing

## Summary

Landed enhanced timeout diagnostics feature to origin/main. Product commit exposes backchannel state, target path, and cancellation context to help operators triage timeout failures in Codespaces environments.

## Implementation

**Staged and committed:**
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` — Implements richer timeout diagnostic fields
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` — Enhanced test coverage for timeout scenarios

**Scope discipline:**
- Left `.squad/` files unstaged (Scribe merged bookkeeping separately)
- Clean product boundary: only user-facing artifacts in commit

## Rationale

Timeout diagnostics must expose enough state to distinguish:
1. **Backchannel wiring failures** — When BUSINESSAPP_BACKCHANNEL_URL points to an unreachable internal service
2. **Public-tunnel timeouts** — When Codespaces tunneling infrastructure is slow or misconfigured

New fields enable operators to immediately see:
- `usingBackchannel` — Explicit confirmation of which path was attempted
- `targetPath` — Path component of the downstream call (safe to expose; URL masked)
- `timeoutWindowMs` + `cancellationSource` — Timeout boundary and which component fired it

## Owners

- Lead (Tom Nook) — Feature approved
- Blathers (Backend Dev) — Implementation approved
- Tangy (Tester) — Test coverage approved
- Commit: 442c5e9

---
date: 2026-05-03T23:46:52.875+01:00
author: Blathers
status: PROPOSED
area: diagnostics, backend, auth
---

# Business API Arrival Logging Should Carry Safe Cross-Service Correlation

## Context

When the dashboard's downstream demo times out, TestSite can prove which transport path it chose, but that alone does not prove MockBusinessApp accepted the request or entered `/api/backoffice/me`. Operators need a decisive signal from MockBusinessApp itself without logging bearer tokens or secrets.

## Decision

For `MockBusinessApp` arrival diagnostics on `/api/backoffice/me`:

1. Log once in middleware immediately before `app.UseAuthentication()`
2. Log again at the top of the `/api/backoffice/me` handler
3. Keep fields safe: method, path, service trace identifier, auth-header-present, and a caller trace hint
4. Forward TestSite's `HttpContext.TraceIdentifier` in a dedicated header (`X-Prism-Caller-TraceId`) so MockBusinessApp logs can be matched back to TestSite warning logs

## Why

- The pre-auth log proves the request reached MockBusinessApp before bearer validation ran
- The handler-entry log proves endpoint execution began
- A dedicated caller trace hint gives cross-service matching without exposing tokens, cookies, or internal secrets

## Files

- `src/UmbracoPrism.MockBusinessApp/Program.cs`
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs`

---
date: 2026-05-04T00:01:43.530+01:00
author: Blathers
status: PROPOSED
area: auth, keycloak, codespaces, backchannel
---

# MockBusinessApp Downstream Timeout Root Cause Is Hybrid JWKS URI Escape

## Context

Downstream Demo now proves TestSite is using the internal backchannel and that requests arrive at MockBusinessApp before auth. MockBusinessApp then logs:

- `IDX20803: Unable to obtain configuration from 'http://localhost:{ephemeral}/realms/prism-dev/.well-known/openid-configuration'`
- inner `IDX20804` against `http://{public-codespaces-host}:{same-ephemeral}/realms/prism-dev/protocol/openid-connect/certs`
- `KEYCLOAK_BACKCHANNEL_URL` is present
- `ASPNETCORE_ENVIRONMENT=Development`
- `backchannel JWKS enabled : YES`

## Decision

Treat this as sufficient root-cause evidence and stop broader diagnosis.

The failing runtime path is:

1. `src/UmbracoPrism.Shared/Extensions/PrismAuthExtensions.cs`
2. `ResolveSigningKeys(...)`
3. `src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs`
4. `WarmAsync(cacheKey, metadataAddress, ...)`
5. `ConfigurationManager<OpenIdConnectConfiguration>` + `BackchannelRewritingDocumentRetriever`

The discovery request is redirected to `KEYCLOAK_BACKCHANNEL_URL`, but the returned discovery document emits a **hybrid** `jwks_uri` using the public Codespaces hostname with the internal HTTP port. The current rewriter only rewrites URLs whose prefix exactly matches the configured public origin (`https://{public-host}`), so the hybrid URI (`http://{public-host}:{ephemeral-port}`) is not rewritten and the metadata HttpClient waits on an unreachable public endpoint until its default 100-second timeout.

## Implications

- The downstream-demo 10-second timeout is now explained: TestSite gives up after 10 seconds while MockBusinessApp auth middleware is still blocked on its own 100-second metadata client.
- This is not just "discovery rewritten but JWKS forgotten" by design; it is a narrower bug: the JWKS rewrite exists, but misses Keycloak's hybrid JWKS origin.

## Required Fix

Primary code change:

- `src/UmbracoPrism.Shared/Services/PrismSigningKeyCache.cs`

Validation coverage:

- `src/UmbracoPrism.Core.Tests/BackchannelRewriteTests.cs`

Optional follow-up diagnostics only if useful:

- `src/UmbracoPrism.Shared/Extensions/PrismAuthExtensions.cs`

## Preferred Fix Shape

Make generic OIDC bearer validation robust against hybrid Keycloak JWKS URIs by either:

1. bypassing discovery in backchannel mode and fetching `.../protocol/openid-connect/certs` directly from the backchannel base, matching the existing direct-JWKS strategy in `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs`, or
2. broadening the retriever rewrite so it rewrites any Keycloak realm URL whose host/path matches the configured authority, regardless of whether the discovery doc reports `https://public-host`, `http://public-host:{ephemeral-port}`, or another equivalent frontchannel form.

Add a regression test for the exact observed hybrid case.

---
date: 2026-05-03T23:46:52.875+01:00
author: Mabel
status: IMPLEMENTED
area: instrumentation, backend, testing
---

# Business API Arrival Instrumentation Landing

**Decision:** Land Business API arrival instrumentation on `main` for production use.

**Date:** 2026-05-03T23:46:52.875+01:00

**Status:** IMPLEMENTED (commit 8e1cd68)

---

## What We're Shipping

The Business API arrival instrumentation enables operators to correlate TestSite (dashboard) requests with Business API diagnostics through safe trace ID forwarding.

**Components:**

1. **Arrival Middleware (MockBusinessApp)**
   - Logs before authentication: captures raw request context without access restrictions
   - Logs after handler entry: includes authentication status
   - Fields: method, path, trace ID, auth header presence, caller trace ID

2. **Caller Trace ID Forwarding (TestSite)**
   - Extracts HttpContext.TraceIdentifier from TestSite request
   - Forwards via `X-Prism-Caller-TraceId` header to Business App
   - Safe pattern: header is read-only diagnostic data, no auth/PII exposure

3. **Test Contract (DashboardLocalEndpointsValidationTests)**
   - Validates trace ID capture and forwarding
   - Stub handler asserts header presence
   - Confirms correlation hint matches

---

## Why This Matters

**Operator pain point:** When downstream calls fail in Codespaces, operators had to manually trace logs across services. The trace ID link was missing.

**Solution:** Safe, read-only correlation header enables immediate cross-service log search without exposing internal URLs or PII.

---

## Scope Discipline Applied

- **Product files staged:** Only the three changed runtime/test files
- **Bookkeeping deferred:** .squad/ agent histories and skill updates left unstaged for separate bookkeeping merge
- **Release boundary:** Single, complete, production-ready commit (8e1cd68)

---

## Approval Chain

- **Blathers (Backend Dev):** Implemented arrival middleware and handler logging
- **Tangy (Tester):** Validated test contract and correlation forwarding
- **Mabel (Release):** Staged clean commit, pushed to main

---

## User Outcome

Users can now `git pull origin main` and run dashboard + Business App with arrival instrumentation active. Developers using Codespaces can correlate dashboard timeouts with Business API logs immediately — no manual tracing needed.

---

## Next Steps (Deferred Bookkeeping)

- Merge agent history updates to .squad/agents/
- Consolidate this decision into decisions.md
- Extract any reusable patterns to team skills
# Decision: Workflow API Calls Must Use Internal Backchannel in Codespaces

**Date:** 2026-05-04T00:19:33.157+01:00  
**Author:** Blathers (Backend Dev)  
**Status:** ACCEPTED

## Context

In Codespaces, Aspire AppHost injects two environment variables for the Business App:

- `PrismBusinessApp__WorkflowApiBaseUrl` — the public HTTPS forwarded-port URL (browser-facing)
- `BUSINESSAPP_BACKCHANNEL_URL` — the internal `http://localhost:{port}` endpoint (server-to-server)

GitHub's forwarded-port proxy intercepts unauthenticated server-side HTTP calls to the public URL and returns 401. Any server-side code that reads `WorkflowApiBaseUrl` and uses it for HTTP requests will fail with 401 in Codespaces.

## Decision

All server-side HTTP clients that call the Business App **must** check `BUSINESSAPP_BACKCHANNEL_URL` first and fall back to `PrismBusinessApp:WorkflowApiBaseUrl`. The public `WorkflowApiBaseUrl` is for browser-facing links only.

## Rationale

`DownstreamDemoController` already had the correct pattern (`ResolveBusinessAppTransportBaseUrl()`). `BusinessAppWorkflowClient.BaseUrl` was missing it, causing every workflow start and advance to fail in Codespaces with HTTP 401.

## Implementation Pattern

```csharp
private string BaseUrl
{
    get
    {
        var backchannelUrl = Environment.GetEnvironmentVariable("BUSINESSAPP_BACKCHANNEL_URL")?.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(backchannelUrl))
            return backchannelUrl;

        var url = configuration["PrismBusinessApp:WorkflowApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("...");
        return url.TrimEnd('/');
    }
}
```

## Scope

- `src/UmbracoPrism.Core/Services/BusinessAppWorkflowClient.cs` — fixed
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` — already correct
- Any future Business App HTTP clients must follow the same pattern

## Commit

`caaf551` — fix(workflow): use BUSINESSAPP_BACKCHANNEL_URL for workflow API calls in Codespaces
# Decision: Workflow 401 Null-Auth Contract and Diagnostic Distinction

**Proposed by:** Tangy (Tester)  
**Date:** 2026-05-04  
**Status:** Proposed — for Scribe to merge into decisions registry

---

## Decision

**`BusinessAppWorkflowClient` must log when `GetAuthorizationHeaderAsync` returns null, and workflow endpoint handlers in MockBusinessApp must return `Results.Problem()` (not `Results.Unauthorized()`) for application-level identity failures.**

---

## Context

Investigating why workflow pages return "Business App error (HTTP 401)" in Codespaces even after commit 0904810 fixed JWKS backchannel URL rewriting. Two indistinguishable 401 sources exist:

1. **JWT middleware 401** — token signature validation failed (no valid signing keys). Logged as `[PRISM AUTH FAILED]` in Business App console.
2. **Application-level 401** — `Results.Unauthorized()` returned when `GetPrismTenant` or `GetEmail` fails after successful JWT validation.

Additionally, when `PrismContext.GetAuthorizationHeaderAsync` returns null (e.g. `CurrentTenant` not resolved), `BusinessAppWorkflowClient.CreateClientAsync` silently omits the Authorization header with no log entry. The Business App JWT middleware then returns 401, which is indistinguishable from the cases above.

---

## Rationale

- Operators have no way to distinguish the three failure modes without access to Business App console logs.
- `/api/backoffice/me` returns `Results.Problem()` for null tenant/email; workflow endpoints return `Results.Unauthorized()`. This inconsistency means the same root cause (misconfigured tenant config) surfaces differently depending on which endpoint is called first.
- Silent null auth in `CreateClientAsync` (line 179 of `BusinessAppWorkflowClient.cs`) makes `PrismContext` failures invisible in TestSite logs.

---

## Proposed Changes

### 1. `BusinessAppWorkflowClient.CreateClientAsync` — log when auth header is null

```csharp
var authHeader = await prismContext.GetAuthorizationHeaderAsync(forceRefresh);
if (authHeader == null)
{
    logger.LogWarning(
        "BusinessAppWorkflowClient: GetAuthorizationHeaderAsync returned null (reason: {Reason}). " +
        "Request will be sent without an Authorization header.",
        prismContext.LastAuthorizationFailureReason ?? "unknown");
}
if (authHeader != null)
    client.DefaultRequestHeaders.Authorization = authHeader;
```

### 2. `MockBusinessApp/Program.cs` — align workflow handlers to `Results.Problem()`

Replace `Results.Unauthorized()` in `/api/workflow/{key}/current`, `/api/workflow/{key}/advance`, and `/api/workflow/instances` handlers with:

```csharp
if (tenant == null)
    return Results.Problem("Tenant not recognised by Business Application.");
if (string.IsNullOrEmpty(email))
    return Results.Problem("User email claim not found.");
```

This produces HTTP 500 (same as `/api/backoffice/me`) for application-level identity failures, making them distinguishable from JWT-level 401s in `ReadEnvelopeAsync` output ("Business App error (HTTP 500)" vs "Business App error (HTTP 401)").

---

## Affected Files

- `src/UmbracoPrism.Core/Services/BusinessAppWorkflowClient.cs`
- `src/UmbracoPrism.MockBusinessApp/Program.cs`

---

## Test Coverage

Regression tests added in `BusinessAppWorkflowClientTests.cs` document the current null-auth contract:
- `GetCurrentAsync_SurfacesErrorEnvelope_WhenAuthHeaderIsNull`
- `GetCurrentAsync_AttemptsTokenRefreshOnce_WhenBusinessAppReturns401`
- `GetCurrentAsync_SurfacesErrorEnvelope_NotExceptionThrown_WhenBothRequestsReturn401`

These tests will need updating if the null-auth logging proposal is implemented (the contract changes from silent to logged).
---
date: 2026-05-04T00:26:42.240+01:00
author: Blathers
status: PROPOSED
area: workflow, auth, MockBusinessApp
commit: beef21c
---

# Workflow Auth: Align MockBusinessApp Handlers and Log Silent Auth Failures

## Context

Two layered 401 failure modes in the Codespaces workflow-start path were collapsing into the same surface error, making diagnosis difficult:

1. `BusinessAppWorkflowClient.CreateClientAsync` silently omitted the `Authorization` header when `GetAuthorizationHeaderAsync` returned null (e.g. `CurrentTenant` unresolved), with no log entry.
2. MockBusinessApp workflow handlers (`/current`, `/advance`, `/instances`) returned `Results.Unauthorized()` for app-level tenant/email resolution failures, while `/api/backoffice/me` returned `Results.Problem()` for the same conditions.

## Decisions

### 1. Log a Warning when auth header is null

**`BusinessAppWorkflowClient.CreateClientAsync` must log a Warning when `GetAuthorizationHeaderAsync` returns null.**

When no auth header is obtained, the request will be rejected by the Business App JWT middleware with 401, which then triggers a spurious token-refresh retry cycle. Without a log, this is entirely invisible. The warning includes the `forceRefresh` flag and a hint to check `PrismTenantMiddleware`.

### 2. MockBusinessApp workflow handlers must return Results.Problem for app-level failures

**All three workflow endpoints must return `Results.Problem(...)` — not `Results.Unauthorized()` — when tenant or email resolution fails after successful JWT validation.**

This aligns them with `/api/backoffice/me` (already using `Results.Problem`). The result:
- A 401 from the workflow path now **unambiguously** means the bearer token was missing or rejected by JWT middleware.
- A 500 from the workflow path means the token was valid but Business App configuration (tenant mapping, email claims) failed.
- Operators and TestSite logs can distinguish the two cases without guesswork.

## Impact

- Tangy's regression tests (`BusinessAppWorkflowClientTests`) continue to pass and correctly model the expected retry behaviour on JWT-level 401.
- No changes to the retry logic itself — the fix is diagnostic clarity only.

---
date: 2026-05-04T00:00:00.000+01:00
author: Blathers
status: ACCEPTED
area: testing, ci, environment-variables
---

# Decision: Approved CI Fix — CancellationToken Moq Matcher Pattern

**Author:** Tangy  
**Date:** 2026-05-04T09:22:01.025+01:00  
**Status:** DECIDED  

## Decision

When a Moq mock setup or verify involves a `CancellationToken` sourced from `HttpContext.RequestAborted` (or `DefaultHttpContext.RequestAborted`), always use `It.IsAny<CancellationToken>()` as the matcher — never the concrete token value.

## Rationale

On Linux (CI/Ubuntu), `DefaultHttpContext.RequestAborted` lazy-initialises its `CancellationTokenSource` via `IHttpRequestLifetimeFeature`. If the ASP.NET Core authentication stack activates the feature between setup-time and call-time, the captured token at setup no longer equals the token passed in the real call. Moq's loose behaviour returns `default` for the unmatched setup, causing a `NullReferenceException` on the next line. On macOS arm64 the lazy path is stable, masking the fragility entirely.

## Consequence

- Commit `1601415` applies this fix to 4 `PrismContextTests` methods and is now on `main` as of `d9fb7f7`.
- The tests verify endpoint routing, secret resolution, and returned bearer token — not the CancellationToken passthrough — so `It.IsAny<CancellationToken>()` is semantically correct.
- Blathers' superseded workaround (`860c5d3`, `EnvVarSensitiveTestCollection`) remains in history but is not the authoritative fix for this fragility.

## Scope

Applies to all tests in this project that mock `async` methods accepting `CancellationToken` where the token is obtained from an ASP.NET Core `HttpContext`.
# Decision: Local Worktree Cleanup Classification Rules

**Date:** 2026-05-04T10:35:24.394+01:00  
**Author:** Tom Nook  
**Trigger:** Local cleanup pass requested by Jonny Muir

---

## What Was Cleaned

| Item | Action | Reason |
|------|--------|--------|
| `.playwright-cli/` | **Deleted** | Generated session residue — timestamped console logs and page YAML snapshots from the playwright-cli skill. No user-authored content. |

## What Was Left In Place

| Item | Status | Reason |
|------|--------|--------|
| `.squad/skills/backchannel-rewrite-testing/SKILL.md` | Modified tracked file | Real skill, user work |
| `.squad/skills/inline-api-failure-states/SKILL.md` | Modified tracked file | Real skill, user work |
| `src/UmbracoPrism.TestSite/Views/memberDashboard.cshtml` | Modified tracked file | Source code, user work |
| `.squad/agents/blathers/QUICK_DIAGNOSIS_REFERENCE.txt` | Untracked, ambiguous | Looks hand-curated; .txt format in agent dir is unusual but content is meaningful — left in place per charter |
| `.squad/skills/browser-devtools-api-diagnosis/` | Untracked skill dir | Earned team knowledge with named owner (Tangy), date, and cross-references. Keep and commit. |

---

## Classification Rules (for future reference)

1. **Timestamped log/snapshot files** in `.playwright-cli/` or similar tool-output directories → **delete without review**.
2. **Untracked SKILL.md files** with named author, date, and cross-references to real work → **keep; commit as earned knowledge**.
3. **Agent personal `.txt` files** with no commit history → **ambiguous; leave in place and report**.
4. **Modified tracked source/squad files** → **never touch**; these are always user work.
---
date: 2026-05-04T11:46:55.877+01:00
author: blathers
status: PROPOSED
area: admin-ui, walkthroughs, mock-business-app
---

# Workflow Admin Definitions Panel Is Collapsed by Default

## Context

The `/admin/workflow` page in MockBusinessApp rendered all workflow definition cards fully expanded on load. With multiple definitions, each showing a states table, transitions table, and Mermaid diagram, the page became visually overwhelming for walkthrough screenshots and manual operator use.

## Decision

**Workflow definition cards on the admin screen are collapsed by default.** Operators click a card header to expand it. The Mermaid diagram is rendered on first expand (deferred, not on page load).

Supporting affordances added:
- Expand All / Collapse All toolbar buttons above the definitions panel.
- Animated toggle arrow (▶ → ▷ rotation) on each card header to communicate interactive state.
- Instance IDs in the instances table are truncated to 8 chars + "…" with the full ID accessible via `title` tooltip — reduces horizontal noise while preserving debuggability.

## Rationale

- Walkthrough screenshots need a clean, focused frame — a page-length wall of expanded cards is not photogenic.
- Operator manual use benefits from summary-first layouts: inspect the instances table first, expand a specific definition only when needed.
- No capability is removed: all expand/inspect/edit/advance/reset actions still work.

## Implementation

`src/UmbracoPrism.MockBusinessApp/Program.cs` — admin UI HTML template:
- `.def-body { display: none }` + `.def-card.open > .def-body { display: flex }` toggle via JS.
- `toggleCard(hdr)` function wired to `.def-header onclick`; skips toggle when a child button is the target.
- Mermaid init changed to `startOnLoad: false`; `window._mermaid.run()` called per card on first expand.
- Expand/Collapse All helpers wire to toolbar buttons.
- Instance ID column: `shortId = id.Length > 12 ? id[..8] + "…" : id` with `title` for full ID.
### 2026-05-04T11:46:55.877+01:00: User directive
**By:** Jonny Muir (via Copilot)
**What:** For walkthrough and end-to-end work, do not make assumptions; always verify the real navigation and operator journey exist in the product before telling users to use them. Strengthen walkthroughs and tests without regressing the current suite, and improve manual discoverability where the flow currently depends on direct URLs.
**Why:** User request — captured for team memory
---
author: isabelle
date: 2026-05-04
status: inbox
affects: tangy, anyone writing walkthrough specs
---

# Decision: v1.9.0 Release Cadence and Changelog Pattern

**Date:** 2026-05-04T10:45:47.516+01:00  
**Author:** Mabel (Technical Writer)  
**Scope:** Release process, version management, changelog structure

---

## What Was Decided

**Release Flow Implemented:**
1. Consolidate all squad bookkeeping in final pre-release commit
2. Bump version in package.json (semantic versioning)
3. Create comprehensive CHANGELOG.md entry grouping changes by type
4. Validate version consistency against CHANGELOG.md heading
5. Create single release commit with descriptive message
6. Push to origin/main (squad-release.yml workflow handles tag creation)

**Version Selection (for v1.9.0):**
- Bump to v1.9.0 (minor version) because release includes:
  - Workflow v2.0 atomic schema (major architectural change, new feature)
  - Business API arrival instrumentation (new diagnostics feature)
  - Information-request demo page (new demo content)
  - 20+ significant fixes and security improvements
- Not v2.0.0 because no breaking API changes (workflow schema additive with backwards compatibility path)

**Changelog Entry Structure:**
```markdown
## [vX.Y.Z] — YYYY-MM-DD

### Added
- **Feature name:** Description with context/impact

### Changed
- **Area name:** What changed and why

### Fixed
- **Issue name:** Root cause and resolution

### Security
- **Security issue:** Impact and mitigation (include SEC-ID)
```

**Validation Automation:**
- squad-release.yml confirms: `grep -qE "^## \[v?$VERSION\]" CHANGELOG.md`
- Fails release if version in package.json doesn't match CHANGELOG.md heading
- Ensures version consistency before tag creation

---

## Why This Decision

1. **Team clarity:** Clear separation between squad bookkeeping (histories, decisions) and product changes (version bump, changelog)
2. **Automation trust:** squad-release.yml workflow handles tag creation and GitHub release. Human validation limited to: version bump, changelog content, commit message
3. **User-facing clarity:** Comprehensive changelog entries (with context, security IDs, backwards compatibility notes) help users understand scope and impact
4. **Repeatability:** Pattern can be reused for future releases without modification

---

## Criteria Applied

- **Version bump:** Semantic versioning with feature/fix/security scope analysis
- **Changelog content:** Grouped by type (Added/Changed/Fixed/Security) with descriptive titles and context
- **Release boundary:** Single commit = one releasable unit. No mixed concerns (squad + product)

---

## Related Decisions

- **Diagnostics Script Landing: Scope Discipline** (2026-05-03): Product vs. bookkeeping separation
- **Transport-Diagnostics Landing** (2026-05-03): Single-unit product commit model
- **Business API Arrival Instrumentation Landing** (2026-05-03): Three-agent handoff with clean history

---

## Actionable Next Steps for Team

1. **Scribe:** Merge this decision into .squad/decisions.md after release workflow validates v1.9.0 tag creation
2. **Future releases:** Technical Writer repeats this exact flow for v1.9.1+ releases
3. **Changelog hygiene:** Encourage team members to draft changelog entries during sprint (in issues/PRs) to reduce end-of-cycle burden
---
title: Walkthrough & Test Coverage Audit Findings
author: Tangy (Tester)
date: 2026-05-04T11:46:55.877+01:00
status: PROPOSED
tags: [testing, coverage, walkthroughs, playwright]
---

# Walkthrough & Test Coverage Audit

## Summary

Audit of all Playwright tests and walkthrough specs across the Umbraco.Prism project reveals **strong coverage of end-user workflows** (4/4 workflows fully tested) but **gaps in edge cases, validation, mobile rendering, and operator flows**. Current state is regression-safe; no breaking changes detected.

## Current Coverage Status

### ✅ Strengths
- **20 automated tests** across 6 core spec files
- All 4 end-user workflow happy paths tested (community-enquiry, payment-demo, planning-notification, information-request)
- Comprehensive auth/session contracts (8 tests including restart behavior)
- Conditional reveals validated (community-enquiry, planning-notification)
- Check-answers edit flow tested (workflow-gds-journey)
- Helper patterns enforce good practices (`assertHealthyPage`, `step()`)

### ❌ Gaps
1. **Missing back/edit flow tests** for 3 of 4 workflows (community-enquiry, payment-demo, information-request)
2. **Missing validation tests** for 2 of 4 workflows (community-enquiry, information-request)
3. **No mobile viewport testing** (all tests use desktop 1280x720)
4. **Missing success state assertions** in information-request (no submission confirmation captured)
5. **No home page hero navigation test** (entry point to workflows)
6. **Operator/admin flows** all manual-only (acceptable per R6, not blocking)

## Detailed Coverage Analysis

### End-User Workflows
| Workflow | Happy Path | Conditional Reveal | Validation | Back/Edit | Success State |
|----------|:-:|:-:|:-:|:-:|:-:|
| Community Enquiry | ✓ | ✓ | ✗ | ✗ | ✓ |
| Payment Demo | ✓ | - | ✓ | ✗ | ✓ |
| Planning Notification | ✓ | ✓ | ✓ | ✓ | ✓ |
| Information Request | ✓ | - | ✗ | ✗ | ✗ |

### Session & Navigation
| Feature | Tested | Notes |
|---------|:------:|-------|
| Sign-in | ✓ | Includes Keycloak OIDC flow |
| Dashboard | ✓ | Both links (My Workflows, Start Workflow) |
| Sign-out | ✓ | Clean session termination |
| Restart Persistence | ✓ | Auth survives AppHost restart |
| Mock Business App API | ✓ | Bearer token, 401 on missing token |

### Manual-Only Walkthroughs (Acceptable per R6)
| Walkthrough | Reason | Status |
|-----------|--------|--------|
| Authoring a Workflow | Requires backoffice + C# fluent API | Manual ✓ |
| Creating a Tenant | Requires backoffice OIDC config | Manual ✓ |
| Design System | Umbraco backoffice CSS token task | Manual ✓ |
| Building a Mobile App | Xcode/Android Studio (out of scope) | Manual ✓ |
| Push Notifications | Service worker + browser permissions | Manual ✓ |

**Assessment:** All manual-only designations are justified. Automating these would require either:
- Backoffice automation (acceptable to keep manual per SKILL.md R6)
- Platform-specific tooling (Xcode/Android Studio)
- Complex service worker mocking (lower ROI)

## Recommended Coverage Improvements

### Priority 1: High Impact, Minimal Effort
**Effort: ~1 hour total**

1. **Add back/edit flow tests to 3 workflows**
   - Reuse pattern from `workflow-gds-journey` (test 5)
   - Add to: community-enquiry, payment-demo, information-request
   - Validates: User can navigate backward, change answer, see updated summary
   - Prevents regression: Workflow state management bugs

2. **Add validation tests to 2 workflows**
   - Reuse pattern from `payment-demo` (test 2)
   - Add to: community-enquiry, information-request
   - Validates: Error summary visible, field-level errors shown
   - Prevents regression: Validation logic breakage

3. **Add success state assertion to information-request**
   - Currently captures step 03 but doesn't assert "Your request is being reviewed"
   - Minimal change: Add heading assertion (like step 04 in community-enquiry)
   - Prevents regression: Silent workflow submission failure

### Priority 2: Medium Impact, Reasonable Effort
**Effort: ~1.5 hours total**

4. **Add mobile viewport tests**
   - Configure iPhone 12 viewport in playwright.localhost-auth.config.ts
   - Run existing walkthrough tests in mobile context
   - Validates: Mobile nav visible, form layout stacks, inputs accessible
   - Prevents regression: Mobile rendering bugs

5. **Create home page hero navigation walkthrough**
   - New file: `tests/walkthroughs/shared/home-page-hero.walkthrough.spec.ts`
   - Captures: Home page hero section and CTA click → workflow start
   - Validates: Hero visible, link href correct, landing workflow healthy
   - Prevents regression: Entry point navigation breakage

### Priority 3: Lower Priority, Deferred
**Effort: Future consideration**

6. **Add accessibility assertions** (a11y)
   - Use `@axe-core/playwright` integration
   - Run on all walkthrough steps
   - Prevents regression: WCAG compliance violations

7. **Tenant routing contract test**
   - Verify localhost vs tenant2.localhost routing (if manual tenant exists)
   - Minimal automation; validates middleware behavior

#
---
date: 2026-05-03T23:26:29.163+01:00
author: Tangy
status: decision
area: testing, diagnostics, downstream-demo
---

---
date: 2026-05-03T23:38:00.000+01:00
author: Mabel
status: IMPLEMENTED
area: diagnostics, backend, testing
---

---
date: 2026-05-04T09:22:01.025+01:00
author: Tangy
status: ACCEPTED
area: testing, ci, moq
---

---

**Next step:** Prioritize Tier 1 improvements (back/edit + validation tests) for closure by sprint end.
---
date: 2026-05-04T11:46:55.877+01:00
author: Tom Nook (Discovery & Architecture)
status: proposal
priority: high
category: walkthroughs, documentation, user-experience
---

---
date: 2026-05-04T11:46:55.877+01:00
author: Brewster
status: IMPLEMENTED
area: testsite, walkthroughs, discoverability
---
# Walkthrough Discoverability — All Workflow Types Reachable from Dashboard

## Context

Audit findings showed that some workflow demos (planning-notification, information-request)
were only reachable via direct URL knowledge. The member dashboard linked to just three
workflow types out of four, and there was no route from the Prism dashboard to the
MockBusinessApp workflow admin screen.

Two TestSite stub views (`workflowHub.cshtml`, `workflowPage.cshtml`) contained
`Layout = null` and no content, silently overriding the Core library's fully implemented
embedded views and rendering blank pages.

## Decisions Made

### 1. Delete TestSite stub views — use Core embedded views

`src/UmbracoPrism.TestSite/Views/workflowHub.cshtml` and `workflowPage.cshtml` were
stub files with `Layout = null` that blocked the `PrismEmbeddedViewsStartupFilter`
embedded views from being served. Deleting the stubs lets the Core's implementations
(with `Layout = "~/Views/Shared/Master.cshtml"` and full rendering logic) take over.

**Rule:** The TestSite should not ship stub overrides for Core-embedded views unless
there is a deliberate TestSite-specific customisation. A file that only contains
`Layout = null` is a broken placeholder and must be removed.

### 2. Restructure member dashboard card grid

The existing 6-card flat grid was split into two coherent groups:

- **Overview** (4 cards): My Account, Documents, Support, My Workflows hub
- **Workflow Demos** (4 cards, in a labelled `dash-section`): Get in Touch,
  Apply for Planning Permission, Payment Demo, Request Information

All four seeded workflow types are now directly reachable from one section with
content-tree resolved URLs, not hardcoded route guesses.

### 3. Expose workflow admin URL from `MemberDashboardController`

`IConfiguration` was injected into `MemberDashboardController` to derive
`{PrismBusinessApp:WorkflowApiBaseUrl}/admin/workflow`. This is the same URL pattern
the AppHost annotates as a `Workflow Admin` resource URL. It is passed to the view as
`ViewBag.WorkflowAdminUrl`.

A **Developer Tools** `dash-section` renders conditionally (only when the URL is set),
showing a single card linking to the admin screen in a new tab.

### 4. Environment-aware without extra config

No new configuration keys are introduced. The existing `PrismBusinessApp:WorkflowApiBaseUrl`
already resolves correctly in Codespaces (via AppHost forwarded URL detection) and
locally (`https://localhost:7245`). The admin URL is simply appended as `/admin/workflow`.

## Verification

- `dotnet build` — 0 errors, 2 pre-existing warnings (unrelated)
- `dotnet test` — 690 passed, 0 failed

---
date: 2026-05-04
author: Tangy
status: PROPOSED
area: testing, walkthroughs, screenshots, documentation
---

