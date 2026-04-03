# Decisions

Umbraco.Prism team decisions. Append-only ledger.

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

## 📌 2026-03-28: P1 #5 Completed — Tenant Cache Invalidation Strategy

**Decision:** Centralize tenant-cache invalidation in `ITenantService` and instrument cache behavior with runtime counters.

**Implementation policy:**
- Tenant cache entries are invalidated via `ITenantService.InvalidateDomain(s)` only.
- Tenant-affecting writes (create/update/delete) must trigger invalidation through the service, not direct controller cache-key manipulation.
- Tenant cache observability counters are required: `Hits`, `Misses`, `Invalidations`, `DatabaseLoads`.

**Validation evidence:**
- Added stress-oriented cache strategy tests in `TenantServiceCacheStrategyTests`:
	- repeated lookup hit/miss effectiveness
	- high-tenant invalidation deduplication across 2,000 domains
	- post-invalidation forced refresh behavior
- Core test suite passed (`36` succeeded, `0` failed).

**Issue impact:**
- GitHub issue #5 closed as **completed**.

---

## 📌 2026-03-28: P1 #6 Completed — Branding Load-Path Optimization + Cache-Coherence Coverage

**Session Log:** `.squad/log/2026-03-28T07:47:36Z-issue-6-branding-optimization.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-branding-load-path.md`
- `.squad/decisions/inbox/blathers-tunnel-input-clarity.md` (deduped; already captured in 2026-03-22 Tunnel Input Clarity decision)

### Blathers — Branding Load Path Hot-Path Optimization (Issue #6)

**Decision:** Precompute normalized branding CSS declarations at tenant cache-load time in `TenantService` and consume those declarations directly in `PrismBrandingMiddleware` during HTML injection.

**Conventions adopted:**
- Keep tenant override dictionaries as the source representation for correctness and compatibility.
- Add runtime-only `PrismTenant` fields for precomputed desktop/mobile declaration strings.
- In middleware, prefer precomputed declarations when available and fall back to dictionary rendering when not.
- Preserve existing tenant cache invalidation behavior (`InvalidateDomain(s)`) as the coherence mechanism for rebuilds after tenant updates.

**Why:** Reduces request-path CPU work under high tenant/request volume by eliminating repeated dictionary iteration, trim operations, and declaration concatenation while keeping scope low-risk.

**Validation:** Focused tests passed (`19/19`) across `TenantServiceCacheStrategyTests`, `PrismBrandingMiddlewareTests`, and `BrandingServiceTests`.

### Tangy — Parallel Cache-Coherence and Update Behavior Test Expansion

**Decision:** Expand branding-path regression tests to verify cross-tenant isolation and same-tenant update reflection behavior under sequential request patterns.

**Why:** Optimization changes were safe to ship only with explicit assertions that stale branding values do not bleed across tenant boundaries and that cache invalidation still refreshes outputs correctly.

**Validation:** Focused branding test run passed for affected test classes.

**Issue impact:**
- GitHub issue #6 closed as **completed**.
- Stale `go:needs-research` label removed.

---

## 📌 2026-03-28: Copper Security Hardening Check + Reliability Boundaries

**Session Log:** `.squad/log/2026-03-28-copper-signing-key-hardening-check.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/copilot-directive-20260328T074900Z.md`
- `.squad/decisions/inbox/copper-signing-key-security.md`
- `.squad/decisions/inbox/tangy-issue-7-reliability.md`

### User directive captured (Jonny Muir via Copilot)

**Decision:** Security work for this round must explicitly include Copper review and hardening ownership.

**Why:** Keep this auth/security slice water-tight and clearly accountable.

---

### Copper — Signing-key warm-path availability hardening

**Decision:** Add a short per-tenant forced-refresh cooldown in signing-key cache warm logic.

**Conventions adopted:**
- Add `ForcedRefreshCooldown` (30s) in signing-key cache warm path.
- In `WarmAsync(..., forceRefresh: true)`, skip metadata fetch when same tenant was refreshed inside cooldown.
- Preserve existing tenant-level lock and overlap deduplication behavior.

**Why:** Bound metadata fetch amplification during unknown-`kid` token bursts without changing fail-closed key behavior.

**Security effect:**
- Confidentiality and integrity remain fail-closed.
- Availability improves by rate-limiting forced refresh pressure per tenant.

**Validation:** Focused suite passed (20/20): `PrismSigningKeyCacheTests`, `PrismOidcConfigurationTests`, `PrismTokenRefreshServiceTests`, `PrismTenantMiddlewareTests`.

**Residual follow-up:** Downstream `PrismAuthExtensions` synchronous metadata retrieval remains a separate availability hardening candidate.

---

### Tangy — Reliability test boundaries for Issue #7

**Decision:** Keep reliability assertions aligned to current architecture and implementation boundaries.

**Conventions adopted:**
- OIDC tests assert missing/rotated keys trigger async background warm, not request-path blocking.
- Refresh resilience tests use token-endpoint partitioning as isolation boundary and verify open-circuit short-circuit behavior for concurrent callers.
- Tenant/branding race tests allow old-or-new snapshots but reject hybrid torn states.

**Why:** Cover real reliability risks without encoding contracts stronger than current implementation.

**Validation:** Focused Core run passed (27/27). Issue #7 remains open for Copper review.

---

## 📌 2026-03-28: P1 #7 Completed — Reliability Expansion Closed with Security Gate (Tangy + Copper)

**Session Log:** `.squad/log/2026-03-28-issue-7-completion.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/tangy-issue-7-reliability.md`
- `.squad/decisions/inbox/copper-issue-7-security-gate.md`

### Tangy — Reliability completion acceptance

**Decision:** Reliability acceptance for Issue #7 is satisfied by the current test suite and focused validation.

**Delta recorded (deduped):**
- Captured completion evidence for the full Issue #7 reliability scope in one focused run.
- Confirmed CI inclusion remains automatic because tests are standard xUnit coverage under `src/UmbracoPrism.Core.Tests`.

**Validation evidence:** Focused run passed (`32` passed, `0` failed).

---

### Copper — Security gate outcome for Issue #7

**Decision:** Security review is **pass-with-conditions** and acceptable for Issue #7 closure.

**Conditions locked:**
1. Keep focused security tests in CI as blocking gate checks.
2. Track downstream synchronous metadata retrieval in `PrismAuthExtensions` as a separate availability hardening follow-up.

**Validation evidence:** Focused security run passed (`19` passed, `0` failed).

**Issue impact:**
- GitHub issue #7 closed as **completed**.

---

## 📌 2026-03-28: PrismAuthExtensions Sync-Metadata Mitigation Completed + Security Gate (Blathers + Copper)

**Session Log:** `.squad/log/2026-03-28-prismauth-sync-metadata-mitigation.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-prismauth-sync-metadata-hardening.md`
- `.squad/decisions/inbox/copper-prismauth-mitigation-security-gate.md`

### Blathers + Copper — Merged mitigation and security outcome

**Decision:** Accept and record completion of the PrismAuthExtensions sync-metadata mitigation with security gate **pass**.

**Conventions locked:**
- Downstream signing-key resolution in `PrismAuthExtensions` remains cache-first and non-blocking on request paths.
- Unknown, stale, or untrusted-key states fail closed (empty key set).
- Tenant allow-list and tenant-bound issuer/audience checks remain mandatory.

**Why:** Closes the previously tracked downstream synchronous metadata retrieval availability risk while preserving tenant isolation and fail-closed trust behavior.

**Validation evidence:** Focused suites reported pass in the merged reviews (mitigation and security gate) with zero failures.

**Outcome:** Security gate is **pass**; mitigation is complete.

## 📌 2026-03-28: README & Marketplace Improvements (Mabel)

**Session Log:** `.squad/log/2026-03-28-readme-improvements.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/mabel-readme-review.md`
- `.squad/decisions/inbox/mabel-readme-improvements.md`

### Mabel — README & Marketplace Structural Improvements

**Decision:** Implement 7 targeted README and marketplace improvements to reduce developer onboarding friction and clarify optional tooling.

**Changes Implemented:**

**HIGH PRIORITY (Required fixes)**
1. **Marketplace JSON Description** — Fixed `umbraco-marketplace.json` Description to accurately reflect multi-tenancy platform (was: "syntax highlighting package")
2. **Prerequisites Section** — Added top-level Prerequisites section with .NET 10.0, Node.js 20+, Azure Key Vault, Entra ID, and mandatory `npm install` callout

**MEDIUM PRIORITY (Implemented cleanly)**
3. **VS Code Extensions Optional** — Changed Storybook and Core tests language from "Install" to "Optionally, install" with CLI alternatives (`npm run test:playwright:ui`, `dotnet test`)
4. **WCAG Opt-Out Code Example** — Added TypeScript code block showing `.stories.ts` usage pattern for `parameters: { a11y: { disable: true } }`
5. **Sample Projects Promotion** — Expanded with use cases, TestSite tenant guidance, and forward reference to "Local Authentication Walkthrough"

**LOW PRIORITY (Also implemented)**
6. **PrismAdmins Note Clarity** — Updated note format to "⚠️ Pending (2026-03-22)" with "not yet shipped" indicator and issue #4 reference
7. **Tunnel Behavior Rationale** — Added explanation: "This prevents redirect URI sprawl accumulating in Entra over repeated dev sessions"

**Files Modified:**
- `README.md` — 8 targeted edits; ~150 lines added/updated
- `umbraco-marketplace.json` — 1 Description field edit

**Validation:**
- ✅ Markdown structure validated
- ✅ All 7 issues addressed
- ✅ No content broken or removed
- ✅ Links and references preserved
- ✅ Tone consistent

**Impact:** Developers now reach "running local Prism instance" with clearer onboarding path, see dependencies upfront, understand optional tooling, have code examples for common patterns, and know where to find working examples.

**Outcome:** All 7 improvements complete and ready for deployment.

## 📌 2026-03-28: Mabel granted release management powers

**By:** Jonny Muir (via Copilot)

**What:** Mabel's charter expanded to include semantic versioning, release cutting, CHANGELOG authoring, and version bumps across csproj + package.json. She infers semver bump automatically from git log using conventional commit signals.

**Why:** User requested dedicated release versioning ownership for the Technical Writer role.

---

## 📌 2026-03-28: Conventional Commits Directive + Mabel Release Powers (User + Copilot)

**By:** Jonny Muir (via Copilot)

### Conventional Commits Standard (Team-wide)

**Decision:** All agents who commit code must follow the conventional commits standard (`feat:`, `fix:`, `perf:`, `chore:`, `docs:`, `test:`, `refactor:`, `style:` prefixes, and `feat!:` or `BREAKING CHANGE:` footer for breaking changes).

**Why:** Mabel's automated semver versioning depends on clean commit signals to infer the correct version bump. Unflagged breaking changes will ship with incorrect semver and no user warning; commit discipline is a prerequisite for reliable release notes.

**Conventions locked:**
- Every commit message MUST use a conventional type prefix (see `.squad/skills/conventional-commits/SKILL.md` for full reference).
- Breaking changes MUST be flagged with `!` (e.g., `feat!:`) or a `BREAKING CHANGE:` footer and discussed with Tom Nook (Lead) before committing.
- Mabel infers semver bump automatically from `git log` using conventional commit signals.

**Skill Reference:** `.squad/skills/conventional-commits/SKILL.md` — All committing agents must read this before every commit to stay aligned.

**Impact:** All committing agents (Tom Nook, Isabelle, Blathers, Tangy, Celeste, Copper, Mabel) must adopt this standard immediately. Release notes and versioning accuracy depend on this.

## 📌 2026-03-29: Release v1.2.0 (Mabel)

**Session Log:** `.squad/log/2026-03-28T10:19:29Z-release-v1.2.0.md`  
**Orchestration Log:** `.squad/orchestration-log/2026-03-28T10:19:29Z-mabel.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/mabel-release-1.2.0.md`

### Mabel — Release v1.2.0 Decision

**Decision:** Released Umbraco Prism v1.2.0 — a minor version bump covering the first comprehensive feature set.

**Semver Signal:**
- **Commits:** 53 `feat:` commits + multiple `fix:`, `perf:`, `docs:`, `chore:` commits
- **Breaking Changes:** None (`BREAKING CHANGE:` footer absent; no `!` type markers)
- **Bump:** MINOR (v1.1.2 → v1.2.0)

**Justification:**
The project has accumulated significant new capabilities warranting a minor version bump:
- Mobile app generation (Capacitor scaffold + iOS/Android emulator support)
- Tenant cache metrics & diagnostics
- Cloudflared tunnel automation for dev
- OIDC per-tenant configuration
- Branding middleware for tenant customization
- Authorization planes for secure tenant isolation
- Storybook + Playwright integration for testing
- Full tenant CRUD in backoffice
- Squad project management framework

This represents the first full-feature release, moving from development versioning (v1.1.2 placeholder) to production-ready versioning after 4 months of substantial development.

**Artifacts Created:**
- **CHANGELOG.md** — New file with 39 entries organized into three categories:
  1. New Features (20+ entries: Squad framework, mobile generation, tenant management, OIDC, branding, authorization, Storybook)
  2. Bug Fixes & Improvements (15+ entries: stability, tooling, configuration)
  3. Documentation (4 entries: README clarity, onboarding, marketplace metadata)
- **Version Synchronization:**
  - `package.json`: 0.0.0 → 1.2.0 (placeholder to production)
  - `csproj`: 1.1.2 → 1.2.0 (synced to minor bump)
- **Git Tag:** `v1.2.0` created with release commit `0059954`

**Changelog Style:**
All entries use plain English (no raw commit hashes or internal references). Each entry answers: "What changed and why does it matter to me?"

**Why:** Mabel's release decision follows conventional commit signals and semver classification to deliver accurate, user-focused release notes that communicate project maturity and feature completeness to stakeholders.

**Impact:** v1.2.0 is now the canonical production release. The project moves from alpha/beta versioning (v1.1.2) to minor version releases, enabling predictable SemVer-based dependency management and clear feature communication to users.

---

---

## 📌 2026-03-28: Blob URL Download Pattern for SPA Environments (Isabelle)

**Session Log:** `.squad/log/2026-03-28T11:19:31Z-blob-url-fix.md`

### Isabelle — Blob URL Download Pattern for SPA Environments

**Decision:** For all programmatic file downloads using blob URLs, adopt the pattern:

```typescript
const url = URL.createObjectURL(blob);
const anchor = document.createElement('a');
anchor.href = url;
anchor.download = fileName;
anchor.style.display = 'none';
anchor.target = '_blank';           // Prevents router interception
anchor.rel = 'noopener noreferrer'; // Security best practice
document.body.appendChild(anchor);
anchor.click();
document.body.removeChild(anchor);
URL.revokeObjectURL(url);
```

Button click handlers triggering downloads should call `preventDefault()` and `stopPropagation()`.

**Root Cause:** Umbraco's SPA router (activated by UmbracoApplicationUrl config) intercepts all `<a>` click events for client-side navigation. When the download anchor was clicked, the router captured the event and attempted `history.pushState()` on the blob: URL, which browsers reject for security.

**Why:** Prevents SecurityError and enables clean blob-based downloads for any file type (ZIP, PDF, images, CSVs, etc.) without triggering SPA navigation.

**Implementation:** Fixed in `src/UmbracoPrism.Client/src/prism-create-tenant-modal.ts` lines 793-851

**Team Notes:**
- **Blathers:** Use this pattern for any backend endpoints returning binary downloads.
- **Tangy:** Consider Playwright tests verifying downloads complete without navigation errors.
- **All:** This applies to any SPA with client-side routing — always set `target="_blank"` on programmatic download anchors.

---

## 📌 2026-03-28: Biometric Auth Architecture for Prism Mobile (Tom Nook, Copper, Kicks)

**Session Log:** `.squad/log/2026-03-28T11:55:34Z-biometric-design.md`

**Orchestration Logs:**
- `.squad/orchestration-log/2026-03-28T11:55:34Z-tom-nook.md` — Architecture overview
- `.squad/orchestration-log/2026-03-28T11:55:34Z-copper.md` — Security threat model
- `.squad/orchestration-log/2026-03-28T11:55:34Z-kicks.md` — Native implementation patterns

**Merged From Inbox:**
- `.squad/decisions/inbox/tom-nook-biometric-arch.md`
- `.squad/decisions/inbox/copper-biometric-security.md`
- `.squad/decisions/inbox/kicks-biometric-native.md`

**Design Document:** `/Design/biometric-auth.md`

### Tom Nook — Biometric Auth Architecture Decisions

**Decision 1:** Opaque server-issued BiometricToken instead of raw Entra tokens on device.

The device Keychain/Keystore stores an opaque Prism-issued `BiometricToken` (UUID v4). The Entra `refresh_token` is stored encrypted on the server only.

**Rationale:** Keeps Entra credentials off the device entirely. Enables server-side revocation without Entra involvement. Limits blast radius if a device is compromised — the BiometricToken is useless without the server-side record and is rate-limited at the `/exchange` endpoint.

---

**Decision 2:** `/exchange` endpoint sets PrismMemberCookie directly.

`POST /umbraco/prism/mobile/biometric/exchange` accepts the BiometricToken, performs Entra token refresh server-side, and returns a `Set-Cookie: PrismMemberCookie` response. The native Capacitor layer reads the cookie and injects it into the WebView store.

**Rationale:** Reuses the existing `PrismMemberCookie` auth mechanism unchanged. No new WebView session model required. Keeps tokens out of WebView JS (avoids XSS exposure vector). Consistent with how the existing OIDC flow establishes the WebView session.

---

**Decision 3:** Cookie injection is native-layer responsibility, not WebView JS.

After the exchange call, the Capacitor native layer injects the `PrismMemberCookie` into the WebView via platform APIs (`WKHTTPCookieStore` on iOS, `CookieManager` on Android) before triggering navigation.

**Rationale:** `Set-Cookie` headers on cross-origin HTTP responses are not accessible to WebView JS. The native HTTP client receives the full response including headers. Injecting from native is the correct platform pattern and avoids needing a JS-readable token at any point.

---

**Decision 4:** Rolling refresh token rotation is a v1 hard requirement.

On each successful `/exchange`, the server replaces the stored Entra refresh_token with the newly issued one (rolling rotation). This is NOT deferred to v1.1.

**Rationale:** Without rolling rotation, a stolen BiometricToken (before detection) can be used indefinitely as long as the Entra refresh token remains valid. Rolling rotation limits the window to one use per exchange.

---

**Decision 5:** BiometricAuthEnabled is opt-in in MobileBundleService.

`PrismMobileBundleRequest` gains an optional `BiometricAuthEnabled` flag. Existing bundles without this flag are unaffected. New bundles with `BiometricAuthEnabled: true` include biometric bridge code and updated package.json dependencies.

**Rationale:** Prevents breaking changes to existing generated apps. Tenant operators choose to adopt biometric login explicitly.

---

**Decision 6:** Biometric enrollment change triggers automatic credential wipe.

On app launch, the native layer checks if the biometric enrollment set has changed since registration. If changed, delete the Keychain credential and force full OIDC re-auth (then re-offer enrol).

**Rationale:** Prevents a scenario where a new fingerprint added to a device inherits the previous owner's stored credential. Standard security practice on both iOS and Android; both platforms provide this signal.

---

### Copper — Biometric Authentication Security Model

**Decision:** Adopt Prism-issued device credentials instead of storing Entra refresh tokens on-device for biometric authentication flows.

**Rationale:** Storing Entra refresh tokens directly in device keystores creates several unacceptable risks:
1. **High-Value Target:** Refresh tokens have long lifetimes and broad OAuth scope
2. **Limited Revocation Control:** Tenant admins cannot selectively revoke device credentials without full Entra user session revocation
3. **Compliance Gap:** Violates principle of least-privilege for mobile credential storage
4. **Multi-Tenant Leakage Risk:** No tenant boundary enforcement in refresh token itself

**Proposed Architecture: Device Credential Model**

1. User completes full Entra OIDC authentication in mobile app
2. App requests device credential from Prism backend (requires valid Entra access token)
3. Server issues device-bound JWT containing:
   - Device ID (UUID generated on first registration)
   - Tenant ID (single tenant binding)
   - User ID (Entra object ID)
   - Expiration (7-30 days, configurable per tenant)
   - Signature (Prism backend signing key)
4. Device credential stored in iOS Keychain / Android Keystore with biometric access control
5. On subsequent app opens: biometric prompt → load device credential → exchange for short-lived access token → establish WebView session

**Security Properties:**
- Server-side device registry enables admin revocation
- Credential scoped to single tenant (prevents cross-tenant abuse)
- Bounded lifetime forces periodic full re-auth
- Device binding (device ID) allows detection of credential theft/replay
- No Entra token leakage on device compromise

**Required Server-Side Controls:**

1. **Device Registry Table:**
   - `DeviceId` (UUID, primary key)
   - `TenantId` (foreign key, indexed)
   - `UserId` (Entra object ID)
   - `DeviceName` (user-provided, for admin display)
   - `RegisteredAt`, `LastUsedAt`
   - `RevokedAt` (nullable)
   - `Platform` (iOS/Android)

2. **Device Credential Exchange Endpoint:**
   - `POST /api/prism/device/exchange`
   - Input: device credential JWT (from keystore)
   - Output: short-lived access token (5-15 min lifetime)
   - Validation:
     - JWT signature valid
     - Device not revoked
     - Tenant matches request context
     - Expiration not exceeded
     - Device ID binding consistent

3. **Admin Revocation API:**
   - `DELETE /api/prism/device/{deviceId}` (tenant admin only)
   - Sets `RevokedAt` timestamp
   - Subsequent exchange requests fail immediately

4. **Automatic Expiration:**
   - Maximum credential age: 30 days (recommended default)
   - Configurable per tenant security policy
   - Expired credentials → force full Entra re-auth

**Multi-Tenant Isolation Requirements:**

1. **Keystore Key Naming:**
   - Pattern: `prism_device_cred_{tenantId}_{userId}`
   - Ensures no cross-tenant credential confusion
   - Allows same device to authenticate to multiple tenants safely

2. **Credential Scoping:**
   - Device credential JWT contains `tenant_id` claim
   - Exchange endpoint validates request tenant matches credential tenant
   - Prevents credential reuse across tenants

3. **Device Registry Isolation:**
   - Device records scoped to tenant
   - Admin revocation limited to tenant-owned devices
   - Query filters always include tenant boundary

**Hard Constraints for Architecture:**

1. No Entra Refresh Token Storage in device keystore
2. Single-Tenant Binding (tenant ID in JWT)
3. Server-Side Registry (central control)
4. Bounded Lifetime (max 30 days)
5. Biometric Failure Handling (fallback to full OIDC)
6. Keystore Isolation (multi-tenant support)

**Recommended Implementation Priority:**

1. **Phase 1 (MVP):** Device credential issuance endpoint, device registry table and basic CRUD, exchange endpoint with validation, iOS/Android keystore integration with biometric access control
2. **Phase 2 (Hardening):** Admin device management UI, tenant-configurable credential lifetime, device registration approval flow, anomaly detection on exchange endpoint
3. **Phase 3 (Advanced):** Credential rotation on suspicious activity, device fingerprinting for binding validation, compliance reporting

---

### Kicks — Biometric Native Plugin & Implementation Decisions

**Decision:** Capacitor plugin stack for biometric auth.

**Selected Plugins:**
- **Biometric Authentication:** `@aparajita/capacitor-biometric-auth@7.x`
- **Secure Credential Storage:** `@aparajita/capacitor-secure-storage@7.x`

**Rationale:**
1. **Active Maintenance:** Both plugins maintained by Aparajita (verified Capacitor 7 compatibility, released 2024-2025)
2. **Native API Coverage:** Biometric plugin wraps iOS LocalAuthentication (LAContext) and Android BiometricPrompt API (API 28+) with FingerprintManager fallback (API 23-27)
3. **Secure Storage Mapping:** Direct mapping to iOS Keychain (`kSecAttrAccessibleWhenUnlockedThisDeviceOnly`) and Android Keystore-backed EncryptedSharedPreferences (AES256-GCM)
4. **TypeScript Quality:** Strong types with enums (`BiometryType`, `BiometryError`) for capability detection and error handling
5. **Fallback Support:** Built-in PIN/passcode fallback via `allowDeviceCredential: true`
6. **Consistency:** Same author ensures API surface consistency between biometric and storage plugins

**Rejected Alternatives:**
- `@capacitor-community/biometric-auth` — less active maintenance, fewer edge case handlers
- `capacitor-biometric-auth` — unmaintained (last release pre-Capacitor 5)
- `@capacitor/preferences` — no encryption layer (unsuitable for credential storage)
- `capacitor-secure-storage-plugin` — stale (Capacitor 5 era)

---

**Decision:** Platform entitlements auto-injection in bootstrap scripts.

**Convention:** Bootstrap scripts (`bootstrap-ios.sh`, `bootstrap-android.sh`) auto-inject required entitlements/permissions after `npx cap add {platform}`.

**iOS: FaceID Usage Description**
- Inject `NSFaceIDUsageDescription` into `ios/App/App/Info.plist` via perl regex
- Text: `"{appName} uses Face ID to securely log you in without requiring your password each time."`
- Reason: FaceID requires explicit usage description or biometric prompt fails silently (iOS privacy requirement); TouchID does not require description

**Android: Biometric Permission**
- Inject `<uses-permission android:name="android.permission.USE_BIOMETRIC" />` into `android/app/src/main/AndroidManifest.xml` via perl regex
- Reason: BiometricPrompt API (API 28+) requires this permission to access biometric hardware

**Why Auto-Inject:**
- Reduces operator error (forgetting to add entitlements manually)
- Maintains consistency with Prism's "zero-config mobile bundle" philosophy
- Scripts remain idempotent (check for existing entry before adding)

**Fallback:** Bundle also includes `resources/ios-info-plist-additions.xml` and `resources/android-manifest-additions.xml` for manual reference if auto-injection fails

---

**Decision:** Biometric registration flow — post-OIDC enrollment.

**Trigger:** After Entra OIDC completes successfully in WebView, prompt user to enable biometric login.

**Flow:**
1. **Detection:** WebView OIDC callback page (`/signin-oidc`) posts message to native layer via Capacitor message bridge when tokens received
2. **Capability Check:** Call `BiometricAuth.checkBiometry()` to verify `isAvailable: true`
3. **User Prompt:** Show native-style dialog: "Enable {FaceID|TouchID|Fingerprint} for faster login?"
4. **Confirmation Auth:** Prompt biometric authentication to confirm user identity (`authenticate()` with reason: "Confirm your identity to enable biometric login")
5. **Store Credential:** On auth success, store credential in SecureStorage
6. **Graceful Fallback:** If biometrics unavailable or user declines, fall back to standard web session (no enrollment)

---

**Decision:** Biometric login flow — launch-time authentication.

**Trigger:** On app launch (cold start or return from background).

**Flow:**
1. **Credential Check:** Check if credential exists in SecureStorage
2. **Biometric Prompt:** If credential exists, prompt biometric authentication (`authenticate()` with reason: "Log in with biometrics")
3. **Token Retrieval:** On auth success, retrieve credential from SecureStorage
4. **Token Exchange:** Call Entra `/token` endpoint with `grant_type=refresh_token` to obtain new access token
5. **Session Injection:** Inject access token into WebView session before page load
6. **Load WebView:** Load Capacitor WebView with session established (user bypasses OIDC login flow)

**Fallback Paths:**
- **User Cancels:** Silent fallback to standard web login (no error message)
- **Biometric Lockout:** Show error message ("Too many failed attempts. Please use your account credentials.") + fallback to web login
- **Credential Expired:** Silently clear stored credential + fallback to web login

---

**Decision:** Capability detection & graceful degradation.

**Pre-Flight Check Pattern:**
```typescript
const info = await BiometricAuth.checkBiometry();
if (!info.isAvailable) {
  // reason: BiometryError.biometryNotAvailable | biometryNotEnrolled | ...
}
```

**Fallback Strategy:**
1. **Simulator/Emulator:** `isAvailable: false` → Hide biometric enrollment option; web login only
2. **Biometrics Not Enrolled:** Show informational message: "Enable Face ID in Settings to use biometric login." Do not offer enrollment.
3. **Hardware Not Available:** Hide biometric features entirely
4. **Biometric Lockout (5 failed attempts):** Immediately fall back to web login with message
5. **Accessibility Users:** Respect system-wide biometric disable settings; always provide web login fallback

**Principle:** Never block app usage if biometrics fail. Always provide "Skip" or "Use Password" option.

---

**Decision:** MobileBundleService C# changes.

**Changes Required:**
1. **`BuildPackageJson()`:** Add `@aparajita/capacitor-biometric-auth` and `@aparajita/capacitor-secure-storage` to `dependencies` section
2. **New Method:** `BuildIosInfoPlistAdditions(string appName)` → returns XML snippet for manual reference
3. **New Method:** `BuildAndroidManifestAdditions()` → returns XML snippet for manual reference
4. **Update:** `BuildBootstrapIosScript()` to auto-inject FaceID usage description (perl regex before closing `</plist>` tag)
5. **Update:** `BuildBootstrapAndroidScript()` to auto-inject biometric permission (perl regex after `<manifest>` opening tag)
6. **Update:** `BuildReadme()` to add "Biometric Login Setup" section with iOS/Android requirements
7. **In `BuildBundleAsync()`:** Add two new entries: `resources/ios-info-plist-additions.xml` and `resources/android-manifest-additions.xml`

**No Changes Needed:**
- `capacitor.config.ts`: Biometric plugins do not require Capacitor config entries (auto-discovered via `npx cap sync`)

---

**Decision:** iOS vs Android platform behavior differences.

| Aspect | iOS | Android |
|--------|-----|---------|
| **Biometric Types** | FaceID (iPhone X+), TouchID (iPhone 5s+) | Fingerprint, Face, Iris (device-dependent) |
| **Usage Description** | Requires `NSFaceIDUsageDescription` (FaceID only) | None |
| **Permission** | None (capability check only) | `USE_BIOMETRIC` in AndroidManifest.xml |
| **Fallback UI** | Shows "Use Passcode" button in prompt | Shows "Use PIN" automatically if `allowDeviceCredential: true` |
| **Prompt UX** | System-modal FaceID animation or TouchID overlay | Bottom sheet with biometric icon |
| **Error Codes** | `LAError` codes (e.g., `biometryLockout`) | `BiometricPrompt` error codes (mapped by plugin) |
| **Storage** | iOS Keychain (`kSecAttrAccessibleWhenUnlockedThisDeviceOnly`) | EncryptedSharedPreferences (Keystore-backed AES256-GCM) |
| **Simulator** | `isAvailable: false` (no biometrics in simulator) | Emulator supports mock enrollment via ADB |
| **API Level** | iOS 11+ (TouchID), iOS 11+ (FaceID) | API 23+ (Keystore), API 28+ (BiometricPrompt) |

**Behavioral Notes:**
- **iOS Lockout:** 5 failed biometric attempts locks biometrics; requires passcode unlock. Plugin returns `biometryLockout` error.
- **Android API 23-27:** Plugin uses FingerprintManager compat layer (different UX than BiometricPrompt but functionally equivalent)

---

**Decision:** Testing strategy.

**iOS Testing:**
- Physical device required (biometrics unavailable in Simulator)
- Verify `NSFaceIDUsageDescription` in Info.plist
- Test FaceID/TouchID prompt appearance
- Test "Use Passcode" fallback button
- Verify Simulator shows "Biometrics not available" fallback

**Android Testing:**
- Physical device or emulator with enrolled biometric
- Emulator mock enrollment: `adb -e emu finger touch 1`
- Verify `USE_BIOMETRIC` permission in AndroidManifest.xml
- Test BiometricPrompt appearance (API 28+) and FingerprintManager compat (API 23-27)
- Test "Use PIN" fallback

**Cross-Platform:**
- `checkBiometry()` returns correct availability status
- Enrollment flow only triggers after successful OIDC callback
- Stored credentials survive app restart
- Biometric lockout (5 failed attempts) falls back gracefully
- Credential removal on logout clears stored credential

---

### Open Questions for Implementation

1. **Copper:** Should the Entra refresh_token encryption key be global (one Key Vault secret) or per-tenant? Recommendation: global key + per-record IV for v1.
2. **Blathers:** Token expiry duration (90-day default) may conflict with shorter Entra CA refresh token windows on some tenants. Needs validation before implementation.
3. **Blathers:** Confirm `/exchange` rate limiting strategy — suggest per-IP + per-token-attempt limits at the ASP.NET middleware level.

**Team Notes:**
- Kicks newly joined squad as Mobile Native Specialist (2026-03-28)
- Design document ready at `/Design/biometric-auth.md` (merged from all three team members)
- Next phase: Blathers implements C# backend changes; TypeScript implements WebView bridge + flows

---

## 📌 2026-07-14: BiometricToken is a Signed JWT — Consistency Fix (Tom Nook)

**Author:** Tom Nook (Lead Architect)  
**Status:** Accepted

`BiometricToken` is a **signed JWT** (Prism backend signing key), not a plain UUID v4. JWT payload: `deviceId` (client-generated UUID stored by the app on first launch), `tenantId`, `userOid`, `iat`, `exp`.

**Device binding via DeviceId claim:** On registration, `DeviceId` is stored in the `prismBiometricTokens` DB table alongside the token hash. On `/exchange`, the server validates that the `deviceId` claim in the presented JWT matches the registered `DeviceId` in the DB row. This closes the bearer theft vector.

**Token lifetime:** 30 days default, configurable per tenant (range: 7–90 days). The previous "90 days, non-configurable" value is removed.

**Audit logging promoted to v1:** Minimum exchange logging (attempt + outcome + token ID + IP) is a v1 requirement (~5 lines of code), not deferred to v2.

**Rate limiting hardened:** 3 failed exchange attempts within 10 minutes for a given token → token locked; requires re-registration. IP-based rate limiting as secondary layer. Replaces the unenforceable "5 requests/minute per device ID" policy.

## 📌 2026-07-14: Biometric Auth Issue Decomposition (Tom Nook)

**Session Log:** `.squad/log/2026-07-14T12:38:13Z-biometric-issues.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/tom-nook-biometric-issues-created.md`

### Tom Nook — Biometric Auth Issue Map (#12–#28)

**Decision:** `Design/biometric-auth.md` has been decomposed into 17 GitHub issues across 4 implementation phases. All issues are live in `jonnymuir/Umbraco.Prism` with `biometric-auth` and `squad:*` labels.

**Issue Map:**

| # | Title | Owner(s) | Phase |
|---|-------|----------|-------|
| #12 | prismBiometricTokens DB table + EF migration | Blathers | 1 — Backend Foundation |
| #13 | BiometricToken JWT signing + key management | Blathers | 1 — Backend Foundation |
| #14 | POST /register endpoint | Blathers | 1 — Backend Foundation |
| #15 | POST /exchange endpoint | Blathers | 1 — Backend Foundation |
| #16 | DELETE /unenrol + admin revocation | Blathers | 1 — Backend Foundation |
| #17 | Exchange audit logging | Blathers | 1 — Backend Foundation |
| #18 | Rate limiting on /exchange | Blathers | 1 — Backend Foundation |
| #19 | BiometricAuthEnabled flag + plugin deps in MobileBundleService | Blathers + Kicks | 2 — MobileBundleService |
| #20 | iOS entitlement injection (NSFaceIDUsageDescription) | Blathers + Kicks | 2 — MobileBundleService |
| #21 | Android manifest injection (USE_BIOMETRIC) | Blathers + Kicks | 2 — MobileBundleService |
| #22 | biometric-bridge.ts — registration flow | Isabelle + Kicks | 3 — Capacitor Client |
| #23 | biometric-bridge.ts — login/exchange flow + cookie injection | Isabelle + Kicks | 3 — Capacitor Client |
| #24 | biometric-bridge.ts — revocation flow + event | Isabelle + Kicks | 3 — Capacitor Client |
| #25 | Fallback to full Entra OIDC on failure | Isabelle + Kicks | 3 — Capacitor Client |
| #26 | Biometric enrollment change detection + credential wipe | Copper + Kicks | 4 — Security & Hardening |
| #27 | Multi-tenant keystore key pattern + server boundary validation | Copper | 4 — Security & Hardening |
| #28 | Penetration test checklist before v1 ship | Copper | 4 — Security & Hardening |

**Key Constraints:**
- Rolling refresh token rotation is v1 mandatory (#15)
- `/exchange` is unauthenticated by design — rate limiting is non-negotiable (#18)
- `biometricToken` must never appear in logs (#17)
- Cross-tenant deletion guard is explicit in #16 and #27
- `@capacitor/preferences` is explicitly forbidden in #19 and #22 (not hardware-backed)
- `squad:kicks` label created as part of this session (was absent from repo label set)

**Decomposition Rationale:**
- Phase 1 before Phase 2: Backend endpoints must exist before MobileBundleService generates bundles referencing them. DB migration (#12) and JWT signing (#13) are the two roots.
- Phase 2 before Phase 3: `BiometricAuthEnabled` flag (#19) controls whether `biometric-bridge.ts` is generated. iOS/Android platform entries (#20, #21) must be in bootstrap before bridge runs on device.
- Audit logging (#17) and rate limiting (#18) are Phase 1, not deferred — implemented alongside the exchange endpoint.
- #28 (pentest checklist) is a spike: closes only when Copper posts a signed-off comment. Blocking Phase 3 merge on #28 is recommended but not encoded in GitHub — note in sprint planning.

---

## 📌 2026-03-29: User Directive — Test Site Content Setup (Copilot)

**Session Log:** `.squad/log/2026-03-29T09:00:49Z-brewster-rework.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/copilot-directive-20260329-content-setup.md`

### User Directive

**Decision:** If the test site or demo requires content editors to manually create pages, navigation, block list entries, or any Umbraco content tree structure to get the demo working, we must: (1) make it as simple as possible — preferably seed/auto-create it; (2) document clearly what is expected and why, in plain language an Umbraco editor would understand.

**Why:** User request — captured for team memory. Affects Brewster's work on the test site and any future Prism package setup documentation.

---

## 📌 2026-03-29: Biometric Refresh Token Encryption (Blathers)

**Session Log:** `.squad/log/...` (pending)

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-biometric-refresh-token-encryption.md`

### Blathers — Biometric Refresh Token Encryption

**Decision:** Use AES-256-GCM for encrypting Entra refresh tokens at rest in `prismDeviceCredentials.RefreshTokenEnc`.

**Conventions:**
- Encryption key is a base64-encoded 32-byte value configured at `Prism:Biometric:EncryptionKey`.
- Wire format: `Base64([12-byte nonce][ciphertext][16-byte authentication tag])`.
- Each encryption produces a unique nonce via `RandomNumberGenerator.Fill`, ensuring identical plaintexts yield different ciphertexts.
- The key should be injected via environment variable or Azure Key Vault reference in production.
- `IRefreshTokenEncryptionService` is the abstraction; `RefreshTokenEncryptionService` is the singleton implementation registered in `PrismComposer`.

**Why:** The design spec requires refresh tokens to be encrypted at rest with AES-256. GCM mode provides authenticated encryption (tamper detection) without needing a separate HMAC. The base64 key format aligns with standard key generation patterns (`Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))`).

**Impact:** Any future endpoint that reads `RefreshTokenEnc` (e.g., the `/exchange` endpoint in Phase 2) must use the same `IRefreshTokenEncryptionService` to decrypt.

## 📌 2026-03-29: OIDC Signing Key Cold-Start Fix (Copilot + Copper + Tangy)

**Session Log:** `.squad/log/2026-03-29T13-53-oidc-signing-key-fix.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/copper-signing-key-review.md`
- `.squad/decisions/inbox/copper-token-warmup-review.md`
- `.squad/decisions/inbox/tangy-auth-test-coverage.md`

### Copilot — Synchronous Key Resolver Cold Start Unblocking

**Decision:** Replace fire-and-forget `WarmAsync` with synchronous blocking fetch in `PrismAuthExtensions.ResolveSigningKeys` when cache is empty or the requested key ID is absent.

**Implementation:**
- When cache is cold or `kid` missing: block on `WarmAsync(...).GetAwaiter().GetResult()`
- Re-read cache snapshot after fetch
- Return empty if key still absent (correct — don't return keys that can't validate the token)
- Background refresh unchanged for approaching-expiry case (ShouldRefresh)
- Guard: `ContainsRequestedKey` validation on return

**Why:** First requests to cold instances received 401 errors (IDX10500: Signature validation failed. No security keys were provided) due to fire-and-forget warmup completing after token validation.

**Addresses:** Bug fix for OIDC authorization failures on cold start.

### Copper — Security Review: Approved with Recommendations

**Verdict:** ✅ Approved — No blocking security issues.

**Security Findings:**

1. **Deadlock Risk:** Safe — .NET 10.0 has no SynchronizationContext; `WarmAsync` uses per-tenant semaphore with no nested locks.
2. **DoS Risk:** Bounded — Per-tenant cooldown (30s) and tenant allow-list prevent unbounded fetch amplification.
3. **Tenant Isolation:** Preserved — Cache keyed by tenant ID; allow-list checked before cache interaction; `GetSnapshot` uses normalized comparison.
4. **Exception Handling:** Exceptions from `WarmAsync` propagate correctly (fail-closed behavior). Test coverage gaps identified.

**Recommendations:**
1. Test exception propagation from `WarmAsync` during synchronous block.
2. Test cold-start concurrency with multiple `kid` values for same tenant.
3. Test case-insensitive tenant ID matching in key resolution.

### Tangy — Test Coverage: 3 New Tests, 168/168 Passing

**Implementation:** 3 new xUnit tests in `PrismAuthExtensionsSecurityTests.cs`

1. **Exception Propagation:** Validates that exceptions during synchronous fetch propagate correctly.
2. **Cold-Start Concurrency Deduplication:** Tests per-tenant `SemaphoreSlim` deduplication; only first waiter performs HTTP fetch.
3. **Case-Insensitive Tenant ID Matching:** Tests `OrdinalIgnoreCase` comparison in `Any(t => Equals(...))` and `ConcurrentDictionary` lookups.

**Architectural Notes:**
- Exception propagation is intentional — token validation must fail-loud when OIDC metadata is unreachable.
- Deduplication lives in `PrismSigningKeyCache.WarmAsync`, not in `ResolveSigningKeys`.
- Case-insensitive matching is end-to-end (tenant lookup + cache store).

**Test Results:** 168/168 passing (100%)

---

**Cross-Agent Notes:**
- Copper security review recommendations fully addressed by Tangy
- All tests passing; ready for merge
- Orchestration logs: `.squad/orchestration-log/2026-03-29T13-53Z-*.md`

## 📌 2026-06-18: Per-tenant AllowBiometricLogin Toggle (Brewster)

**Session Log:** `.squad/log/2026-06-18-biometric-tenant-toggle.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/brewster-biometric-toggle.md`

### Per-tenant `AllowBiometricLogin` Flag

**Decision:** Implement a per-tenant `AllowBiometricLogin` toggle (default `true`, backward compatible) to allow admins to disable biometric login at the tenant level.

**Implementation:**

1. **Database:** New `AllowBiometricLogin` boolean column in `prismTenants` table (default `TRUE`). Migration `AddAllowBiometricLoginColumn` is idempotent and registered as final step in `PrismMigrationPlan`.

2. **Domain Model:** Field added to `PrismTenantSchema` and propagated through `TenantService` to `PrismTenant` domain model, accessible via `IPrismContext.CurrentTenant.AllowBiometricLogin` at request time.

3. **Backoffice UI:** Toggle switch in the **General tab** of `prism-create-tenant-modal.ts`, below Hostname field. Uses custom CSS toggle. Payload field: `allowBiometricLogin` (camelCase).

4. **API Enforcement:** Both `BiometricController.Register` and `BiometricController.Exchange` check `tenant.AllowBiometricLogin` immediately after tenant null guard. If `false`, return `HTTP 403` with `{ error: "Biometric login is not enabled for this tenant." }`. Exchange action also emits audit log with `"biometric_disabled"` failure reason.

**Why:** Admins need granular control over tenant capabilities. Default `true` ensures backward compatibility; no existing tenants are affected.

**Status:** ✅ Implemented and tested. Dotnet and npm builds passing.

## 📌 2026-03-29: EditorUiAlias must be set on programmatically-created data types

**By:** Jonny (via Brewster)

**What:** In Umbraco v14+, when creating IDataType programmatically, set both EditorAlias (e.g. "Umbraco.MultiUrlPicker") AND EditorUiAlias (e.g. "Umb.PropertyEditorUi.MultiUrlPicker"). Missing EditorUiAlias causes backoffice to show "property editor UI is missing" error.

**Why:** User-reported bug. Umbraco v14+ split property editors into schema (backend) and UI (frontend Web Component) with separate aliases.

---


## 📌 2026-03-30: Remove btn-mobile-signin Pattern from Hero CTAs (Isabelle)

**Session Log:** `.squad/log/2026-03-29-biometric-flow-and-signin-dedup.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-signin-dedup.md`

### Isabelle — Remove btn-mobile-signin Pattern

**Context:**
The unauthenticated hero section contained a `btn-mobile-signin` anchor that duplicated the primary "Sign In" CTA. It was hidden in desktop mode (`display:none`) and revealed only under `html.prism-mobile`, creating two "Sign In" buttons in the mobile app body.

**Decision:**
**Do not use hidden-then-revealed buttons as a pattern for mobile-specific auth CTAs.** The primary `btn-primary` CTA already gets full-width grid layout in mobile mode — no replacement is needed. If a mobile-specific variant of an auth action is ever needed (e.g., biometric login shortcut), introduce it as a distinct named element with a unique label, not as a ghost-copy of the primary CTA.

**Changes:**
- Removed `btn-mobile-signin` anchor element from `HomePage.cshtml`
- Removed unused `mobileAuthHref` and `mobileAuthLabel` C# variables
- Removed CSS rules: `.btn-mobile-signin { display:none }` and `html.prism-mobile .btn-mobile-signin { display:inline-flex }`

**Why:** Silent duplication via hidden-then-revealed buttons is hard to spot in code review and creates confusing UX (two identical CTAs). Explicit named elements force clarity in both code and design.

**Status:** ✅ Implemented. Build clean.

---

## 📌 2026-07-14 (backdated to 2026-03-29): Biometric Client-Side Flow Implementation (Kicks)

**Session Log:** `.squad/log/2026-03-29-biometric-flow-and-signin-dedup.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/kicks-biometric-client-flow.md`

### Kicks — Biometric Client-Side Flow Implementation

**Problem:**
Jonny deployed the Prism mobile app to iPhone and could log in with Entra External ID, but:
- No biometric enrollment prompt appeared after first login
- On subsequent app opens, a full Entra login was required every time
- The backend `BiometricController` existed but the client-side flow was entirely missing

**Root Causes Identified:**

1. **`biometric-bridge.ts` bug:** `authenticate()` called `response.json()` on the `/exchange` response, but `BiometricController.Exchange()` returns `Ok()` (empty 200) + `Set-Cookie: PrismMemberCookie`. The JSON parse threw, making biometric authentication always fail silently.

2. **No startup biometric flow in `www/index.html`:** `MobileBundleService.BuildPlaceholderIndex()` generated a bootstrap that always navigated directly to the start URL without attempting biometric auth first.

3. **No enrollment trigger after Entra login:** Nothing prompted users to enable Face ID/Touch ID after their first successful Entra authentication.

4. **Missing CORS headers on `/exchange`:** The startup shell (`capacitor://localhost`) calling `/exchange` cross-origin would fail without `Access-Control-Allow-Origin` headers.

**Decisions Made:**

### D1: Exchange returns cookie, not sessionToken
`authenticate()` return type changed from `Promise<string>` to `Promise<void>`. The `PrismMemberCookie` is set server-side via `SignInAsync`; the client does not need to handle a token value. Added `credentials: 'include'` to the exchange fetch to ensure the Set-Cookie is accepted cross-origin.

### D2: Startup biometric flow via `Cap.nativePromise()`
Since `www/index.html` is vanilla JS (no ES module bundler), Capacitor plugins cannot be imported via npm. Instead, `window.Capacitor.nativePromise(pluginId, methodName, options)` is used to call native plugins directly. Plugin method names used:
- `BiometricAuthNative.checkBiometry` / `BiometricAuthNative.internalAuthenticate`
- `SecureStorage.internalGetItem` / `internalRemoveItem` / `internalSetItem`
  - Key prefix: `capacitor-storage_` (SecureStorage applies this internally)
  - Data is JSON-encoded: `JSON.stringify(value)` on write, `JSON.parse(data)` on read
- `Preferences.get` / `set` / `remove`

### D3: Enrollment banner injected via PrismBrandingMiddleware
When `isPrismMobileRequest && tenant.AllowBiometricLogin && user.IsAuthenticated`, `PrismBrandingMiddleware` injects a `<script id="prism-biometric-enroll">` into the `<head>` of the response HTML. This script:
- Checks for existing biometric registration (SecureStorage token key)
- Checks biometry availability (`BiometricAuthNative.checkBiometry`)
- Shows a bottom-sheet enrollment banner if enrollment is needed
- Handles the full registration flow: biometric confirm → POST `/register` → SecureStorage store → enrollment fingerprint save
- Gracefully handles cancellation and errors

### D4: CORS for Capacitor origins on `/exchange`
Added explicit CORS headers (`Access-Control-Allow-Origin`, `Access-Control-Allow-Credentials`) on the `/exchange` endpoint for `capacitor://localhost` (iOS) and `http://localhost` (Android). Added `[HttpOptions("exchange")]` preflight handler. This is scoped only to the exchange endpoint (unauthenticated by design) and only for known Capacitor origins.

**Files Changed:**
- `src/UmbracoPrism.Client/src/biometric-bridge.ts` — fix authenticate(), add credentials:include
- `src/UmbracoPrism.Core/Services/MobileBundleService.cs` — add tryBiometricSignIn() to www/index.html bootstrap
- `src/UmbracoPrism.Core/Middleware/PrismBrandingMiddleware.cs` — inject enrollment banner on authenticated mobile pages
- `src/UmbracoPrism.Core/Controllers/BiometricController.cs` — CORS for Capacitor origins on /exchange

**Key Technical Insights:**
- `PrismMemberCookie` is `SameSite=Lax` → Set-Cookie IS stored from cross-origin fetch (with `credentials: 'include'`), AND the cookie IS sent on subsequent top-level navigation
- `BiometricController.Exchange()` returns `Ok()` (empty 200) + `Set-Cookie`, no JSON body, no `sessionToken` — session established via cookie alone
- `@aparajita/capacitor-secure-storage` applies `capacitor-storage_` prefix internally; all data is JSON-encoded by the wrapper
- `@aparajita/capacitor-biometric-auth` plugin ID is `BiometricAuthNative`. Direct raw bridge call: `nativePromise('BiometricAuthNative', 'internalAuthenticate', {reason, allowDeviceCredential, iosFallbackTitle})`

**Known Constraints:**
- The enrollment banner is only injected by the server when the user is authenticated — i.e., it will appear on the first page load after a successful Entra login that creates a `PrismMemberCookie` session.
- Requires `biometricAuthEnabled: true` in the generated mobile bundle (`MobileBundleService`) for the startup flow. The enrollment banner is controlled solely by `tenant.AllowBiometricLogin`.
- `NSFaceIDUsageDescription` in `Info.plist` is handled by `bootstrap-ios.sh` (`plutil` injection). Developers must re-run bootstrap if regenerating the iOS project.

**Status:** ✅ Implemented. Build clean. Tested on iOS device by Jonny (enrollment flow works, Face ID prompts appear after Entra login).


---

## 📌 2026-06-16 (backdated to 2026-03-31): Biometric Token Lifecycle Hardening (Copper)

**Session Log:** `.squad/log/2026-03-31T12:09:44Z-biometric-lifecycle-v132-release.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/copper-biometric-token-lifecycle.md`

### Copper — Biometric Token Lifecycle Hardening

**Decision:** Harden biometric token lifecycle against stale-token and logout-revocation attacks.

**Context:** iOS Keychain persists across app deletion/reinstall, but localStorage does not. This asymmetry creates two security vulnerabilities:
1. Stale reinstall: Keychain token exists but no enrollment fingerprint in localStorage — attacker could trigger auto-login with a token from the previous user.
2. Missing logout revocation: Logout cleared the session cookie but left the Keychain token valid until expiry (90 days by default).

**Decisions Adopted:**

1. **Stale token detection via localStorage sentinel**
   - The enrollment fingerprint key (`prism_biometric_enrollment_state_{tenantHost}`) in localStorage is the authoritative fresh-install indicator.
   - Token-in-Keychain + no-fingerprint-in-localStorage = stale token from previous install.
   - Stale tokens are cleared from Keychain.

2. **Defence-in-depth: both auto-login and enroll scripts check independently**
   - `BuildBiometricAutoLoginScriptTag`: clears stale token and returns (shows login page)
   - `BuildBiometricEnrollScriptTag`: clears stale token and shows enrollment banner
   - Rationale: Both scripts run independently on different page types; both must be hardened.

3. **Logout must revoke biometric credentials client-side and server-side**
   - Client-side: Enroll script attaches capture-phase click listener; on logout navigation, clears Keychain token + localStorage fingerprint.
   - Server-side: Calls `DELETE /umbraco/prism/mobile/biometric/revoke` with `credentials: 'include'`.
   - Revocation is best-effort; navigation proceeds regardless of success/failure.

4. **New `DELETE /umbraco/prism/mobile/biometric/revoke` endpoint**
   - Route: `DELETE umbraco/prism/mobile/biometric/revoke?deviceId={optional}`
   - Requires `PrismMemberCookie` authentication (same as Register/Unenrol)
   - Scoped by `TenantId` + `UserId` from authenticated cookie (prevents cross-user revocation)
   - Optional `deviceId` param: revoke single device if provided, all devices if omitted (logout path)
   - Soft-delete (sets `RevokedAt` timestamp); preserves audit trail; idempotent

**Technical Rationale:**
- **Soft-delete over hard-delete:** Preserves audit trail; consistent with existing `Unenrol` pattern.
- **Event delegation for logout:** Uses capture-phase click listener + `e.target.closest(...)` for robustness; no hard dependency on specific element IDs.
- **Both scripts must check:** Even though auto-login runs on login pages (before Keychain is populated), and enroll runs on authenticated pages, the defence-in-depth pattern ensures no edge case bypasses the check.
- **localStorage is the source of truth:** Keychain state is not a reliable indicator of freshness on iOS (persists across app deletion), making localStorage the only reliable sentinel.

**Alternatives Rejected:**
- Server-side revocation list check in auto-login: Added network round-trip before biometric prompt; UX regression.
- Clearing Keychain on every startup if localStorage empty: This is what we do — defence-in-depth.
- Hard-delete credential on revoke: Soft-delete (`RevokedAt`) is correct for audit trail.

**Files Changed:**
- `src/UmbracoPrism.Core/Middleware/PrismBrandingMiddleware.cs`
  - `BuildBiometricAutoLoginScriptTag()`: stale token check; clear Keychain if fingerprint missing
  - `BuildBiometricEnrollScriptTag()`: stale token check; clear Keychain if fingerprint missing; logout listener
- `src/UmbracoPrism.Core/Controllers/BiometricController.cs`
  - New `[HttpDelete("revoke")]` endpoint

**Build Status:** ✅ Clean (0 errors, 0 warnings)

**Release:** v1.3.2

---

## 📌 2026-04-02: Isabelle — Frontend Directory Restructure + Mobile Boundary Guard

**Session Log:** `.squad/log/2026-04-01T23-33-13Z-src-restructure.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-src-restructure.md`

### Isabelle — Frontend Src Directory Restructure

**Decision:** Split `src/UmbracoPrism.Client/src/` flat component directory into:
- **`src/backoffice/`** — all Umbraco backoffice components + shared utilities (biometric-bridge, index.ts entry point, index.css)
- **`src/mobile/`** — `prism-mobile-nav.ts` and its Storybook story

Add an ESLint 9 flat config (`eslint.config.mjs`) with `no-restricted-imports` rule scoped to `src/mobile/**` to hard-error on any `@umbraco-cms/backoffice` import.

**Rationale:**
- **Architectural clarity:** The `mobile/` directory can never accidentally gain Umbraco dependencies
- **Deployment efficiency:** `prism-mobile-nav.js` is loaded on every member-facing page view and must remain lean
- **Safe refactoring:** `biometric-bridge.ts` is only consumed by backoffice biometric components (`prism-biometric-register`, `prism-biometric-settings`) — moves to `backoffice/` where it belongs
- **Build output stability:** Vite entry points updated; output filenames (`prism-dashboard.js`, `prism-mobile-nav.js`) unchanged — Razor partials load by these exact names
- **Storybook compatibility:** Existing glob `'../src/**/*.stories.@(ts|tsx)'` automatically covers nested subdirectories — no config change needed

**Files Moved:**
- 10 files → `src/backoffice/` (biometric-bridge, index.ts, index.css, prism-create-tenant-modal.ts/stories, prism-dashboard.ts/stories, prism-biometric-register.ts/stories, prism-biometric-settings.ts/stories)
- 2 files → `src/mobile/` (prism-mobile-nav.ts, prism-mobile-nav.stories.ts)

**Files Created/Updated:**
- `eslint.config.mjs` (new) — ESLint 9 flat config with `no-restricted-imports` boundary guard
- `vite.config.ts` — entry points updated to `src/backoffice/index.ts` and `src/mobile/prism-mobile-nav.ts`

**Validation:**
- Build clean: `tsc && vite build` → 0 errors
- Output sizes unchanged: `prism-dashboard.js` 49.73 kB, `prism-mobile-nav.js` 5.84 kB
- Relative imports between co-located files unaffected (same-directory moves preserve import paths)

**Key Learning:** When splitting a flat directory into subdirectories, if related files move to the same target directory, relative import paths do not need updating — files' relative positions to each other remain unchanged, so imports stay correct.


---

## Decision: DemoMobileNavSeeder Recovery and Pattern

**Date:** 2026-04-02  
**Author:** Brewster  
**Status:** Accepted

`DemoMobileNavSeeder.cs` was lost from main (committed to a feature branch after PR opened, never merged). Mobile nav was silently not rendering because `_MobileShellNav.cshtml` guards on `Model != null && Model.Any()`.

**Decision:** Keep `DemoMobileNavSeeder.cs` in `src/UmbracoPrism.TestSite/` as a permanent Development-only startup seeder. Auto-discovered via `.AddComposers()` — no manual registration.

**Pattern:**
- Demo seeders belong in the TestSite project root
- Implement `INotificationAsyncHandler<UmbracoApplicationStartedNotification>`
- Guard with `runtimeState.Level < RuntimeLevel.Run` and `env.IsDevelopment()`
- Must be idempotent (check before write)
- Log at Debug for skip cases, Information for success, Warning for failures
- Requires Settings content node (alias `settings`) to exist; skips silently on fresh DB

---

## Decision: Always HTML-encode JSON in HTML Attributes (Razor)

**Date:** 2026-04-02  
**Author:** Isabelle  
**Status:** Accepted

`_MobileShellNav.cshtml` passed a `System.Text.Json`-serialised JSON string directly into a double-quoted HTML attribute (`items="@itemsJson"`). `System.Text.Json` produces `"` delimiters which terminate the attribute early — the component received truncated JSON, `JSON.parse` threw, and the nav rendered silently empty.

**Decision:** When passing JSON from C# into a double-quoted HTML attribute in Razor views, always use `@Html.AttributeEncode()`:

```razor
<prism-mobile-nav items="@Html.AttributeEncode(itemsJson)" ...>
```

`AttributeEncode` replaces `"` → `&quot;`. Browsers decode `&quot;` → `"` before returning `getAttribute()`, so `JSON.parse` receives valid JSON. Single-quote attributes are unsafe if label text may contain single quotes.

---

## 📌 2026-04-02: Solo-Contributor Workflow — Skip PRs

**Date:** 2026-04-02  
**Author:** Jonny (via Copilot)  
**Status:** Accepted

For solo-contributor work on this repo, skip pull requests. Commit directly to main (or short-lived branches merged immediately without formal PR review). PRs are unnecessary overhead for a single-contributor workflow.

---

## 📌 2026-04-02: Mobile Nav Icon Mapping Convention (Brewster)

**Session Log:** `.squad/log/2026-04-02-mobile-nav-icons-and-styling.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/brewster-nav-icons.md`

### Brewster — Icon Mapping Convention for Mobile Nav

**Date:** 2025-07-18  
**Status:** Accepted

The `prism-mobile-nav` Lit component supports an `icon` property on each nav item, mapped to built-in SVG icons (`home`, `dashboard`, `account`, `settings`, `transactions`, `notifications`, `more`). The Razor partial `_MobileShellNav.cshtml` now populates this field using a **URL-first, label-fallback** convention.

**Implementation:** Local function `IconForLink` in the partial:

1. **URL matching takes priority** — checks lowercased, trailing-slash-trimmed href for known substrings.
2. **Label fallback** — if the URL yields no match, checks the lowercased nav item label.
3. **Null for unknowns** — items with no recognisable pattern receive `icon = null`, which is omitted from the serialised JSON. The component renders label-only gracefully.

**Icon → URL/label keyword mapping:**

| Icon           | URL keywords                          | Label keywords         |
|----------------|---------------------------------------|------------------------|
| `home`         | `""` or `"/"`                         | `home`                 |
| `dashboard`    | `dashboard`                           | `dashboard`            |
| `account`      | `account`, `profile`                  | `account`, `profile`   |
| `settings`     | `setting`                             | `setting`              |
| `transactions` | `transaction`, `payment`              | —                      |
| `notifications`| `notification`, `alert`              | —                      |
| `more`         | `help`, `support`, `more`             | —                      |

**Why:** 
- No CMS property changes needed — mapping is purely derived from existing URL and label data.
- Easily extended: add new `if` branches to `IconForLink` as new icon names are added to the component.
- Null-safe and gracefully degrading — no site breakage if a link doesn't match any rule.

---

## 📌 2026-04-02: Mobile Nav iOS White Style Defaults (Isabelle)

**Session Log:** `.squad/log/2026-04-02-mobile-nav-icons-and-styling.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-white-nav.md`

### Isabelle — prism-mobile-nav Defaults to Apple iOS White Style

**Date:** 2026-03-30  
**Status:** Accepted

Changed `prism-mobile-nav` default styling from dark glass (navy `rgba(15,23,42,0.94)`) to Apple iOS-inspired white frosted glass (`rgba(255,255,255,0.95)`).

**Rationale:** The white tab bar is the dominant pattern on iOS and matches the Umbraco Prism TestSite's light UI. Dark glass is still fully supported via CSS custom properties — just no longer the default.

**Changes:**

- **Component defaults** (`prism-mobile-nav.ts`): Updated all CSS `var()` fallback values to iOS palette. Active colour defaults to `#007aff` (iOS blue) rather than `#4f46e5` (indigo). Label weight dropped from 600 → 500 for iOS feel.
- **Storybook** (`prism-mobile-nav.stories.ts`): `mobileDecorator` background changed to `#f2f2f7` (iOS system background). `LightTheme` story renamed `DarkTheme` with dark glass overrides.
- **TestSite branding** (`prism-components.css`): Explicit white nav vars added to `prism-mobile-nav {}` block for documentation and tenant-override discoverability.

**Implications:** Tenants relying on the previous dark defaults will need to add explicit CSS variable overrides. This is a visual breaking change for existing deployments without custom branding.

---

## 📌 2026-04-02: Mobile Nav Icon Strategy — Interim URL Convention (Copilot)

**Session Log:** `.squad/log/2026-04-02-mobile-nav-icons-and-styling.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/copilot-mobile-nav-icon-approach.md`

### Copilot — Mobile Nav Icon Strategy Rationale

**Date:** 2026-04-02  
**Author:** Jonny Muir (via Copilot — autonomous decision)  
**Status:** Accepted

Icon mapping for `prism-mobile-nav` uses URL/label convention in `_MobileShellNav.cshtml` as a pragmatic first step. The proper Umbraco reference implementation should use a custom `MobileNavItem` Element Type (Block List property on the Settings doc type) with an explicit `icon` dropdown field — so backoffice editors can choose icons without relying on URL pattern inference.

**Why URL Convention Is Interim:**
- Umbraco's built-in `Link` type has no icon field. URL convention mapping is fragile for non-standard URLs.
- For a reference implementation, a custom Element Type is the correct pattern.
- The convention mapping is an acceptable intermediate state while the proper schema work is planned.

**Next Step:**
Create a `MobileNavItem` Element Type with `label`, `url`, `icon` (dropdown), `target` fields; change Settings doc type to use Block List; update partial + seeder + Master.cshtml accordingly.

---

## Decisions from Session 2026-04-03

The following decisions were created during the mobile nav media icons integration sprint and are now merged into the shared decisions file.

---

# Decision: Replace Multi URL Picker with Block List for Mobile Nav Icons

**Date:** 2025-07-17
**Author:** Brewster (Umbraco Platform Specialist)
**Status:** Implemented

## Context

`Settings.mobileNavLinks` used `Umbraco.MultiUrlPicker` → `IEnumerable<Link>`. The `Link` model has no icon field, so icons were resolved by URL pattern-matching in `_MobileShellNav.cshtml` — a fragile convention that breaks as soon as an editor uses a non-standard URL.

## Decision

Replace Multi URL Picker with a Block List backed by a new `MobileNavItem` element type. Editors can now pick icons directly from the Umbraco media library per nav item.

## Implementation

- **New element type:** `mobileNavItem` (`IsElement = true`) with `navLabel`, `navUrl`, `navIcon` (Media Picker), `openInNewTab` (Toggle).
- **New data types:** `Mobile Nav Icon Picker` (MediaPicker3, single) and `Mobile Nav Block List` (BlockList, max 4).
- **Schema setup:** `MobileNavSchemaSetup.cs` — idempotent startup handler, Development only.
- **Registration:** `TestSiteComposer.cs` wires up both `MobileNavSchemaSetup` and `DemoMobileNavSeeder` (previously unregistered — bug fixed as a side effect).
- **Partial:** `_MobileShellNav.cshtml` updated to `@model IEnumerable<BlockListItem>` reading block content properties.
- **Master layout:** reads `BlockListModel` instead of `IEnumerable<Link>`.

## Consequences

- Editors must re-enter nav items via the backoffice (old Multi URL Picker values are not migrated — the property is replaced).
- The URL-convention icon hack is removed permanently.
- `BlockListModel` implements `IEnumerable<BlockListItem>` so the partial call is type-compatible.
- Future nav items can include icons from SVG media items in the Umbraco library.

# Decision: Media URL icons in prism-mobile-nav

**Date:** 2025-07-14  
**Author:** Isabelle (Frontend Dev)

## Context

The `icon` field on `NavItem` previously only accepted named built-in keys (`home`, `account`, etc.). Umbraco editors now need to pick icons from the media library, which produces URLs.

## Decision

Distinguish icon types at runtime using a prefix check (`/`, `http`, `data:`). Named keys use the existing SVG path lookup; URLs render as `<img aria-hidden="true">` elements.

## Rationale

- Zero breaking changes — existing named icons unchanged
- No new dependencies
- `<img>` with `aria-hidden="true"` and empty `alt` is accessible (decorative icon, label from sibling `<span>`)
- Opacity transitions (0.6 inactive → 1 active → 0.85 hover) mirror named icon behaviour via `color` inheritance

## CSS approach

Added `.nav-icon--img` class. Named SVG icons use `currentColor` (inherits from `.nav-item` `color` transition). `<img>` elements can't use `currentColor`, so opacity is used instead. Editors should upload SVGs in a neutral colour for best results.

---

## 📌 2026-04-03: Release v1.4.0 (Mobile Nav Media Library Icons) (Mabel)

**Session Log:** `.squad/log/2026-04-03T09:11:01Z-release-v1.4.0.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/mabel-release-v1.4.0.md`

### Mabel — Release v1.4.0

**Date:** 2026-04-09  
**Agent:** Mabel (Technical Writer & Release Manager)  
**Status:** ✅ Complete

**Summary**

Cut release **v1.4.0** of Umbraco Prism, bumping from v1.3.2 to v1.4.0 (minor version).

**Rationale**

The mobile navigation feature now supports **configurable icons sourced from the Umbraco media library**, enabling backoffice control over nav item appearance without code changes. This is a user-facing new capability (not a breaking change), warranting a minor version bump per semantic versioning.

**Changes Included**

**Features**
- Mobile nav items now accept a `navIcon` media picker property
- Icons are seeded automatically into "Prism Navigation Icons" media folder with sample SVG files

**Bug Fixes & Improvements**
- Fixed demo widget UX (z-index stacking above mobile nav, auto-repositioning)
- Removed redundant "Simulate PrismMobile" checkbox from hero buttons
- Removed "Prism mobile mode active" banner (widget now indicates state)
- Fixed block list draft state in v14+ (added `expose` array)
- Fixed Settings node persistence in seeder
- Fixed media key persistence across seeder runs (icons reuse existing media)
- Corrected mobile nav property descriptions (`navLabel`, `navUrl` null issue)
- Updated block list label template to v17+ syntax (`{=navLabel}` instead of `{{navLabel}}`)
- Removed backwards-compatibility patching code (v17+ only library)

**Files Modified**
- `src/UmbracoPrism.Core/UmbracoPrism.Core.csproj` — version 1.3.2 → 1.4.0
- `src/UmbracoPrism.Client/package.json` — version 1.3.1 → 1.4.0
- `CHANGELOG.md` — added v1.4.0 section with organized feature/fix/improvement entries

**Commit & Tag**
- **Commit:** `4d6d193` — chore: release v1.4.0
- **Tag:** `v1.4.0` (light tag, not annotated)
- **Not pushed** — per release workflow, push is left to maintainer

**Changelog Pattern**

Organized release notes into three sections:
1. **New Features** — user-facing capabilities (media library icons)
2. **Bug Fixes & Improvements** — stability and correctness fixes with rationale
3. (Not included in v1.4.0: Upgrade Notes, which are reserved for breaking changes)

Each entry is written in plain English, present tense, active voice, explaining what changed and why it matters to developers.

**README Review**

Reviewed README.md for sections on mobile nav configuration. Confirmed no updates needed — mobile nav feature is discoverable via Umbraco backoffice (Settings node with media picker), not requiring explicit documentation in README. Existing "Produce Mobile" and "Mobile Runtime Behavior" sections remain current.

**Decisions Respected**

- Followed semantic versioning per .squad/skills/conventional-commits/SKILL.md
- Matched changelog style to previous releases (v1.2.0, v1.3.2)
- Maintained version sync across csproj and package.json (required for NuGet distribution and npm ecosystem)
- Left git push to maintainer (release workflow does not include push)

---

## 📌 2026-04-03: Azure Key Vault Auto-Wiring Architecture (Blathers)

**Session Log:** `.squad/log/2026-04-03T09:50:47Z-keyvault-refactor.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-keyvault-arch.md`

### Blathers — Key Vault Configuration Architecture Research

**Decision:** Adopt **Option A: WebApplicationBuilder Extension Method** for Azure Key Vault configuration wiring.

**Approach:**
- Implement explicit opt-in via `builder.AddPrismKeyVault()` in consumer's Program.cs
- Extension reads `Prism:VaultUri` from configuration
- If configured, calls `builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential())`
- If not configured, silently skips (supports local dev without vault)

**Why Option A over Alternatives:**
1. **Correct timing:** Runs before `CreateUmbracoBuilder()` when configuration is still mutable
2. **Explicit opt-in:** Clear security posture for multi-tenant package
3. **Consumer control:** Consumer places extension in Program.cs, understands Key Vault is enabled
4. **Works with Umbraco v17 startup model:** Compatible with composition pipeline
5. **Minimal friction:** Reduces 6 lines to 1 line for consumers

**Rejected Options:**
- **IStartupFilter:** Runs too late (after configuration is built)
- **IUmbracoBuilder extension:** Configuration frozen by that point
- **HostingStartup:** See Copper's security analysis (supply chain risk, implicit opt-out)
- **IOptions lazy-load:** Services need secrets at startup, not runtime

**Required NuGet Addition:**
- `Azure.Extensions.AspNetCore.Configuration.Secrets` v1.3.2 (provides `AddAzureKeyVault()` extension)

**Next Steps:** Implementation pending Copper's security review

---

## 📌 2026-04-03: Azure Key Vault Auto-Wiring Security Review (Copper)

**Session Log:** `.squad/log/2026-04-03T09:50:47Z-keyvault-refactor.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/copper-keyvault-security.md`

### Copper — Key Vault Wiring Security Analysis

**RECOMMENDATION: REJECT Option D (HostingStartup), ADOPT Option A (Extension Method)**

**HostingStartup Critical Risks:**
1. **Automatic execution:** Runs without consumer consent when package is referenced
2. **Implicit trust boundary:** Prism acquires credentials on behalf of consumer
3. **Supply chain risk:** Third-party package executes arbitrary code before Program.cs
4. **Configuration precedence ambiguity:** HostingStartup runs before Program.cs, shadowing consumer config overrides
5. **Opt-out model:** Implicit behavior violated security-critical package requirement for explicit control

**DefaultAzureCredential Assessment:**
- ✅ Acceptable for runtime secret retrieval (SecretVaultService usage)
- ❌ Not for automatic startup wiring (silent failure risk, credential sprawl)
- ⚠️ Requires URI validation to prevent SSRF

**Configuration Ordering Risk:**
- HostingStartup adds Key Vault before consumer's config sources
- Consumer environment variable overrides may be shadowed by vault values
- Explicit opt-in eliminates this ambiguity

**Opt-In vs. Opt-Out Principle:**
- Prism is security-critical, multi-tenant package
- Automatic credential behavior fails enterprise security audits
- Explicit `builder.AddPrismKeyVault()` provides clear intent and auditability

**Recommended Implementation (Option A with Hardening):**

```csharp
public static WebApplicationBuilder AddPrismKeyVault(this WebApplicationBuilder builder)
{
    var vaultUri = builder.Configuration["Prism:VaultUri"];
    
    if (string.IsNullOrWhiteSpace(vaultUri))
        return builder; // No vault configured, skip silently
    
    // SECURITY: Validate vault URI to prevent SSRF
    if (!Uri.TryCreate(vaultUri, UriKind.Absolute, out var uri) ||
        uri.Scheme != "https")
    {
        throw new InvalidOperationException(
            $"Prism: VaultUri must be a valid HTTPS URI. Got: {vaultUri}");
    }
    
    builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());
    return builder;
}
```

**Required Security Gates Before Merge:**
1. ✅ URI validation enforces HTTPS scheme (SSRF prevention)
2. ✅ Extension method is public and documented
3. ✅ Consumer test site updated to use `builder.AddPrismKeyVault()`
4. ✅ README documents usage, permissions, and secret naming
5. ⏳ Follow-up task: Fail-fast secret validation at startup
6. ⏳ Security test: URI validation with malformed/non-HTTPS inputs

**Conventions for Follow-Up Tasks:**
- Missing required secrets should produce explicit `InvalidOperationException` at startup
- Error message should identify which secret and which vault
- Support graceful degradation for non-biometric workloads

---

## 📌 2026-04-03: Azure Key Vault Extension Implementation (Blathers)

**Session Log:** `.squad/log/2026-04-03T09:50:47Z-keyvault-refactor.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-keyvault-impl.md`

### Blathers — AddPrismKeyVault() Implementation Details

**Implementation Status:** ✅ Complete

**Decisions Made:**

1. **Error Handling:** Skip silently when `Prism:VaultUri` is null/whitespace, throw `InvalidOperationException` when configured with invalid URI
2. **Extension Return Type:** Return `WebApplicationBuilder` (fluent interface, matches ASP.NET Core conventions)
3. **NuGet Version:** Use `Azure.Extensions.AspNetCore.Configuration.Secrets` v1.3.2 (stable, matches TestSite)
4. **URI Validation:** Validate HTTPS scheme only (not hostname pattern)
   - Prevents SSRF attacks (Copper's requirement)
   - Allows Azure sovereign clouds without region-specific patterns
   - Azure SDK validates actual endpoint accessibility

**Files Modified:**
- `src/UmbracoPrism.Core/Extensions/PrismKeyVaultExtensions.cs` (34 lines, new)
- `src/UmbracoPrism.Core/UmbracoPrism.Core.csproj` (NuGet reference added)
- `src/UmbracoPrism.TestSite/Program.cs` (9 lines → 5 lines, refactored)

**Implementation Details:**

```csharp
public static WebApplicationBuilder AddPrismKeyVault(this WebApplicationBuilder builder)
{
    var vaultUri = builder.Configuration["Prism:VaultUri"];
    
    if (string.IsNullOrWhiteSpace(vaultUri))
        return builder;
    
    if (!Uri.TryCreate(vaultUri, UriKind.Absolute, out var uri) || 
        uri.Scheme != Uri.UriSchemeHttps)
    {
        throw new InvalidOperationException(
            $"Prism: VaultUri '{vaultUri}' must be a valid HTTPS URI...");
    }
    
    builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());
    return builder;
}
```

**Verification Results:**
- ✅ Build: green
- ✅ Tests: 168 passing
- ✅ TestSite Program.cs: runs locally (no vault) and in Azure (with vault)
- ✅ Consumer integration: downstream services can call extension

**Consequences:**
- Consumers reduce boilerplate from 9 lines to 1 line
- Security validation (HTTPS-only) enforced consistently
- Local dev supported (silent skip if no vault configured)
- Fail-fast on misconfiguration (exception on startup if URI is invalid)

**Commit:** SHA `63b603e` — "refactor: move Key Vault wiring into AddPrismKeyVault() extension"

---

## 📌 2026-04-03: Biometric Security Key Setup Documentation (Mabel)

**Session Log:** `.squad/log/2026-04-03T09:50:47Z-keyvault-refactor.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/mabel-biometric-docs.md`

### Mabel — Biometric Authentication Key Setup Documentation

**Decision:** Create comprehensive developer-facing documentation for biometric authentication key generation, storage, and verification.

**Context:**
Biometric authentication in Umbraco.Prism requires two cryptographic keys:
1. **SigningKey** — HMAC-SHA256 key for signing BiometricToken JWTs (32+ characters)
2. **EncryptionKey** — Base64-encoded 32-byte AES-256-GCM key for encrypting refresh tokens

Both required at startup; missing keys throw `InvalidOperationException` with clear messages. Developers previously lacked step-by-step guidance.

**Deliverables:**

**New:** `docs/biometric-setup.md` — Comprehensive guide covering:
- Key purposes and requirements (SigningKey vs. EncryptionKey)
- Prerequisites (tenant config, Key Vault access)
- Local development (5 steps: generate key, store in User Secrets, verify)
- Production deployment (6 steps: vault config, secret creation, managed identity, testing)
- Security best practices (rotation, source control, audit logging)
- Troubleshooting (6 common error scenarios with solutions)

**Updated:** `README.md` — Configuration Options section
- Added cross-reference: `→ **Full guide:** See [docs/biometric-setup.md]() for step-by-step instructions`
- Follows established pattern for deeper documentation walkthroughs

**Writing Conventions Established:**

1. **Multi-platform key generation:** Provide OpenSSL/PowerShell/bash/password manager alternatives
2. **Platform-specific paths:** Show both Unix (`~/.microsoft/usersecrets`) and Windows (`%APPDATA%`) paths
3. **Error message documentation:** Map startup exceptions directly to source code with exact exception text
4. **Cross-reference pattern:** Use `→ **Full guide:** See [path]()` when README points to deeper /docs/ walkthroughs

**Technical Grounding:**
- Validated against BiometricTokenService.cs (SigningKey lines 36–39)
- Validated against RefreshTokenEncryptionService.cs (EncryptionKey lines 26–47)
- Key Vault naming convention: `Prism--Biometric--SigningKey` (from TestSite Program.cs)
- User Secrets paths: .NET 6.0+ documentation standards

**Impact:**
- Developer onboarding: clone → running app with biometric keys in <5 minutes
- Security operationalization: Copper's security model now actionable
- Reduced support burden: comprehensive troubleshooting section preempts common questions
- Documentation completeness: biometric feature fully documented end-to-end

**Optional Follow-Up:**
- Automation script (`scripts/setup-biometric-keys.sh` or `.ps1`) for one-time setup (non-blocking)

## 📌 2026-04-03: v1.5.0 Release — Zero-Config Key Vault Integration (Blathers + Copper + Tangy + Mabel)

**Session Log:** `.squad/log/2026-04-03-v150-release.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-iconfigureoptions-approach.md`
- `.squad/decisions/inbox/blathers-keyvault-errmsgs.md`
- `.squad/decisions/inbox/blathers-version-bump.md`
- `.squad/decisions/inbox/copper-health-security-review.md`
- `.squad/decisions/inbox/tangy-keyvault-review.md`
- `.squad/decisions/inbox/mabel-community-files.md`
- `.squad/decisions/inbox/mabel-docs-update.md`

### Blathers — IConfigureOptions for Azure Key Vault Integration

**Decision:** Adopt `IConfigureOptions<PrismBiometricOptions>` for Azure Key Vault integration, replacing the consumer-facing `builder.AddPrismKeyVault()` extension call requirement.

**Convention:**
- **PrismKeyVaultConfigureOptions** implements `IConfigureOptions<PrismBiometricOptions>` and is registered in `PrismComposer` via `ConfigureOptions<>()`
- Runs at options-resolution time (lazy), not at IConfigurationBuilder time (eager)
- If `Prism:VaultUri` is null/empty → silent skip (local dev, no vault)
- If `Prism:VaultUri` is set but not HTTPS → throw `InvalidOperationException` (fail-fast)
- Fetches `Prism--Biometric--SigningKey` and `Prism--Biometric--EncryptionKey` directly from Key Vault using `SecretClient`
- Azure SDK retry policy explicitly configured: 3 retries, exponential backoff, 0.8s base delay, 8s max delay
- On `RequestFailedException` with 404/403 status → throw `InvalidOperationException` with config-error message (no retry)
- On other exceptions → throw `InvalidOperationException` with "temporarily unavailable" message (SDK already retried)

**Rationale:**
- `IConfigurationBuilder.AddAzureKeyVault()` eagerly fetches **all** secrets at startup, blocking app boot on Key Vault availability
- `IConfigureOptions` is lazy — only fetches secrets when `IOptions<PrismBiometricOptions>` is first resolved (typically first auth request)
- Allows test sites and local dev to skip Key Vault entirely by omitting `Prism:VaultUri`
- Reduces package consumer friction: no explicit Program.cs call required

**Health Check:**
- **PrismKeyVaultHealthCheck** registered in `PrismComposer` with tag `"prism"`
- Caches result for 30 seconds (lock-protected) to prevent DoS amplification
- Returns `Healthy("Key Vault not configured")` when VaultUri is null/empty
- Returns `Healthy()` when secrets fetched successfully
- Returns `Degraded()` on failure — NEVER exposes secret names, vault URI, or error details in response body
- Exception details logged to `ILogger` at Warning level only

**Files Affected:**
- `src/UmbracoPrism.Core/Configuration/PrismKeyVaultConfigureOptions.cs` (new)
- `src/UmbracoPrism.Core/HealthChecks/PrismKeyVaultHealthCheck.cs` (new)
- `src/UmbracoPrism.Core/PrismComposer.cs` (ConfigureOptions + health check registration)
- `src/UmbracoPrism.TestSite/Program.cs` (removed `builder.AddPrismKeyVault()` call)
- `src/UmbracoPrism.Core/Extensions/PrismKeyVaultExtensions.cs` (unchanged; remains as optional)

### Blathers — KeyVault Error Message Improvements

**Context:** `PrismKeyVaultConfigureOptions.Configure()` had four quality issues:
1. HTTP 401 fell through to the generic "transient" catch, giving a misleading message.
2. 403/404 message named internal vault secret names, a minor info-leak in logs.
3. Secret name strings were magic literals duplicated in two `GetSecret()` calls.
4. Non-atomic assignment: `options.SigningKey` could be set while `options.EncryptionKey` remained null if the second fetch threw.

**Decisions Made:**
- **401 = configuration error, not transient** — wrong/missing Managed Identity or wrong tenant treated as non-retryable `InvalidOperationException`
- **No secret key names in error messages** — reference "required Prism biometric secrets" or config section instead
- **Secret names extracted to constants** — `SigningKeySecretName` and `EncryptionKeySecretName` for single source of truth
- **Atomic options assignment** — both secrets fetched to local variables before either is written to options

**What was NOT changed:**
- Fail-late design (no IHostedService warm-up — intentionally rejected)
- Retry policy (3× exponential, 0.8–8 s)
- HTTPS validation
- `AddPrismKeyVault()` extension method

**Build Status:** ✅ Passed; 168/168 tests passed

### Blathers — Version Bump from 1.4.0 to 1.5.0

**Rationale:** Release includes meaningful feature additions warranting a **minor version bump**:
1. **Zero-config Azure Key Vault Integration** via `IConfigureOptions<PrismBiometricOptions>`
2. **Improved Key Vault Error Handling** with distinct 401/403/404/transient distinction
3. **Documentation & Community** (CONTRIBUTING.md, FUNDING.yml)
4. **Backwards Compatibility** — `AddPrismKeyVault()` retained as optional explicit opt-in

**Files Updated:**
- `src/UmbracoPrism.Core/UmbracoPrism.Core.csproj` (1.4.0 → 1.5.0)
- `package.json` (1.4.0 → 1.5.0)
- `umbraco-marketplace.json` (1.4.0 → 1.5.0)
- `CHANGELOG.md` (v1.5.0 section with comprehensive release notes)

### Copper — Security Review: IConfigureOptions + /health Endpoint

**Verdict:** ✅ **APPROVED WITH CONSTRAINTS**

**Threat Model Coverage:**
1. **Credential Exposure** (LOW) — DefaultAzureCredential instantiation location carries no additional risk; no credential chain details in error messages
2. **Fail-Late Implications** (MEDIUM → LOW) — Biometric auth is optional; OIDC fallback remains; post-deployment smoke test bridges gap
3. **Retry Amplification** (MINIMAL) — IOptions singleton caches result for app lifetime; SecretClient.GetSecret() called once per resolution
4. **Secrets in Memory** (ACCEPTED) — Identical risk to previous `builder.Configuration.AddAzureKeyVault()` pattern
5. **Dependency Chain** (LOW) — Path 1 (IConfigurationBuilder) and Path 2 (IConfigureOptions) are independent; no conflicts if both used

**Health Check Constraints (Implemented by Blathers):**
- Response body MUST use generic failure reasons only (no secret names, vault URIs, or stack traces)
- MUST cache result for minimum 30 seconds (recommend 60 seconds for production)
- MUST be registered with `tags: ["prism"]` for consumer filtering
- MUST NOT implement endpoint auth in package (consumer's choice via middleware/access control)

**Documentation Constraints (Implemented by Mabel):**
- MUST document endpoint access control options (internal-only endpoint pattern recommended)
- MUST warn that `/health` should NOT be publicly accessible without rate limiting
- MUST include example of tag-based filtered endpoints
- MUST document post-deployment smoke test recommendation
- MUST document secrets remain in memory for app lifetime (recommend process-level isolation for high-security scenarios)

**Risk Assessment:**
- Change 1 (IConfigureOptions): LOW risk with constraints
- Change 2 (Health Check): MEDIUM → LOW risk with caching and access control guidance
- **Overall:** ✅ PASS

### Tangy — Code Review: PrismKeyVaultConfigureOptions

**Verdict:** ⚠️ FINDINGS — 2 blockers identified

**Blocker 1: IHostedService Warm-Up** — REJECTED BY DESIGN
- **Finding:** Fail-late approach questioned; IHostedService warm-up suggested for early validation
- **Response:** Jonny explicitly rejected warm-up pattern; fail-late is intentional design choice
- **Resolution:** No action required; documented as intentional

**Blocker 2: 401 Error Message Handling** — ACCEPTED AS FIX
- **Finding:** 401 responses fell through to generic "transient" message
- **Status:** Fixed; 401 now correctly identified as configuration error
- **Resolution:** Approved and merged

**Test Status:** ✅ 168/168 passed

### Mabel — Community Health Files for Umbraco.Prism

**Context:** Jonny asked if Umbraco.Prism should add `CONTRIBUTING.md` and `FUNDING.yml` to signal professional maturity.

**Existing Maturity Signals:**
- 4 versioned releases (v1.2.2–v1.4.0)
- Detailed CHANGELOG with semantic versioning
- GitHub Actions CI/CD and squad automation
- Marketplace listing (Umbraco)
- Professional README with architecture, mobile feature docs, examples
- MIT license
- Squad AI team infrastructure

**Decision:** ✅ **YES — add both CONTRIBUTING.md and FUNDING.yml**

**CONTRIBUTING.md (Root):**
- Clarifies expectations for bug reports, PRs, code standards
- Flags biometric/security code as requiring extra scrutiny
- Directs security issues to private channels
- Acknowledges solo maintainer reality while respecting squad team structure
- Professional tone: direct, useful, no clichés

**FUNDING.yml (.github/):**
- Signals confidence and sustainability
- GitHub Sponsors link (even without active funding goal) is a legitimacy signal
- Appropriate for versioned, marketplace-distributed packages with enterprise scope
- Low overhead; no management burden upfront

**Files Created:**
- `CONTRIBUTING.md` ✅
- `.github/FUNDING.yml` ✅

### Mabel — Key Vault Documentation Update (Zero-Consumer-Code Approach)

**Decision:** Update Key Vault integration documentation to reflect new zero-consumer-code setup and fail-late default behavior.

**docs/biometric-setup.md Changes:**
- `Prism:VaultUri` in appsettings.json is now the primary (and only required) configuration step
- No Program.cs changes needed for zero-config setup
- `builder.AddPrismKeyVault()` documented as optional for fail-fast startup validation
- Clear explanation of fail-late behavior: "Key Vault config errors will surface on the first biometric login"
- Recommendation for smoke testing after production deployment
- New section detailing error codes (401, 403, 404, transient) and what each means

**docs/umbraco-setup.md Changes:**
- Clarified that only `builder.Services.AddPrism()` is required
- `builder.AddPrismKeyVault()` is optional and only needed for fail-fast behavior
- Provided two code examples: minimal (no Key Vault) and with optional fail-fast
- Updated Next Steps to remove implication that `AddPrismKeyVault()` is required

**Rationale:**
- Implementation now supports automatic Key Vault integration via `PrismKeyVaultConfigureOptions`
- Zero consumer code: if `Prism:VaultUri` is in appsettings.json, Key Vault loads automatically
- Fail-late default more graceful for development/staging
- Optional fail-fast bridge for teams needing startup validation

**Added Security Considerations Section:**
- Per Copper's constraints documentation
- Endpoint access control options (internal-only endpoint pattern recommended)
- Rate limiting guidance for public `/health` exposure
- Post-deployment smoke test recommendation

---

## Impact Summary

**What Changed for Consumers:**
- ✅ **Simpler on-boarding:** Add `Prism:VaultUri` to appsettings; no Program.cs changes needed
- ✅ **Better error messages:** Distinct 401/403/404/transient guidance
- ✅ **Optional backward compatibility:** `AddPrismKeyVault()` still available for explicit control
- ✅ **Better documentation:** Clear fail-late vs. fail-fast trade-offs

**What Shipped (Non-Breaking):**
- `PrismKeyVaultConfigureOptions` (automatic, no code change needed)
- `PrismKeyVaultHealthCheck` (available via `/health` with tag filtering)
- CONTRIBUTING.md and FUNDING.yml (governance signals)
- Improved docs (setup guides, error reference, security considerations)

**Test Results:** ✅ 168/168 tests passed  
**Build:** ✅ Success  
**Security Review:** ✅ Approved with constraints implemented

