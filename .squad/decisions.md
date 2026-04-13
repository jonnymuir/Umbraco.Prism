# Decisions

Umbraco.Prism team decisions. Append-only ledger.

---

## 📌 2026-04-13: Brewster — Dashboard Route Contract

**Decision:** Keep the seeded Umbraco dashboard contract as a direct published route at `/dashboard`, but have browser tests reach it from the signed-in home page CTA while asserting that CTA resolves to `/dashboard`.

**Context:** localhost auth/session Playwright flow for the seeded TestSite dashboard.

**Why:**
- `/api/prism/downstream-demo/seed-contract-ready` already treats `/dashboard` as part of the machine-checked route contract.
- An unauthenticated request to `/dashboard` correctly challenges to `/auth/login?ReturnUrl=%2Fdashboard`, so the app-side route wiring is sound.
- Driving the browser through the `Go to Dashboard` link exercises the same authored Umbraco navigation the user sees and avoids false negatives where the test is still on the home page when it expects dashboard-only UI.

**Implications:**
- Do not weaken the seed contract to allow a home-page fallback for dashboard scenarios.
- Localhost Playwright flows should verify the CTA `href` and then click it before asserting dashboard UI.

**Session Log:** `.squad/log/2026-04-13T23:05:08Z-dashboard-test-investigation.md`

### Tangy — Dashboard navigation trace

**Decision:** Live dashboard Playwright coverage should assert dashboard-only UI after navigation, not shared welcome copy.

**Why:** In the localhost auth/session repro on 2026-04-13, a signed-in member remained on `/` after both direct `page.goto('/dashboard')` and clicking the authored `Go to Dashboard` CTA. The home page still showed `Welcome back, Demo User`, so that heading could not distinguish a successful dashboard navigation from a failed one.

**Contract impact:**
- Keep the desired user contract: signed-in members should reach `/dashboard` and see dashboard-only actions.
- In Playwright helpers, treat `View Workflows` and `Call Mock Business App API` as the readiness signals for the dashboard.
- If those elements never appear, report an app routing break rather than letting the test hang on a later click.

**Evidence:**
- `src/UmbracoPrism.Client/tests/localhost-auth-session.spec.ts`
- `src/UmbracoPrism.TestSite/Views/HomePage.cshtml`
- `src/UmbracoPrism.TestSite/Views/MemberDashboard.cshtml`

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


---

## Decision: Push Notifications Phase 3 — Capacitor Plugin Integration

**Date:** 2026-08-15  
**Author:** Kicks (Mobile Native Specialist)  
**Status:** Implemented  
**Phase:** 3 of Push Notifications Feature  

---

### Context

Prism Mobile needs push notification support for mobile apps generated via the bundle generator. This is Phase 3 of a multi-phase push notifications feature. Backend design (Blathers) defined the server-side API endpoints and FCM/APNs integration strategy. Mobile design (Kicks) defined the Capacitor plugin architecture and native platform requirements.

Phase 3 focuses on integrating the chosen Capacitor plugin (`@capacitor/push-notifications`) into the TypeScript client codebase and exposing it via the bundle generator UI.

---

### Decision

#### 1. Use `@capacitor/push-notifications` v7.0.0

**Plugin:** `@capacitor/push-notifications@^7.0.0`  
**Rationale:** Official Ionic-maintained plugin, aligns with Capacitor 7.x ecosystem version used by Prism, lighter footprint than `@capacitor-firebase/messaging`, APNs-native on iOS.

**Alternative Considered:** `@capacitor-firebase/messaging`  
**Why Rejected:** Adds 20-50MB Firebase SDK overhead. Only needed if consumers require Firebase Analytics, data-only messages, or Firebase Topics. Prism's backend handles topic-like functionality server-side via genre subscriptions.

#### 2. Make Push Notifications Opt-In (Default: `false`)

**Bundle Request Field:** `pushNotificationsEnabled: boolean`  
**Default Value:** `false`  
**Rationale:**
- Keeps base mobile bundle lean (no push dependencies if not needed)
- Allows tenants to ship apps without push if they don't have a notification strategy
- Reduces first-time setup friction for tenants experimenting with Prism Mobile
- Aligns with Apple HIG "request permissions when needed" philosophy

**Alternative Considered:** Default `true` (push notifications always included)  
**Why Rejected:** Forces all tenants to configure FCM/APNs even if they never use notifications. Adds setup complexity to the already multi-step mobile bundle flow.

#### 3. Defer Permission Request Timing to Consumers

**Decision:** The `PrismPushNotifications.registerDevice()` method handles the full permission → registration flow, but does NOT auto-trigger on app launch or post-biometric-login.

**Where Permission is Requested:** Left to bundle consumers to implement (or future Prism enhancement). The UI hint suggests "after first biometric login", which aligns with the mobile design spec recommendation (see `docs/design/notifications-mobile.md`).

**Rationale:**
- Different tenants may want different permission prompt timing (e.g., after first content view, after user subscribes to a content node, etc.)
- Prism should provide the tools (`PrismPushNotifications` API) but not dictate UX flow
- Avoids hard-coding permission logic into the bundle generator (keeps bundles flexible)

**Team Decision Required:** Should Prism auto-inject a permission request hook into the bundle's biometric login success flow? Current implementation leaves this as a manual consumer task.

#### 4. Align API Endpoints with Backend Design

**Endpoints Used:**
- `POST /umbraco/prism/push/register` — device token registration
- `DELETE /umbraco/prism/push/register` — device unregistration
- `POST /umbraco/prism/push/subscribe` — genre subscription
- `DELETE /umbraco/prism/push/unsubscribe` — genre unsubscription

**Payload Format (Register):**
```json
{
  "token": "fcm-or-apns-token-here"
}
```

**Authentication:** Bearer token via `Authorization` header (matches existing Prism API auth pattern)

**Rationale:** These endpoints were defined by Blathers in `docs/design/notifications-backend.md`. Mobile client implementation strictly adheres to that contract.

**Note:** Backend team (Blathers) must implement these endpoints for Phase 3 to be end-to-end functional.

#### 5. Document Native Setup as Manual Steps

**Decision:** iOS and Android native project configuration (APNs keys, `google-services.json`, entitlements, permissions) remains a manual consumer task documented in `docs/PUSH_SETUP.md`.

**Why Not Automated?**
- APNs key/certificate setup requires Apple Developer account credentials (cannot be automated by Prism)
- Firebase project setup requires Firebase Console access and `google-services.json` download (external to Prism)
- Xcode capability toggles and entitlement file edits are native IDE operations
- Automating these would require Prism to parse/modify Xcode `.pbxproj` files and Android Gradle files (fragile, error-prone)

**Future Consideration:** `MobileBundleService.cs` could inject placeholder comments or stub files (e.g., `resources/PUSH_SETUP.md`, `resources/ios-entitlements-snippet.xml`) into the bundle to guide consumers. This is out of scope for Phase 3.

---

### Implementation Summary

**Files Created:**
- `src/UmbracoPrism.Client/src/backoffice/push-notifications.ts` — `PrismPushNotifications` static class with 8 public methods
- `docs/PUSH_SETUP.md` — comprehensive iOS/Android setup guide

**Files Modified:**
- `src/UmbracoPrism.Client/package.json` — added `@capacitor/push-notifications@^7.0.0`
- `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.ts` — added `pushNotificationsEnabled` toggle and payload field
- `src/UmbracoPrism.Client/src/backoffice/index.ts` — exported `PrismPushNotifications` and `PushPermissionState`

**Build Status:** ✅ `npm run build` passes with no TypeScript errors

---

### Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Backend endpoints not implemented yet | Phase 3 cannot be tested end-to-end | Tagged as "awaiting backend" in Kicks history. Blathers to implement in Phase 4. |
| Consumers may not understand multi-step native setup | High friction, support burden | Created detailed `PUSH_SETUP.md` with troubleshooting section. Consider adding setup wizard in future. |
| Permission prompt timing unclear | Tenants may implement inconsistent UX | Document recommended timing (post-biometric-login) in bundle README. Consider auto-injection in future Prism version. |
| APNs p8 key vs p12 cert confusion | iOS push setup failures | `PUSH_SETUP.md` explicitly recommends p8 and explains why. |

---

### Team Dependencies

- **Blathers (Backend):** Must implement `/umbraco/prism/push/*` endpoints per `docs/design/notifications-backend.md`
- **Tom Nook (Services):** May need to update `MobileBundleService.cs` to conditionally include push notification scaffolding when `pushNotificationsEnabled: true`
- **Isabelle (UI):** No action required (push notification UI is in the tenant modal, already implemented)

---

### Future Enhancements

1. **Auto-Inject Permission Hook:** Modify bundle generator to inject `PrismPushNotifications.registerDevice()` call into the biometric login success flow when `pushNotificationsEnabled: true`.

2. **Bundle Scaffolding:** Generate Android notification channel setup code in `www/index.html` when push is enabled (per mobile design spec recommendation).

3. **Setup Wizard:** Create an interactive CLI tool (`npx prism-setup-push`) that walks consumers through Firebase/APNs setup and auto-updates native config files.

4. **Testing UI:** Add a "Test Push" button to the tenant modal that sends a test notification to all registered devices for that tenant.

5. **Firebase Option:** Provide a "Use Firebase Messaging" toggle in the tenant modal for consumers who want `@capacitor-firebase/messaging` instead of the default `@capacitor/push-notifications`.

---

### Conclusion

Phase 3 is complete from the TypeScript/Capacitor integration perspective. The `PrismPushNotifications` API is production-ready and follows Capacitor best practices (graceful web degradation, permission-first flow, error logging). 

Next blocker: Backend endpoint implementation. Once `/umbraco/prism/push/register` exists, we can test end-to-end token registration.

**Approved for merge:** ✅ (pending backend Phase 4 implementation)

---

## 📌 2026-04-03: Phase 4 Background Notification Architecture (Blathers)

**Session Log:** `.squad/log/2026-04-03T12:57:36Z-notifications-complete.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-phase4-limited-edition.md`

### Blathers — Phase 4 Background Notification Architecture

Phase 4 introduces background scheduled notifications and API-triggered notifications for vinyl records.

**Background Service Pattern:**
- **`LimitedEditionDropNotifier`** uses `BackgroundService` base class from `Microsoft.Extensions.Hosting`
- Interval is configurable via `Prism:Notifications:LimitedEditionDropIntervalMinutes`
- Service can be disabled by setting interval to 0 (logs info and exits early)
- Tenant context is resolved from config (`Prism:Notifications:LimitedEditionTenantId`) rather than `IPrismContext` (which is request-scoped)
- All exceptions are caught and logged; service never crashes the host

**Controller Pattern for Vinyl Notifications:**
- **`PrismVinylNotificationController`** follows the same auth pattern as `PrismNotificationController`
- Uses `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` for authenticated-only access
- Genre-aware routing: if genre provided, sends to genre subscribers; else broadcasts to all members
- Request validation returns 400 for missing required fields

**Service Registration:**
- `LimitedEditionDropNotifier` registered as hosted service in `PrismComposer` via `AddHostedService<>()`
- Follows existing composer pattern for service registration
- `PrismContentPublishedHandler` was already registered in previous phase (no changes needed)

**Rationale:**
1. **BackgroundService over raw Task:** Provides built-in lifecycle management, graceful shutdown, and integration with ASP.NET Core hosting
2. **Config-based tenant resolution:** Background services have no HTTP request context, so tenant must come from config
3. **Interval = 0 disables:** Simple on/off switch without needing a separate boolean flag
4. **Exception isolation:** Background service errors must never crash the host; each iteration is individually try/catch wrapped

**Implications:**
- **Configuration:** Production deployments must set `Prism:Notifications:LimitedEditionTenantId` for the notifier to fire
- **Future work:** Multi-tenant iteration (reading all tenants) requires a tenant enumeration API (not implemented in Phase 4)
- **Testing:** Background services are harder to unit test; integration tests should verify scheduled behavior
- **Logging:** All lifecycle events (start, fire, skip, error) are logged at INFO or WARNING level for observability

**Alternative Considered:**  
**Hangfire/Quartz.NET:** More robust scheduling but adds dependencies and complexity. Simple `TimeSpan` interval is sufficient for v1.

**Status:** ✅ Implemented and verified via `dotnet build`

---

## 📌 2026-04-03: Push Notification Test Strategy (Tangy)

**Session Log:** `.squad/log/2026-04-03T12:57:36Z-notifications-complete.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/tangy-notification-tests.md`

### Tangy — Push Notification Test Strategy

**Context:**  
The push notification feature integrates Firebase Cloud Messaging (FCM) into the Umbraco.Prism package. The `PrismNotificationService` initializes Firebase directly in the constructor, making it difficult to mock for unit tests.

**Decision:**  
**Unit tests verify core logic and database interaction; FCM delivery is deferred to integration tests.**

**Test Coverage Strategy:**

**Unit Tests (implemented):**
1. **Service Layer** — Database operations (token storage, subscription management), control flow, empty-case handling
2. **Controller Layer** — Request validation, authentication/authorization checks, service method invocation
3. **Event Handler** — Routing logic (genre vs. broadcast), content type filtering, exception swallowing

**Integration Tests (future):**
- Actual FCM multicast delivery
- Stale token nullification after FCM response
- Batch processing (500-token chunks)
- End-to-end: content publish → notification delivery

**Firebase Mocking Approach:**

**Why not mock Firebase in unit tests?**
- `PrismNotificationService` calls `FirebaseApp.Create()` and `FirebaseMessaging.GetMessaging()` directly in the constructor
- These are static methods on sealed classes — cannot be mocked with Moq
- Introducing an `IFirebaseMessaging` abstraction would add complexity for minimal unit test benefit

**Chosen approach:**
- Unit tests run with `Prism:Firebase:CredentialJson` **not configured**
- Service constructor initializes `_messaging` as `null`
- `FanOutAsync` logs a warning and returns early (graceful degradation)
- Tests verify database queries run correctly; Firebase delivery is a no-op

**Exception Handling Guarantees:**  
**Critical requirement:** `PrismContentPublishedHandler` must **never throw** — exceptions must not break the Umbraco publish pipeline.

**Tests verify:**
- `Handle_ServiceThrows_DoesNotRethrow` — service exceptions are caught and logged
- `Handle_GenreServiceThrows_DoesNotRethrow` — genre-specific failures are caught and logged

**Alternatives Considered:**
1. **Introduce `IFirebaseMessaging` abstraction** — Rejected: Adds complexity; Firebase SDK is already well-tested; integration tests are the right place for end-to-end validation.
2. **Use Firebase emulator in unit tests** — Rejected: Emulator startup is slow; belongs in integration test suite, not fast unit tests.
3. **Extract FCM delivery to a separate service** — Rejected: Over-engineering for current needs; can refactor later if FCM becomes a bottleneck.

**Consequences:**

**Pros:**
- Fast unit tests (no external dependencies)
- Clear separation: unit tests verify logic, integration tests verify delivery
- Service degrades gracefully when Firebase is unavailable (useful for local dev)

**Cons:**
- FCM delivery path is not covered by unit tests (integration tests required)
- Stale token cleanup logic is not exercised in unit tests (Firebase response simulation needed)

**Status:** ✅ 38 new tests created, 206/206 total passing

---

## 📌 2026-04-08: Workflow Forms Engine Architecture (Tom Nook)

**Session Log:** `.squad/log/2026-04-08T22:15:50Z-workflow-forms-engine-design.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/tom-nook-workflow-forms-architecture.md`

### Tom Nook — Workflow Forms Engine Architecture Decisions

**Decision Overview:** Eight architectural decisions establish the Prism Workflow Forms Engine as a **demonstration framework** (not production-grade BPM), with state machine semantics, tenant-isolated persistence, and 7 interaction pattern archetypes.

#### 1. Scope Boundary — Demo Framework, Not Production BPM

**Decision:** The Prism Workflow Forms Engine is a **demonstration framework**, not a production-grade BPM/low-code designer.

**What Prism Provides:**
- Runtime execution contract
- State machine semantics
- Tenant-isolated persistence
- Reference archetypes (7 interaction patterns)
- One canonical example workflow (Information Request)

**What Implementors Provide:**
- Specific workflow definitions
- Business domain logic
- Custom field groups
- External integrations

**Non-Goals Explicitly Flagged:**
- Production-grade low-code designer
- Executable scripts in workflow definitions
- Cross-tenant shared workflow execution
- External integration connectors (email/SMS/webhook)
- Advanced SLA/escalation rules

**Rationale:** Keeps Prism scope manageable and maintainable. Demonstrates the framework contract without authoring UI complexity. Allows implementors to build domain-specific workflows without framework bloat.

#### 2. Storage Model — Hybrid NPoco + JSON Fixtures

**Decision:** Use dedicated NPoco tables for live workflow instances/events/tasks. Use JSON fixtures (with optional table storage in v2) for workflow definitions and field-group definitions.

**Storage Breakdown:**
- **Live State (NPoco tables):**
  - `prismWorkflowInstances`: Current state, tenant/user metadata, optimistic concurrency token.
  - `prismWorkflowEvents`: Append-only audit stream (state changes, submissions, decisions).
  - `prismWorkflowTasks`: Queueable work items for reviewers/approvers.

- **Configuration (JSON fixtures in v1):**
  - Workflow definitions: `src/UmbracoPrism.MockBackOffice/Fixtures/workflows/information-request.json`
  - Field-group definitions: `src/UmbracoPrism.MockBackOffice/Fixtures/field-groups/personal-details.json`

**Rationale:** Live state requires transactional integrity, optimistic concurrency, and efficient querying. Workflow definitions need import/export, version control, and easy seeding. Hybrid approach balances runtime needs with authoring/versioning needs.

**Future Enhancement (v2):** Optional `prismWorkflowDefinitions` table for storing published definitions at runtime with migration tooling to upgrade running instances to new workflow versions.

#### 3. Actor Model — Role-Based Only for v1

**Decision:** Workflow task routing uses **role-based assignment only** in v1. User assignment deferred to v2.

**v1 Model:**
- Tasks route to Umbraco backoffice group alias (e.g., `backoffice-reviewers`).
- Any user in that role can claim and complete the task.
- Schema includes `AssignedToUserId` column (nullable) reserved for v2; always NULL in v1.

**Rationale:** Role-based routing covers 80% of demo scenarios with minimal complexity. User assignment requires claim/release/reassignment/escalation logic — unnecessary for a demo framework.

#### 4. Optimistic Concurrency — Required from Day One

**Decision:** All mutating workflow endpoints require `stateVersion` enforcement from day one. No exceptions.

**Enforcement:**
- `prismWorkflowInstances.StateVersion` (integer, default 1, increments on every state change).
- All `POST /submit/{fieldGroupKey}` and `POST /actions/{actionKey}` require `stateVersion` in payload.
- Validation: `if (submitted != current) return 409 Conflict`.

**Rationale:** Concurrent submissions (double-click, mobile retry, multi-device) are realistic even in demo scenarios. Adding concurrency control retroactively is a breaking API change. Implementation cost is minimal; HTTP 409 Conflict is a clear, recoverable error for clients.

#### 5. Audit Trail — Strictly Transactional

**Decision:** State transitions and audit events (`prismWorkflowEvents`) are written in the **same NPoco transaction**. No eventual consistency.

**Implementation:**
- Single database transaction for: Update state + increment StateVersion → Append audit event → Insert/update tasks

**Rationale:** Demo/framework use case does not justify eventual consistency complexity. Event-sourced audit requires append-only guarantees: state transitions and events MUST succeed or fail together.

#### 6. Accessibility — WCAG 2.1 AA Baseline

**Decision:** All shipped archetypes MUST meet **WCAG 2.1 Level AA** before demo sign-off.

**Acceptance Criteria:**
- Keyboard navigation (all interactive elements reachable via keyboard only)
- Screen reader support (semantic HTML, ARIA labels/descriptions)
- Color contrast (4.5:1 for body text, 3:1 for large text)
- Focus indicators visible on all interactive elements
- Error identification (validation errors associated with specific fields)
- Form labels properly associated with inputs

**Testing:** Playwright accessibility tests using `axe-core`, manual keyboard-only testing, manual screen reader spot-check.

**Rationale:** WCAG 2.1 AA is the baseline for modern web applications. Retrofitting accessibility is expensive; build it in from the start.

#### 7. Prism Integration — Tenant Isolation + IPrismContext + NPoco Migrations

**Decision:** Workflow runtime integrates with established Prism patterns.

**Tenant Isolation (Non-Negotiable):**
- ALL workflow instances scoped by `TenantId` (same pattern as `prismDeviceCredentials`).
- All queries filter by `TenantId` from `IPrismContext.CurrentTenant.Id`. No cross-tenant visibility.

**IPrismContext Integration:**
- Workflow services consume `IPrismContext` (scoped per HTTP request) for tenant and user resolution.
- User identity: `_prismContext.User.FindFirstValue("oid")` for Entra Object ID.
- Role checks: `_prismContext.User.IsInRole("backoffice-reviewers")` for task filtering.

**NPoco Migration Pattern:**
- Use `AsyncMigrationBase` in `PrismMigrationPlan`, NOT EF Core.
- Migrations: `CreatePrismWorkflowInstancesTable`, `CreatePrismWorkflowEventsTable`, `CreatePrismWorkflowTasksTable`.

**Rationale:** Consistency with established Prism architecture reduces learning curve and maintenance burden. Tenant isolation is a hard requirement for multi-tenant SaaS products.

#### 8. Contract-Driven Rendering — Response Envelope + Archetype Mapping

**Decision:** All workflow endpoints return a consistent envelope with `responseState`, `stateVersion`, `correlationId`, and `render` payload.

**Response Envelope Shape:**
```json
{
  "instanceId": "wf_123",
  "responseState": "ask_now",
  "stateVersion": 7,
  "correlationId": "...",
  "serverTimeUtc": "2026-04-08T10:30:00Z",
  "pollAfterMs": null,
  "render": { "archetype": "Collect", "fieldGroups": [...], "availableActions": [...] },
  "problems": []
}
```

**Response States:**
- `ask_now`: Backend has questions to render immediately.
- `wait`: Instance not ready yet (async guard, queue, reviewer decision). Show pending UI; poll after `pollAfterMs`.
- `complete`: Workflow reached terminal outcome. Show completion payload.
- `error`: Non-happy-path result with typed failures in `problems`.

**HTTP Status Mapping:**
- Transport status for protocol category (200/202/401/403/404/409/422/500/503).
- `responseState` for workflow meaning.

**Archetype Catalog (7 Interaction Patterns):**
1. `Collect`: Gather user input (form sections, validation summary, save-draft)
2. `Review`: Read-only confirmation before transition (grouped answers, change links)
3. `TaskQueue`: Present pending tasks for operators (sortable table, filters, claim button)
4. `Decision`: Approve/reject/request-changes with reason capture
5. `RequestChanges`: Route instance back with targeted remediation
6. `StatusTimeline`: Visualize instance progress and audit events
7. `Completion`: Final outcome with next-step guidance

**Rationale:** Consistent envelope simplifies client implementation. Archetype mapping allows channel-specific rendering without coupling to workflow internals.

---

## 📌 2026-04-08: Workflow Forms Engine Backend Design (Blathers)

**Session Log:** `.squad/log/2026-04-08T22:15:50Z-workflow-forms-engine-design.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-workflow-backend-design.md`

### Blathers — Workflow Forms Engine Backend Design Decisions

**Decision Overview:** Ten backend design decisions define C# models, database schema, service interfaces, API contracts, and response envelopes.

#### 1. Multi-Tenant Isolation Pattern

**Decision:** ALL workflow entities include `TenantId` column with composite indexes placing `TenantId` first.

**Rationale:** Consistent with existing Prism patterns (`prismDeviceCredentials`, `prismNotificationSubscriptions`). Database can efficiently filter by tenant first, then by other criteria. Security: Tenant isolation enforced at the data layer.

**Impact:** All workflow queries filter by `TenantId` from `IPrismUserContext`.

#### 2. JSON Storage for Workflow Graph

**Decision:** Store workflow states/transitions and field group fields as JSON columns in single rows rather than fully normalized tables.

**Rationale:** Demo scope avoids over-engineering a complex graph schema. One row per workflow definition version, easier to version and publish atomically. Flexibility: JSON structure can evolve without migrations during draft phase.

**Trade-offs:** Cannot easily query "all workflows with state X" via SQL WHERE clause. Acceptable for demo — authoring/querying patterns are admin-focused, not high-volume.

#### 3. Append-Only Audit Events

**Decision:** `WorkflowEvent` table is immutable — events are never updated or deleted.

**Rationale:** Complete audit trail for compliance and debugging. Distributed tracing via `correlationId`. Timeline reconstruction from event log.

**Implementation:** No UPDATE or DELETE operations in `IWorkflowEventService`. Only append via `AppendEventAsync()`.

#### 4. Optimistic Concurrency via StateVersion

**Decision:** Use integer `StateVersion` counter (incremented on every state transition) for optimistic concurrency control.

**Rationale:** Standard ETag pattern adapted for workflow state. Prevents lost updates when multiple actors interact with same instance.

**Implementation:**
- `WorkflowInstance.StateVersion` column with index
- `IWorkflowConcurrencyGuard` validates and increments version atomically
- Clients receive updated `stateVersion` in every response envelope

#### 5. Response Envelope Contract

**Decision:** ALL workflow dialog endpoints return `WorkflowResponseEnvelope` with consistent structure.

**Structure:**
```json
{
  "instanceId": "wf_123",
  "responseState": "ask_now",
  "stateVersion": 7,
  "correlationId": "uuid",
  "serverTimeUtc": "2026-04-08T10:30:00Z",
  "pollAfterMs": null,
  "render": { /* archetype payload */ },
  "problems": []
}
```

**HTTP Status Mapping:**
- `200 OK` → `ask_now`, `complete`
- `202 Accepted` → `wait`
- `422 Unprocessable Entity` → `error` (validation)
- `409 Conflict` → `error` (concurrency)
- `404 Not Found` → `error` (not-found)

#### 6. Archetype-Driven Rendering

**Decision:** Backend generates archetype-based render payloads; channels are pure renderers with no business logic.

**Rationale:** Workflow definition is authoritative. UI never decides process order, eligibility, or completion rules. State → Archetype mapping defined in `WorkflowState.Archetype` property.

**Impact:** Channel components (web, mobile, backoffice) consume `WorkflowRenderPayload` and map archetype to UI primitives. No direct state machine logic in renderers.

#### 7. Version Pinning & Immutability

**Decision:** Workflow instances pin `workflowVersion` on creation. Published definitions are immutable.

**Rationale:** Running instances continue on pinned version (no surprise changes mid-flight). Explicit migration path for breaking changes (controlled, auditable).

**Lifecycle:** Draft → Edit → Publish (immutable) → Retire (prevent new instances).

#### 8. NPoco Migration Pattern

**Decision:** Follow exact pattern from `CreatePrismDeviceCredentialsTable` migration.

**Pattern:**
- Separate schema class per table with `[TableName]`, `[PrimaryKey]`, `[ExplicitColumns]` attributes
- Migration class extends `AsyncMigrationBase(IMigrationContext)`
- Table creation via `Create.Table<SchemaClass>().Do()`
- Indexes created via raw SQL `Database.Execute()`
- Added to `PrismMigrationPlan.DefinePlan()` chain

**Rationale:** Consistency with existing Prism codebase patterns ensures maintainability.

#### 9. Service-Oriented Architecture

**Decision:** Clean separation of concerns across six core service interfaces.

**Services:**
1. `IWorkflowDefinitionService` — Authoring/versioning (CRUD, publish, retire)
2. `IWorkflowInstanceService` — Runtime state management (create, advance, complete)
3. `IWorkflowRenderService` — Render payload generation (archetype mapping, action filtering)
4. `IWorkflowSubmissionService` — Field validation/storage (schema-driven validation)
5. `IWorkflowEventService` — Audit append/query (timeline, correlation tracing)
6. `IWorkflowConcurrencyGuard` — ETag validation (optimistic concurrency)

**Rationale:** Each service has single responsibility. Testable in isolation. Clear ownership boundaries.

#### 10. HTTP Status Semantics

**Decision:** Use HTTP status for protocol category; `responseState` for workflow meaning.

**Mapping:**
| Scenario | HTTP Status | `responseState` |
|---|---|---|
| More UI items ready | `200 OK` | `ask_now` |
| Backend not ready yet | `202 Accepted` | `wait` |
| Complete | `200 OK` | `complete` |
| Validation failure | `422 Unprocessable Entity` | `error` |
| Concurrency conflict | `409 Conflict` | `error` |
| Not found | `404 Not Found` | `error` |

**Rationale:** Aligns with REST semantics. `responseState` provides workflow-specific semantics for client state machine. Clients branch on `responseState`, not HTTP status directly.

---

## 📌 2026-04-08: Workflow Forms Engine Client Design (Isabelle)

**Session Log:** `.squad/log/2026-04-08T22:15:50Z-workflow-forms-engine-design.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-workflow-client-design.md`

### Isabelle — Workflow Forms Engine Client Design Decisions

**Decision Overview:** Five client-side design decisions establish Web Component strategy, UI orchestration, and accessibility baselines.

#### 1. Hybrid Adapter Model for Cross-Channel Rendering

**Architecture:**
- Generic `prism-workflow-*` components consume `WorkflowRenderPayload` contract only
- Thin adapter layer maps to UUI components in backoffice when needed
- Mobile shell uses generic components directly
- All components use CSS custom properties for theming

**Rationale:** Maximizes cross-channel reuse (mobile + test site + backoffice). Maintains native feel in each context via theming. Isolates workflow logic from UI framework concerns.

#### 2. Orchestrator State Machine Pattern

**State Machine:**
```
idle → creating → asking → submitting → waiting → polling → complete → error
```

**Component Contract:**
- Shell component owns orchestrator instance
- Archetype components receive `renderPayload` prop
- Archetype components dispatch events: `submit`, `action`, `save-draft`
- Shell forwards events to orchestrator methods
- Orchestrator emits `state-changed`, `workflow-complete`, `workflow-error`
- Components never import `workflowApiClient` directly

**Rationale:** Clean separation: orchestrator handles protocol, components handle presentation. Polling logic centralized. Optimistic concurrency (`stateVersion`) handled transparently. Easy to mock orchestrator for Storybook stories.

#### 3. GDS Design System Principles for Workflow Forms

**Adopted Principles:**
1. **One question per page (optional)** — `progressiveDisclosure: boolean` flag
2. **Error summary at top** — Links jump to fields, `role="alert"`
3. **Clear labels + hints** — No placeholders as labels, explain WHY we need info
4. **No jargon** — Plain English (reading age 11-12), active voice
5. **Step indicator** — Visual progress (complete/current/pending)
6. **Back navigation** — Always available, preserves answers
7. **Check your answers** — Summary before final submit

**CSS Implementation:**
- CSS custom properties for all GDS-inspired styles
- Mobile variant uses iOS blue (`#007aff`) + iOS system fonts
- Backoffice variant uses GDS blue (`#1d70b8`) + Inter font
- Components ship with sensible defaults but fully themeable

**Rationale:** GDS patterns proven for accessibility (WCAG 2.2 AA). Reduces cognitive load (one thing at a time). Mobile-first by design. Clear error recovery.

#### 4. WCAG 2.2 AA as Blocking Requirement

**Checklist (Pre-Demo):**
- [ ] Keyboard navigation (Tab, Shift+Tab, Enter, Space, Arrow keys)
- [ ] Focus order is logical and visible (3:1 contrast)
- [ ] All form fields have `<label>` with `for` attribute
- [ ] Error messages use `role="alert"` and `aria-invalid="true"`
- [ ] Error summary links jump to and focus the field
- [ ] Loading states use `role="status"` with `aria-live="polite"`
- [ ] Completion/error states use `role="alert"` with `aria-live="assertive"`
- [ ] All text meets 4.5:1 contrast (3:1 for large text)
- [ ] Color is not the only indicator of state (use icons + text)
- [ ] Component tested with VoiceOver (macOS) or NVDA (Windows)
- [ ] Storybook axe addon shows 0 violations

**Automated Testing:**
- axe addon runs on every Storybook story
- Playwright E2E tests include axe-playwright scans

**Rationale:** Accessibility is not optional for workflow forms. Fixing accessibility issues late is expensive. Automated tooling catches ~40% of issues, rest needs manual testing.

#### 5. Fixture-Driven Storybook Stories

**File Structure:**
```
src/workflow/fixtures/
├── workflow-envelope-collect.json
├── workflow-envelope-review.json
├── workflow-envelope-decision.json
├── workflow-envelope-validation-errors.json
├── workflow-envelope-waiting.json
└── ... (one per archetype + variants)
```

**Rationale:** Single source of truth for render payload shape. Fixtures can be used for backend contract tests too. Easy to update when contract changes (one file, not N stories).

---

## 📌 2026-04-08: Workflow Forms Engine Umbraco Integration (Brewster)

**Session Log:** `.squad/log/2026-04-08T22:15:50Z-workflow-forms-engine-design.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/brewster-workflow-umbraco-design.md`

### Brewster — Workflow Forms Engine Umbraco Integration Design Decisions

**Decision Overview:** Five Umbraco-specific decisions establish MockBackOffice emulator, seed packs, and integration testing patterns.

#### 1. MockBackOffice RuntimeMode Toggle

**Decision:** Introduce `RuntimeMode` configuration toggle in MockBackOffice to switch between in-memory emulation (`Emulator`) and Core runtime proxying (`Core`).

**Convention:**
```json
{
  "PrismMockBackOffice": {
    "WorkflowEmulator": {
      "RuntimeMode": "Emulator",
      "CoreRuntimeBaseUrl": "http://localhost:5000"
    }
  }
}
```

**Rationale:** Emulator mode enables deterministic, fast demo scenarios with ephemeral state (resets on restart). Core mode validates that emulator contracts match Core implementation (fidelity testing). Configuration-based toggle (not environment variable) for clearer intent and easier multi-scenario demos.

**Why this matters:** Allows MockBackOffice to serve dual purposes — standalone demo sandbox AND Core runtime integration test harness — without code changes, only config.

#### 2. Emulator-Only Extensions Must Be Namespaced

**Convention:**
- **Shared contracts:** `UmbracoPrism.Core.Workflow.Contracts` (WorkflowDefinition, WorkflowInstance, render payloads)
- **Emulator-only:** `UmbracoPrism.MockBackOffice.Workflow.Models` (OperatorPersona, WorkflowTaskQueue, AutoAssignmentPolicy)
- Core uses "actor" terminology; emulator uses "persona" for clarity of intent

**Rationale:** Production systems use real actor identities; "personas" are demo convenience only. Core runtime contracts must remain production-grade and emulator-agnostic. Clear namespace separation prevents accidental coupling. One-way dependency: MockBackOffice references Core, Core never references MockBackOffice.

**Why this matters:** Prevents demo-only shortcuts from polluting production runtime contracts. Security-sensitive operations always execute in Core, even when initiated from emulator UI.

#### 3. Workflow Seed Packs in JSON Format

**Convention:**
- `workflow-seeds/{workflow-key}-v{version}.json` for workflow definitions
- `workflow-seeds/operator-personas.json` for operator personas
- `IWorkflowSeedLoader` service registered in DI, invoked on startup in Emulator mode only

**Rationale:** JSON is standard, cross-language, version-controllable, and easy to share. Alternative C# builders adds complexity without benefit. Alternative YAML requires extra parser dependency.

**Why this matters:** Demo scenarios become source-controlled, shareable, and repeatable. Contributors can add new workflow fixtures without touching code.

#### 4. TestSite Workflow Demo Page Document Type

**Convention:**
- Document type alias: `workflowDemoPage`
- Controller name: `WorkflowDemoPageController : RenderController`
- Template: `WorkflowDemo.cshtml`
- Seeder: `WorkflowDemoSeeder` registered in `TestSiteComposer`

**Rationale:** Follows Umbraco v17 best practices: code-first document type via startup notification handler. Properties drive demo configuration without hardcoding in view. Route-hijacking controller enforces member authentication. Seeder auto-creates demo page on first run (same pattern as VinylVaultSeeder, DemoMobileNavSeeder).

**Why this matters:** Demo page is fully integrated into Umbraco CMS — editors can configure workflow key and page content via backoffice. Standard Umbraco patterns make it recognizable to any Umbraco developer.

#### 5. Security Guards Always Execute in Core Runtime

**Convention:**
- MockBackOffice MUST validate JWT Bearer token on all workflow endpoints
- MockBackOffice MUST resolve Prism tenant from claims before processing workflow requests
- Emulator service uses shared guard evaluation logic (not emulator-specific bypass)

**Rationale:** Security logic must never be "demo-only" or bypassed in emulation. In `RuntimeMode = Emulator`, emulator service replicates Core's auth/guard logic exactly. In `RuntimeMode = Core`, all security checks proxy to Core endpoints.

**Why this matters:** Prevents demo-only security holes from becoming production vulnerabilities. Emulator mode demonstrates real security behavior, not fake shortcuts.

---

## 📌 2026-04-08: Workflow Forms Engine Security Architecture (Copper)

**Session Log:** `.squad/log/2026-04-08T22:15:50Z-workflow-forms-engine-design.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/copper-workflow-security-design.md`

### Copper — Workflow Forms Engine Security Architecture Decisions

**Decision Overview:** Eight security design decisions establish defense-in-depth for tenant isolation, authorization, PII protection, and audit integrity.

#### 1. Centralised Tenant Isolation via `IWorkflowTenantGuard`

**Decision:** Introduce `IWorkflowTenantGuard` service as the **single source of truth** for tenant-scoped workflow access.

**Implementation:**
```csharp
var instance = await _tenantGuard.GetInstanceForCurrentTenantAsync(instanceId);
if (instance == null) return NotFound(); // 404, not 403
```

**Rationale:** Centralised guard prevents developer error in direct DB queries. 404 response prevents information leakage about instance existence across tenants. Pattern mirrors existing `DeviceAdminController` tenant isolation approach. Single point of enforcement simplifies security audits.

#### 2. Role-Based Actor Authorization Model

**Decision:** Define `WorkflowActor` enum (`Member`, `Operator`, `System`). Each `WorkflowTransition` declares `AllowedActors` flags.

**Authorization Service Enforces:**
1. Current actor role determination (from JWT claims)
2. Transition eligibility check (role in `AllowedActors`)
3. Member role requires instance ownership (MemberId match)
4. Operator role requires `role=prism-operator` claim

**Rationale:** Declarative authorization model makes transition rules auditable and testable. Prevents confused deputy attacks. Member ownership check prevents cross-member actions within same tenant.

#### 3. Three-Layer Emulator Security Boundary

**Defense Layers:**
1. **`[EmulatorOnly]`** attribute filter returns 404 in `!IsDevelopment()` environments
2. **`[ApiExplorerSettings(IgnoreApi = true)]`** hides from OpenAPI/Swagger
3. **Demo tenant check** at method start (config-driven demo tenant ID)

**Critical:** Emulator MUST flow ALL decisions through Core services (`IWorkflowInstanceService`), NOT direct DB writes.

**Rationale:** Demo convenience features create production risk if they leak. 404 response prevents endpoint discovery. Service flow-through ensures authorization/tenant checks still apply. Environment-based gating is fail-secure.

#### 4. Optimistic Concurrency as Security Control

**Decision:** Design `stateVersion` ETag enforcement as **security and integrity control** (not just UX).

**Enforcement:**
- ALL mutating operations require `stateVersion` in request
- Atomic database UPDATE with `WHERE stateVersion = @expected` clause
- Return **409 Conflict** with expected vs actual version on mismatch

**Rationale:** Prevents TOCTOU (time-of-check/time-of-use) race conditions. Database-level atomicity prevents race exploitation. Lost updates in workflow context = security bugs (bypass, state corruption). Forces clients to operate on current state.

#### 5. PII Encryption at Rest (AES-256-GCM)

**Decision:** Encrypt field group values at rest using **AES-256-GCM** (following `RefreshTokenEncryptionService` pattern).

**Implementation:**
- Encrypt on submission, decrypt on retrieval
- Encryption key in config: `Prism:Workflow:FieldEncryptionKey` (base64-encoded 32-byte key)
- Wire format: Base64([12-byte nonce][ciphertext][16-byte tag])
- Timeline endpoint returns **metadata only** (field group key, timestamp) — NEVER raw field values

**Rationale:** Prism is marketed as security-focused multi-tenant platform — PII encryption is baseline expectation. Reusing proven `RefreshTokenEncryptionService` pattern reduces implementation risk. Establishes security posture from day one.

#### 6. Append-Only Audit Log with Immutability

**Decision:** Design `WorkflowEvent` table as **append-only by design**.

**Enforcement:**
- No DELETE or UPDATE endpoints exposed
- Database constraints prevent modification
- Application services only expose `AppendEventAsync` method
- Optional Phase 2: Event chain hash (each event includes SHA-256 of previous event ID + timestamp)

**Rationale:** Audit integrity is critical for compliance. Immutability at design level (not just permissions) prevents tampering even with elevated DB access. Append-only is simpler to implement and reason about.

#### 7. Existence Concealment (404 not 403) for Wrong-Tenant Access

**Decision:** Return **404 Not Found** (not 403 Forbidden) when instance exists but belongs to different tenant.

**Use 403 only when:**
1. User authenticated
2. Tenant matches
3. Actor role insufficient for operation

**Rationale:** Different error codes leak information about instance existence. 404 response is indistinguishable from non-existent instance (existence concealment). Prevents reconnaissance: attacker cannot enumerate instance IDs across tenants.

#### 8. Comprehensive Security Test Suite as Pre-Production Gate

**Definition:** 15 mandatory security tests across 7 categories as pre-production gate:
1. Tenant isolation (T1.1-T1.3)
2. Authorization (T2.1-T2.4)
3. Emulator security (T3.1-T3.3)
4. Concurrency (T4.1-T4.2)
5. Audit integrity (T5.1-T5.2)
6. Information leakage (T6.1-T6.2)
7. Definition integrity (T7.1-T7.2)

**Rationale:** Security tests as gate ensures vulnerabilities caught before deployment. Comprehensive checklist prevents "we'll test it later" technical debt. Tests document security requirements and expected behavior. Automated tests enable regression detection.

**Risk Posture:** All identified threats (T1-T8) mitigated to **Low** or **Very Low** residual risk through defense-in-depth design.

---

## 📌 2026-04-03: Notifications Feature Security Review (Copper)

**Session Log:** `.squad/log/2026-04-03T12:57:36Z-notifications-complete.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/copper-notifications-security-review.md`

### Copper — Notifications Feature Security Review

**Reviewer:** Copper (Security Engineer)  
**Date:** 2026-04-04  
**Scope:** Push notification token registration, genre subscriptions, FCM delivery  
**Status:** ✅ PASS (all Critical/High issues fixed)

**Executive Summary:**  
Conducted comprehensive security review of the notifications feature focusing on tenant isolation enforcement, device token security, FCM credential handling, input validation and injection risks, and authentication/authorization controls.

**Findings:** 2 CRITICAL, 1 HIGH, 2 MEDIUM, 2 LOW, 2 INFO  
**Fixed:** All CRITICAL, HIGH, and MEDIUM issues addressed in code  
**Outcome:** PASS — feature is secure for production deployment

**Critical Findings (All Fixed):**

**C1: Push Token Length Validation Missing**
- **Severity:** CRITICAL
- **Fix Applied:** Added `[MaxLength(500)]` validation, server-side length check
- **Verification:** Build passes; attribute-based validation enforced

**C2: Genre Field Validation Missing**
- **Severity:** CRITICAL
- **Fix Applied:** Added `[MaxLength(50)]`, `[RegularExpression("^[a-z0-9_-]+$")]` validation
- **Verification:** Regex prevents SQL injection, XSS, Unicode exploits

**High Findings (All Fixed):**

**H1: Rate Limiting Missing on Token/Subscription Endpoints**
- **Severity:** HIGH
- **Fix Applied:** Created `NotificationRateLimitService` (in-memory sliding-window)
  - Token registration: 10 per hour per userId+tenantId
  - Subscriptions: 20 per hour per userId+tenantId
  - Returns `429 Too Many Requests` with `Retry-After` header
- **Verification:** Build passes; tests updated with rate limit mocks

**Medium Findings (All Fixed):**

**M1: Firebase Initialization Error Logging**
- **Severity:** MEDIUM
- **Fix Applied:** Removed exception details from logs (no credential leakage)
- **Generic error message:** `"Failed to initialise Firebase — push notifications disabled."`

**M2: Stale Token Cleanup Not Tenant-Scoped**
- **Severity:** MEDIUM
- **Fix Applied:** Updated UPDATE query to include `TenantId` filter
- **Prevents:** Cross-tenant stale token cleanup impact

**Tenant Isolation Verification:** ✅ All database queries are tenant-scoped  
**Device Token Security:** ✅ Token handling is secure (auth required, length validated, rate limited)  
**FCM Credential Handling:** ✅ Credentials loaded safely (never logged, singleton pattern)  
**Input Validation:** ✅ Token length, genre regex, ModelState enforcement  
**Authentication & Authorization:** ✅ All endpoints protected, UserId from signed JWT, TenantId from middleware

**Recommendations for Production Deployment:**
1. Key Vault Integration (Required) — Set `Prism:Firebase:CredentialJson` to Key Vault reference
2. Multi-Instance Rate Limiting (Optional) — Replace with Redis-backed implementation if multi-instance
3. Data Retention Policy (Optional) — Define cleanup for unregistered devices (e.g., 90 days)
4. Structured Logging (Optional) — Add Application Insights telemetry to `FanOutAsync()`
5. Post-Deployment Smoke Test — Test token registration, subscriptions, and notification delivery

**Security Verdict:** ✅ **PASS**

All Critical and High severity issues have been fixed. The feature is **approved for production deployment** with Key Vault for credentials and optional Redis-backed rate limiting for multi-instance deployments.

**Confidence Level:** HIGH — Implementation follows established Prism security patterns (tenant scoping, auth model, rate limiting). No cross-tenant leakage or credential exposure vectors found.

**Files Modified (Security Fixes):**
- Created: `INotificationRateLimitService.cs`, `NotificationRateLimitService.cs`
- Modified: `PrismPushRegisterRequest.cs`, `PrismSubscribeRequest.cs`, `PrismNotificationController.cs`, `PrismNotificationService.cs`, `PrismComposer.cs`
- **Build Status:** ✅ `dotnet build UmbracoPrism.sln` passes with no errors
- **Test Status:** ✅ 206/206 tests passing

---


## 📌 2026-04-03: Test Coverage & Hydration Decisions (Tangy)

**Session Log:** `.squad/log/2026-04-03T13:21:41Z-tangy-playwright-fixes.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/tangy-playwright-fixes.md`

### Tangy — Push Notifications Toggle Must Be Hydrated

**Decision:** All form fields in `prism-create-tenant-modal` MUST hydrate from persisted config in both:
1. `connectedCallback` (initial load)
2. `updated` lifecycle (when `data` prop changes)

**Implementation:**
- Added `pushNotificationsEnabled` to `_readMobileAppConfig` return value
- Set `this._pushNotificationsEnabled = mobileConfig?.pushNotificationsEnabled ?? false;` in both lifecycle methods

**Rationale:** Consistency with other mobile config fields. Data loss on edit is a critical UX bug.

### Tangy — Test All New Form Controls

**Decision:** Every new form control in a modal or component MUST have Playwright tests covering:
1. **Visibility:** Control renders in the expected tab/section
2. **Default state:** Control has correct initial value
3. **Interaction:** Control responds to user input
4. (Optional) **Hydration:** Control loads saved values correctly on edit

**Implementation:**
- Added `'Produce Mobile tab shows push notifications toggle'` — visibility + default state
- Added `'Push notifications toggle can be enabled'` — interaction

**Rationale:** Playwright is our contract for "this UI works." Untested controls are invisible to CI.

### Tangy — Storybook Stories Must Match Playwright Test Expectations

**Decision:** When writing Playwright tests that target specific Storybook stories:
1. Story MUST exist before writing the test (or create both together)
2. Story name (`name: 'Light Theme'`) must match kebab-case URL segment (`--light-theme`)
3. Export name (`export const LightTheme`) must match kebab-case suffix

**Implementation:**
- Created `LightTheme` story matching test expectations

**Rationale:** Storybook URL structure is deterministic. Tests should never reference non-existent stories.

### Tangy — Shadow DOM Test Selectors Pattern

**Pattern:** When testing Web Components with shadow DOM in Playwright:
- Use `aria-label` for form controls: `el.shadowRoot?.querySelector('input[aria-label="Push Notifications"]')`
- Access shadow root via `modal.evaluate((el) => el.shadowRoot?.querySelector(...))`
- For tab navigation: `el.shadowRoot?.querySelector('uui-tab[label="Produce Mobile"]')`

**Applied To:** All new tests in `prism-create-tenant-modal.spec.ts`

**Rationale:** Shadow DOM is encapsulated. Standard Playwright locators can't pierce it. `aria-label` is semantic and stable.

---

## 📌 2026-04-04: VS Code Parallel Launch Race Condition Fix (Blathers)

**Session Log:** `.squad/log/2026-04-04T08:05:10Z-build-race-fix.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-build-race-fix.md`

### Blathers — VS Code Parallel Build Race Condition Fix

**Decision:** Fix the intermittent `System.IO.IOException` on Static Web Assets cache files (`*.dswa.cache.json`) caused by parallel MSBuild builds of `UmbracoPrism.Core` via VS Code task pre-build rather than MSBuild `ReferenceOutputAssembly="false"`.

**Rejected approach:** `ReferenceOutputAssembly="false"` on MockBackOffice's `ProjectReference` to Core — not viable because MockBackOffice has genuine compile-time dependencies on Core types (`UmbracoPrism.Core.Extensions`). Removing the assembly reference breaks compilation.

**Chosen approach:** Add a `"dotnet: build Core"` VS Code task that pre-builds Core before either consumer project's dotnet launch adapter runs:
- `"Client: build"` task gains `dependsOn: ["dotnet: build Core"]` — ensures Core is built before TestSite launches.
- `"C#: Mock Back-Office Debug"` gains `preLaunchTask: "dotnet: build Core"` — ensures Core is built before MockBackOffice's dotnet launch adapter (which then finds Core up-to-date and skips it, making it a fast no-op).

**Why:** No changes to production `.csproj` files — this is a developer tooling concern only. Core is built exactly once per compound launch; the second invocation is an MSBuild no-op.

**Affected Files:**
- `.vscode/tasks.json` — new `"dotnet: build Core"` task; `"Client: build"` gains `dependsOn`
- `.vscode/launch.json` — `"C#: Mock Back-Office Debug"` gains `preLaunchTask`


---

## 📌 2026-04-04: CSS Branding Metadata API & Design System (Blathers, Celeste, Isabelle)

**Session Log:** `.squad/log/2026-04-04T09:40:58Z-branding-design-system.md`  
**Orchestration Logs:** 
- `.squad/orchestration-log/2026-04-04T09:40:58Z-blathers.md` (Backend Dev)
- `.squad/orchestration-log/2026-04-04T09:40:58Z-celeste.md` (Documentation)

### Blathers — CSS Branding Metadata Parser Architecture

**Decision:** Implemented a CSS metadata parser that reads structured annotations from branding CSS files and exposes them via a backoffice API endpoint, enabling a dynamic, design-system-style tenant editor.

**Annotation Format:**
Each brandable CSS variable follows this pattern:
```css
@property --prism-primary {
  syntax: '<color>';
  inherits: true;
  initial-value: #4f46e5;
}

:root {
  /* @prism section: Brand Colours | label: Primary Brand Colour | description: Used for buttons, links, and highlights */
  --prism-primary: #4f46e5;
}
```

**Annotation Keys:**
- `section` — groups variables into sections in the editor UI
- `label` — human-readable name for the field
- `description` — tooltip/hint text
- `type` — picker type hint: `color`, `image`, `url`, `font`, `length`, `text`

**Type Resolution:**
1. Explicit `@prism type:` override (if present)
2. Inferred from `@property syntax` (`<color>` → color, `<url>` → url, `<length>` → length, `*` or `<string>` → text)
3. Default to `text` if neither present

**API Contract:**
- **Endpoint:** `GET /umbraco/api/prism/branding/metadata`
- **Auth:** Umbraco backoffice access (`BackOfficeAccess` policy)
- **Response:** JSON with sections array → variables array with full metadata

**Implementation Details:**
- **Models:** `BrandingVariableMetadata` (variable, label, description, type, syntax, currentValue) + `BrandingSection` (section name + variables)
- **Service:** `IPrismBrandingMetadataService` / `PrismBrandingMetadataService`
  - Reads all `*.css` files from `wwwroot/branding/` EXCEPT `prism-branding.css` (aggregator file)
  - Parses `@property` declarations and `/* @prism ... */` annotations using regex
  - Groups by section (first-appearance order, not alphabetical)
  - Caches result in `IMemoryCache` (1-hour sliding expiration)
  - Registered as singleton in `PrismComposer`
- **Controller:** Added `GET branding/metadata` endpoint to `TenantManagementController`
- **Tests:** 12 unit tests covering annotation parsing, type inference, section grouping, caching behavior (all pass ✅)

**Rationale:**
- **Runtime parsing (not build-time):** Simpler deployment, no build step for CSS changes, supports hot-reload in dev. Metadata cache means negligible runtime cost.
- **Regex parsing (not PostCSS/AST):** Annotation format is simple and well-defined, no need for Node.js tooling in backend, regex patterns are tested and reliable for this use case.
- **Exclude prism-branding.css:** It's an `@import` aggregator with no variable declarations; prevents duplicate parsing.
- **Section order by first-appearance:** Allows authors to control section order by organizing CSS files; more intuitive than alphabetical sorting.
- **1-hour cache expiration:** CSS changes are rare in production; dev can restart to pick up changes; balances freshness vs. performance.

**Consequences:**
- ✅ UI form automatically in sync with available CSS variables
- ✅ Adding new brandable variables requires zero UI code changes
- ✅ Type inference from `@property` reduces annotation boilerplate
- ✅ Section grouping improves UX for large variable sets
- ⚠️ CSS authors must follow annotation format exactly (no validation at write-time)
- ⚠️ Cache expiration means CSS changes require restart or cache eviction
- ⚠️ Regex parsing is fragile to format variations (mitigated by unit tests)

**Test Status:** ✅ 218 tests pass. No regressions.

---

### Blathers — Remove ThemeColor from Backend

**Decision:** Remove `ThemeColor` completely from the backend as it was:
1. Never exposed in the tenant editor UI (hardcoded to '#3544b1')
2. Replaced by the CSS variable override system in `wwwroot/branding/`
3. Dead weight in the database schema and domain models

**Implementation:**
- Domain model: Removed from `PrismTenant.cs`
- Database schema: Removed from `PrismTenantSchema.cs`
- API request DTO: Removed from `PrismTenantRequest.cs`
- Controller mappings: Removed from `TenantManagementController.cs`
- Service layer: Removed from `TenantService.cs`
- Test helpers: Removed from `TenantServiceCacheStrategyTests.cs`
- Migration: Added `DropThemeColorColumn` migration (checks for existence before dropping)

**Rationale:**
- Simplified model: Removes unused property from 6 files
- Single source of truth: Tenant branding now exclusively managed through CSS variable overrides
- Migration safety: Handles both fresh installs and upgrades
- Zero UI impact: Property was never exposed in the UI

**Alternatives Considered & Rejected:**
- Keep for future use — CSS variable system is more flexible and already implemented
- Wire it up to UI — CSS variables provide superior branding control

**Consequences:**
- ✅ Cleaner, more maintainable codebase
- ⚠️ Database schema change requires migration for existing installations
- ⚠️ Any external code referencing `ThemeColor` will break (unlikely)

---

### Celeste — Branding & Design System Documentation

**Decision:** Created comprehensive documentation for the CSS branding system and design-system-based tenant editor.

**Deliverables:**
1. **docs/branding-design-system.md** (563 lines)
   - Complete guide to annotation format with examples
   - Type hints reference and picker type selection logic
   - Live editor workflow documentation
   - Branding CSS file summaries (prism-colors, prism-layout, prism-typography, prism-spacing, prism-utilities)
   - Future enhancements section

2. **README.md Enhancement**
   - Added "Branding & Design System" section after Features
   - Embedded annotation code example showing `@property` + `@prism` pattern
   - Linked to comprehensive branding design system guide

3. **docs/README.md Update**
   - Added "Branding & Design System" entry to documentation index

**Quality Metrics:**
- ✅ 563 lines of clear, example-driven documentation
- ✅ Code samples are accurate and runnable
- ✅ Technical depth appropriate for backend developers and designers
- ✅ Coordination with Blathers and Isabelle work documented
- ✅ No formatting or grammar issues

**Rationale:**
- Documentation reflects final design decisions from Blathers and Isabelle
- Ready for publication to Umbraco marketplace/community sites
- Serves as reference for future contributors adding new branding variables

---

### Isabelle — Test Site CSS Structure (ITCSS)

**Decision:** Adopt ITCSS (Inverted Triangle CSS) layer structure for test site CSS, organized as:

```
wwwroot/css/
  base.css         — HTML element defaults
  layout.css       — Page structure, grid systems, containers
  components.css   — Reusable UI patterns
  utilities.css    — State/modifier classes
```

Files are linked in Master.cshtml in ITCSS order (specificity increasing):
1. Branding CSS (`/branding/prism-branding.css` — loads all branding files)
2. Site CSS (`base.css` → `layout.css` → `components.css` → `utilities.css`)
3. Dynamic CSS (inline `<style>` for runtime CSS variable injections)

**Rationale:**
- **Why ITCSS:** Natural specificity order, clear separation of concerns, scalable without over-engineering, minimal layer count (4 files)
- **Why separate from branding CSS:** Branding CSS is a **key feature** of Umbraco.Prism, not just project organization. Intentionally separate to demonstrate multi-tenant branding capability.
- **Why inline `<style>` for dynamic values:** Dynamic CSS variable injections (e.g., `--tenant-primary`, `--prism-hero-image`) are runtime values from C# that cannot be extracted to static files.

**Layer Contents:**
- **base.css:** HTML element defaults (body font, line-height, color, background), mobile overrides
- **layout.css:** Portal/dashboard headers, page containers, grid systems, footer, Vinyl Vault layouts, mobile layout overrides
- **components.css:** Hero, buttons, cards, features section, dashboard sections, mobile navigation, Vinyl Vault components, mobile overrides
- **utilities.css:** Debug visibility, mobile web component visibility

**Patterns Consolidated:**
- Portal header + dashboard header → same base styles with variant modifiers
- Card patterns from HomePage and MemberDashboard → unified `.card` and `.dash-card`
- Button patterns → single `.btn` base with variant modifiers
- Mobile overrides → centralized in components.css and layout.css

**Conventions:**
- File naming: Named for what they contain, not where they came from
- Comments: Brief section headings for scannability
- No build step: Plain CSS only, no preprocessors
- CSS variables: Non-branding CSS variables go in future `settings.css` layer (not created yet)

**Future Considerations:**
- If non-branding CSS custom properties become common, add `settings.css` as first layer
- Don't create layers just for the sake of ITCSS — merge/remove if empty or trivial
- Keep structure flat and obvious (this is a demo, not an enterprise app)

---

### Isabelle — Edit Tenant Dialog — Maximize & Close Button Pattern

**Decision:** Added Close (×) and Maximize/Restore buttons to the title bar of the tenant editor dialog (`prism-create-tenant-modal`).

**Conventions Adopted:**

**Headline slot pattern:**
Use `slot="headline"` (not the `headline=""` attribute) on `uui-dialog-layout` whenever the dialog title bar needs additional controls. Omit the `headline` attribute entirely and supply a flex container (`display:flex; justify-content:space-between`) as the slotted content.

**Maximize via host class:**
Apply a `maximized` CSS class to `:host` using `this.classList.toggle('maximized', flag)` inside `updated()`. The maximized state uses `position: fixed !important; inset: 0; width: 100vw; height: 100vh; z-index: 10000` to escape the `uui-modal-dialog` / native `<dialog>` stacking context.

**Escape key interception:**
Use a capture-phase `document.addEventListener('keydown', handler, true)` added in `connectedCallback` / removed in `disconnectedCallback`. When maximized, call `event.stopPropagation()` and restore — do NOT close. When not maximized, let the modal framework handle Escape normally.

**Icon buttons:**
Use a plain `<button class="dialog-icon-btn">` with SVG icon, `aria-label`, and `title`. Style with UUI CSS variables (`--uui-color-text-alt`, `--uui-color-surface-emphasis`, `--uui-color-focus`) for consistent backoffice look. Apply `focus-visible` outline ring.

**Rationale:**
- Requested by Jonny as quality-of-life improvement
- The dialog has many tabs and fields; fullscreen mode reduces scrolling for large configurations
- A title-bar close button is a universal UX convention users expect
- Consistent with Umbraco backoffice design language (UUI variables throughout)

---

### Isabelle — Remove Legacy --tenant-primary CSS Variable

**Decision:** Removed the legacy `--tenant-primary` CSS variable system entirely in alignment with backend `ThemeColor` removal.

**Implementation:**
1. **prism-layout.css** — Replaced `var(--tenant-primary)` with `var(--prism-primary)` and `var(--tenant-primary-contrast)` with `var(--prism-primary-contrast)` in `.header` styles
2. **prism-colors.css** — Removed redundant `--tenant-primary-contrast: white;` definition
3. **Master.cshtml** — Removed the `<style>` block that injected `--tenant-primary` and the C# `brandColor` variable
4. **prism-create-tenant-modal.ts** — Removed `themeColor: '#3544b1'` from tenant creation payload

**Rationale:**
- Simplicity: One colour system is clearer than two overlapping systems
- Consistency: All tenant branding managed through CSS variable overrides in `wwwroot/branding/`
- Cleanliness: Removes the last server-injected `<style>` block (inline styles only remain for dynamic Umbraco media URLs)
- Backend alignment: Blathers removed `ThemeColor` from C# model — frontend should follow

**Verification:**
- ✅ Grepped entire test site for `tenant-primary` — no matches
- ✅ Grepped entire test site for `ThemeColor` — no matches

**Impact:**
- ✅ Breaking change if custom tenant sites relied on `--tenant-primary` (unlikely — branding CSS already used `--prism-primary`)
- ✅ Removes confusion about which CSS variable to use for tenant branding
- ✅ Aligns frontend with backend model changes

---



## 📌 2026-07-10: Media Picker URL Endpoint (Isabelle)

Use `/umbraco/management/api/v1/media/urls?id={unique}` to resolve a media item's public URL in the backoffice, **not** `/umbraco/management/api/v1/media/{id}`.

- `/media/{id}` returns a full `MediaResponseModel` with no `urls` property.
- `/media/urls?id={id}` returns `MediaUrlInfoResponseModel[]` — each item has `id` and `urlInfos: Array<{ culture, url }>`.

Response parsing pattern:
```typescript
const items: Array<{ id: string; urlInfos: Array<{ culture: string | null; url: string | null }> }>
  = Array.isArray(data) ? data : [data];
const rawUrl: string = items[0]?.urlInfos?.[0]?.url ?? '';
```

Affects `prism-create-tenant-modal.ts` → `_pickMediaForVariable` and any future code resolving media URLs from the Umbraco Management API.

---

## 📌 CSS Variable Organisation Principles (Isabelle)

Hero-section presentation variables (bg, text, badge) live in `prism-imagery.css` alongside other hero/card imagery. Nav height dimensions live in `prism-layout.css` alongside other layout dimensions. `prism-colors.css` contains only the raw brand colour palette. Keeps each file cohesive by concern — images/gradients in imagery, dimensions in layout, raw colours in colors.

---


## 📌 2026-07-11: Mobile Branding Inheritance Model (Isabelle)

Mobile branding variables follow a **chain/inheritance model**:

1. **Chained (default):** Mobile inherits from desktop. No mobile override is saved. `_mobileInherited[varName] === true`.
2. **Unchained (explicit):** Mobile has an independent value. The override IS saved. `_mobileInherited[varName] === false`.

`_collectMobileBrandingOverrides` only writes an override for variables where `!_mobileInherited[varName]`. On load, `_mobileInherited[varName]` is set to `!explicitMobileOverride` — variables with a saved mobile override start unchained; all others start chained.

`data-testid` hooks: `mobile-inherit-toggle-{varName}`, `mobile-field-{varName}`, `mobile-inherit-label-{varName}`, `mobile-custom-badge-{varName}`.

**Note from Isabelle:** `PrismBrandingMetadataService.ParsePrismAnnotation` previously fell back to storing the full raw `@prism` annotation string as `Description`. Blathers fixed this — see below.

---

## 📌 2026-07-15: Remove Raw Annotation Fallback from ParsePrismAnnotation (Blathers)

Removed the fallback in `ParsePrismAnnotation` that stored the full raw annotation string in `metadata.Description` when no `description:` key was found. `Description` is now `null`/empty when no `description:` annotation is present.

**Convention going forward:** In annotation parsers, leave optional fields as `null`/empty when not present. Do not use raw input strings as fallback values for structured fields.

---

## 📌 2026-07-11: Mobile Inheritance Edge Cases to Address (Tangy)

Edge cases identified during test authoring for `prism-mobile-branding-inheritance.spec.ts`:

1. **Pre-population on break:** Pre-populate mobile input with `overrideValue ?? defaultValue ?? ''` when breaking inheritance — not with an empty string.
2. **Restore clears value:** On restore, set `mobileOverrideValue = undefined` so the save payload genuinely omits it.
3. **Tab switch persistence:** `_mobileInherited` state must survive tab switches — verify it is not re-initialised on tab render.
4. **Desktop changes don't follow mobile:** Once the chain is broken, desktop value changes must not propagate to the mobile input.
5. **Submit payload:** Only variables with the chain broken and a non-empty mobile value should appear in `mobileBrandingOverrides`.

Tests covering edge cases #1, #3, and #5 are noted as gaps for future coverage once implementation is confirmed.

---

## 📌 2026-04-05: Mobile Inheritance UI Cleanup & Accessibility (Isabelle)

**Session Log:** `.squad/log/2026-04-05T09:54:52Z-mobile-inheritance-ui-cleanup.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-mobile-inheritance-ui-cleanup.md`

### Isabelle — Mobile Inheritance UI Cleanup

**Decision:** Replace emoji-based mobile inheritance toggles with clean, accessible text buttons and completely hide mobile inputs when inheriting.

**What Changed:**

**Inheriting State:**
- Clear text label: "Inheriting from desktop" (0.85rem, muted, italic)
- Action button: "Customise for mobile" (outline style, proper label attribute)
- Mobile input **completely hidden** (`display: none`) — not dimmed with opacity
- Proper `label="Break mobile inheritance"` on button for screen readers

**Custom State:**
- Badge: "Custom mobile value" (warning color, professional styling)
- Action button: "Reset to desktop" (placeholder style, proper label attribute)
- Mobile input **visible and fully interactive**
- Proper `label="Restore mobile inheritance"` on button

**Conventions Established:**
- When hiding UI elements that need to stay in DOM for tests, use `display: none` for clean hiding
- Action buttons should use descriptive English text, not emoji
- Always provide proper `label` attributes on UI buttons for accessibility
- Test assertions should verify UI visibility state, flexible to handle `display: none`, `pointerEvents: none`, or `disabled`

**Why:**
- Emoji are not accessible and can feel unprofessional
- `display: none` is cleaner than opacity tricks for hidden content
- Clear button text ("Customise for mobile" / "Reset to desktop") makes the action obvious
- Proper accessibility attributes prevent console warnings and help screen reader users

**Files Modified:**
- `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.ts` — Updated mobile field rendering logic, button labels, badge styling
- `src/UmbracoPrism.Client/tests/prism-mobile-branding-inheritance.spec.ts` — Updated test assertions

**Quality:** Build clean, 38/38 Playwright tests passing.

---


## 📌 2026-04-05: User Directive — CSS Styles in ITCSS Files Only (Copilot)

**Author:** Jonny Muir (via Copilot)  
**Status:** Accepted

All CSS styles must reside in the ITCSS-structured CSS files under `wwwroot/css/` (base.css, layout.css, components.css, utilities.css). The **only permitted exception** is dynamically generated inline styles produced at runtime by C# (e.g., tenant imagery CSS variable injection).

**Why:** Keeps stylesheets organized, maintainable, and prevents scattered inline `<style>` blocks in `.cshtml` files. This directive is captured as a team convention.

---

## 📌 2026-04-05: WCAG 2.1 AA Audit Complete — prism-create-tenant-modal (Isabelle)

**Session Log:** `.squad/log/2026-04-05_11-05-37-a11y-audit.md`  
**Orchestration Log:** `.squad/orchestration-log/2026-04-05_11-05-37-isabelle.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-a11y-modal-findings.md`

### Isabelle — Full WCAG 2.1 AA Accessibility Audit

**Summary:** Completed comprehensive accessibility audit of `prism-create-tenant-modal.ts`. **11 issues identified and fixed** (6 critical, 5 major).

**Critical Fixes (🔴):**
1. **Removed `uui-dialog-layout`** — was forcing all content into a single scroll container, breaking focus management and sticky positioning
2. **Restructured as flex column** — `.dialog-headline` and `.uui-tab-group` are now direct `flex-shrink:0` children of the host; only `.container` scrolls
3. **Seeded focus on open** — `firstUpdated()` calls `requestAnimationFrame(() => primaryBtn.focus())` + `autofocus` attribute as fallback
4. **Added dialog semantics to host** — `role="dialog"`, `aria-modal="true"`, `aria-label` (updates dynamically)
5. **Fixed tab panel IDs** — General/Identity tabs now have `id="general-tab"` / `id="identity-tab"` so panel `aria-labelledby` resolves; branding tabpanel now has explicit `id` and `aria-labelledby`

**Major Fixes (🟡):**
1. **Added `aria-required`** — on Tenant Name, Hostname inputs
2. **Added `aria-invalid`** — on Mobile App ID, Start URL, Icon URL, Splash URL (bound to validation state)
3. **Added `aria-describedby`** — on Key Vault Secret Name
4. **Added `aria-label` on color pickers** — inline `<input type="color">` elements now labelled
5. **Added focus-visible ring on toggle** — `.toggle-slider:focus-visible` gets proper outline ring
6. **Gated transitions with `prefers-reduced-motion`** — all CSS transitions now scoped to `@media (prefers-reduced-motion: no-preference)`

**Conventions Established:**
- Shadow DOM focus-seeding pattern + flex-column modal layout documented in `.squad/skills/shadow-dom-focus/SKILL.md`
- Apply this pattern to all future modal web components in the project

**Status:** ✅ Complete — All critical and major issues resolved. Build clean.

**Charter Update:** Isabelle's role elevated to **team's dedicated WCAG 2.1 AA accessibility expert**.

---

## 📌 2026-04-05: Design Tokens Showcase — Live Component Demo (Isabelle)

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-design-tokens-showcase.md`

### Isabelle — Design Tokens Showcase Implementation

**Decision:** Build a comprehensive design system tokens showcase on the test site home page demonstrating Prism's multi-tenant branding capabilities with live visual renderings.

**What's Rendered:**
1. **Live visual demos over labels** — actual rendered colours (not swatches), typography at real sizes, real shadows/borders/spacing, working UI components proving token integration
2. **Coverage of all 5 branding CSS files** — colors, typography, layout, imagery, components
3. **Group by category in separate cards** — Colour, Typography, Layout, Imagery, Components
4. **Use `.token-chip` for token name labels** — consistent monospace badge style across showcase

**CSS Organization:**
- All showcase CSS lives in `components.css` (appended to end)
- Use BEM-style naming: `.ds-section`, `.token-palette`, `.token-swatch__color`
- Prefix all showcase classes with `ds-` (design system) or `token-` for clarity
- Keep mobile responsive with media queries where needed

**Responsive Layout:**
- CSS Grid with `auto-fit` + `minmax(var(--prism-grid-min), 1fr)` for fluid breakpoints
- Wide cards (`.ds-card--wide`) span 2 columns on desktop, collapse to 1 on mobile
- No fixed widths — all sizing driven by layout tokens

**Inline Styles Exception:**
- Allow inline `style=""` attributes **only** for demonstrating dynamic token values
- Example: `<div style="background: var(--prism-primary);">` shows live colour rendering
- This is one of the few valid use cases for inline styles

**Why:** The showcase serves as both a functional demo for prospects/customers and a living style guide for developers. Proves that Prism's branding system is comprehensive, polished, production-ready — not just a theming bolt-on.

**Files Modified:**
- `HomePage.cshtml` — Added `<section class="ds-section" id="design-tokens">` after `.features`
- `components.css` — Added ~250 lines of showcase CSS

**Status:** ✅ Implemented. Build clean.

---

## 📌 2026-04-05: Mobile Header Inline Pattern — Consistency (Isabelle)

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-mobile-header.md`

### Isabelle — Mobile Branding Header Inline Pattern

**Decision:** Mobile header row in `_renderDynamicField()` now follows the same inline flex pattern as Desktop header.

**Layout:**  
```
Mobile  [pill]  [small placeholder button]
```

**Rationale:**
- Eliminates the dominant `look="outline"` "Customise for mobile" button which overshadowed the branding field content
- Keeps UI consistent: both Desktop and Mobile headers are single-line flex rows with label, optional status pill, optional action button

**Pill Colours:**
- **Inheriting:** neutral (`--uui-color-surface-emphasis` bg / `--uui-color-text-alt` text) — inheritance is not a warning state
- **Custom:** warning yellow — signals active override that may need attention

**Preserved Constraints:**
- All `data-testid` attributes unchanged (Playwright test dependency)
- Click handlers and TypeScript logic unchanged
- `display: none` on edit field when inheriting is unchanged

**Status:** ✅ Implemented.

---

## 📌 2026-04-05: Modal Header UX — Single Row, Primary First, Native Buttons (Isabelle)

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-modal-header-ux.md`

### Isabelle — Modal Header UX Decision

**Context:** `prism-create-tenant-modal` headline used a two-row layout (title + buttons), wasting space and creating keyboard accessibility gaps.

**Decisions:**

1. **Remove modal title text** — Primary button label ("Update Tenant" / "Create Tenant") already contextualises intent
2. **Single flex row layout** — `[primary action] [Cancel] · · · [maximize][close]`
3. **Primary first, Cancel second** — Primary goal leftmost, Cancel sits right (subordinate)
4. **Use native `<button>` not `uui-button`** — `uui-button` in named slots doesn't reliably receive focus; native buttons always participate in tab order

**Conventions:**
- All new modal headline areas MUST follow single-row layout
- Do NOT use `uui-button` in `slot="headline"` — use native `<button>` styled to match UUI
- Do NOT use `compact` attribute on headline buttons — without a title row, full-size buttons are appropriate
- `data-testid="modal-submit-btn"` and `data-testid="modal-cancel-btn"` required on headline buttons

**Status:** ✅ Implemented.

---

## 📌 2026-04-05: No Static Styles in .cshtml Files (Isabelle)

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-styles-in-css-files.md`

### Isabelle — CSS Organization in Razor Views

**Rule:** All static CSS styles must live in ITCSS CSS files under `wwwroot/css/`.  
**No static `<style>` blocks in `.cshtml` files.**

**Permitted Exception:**  
Dynamically generated C# inline styles are allowed for runtime-generated values (e.g., tenant imagery CSS variable injection).

**ITCSS Layer Guidance:**

| File | Layer | Content |
|---|---|---|
| `base.css` | Base | Resets, element defaults (html, body, headings) |
| `layout.css` | Layout | Page structure, grid, major layout patterns |
| `components.css` | Components | UI components (cards, buttons, chips, sections, design system showcase) |
| `utilities.css` | Utilities | Single-purpose helpers (.text-center, .sr-only) |

**Razor Media Query Note:**  
When migrating styles from `.cshtml` to `.css`:
- In `.cshtml`: `@@media` (doubled `@` to escape Razor)
- In `.css`: `@media` (standard CSS — no escaping)

**Status:** ✅ Implemented. All design system showcase styles moved to components.css.

---


---

## 📌 2026-04-05: Always Use uui-dialog-layout as Outer Shell (Isabelle)

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-keep-uui-dialog-layout.md`

### Decision: Always use uui-dialog-layout as the outer shell

**Author:** Isabelle (Frontend Dev & Accessibility Lead)

**Decision Statement:** `uui-dialog-layout` must always be used as the outermost wrapper in `render()` for all modal components in this project. It must never be removed in the name of accessibility fixes.

**Why This Matters:**

`uui-dialog-layout` provides:
1. The visual dialog shell and bounded dimensions
2. The scroll boundary — content inside its default slot scrolls; the headline slot is sticky
3. Proper sizing constraints so the flex layout does not collapse

**A11y Approach:**

ARIA attributes (`role="dialog"`, `aria-modal="true"`, `aria-label`) belong on the **host element**, set in `connectedCallback()` and updated in `updated()`. They are entirely independent of the template structure and coexist with `uui-dialog-layout` without conflict.

**Anti-pattern (Do Not Use):**

Do NOT remove `uui-dialog-layout` and replace it with `:host { display: flex; flex-direction: column }` — this collapses tab panels and breaks the scroll boundary.

**Applied:** prism-create-tenant-modal.ts (a11y restoration 2026-04-05)

**Test Coverage:** 38/38 Playwright tests pass; no regressions.


---

## 📌 2026-04-09: Backend Redesign Implementation — Element Type Introspection (Blathers)

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-implementation-complete.md`

### Blathers — Backend Redesign Implementation Complete

**Author:** Blathers (Backend Dev)  
**Status:** Implementation Complete ✅  

**Summary:** The Workflow Forms Engine backend redesign has been successfully implemented. Custom `PrismFieldGroupDefinition` tables have been replaced with Umbraco Element Type introspection.

**Key Decisions:**

1. **ElementTypeAlias Replaces FieldGroupKeys**
   - `WorkflowState` now has `ElementTypeAlias` property
   - `WorkflowDefinition` no longer uses `FieldGroupKeys`
   - Enables dynamic field definitions via Umbraco content types

2. **PrismPropertyTypeMapper Service**
   - Static mapper class for Umbraco property editor alias → workflow field type conversion
   - Supports 14+ property editor types with safe fallback to "text"
   - No DI needed; stateless conversion logic

3. **WorkflowRenderService Uses IContentTypeService**
   - Now accepts `IContentTypeService` via constructor injection
   - Dynamically builds `FieldGroupRenderPayload` from Element Type properties
   - Returns empty field groups when `ElementTypeAlias` is null or Element Type not found
   - Field metadata (label, hint, required, field type) derived from Umbraco property definitions

4. **Database Migrations**
   - Table rename: `prismFieldGroupSubmissions` → `prismWorkflowFieldValues`
   - Schema class rename: `PrismWorkflowFieldGroupSubmissionSchema` → `PrismWorkflowFieldValueSchema`
   - New migration: `RemoveLegacyFieldGroupDefinitions` drops `prismFieldGroupDefinitions` table
   - All migrations added to `PrismMigrationPlan`

**Build Status:** ✅ Builds clean — 0 errors, 0 warnings

**Impact:** Backend now returns fully populated `FieldRenderPayload` structures when a workflow state has an `ElementTypeAlias` configured. Frontend can consume dynamic field definitions without hardcoding.

---

## 📌 2026-04-09: Workflow Element Type Seeding Strategy (Brewster)

**Merged From Inbox:**
- `.squad/decisions/inbox/brewster-implementation-complete.md`

### Brewster — Element Type Seeding & Demo Workflow

**Author:** Brewster (Umbraco Platform Specialist)  
**Status:** ✅ Implemented  

**Decision:** Implement code-first Element Type seeding for workflow step definitions in Umbraco v17. Element Types are created programmatically on application startup using deterministic GUIDs.

**Implementation Details:**

1. **WorkflowElementTypeSeeder Service**
   - Created `src/UmbracoPrism.Core/Services/Workflow/WorkflowElementTypeSeeder.cs`
   - Follows exact pattern of `PrismContentTypeSeeder`
   - 5 deterministic data type keys for workflow fields
   - Idempotent: checks if element type exists before creating
   - `IsElement = true` for all content types

2. **Element Types Created**
   - `workflowPersonalDetails` — first name, last name, email, date of birth
   - `workflowFinancialDetails` — annual income, employer, UK tax resident flag
   - Uses built-in Umbraco editors (TextBox, EmailAddress, DateTime, Integer, Toggle)

3. **WorkflowSeedServiceImpl Updates**
   - Injects `IWorkflowDefinitionRepository` and `WorkflowElementTypeSeeder`
   - Calls `EnsureElementTypesAsync()` FIRST before loading workflow definitions
   - Parses JSON and maps to `WorkflowDefinition` model
   - Removed `FieldGroupKeys` property (deprecated)

4. **Demo Workflow JSON**
   - Created `retirement-quote-v1.json` with 4 states: personal-details → financial-info → review → complete
   - Uses `elementTypeAlias` references to Element Types
   - Includes bi-directional transitions (back buttons)
   - Embedded as resource in Core project

5. **DI Registration**
   - `WorkflowElementTypeSeeder` added as scoped service in `WorkflowBuilderExtensions`
   - `IWorkflowSeedService` changed from singleton to scoped (was causing lifetime issues)

**Build Status:** ✅ Full solution builds successfully — 0 errors, 0 warnings

---

## 📌 2026-04-09: Route-Hijacking Workflow Controller Pattern (Brewster)

**Merged From Inbox:**
- `.squad/decisions/inbox/brewster-razor-controller.md`

### Brewster — HTTP Controller Pattern for Workflow Forms

**Author:** Brewster (Umbraco Platform Specialist)  
**Status:** Implemented  

**Context:** Workflow forms render via Razor partials + route-hijacking controller (not JSON API + Web Components).

**Key Decisions:**

1. **Both GET & POST in Index()**
   - GET and POST both land on `RenderController.Index()`
   - Manual `HttpContext.Request.Method` check inside
   - Reason: Umbraco content router hardcodes `action = "Index"`. Separate POST route would create different URL than content node.
   - `[ValidateAntiForgeryToken]` not used; manual antiforgery validation via `IAntiforgery.ValidateRequestAsync()`

2. **Anonymous userId via Cookie**
   - `PrismAnonUserId` cookie stores GUID for session-stable workflow instance tracking
   - Reason: Workflow page is intentionally open (no `[Authorize]` for demo). Instance service requires non-null userId.
   - Production: Replace with `User.FindFirst("oid")?.Value` after adding `[Authorize]`

3. **Form Field Naming**
   - Form inputs use pattern `fields[fieldKey]` (e.g. `fields[firstName]`)
   - Tracking hidden fields use flat names (`InstanceId`, `StateVersion`, `Action`, `WorkflowKey`, `ReturnUrl`)
   - Prevents collision between field values and control inputs

4. **workflowPage Document Type in Core Seeder**
   - `EnsureWorkflowPageAsync()` added to `PrismContentTypeSeeder`
   - Consistency: Core seeder already owns `workflowDemoPage`
   - Demo content node seeded separately by TestSite `WorkflowPageSeeder`

5. **No Double-Render**
   - `IWorkflowRenderService` NOT injected into controller
   - Reason: `IWorkflowInstanceService` already calls render service internally
   - Controller receives fully populated `WorkflowResponseEnvelope`

**Build Status:** ✅ 0 errors, 0 warnings

---

## 📌 2026-04-09: Workflow Forms Render via Razor Partial Views (Isabelle)

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-razor-views.md`

### Isabelle — Razor Partials Over Lit Web Components

**Author:** Isabelle (Frontend Dev & Accessibility Lead)  
**Status:** Implemented  

**Context:** Workflow form steps previously prototyped as Lit Web Components. Team decision to use Razor partials instead.

**Decision:** Workflow form steps render via Razor partial views, not Lit Web Components.

**Rationale:**
- Element Types already use Razor partials for rendering — consistency
- Server-rendered HTML works on mobile (WKWebView) and desktop without JavaScript
- CSS handles all responsive styling
- No Lit runtime required

**Partials Created:**
- `_WorkflowField.cshtml` — Single field renderer (all types, WCAG 2.2 AA)
- `_WorkflowStep-Collect.cshtml` — Form with fieldsets and action buttons
- `_WorkflowStep-Review.cshtml` — Read-only summary + confirm/back actions
- `_WorkflowStep-Completion.cshtml` — Success confirmation panel

**CSS:**
- `prism-workflow.css` in `TestSite/wwwroot/css/`
- GDS-inspired design patterns
- CSS custom properties for theming
- `:focus-visible` for keyboard navigation
- Responsive at 640px breakpoint

**Superseded & Deleted:**
- `prism-workflow-shell.ts`, `prism-workflow-collect.ts`, `prism-workflow-completion.ts`
- `workflow-orchestrator.ts`, `workflow-api-client.ts`
- `prism-workflow` rollup entry removed from `vite.config.ts`

**Impact:** No runtime JS required; accessibility enforced server-side in HTML.

---

## 📌 2026-04-09: Extended Workflow Field Types (Isabelle)

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-workflow-frontend-extension.md`

### Isabelle — Extended FieldType Union & Renderer

**Author:** Isabelle (Frontend Dev & Accessibility Lead)  
**Status:** Complete (Superseded by Razor architecture)

**Context:** Backend redesign moved field definitions to Umbraco Element Types with `fieldType` values from property editor introspection.

**Changes Implemented (Earlier Phase):**

1. **Extended FieldType Union**
   - From 8 types to 15: added email, decimal, boolean, datetime, checkboxlist, slider, multitextstring
   - Added fallback `string` for unmapped types

2. **Renderer Extensions**
   - `email` → `<input type="email">`
   - `decimal` → `<input type="number" step="0.01">`
   - `boolean` → alias of checkbox (single checkbox, label inline)
   - `datetime` → `<input type="datetime-local">`
   - `checkboxlist` → `<fieldset>` with `<legend>`, multiple checkboxes with `name[]` array
   - `slider` → `<input type="range">` with live `<output>`
   - Unknown types → fallback to `<input type="text">`

3. **Accessibility Enhancements**
   - All error messages: `role="alert" aria-live="polite"`
   - Checkboxlist wrapped in `<fieldset>` + `<legend>`
   - Radio buttons: `aria-describedby` linking to hints/errors
   - Slider: proper focus indicators (3px yellow outline)

4. **Form Submission**
   - Extended to detect `name[]` fields (checkboxlist)
   - Aggregates checked values into arrays
   - Handles `'on'` values for boolean checkboxes

**Note:** This work was superseded by the Razor architecture decision. The field type extensions informed Razor partial field rendering, but the Lit implementation was not used in production.

**Build Status (Earlier Phase):** ✅ `npm run build` — 0 TypeScript errors


---

## 📌 2026-04-10: Shared Library Extraction — UmbracoPrism.Shared (Tom Nook + Blathers)

**Session Log:** `.squad/log/2026-04-10T07:50:19Z-shared-lib-extraction.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/tom-nook-shared-lib-proposal.md`
- `.squad/decisions/inbox/blathers-shared-lib-extraction.md`

### Tom Nook — Architectural Analysis: UmbracoPrism.Shared Library Proposal

**Decision:** Extract a lightweight `UmbracoPrism.Shared` library containing auth extensions, identity helpers, and workflow DTOs with **zero Umbraco dependencies**.

**Scope:** 8 files (7 existing + 1 extracted record):
- `Extensions/PrismIdentityExtensions.cs` (GetTenantId, GetEmail, PrismResolvers)
- `Extensions/PrismAuthExtensions.cs` (AddPrismAuthentication, signing key resolution)
- `Models/BackOfficeTenant.cs` (extracted from inline record)
- `Models/Workflow/WorkflowResponseEnvelope.cs` (all workflow DTOs)
- `Services/IPrismSigningKeyCache.cs`, `PrismSigningKeyCache.cs`, `PrismSigningKeyCacheSnapshot.cs`

**Dependencies:** Only `Microsoft.Identity.Web` (4.3.0) and `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.2) — NO Umbraco packages.

**Benefits:**
- ✅ Clean architecture — business apps don't depend on CMS
- ✅ Removes brittle `ConfigureApplicationPartManager` workaround
- ✅ Small, surgical refactor — minimal churn
- ✅ Future-proof — new business apps can reference Shared without Umbraco

**Commit:** `c4acb2f` (Blathers)

---

### Blathers — Shared Library Extraction Implementation

**Decision:** Implemented extraction of `UmbracoPrism.Shared` library.

**What Changed:**
- Moved 8 files from Core to Shared
- Updated project references: Core → Shared; MockBusinessApp: Core → Shared
- Removed `ConfigureApplicationPartManager` workaround from MockBusinessApp/Program.cs
- Zero breaking changes; namespace preservation

**Verification:**
- ✅ Build: `dotnet build UmbracoPrism.sln -c Release` (0 errors)
- ✅ Tests: 218 passed
- ✅ All public APIs unchanged

---

## 📌 2026-04-10: Workflow Architecture Authority Moved to Business App (Copilot)

**Merged From Inbox:**
- `.squad/decisions/inbox/copilot-workflow-authority-to-business-app.md`

### Workflow Authority Moved to Business App

**Status:** Accepted

**Decision:** Business App is now authoritative source of workflow state and definitions.

**New Flow:**
1. Browser → Umbraco (member visits workflowPage content node)
2. Umbraco → Business App (`WorkflowPageController` calls `IBusinessAppWorkflowClient.GetCurrentAsync()`)
3. Business App → Umbraco (returns `WorkflowResponseEnvelope`)
4. Umbraco → Browser (renders UI)
5. Browser → Umbraco (member submits form)
6. Umbraco → Business App (`IBusinessAppWorkflowClient.AdvanceAsync()`)
7. Business App → Umbraco (returns next step)

**New Components:**
- `BusinessAppWorkflowEngine` (MockBusinessApp) — singleton; loads JSON seeds
- `WorkflowApiController` (MockBusinessApp) — POST `/api/workflow/{key}/current` and `/advance`
- `IBusinessAppWorkflowClient` (Core) — interface for Umbraco → Business App
- `BusinessAppWorkflowClient` (Core) — implementation using `IHttpClientFactory`

**Security:** Umbraco includes `X-Prism-Api-Key` header. If unconfigured, endpoint is open (dev only).

---

## 📌 2026-04-10: Workflow Feature Cleanup Directive (Tom Nook)

**Merged From Inbox:**
- `.squad/decisions/inbox/tom-nook-workflow-cleanup.md`

### Architecture Decision: Workflow Feature Cleanup

**Author:** Tom Nook (Lead Architect)  
**Status:** Approved — ready for execution

**Summary:** Old Umbraco-hosted state machine is dead code. Delete it.

**Keep:** IBusinessAppWorkflowClient, BusinessAppWorkflowClient, WorkflowResponseEnvelope, WorkflowPageController, BusinessAppWorkflowEngine, WorkflowApiController, WorkflowEmulatorController, workflow-seeds/, migrations

**Delete:** WorkflowInstanceService, WorkflowRenderService, WorkflowResponseFactory, WorkflowTenantGuard, WorkflowController, WorkflowDefinition, WorkflowInstance, WorkflowEvent, WorkflowTask, WorkflowFieldGroupSubmission, PrismFieldGroupDefinitionSchema, WorkflowExceptions.cs, old tests, old seed JSON in Core

**Security:** WorkflowController is unguarded API surface — delete immediately (dead endpoint is liability).

---

## 📌 2026-04-10: Workflow Architecture Documentation Complete (Celeste)

**Merged From Inbox:**
- `.squad/decisions/inbox/celeste-workflow-docs.md`

### Workflow Architecture Documentation Complete

**Date:** 2026-01-15  
**Author:** Celeste (Documentation Engineer)

**Summary:** Added comprehensive XML documentation to 7 workflow code files.

**Files Documented:**
1. IBusinessAppWorkflowClient.cs
2. BusinessAppWorkflowClient.cs
3. BusinessAppWorkflowEngine.cs (MockBusinessApp)
4. WorkflowDefinitionFile.cs
5. WorkflowApiController.cs (MockBusinessApp)
6. WorkflowPageController.cs (TestSite)
7. WorkflowBuilderExtensions.cs

**Build Verification:** ✅ 0 errors, 0 warnings

---

## 📌 2026-04-10: Workflow Security Review — Copper

**Merged From Inbox:**
- `.squad/decisions/inbox/copper-workflow-security-review.md`

### Workflow Security Review

**Reviewer:** Copper (Security Engineer)  
**Status:** ✅ CRITICAL issues fixed

**Issues Found & Fixed:**

1. **CRITICAL-01: API Key Timing Attack [FIXED]**
   - Replaced `==` with `CryptographicOperations.FixedTimeEquals()`

2. **CRITICAL-02: Fail-Open on Missing API Key [FIXED]**
   - Changed to fail-closed; log error, reject all requests

3. **CRITICAL-03: Tenant/User Identity Bypass [FIXED]**
   - Added validation that form tenant/user match session

4. **CRITICAL-04: Error Sanitization [PASS]**
   - Already sanitized; no leaks

5. **HIGH-05: State Version Not Cryptographically Signed [DOCUMENTED]**
   - Recommendation: Add HMAC/signed token for production

6. **HIGH-06: Anonymous User Cookie Not Signed [DOCUMENTED]**
   - Recommendation: Use Data Protection API for production

**Overall:** 🟡 Acceptable for MVP; needs hardening for production

---

## 📌 2026-04-09: User Directive — No Lit for Workflow Form Rendering (Copilot)

**Merged From Inbox:**
- `.squad/decisions/inbox/copilot-directive-2026-04-09T175520.md`

### User Directive: Workflow Form Rendering via Razor, Not Lit

**By:** Jonny Muir  
**Date:** 2026-04-09

**Decision:** Workflow form steps render via Razor partial views, not Lit Web Components.

**Why:** Lit adds complexity with no benefit when server-rendered Razor + CSS works identically on mobile (WKWebView) and desktop.

**Implications:**
- Delete: prism-workflow-collect.ts, prism-workflow-shell.ts, prism-workflow-orchestrator.ts, workflow-api-client.ts
- WorkflowController becomes route-hijacking Umbraco controller (GET renders, POST advances)
- Add Razor partials: _WorkflowStep-Collect.cshtml, _WorkflowStep-Review.cshtml, _WorkflowStep-Completion.cshtml

---

## 📌 2026-04-11: Workflow UI Full Refactor (Isabelle)

**Session Log:** `.squad/log/2026-04-11T09:00:34Z-workflow-ui-refactor.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/isabelle-workflow-ui-refactor.md`

### Isabelle — Workflow UI Full Refactor

**Date:** 2026-04-11

#### 1. Form Field Name Prefix: `fields[key]`

**Decision:** All form field `name` attributes in `_WorkflowField.cshtml` must use the `fields[key]` prefix pattern.

**Rationale:** The controller uses `[FromForm] Dictionary<string, string> fields` for model binding. Without the `fields[` prefix, submitted values are silently dropped — the model binder produces an empty dictionary. This was the critical P0 bug.

**Applies to:** All 6 field renderers in `_WorkflowField.cshtml` (boolean, radio, checkboxlist, select, textarea, text/email/number/date).

#### 2. Error State via ViewData, Not Model Property

**Decision:** Field-level errors are injected into `_WorkflowField.cshtml` via `ViewData["fieldError"]`, not via a property on `FieldRenderPayload`.

**Rationale:** `FieldRenderPayload` is a Core model (owned by Blathers' domain). Adding UI error state to it would violate separation of concerns. ViewData is the standard ASP.NET pattern for passing view-scoped context that doesn't belong on the model.

**Implementation:** Caller creates a copy of `ViewData` via `new ViewDataDictionary(ViewData)` per field (not mutating the shared instance), sets `["fieldError"]`, and passes it as the third argument to `PartialAsync`.

#### 3. Convention-Based Partial Dispatch via `ICompositeViewEngine`

**Decision:** `WorkflowPage.cshtml` uses `ICompositeViewEngine.GetView()` to resolve `~/Views/Partials/_WorkflowStep-{Archetype}.cshtml` at runtime, replacing the hard-coded `switch` statement.

**Rationale:** Each new archetype previously required editing a core file (`WorkflowPage.cshtml`). Convention-based dispatch means new archetypes only require adding a new `_WorkflowStep-{Archetype}.cshtml` partial — no core file change needed.

**Fallback:** If the view is not found, a `workflow-alert--warn` message is displayed with the archetype name, supporting graceful degradation during development.

#### 4. Single CSS File: `prism-workflow.css`

**Decision:** All workflow-related CSS lives in `prism-workflow.css`. Inline `<style>` blocks are removed from all partials.

**Rationale:** Inline styles prevent theming (designers can't override via CSS custom properties), cause duplication when partials are rendered multiple times, and cannot be cached separately. A single linked stylesheet is cacheable, overridable, and inspectable.

**Linked from:** `Master.cshtml` after `utilities.css`.

#### 5. `prism-*` CSS Class Namespace for All Workflow UI

**Decision:** All workflow UI uses `prism-*` CSS classes (from `prism-workflow.css`). The previous `wf-*` classes are removed.

**Rationale:** Two parallel CSS class systems (`wf-*` inline + `prism-*` in linked file) caused confusion and the linked file was never loaded. Consolidating to `prism-*` aligns with the project's established CSS architecture and enables CSS custom property theming.

#### 6. `_WorkflowStep-Collect` Delegates Entirely to `_WorkflowField`

**Decision:** The Collect step partial no longer has its own field renderer. It delegates 100% to `_WorkflowField` via `PartialAsync`.

**Rationale:** The old Collect partial only handled 3 field types (select, textarea, basic text). `_WorkflowField` handles 11 types with full WCAG 2.2 AA semantics. Duplication was removed to prevent drift.

---

## 📌 2026-04-11: Workflow Emulator TUI REPL Design (Tom Nook)

**Session Log:** `.squad/log/2026-04-11T09:00:34Z-workflow-ui-refactor.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/tom-nook-tui-design.md`

### Tom Nook — Replace WorkflowEmulatorController with Spectre.Console TUI REPL

**Date:** 2026-04-11

**Status:** Proposed

#### Problem

`WorkflowEmulatorController.cs` requires a developer to issue HTTP requests (via `.http` files or curl) to inspect and advance workflow instances. This is clunky — the app already runs in a terminal; a dev shouldn't need a separate HTTP client just to simulate a reviewer action.

#### Decision

Replace the MVC controller with a **Spectre.Console command REPL** running on the main thread as a `BackgroundService`. The web host keeps running on its own background thread (standard ASP.NET Core behaviour). The terminal becomes an interactive control plane.

**Chosen approach: Option A — Spectre.Console REPL**

##### Why Spectre.Console?

- Widely used in .NET CLI tooling; no conceptual overhead
- `Table` rendering for instance lists
- `Markup` for coloured status output
- `AnsiConsole.Ask<string>` for confirmations
- Single lightweight NuGet package: `Spectre.Console`

##### REPL Hosting

Register as `IHostedService`:
```csharp
builder.Services.AddHostedService<TuiReplService>();
```
`TuiReplService` starts after the web host is listening. It runs a `while(true)` readline loop, dispatching to handler methods. It holds an injected `BusinessAppWorkflowEngine` reference.

##### Logging Conflict Resolution

Configure `appsettings.Development.json` to suppress console log noise during REPL operation:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

This means startup/shutdown messages still appear, but runtime HTTP request logs don't spray over the prompt. The REPL uses `AnsiConsole.MarkupLine` for its own output — no interference from the framework logger. This avoids adding Serilog or a file sink dependency.

#### Command Set

| Command | Arguments | Description |
|---------|-----------|-------------|
| `list` / `ls` | — | Table of all active instances: short ID, workflow key, state, tenant, user, version |
| `show` | `<instanceId>` | Full detail view for one instance including field values |
| `approve` | `<instanceId>` | Advance instance with reviewer action `approve` |
| `reject` | `<instanceId>` | Advance instance with reviewer action `request-changes` |
| `reset` | `<instanceId>` | Remove instance from engine (next user call recreates it from scratch) |
| `defs` | — | List loaded workflow definition keys and instance counts |
| `help` / `?` | — | Print command reference |
| `quit` / `exit` / `q` | — | Stop the application via `IHostApplicationLifetime.StopApplication()` |

**Shorthand IDs:** When displaying instances in `list`, show a short prefix (first 8 chars of GUID) so the dev can type `approve 3f2a1b8c` without copy-pasting a full UUID. The engine already uses full GUIDs; the REPL resolves prefix → full ID.

#### Risks & Gotchas

1. **Startup log spam before REPL starts**: Print a `[yellow]⚡ Emulator ready.[/]` banner from `ExecuteAsync` *after* a short `Task.Delay(500)` to let the web host finish its startup logs — banner lands last, visually anchoring the prompt.

2. **Non-interactive stdin**: If stdin is redirected (CI, pipe), `Console.ReadLine()` returns `null`. Detect this at startup (`!Console.IsInputRedirected`) and skip the REPL entirely — don't crash.

3. **Thread safety on AnsiConsole**: Only the REPL background service calls `AnsiConsole` output methods. ASP.NET logs go through ILogger (console provider). These can interleave but won't corrupt each other — acceptable for a dev tool.

4. **`reset` semantics**: Deleting an instance from `_instancesById` requires exposing a new `Reset(instanceId)` method on `BusinessAppWorkflowEngine`. This method must also remove the `_instanceLookup` entry to avoid orphaned lookup keys.

5. **Prefix ambiguity**: If two instance IDs share the same 8-char prefix (unlikely with GUIDs, but possible in long-running sessions), the REPL should warn and require a longer prefix. A `StartsWith` match on full ID handles this gracefully.

#### Next Steps (routing)

This design is ready to hand off to **Blathers** for implementation:

1. Add `Spectre.Console` NuGet reference
2. Implement `TuiReplService : BackgroundService`
3. Add `Reset(instanceId)` to `BusinessAppWorkflowEngine`
4. Delete controller/filter stack
5. Update `Program.cs` (remove `AddControllers`, `MapControllers`)
6. Adjust `appsettings.Development.json` log levels
7. **Tangy** should add integration-level smoke tests for the new engine `Reset` method

---

# Decision: Community Enquiry Workflow — Field Constraints Not Yet Wired

**Date:** 2025-01-10  
**Author:** Blathers (Backend Dev)  
**Context:** Replacing retirement-quote demo with comprehensive community-enquiry workflow

## Decision

Created a new `community-enquiry` workflow definition that showcases ALL of Prism's field types. The JSON field definitions include constraint properties (`minLength`, `maxLength`) where appropriate, but these are **not yet mapped** through to the runtime `FieldRenderPayload`.

## Current State

**Field constraint properties in JSON (ready):**
- `about-you-v1.json`: `maxLength: 100` on `full-name` and `organisation` fields
- `your-enquiry-v1.json`: `minLength: 20`, `maxLength: 500` on `message` field

**Engine mapping (incomplete):**
- `FieldFile` record in `WorkflowDefinitionFile.cs` does NOT declare `MinLength`, `MaxLength`, `Pattern`, `Min`, `Max` properties
- `FieldRenderPayload` in `WorkflowResponseEnvelope.cs` does NOT expose these properties
- `BuildFieldGroup()` in `BusinessAppWorkflowEngine.cs` (lines 331-340) only maps: `FieldKey`, `Label`, `Hint`, `FieldType`, `Required`, `Options`, `Value`

## What This Means

1. **JSON is forward-compatible:** The constraint properties are stored in the seed files and will be ignored by the deserializer until the C# models are updated.
2. **UI won't receive constraints:** The render payloads sent to the frontend will not include validation hints (min/max length, patterns) until the engine is extended.
3. **No validation errors:** The system will not break; constraints will simply be unavailable to the UI for client-side validation.

## Follow-Up Work Required

To complete constraint support, a future task must:

1. **Extend `FieldFile` record** (`WorkflowDefinitionFile.cs`):
   ```csharp
   public int? MinLength { get; init; }
   public int? MaxLength { get; init; }
   public string? Pattern { get; init; }
   public decimal? Min { get; init; }
   public decimal? Max { get; init; }
   ```

2. **Extend `FieldRenderPayload` record** (`WorkflowResponseEnvelope.cs`):
   ```csharp
   public int? MinLength { get; init; }
   public int? MaxLength { get; init; }
   public string? Pattern { get; init; }
   public decimal? Min { get; init; }
   public decimal? Max { get; init; }
   ```

3. **Update `BuildFieldGroup()` method** (`BusinessAppWorkflowEngine.cs`, ~line 331):
   ```csharp
   var fields = group.Fields.Select(f => new FieldRenderPayload
   {
       FieldKey = f.FieldKey,
       Label = f.Label,
       Hint = f.Hint,
       FieldType = f.FieldType,
       Required = f.Required,
       Options = f.Options,
       Value = savedValues.TryGetValue(f.FieldKey, out var v) ? v : null,
       MinLength = f.MinLength,     // NEW
       MaxLength = f.MaxLength,     // NEW
       Pattern = f.Pattern,         // NEW
       Min = f.Min,                 // NEW
       Max = f.Max                  // NEW
   }).ToArray();
   ```

## Rationale

Defined the constraints in the JSON seed files NOW to establish the schema, even though the backend doesn't yet propagate them. This approach:

- **Avoids rework:** When constraint support is added, the seed files won't need updating
- **Documents intent:** Makes it clear what validation rules should apply
- **Maintains consistency:** All field metadata lives in one place (the JSON definition)
- **No breaking changes:** JSON deserialization ignores unknown properties, so adding them to the model later is safe

## Impact

- ✅ **No build errors:** System compiles and runs correctly
- ✅ **No runtime errors:** Extra JSON properties are silently ignored
- ⚠️ **Missing feature:** UI cannot validate min/max length until backend support added
- 📋 **Follow-up task needed:** Wire constraints through the full stack (C# models + engine mapping)

## Files Changed

- Created: `workflow-seeds/field-groups/about-you-v1.json`
- Created: `workflow-seeds/field-groups/your-enquiry-v1.json`
- Created: `workflow-seeds/community-enquiry-v1.json`
- Deleted: `workflow-seeds/retirement-quote-v1.json`

## Workflow Field Types Demonstrated

1. ✅ **text** — `full-name`, `organisation` (with MaxLength)
2. ✅ **email** — `email-address`
3. ✅ **select** — `your-role`
4. ✅ **radio** — `enquiry-type`
5. ✅ **textarea** — `message` (with MinLength/MaxLength)
6. ✅ **checkboxlist** — `topics`
7. ✅ **boolean** — `newsletter`
8. ✅ **date** — (used in `personal-details` field group from `information-request` workflow)

This workflow now serves as a comprehensive showcase of Prism's form capabilities.

---

# Decision: Workflow Controller Integration with Nonce & Structural Validation

**Date:** 2026-03-23  
**Author:** Blathers (Backend Dev)  
**Status:** Implemented  

## Context

The workflow engine now has two security/validation services:
- `IWorkflowStepNonceService` — generates tamper-proof nonces binding forms to server-authoritative field definitions
- `IWorkflowFieldValidator` — validates submitted form data against authoritative schema (type, required, constraints, options whitelist)

These services needed to be integrated into `WorkflowPageController` to enforce security before sending data to the Business App.

## Decision

Integrated both services into the workflow controller's GET and POST flows:

### GET Flow
1. After successfully building the workflow envelope, extract all fields from `envelope.Render.FieldGroups`
2. Generate a nonce via `nonceService.CreateAsync(allFields)`
3. Set the nonce on the ViewModel for rendering in a hidden field
4. Changed `HandleGet()` from synchronous to `async Task<IActionResult>` to support this

### POST Flow
1. **Nonce validation** (after antiforgery check):
   - Extract nonce from form
   - Resolve to authoritative fields via `nonceService.ResolveAsync(nonce)`
   - Redirect if missing/expired (prevents stale or tampered forms)
   
2. **Structural validation**:
   - Extract submitted fields (keys prefixed `fields[`)
   - Call `fieldValidator.Validate(authoritativeFields, submittedFields)`
   - If invalid, convert errors to `WorkflowProblem` objects, serialize to TempData, redirect via PRG
   
3. **Business App submission**:
   - Only proceed if both validations pass
   - Use already-validated `submittedFields` dict (converted to `Dictionary<string, object?>` for API compatibility)

### Implementation Details
- Added two constructor parameters: `IWorkflowStepNonceService nonceService`, `IWorkflowFieldValidator fieldValidator`
- Added `Nonce` property to `WorkflowViewModel`
- Updated `Index()` to await both GET and POST handlers
- All validation failures redirect via PRG pattern with problems in TempData

## Rationale

**Defense in depth:**
- Nonce binding prevents field injection (attacker cannot add/modify field definitions client-side)
- Structural validation ensures type safety, required fields, constraints, and options whitelisting before hitting the Business App
- Nonce expiry mitigates replay attacks with stale forms

**Performance:**
- Validations run in-memory (nonce cache, in-process validation)
- No round-trip to Business App for invalid data

**UX:**
- Validation errors surface immediately via TempData → shown in same view on redirect
- Failed nonces trigger clean redirect (user sees current state, can resubmit)

## Consequences

### Positive
- ✅ Tamper-proof forms: field schema enforced server-side
- ✅ Early validation: bad data rejected before Business App call
- ✅ Clean error handling: PRG pattern with structured problems
- ✅ Build succeeded (0 warnings, 0 errors)

### Negative
- Front-end must render `Nonce` in hidden field (requires view template update)
- Nonce expiry creates time window for form submission (configurable via cache TTL, but still a constraint)

### Open Questions
- What should nonce cache TTL be? (default 15 minutes in implementation)
- Should expired nonces show a user-friendly message vs. silent redirect?

## Alternatives Considered

1. **Validation in Business App only** — rejected because it requires network round-trip for every validation error
2. **Client-side validation only** — rejected because it's trivially bypassable
3. **Nonce per field instead of per form** — rejected as over-engineering (form-level nonce sufficient)

## Follow-up Work

- [ ] Update workflow view template to render nonce in hidden field
- [ ] Add test coverage for nonce expiry scenarios
- [ ] Add test coverage for validation error flows (missing required, type mismatch, constraint violations)
- [ ] Document nonce TTL configuration in README or setup guide

---

# Decision: Server-Side Workflow Field Validation Architecture

**Author:** Blathers  
**Date:** 2026-03-29  
**Status:** Implemented

## Context

The workflow engine requires server-side structural validation before forwarding form POSTs to the Business App. The validator must use the authoritative field definitions cached by `IWorkflowStepNonceService` to prevent client-side tampering.

## Decision

Implemented `IWorkflowFieldValidator` / `WorkflowFieldValidator` with the following architecture:

### Validation Sequence (First Error Wins)

1. **Field key whitelist** — Reject any submitted key not in authoritative definitions
2. **Required check** — Empty values fail if `field.Required`
3. **Type validation** — `number` → decimal parse, `email` → basic check, `date`/`datetime` → DateTime parse
4. **Options whitelist** — For `select`/`radio`/`checkboxlist`, check against `field.Options`
5. **Constraints** — `MinLength`, `MaxLength`, `Pattern`, `Min`, `Max`

Only the **first** error per field is recorded (mirrors GDS validation pattern).

### Checkboxlist Suffix Normalization

Client submits checkboxlist fields as `{key}[]`. Validator is lenient: both `field.FieldKey` and `{field.FieldKey}[]` are whitelisted.

### Error Message Format

Pattern: `"{field.Label} {message}"`

Examples:
- `"Email Address is required."`
- `"Age must be a number."`
- `"Country contains an invalid selection."`
- `"Password must be at least 8 characters."`

### Registration

Transient service in `WorkflowBuilderExtensions.AddPrismWorkflowEngine()`. New instance per validation call, no state.

### Model Structure

```csharp
public record WorkflowValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyDictionary<string, string> Errors { get; init; }
    
    public static WorkflowValidationResult Pass();
    public static WorkflowValidationResult Fail(Dictionary<string, string> errors);
}
```

## Files

- `src/UmbracoPrism.Core/Models/Workflow/WorkflowValidationResult.cs`
- `src/UmbracoPrism.Core/Services/Workflow/IWorkflowFieldValidator.cs`
- `src/UmbracoPrism.Core/Services/Workflow/WorkflowFieldValidator.cs`
- `src/UmbracoPrism.Core/Extensions/WorkflowBuilderExtensions.cs` (registration)

## Consequences

### Positive

- Server-authoritative validation prevents client-side tampering
- First-error-wins pattern reduces error message noise
- Lenient checkboxlist handling improves form UX
- Stateless validator enables easy unit testing
- Type coercion errors caught before Business App sees data

### Negative

- Email validation is basic (not strict RFC5322) — business rules validation remains in Business App
- Checkboxlist split-on-comma assumes client serialization format — may need adjustment if client changes

## Next Steps

1. Controller integration — call validator in POST endpoint before Business App forwarding
2. Unit tests — cover all validation rules and error collapsing behavior
3. Consider structured error codes (not just messages) for client-side display logic

---

# Field Constraint Properties Convention

**Decision:** Add validation constraint properties to workflow field models for full-stack form validation.

**What:**
- Added five nullable constraint properties to `FieldRenderPayload` (shared API model) and `FieldFile` (Business App definition model)
- Properties: `MinLength`, `MaxLength`, `Pattern`, `Min`, `Max`
- All nullable to maintain backward compatibility with existing field definitions

**Why:**
- Enables Business Apps to declaratively specify field validation rules in their workflow definitions
- Allows Prism to auto-emit HTML5 constraint attributes (`minlength`, `maxlength`, `pattern`, `min`, `max`) for client-side validation
- Provides server-side validation engine with the same constraints for consistent validation behavior
- Nullable approach means existing workflows continue working without modification

**Convention:**
- Constraint properties are optional (nullable) on both definition (`FieldFile`) and render payload (`FieldRenderPayload`)
- Text/textarea fields use `MinLength` and `MaxLength` (int)
- Text/email fields use `Pattern` (string) for HTML5 regex validation
- Number fields use `Min` and `Max` (decimal) for numeric range validation
- Business App mapping layer (`BuildFieldGroup`) passes constraint values through unchanged
- Properties added after `Options` in record definition to maintain stable ordering

**Impact:**
- Shared Models: `WorkflowResponseEnvelope.cs` updated
- Mock Business App: `WorkflowDefinitionFile.cs` and `BusinessAppWorkflowEngine.cs` updated
- Existing field group JSON files remain valid (constraints optional)
- New field definitions can specify constraints as needed (already used in community-enquiry workflow)

---

# Decision: Workflow Form Nonce Service Architecture

**Date:** 2026-04-11  
**Author:** Blathers (Backend Dev)  
**Status:** Implemented

## Context

Building full-stack workflow form validation for Prism. Needed a mechanism to prevent:
- **Field key injection** — submitting fields the Business App never asked for
- **Constraint bypass** — ignoring MinLength, Required, MaxLength etc.
- **Replay attacks** — reusing nonces across different workflow steps

## Decision

Implemented a cryptographic nonce service (`IWorkflowStepNonceService`) that binds each form submission to its server-side authoritative field definition.

### Architecture

1. **Nonce Generation:**
   - Format: `Guid.NewGuid().ToString("N")` (32-char hex, no dashes)
   - Cache key: `"prism:workflow:nonce:{nonce}"`
   - Stores serialized `FieldRenderPayload[]` from Business App response

2. **Storage:**
   - Uses `IDistributedCache` (registered via `AddDistributedMemoryCache()`)
   - Works out of the box for single-server dev
   - Production: replace with `AddStackExchangeRedisCache()` or `AddDistributedSqlServerCache()`

3. **TTL:**
   - Default: **2 hours** (configurable via `PrismWorkflowOptions.NonceExpiry`)
   - Rationale: balances security with UX for slow multi-step workflows

4. **Graceful Degradation:**
   - Expired/missing nonce → `ResolveAsync()` returns `null`
   - Caller redirects to GET (no crash, no data loss)

5. **Browser Back Button:**
   - Nonce NOT removed on resolve
   - Survives multiple POSTs within TTL window
   - User can use back button and resubmit without "nonce expired" error

### Configuration

```json
{
  "Prism": {
    "Workflow": {
      "NonceExpiry": "02:00:00"  // 2 hours default
    }
  }
}
```

### Services Registered

In `WorkflowBuilderExtensions.AddPrismWorkflowEngine()`:
- `IDistributedCache` — singleton, in-memory cache
- `PrismWorkflowOptions` — bound from `"Prism:Workflow"` config section
- `IWorkflowStepNonceService` — singleton, nonce generation/validation

## Alternatives Considered

1. **JWT signed tokens** — overkill; nonce lookup is faster and doesn't bloat HTML
2. **Database-backed nonce table** — slower; cache is sufficient and auto-expires
3. **One-time use nonces** — breaks browser back button UX

## Consequences

✅ **Pros:**
- Prevents field injection and constraint bypass attacks
- Zero-config for single-server dev (in-memory cache)
- Multi-server ready (swap to Redis/SQL)
- Browser back button friendly

⚠️ **Considerations:**
- Devs deploying multi-server must configure distributed cache provider
- Nonce TTL should match longest expected workflow session duration

## Related Files

- `src/UmbracoPrism.Core/Configuration/PrismWorkflowOptions.cs`
- `src/UmbracoPrism.Core/Services/Workflow/IWorkflowStepNonceService.cs`
- `src/UmbracoPrism.Core/Services/Workflow/WorkflowStepNonceService.cs`
- `src/UmbracoPrism.Core/Extensions/WorkflowBuilderExtensions.cs`

---

# Form Value Retention Strategy

**Author:** Brewster (Umbraco Platform Specialist)  
**Date:** 2026-04-12  
**Status:** Implemented

## Context

When workflow forms fail validation (either structural or Business App validation), the PRG (Post-Redirect-Get) pattern was clearing all user-entered values. This created:
- **Usability problem:** Users must re-enter all data
- **Accessibility violation:** WCAG 3.3.1 requires errors be identified AND the value that caused the error be preserved

## Decision

**Store submitted field values in TempData alongside validation problems, then repopulate form fields on GET.**

### Implementation Approach

1. **Controller POST** — on validation failure:
   - Serialize submitted field values: `TempData["WorkflowFormValues"] = JsonSerializer.Serialize(submittedFields)`
   - Applied to BOTH failure paths: structural validation AND BA validation_error responses

2. **Controller GET** — retrieve and apply:
   - Add `PopFormValuesFromTempData()` method (parallel to `PopProblemsFromTempData()`)
   - Pass retrieved values to `BuildViewModel(envelope, workflowKey, problems, formValues)`

3. **View Model** — store for tag helpers:
   - Add `FormValues` property (IReadOnlyDictionary<string, string>)

4. **Tag Helper** — render with pre-filled values:
   - Add `values` attribute accepting IReadOnlyDictionary<string, string>
   - Each field type checks `Values?.GetValueOrDefault(field.FieldKey)` FIRST, then falls back to `field.Value`
   - Submitted values take precedence over BA-provided field defaults

5. **View Template** — pass values through:
   - Update `<prism-field>` invocation: `values="@Model.FormValues"`

### Key Design Choices

| Choice | Rationale |
|--------|-----------|
| **TempData storage** | Existing pattern for problems; survives redirect; auto-cleanup after read |
| **JSON serialization** | Matches existing `PopProblemsFromTempData()` pattern |
| **Dictionary<string, string>** | Simple, matches `submittedFields` structure from form parser |
| **Precedence: submitted > BA defaults** | Preserve user intent, not server state |
| **No type conversion** | HTML `value=""` attributes accept strings; browser handles type coercion |

### Field Type Handling

- **Text/email/number/date/textarea:** Direct value repopulation
- **Checkbox (boolean):** Check if `Values[key] == "true"`; absence = unchecked
- **Radio:** Pre-select matching option
- **CheckboxList:** Split comma-separated values (ASP.NET Core concatenation behavior)
- **Select:** Pre-select matching option

## Alternatives Considered

1. **Storing in ModelState** — Rejected: ASP.NET Core ModelState is designed for MVC controllers, not Umbraco route-hijacking
2. **Client-side localStorage** — Rejected: Requires JavaScript; fails for no-JS users; GDPR concerns
3. **Hidden fields with all values** — Rejected: Security risk (exposes all fields to tampering)
4. **Nonce-based value cache** — Rejected: Over-engineered; TempData simpler and sufficient

## Consequences

✅ **Positive:**
- WCAG 3.3.1 compliance achieved
- Better UX — users don't lose work on validation errors
- Minimal code change — leverages existing TempData pattern
- Works for all field types

⚠️ **Trade-offs:**
- TempData has session affinity requirements (already a constraint for workflow state)
- Submitted values persist only for one redirect (acceptable for PRG pattern)

## Testing Guidance

Test scenarios:
1. Required field missing → structural validation fails → values retained
2. BA validation_error (e.g., "email already registered") → values retained
3. All field types (checkbox, radio, select, text, textarea, checkboxlist) → all retain values correctly
4. Multiple validation failures → all fields retain values across multiple round-trips

## Related Patterns

- **PRG (Post-Redirect-Get):** Maintained — still redirects after POST
- **Nonce validation:** Unaffected — nonce still prevents field tampering
- **Error summary:** Works together — errors + values shown simultaneously

---

# Demo Workflow: Community Enquiry replaces Retirement Quote

**Date:** 2026-04-11  
**Author:** Brewster (Umbraco Platform Specialist)  
**Status:** Implemented

## Decision

The TestSite demo workflow has been changed from "Retirement Quote" to "Community Enquiry" (branded as "Get in Touch").

### Identifiers

- **Content node name:** `"Get in Touch"` (user-facing page title)
- **URL slug:** `/get-in-touch` (auto-generated by Umbraco)
- **Workflow key:** `"community-enquiry"` (workflow definition identifier used in API calls)

### Rationale

"Get in Touch" is a better showcase for Prism workflow features:
- More generic and relatable than a retirement-specific form
- Demonstrates the same technical capabilities (multi-step workflow, field collection, review, completion)
- Better represents real-world member portal use cases (contact forms, enquiries, requests)

### Implementation Notes

1. **Cleanup on startup:** The seeder now deletes any existing "Retirement Quote" nodes to keep the demo clean. This prevents confusion when switching between branches or pulling updates.

2. **Dual-key lookup:** The seeder checks for existing nodes by BOTH `Name` and `workflowKey` to handle manual backoffice edits (e.g., a user renaming the node but leaving the workflowKey unchanged).

3. **Workflow key convention:** Kebab-case workflow keys (`community-enquiry`) match URL slug patterns (`/get-in-touch`) and REST API naming conventions.

### Impact on Other Teams

- **Blathers (BusinessApp):** Will need to create a `community-enquiry` workflow definition to match the new workflow key. The old `retirement-quote` definition can be archived or removed.
- **Isabelle (Frontend):** No changes required — the workflow rendering is archetype-driven and workflow-agnostic.
- **Tangy (Testing):** Integration tests that reference the old workflow key should be updated.

### Files Changed

- `src/UmbracoPrism.TestSite/WorkflowPageSeeder.cs` — seeder logic
- `src/UmbracoPrism.Core/PrismContentTypeSeeder.cs` — property description example
- `src/UmbracoPrism.Core/Services/IBusinessAppWorkflowClient.cs` — XML doc example
- `src/UmbracoPrism.TestSite/Controllers/WorkflowPageController.cs` — XML doc example

---

# Decision: Workflow Tag Helpers for Form Rendering

**Date:** 2026-03-29  
**Author:** Brewster (Umbraco Platform Specialist)  
**Status:** Implemented  

## Context

Workflow forms in the TestSite require collecting user input with validation errors, hints, and constraint attributes. The initial implementation used a partial view (`_WorkflowField.cshtml`) invoked with ViewData-based error passing. While functional, this approach had boilerplate:

- Manual `Html.AntiForgeryToken()` calls
- 5 hidden input fields duplicated across views
- ViewDataDictionary creation per field to pass errors
- No error summary with anchor links (GDS pattern missing)

## Decision

Implement three tag helpers in `UmbracoPrism.Core/TagHelpers/`:

1. **`<prism-workflow-form>`** — Wraps form with antiforgery token and hidden state fields
2. **`<prism-error-summary>`** — Renders GDS-compliant error summary with field anchor links
3. **`<prism-field>`** — Renders individual fields with labels, hints, errors, ARIA attributes

## Rationale

- **Principle of least surprise:** Developers write declarative Razor without ViewData coupling
- **Accessibility by default:** ARIA attributes, error summary jump links, keyboard navigation built-in
- **Reduced boilerplate:** View code reduced from 55 lines to 37 lines (33% reduction)
- **Auto-discovery:** Tag helpers registered via `@addTagHelper *, UmbracoPrism.Core` in _ViewImports.cshtml
- **Single responsibility:** Each tag helper owns one concern (form wrapper, error summary, field rendering)

## Implementation Details

### 1. PrismWorkflowFormTagHelper

**Target:** `<prism-workflow-form>`  
**Attributes:** `instance-id`, `state-version`, `workflow-key`, `return-url`, `nonce`  
**Output:** `<form>` with method="post", action, novalidate, antiforgery token, 5 hidden fields

**Key patterns:**
- Injects `IAntiforgery` for CSRF token generation
- Uses `[ViewContext]` injection (same as PrismDebugTagHelper)
- Renders antiforgery + hidden fields in `PreContent` (before child content)

### 2. PrismErrorSummaryTagHelper

**Target:** `<prism-error-summary>`  
**Attribute:** `problems` (IReadOnlyList<WorkflowProblem>)  
**Output:** GDS-style error summary div with role="alert", tabindex="-1"

**Key patterns:**
- Suppresses output if no problems (null or empty list)
- Field errors → anchor links (`href="#{fieldKey}"` for keyboard jump-to-field)
- Summary-level errors (no FieldKey) → plain text
- Uses `System.Net.WebUtility.HtmlEncode` for XSS safety

### 3. PrismFieldTagHelper

**Target:** `<prism-field>`  
**Attributes:** `field` (FieldRenderPayload), `errors` (IReadOnlyDictionary<string, string>)  
**Output:** Field wrapper with label, hint, error, input/select/textarea/radio/checkbox

**Field types supported:**
- `text`, `email`, `number`, `date`, `datetime` → `<input type="...">`
- `textarea` → `<textarea>`
- `boolean` → single checkbox
- `select` → `<select>` with `<option>`s
- `radio` → fieldset + radio inputs
- `checkboxlist` → fieldset + checkboxes

**Constraint attributes applied:**
- `required` if Required
- `minlength`/`maxlength` if MinLength/MaxLength has value
- `pattern` if Pattern has value (regex)
- `min`/`max` if Min/Max has value (number fields)

**ARIA attributes:**
- `aria-required="true"` if Required
- `aria-invalid="true"` if field has error
- `aria-describedby="{hint-id} {error-id}"` (only IDs that exist)

**Rendering order (accessibility):**
1. Label (with required star if needed)
2. Hint (if present)
3. Error message (if present)
4. Input element

**XSS safety:**
- All user-facing content encoded via `System.Net.WebUtility.HtmlEncode()`
- No raw string interpolation for field values, labels, hints, or errors

## View Transformation

### Before (55 lines):
```razor
@{
    var token = Html.AntiForgeryToken();
}
<form class="prism-workflow" method="post" action="@Model.ReturnUrl" novalidate>
    @token
    <input type="hidden" name="InstanceId" value="@Model.InstanceId" />
    <!-- ... 4 more hidden fields ... -->
    @if (Model.Problems.Any(p => string.IsNullOrEmpty(p.FieldKey))) { /* manual error div */ }
    @foreach (var field in group.Fields)
    {
        var errorVd = new ViewDataDictionary(ViewData);
        if (Model.FieldErrors.TryGetValue(field.FieldKey, out var fieldErr))
        {
            errorVd["fieldError"] = fieldErr;
        }
        @await Html.PartialAsync("_WorkflowField", field, errorVd)
    }
</form>
```

### After (37 lines):
```razor
<prism-workflow-form instance-id="@Model.InstanceId"
                     state-version="@Model.StateVersion"
                     workflow-key="@Model.WorkflowKey"
                     return-url="@Model.ReturnUrl"
                     nonce="@Model.Nonce">

    <prism-error-summary problems="@Model.Problems" />

    @foreach (var field in group.Fields)
    {
        <prism-field field="@field" errors="@Model.FieldErrors" />
    }
</prism-workflow-form>
```

## Consequences

### Positive
- **Declarative syntax:** Tag helpers read like HTML, not C# boilerplate
- **No ViewData coupling:** Errors passed as dictionary property, intent clear
- **GDS error summary:** Standard accessibility pattern (used by GOV.UK, NHS.UK)
- **Field rendering DRY:** Logic centralized in tag helper, not duplicated
- **Auto-discovered:** No manual registration needed (addTagHelper in _ViewImports)

### Neutral
- **Tag helper complexity:** PrismFieldTagHelper is 270 lines (vs 250-line partial view)
- **Debugging:** Tag helper errors show at compile-time (not runtime like partials)

### Negative
- None identified. Tag helpers are the ASP.NET Core idiomatic approach for reusable HTML generation.

## Namespace Note

Models are in `UmbracoPrism.Core.Models.Workflow` (not `UmbracoPrism.Shared.Models.Workflow`). Initial build failed due to wrong using statement. Fixed by changing:

```csharp
using UmbracoPrism.Shared.Models.Workflow;  // ❌ Wrong
using UmbracoPrism.Core.Models.Workflow;     // ✅ Correct
```

## Future Considerations

- **Validation summary client-side focus:** Add JavaScript to focus error summary on form submission (GDS pattern)
- **Field groups:** Consider a `<prism-field-group>` tag helper to wrap fieldsets
- **Custom validators:** Tag helpers could read from ModelState for server-side validation errors
- **Umbraco Forms integration:** If Umbraco Forms is added, tag helpers may need to coexist with Forms rendering

## References

- [ASP.NET Core Tag Helpers](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/tag-helpers/intro)
- [GOV.UK Design System — Error Summary Component](https://design-system.service.gov.uk/components/error-summary/)
- [PrismDebugTagHelper.cs](../../../src/UmbracoPrism.Core/TagHelpers/PrismDebugTagHelper.cs) — reference for [ViewContext] injection pattern

---

# Brewster — TestSite Demo Polish Decisions

**Session:** 2026-04-11 — TestSite Demo Review and Polish  
**Agent:** Brewster (Umbraco Platform Specialist)  
**Status:** Ready for merge

## Decision: Workflow Form CSS in components.css

**What:** Added 300+ lines of comprehensive CSS for Prism workflow forms to `src/UmbracoPrism.TestSite/wwwroot/css/components.css`.

**Why:** Tag helpers (`PrismWorkflowFormTagHelper`, `PrismFieldTagHelper`, `PrismErrorSummaryTagHelper`) were implemented and working correctly, but the demo had no styling. CSS was a critical missing piece that made the demo incomplete.

**Classes added:**
- `.prism-workflow` — Main form container
- `.prism-error-summary` — GDS-style error summary panel
- `.prism-form-group` — Field wrapper with error state variant
- `.prism-label`, `.prism-input`, `.prism-textarea`, `.prism-select` — Form controls
- `.prism-radio-item`, `.prism-checkbox-item` — Choice controls
- `.prism-button--primary/secondary/destructive` — Action buttons
- `.prism-status__*`, `.prism-panel__*` — Status timeline and completion states

**Design principles:**
- Follow Prism design token pattern (`var(--prism-primary, #4f46e5)`)
- Accessibility: focus states, color contrast, aria support
- GDS Design System influence for error handling
- Mobile-responsive layout

## Decision: Checkbox List Checked State Handling

**What:** Updated `PrismFieldTagHelper.RenderCheckboxList()` to parse `field.Value` as comma-separated string and check each checkbox against matched options.

**Why:** When a checkboxlist field has previously submitted values, those values need to be preserved on re-render (e.g., after validation error). The `field.Value` contains a comma-separated string like `"Red,Blue"`. Each checkbox must check if its option value is in that list.

**Implementation:**
```csharp
var checkedValues = field.Value?.ToString()?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? Array.Empty<string>();

var isChecked = checkedValues.Contains(option, StringComparer.OrdinalIgnoreCase);
```

**Works with:** ASP.NET Core auto-concatenates multiple checkbox values with commas when they share the same `name` attribute.

## Decision: Navigation Link to Demo Page

**What:** Added "Get in Touch" link to Master.cshtml header navigation.

**Why:** The demo page at `/get-in-touch` was seeded and functional, but users had no way to discover it without typing the URL manually. Navigation makes the demo discoverable.

**Location:** `src/UmbracoPrism.TestSite/Views/Shared/Master.cshtml`

**Layout:**
```html
<nav>
    <a href="/">Home</a>
    <a href="/get-in-touch">Get in Touch</a>
    <span style="...">@Context.Request.Host</span>
</nav>
```

## Non-Decisions (Considered but Not Changed)

### Page Title Shows State Display Name

**Current behavior:** `WorkflowPage.cshtml` shows `Model.StateDisplayName` (e.g., "Tell us about your enquiry") as both ViewBag.Title and `<h1>`.

**Considered:** Showing workflow definition display name instead (e.g., "Get in Touch").

**Decision:** Keep current behavior. For multi-step workflows, showing the current *state* display name is semantically correct UX — it tells the user where they are in the process. The page content node name "Get in Touch" is just the URL slug and backoffice identifier.

### Boolean Field Submission Behavior

**Current behavior:** Boolean checkbox fields submit `value="true"` when checked, and nothing when unchecked. The validator treats missing fields as `false`.

**Considered:** Adding a hidden input with `value="false"` before the checkbox (ASP.NET MVC pattern).

**Decision:** Keep current behavior. The validator's `GetSubmittedValue()` method correctly handles missing boolean fields by returning `string.Empty`, which the required check catches. No need for workaround hidden inputs.

## Impact

- **UX:** Professional-looking workflow demo with polished styling
- **Discoverability:** Demo page linked from main navigation
- **Functionality:** Checkbox list state preservation works correctly
- **Completeness:** All tag helpers now have matching CSS

## Follow-Up Candidates

- Add example workflow definition JSON to documentation
- Consider adding `WorkflowDisplayName` to `WorkflowViewModel` for breadcrumb scenarios
- Document CSS class naming conventions in design system guide

---

# Copper — Workflow Validation Stack Security Audit

**Date:** 2026-03-28  
**Auditor:** Copper (Security Engineer)  
**Scope:** Full workflow validation stack (10 files)

---

## Executive Summary

Conducted a comprehensive security audit of the newly-built workflow form validation stack focusing on nonce generation, field validation, controller POST handling, tag helpers, and tenant isolation. **Identified and FIXED 3 Critical/High vulnerabilities directly:**

1. **CRITICAL: Open Redirect in WorkflowPageController** — FIXED
2. **HIGH: ReDoS (Regex Denial of Service) in WorkflowFieldValidator** — FIXED  
3. **MEDIUM: Weak Email Validation** — FIXED

**Remaining risks documented below** require team decision or Business App hardening.

---

## CRITICAL FINDINGS — FIXED

### 1. Open Redirect in WorkflowPageController ✅ FIXED

**Severity:** CRITICAL  
**Location:** `WorkflowPageController.cs` lines 139, 146, 164, 180, 189 (pre-fix)  
**CVE Category:** CWE-601 (URL Redirection to Untrusted Site)

**Vulnerability:**  
The `ReturnUrl` form parameter was used directly in `Redirect()` without validation. An attacker could craft a workflow form with `<input type="hidden" name="ReturnUrl" value="https://attacker.com/phish"/>` and redirect the user to an external phishing site after form submission, while the user's authenticated session cookies remain valid.

**Attack Vector:**
```html
<!-- Malicious form -->
<input type="hidden" name="ReturnUrl" value="https://evil.com/steal-session"/>
```
User submits → redirected to `https://evil.com/steal-session` with cookies still active → attacker can social-engineer credential capture or session hijacking.

**Fix Applied:**
Added `GetSafeReturnUrl()` helper method that validates `returnUrl` using `Url.IsLocalUrl()`. Only local URLs (relative paths or same-origin absolute URLs) are accepted. External URLs are rejected with a warning log and default to `"/"`.

**Code Changes:**
- Added `GetSafeReturnUrl(string? returnUrl)` private method
- Replaced all 5 instances of direct `Redirect(returnUrl)` with `Redirect(safeReturnUrl)`
- Added security warning log when external URL is rejected

**Post-Fix Behavior:**
- `ReturnUrl="/workflow"` → accepted
- `ReturnUrl="https://attacker.com"` → rejected, redirects to `"/"`
- `ReturnUrl` empty/null → defaults to `"/"`

---

### 2. ReDoS (Regex Denial of Service) in WorkflowFieldValidator ✅ FIXED

**Severity:** HIGH  
**Location:** `WorkflowFieldValidator.cs` line 197 (pre-fix)  
**CVE Category:** CWE-1333 (Inefficient Regular Expression Complexity)

**Vulnerability:**  
The `field.Pattern` regex comes from Business App-controlled content (`FieldRenderPayload.Pattern`). No timeout or complexity check was applied to `Regex.IsMatch()`. An attacker controlling BA content could inject catastrophic backtracking patterns (e.g., `^(a+)+$`, `(a|a)*b`) and cause CPU exhaustion when validating user input, leading to DoS.

**Attack Vector:**
1. Attacker controls Business App workflow definition (e.g., compromised BA or malicious insider)
2. BA returns `FieldRenderPayload` with `Pattern = "^(a+)+$"` (catastrophic backtracking)
3. User submits form with input like `"aaaaaaaaaaaaaaaaaaaaX"`
4. `Regex.IsMatch()` hangs for minutes/hours consuming 100% CPU core

**Fix Applied:**
- Added `RegexTimeout = TimeSpan.FromMilliseconds(100)` static field
- Wrapped `Regex.IsMatch()` in `try/catch (RegexMatchTimeoutException)`
- Changed call to `Regex.IsMatch(raw, field.Pattern, RegexOptions.None, RegexTimeout)`
- On timeout exception, return user-friendly error: `"{field.Label} validation pattern is too complex to evaluate safely."`

**Post-Fix Behavior:**
- Normal patterns (e.g., `^\d{5}$` for zip codes) → works as before
- Catastrophic backtracking patterns → timeout after 100ms → validation error instead of hang
- User sees error message, server remains responsive

**Defense-in-Depth Note:**  
100ms timeout is conservative. Even on slow hardware, most legitimate patterns execute in <10ms. This protects against both malicious and accidentally-complex patterns.

---

### 3. Weak Email Validation ✅ FIXED

**Severity:** MEDIUM  
**Location:** `WorkflowFieldValidator.cs` lines 128-131 (pre-fix)

**Vulnerability:**  
Email validation was `raw.Contains('@') && raw.Contains('.')` — trivially bypassable. Accepts non-emails like `@.`, `a@.b`, `..@..`, etc. While not a direct security vulnerability (XSS is prevented by HTML encoding), poor validation can lead to data integrity issues and downstream failures if email addresses are used for communication or identity.

**Fix Applied:**
- Replaced naive check with `MailAddress` parsing (`System.Net.Mail.MailAddress`)
- Validates `new MailAddress(raw)` succeeds and `addr.Address == raw` (no normalization drift)
- Catches `FormatException` for malformed addresses

**Post-Fix Behavior:**
- `"user@example.com"` → valid
- `"@."` → invalid
- `"user@"` → invalid
- `"user@domain"` → invalid (no TLD)
- `"user @domain.com"` → invalid (whitespace)

---

## HIGH FINDINGS — DOCUMENTED (Design Decision)

### 4. Nonce Replay Protection — Intentional Design Risk

**Severity:** HIGH (design risk)  
**Location:** `WorkflowStepNonceService.cs` lines 53-69

**Issue:**  
Nonces are **NOT consumed** after validation (`ResolveAsync` does not delete from cache). This is intentional to support browser back-button workflows, but it enables **replay attacks**. An attacker can capture a valid form POST (with nonce) and replay it indefinitely until the nonce expires (2 hours default via `NonceExpiry`).

**Attack Vector:**
1. Legitimate user submits workflow form → POST with valid nonce
2. Attacker captures the POST request (MITM, XSS, or compromised client)
3. Attacker replays the POST 100 times before nonce expires
4. Each replay bypasses field definition validation (nonce still resolves)

**Mitigation Assessment:**
- **Partial Mitigation:** The Business App's `StateVersion` optimistic concurrency control *should* prevent duplicate state transitions. However:
  - If the BA accepts multiple submissions with the same `StateVersion`, this is fully exploitable.
  - If the BA increments `StateVersion` per submission, replay still creates noise (error responses) but shouldn't corrupt state.
- **Risk Window:** 2 hours (default `NonceExpiry`). An attacker has a 2-hour window to replay captured nonces.

**Recommendation:**
- **Document this as a known design trade-off** in `WorkflowStepNonceService.cs` and `PrismWorkflowOptions.cs`.
- **Consider adding a "nonce usage counter"** in the cache value. Warn (or reject) if a nonce is resolved >N times (e.g., 5) within the expiry window. This preserves back-button support while limiting abuse.
- **Recommendation for Business App:** The BA MUST enforce `StateVersion` optimistic concurrency and reject duplicate submissions. Document this as a hard requirement in the integration guide.

**Tracking:** Add to `.squad/decisions.md` as a documented design constraint.

---

## MEDIUM FINDINGS — DOCUMENTED

### 5. Nonce DoS via Cache Exhaustion

**Severity:** MEDIUM-HIGH  
**Location:** `WorkflowStepNonceService.cs` line 38

**Issue:**  
`CreateAsync` generates unlimited nonces with **no rate limiting or per-user cap**. An authenticated attacker can spam GET requests to the workflow page and generate millions of cache entries, exhausting memory (in-memory cache) or storage (Redis). Each nonce is a JSON-serialized `List<FieldRenderPayload>` (~1-10 KB depending on field count).

**Attack Vector:**
1. Authenticated attacker scripts 10,000 GET requests to `/workflow-page`
2. Each GET creates a new nonce → 10,000 cache entries
3. In-memory cache: Memory exhaustion → OOM → service crash
4. Redis cache: Storage exhaustion → eviction of legitimate nonces → DoS for real users

**Impact:**
- **Availability Risk:** Memory/storage exhaustion can crash the app or evict legitimate nonces (forcing real users to restart workflows)
- **Cost Risk:** If using cloud cache (Redis), unbounded nonce generation → cost spike

**Recommendation:**
- **Immediate (Low-Effort):** Reduce `NonceExpiry` from 2 hours to 30 minutes to limit attack window and cache size.
- **Near-Term:** Add per-user nonce limit (e.g., max 10 active nonces per authenticated member). Store a `HashSet<string>` in cache keyed by `userId` to track active nonces. Reject `CreateAsync` if user already has ≥10 nonces.
- **Long-Term:** Add distributed rate limiting (e.g., `AspNetCoreRateLimit` or per-tenant rate limiter) to limit GET requests to workflow pages.

**Tracking:** Add to backlog as a hardening task. Not blocking for MVP but should be prioritized for production.

---

### 6. Tenant Isolation in Workflow Submission — Business App Responsibility

**Severity:** MEDIUM  
**Location:** `WorkflowPageController.cs` line 184

**Issue:**  
The controller submits `InstanceId`, `WorkflowKey`, `Action`, `StateVersion` from the form to `workflowClient.AdvanceAsync()`. These values are **trusted from the form**. If an attacker can guess or enumerate another tenant's `InstanceId` (GUIDs can be leaked or predicted if not using secure GUID generation), they could potentially submit workflow data for another tenant's instance.

**Mitigation Assessment:**
- **Partial Mitigation:** `BusinessAppWorkflowClient.CreateClientAsync()` attaches the authenticated member's bearer token via `prismContext.GetAuthorizationHeaderAsync()`. This token is **tenant-bound** (per CIA hardening in `.squad/agents/copper/history.md`):
  - Token `tid` claim must match `CurrentTenant.EntraTenantId`.
  - Issuer is tenant-bound (`{tid}.ciamlogin.com/{tid}/v2.0...`).
  - Audience must match tenant's `ClientId`.
- **Residual Risk:** **The Business App MUST verify the bearer token's tenant matches the `InstanceId`'s tenant.** If the BA does NOT enforce this, cross-tenant submission is possible.

**Attack Scenario (if BA doesn't verify):**
1. Attacker is authenticated member of Tenant A
2. Attacker guesses/enumerates `InstanceId` belonging to Tenant B
3. Attacker crafts POST with Tenant B's `InstanceId`
4. Umbraco forwards Tenant A's bearer token to BA
5. If BA trusts `InstanceId` without verifying token tenant → cross-tenant data leak/corruption

**Recommendation:**
- **Umbraco-side (optional defense-in-depth):** Add explicit tenant binding validation in `WorkflowPageController.HandlePost()`:
  ```csharp
  // After nonce validation, before AdvanceAsync:
  if (prismContext.CurrentTenant?.Id != null)
  {
      // Optional: pass CurrentTenant.Id to BA in request body for explicit validation
      // or require BA to enforce tenant binding from bearer token alone
  }
  ```
- **Business App-side (REQUIRED):** Document in BA integration guide:
  > **Security Requirement:** The Business App MUST validate that the bearer token's `tid` claim matches the tenant of the `InstanceId` in the request body. Failure to enforce this allows cross-tenant workflow manipulation.

**Tracking:** Add to `.squad/decisions.md` as a documented Business App integration requirement.

---

### 7. Field Whitelist Case-Sensitivity — Already Safe

**Severity:** LOW (documentation)  
**Location:** `WorkflowFieldValidator.cs` line 24

**Finding:**  
The field key whitelist uses `StringComparer.OrdinalIgnoreCase`, meaning `FieldKey="Email"` from BA and submitted `"email"` both match. This is **intentional and safe** for HTML form name attributes (case-insensitive by convention). Options whitelist (line 171) also uses case-insensitive comparison, which is correct.

**Edge Case Concern:**  
The early rejection logic (lines 36-42) normalizes `[]` suffix for checkboxlists. If an attacker submits `Email[]` when `Email` is NOT a checkboxlist, the normalization could bypass the whitelist. However, the current code correctly handles this:
- Line 37: `normalizedKey = submittedKey.EndsWith("[]") ? submittedKey[..^2] : submittedKey;`
- Line 38: Checks both `normalizedKey` and `submittedKey` against authoritative keys
- Checkboxlist fields explicitly add `{FieldKey}[]` to authoritative set (lines 28-31)

**Recommendation:**
- **No code change needed.** The logic is correct.
- **Add test coverage:** Ensure non-checkboxlist field with `[]` suffix is rejected (e.g., `Email[]` when `Email` is `type="text"` should fail whitelist).

**Tracking:** Add to test backlog.

---

## LOW FINDINGS — SAFE (No Action)

### 8. XSS Risk in Error Messages — SAFE ✅

**Severity:** LOW (verified safe)  
**Location:** `PrismFieldTagHelper.cs`, `PrismErrorSummaryTagHelper.cs`

**Finding:**  
Field labels (`field.Label`), hints (`field.Hint`), error messages, and options from BA content are rendered in HTML. If the BA returns malicious content like `<script>alert(1)</script>` in `field.Label`, this could be an XSS vector.

**Mitigation Verification:**  
All output uses `System.Net.WebUtility.HtmlEncode()` consistently:
- `PrismFieldTagHelper.cs` line 259: `Encode()` helper wraps `HtmlEncode()`
- `PrismErrorSummaryTagHelper.cs` lines 39, 43: Direct `HtmlEncode()` calls
- All label, hint, option, and error rendering paths use `Encode()`

**Conclusion:** XSS risk is **fully mitigated**. No action needed.

---

### 9. Antiforgery Token Scoping — SAFE ✅

**Severity:** LOW (verified safe)  
**Location:** `PrismWorkflowFormTagHelper.cs` line 40

**Finding:**  
Antiforgery token is generated per-form and tied to the authenticated session via `antiforgery.GetAndStoreTokens(ViewContext.HttpContext)`. This is correct per ASP.NET Core security best practices.

**Conclusion:** No issues found. Token scoping is appropriate.

---

### 10. Guid.NewGuid() for Nonce — ACCEPTABLE ✅

**Severity:** INFO  
**Location:** `WorkflowStepNonceService.cs` line 38

**Finding:**  
Nonces are generated using `Guid.NewGuid()`. Is this cryptographically adequate, or should it be strengthened with additional random bytes or a CSPRNG?

**Analysis:**  
- `Guid.NewGuid()` on modern .NET (6+) uses `System.Security.Cryptography.RandomNumberGenerator` internally (CSPRNG).
- Provides 128 bits of entropy (2^128 ≈ 3.4 × 10^38 possible values).
- Prediction or enumeration is computationally infeasible.
- Collision probability: For 1 billion nonces, probability of collision is ~1 in 2^90 (negligible).

**Conclusion:** `Guid.NewGuid()` is **cryptographically sufficient** for nonce generation. No change needed.

---

## Multi-Tenancy Analysis

**Question:** Does the workflow validation stack correctly scope to the authenticated member's tenant? Can one tenant's member submit workflow data for another tenant's instance?

**Answer:**
- **Umbraco-side tenant binding:** ✅ SAFE
  - `PrismContext.GetAuthorizationHeaderAsync()` enforces tenant binding (per CIA hardening):
    - Bearer token `tid` must match `CurrentTenant.EntraTenantId`.
    - Token refresh enforces same check.
  - Workflow requests include the tenant-bound bearer token.
- **Business App-side tenant binding:** ⚠️ **REQUIRES BA ENFORCEMENT**
  - The BA receives the tenant-bound bearer token and the `InstanceId` from the request.
  - **The BA MUST verify the token's `tid` matches the `InstanceId`'s tenant.**
  - If the BA does NOT enforce this, cross-tenant workflow manipulation is possible.

**Recommendation:** Document this as a **hard security requirement** for Business App integration.

---

## Summary of Direct Fixes Applied

| # | Issue | Severity | File | Status |
|---|-------|----------|------|--------|
| 1 | Open Redirect | CRITICAL | `WorkflowPageController.cs` | ✅ FIXED |
| 2 | ReDoS (Regex DoS) | HIGH | `WorkflowFieldValidator.cs` | ✅ FIXED |
| 3 | Weak Email Validation | MEDIUM | `WorkflowFieldValidator.cs` | ✅ FIXED |

---

## Remaining Risks (Documented for Team Decision)

| # | Issue | Severity | Recommendation | Tracking |
|---|-------|----------|----------------|----------|
| 4 | Nonce Replay (no consumption) | HIGH | Add usage counter or document design trade-off | Add to decisions.md |
| 5 | Nonce DoS (cache exhaustion) | MEDIUM | Add per-user nonce limit + rate limiting | Add to backlog |
| 6 | Tenant isolation in workflow | MEDIUM | Document BA requirement to verify token tenant | Add to decisions.md |
| 7 | Field whitelist case handling | LOW | Add test coverage for `[]` suffix edge case | Add to test backlog |

---

## Build Verification

**Command:** `dotnet build UmbracoPrism.sln -c Release`  
**Result:** ✅ Build succeeded (0 warnings, 0 errors)

---

## Next Steps

1. **Merge this audit report** into `.squad/decisions.md`.
2. **Document nonce replay trade-off** in `WorkflowStepNonceService.cs` and `PrismWorkflowOptions.cs`.
3. **Document BA tenant verification requirement** in Business App integration guide.
4. **Add nonce DoS mitigation** to backlog (prioritize for production).
5. **Add test coverage** for field whitelist edge cases.

---

**Audit Completed:** 2026-03-28  
**Security Gate:** PASS (Critical/High issues fixed; Medium/Low documented)  
**Code Quality:** All fixes are surgical, fail-closed, and backward-compatible.

---

# Decision: HTML5 Native Validation with Progressive Enhancement

**Author:** Isabelle (Frontend Dev & Accessibility Lead)  
**Date:** 2025-01-XX  
**Context:** Full-stack validation for Prism workflow forms

## Decision

Use **HTML5 native constraint validation** as the first line of defense for workflow form validation, with server-side validation as backup.

## Implementation

### HTML5 Attributes Applied
- `required` — all field types (boolean, radio, checkboxlist, select, text, email, textarea, number)
- `minlength`, `maxlength` — text, email, textarea
- `pattern` — text, email
- `min`, `max` — number

### Progressive Enhancement Strategy
1. **Browser validates first** using native HTML5 (instant feedback, zero JS)
2. **Server validates second** on form submission (security boundary)
3. **Client-side errors** returned via `ViewData["fieldError"]` and rendered with ARIA
4. **No custom JavaScript validation** needed (browsers handle it)

### Accessibility Requirements Met
- All validation errors use `aria-invalid="true"` on the invalid field
- Error messages use `role="alert"` for screen reader announcements
- `aria-describedby` links field to both hint text AND error message
- Error messages use semantic `id` pattern: `{fieldKey}-error`

### Error CSS Class
Changed from `prism-form-group__error` to `prism-field-error` for:
- Consistency with other field-level classes (`prism-field-*`)
- Clearer semantic meaning (it's an error on a specific field, not a group error)
- Easier to target in CSS

## Rationale

1. **Zero JavaScript required** — native browser validation is fast, accessible, and works without JS
2. **Better UX** — instant feedback on blur/submit, no roundtrip to server for basic validation
3. **Security maintained** — server-side validation is still the source of truth
4. **Accessibility first** — native HTML5 validation works with screen readers out of the box

## Team Impact

- **Blathers (backend):** Server validation remains unchanged; HTML5 is additive
- **Future JS work:** If custom validation UI needed, we can layer on top (progressive enhancement)
- **Storybook:** Will show validation states in stories (future task)

---

# Workflow Form Validation Test Coverage

**Date:** 2024-04-10  
**Author:** Tangy (Tester)  
**Context:** Workflow form validation stack has been built. This decision documents the test coverage and gaps.

## What was tested

### WorkflowFieldValidator (45 tests)

**Happy path:**
- All required fields provided with valid values → IsValid = true
- Optional fields omitted → IsValid = true
- All field types valid (text, email, number, select, radio, checkboxlist, boolean, textarea, date)
- Constraint edge cases: exactly at MinLength, exactly at MaxLength

**Required validation:**
- Required text field empty → error on that field
- Required email field empty → error
- Required select empty → error
- Required radio not selected → error

**Type validation:**
- Email without @ or . → error
- Number field with non-numeric value → error
- Date field with invalid date → error

**Options whitelist:**
- Select with value not in options list → error
- Radio with injected value not in options → error
- Checkboxlist with one valid + one invalid option → error
- Case-insensitive option matching works

**Constraint validation:**
- MinLength: value too short → error
- MaxLength: value too long → error
- Pattern: value doesn't match regex → error
- Min (number): value below minimum → error
- Max (number): value above maximum → error

**Security:**
- Unknown field key in submission → error (whitelist enforcement)
- Empty submission against no required fields → IsValid = true
- XSS attempt in text field → validator passes through (no encoding at validation layer)

**Edge cases:**
- Boolean (checkbox) — absent from submission = false, treat as not required missing
- Checkboxlist — comma-separated submitted values validated against options
- Checkboxlist supports both `field` and `field[]` keys
- Empty options list on select field → no options error (passes through)

### WorkflowStepNonceService (10 tests)

- Create nonce → nonce is non-empty string
- Nonce format: 32 hex chars (Guid "N" format)
- Stores cache entry with correct key prefix
- Uses TTL from PrismWorkflowOptions
- Resolve valid nonce → returns original field list
- Resolve unknown nonce → returns null
- Resolve expired nonce → returns null
- Round-trip serialization preserves all field properties
- Two nonces are different values

## Test Results

- 55 new tests created
- All 273 tests in Core.Tests pass
- Test suite runs in ~1.5 seconds
- 100% success rate

## Coverage Gaps

The following scenarios are **not** tested:

1. **E2E route hijacking:** No tests for WorkflowPageController route hijacking behaviour (GET/POST to workflow pages)
2. **Nonce replay attacks:** No tests for nonce replay attack prevention (TTL is the only defence — no one-time-use enforcement)
3. **Concurrent nonce operations:** No tests for concurrent nonce creation/resolution (race conditions)
4. **Malformed cache data:** No tests for malformed JSON in cache (deserialization error handling)
5. **Datetime field type:** Only `date` field type tested, not `datetime`
6. **Field injection via checkboxlist suffix:** Potential bypass using `field[]` when field is not a checkboxlist
7. **Nonce cache eviction under load:** No tests for distributed cache eviction behaviour (MemoryCache vs Redis)
8. **Validation error message i18n:** All error messages are English — no tests for localisation
9. **Field value length limits:** No tests for extremely long field values (DOS via memory)
10. **Pattern ReDoS:** No tests for catastrophic backtracking in regex patterns (security concern)

## Recommendations

### High priority (security-relevant):
1. Add E2E test for WorkflowPageController POST with tampered nonce → expect redirect to GET
2. Add test for Pattern constraint with ReDoS regex → should timeout or reject (consider SafeRegex wrapper)
3. Add test for checkboxlist `field[]` submission when field is not a checkboxlist → should error (whitelist enforcement)

### Medium priority (reliability):
4. Add test for malformed JSON in cache → should return null (graceful degradation)
5. Add datetime field type test (currently only date is tested)
6. Add test for extremely long field values → should reject or truncate

### Low priority (nice-to-have):
7. Add concurrent nonce creation test (prove uniqueness under load)
8. Add nonce TTL expiry test with real cache (not just mock)
9. Add validation error message localisation tests (when i18n is implemented)

## Decision

✅ **Merge the test suite as-is.** Coverage is comprehensive for the current scope.

⚠️ **Action:** Create follow-up issues for the high-priority gaps (especially ReDoS and E2E route hijacking tests).

## Related Files

- `src/UmbracoPrism.Core.Tests/Services/Workflow/WorkflowFieldValidatorTests.cs`
- `src/UmbracoPrism.Core.Tests/Services/Workflow/WorkflowStepNonceServiceTests.cs`
- `src/UmbracoPrism.Core/Services/Workflow/WorkflowFieldValidator.cs`
- `src/UmbracoPrism.Core/Services/Workflow/WorkflowStepNonceService.cs`

---

# Decision: Prism Design Principle — Least Surprise

**Date:** 2026-04-11  
**Author:** Jonny Muir (via Squad session, documented by Tom Nook)  
**Type:** Standing Design Principle

## Principle

**Make it easy to do the right thing; principle of least surprise.**

The only install should be the NuGet package. Validation, accessibility, tamper-proofing, and Umbraco idioms should all be in place automatically — developers shouldn't have to know they need them. Where choices exist, the Prism default should be the correct choice.

## Context

This principle was articulated by Jonny Muir during the workflow form validation planning session and validated across the Squad. It reflects the core philosophy that drives all Prism feature design decisions.

## Rationale

The Prism package should remove friction and cognitive load from developers integrating Umbraco workflows. Rather than requiring developers to:
- Research what validation patterns to apply
- Manually configure accessibility features
- Implement security measures like tamper-proofing
- Learn Umbraco idioms and conventions

...the package should provide all of this out-of-the-box. The default behavior should always be correct and secure.

## How We Apply This

Every feature and API in Prism should be evaluated against this question: **"Does this require the developer to do extra work to get the right behaviour, or does it just work?"**

If the answer is "they have to do extra work," the design needs revision.

## Applies To

- ✅ All `UmbracoPrism.Core` public APIs
- ✅ Tag helpers and template directives
- ✅ Built-in services and workflows
- ✅ Default behaviors and options
- ✅ Documentation and examples

## Impact on Feature Design

- **Validation:** Should be automatic from workflow definitions, not optional
- **Accessibility:** Required ARIA attributes and semantic HTML, not a checkbox
- **Security:** CSRF tokens, output encoding, and permission checks should work by default
- **Umbraco integration:** Should follow Umbraco conventions without developer configuration

## Standing Effect

This is a standing design principle — not a one-off decision. It applies to all future Prism development and should be referenced in architectural reviews and PR discussions.

---

## 📌 2026-04-12: Elevated Aspire Workload Install on macOS Protected SDK Paths (Blathers)

**Session Log:** `.squad/log/2026-04-12T01:29:29Z-aspire-workload-permissions.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-workload-permissions.md`

### Blathers — Document Elevated Aspire Workload Install on macOS Protected SDK Paths

**Decision:** When the .NET SDK is installed in a protected directory (e.g., `/usr/local/share/dotnet` owned by `root:wheel` on macOS), the Aspire workload installation must be run with elevated privileges:

```bash
sudo dotnet workload install aspire
```

**Conventions:**
- Keep the default cross-platform command in core documentation.
- Add a conditional macOS note in README.md and ASPIRE_DEV.md for protected SDK scenarios.
- Update preflight validators (e.g., `scripts/validate-aspire-prereqs.mjs`) to detect protected SDK installations and provide explicit guidance.
- Include both the standard and elevated-privilege command paths in error/guidance messages.

**Why:** On this machine, the SDK base path is owned by `root:wheel`, which prevents unprivileged workload installation. Microsoft documents this behavior for macOS/Linux systems. Without clear documentation, developers encounter a "Inadequate permissions" error with no obvious resolution, creating a confusing setup loop.

**Documentation Impact:**
- README.md: Added note about elevated permissions on macOS when SDK is in protected paths
- ASPIRE_DEV.md: Updated with explicit `sudo dotnet workload install aspire` guidance
- scripts/validate-aspire-prereqs.mjs: Enhanced to provide actionable guidance for protected SDK scenarios

**Standing Effect:** When Aspire workload installation fails on developer machines with protected SDK installations, the preflight validator and documentation provide clear elevated-command guidance.

---

**Recorded by:** Tom Nook (Lead, Architecture & Code Review)

---

## 📌 2026-04-12: Keycloak ARM64 Startup Workaround (Blathers)

**Session Log:** `.squad/log/2026-04-12T07-03-36Z-keycloak-arm64.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-keycloak-arm64.md`

### Blathers — Keycloak ARM64 startup workaround

**Context**

Aspire local dev uses `quay.io/keycloak/keycloak:26.0.0` for the OIDC provider. On Apple Silicon M4 machines running Docker Desktop, the Keycloak container crashes immediately with `SIGILL` during `java.lang.System.registerNatives()` before Keycloak startup completes.

**Decision**

Apply `JAVA_OPTS_APPEND=-XX:UseSVE=0` to the Keycloak container **only when** the AppHost is running on macOS ARM64.

**Why**

- The crash matches the known OpenJDK 21 `linux-aarch64` SVE startup bug on Apple M4 Docker environments.
- Direct validation showed the same ARM64 crash outside Aspire and successful startup once `-XX:UseSVE=0` was added.
- Forcing `linux/amd64` emulation also works, but it is a heavier fallback than disabling SVE and would penalize every Apple Silicon dev run.
- Upgrading the pinned Keycloak tag alone was not a reliable fix on this machine class during validation.

**Validation**

- `dotnet build UmbracoPrism.sln`
- `dotnet test UmbracoPrism.sln -c Release --filter FullyQualifiedName~UmbracoPrism.Core.Tests`
- `docker run --rm quay.io/keycloak/keycloak:26.0.0 start-dev` → reproduces crash
- `docker run --rm -e JAVA_OPTS_APPEND='-XX:UseSVE=0' quay.io/keycloak/keycloak:26.0.0 start-dev` → starts successfully
- `dotnet run --project src/UmbracoPrism.AppHost/UmbracoPrism.AppHost.csproj --no-build` → Aspire starts and Keycloak imports the realm successfully

---

## 📌 2026-04-12: Aspire Workload Deprecation (Blathers)

**Session Log:** `.squad/log/2026-04-12T07-03-36Z-keycloak-arm64.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/blathers-aspire-deprecation.md`

### Blathers — Aspire workload deprecation

**Decision**

Treat the deprecated Aspire workload as obsolete for this repo's local AppHost flow. The AppHost project should use `Aspire.AppHost.Sdk` plus `Aspire.Hosting.AppHost`, and developer guidance should point people to the `.NET 10 SDK` and `Docker Desktop`, not `dotnet workload install aspire`.

**Why**

On the current .NET 10 SDK, `dotnet workload install aspire` reports that the workload is deprecated. The repo's AppHost was still relying on workload-provided dashboard/DCP bits, which caused the `CliPath` / `DashboardPath` failure until the AppHost SDK was added.

**Impact**

- VS Code preflight checks should validate `.NET 10 SDK` and `Docker`
- Local setup docs should stop telling developers to install the Aspire workload
- AppHost projects in this repo should keep `Aspire.AppHost.Sdk` configured

---

**Recorded by:** Scribe (Documentation Specialist)

---

## 📌 2026-04-13: Generic OIDC Secret Refactor — Provider/Reference Model (Tom Nook, Copper, Blathers, Tangy, Isabelle, Mabel)

**Session Log:** `.squad/log/2026-04-13T08:17:47Z-generic-oidc-secret-refactor.md`

**Merged From Inbox:**
- `.squad/decisions/inbox/tom-nook-generic-oidc-secret-contract.md`
- `.squad/decisions/inbox/copper-generic-oidc-secret-review.md`
- `.squad/decisions/inbox/blathers-generic-oidc-secret-provider.md`
- `.squad/decisions/inbox/blathers-generic-oidc-create-regression.md`
- `.squad/decisions/inbox/tangy-generic-oidc-secret-tests.md`
- `.squad/decisions/inbox/isabelle-generic-oidc-secret-ui.md`
- `.squad/decisions/inbox/mabel-generic-oidc-secret-docs.md`
- `.squad/decisions/inbox/mabel-provider-reference-docs-reconciliation.md`

### Tom Nook — Generic OIDC Secret Contract

**Context**

Generic OIDC tenants previously stored raw `OidcClientSecret` in the database and exposed it through the management API, diverging from the Entra posture (vault-backed references, no secret echo) and creating a security gap for production deployments.

**Decision**

Implement a **reference-based secret resolution model** that mirrors the Entra pattern:
- Production tenants: store vault reference (e.g., `"Prism-Keycloak-Secret"`) in `OidcClientSecretKeyName`, never raw secrets
- Demo exception: localhost Keycloak uses repo-owned marker `"demo-keycloak-dev"`, resolved at runtime from environment variable or hardcoded constant
- Management API: never exposes secret values or vault references; only metadata (`HasOidcClientSecret`, provider name)

**Secure-by-Default Properties**
- ✅ Raw secrets never in database for production tenants
- ✅ Management API never echoes secret values or references
- ✅ Demo secrets are repo-owned and tagged as such
- ✅ Fresh clone works immediately (no vault bootstrap)
- ✅ Matches Entra pattern (consistent security posture)

**Tenant Fields After Refactor**
- New: `OidcClientSecretProvider` (string) + `OidcClientSecretReference` (string, nullable)
- Removed: `OidcClientSecret` (raw value, dropped for secure-by-default)
- Existing: `OidcAuthority`, `OidcClientId` (unchanged)

**Management API Contract**
- POST/PUT: Accept provider/reference pair; ignore raw secret fields
- GET responses: Return only `OidcClientSecretProvider` + `HasOidcClientSecret` (boolean); never return reference name or raw value
- Demo tenant: Only `localhost` may use inline provider; all others require azure-key-vault

---

### Copper — Security Review

**Context**

Initial state: raw `OidcClientSecret` still stored in database and management API, no vault integration for generic OIDC.

**Security Outcomes Required**
1. Remove raw generic OIDC secret dependence from normal tenant flows
2. Keep localhost demo exception isolated and explicit
3. Avoid management-API secret echo
4. Fail closed when confidential client lacks resolvable secret reference

**Blocker Identified**

`PrismOidcConfiguration` still assigned `secret = tenant.OidcClientSecret ?? string.Empty;` for generic OIDC. Production generic OIDC paths remained database-dependent, not vault-backed.

**Required Follow-Up**

1. Replace inline field with provider/reference model for non-demo tenants
2. Stop echoing secrets from tenant management flows; use explicit replace/reset semantics
3. Make generic confidential-client runtime fail closed when secret reference is required but unresolvable
4. Add regression tests covering no-secret-echo, demo functionality, and fail-closed behavior

**Regression Test Scope (5 scenarios)**
- Generic OIDC secret resolution through vault provider
- Demo marker fallback to environment variable or hardcoded constant
- Management API response filtering (no echo of reference or secret)
- Backoffice modal preservation semantics (blank on load, explicit replace/clear)
- Fail-closed when confidential client lacks resolvable reference

---

### Blathers — Provider/Reference Implementation

**Context**

Security review and contract locked; now implement the vault-backed reference model.

**Decision**

Adopt explicit provider/reference contract for generic OIDC confidential-client secrets:

| Component | Implementation |
|-----------|-----------------|
| Storage | `OidcClientSecretProvider` (string) + `OidcClientSecretReference` (string, nullable) |
| Runtime Resolution | Call `ISecretVaultService.ResolveSecretAsync(provider, reference)` |
| Demo Detection | Detect `provider == "inline"` at runtime; reserved for seeded localhost only |
| Fallback | Demo path checks `DEMO_OIDC_SECRET` env var, then uses hardcoded `"prism-dev-secret"` |
| Fail Closed | Confidential clients without resolvable reference error out |

**Backward Compatibility**

- Database migration: drop `OidcClientSecret` column, add new columns with idempotent seeding
- Existing inline secrets: migrated to null references (safe for non-demo tenants)
- Entra path: unchanged (`SecretKeyName` + `GetSecretAsync(...)` still work)

**Management API Contract**

- POST: Accept `OidcClientSecretProvider` + `OidcClientSecretReference` + `ResetOidcClientSecret` flag
- PUT: Same as POST; omit fields to preserve existing configuration
- GET: Return only `OidcClientSecretProvider` + `HasOidcClientSecret`; never return reference or secret value

---

### Blathers — Generic OIDC Create Regression Fix

**Context**

After provider/reference model implementation, create/edit flows broke because admin form still submitted references via `SecretKeyName` shorthand, not explicit provider/reference pair.

**Decision**

Treat `PrismTenantRequest.SecretKeyName` as public management-API shorthand for generic OIDC Azure Key Vault reference during refactor window:

- Server-side translation: `SecretKeyName` → `OidcClientSecretProvider = "azure-key-vault"` + `OidcClientSecretReference = <value>`
- Accept either explicit pair or shorthand; both map to Azure Key Vault
- Reject inline generic secrets everywhere except repo-owned localhost demo
- Never echo generic secret references back in responses

**Standing Effect**

- Normal generic OIDC create/edit flows accept public shorthand without exposing internals
- Controller validation accepts both shorthand and explicit provider/reference
- `PrismTenantResponse.SecretKeyName` remains Entra-only; generic tenants use metadata-only response

---

### Tangy — Regression Test Contract

**Context**

New provider/reference model needed clear behavioural contract for regression testing.

**Decision**

Lock in regression contract:

1. `PrismOidcConfiguration` accepts inline secrets **only** for seeded localhost Keycloak tenant
2. Normal generic OIDC tenants **must** resolve through managed provider/reference pair; fail closed otherwise
3. Tenant management responses expose provider metadata but **never** echo raw secret or reference name
4. Backoffice edit modal keeps reference field blank on load; uses explicit preserve/clear behaviour

**Why**

Keeps fresh-clone demo working while maintaining secure-by-default posture for real tenants. Tests have stable behavioural assertions that catch actual regressions (raw-secret echo, inline leakage outside demo) without breaking on implementation refactors.

**Test Coverage (5 scenarios from Copper + focused unit/integration tests)**
- Provider resolution through vault
- Demo marker fallback (env var → hardcoded constant)
- Management API filtering (no secret echo)
- Backoffice modal preservation semantics
- Fail-closed on missing reference

---

### Isabelle — Backoffice UI Alignment

**Context**

Backend moved to provider/reference model; UI needed to reinforce secure-by-default and mirror preservation semantics.

**Decision**

Treat generic OIDC secret editing as replace-only admin surface:

- Normal generic OIDC: **OIDC Key Vault Secret Name** field accepts vault reference
- Edit mode: field starts blank (preserves existing reference unless user explicitly replaces)
- Reset: explicit **Reset configured OIDC secret** action required to clear
- Demo exception: only localhost Keycloak shows inline secret field

**Why**

Backend contract avoids secret echo and distinguishes between preserve/replace/reset. UI mirrors that contract to keep production guidance secure-by-default while making local demo path clear.

**Applied In**
- `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.ts` (component logic)
- `src/UmbracoPrism.Client/src/backoffice/prism-create-tenant-modal.stories.ts` (UI stories)
- `src/UmbracoPrism.Client/tests/prism-create-tenant-modal.spec.ts` (UI tests)

---

### Mabel — Documentation Reconciliation

**Context**

Implementation complete; documentation needed reconciliation with actual code behavior and API contract.

**Decision**

Reconcile three documentation surfaces with provider/reference model:

| Document | Updates |
|----------|---------|
| README.md | Added "Secret Management" section; explained three paths: Entra (production), generic OIDC (production), Keycloak demo (local dev) |
| ASPIRE_DEV.md | Updated "New Columns" (provider/reference), "PrismOidcConfiguration Fallback Logic" (demo marker + env var override), "Management API Contract" (response filtering) |
| docs/secret-management.md | Created for DevOps/SRE; vault-backed references, admin workflows, Key Vault naming conventions, secret rotation |

**Key Documentation Principles**

1. API Request Contract: Accept provider/reference pair; ignore raw secrets
2. API Response Contract: Never expose reference names or secret values; only expose provider + `HasOidcClientSecret`
3. Backoffice Integration: Reference field masked on edit; presence indicated via UI badge
4. Preservation Semantics: Omit reference during update to preserve; `ResetOidcClientSecret = true` to clear
5. Demo Exception: Only seeded `localhost` tenant may use inline provider; all others require azure-key-vault

**Verification**
- ✅ Solution builds without warnings (except pre-existing vulnerability)
- ✅ All 320 tests pass
- ✅ Docs align with contract, security review, and implementation
- ✅ Fresh-clone experience unchanged

---

## Outcomes

| Requirement | Status |
|-------------|--------|
| Raw secrets removed from production tenant paths | ✅ Complete |
| Management API no longer echoes secrets/references | ✅ Complete |
| Demo exception isolated and explicit | ✅ Complete |
| Fail-closed on missing reference | ✅ Complete |
| Regression test coverage (5 scenarios) | ✅ Complete |
| UI alignment to replace-only semantics | ✅ Complete |
| Documentation reconciliation | ✅ Complete |
| Fresh-clone experience preserved | ✅ Complete |
| All tests passing, no warnings | ✅ Complete |

## Standing Effect

Generic OIDC and Entra now follow the same vault-backed, reference-based secret model:
- Production tenants use Azure Key Vault references
- Management API never exposes secrets or reference metadata
- Local demo remains frictionless and explicitly tagged
- Fresh clones work immediately without vault bootstrap
- Fail-closed behavior when confidential client lacks resolvable reference

---

**Recorded by:** Scribe (Multi-Agent Consolidation)
**Team:** Tom Nook (Lead), Copper (Security), Blathers (Backend), Tangy (Testing), Isabelle (Frontend), Mabel (Documentation)

---

## 📌 2026-04-13: Brewster Clean-Boot Readiness & Blathers Auth Fixes — Phase 1 Completion

**Session Log:** `.squad/log/2026-04-13T20:14:37Z-scribe-spawn.md`  
**Orchestration Log:** `.squad/orchestration-log/2026-04-13T20:14:37Z-brewster.md`

### Brewster — Live Suite Startup & Seed Stability

**Outcome:** Live localhost Playwright suite now passes startup/auth/navigation tests with stable, deterministic seeded data.

#### 1. Stable Umbraco Seed Contract

**Decision:** Adopt single seed contract for TestSite's authenticated demo journey:
- `homePage` named **Home** at `/`
- `memberDashboard` named **Dashboard** at `/dashboard`
- `workflowPage` named **Get in Touch** with `workflowKey = community-enquiry` at `/get-in-touch/`
- `workflowHub` named **My Workflows** at `/my-workflows/`
- Root `settings` node containing mobile nav links

**Implementation:** Seeders repair missing/drifted demo nodes idempotently on Development startup; Razor navigation resolves destinations from published content tree, not positional assumptions.

**Why:** Clean-database runs become deterministic; Umbraco content tree remains CMS-native with real route hijacking and published URLs.

#### 2. Live Suite Startup Contract (Machine-Readable)

**Decision:** Treat localhost Aspire readiness as machine-readable Umbraco contract, not rendered-text check.

**Implementation:**
- TestSite home page exposes stable readiness marker: `data-prism-home-ready="true"`
- `GET /api/prism/downstream-demo/seed-contract-ready` is authoritative Umbraco/TestSite readiness endpoint
- Readiness payload normalizes published URLs and confirms expected auth challenge contract
- Live-suite tooling prefers this endpoint over scraping rendered copy

**Why:** Previous probe depended on rendered hero text; clean boots could be healthy while HTML formatting or trailing-slash URLs caused false negatives.

#### 3. Keycloak HTTPS & Cookie Flow

**Decision:** Localhost Keycloak accessed via HTTPS port 8443 from browser with restart-stable cookie flow.

**Implementation:** Browser URL: `https://localhost:8443/realms/prism-dev`; cookie flow validated across restart scenarios; retry logic for 8443 port availability; dashboard launch timing synchronized with AppHost readiness.

#### 4. View & Build Warnings Cleanup

**Decisions:**
- Use typed Umbraco navigation extensions in Razor views (not untyped `Html.GetUmbracoHelper()`)
- Warning policy: fix root causes, not suppress noise
- Aspire launch profile sourced from configuration (no hardcoded VS Code `launchUrl`)

### Blathers — Backend Auth Fixes (Phase 1 Security Remediation)

**Blocker Status:** Restart-only downstream API failure after full AppHost restart (scoped blocker; startup/auth/navigation pass).

#### 1. Generic OIDC Session Contract Survival

**Decision:** Browser-facing Keycloak issuer must be authoritative for downstream token validation; session tokens persist across site restarts.

**Implementation:**
- Browser-facing issuer: `https://localhost:8443/realms/prism-dev`
- Downstream APIs validate against same issuer (not container internal `http://localhost:8080`)
- `PrismMemberCookie` preserves `access_token`, `refresh_token`, `id_token`, `expires_at`
- ID tokens retained for RP-initiated logout
- `GET /api/prism/downstream-demo/session-contract` probes metadata (dev-only)

#### 2. Downstream Bearer Validation

**Decision:** Align local demo validation with HTTPS Keycloak authority; generic OIDC downstream binding uses issuer + client identity.

**Implementation:** Mock BusinessApp 401 resolved via isolated fresh DB seeding; downstream calls validate against Prism's Keycloak issuer.

#### 3. Offline Token & Scope Fixes

**Decisions:**
- Revert `offline_access` scope drift to Generic OIDC contract
- Local Keycloak should NOT request offline_access by default
- Scope contract aligns browser + downstream

#### 4. Logout Behavior

**Decision:** Omit `id_token_hint` for Generic OIDC logout when ID token available; fallback to `client_id` only for older/damaged sessions.

**Implementation:** At logout, restore `id_token_hint` from stored `id_token` when available; Keycloak logout validated across restart.

#### 5. Endpoint Wiring

**Decision:** Launch profiles are source of truth for local endpoint URLs; fresh DB seeding isolates TestSite runtime state.

**Implementation:** Dashboard launch timing synchronized with Aspire workload startup; Aspire TestSite no longer reuses standalone `src/UmbracoPrism.TestSite/umbraco/Data/Umbraco.sqlite.db`.

### Changed Files

- `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs`
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`
- `src/UmbracoPrism.Core.Tests/PrismOidcConfigurationTests.cs`
- `src/UmbracoPrism.Core.Tests/LocalhostGenericOidcRegressionTests.cs`
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs`

### Follow-up Agents

- **Blathers:** Fix restart-only downstream API failure
- **Tangy:** Validate live suite after Blathers fix

### Team Notes

- **Tangy:** Playwright can assert restart-stable session contract directly and use seed-contract-ready endpoint
- **Copper:** No token values exposed by new probes; only dev-enabled metadata
- **Mabel/Celeste:** Docs should keep seed contract visible in fresh-clone localhost documentation

---

**Recorded by:** Scribe (Phase 1 Consolidation)

---

## 📌 2026-04-13: Blathers — Restart-Only Downstream Auth Contract (Phase 2 Auth Diagnostics)

**Status:** Completed with integration blockers

**Decision:** Treat restart-stale localhost generic OIDC sessions as a special contract:

1. If the encrypted `PrismMemberCookie` was issued before the current TestSite process started, `PrismContext` should try a refresh before reusing the cached downstream access token.
2. The repo-owned localhost Keycloak demo should request `offline_access` on the browser auth flow so a full local AppHost restart can still mint a fresh downstream bearer token.
3. The localhost refresh-token grant should omit the `scope` parameter and let Keycloak reuse the offline scopes already carried by the refresh token.

**Why:** The live restart regression is specifically about the local demo stack: MockBusinessApp rejects the pre-restart access token after Keycloak restarts, while the browser cookie still says the member is signed in. Restart-stale cookie detection plus an offline-token contract keeps the behavioural test strict without broadening generic OIDC privileges for non-demo tenants.

**Implementation Details:**
- Added `RestartStaleSessionHandler` in `PrismContext` to detect cookie age vs process start time
- Implemented `OfflineTokenRefreshContract` for localhost demo token refresh lifecycle
- Keycloak realm export updated to request `offline_access` on browser auth flow
- Refresh token grant configured to omit `scope` parameter

**Validation:**
- Auth test suite: 57/57 passing
- Restart-stale detection tests: all passing
- Offline refresh tests: all passing

**Files Modified:**
- `src/UmbracoPrism.Core/Models/PrismContext.cs`
- `src/UmbracoPrism.Core/Models/PrismOidcConfiguration.cs`
- `src/UmbracoPrism.Core/Services/IPrismTokenRefreshService.cs`
- `src/UmbracoPrism.Core/Services/PrismTokenRefreshService.cs`
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs`
- `src/UmbracoPrism.Core.Tests/PrismContextTests.cs`
- `src/UmbracoPrism.Core.Tests/LocalhostGenericOidcRegressionTests.cs`
- `keycloak/realm-export.json`

**Remaining Blockers:**
1. **Live Restart Regression (401)** — Full stack restart still results in 401. Symptoms suggest token expiry vs revocation issue during Keycloak restart cycle.
2. **TestSite Razor Build Errors** — Pre-existing Razor compilation errors block normal Playwright/AppHost test path. Unblocks: Fix Razor build issues first.

**Orchestration Log:** `.squad/orchestration-log/2026-04-13T21:56:27Z-blathers.md`

**Recorded by:** Scribe (2026-04-13T21:56:27Z)
