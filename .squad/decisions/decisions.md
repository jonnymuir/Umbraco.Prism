# Decisions

## Decision: Direct Main Branch Workflow

**Date:** 2026-03-28  
**Agent:** Copilot (via user directive)  
**Status:** Active

### What Was Decided

Work directly on main branch, one issue at a time. No pull requests — commits go straight to main after work is complete.

### Why This Matters

Solo developer project context. PR overhead is unnecessary when individual is responsible for all decisions and testing. Enables faster iteration while maintaining code quality through direct validation and commit practices.

---

## Decision: PrismDeviceCredential Schema Choices (Issue #12)

**Author:** Blathers (Backend Dev)  
**Date:** 2026-03-28  
**Issue:** #12 — Phase 1: prismBiometricTokens DB table + migration

### Type Choices

| Field | Type | Rationale |
|---|---|---|
| `TenantId` | `nvarchar(450)` | Logical tenant string identifier (not int FK) per issue spec; matches Umbraco identity column sizing |
| `UserId` | `nvarchar(450)` | Entra OID stored as string; 450 is Umbraco's standard for identity keys |
| `DeviceId` | `nvarchar(64)` | Client UUID as string, 36 chars + headroom |
| `TokenHash` | `nvarchar(512)` | SHA-256 hex (64 chars) with headroom for algorithm prefixing |
| `RegisteredAt` | `datetime2` + `getutcdate()` default | UTC enforced; `datetime2` is higher precision than `datetime` |
| `FailedAttempts` | `int` + default `0` | Rate-limiting counter; int sufficient for any realistic limit |
| `Platform` | `nvarchar(50)` | Bounded enum-like values ('ios', 'android'); validated at application layer |

### Index Rationale

| Index | Type | Rationale |
|---|---|---|
| `(TenantId, DeviceId)` | UNIQUE | Enforces one credential entry per device per tenant at DB level |
| `(TenantId, UserId)` | Non-unique | Supports listing/revoking all devices for a user within a tenant |
| `(TokenHash)` | Non-unique | Exchange endpoint hashes the incoming JWT and looks up the record; hot path |

### Composite Index Approach

The Umbraco `[Index]` NPoco annotation only supports single-column indexes. Composite indexes were created via `Database.Execute()` raw SQL inside the migration class. This is consistent with the Umbraco migration pattern and safe because the `TableExists` guard ensures idempotency.

### What Was Deferred

- `RefreshTokenEnc` field (from the original design doc SQL) is not in this phase; the issue spec omits it and it belongs to the `/exchange` service implementation, not the registry schema.
- Per-tenant expiry configuration (7–90 day range) is an application-layer concern; the `ExpiresAt` column stores the computed value set at registration time.

---

## Decision: Native Biometric Platform Configuration

**Date:** 2026-01-25  
**Author:** Kicks (Mobile Native Specialist)  
**Context:** Issues #20, #21 — iOS and Android biometric platform config in MobileBundleService

### Decision

The `MobileBundleService` now conditionally injects platform-specific biometric configuration into generated mobile app bundles when the `BiometricAuthEnabled` flag is set to true.

### iOS Configuration
- **Info.plist Key:** `NSFaceIDUsageDescription` with usage string
- **Injection Method:** `plutil -insert` command in bootstrap-ios.sh script
- **When:** After `npx cap add ios` but before app build/run
- **Rationale:** FaceID requires explicit usage description in Info.plist for App Store approval; TouchID does not

### Android Configuration
- **Manifest Permission:** `android.permission.USE_BIOMETRIC`
- **Injection Method:** `sed` insertion before `<application>` tag in bootstrap-android.sh script
- **When:** After `npx cap add android` but before app build/run
- **API Level:** Targets API 28+ (BiometricPrompt API); no need for deprecated `USE_FINGERPRINT` permission

### Plugin Dependencies
When `BiometricAuthEnabled` is true, package.json includes:
- `@aparajita/capacitor-biometric-auth@^7.0.0` — biometric authentication prompts
- `@aparajita/capacitor-secure-storage@^7.0.0` — secure Keychain/Keystore access

**Plugin Selection Rationale:** `@aparajita` packages chosen over `@capacitor-community` alternatives for:
- Capacitor 7 compatibility
- Active maintenance
- Superior iOS Keychain and Android Keystore mapping
- Consistent API surface from same author

### Implementation Details

Both iOS and Android bootstrap scripts follow this pattern:
1. Check if the platform-specific file exists
2. Check if the required entry is already present (idempotent)
3. If not present, inject using platform-appropriate tool (`plutil` for iOS plist, `sed` for Android XML)
4. Provide clear feedback to developer

This approach ensures the scripts can be run multiple times without duplication and gracefully handle cases where the platform hasn't been added yet.

### Future Considerations

- If the tenant disables biometric auth after a bundle is generated, developers must manually remove the permissions or regenerate the bundle
- The `BiometricAuthEnabled` flag is currently a simple boolean; future enhancements might allow for platform-specific toggles (iOS-only, Android-only)
- No Capacitor config changes needed — plugins auto-register via Capacitor's discovery mechanism

### Testing Notes

The configuration injection happens during the bootstrap script phase, which occurs on the developer's machine after bundle extraction. This means:
- No server-side testing needed for the injection itself
- Testing requires full Capacitor app generation and platform addition
- Verification: check generated Info.plist and AndroidManifest.xml after running bootstrap scripts

---

## Decision: GitHub Release Workflow Convention

**Date:** 2026-03-29  
**Agent:** Blathers (Backend Dev)  
**Status:** Implemented

### What Was Decided

Adopt automated GitHub Release creation as part of the `package-release.yml` workflow, triggered unconditionally on every `v*` tag push.

### Conventions

- **Permissions:** The `pack` job requires `permissions: contents: write` at job level to allow `GITHUB_TOKEN` to create GitHub Releases.
- **Release action:** Use `softprops/action-gh-release@v2` — the standard well-maintained action. Set `draft: false`, `prerelease: false`, `generate_release_notes: false`.
- **Release name:** Use `github.ref_name` (e.g. `v1.2.0`) as the release title.
- **Release body:** Extract from `CHANGELOG.md` using the `awk` pattern:
  ```sh
  awk "/^## \[${TAG}\]/{found=1; next} found && /^## \[/{exit} found{print}" CHANGELOG.md
  ```
  This captures everything between the current tag's heading and the next `## [` heading, matching Mabel's CHANGELOG format (`## [vX.Y.Z] — YYYY-MM-DD`).
- **Assets:** Attach `artifacts/*.nupkg` to the release.
- **Gate:** GitHub Release creation is **not** gated on `NUGET_API_KEY`. NuGet publish remains gated (`if: ${{ env.NUGET_API_KEY != '' }}`).

### Why This Matters

Publishing a GitHub Release alongside the NuGet package gives users a changelog-rich release page on GitHub without manual steps. Extracting from `CHANGELOG.md` ensures the release body matches the canonical team-maintained changelog written by Mabel (Scribe). Keeping it unconditional means a release is always created even if NuGet publish is skipped.

---

## Decision: Comprehensive Copilot Instructions Created

**Date:** 2026-03-22  
**Agent:** Docsmith (Documentation Specialist)  
**Status:** Implemented

### What Was Decided

Created `.github/copilot-instructions.md` as a central reference for future Copilot sessions working on Umbraco Prism.

### Why This Matters

Umbraco Prism is a complex multi-tenancy package with:
- **Mixed stack:** .NET 10 Core + Node.js 22 Client (web components)
- **Multiple testing frameworks:** XUnit (C#) + Playwright (TypeScript)
- **Architectural subtlety:** Middleware-driven tenant resolution, stateless OIDC, mobile app generation
- **Team conventions:** Not obvious from a single file (scattered across Middleware/, Services/, Auth/)

Future Copilot sessions will spend less time exploring and more time implementing, reducing rework and ensuring consistency.

### What Was Included

1. **Build/Test/Lint Commands** (284 lines total)
   - All commands that actually exist in CI workflows and local development
   - Prerequisites (Node.js 22.17.1, .NET 10.0.x)
   - How to run single tests (XUnit filter syntax, Playwright UI mode)

2. **High-Level Architecture**
   - 7 interconnected layers (Runtime, Identity, Persistence, Services, Authorization, Backoffice, Sample Projects)
   - Cross-references to physical file locations (Services/, Middleware/, Persistence/)
   - Diagram-free but explicit: describes responsibilities and integration points

3. **Key Conventions**
   - Code organization (why each folder exists)
   - Naming rules (IPrismXxx, XxxService, PrismXxxMiddleware)
   - Database/migration patterns
   - Mobile feature conventions (Produce Mobile, safe-area support)
   - Admin policy reasoning
   - Secrets management (Key Vault per tenant)

4. **Common Tasks & Reference Tables**
   - How to add a new service
   - How to run tests locally
   - Debugging mobile bundles
   - Local Entra sign-in walkthrough
   - Accessibility requirements
   - Dependencies table (versions, notes)
   - File/directory reference table

### Integration with Existing Project

- No .github/copilot-instructions.md existed; created from scratch
- Drew from: README.md (architecture, features), package.json (build script names), .csproj files (test frameworks, versions), CI workflows (actual command syntax)
- Verified against: src/ structure, Services/, Middleware/, Persistence/ organization

### Follow-Up

- Scribe history updated with high-level learnings
- File is self-contained; no dependencies on external docs
- Can be incrementally improved as project evolves (add sections as new patterns emerge)

### Rationale

This document is **not** generic Copilot advice (no "make atomic commits" or "use descriptive variable names"). It is purely project-specific, answering: "What is the shape of this codebase, and how does work get done here?"

---

## Decision: Content Type & Starter Content Seeders

**Date:** 2026-03-29  
**Author:** Blathers (Backend Dev)  
**Status:** Implemented

### What Was Decided

Implemented two notification handlers that run on application startup to eliminate manual Umbraco backoffice setup for new Prism installations.

### 1. PrismContentTypeSeeder (Always Runs)

- **When:** Every startup (idempotent)
- **What:** Creates `homePage` and `memberDashboard` document types if they don't exist
- **Why:** `MemberDashboardController` inherits `RenderController` and requires the `memberDashboard` type to route properly

**Implementation:**
- Uses `IContentTypeService.Get()` guard to check existence
- Only `homePage` has `AllowedAsRoot = true`
- Runs on `UmbracoApplicationStartedNotification` with runtime level check

### 2. PrismStarterContentSeeder (Opt-In)

- **When:** Startup IF `Prism:SeedStarterContent = true` AND content tree is empty
- **What:** Creates "Home" page (homePage type) at root, "Dashboard" page (memberDashboard type) as child, and publishes both
- **Why:** Gives package consumers a working member portal immediately after install

**Implementation:**
- Checks `IContentService.GetRootContent()` — only seeds if empty
- Uses v17 pattern: `Create()` → `Save()` (check `.Success`) → `Publish()`
- Non-destructive: never overwrites existing content

### Configuration Model

Created `PrismConfiguration` class in `/Models/` with:
```csharp
public const string SectionName = "Prism";
public bool SeedStarterContent { get; set; } = false;
```

Registered in `PrismComposer` using `IOptions<T>` pattern (matches existing options models).

### Umbraco v17 API Notes

- **Content types:** `IContentTypeService.Save()` is marked obsolete but functional. New approach uses separate Create/Update methods, but Save still works for both operations.
- **Content creation:** `IContentService.Create()` returns `IContent` directly (not wrapped in result object)
- **Content saving:** `IContentService.Save()` returns `Attempt<T>` with `.Success` property
- **Publishing:** `IContentService.Publish(content, new[] { "*" })` for all cultures

### Testing

- Build: ✅ 0 errors (1 non-blocking deprecation warning)
- Tests: ✅ All 165 tests pass
- Idempotency: Both handlers safe to run repeatedly

### Impact on Other Features

- **Isabelle (Frontend):** Dashboard view files will be discovered automatically via MVC's default view discovery from TestSite's `Views/MemberDashboard/` folder
- **Copper (Security):** No security concerns — seeders only create content types and sample content, no auth/permissions involved
- **Package consumers:** Can now install Prism, set one config flag, and get a working member portal without touching the backoffice

### Files Changed

- `src/UmbracoPrism.Core/Models/PrismConfiguration.cs` (created)
- `src/UmbracoPrism.Core/PrismContentTypeSeeder.cs` (created)
- `src/UmbracoPrism.Core/PrismStarterContentSeeder.cs` (created)
- `src/UmbracoPrism.Core/PrismComposer.cs` (registered handlers + config)
- `src/UmbracoPrism.TestSite/appsettings.json` (enabled seeding)

### Future Considerations

- **Blueprint support:** Deferred due to obsolete API (`CreateContentFromBlueprint` scheduled for removal in v18). Teams can create blueprints manually in backoffice if needed.
- **Additional document types:** If future controllers need more types (e.g., `resetPassword`, `accountSettings`), add them to `PrismContentTypeSeeder.HandleAsync()`.
- **Multi-language seeding:** Current implementation uses `"*"` for all cultures. If specific culture seeding is needed, inject `ILocalizationService` and iterate enabled cultures.

---

## Decision: Umbraco Setup Documentation

**Date:** 2026-03-29  
**Author:** Mabel (Scribe/Documentation)  
**Status:** Implemented

### What Was Decided

Create dedicated Umbraco setup guide and position it clearly in README for developers integrating Prism into new or existing Umbraco installations.

### Documentation Structure

#### New File: `/docs/umbraco-setup.md`

An 8-step guide covering the full integration path:

1. **Install NuGet package**
2. **Register services in Program.cs**
3. **Automatic startup seeding** — explains `PrismContentTypeSeeder` creating `homePage` and `memberDashboard` non-destructively
4. **Content tree structure** — ASCII diagram showing Home → Dashboard hierarchy
5. **Manual setup path** — for existing Umbraco sites (3 steps: create Home, create Dashboard, configure tenant)
6. **Auto-seed path** — for greenfield sites (`"Prism:SeedStarterContent": true` flag)
7. **MockBackOffice demo** — demonstrates downstream credential flow, includes run commands and verification steps
8. **Verification checklist** — concrete success criteria (document types visible, content tree correct, tenant configured, dashboard loads)

#### Updated `README.md`

Added "## Umbraco Setup" section between Architecture and Integration & Usage sections:

- Bullet-point summary of install, document types, content tree, seeding flag, tenant config
- One-liner about MockBackOffice demo
- Link to `/docs/umbraco-setup.md` for detailed guide
- Maintains 5-8 bullet constraint requested

### Documentation Conventions

- **Document type aliases use code formatting:** `homePage`, `memberDashboard`
- **Content tree shown as ASCII diagram** for clarity
- **Non-destructive seeding emphasized:** "Prism does NOT touch existing content tree, members, navigation"
- **Two paths presented equally:** existing sites get manual steps, greenfield sites get auto-seed option
- **Verification-first:** developers know what success looks like before testing
- **Forward references only:** no duplication of Entra setup, mobile generation, or biometric auth (those live in main README Integration & Usage)

### Why This Matters

Blathers' auto-seeding feature is a significant improvement that reduces friction for new users. Without clear setup documentation, developers don't understand:

1. What Prism creates automatically vs. what they must create
2. Whether their existing content/members are safe
3. How to verify success
4. How to test the platform with downstream credential flow

Splitting into a dedicated guide (full reference) + README brief (quick overview) follows the project's onboarding-first philosophy and lets new users get running fast without scrolling a 800+ line README.

### Impact

- **Onboarding clarity:** New developers can follow a linear 8-step path instead of hunting through architecture docs
- **Reduced support questions:** Explicit verification steps + non-destructive seeding guarantee prevent common confusion
- **MockBackOffice adoption:** Dedicated section with run commands + test steps makes the demo discoverable and concrete
- **First-time user experience:** Integration point is now the second thing in README (after Prerequisites), not buried after 600+ lines
