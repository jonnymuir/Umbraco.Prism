# Tom Nook — History

## Project Context

**Project:** Umbraco.Prism — Multi-tenancy package for Umbraco v17+
- Dynamic branding with CSS variable overrides
- Stateless OIDC identity (tenant-specific ClientId/Authority per request)
- Produce Mobile feature: Download native-shell Capacitor app starters with tenant settings
- **Stack:** .NET 10.0.x, Node.js 22.17.1, Web Components, Playwright, XUnit tests

**Key Skills on Team:**
- Isabelle: Web Components, Storybook, Playwright UI tests
- Blathers: C# backend, services architecture, databases, auth
- Tangy: Testing methodology, edge cases, test coverage
- Scribe: Session logging, decisions, team memory

## Architecture Established

- **Middleware:** PrismTenantMiddleware (tenant resolution) → PrismBrandingMiddleware (CSS injection)
- **Identity:** Stateless OIDC via PrismOidcConfiguration (swapped per request)
- **Services:** TenantService, BrandingService, MobileBundleService, SecretVaultService
- **Database:** PrismMigrationPlan handles schema evolution; no local Members (stateless auth)
- **Mobile:** Capacitor bundle generation from Backoffice settings; safe-area support for notched devices

## Key Patterns

1. **Naming:** `IPrismXxx` (interfaces), `XxxService` (services), `PrismXxxMiddleware` (middleware), `PrismXxx` (models)
2. **Drop-box pattern:** Agents write decisions to `.squad/decisions/inbox/{name}-{slug}.md` → Scribe merges to canonical
3. **Parallelism:** Spawn all independent agents as background mode in one turn; no serialization on shared files
4. **Eager downstream:** Anticipate testing, docs, scaffolding work; spawn while implementation runs

## Learnings

### Stateless OIDC Architecture (2026-03-22)
**What works:** Per-request tenant resolution + dynamic OIDC config is elegant:
1. PrismTenantMiddleware resolves hostname → fetches PrismTenant from cache (30 min TTL)
2. IPrismContext (scoped per request) holds the tenant
3. PrismOidcConfiguration.PostConfigure hooks token validation to use tenant's EntraTenantId/ClientId dynamically
4. No per-tenant authentication state; every request is fully self-contained.

**Design trade-off:** Burst of Azure calls on cache miss (30-min window hits DB + may trigger OIDC metadata fetch).
**Risk:** Token refresh in PrismContext uses blocking `.GetAwaiter().GetResult()`; no circuit breaker if CIAM endpoint is degraded.

### Multi-tenancy via Shared Schema (2026-03-22)
**Database model:** Single `prismTenants` table with tenant metadata + JSON blobs (BrandingOverrides, MobileBrandingOverrides, MobileAppConfig).
**Pros:** No schema sprawl; easy to add tenants dynamically; secrets stored in Azure Key Vault by name reference.
**Cons:** Scales to ~1K tenants without issue, but no advanced partitioning for 10K+ (would need read replicas).
**Cache strategy:** Runtime cache (30 min tenant, 1 hour secrets, 10 min branding tabs); no pre-warming or lease renewal.

### Branding Injection Pattern (2026-03-22)
**Flow:** PrismBrandingMiddleware buffers HTML response → injects CSS overrides + mobile shell guards into `<head>`.
**Smart details:**
- Scans CSS files in app root on boot; parses CSS variables (regex-based)
- Merges tenant overrides with detected defaults
- Supports both web (`--var`) and mobile (`prism-mobile` media) variants
- Graceful degradation: no overrides = no injection; silently skips non-HTML responses

**Concern:** CSS file scan happens on first BrandingService call; slow on monolith apps. Needs lazy/explicit registration.

### Mobile Bundle Generation (2026-03-22)
**What it does:** MobileBundleService generates ZIP with:
- Capacitor config (bundled with tenant ENVvars)
- Package.json + bootstrap scripts
- Safe-area CSS for notched devices
- Placeholder index.html with error UI

**Design quality:** Excellent separation of concerns; generates valid JS configs; validates app ID format.
**Risk:** Accepts arbitrary URLs (StartUrl, IconUrl, SplashUrl) without SSRF guards; no bundle size limits; no rate limiting on endpoint.

### Test Coverage Commentary (2026-03-22)
**Good:** Unit tests for middleware, context, services; Playwright ITS for UI components; FluentAssertions for readability.
**Gaps:**
- No full OAuth flow test (redirect → token exchange → cookie set)
- No token refresh failure scenarios
- No mobile bundle edge cases (special chars in app name, concurrent generation)
- No OIDC key rotation test (forces 401; should retry fresh metadata)

### Authorization Inconsistency (2026-03-22)
**Current model:**
- User isolation: `PrismTenantHandler` checks `user.EntraTenantId == currentTenant.EntraTenantId`
- Admin gate: `PrismAdminHandler` checks Umbraco *local* group membership (not Entra groups)

**Issue:** Admin users may be synced from Entra, but policy checks local Umbraco groups. Potential for permission drift.
**Recommendation:** Standardize on Entra groups for consistency.

### Ralph Issue Triage Learnings (2026-03-22)
- Issues #2-#7 were all architecture-driven and had overlapping squad owner labels, which diluted ownership clarity.
- Keeping one primary `squad:*` owner per issue removed ambiguity while preserving domain labels (`architecture`, `security`, `performance`, `testing`).
- Cross-cutting policy work (Issue #4) belongs with lead ownership first, then can split into implementation follow-ups.
- Reliability test expansion (Issue #7) should be treated as a test-plan parent that is expected to split into scenario-focused child issues.
- Label hygiene rule to keep: triage inbox label `squad` can remain, but only one primary `squad:*` label should exist per issue.

### Auth Model Kickoff Learnings (2026-03-22)
- Enforcement is currently mixed: `PrismTenantHandler` uses Entra tenant claim (`tid`), while `PrismAdminHandler` uses Umbraco local backoffice group aliases.
- `PrismAdmins` is only applied on `TenantManagementController`, layered on top of Umbraco backoffice access policy; this creates a split trust model for Prism-specific authorization.
- First safe slice should be compatibility-first: Entra claim evaluation for admins with optional Umbraco fallback, warning logs on fallback use, and explicit config validation before strict mode.
- Immediate test gap: there are no dedicated auth policy handler tests in `UmbracoPrism.Core.Tests` for admin claim/group permutations or tenant mismatch behavior.
- Rollout should ship in phases: compatibility mode first, strict Entra mode second, legacy group fallback removal last.

### Auth Model Decomposition (Issue #4, 2026-03-22)
- Deep-read of `PrismAdminHandler`, `PrismTenantHandler`, `PrismAdminOptions`, `PrismComposer`, `TenantManagementController`, and `PrismAuthExtensions` confirmed the split trust root.
- `PrismTenantHandler` is already claim-native — no changes needed. Only `PrismAdminHandler` migrates.
- `PrismAdminOptions` currently only has `GroupAliases`; new options shape needs `EntraAdminClaimType`, `EntraAdminClaimValues`, `StrictEntraMode`, `EnableUmbracoGroupFallback`.
- Three child issues created: #8 (compatibility implementation, squad:tom nook), #9 (test suite, squad:blathers), #10 (fallback removal, squad:tom nook).
- Hard gate pattern: #10 (removal) cannot merge before #8 is deployed + #9 CI-green + one release cycle of zero fallback log fires. Write the gate into the issue body itself — do not rely on process memory.
- Using `--body-file` for `gh issue create` is far more reliable than inline `$'...'` body strings for multi-line content. Preferred approach going forward.
- Deprecation signal in `PrismComposer` startup (warn when old config exists but new config absent) gives operators advance notice before the breaking removal in #4.3 — this pattern is worth repeating for any config migration.

### Team Expansion Routing Note (2026-03-22)
- Celeste (Documentation Engineer) and Copper (Security Engineer) joined the squad.
- Route to Celeste for XML documentation coverage, public API clarity, and documentation standards alignment.
- Route to Copper for tenant-isolation hardening, OAuth/cache-boundary security review, and CIA-focused risk reduction.

## Learnings

- 2026-03-28: Team now uses conventional commits. Read .squad/skills/conventional-commits/SKILL.md before every commit. Breaking changes must be flagged with ! or BREAKING CHANGE: footer and discussed with Tom Nook first.

### Biometric Auth Architecture (2026-07-14)

**What was designed:** End-to-end biometric login feature for Prism Mobile (Capacitor WebView wrapper).

**Key architectural decisions made:**

1. **Never store raw Entra tokens on device.** An opaque Prism-issued `BiometricToken` (UUID) lives in the device Keychain/Keystore. The Entra refresh_token is stored encrypted server-side only.

2. **Exchange endpoint is the auth root.** `POST /umbraco/prism/mobile/biometric/exchange` accepts the BiometricToken, does the Entra token refresh server-side, and returns a `PrismMemberCookie`. No Entra token ever touches the WebView JS layer.

3. **Cookie injection from native layer, not WebView JS.** The `Set-Cookie` header from `/exchange` is read by the Capacitor native layer and injected via `WKHTTPCookieStore` (iOS) / `CookieManager` (Android) before WebView navigation. This avoids CORS restrictions on `Set-Cookie` and keeps tokens away from WebView JS.

4. **Rolling refresh token rotation is v1 hard requirement.** Not deferrable on security grounds.

5. **Biometric enrollment change = automatic credential wipe.** If the fingerprint set or Face ID changes, the stored credential is cleared before it can be misused.

6. **`BiometricAuthEnabled` is opt-in in `MobileBundleService`.** Existing bundles are unaffected. New bundles include a generated `biometric-bridge.ts` and updated `package.json` deps.

7. **`/exchange` is unauthenticated by design** (biometric token IS the credential). Rate limiting on this endpoint is non-optional — flagged as a required implementation constraint.

**Risk flagged:** Token expiry duration (90-day default) may conflict with shorter Entra CA refresh token windows on some tenants. Needs security sign-off before implementation.

**Open question for Copper:** Should the refresh token encryption key be global (single Key Vault secret) or per-tenant? Recommendation is global key + per-record IV for v1.

### Biometric Auth Design Hardening (2026-07-14)

**Copper's security review resolved the UUID-vs-JWT inconsistency.** BiometricToken is now definitively a signed JWT (not a plain UUID v4). The UUID concept survives only as the `deviceId` claim value inside the JWT — a client-generated identifier stored by the app on first install.

**DeviceId binding closes the bearer theft vector.** The `DeviceId` is stored in `prismBiometricTokens` alongside the token hash on registration. On every exchange, the server asserts that the `deviceId` claim in the presented JWT matches the stored value. A stolen JWT cannot be replayed from a different device without that binding check.

**Token lifetime standardised at 30 days default (7–90 configurable).** Three conflicting values existed in the design doc (7 days, 30 days, 90 days). All resolved to 30 days default.

**Audit logging is a v1 requirement, not v2.** Minimum exchange logging (attempt, outcome, token ID, IP) is ~5 lines of code and should not be deferred. Moved to v1 in-scope list.

**Rate limiting policy is now concrete.** Replaced the unenforceable "5 req/min per device ID" with: 3 failed exchange attempts within 10 minutes → token locked, requires re-registration. IP-based limiting as secondary layer.

### Biometric Auth Issue Decomposition (2026-07-14)

**17 issues created (#12–#28) decomposing Design/biometric-auth.md into 4 implementation phases.**

**Phase ownership model proved effective:** One primary `squad:*` label per issue, with a secondary label only where joint ownership is genuinely required (e.g., `squad:blathers` + `squad:kicks` for MobileBundleService, `squad:isabelle` + `squad:kicks` for Capacitor bridge). Avoids the ownership dilution pattern flagged in the Ralph issue triage learnings.

**Dependency order matters:** Created issues #12 (DB) and #13 (JWT signing) before register/exchange endpoints so issue numbers could be referenced in dependent bodies. Use `--body-file` for all multi-line issue bodies — far more reliable than inline shell strings.

**`squad:kicks` label was missing** — created it as part of this task. Any future mobile native work should use this label.

**Security issues flagged as `priority:p1`** — rate limiting (#18), audit logging (#17), tenant isolation (#27), enrollment change detection (#26), and pentest checklist (#28) are all marked p1. None should slip to v2.

**Pentest checklist (#28) is a `type:spike`** — it produces a sign-off comment, not code. Closing it requires Copper's explicit go/no-go comment on the issue thread.

**`biometric-auth` label created** with color `#7B68EE` for grouping all 17 issues.

### v1.3.2 Release (2026-03-31)

**Released biometric auto-login and token lifecycle hardening features.** Two significant milestones shipped:

1. **Server-side biometric auto-login injection** — `PrismBrandingMiddleware` now injects an IIFE script into unauthenticated mobile HTML pages. Script checks SecureStorage for a biometric token, prompts Face ID/Touch ID, exchanges the token for a session cookie via `/exchange`, and reloads the page. Graceful fallback to normal login if no token or biometry is declined.

2. **Token lifecycle hardening** — Stale token detection after reinstall (checks `localStorage.ENROLL_KEY` alongside Keychain token; clears stale tokens), credential clearing on logout (detects logout/signout navigation, clears SecureStorage token + enrollment state + device ID + calls new revoke endpoint), and new `DELETE /umbraco/prism/mobile/biometric/revoke` endpoint for soft-revoking credentials (per-device or all-for-user, idempotent 204).

**Supporting changes:**
- Fixed `context.User.Identity?.IsAuthenticated` always returning false in middleware — moved middleware to run after auth pipeline
- Secure signing key pattern: `ILogger<PrismBrandingMiddleware>` in constructor, key via User Secrets (dev) / Azure Key Vault (prod)
- Test fix: `PrismBrandingMiddlewareTests.cs` updated with `NullLogger<PrismBrandingMiddleware>`

**Release process:**
- Committed 2 existing commits from session (9026805 + new cc665bc)
- Version bump 1.3.1 → 1.3.2 in `UmbracoPrism.Core.csproj`
- Updated CHANGELOG.md with v1.3.2 section (features, fixes, upgrade notes)
- Tagged v1.3.2 and pushed origin main + tags
- Release notes prepared (gh auth issue prevented automated GitHub release creation; manual web UI creation needed or gh token setup)

**Key learnings:**
- Conventional commit format with Co-authored-by trailer enforced across all commits
- Release notes should include upgrade instructions (e.g., signing key setup) alongside feature descriptions
- GitHub Actions deploy workflow may need adjustment if gh CLI token is required for release creation in future

### Push Notifications Architecture Design (2026-07-14)

**What was designed:** End-to-end push notification feature for Prism Mobile, covering content-driven notifications (Umbraco backoffice events) and backend-triggered notifications (developer API).

**Design doc:** `docs/design/notifications-architecture.md`

**Key architectural decisions made:**

1. **FCM as default provider behind `IPrismPushGateway` interface.** Firebase Cloud Messaging (HTTP v1 API) is the shipped default. Interface exists from v1 so consumers can swap providers. FCM is free, cross-platform, and has native Capacitor plugin support.

2. **Extend `prismDeviceCredentials` with `PushToken` column.** Reuse the existing device credential row rather than creating a separate table. The device concept stays unified — one row per device per tenant, whether it has biometric auth, push, or both.

3. **Prism-managed subscriptions over FCM topics.** New `prismNotificationSubscriptions` table stores per-user (not per-device), per-tenant topic subscriptions. Gives full control: queryable, tenant-isolated, admin-visible. FCM topics are device-scoped and not tenant-aware — wrong abstraction.

4. **`IPrismNotificationService` is the developer-facing API.** Four methods: `SendToTopicAsync`, `SendToUserAsync`, `SendToAllAsync`, `SendToDevicesAsync`. Simple, injectable, Umbraco-idiomatic. Payload model is `PrismPushPayload` (title, body, image, deep-link, data, category).

5. **Content event hook via `PrismContentNotificationHandler`.** Listens to `ContentPublishedNotification` (Umbraco's built-in notification system). Resolves tenant, builds payload, sends to `content:{nodeId}` and `contentType:{alias}` topics. Opt-in via config.

6. **Synchronous delivery for v1.** In-process sending with async batching (500 tokens per FCM request). Queue-based delivery deferred to v2. Interface stays the same — queueing is an internal detail.

7. **MobileBundleService generates `notifications-bridge.ts`.** Same conditional generation pattern as `biometric-bridge.ts`. Consumer gets push scaffolding (permission, registration, deep-link handling) without writing Capacitor code.

8. **"Content Expiry Watchdog" as backend-triggered demo.** Hourly `IRecurringBackgroundTask` checks for expiring content and pushes notifications. Real-world value, demonstrates the scheduled-task pattern, ships as example code.

**Decisions file:** `.squad/decisions/inbox/tom-nook-notifications-design.md` (6 decisions needing team review)

**Open questions for team:**
- Should `PushToken` extend `prismDeviceCredentials` or get its own table? (Decision 1 — needs agreement)
- FCM service account key storage: file path vs. Key Vault secret? (Design doc recommends supporting both via `keyvault:` prefix)
- Single-tenant vs. multi-tenant content event resolution: default to single-tenant, opt-in domain-based for multi-tenant?
- v1 scope boundary: is the Content Expiry Watchdog shipped as auto-registered or example-only?

### Notifications Design Pre-Implementation Alignment (2026-07-14)

**Task:** Cross-cutting consistency check across all four notification design documents before implementation begins.

**What was done:** Read architecture, backend, mobile, and demo docs; checked 8 consistency points (device token storage, API surface, contracts, triggers, flags, credential location, subscriptions, permissions timing).

**Findings:**
1. **Device token storage conflict resolved:** Backend doc still described separate `prismDeviceTokens` table; updated it to match confirmed decision of extending `prismDeviceCredentials` with `PushToken` column.
2. **API endpoints consistent:** Mobile and backend both define `POST /umbraco/prism/mobile/push/register` with identical contract.
3. **Mobile ↔ Backend contract clear:** Token registration flow is well-aligned; no rework needed.
4. **Demo ↔ Backend triggers aligned:** Vinyl Vault's `ContentPublishedNotification` scenarios match backend handler.
5. **`PushNotificationsEnabled` consistently applied:** Mobile-layer opt-in flag; backend doesn't need to know about it (generation-time decision).
6. **FCM credential storage consistent:** Both docs recommend Azure Key Vault with optional `keyvault:` prefix pattern.
7. **Subscription table schema identical:** Same `prismNotificationSubscriptions` schema in both architecture and backend.
8. **Permission timing not blocked:** Mobile requests after biometric login; no conflicts in other layers.

**Action taken:** Updated `docs/design/notifications-backend.md`:
- Removed `PrismDeviceTokenSchema` class and `CreatePrismDeviceTokensTable` migration
- Added `PushToken` property to existing `prismDeviceCredentials` extension pattern
- Replaced stale token cleanup: `UPDATE ... SET PushToken = NULL` instead of DELETE
- Updated Phase 1 checklist

**Go/No-Go:** ✅ **GO FOR IMPLEMENTATION** — All cross-cutting concerns resolved. No documentation rework needed during implementation.

**Key insight for team:** Extending the device credential row (rather than creating a new table) keeps the device model unified and future-proof. A device may have biometric auth without notifications (legacy biometric-only deployments), or notifications without biometric (push-only tenants), or both — the schema supports all three cleanly.

### Workflow Forms Engine Architecture (2026-04-08)

**What was designed:** End-to-end Prism Workflow Forms Engine as a demonstration framework where workflow configuration is the source of truth and channels are renderers of workflow state.

**Key architectural decisions made:**

1. **Scope boundary: Demo framework, not production BPM.** Prism provides the runtime execution contract, state machine semantics, and reference archetypes. Implementors provide specific workflow definitions and business logic. Ships with one canonical example workflow (Information Request: Draft → Submitted → UnderReview → [Approved|Rejected]) to demonstrate the framework.

2. **Storage model: Hybrid NPoco + JSON fixtures.** Workflow instance state (live runtime data) uses NPoco tables (`prismWorkflowInstances`, `prismWorkflowEvents`, `prismWorkflowTasks`) for transactional integrity and querying. Workflow definitions (versioned configuration) seed from JSON fixtures with optional table storage in v2. Umbraco content storage is inappropriate because workflows are system configuration, not content.

3. **Actor model: Role-based only for v1.** Task routing uses Umbraco backoffice group aliases (e.g., `backoffice-reviewers`). User assignment deferred to v2 to avoid claim/release/reassignment complexity. `WorkflowTask` schema includes `AssignedToUserId` column (nullable) reserved for future use.

4. **Optimistic concurrency: Required from day one.** All mutating endpoints (`/submit/{fieldGroupKey}`, `/actions/{actionKey}`) require `stateVersion` in payload. Validation enforces `submitted == current` or returns 409 Conflict. Adding concurrency control retroactively is a breaking API change; ship it now.

5. **Audit trail: Strictly transactional.** State transitions and audit events (`prismWorkflowEvents`) are written in the same NPoco transaction. No eventual consistency complexity for demo/framework use case. Implementors can add async audit projection separately if needed.

6. **Accessibility: WCAG 2.1 AA baseline.** All shipped archetypes (Collect, Review, TaskQueue, Decision, RequestChanges, StatusTimeline, Completion) must meet WCAG 2.1 AA before sign-off. Enforced via Playwright axe-core tests + manual keyboard/screen reader testing. GDS design system aesthetic (progressive disclosure, plain language, mobile-first responsive).

7. **Prism integration: Tenant isolation + IPrismContext + NPoco migrations.** All workflow instances scoped by `TenantId` (same pattern as `prismDeviceCredentials`). Workflow runtime services consume `IPrismContext` for tenant/user resolution. Migrations use `AsyncMigrationBase` in `PrismMigrationPlan` (NOT EF Core). MockBackOffice extended under `/api/backoffice/workflows/*` namespace.

8. **Contract-driven rendering: Response envelope + archetype mapping.** All workflow endpoints return consistent envelope with `responseState` (`ask_now`, `wait`, `complete`, `error`), `stateVersion`, `correlationId`, and `render` payload. HTTP status maps to transport outcome; `responseState` maps to workflow meaning. Channels consume only render payload contract; never interpret raw transition graph.

**Design doc:** `docs/design/workflow-forms-engine.md` (authoritative)

**Phase 0 deliverable shipped:** JSON fixture for Information Request workflow + field groups (personal-details, request-details).

**Companion design docs required:**
- **Blathers:** Backend contracts (`docs/design/workflow-forms-backend.md`) — endpoint contracts, validation rules, error codes.
- **Isabelle:** Client archetypes (`docs/design/workflow-forms-client.md`) — component API, props, events, Storybook fixtures.
- **Brewster:** TestSite integration (`docs/design/workflow-forms-testsite.md`) — seeding strategy, emulator endpoints, demo personas.
- **Copper:** Security review (`docs/design/workflow-forms-security.md`) — tenant isolation, authorization checks, CSRF/rate limiting.

**Open questions for team:**
- None. All five open questions from proposal resolved with rationale in design doc.

**Risk flagged:** Over-scoping into full BPM product. Mitigated with strict non-goals and demo-first implementation phases.

## Workflow Forms Engine Design — Architecture Lead (2026-04-08)

**Decision Set:** `📌 2026-04-08: Workflow Forms Engine Architecture (Tom Nook)` in `.squad/decisions.md`

**Role:** Lead architect for Workflow Forms Engine design phase. Resolved all 5 open questions from proposal (`docs/design/workflow-forms-engine-demo.md`). Set authoritative architecture foundation for downstream Backend (Blathers), Client (Isabelle), Umbraco (Brewster), and Security (Copper) teams.

**Decisions Produced:** 8 core architectural decisions
1. Scope Boundary — Demo Framework (not production BPM)
2. Storage Model — Hybrid NPoco + JSON Fixtures
3. Actor Model — Role-Based Only for v1
4. Optimistic Concurrency — Required from Day One
5. Audit Trail — Strictly Transactional
6. Accessibility — WCAG 2.1 AA Baseline
7. Prism Integration — Tenant Isolation + IPrismContext + NPoco Migrations
8. Contract-Driven Rendering — Response Envelope + Archetype Mapping

**Cascading Decisions for Team:** All downstream decisions (backend models, client components, Umbraco integration, security controls) derived from these 8 architectural pillars.

**Design Phase Status:** ✅ Complete (design docs: 2 of 5 completed; Blathers/Isabelle/Brewster/Copper to produce companion specs)



## Session: Workflow Forms Engine Redesign — 2026-04-09

**Timestamp:** 2026-04-09T17:48:03Z  
**Role:** Lead architect  
**Sprint Type:** Cross-agent architecture sprint (parallel with Brewster, Blathers, Isabelle)

### Deliverables

1. **Design Document:** `docs/design/workflow-forms-engine-redesign.md` — 8-section comprehensive architecture
2. **Decision Record:** Merged into `.squad/decisions/decisions.md` as "Workflow Forms Engine Redesign — Element Types as Step Definitions"
3. **Orchestration Log:** `.squad/orchestration-log/2026-04-09T17:48:03Z-tom-nook.md`

### Key Decisions

- **Replace** `PrismFieldGroupDefinition` schema with Umbraco Element Types
- **Leverage** Umbraco's native property editors and validation
- **Delete** bespoke Lit components; implement dynamic form renderer
- **Maintain** workflow state machine, response envelope, controller endpoints unchanged
- **Fix** MockBackOffice assembly isolation (prevents Umbraco management API leakage)

### Phase Outcomes

- Architecture established and peer-reviewed
- Implementation readiness confirmed by Brewster, Blathers, Isabelle
- Next: Backend and frontend tickets assigned; Element Type seeding begins



## Session: 2026-04-10 — Shared Library Extraction + Workflow Architecture Decisions

**Agents spawned:** Tom Nook (analysis), Blathers (implementation)  
**Session log:** `.squad/log/2026-04-10T07:50:19Z-shared-lib-extraction.md`

### Tom Nook

**Task:** Analyze shared library extraction — recommend whether to extract auth/identity helpers from Core into a zero-Umbraco-dependency library.

**Decision:** ✅ **YES — extract UmbracoPrism.Shared** before 1.8.0 release.

**Findings:**
- MockBusinessApp uses only 10 types from Core across 3 namespaces (Extensions, Models.Workflow, Services).
- 8 files to move (7 existing + 1 extracted record) with **zero Umbraco transitive dependencies**.
- Current `ConfigureApplicationPartManager` workaround is code smell; proper fix is cheap.
- Benefits: clean architecture (business apps don't couple to CMS), removes brittle workaround, future-proof for new business apps.

**Output:** `.squad/decisions/inbox/tom-nook-shared-lib-proposal.md` (comprehensive analysis, file list, impact assessment)

### Blathers

**Task:** Implement shared library extraction per Tom Nook's proposal.

**Decision:** ✅ **Created UmbracoPrism.Shared** (.NET 10.0 library, zero Umbraco packages, only Microsoft.Identity.Web + JwtBearer)

**Actions:**
1. Created project: UmbracoPrism.Shared
2. Moved 8 files: PrismIdentityExtensions, PrismAuthExtensions, BackOfficeTenant (extracted), WorkflowResponseEnvelope, 3x IPrismSigningKeyCache*
3. Updated references: Core → Shared; MockBusinessApp: Core → Shared
4. Removed `ConfigureApplicationPartManager` workaround from MockBusinessApp/Program.cs (6 lines deleted)
5. Namespace preservation (UmbracoPrism.Core.* paths unchanged → minimal churn)

**Verification:**
- ✅ Build: `dotnet build UmbracoPrism.sln -c Release` (0 errors, 0 warnings)
- ✅ Tests: 218 Core tests passing
- ✅ Zero breaking changes; all public APIs unchanged
- ✅ MockBusinessApp builds without Core dependency

**Commit:** c4acb2f

**Output:** `.squad/decisions/inbox/blathers-shared-lib-extraction.md` (implementation details, structure, verification)

---

### Decisions Merged (7 inbox files → decisions.md)

1. **tom-nook-shared-lib-proposal.md** — Architectural analysis recommending extraction
2. **blathers-shared-lib-extraction.md** — Implementation with commit c4acb2f
3. **copilot-workflow-authority-to-business-app.md** — Workflow authority moved to MockBusinessApp (HTTP proxy pattern)
4. **tom-nook-workflow-cleanup.md** — Directive to delete dead code from old state machine; approved for execution
5. **celeste-workflow-docs.md** — XML documentation complete on 7 workflow files
6. **copper-workflow-security-review.md** — Security review; 4 CRITICAL + 2 HIGH issues identified and fixed
7. **copilot-directive-2026-04-09T175520.md** — No Lit for workflow form rendering; use Razor partials instead

All inbox files deleted after merge.

---

**Status:** Complete. Scribe merged decisions, created orchestration logs, updated history.

## Session: Workflow minimal API refactor
- Deleted WorkflowApiController.cs
- Moved /api/workflow/* endpoints to Program.cs as MapPost minimal API
- userId now derived via GetEmail() (consistent with /api/backoffice/me)
- WorkflowAdvanceApiRequest record moved to Program.cs bottom

---

## Session: TUI Control Plane Design for MockBusinessApp — 2025-07-15

**Role:** Lead architect  
**Requested by:** Jonny  
**Task:** Assess and design replacing `WorkflowEmulatorController.cs` with an embedded terminal TUI

### Current State Assessed
- `WorkflowEmulatorController` exposes 3 endpoints (list instances, advance-as-reviewer, list definitions) protected by `[EmulatorOnly]` (dev env only) + JWT Bearer auth
- Developer must use `.http` files or curl to trigger reviewer actions — awkward for a dev mock tool
- `BusinessAppWorkflowEngine` is a singleton with `GetAllInstances()`, `AdvanceAsReviewer()`, `GetCurrent()`, `Advance()` — all the data is accessible in-process
- App already runs in a terminal; the opportunity is to make that terminal interactive

### Recommendation: Option A — Spectre.Console REPL as BackgroundService

**Chosen over:**
- Terminal.Gui (Option B): ncurses panels are overkill for a list+action dev tool
- Bare Console.ReadLine (Option C): no tables, no colour, no discoverability

**Key decisions:**
1. **Hosting:** `TuiReplService : BackgroundService`, registered via `AddHostedService<TuiReplService>()`. Starts after web host is listening. Runs `while(true)` readline loop.
2. **Logging conflict:** Configure `appsettings.Development.json` to set `Default` log level to `Warning` — suppresses runtime request logs, lets startup/shutdown messages through. REPL uses `AnsiConsole.MarkupLine` exclusively. No Serilog dependency required for first pass.
3. **Remove controller stack:** `AddControllers()`, `MapControllers()`, `Controllers/`, `Filters/`, and `Microsoft.AspNetCore.OpenApi` package can all be deleted.
4. **NuGet:** Add `Spectre.Console` only.
5. **New engine method needed:** `Reset(instanceId)` on `BusinessAppWorkflowEngine` to support the `reset` command (must clean both `_instancesById` and `_instanceLookup`).

### Command Set Designed
`list` / `show <id>` / `approve <id>` / `reject <id>` / `reset <id>` / `defs` / `help` / `quit`

Short 8-char ID prefix matching so devs don't paste full GUIDs.

### Decision Written
`.squad/decisions/inbox/tom-nook-tui-design.md` — full proposal with architecture, command set, risks, and routing to Blathers for implementation.

### Next Routing
- **Blathers:** Implement `TuiReplService`, `Reset()` engine method, remove controller stack, update `Program.cs`
- **Tangy:** Smoke tests for new `Reset()` method on `BusinessAppWorkflowEngine`

---

## Session: Workflow UI Analysis — 2025-07-15

**Role:** Lead architect
**Requested by:** Jonny
**Task:** Thorough analysis of the workflow UI layer (end-to-end, critique, simplification recommendations)

### Files Audited

- `WorkflowPageController.cs` — Route-hijacking controller, GET/POST via single `Index()`
- `WorkflowPage.cshtml` — Main view; Archetype switch dispatch to partials
- `_WorkflowStep-Collect.cshtml` — Form rendering with inline field switch
- `_WorkflowStep-Review.cshtml` — Read-only summary + form actions
- `_WorkflowStep-StatusTimeline.cshtml` — Stub: static text + emoji only
- `_WorkflowStep-Completion.cshtml` — Static hardcoded completion text
- `Views/Shared/_WorkflowField.cshtml` — Comprehensive field renderer (NOT used by Collect partial)
- `WorkflowViewModel.cs` — PublishedContentWrapped view model
- `WorkflowAdvanceRequest.cs` — Dead model (never used by controller)
- `WorkflowDemoPage.cshtml` — Separate Web Component approach (`<prism-workflow-shell>`)
- `retirement-quote-v1.json`, `information-request-v1.json` — Workflow definitions
- `personal-details-v1.json`, `request-details-v1.json` — Field group seeds

### Key Findings

**CRITICAL BUG — `_WorkflowField.cshtml` field naming is broken:**
`Views/Shared/_WorkflowField.cshtml` uses bare field names (`name="@Model.FieldKey"`) but the controller POST handler expects `fields[{fieldKey}]` format. If this partial were used for Collect, all field values would be silently dropped. The Collect partial has its own (duplicate) inline renderer — this is what keeps forms working today.

**Dead code — `WorkflowAdvanceRequest.cs`:**
Defined but never model-bound; controller reads directly from `HttpContext.Request.Form`. Safe to delete.

**Two archetypes are stubs:**
`StatusTimeline` renders emoji + hardcoded static text. `Completion` has hardcoded "A member of our team will be in touch shortly." Neither is driven by definition data or CMS content.

**Dual field renderers (duplication):**
Field rendering logic exists in two places: inline in `_WorkflowStep-Collect.cshtml` (covers text/select/textarea) and in `_WorkflowField.cshtml` (covers text/select/textarea/boolean/radio/checkboxlist/number/decimal/date/datetime). These are inconsistent in coverage; `_WorkflowField.cshtml` is the richer one but is not wired up.

**Inline CSS in every partial:**
Each partial carries its own `<style>` block. Real sites will inject duplicate styles into the body, and there is no canonical stylesheet designers can reference or override.

**Two competing UI approaches:**
`WorkflowPage.cshtml` (Razor server-rendered) and `WorkflowDemoPage.cshtml` (Web Component `<prism-workflow-shell>`) are entirely separate. It is unclear which is canonical.

**Sync-over-async in controller:**
`GetAwaiter().GetResult()` in `HandleGet()` can deadlock under ASP.NET Core's default synchronisation context. The Umbraco route-hijacking constraint (Index must be synchronous) is real, but `Task.Run()` wrapping would be safer.

**Switch-statement dispatch is fragile:**
Adding a new Archetype requires editing `WorkflowPage.cshtml`. Convention-based partial resolution (`_WorkflowStep-{Archetype}.cshtml`) would be zero-code to extend.

### Recommendations

1. Fix `_WorkflowField.cshtml` field name prefix — add `fields[...]` wrapper and use it from Collect partial; delete duplicate inline renderer.
2. Delete `WorkflowAdvanceRequest.cs` — dead code.
3. Extract inline CSS to `prism-workflow.css` — single stylesheet in wwwroot; partials reference BEM classes only.
4. Replace switch in `WorkflowPage.cshtml` with convention-based partial discovery.
5. Drive StatusTimeline and Completion from definition/CMS data, not hardcoded strings.
6. Decide and document canonical UI approach: server-rendered Razor OR Web Component, not both simultaneously.
7. Add safe async wrapper in controller to avoid sync-over-async deadlock risk.

### Workflow Form Validation — Design Architecture Review (2026-04-11)

**Role:** Lead architect

**Task:** Validate Jonny's proposed validation plan; critique the design principle; evaluate current state; identify gaps and risks; advise on tag helper approach; assess DX end-to-end.

**Deliverable:** `.squad/decisions/inbox/tom-nook-validation-architecture.md` (comprehensive architectural review with decision matrix)

**Key Assessment:**

The plan is **sound and ready to implement** with three critical blockers resolved first:

1. **FieldRenderPayload model gaps** — Missing `MinLength`, `MaxLength`, `Pattern`, `Min`, `Max` properties. These are prerequisites for HTML5 emission and server validation. Hard blocker for tasks #3–#5.

2. **IDistributedCache auto-registration** — Plan assumes manual registration; violates "principle of least surprise." Must ship with sensible defaults: in-memory for dev (with warning), explicit guidance for Redis/SQL Server in production.

3. **Nonce cache TTL configurability** — 30 minutes too short for multi-step workflows (users step away). Make configurable via `PrismWorkflowOptions.StepNonceTtl`; default to 2 hours.

**Design Principle — APPROVED FOR RECORDING:**

> "Make it easy to do the right thing; principle of least surprise. The only install should be including the package. Validation, accessibility, tamper-proofing, and Umbraco idioms should all be in place automatically — developers shouldn't have to know they need them. Where choices exist, the Prism default should be the correct choice."

This principle is **correct and should be recorded in decisions.md as a first-class architectural decision.** It resolves future design debates: defaults must work; configuration is for advanced cases; the "configure everything" trap is a failure mode for .NET platform packages.

**Five-Layer Validation Architecture:**

All layers are correctly scoped (model constraints → HTML5 → tamper-proofing → server structural → BA logic). The nonce + distributed cache approach is proven and correct vs. HMAC signing. Controller integration requires careful wiring; no shortcuts allowed (must validate before calling BA).

**Tag Helper Approach:**

`<prism-workflow-form>` tag helper is the correct Umbraco 17 idiom. Matches Prism's Web Component naming convention; developers familiar with Umbraco will recognize the pattern. No need for `<prism-workflow-step>` tag helper yet (deferrable enhancement).

**Secondary Concerns (Not Blockers):**

- Unknown key handling: currently silent discard. Should log warnings; optional strict mode to treat as error.
- BA error protocol: need clarity on `error` vs. `validation_error` distinction. Document strictly or introduce `WorkflowResponseState` enum.
- Nonce in all POST partials: must be in all form-rendering step types, not just Collect.
- Developer experience validated end-to-end: install package → use tag helper → validation works automatically. Only BA and cache configuration require developer input.

**Implementation Sequencing:**

- Design principle (decision) — Blocker #1
- Model constraints → IDistributedCache registration → Nonce TTL configuration (Blathers) — Blockers #2–#3
- HTML5 attributes / Nonce service / Field validator — Parallel phase
- Controller integration → Tag helper → Error CSS — Sequential
- Test site demo → User guide — Last

**Decisions Written:**

1. Design principle recorded to decisions.md
2. Three critical blockers identified with clear resolution steps
3. Implementation sequencing with dependency graph
4. Tag helper idiom validated
5. End-to-end DX walkthrough confirmed

**Key Insight:** This plan exemplifies the right architecture for a platform package. The "developer installs package and validation just works" outcome is achievable if the three blockers are resolved before any implementation begins. No shortcuts on model extensions, cache registration, or TTL configurability.

---

## 2026-04-11: Recorded "Principle of Least Surprise" as Standing Design Decision

**Action:** Formalized the Prism design principle articulated during workflow validation planning.

**Deliverable:** `.squad/decisions/inbox/tom-nook-design-principle.md`

**Status:** Complete

**Rationale:** This principle was already documented in historical notes from the validation architecture review. Elevated it to a formal standing decision so it serves as a reference point for all future feature design and architectural reviews.

**Standing Effect:** This principle now governs evaluation of all new Prism APIs, defaults, and behaviors. Every feature must answer: "Does this require developers to do extra work, or does it just work?"
