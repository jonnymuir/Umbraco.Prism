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
