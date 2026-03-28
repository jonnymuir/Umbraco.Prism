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
