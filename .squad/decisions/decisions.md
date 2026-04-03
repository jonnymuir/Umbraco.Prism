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

---

## Decision: Editor-Configurable Mobile Navigation (Brewster Pass 1)

**Date:** 2026-03-29  
**Author:** Brewster (Umbraco Platform Specialist)  
**Status:** Superseded by Settings Node Pattern (Pass 2)

### Context

The TestSite `HomePage.cshtml` had a hardcoded mobile navigation bar with 3 links that varied based on authentication state. This was inflexible and not editor-friendly.

### Decision

Replaced hardcoded mobile nav with an **editor-configurable Multi URL Picker property** on the `homePage` document type.

#### Implementation Details

1. **Property Configuration:**
   - Property alias: `mobileNavLinks`
   - Editor: Umbraco's built-in Multi URL Picker (GUID `fd1e0da5-5606-4862-b679-5d0cf3a52a59`)
   - Property group: "Mobile Navigation"
   - Description: "Configure up to 4 navigation links for the mobile app bottom navigation bar (max 4 items recommended)"
   - Non-mandatory (editors can opt out of mobile nav by leaving it empty)

2. **Seeder Pattern:**
   - Extended `PrismContentTypeSeeder.cs` with `EnsureMobileNavPropertyAsync()` method
   - Uses Umbraco's built-in Multi URL Picker rather than creating custom data types (avoids complex property editor instantiation)
   - Idempotent: checks if property exists before adding
   - Added `IDataTypeService` to constructor dependencies

3. **View Pattern:**
   - Created reusable partial: `Views/Partials/_MobileShellNav.cshtml`
   - Model: `@model IEnumerable<Umbraco.Cms.Core.Models.Link>`
   - Handles null/empty gracefully (renders nothing)
   - Detects active link by comparing `link.Url` to `Context.Request.Path`
   - Preserves existing CSS classes (`.mobile-shell-nav`, `.mobile-shell-nav__item`, `.mobile-shell-nav__item--active`)

4. **HomePage Integration:**
   - Reads property via `Model.Value<IEnumerable<Link>>("mobileNavLinks")`
   - Passes to partial via `@Html.Partial("_MobileShellNav", mobileNavLinks)`
   - Replaced 15 lines of hardcoded HTML with 3 lines of Razor

### Rationale

- **Umbraco-idiomatic:** Multi URL Picker is the standard Umbraco pattern for configurable navigation - any Umbraco developer will recognize this immediately
- **Editor-friendly:** Backoffice UI for adding/reordering links with content tree picker
- **Flexible:** Editors can configure 1-4 links, or omit mobile nav entirely
- **Reusable:** Partial can be used on other pages (e.g., MemberDashboard)
- **No custom UI needed:** Uses existing Umbraco backoffice functionality

### Consequences

#### Positive
- Mobile nav is now fully editor-controlled
- No code changes needed to modify navigation structure
- Pattern is extensible to other pages
- Test site demonstrates proper Umbraco content type configuration

#### Considerations
- Editors must configure mobile nav links after creating a HomePage node (property starts empty)
- Built-in Multi URL Picker has no enforced max limit - description recommends 4 items but doesn't restrict
- If more complex validation needed (e.g., strict 3-4 item limit), a custom data type would be required

### Alternatives Considered

1. **Create custom data type with maxNumber: 4 config**
   - Rejected: Complex to create programmatically in v17 (requires property editor instantiation with multiple dependencies)
   - Built-in data type is simpler and "good enough"

2. **Keep hardcoded nav with config file override**
   - Rejected: Not Umbraco-native; editors expect backoffice control

3. **Custom Angular backoffice editor**
   - Rejected: Overkill for simple link list; Multi URL Picker already provides ideal UX

### Related Files

- `src/UmbracoPrism.Core/PrismContentTypeSeeder.cs` (lines 52-104)
- `src/UmbracoPrism.TestSite/Views/Partials/_MobileShellNav.cshtml`
- `src/UmbracoPrism.TestSite/Views/HomePage.cshtml` (lines 472-475)

---

## Decision: Settings Node Pattern for Site-Wide Configuration (Brewster Pass 2)

**Date:** 2026-03-29  
**Author:** Brewster (Umbraco Platform Specialist)  
**Status:** Implemented

### Context

Pass 1 added `mobileNavLinks` as a per-page property on `homePage`. This works but doesn't scale — every new doc type would need the same property. The standard Umbraco community pattern for site-wide settings is the Settings Node Pattern (Paul Seal).

### Decision

We will use the **Settings Node Pattern** — a standard Umbraco community approach for site-wide configuration:

1. Create a `settings` document type with:
   - `AllowedAsRoot = true`
   - `Icon = "icon-settings-alt"`
   - No template (it's a config node, not a rendered page)
   - Property groups for various settings (e.g., "Mobile Navigation")

2. Seed a single root-level Settings node via `PrismStarterContentSeeder`

3. Master layout reads Settings at the top:
   ```csharp
   var settings = Umbraco.ContentAtRoot().FirstOrDefault(x => x.ContentType.Alias == "settings");
   var mobileNavLinks = settings?.Value<IEnumerable<Link>>("mobileNavLinks");
   ```

4. All site-wide UI (mobile nav, footer, etc.) renders from Master using Settings data

### Rationale

- **Single source of truth:** Editors configure once, all pages inherit
- **No per-page duplication:** New doc types don't need the same properties
- **Standard Umbraco pattern:** Recognized by any Umbraco developer
- **Separation of concerns:** Master handles site-wide UI, pages handle content
- **Scalable:** Easy to add more site-wide settings (footer links, social media, contact info, etc.)

### Alternatives Considered

- **Per-page properties:** Doesn't scale; every doc type needs the same property
- **Configuration files:** Not editor-friendly; requires deployment for changes
- **Composition:** Over-engineering for simple site-wide config

### Files Modified

- `src/UmbracoPrism.Core/PrismContentTypeSeeder.cs` — added Settings doc type creation, refactored mobile nav property logic
- `src/UmbracoPrism.Core/PrismStarterContentSeeder.cs` — seeds Settings node alongside Home
- `src/UmbracoPrism.TestSite/Views/Shared/Master.cshtml` — reads Settings, renders mobile nav globally
- `src/UmbracoPrism.TestSite/Views/HomePage.cshtml` — removed per-page nav logic and CSS

### Impact

- Existing installations: Next startup will create Settings doc type; seeder won't run (content already exists)
- New installations: Get Settings node automatically when `SeedStarterContent = true`
- Editors: Configure mobile nav once in Settings node, not per-page
- Developers: Extend Settings with new properties as needed (footer, social, etc.)

---

## Decision: Data Type Editor Lookup Pattern

**Date:** 2026-03-29  
**Agent:** Brewster (Umbraco Platform Specialist)  
**Context:** Bug fix for incorrect data type editor creation in seeder

### Pattern

**DO NOT** use hard-coded GUIDs to look up or clone Umbraco property editors.  
**ALWAYS** use `PropertyEditorCollection[alias]` to get editors by their string alias.

```csharp
public class MySeeder(PropertyEditorCollection propertyEditorCollection, ...) 
{
    private async Task<IDataType?> CreateDataTypeAsync()
    {
        const string editorAlias = "Umbraco.MultiUrlPicker";
        
        // ✅ CORRECT: Look up by alias
        var editor = propertyEditorCollection[editorAlias];
        if (editor == null) return null;
        
        var dataType = new DataType(editor, configurationEditorJsonSerializer)
        {
            Name = "My Custom Data Type",
            DatabaseType = ValueStorageType.Ntext,
            ConfigurationData = new Dictionary<string, object> { { "maxNumber", 4 } }
        };
        
        await dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        return dataType;
    }
}
```

### Why

1. **GUIDs are not self-documenting** — hard-coded GUIDs don't indicate what editor they reference
2. **GUIDs are easy to get wrong** — the GUID `fd1e0da5-5606-4862-b679-5d0cf3a52a59` was assumed to be Multi URL Picker, but it's actually Multi Node Tree Picker
3. **Aliases are self-documenting** — `propertyEditorCollection["Umbraco.MultiUrlPicker"]` clearly states intent
4. **PropertyEditorCollection is the correct abstraction** — DI-injectable registry for all property editors
5. **No compile-time safety with GUIDs** — errors only discovered at runtime

### Technical Notes

- **Injection:** Add `PropertyEditorCollection propertyEditorCollection` to constructor
- **Lookup:** `propertyEditorCollection[alias]` returns `IDataEditor?` (null if not found)
- **Common Aliases:**
  - Multi URL Picker: `"Umbraco.MultiUrlPicker"`
  - Multi Node Tree Picker: `"Umbraco.MultiNodeTreePicker"`
  - Content Picker: `"Umbraco.ContentPicker"`
  - Media Picker: `"Umbraco.MediaPicker3"`

### Impact

- All future data type creation in seeders should follow this pattern
- If a data type is created incorrectly, use `IDataTypeService.DeleteAsync` to remove the old one before creating the correct one

**Files Modified:**
- `src/UmbracoPrism.Core/PrismContentTypeSeeder.cs`

---

## Decision: Custom Data Type Creation with Configuration

**Date:** 2026-03-29  
**Agent:** Brewster (Umbraco Platform Specialist)  
**Context:** Seeding Umbraco content types with custom property editor configuration

### Pattern

When seeding content types that require **property editors with custom configuration** (e.g., Multi URL Picker with `maxNumber: 4`):

1. **Create custom data types** programmatically rather than using built-in ones with wrong configuration
2. **Retrieve the built-in editor** via `PropertyEditorCollection[alias]`
3. **Use proper v17 constructor:** `new DataType(IDataEditor, IConfigurationEditorJsonSerializer)`
4. **Check for existing** data type by name before creating (idempotent)
5. **Inject required services:** `IConfigurationEditorJsonSerializer` must be in seeder constructor

```csharp
private async Task<IDataType?> GetOrCreateCustomDataTypeAsync(
    string dataTypeName, 
    string editorAlias,
    Dictionary<string, object> customConfig)
{
    // Check for existing
    var existingDataTypes = await dataTypeService.GetByEditorAliasAsync(editorAlias);
    var dataType = existingDataTypes?.FirstOrDefault(dt => dt.Name == dataTypeName);
    if (dataType != null) return dataType;

    // Get editor via PropertyEditorCollection
    var editor = propertyEditorCollection[editorAlias];
    if (editor == null) return null;

    // Create with custom config
    var newDataType = new DataType(editor, configurationEditorJsonSerializer)
    {
        Name = dataTypeName,
        DatabaseType = ValueStorageType.Ntext,
        ConfigurationData = customConfig
    };
    
    await dataTypeService.CreateAsync(newDataType, Constants.Security.SuperUserKey);
    return newDataType;
}
```

### Why

- Built-in data types have fixed configuration (e.g., Multi URL Picker defaults to single link)
- Programmatic creation allows package to control editor experience without manual backoffice setup
- Idempotent check prevents duplicate data types on app restarts

### Conventions

- Name custom data types with package prefix: `"Prism Mobile Nav Links"` (not generic names)
- Use `Dictionary<string, object>` for `ConfigurationData` (not `object?`) to avoid nullability warnings
- Always inject `IConfigurationEditorJsonSerializer` when creating DataType instances

**Files Modified:**
- `src/UmbracoPrism.Core/PrismContentTypeSeeder.cs`

---

## Decision: Pre-Seeding Editor-Configurable Properties with Defaults

**Date:** 2026-03-29  
**Agent:** Brewster (Umbraco Platform Specialist)  
**Context:** Settings node needs default mobile nav links on fresh installations

### Pattern

When seeding Settings nodes with **editor-configurable properties that need sensible defaults**:

1. **Pre-seed values** immediately after creating the content node
2. **Use simple, reliable formats** (external-type links, not content UDI) for nav links
3. **Serialize with System.Text.Json** for Multi URL Picker JSON arrays
4. **Set and Save before Publish** to ensure property values persist

```csharp
using System.Text.Json;

// After creating content node:
var navLinksJson = JsonSerializer.Serialize(new[]
{
    new { name = "Home", target = "", type = "external", url = "/" },
    new { name = "Dashboard", target = "", type = "external", url = "/dashboard" }
});
settings.SetValue("mobileNavLinks", navLinksJson);
contentService.Save(settings);
contentService.Publish(settings, new[] { "*" });
```

### Why

- Fresh installs should "just work" — editors see working examples, not empty properties
- Reduces onboarding friction (editors can modify existing links rather than create from scratch)
- External-type links are simpler than content UDI links and work before content tree exists

**Files Modified:**
- `src/UmbracoPrism.Core/PrismStarterContentSeeder.cs`

---

## Decision: Seeder Idempotency for Existing Installations

**Date:** 2026-03-29  
**Agent:** Brewster (Umbraco Platform Specialist)  
**Context:** Fixing seeders to support both fresh installations and upgrades

### Problem

Initial seeder implementations assumed fresh installations (empty content tree). Two critical bugs emerged:

1. **Property exists → assume correct:** `PrismContentTypeSeeder` early-returned if property existed, without checking if it used the correct data type. Existing properties remained stuck on old built-in data type instead of upgrading.

2. **Tree not empty → skip everything:** `PrismStarterContentSeeder` exited completely if content tree wasn't empty, which prevented Settings node creation and default nav links population for existing installations.

### Decision

**Content Type Seeder Pattern:**
- Always validate actual state, not just existence
- Check if property's `DataTypeKey` matches expected data type
- If wrong data type → update and save
- Pattern: `if (existingProperty.DataTypeKey == newDataType.Key) return;`

**Content Seeder Pattern:**
- Separate tree-empty guard from configuration-empty guard
- Home + Dashboard seeding → only runs on empty tree
- Settings defaults → always runs, checks if values are empty
- Pattern: Two methods with independent guards

```csharp
// Content Type Pattern
var newDataType = await GetOrCreatePrismMobileNavDataTypeAsync();
var existingProperty = contentType.PropertyTypes.FirstOrDefault(p => p.Alias == propertyAlias);

if (existingProperty != null)
{
    if (existingProperty.DataTypeKey == newDataType.Key) return; // Already correct
    existingProperty.DataTypeKey = newDataType.Key; // Wrong type → update
    contentTypeService.Save(contentType);
    return;
}

// Content Seeder Pattern
if (!rootContent.Any())
{
    SeedHomeAndDashboard(); // Only for empty tree
}

EnsureSettingsDefaults(); // Always run (idempotent)
```

### Benefits

✅ Existing installations auto-upgrade data types on next startup  
✅ Settings node and defaults populate even on non-empty trees  
✅ No manual backoffice edits required for upgrades  
✅ Future seeders follow proven idempotency pattern  

### Trade-offs

⚠️ Slightly more complex seeder logic (separate methods, state validation)  
⚠️ Settings defaults always checked on startup (minimal performance cost)  

**Files Modified:**
- `src/UmbracoPrism.Core/PrismContentTypeSeeder.cs`
- `src/UmbracoPrism.Core/PrismStarterContentSeeder.cs`


---

# Decision: Deterministic GUID for Prism Custom Data Types

**Date:** 2026-06-17
**Author:** Brewster (Umbraco Platform Specialist)

## Decisions

### 1. Use deterministic fixed GUIDs for all Prism-owned data types

When creating custom data types in Prism seeders, assign a project-specific fixed GUID by setting `Key = <fixed Guid>` on the `DataType` instance before calling `dataTypeService.CreateAsync(...)`.

This allows reliable idempotent lookup via `dataTypeService.GetAsync(key)` across installs and upgrades, without depending on name-based search which is fragile.

**Fixed GUID for Prism Mobile Nav Links:** `3b4c5d6e-7f80-9a1b-c2d3-e4f567890abc`

### 2. Use remove + re-add for property type migration, not in-place DataTypeKey mutation

When a content type property is found using the wrong data type, do NOT mutate `existingProperty.DataTypeKey` in-place. Instead:
1. `contentType.RemovePropertyType(alias)` and save
2. Re-fetch the content type from DB: `contentTypeService.Get(alias)`
3. Fall through to create the property fresh with the correct `PropertyType(shortStringHelper, newDataType, alias)` constructor

This ensures Umbraco's internal integer `DataTypeId` is set correctly, not just the GUID key.

## Lessons

### `dataTypeService.DeleteAsync` silently fails when a data type is in use

Umbraco blocks deletion of data types that are referenced by content types at the DB level. The `Attempt<>` result carries the failure but if the caller ignores it, code silently continues with the old data type still in place. Always check and log the `Attempt` result.

### In-place `DataTypeKey` mutation on `PropertyType` is unreliable

`PropertyType` stores both `DataTypeKey` (GUID) and `DataTypeId` (int). Setting only the GUID via the setter does not update the integer ID used internally by Umbraco for validation lookup. The property validation still uses the old data type, causing JSON deserialization errors at publish time (as seen with MultiNodeTreePicker vs MultiUrlPicker).

### Re-fetch content type from DB after structural changes

After removing a property type and saving, re-fetch the content type from the database (`contentTypeService.Get(alias)`) to get a clean, cache-free object before adding properties. Operating on a stale in-memory object after structural changes can cause inconsistent state.

### Guard pattern prevents startup crash

A GUID comparison guard in `EnsureSettingsDefaults` (`mobileNavProperty.DataTypeKey != expectedDataTypeKey`) allows the seeder to safely save/publish an empty Settings node rather than crashing with a JSON deserialization exception. The user can fill in nav links manually via the backoffice.

---

## Decision: TestSite Views Use Master.cshtml as Shared Layout

**Date:** 2026  
**Author:** Brewster (Umbraco Platform Specialist)  
**Status:** Implemented

### Decision

All TestSite Razor views must use `Layout = "Master"` (not `Layout = null`). Views should contain only page-specific content, styles, and C# logic — never full HTML boilerplate.

### Context

`HomePage.cshtml` and `MemberDashboard.cshtml` were previously `Layout = null` standalone pages. This caused:
- `prism-mobile-nav` web component never being injected on those pages
- `prism-branding.css` (and other shared CSS) never loading
- Duplicated `<header>`, `<footer>`, mobile nav partial in every view

### Rules Going Forward

1. **New views:** Always start with `Layout = "Master";` — never `Layout = null`.
2. **Master.cshtml provides:** DOCTYPE, html/head/body shell, `<link>` tags for shared CSS (`prism-branding.css`), tenant-scoped `:root` CSS variables, shared header, shared footer, `_MobileShellNav` partial, `@RenderBody()`.
3. **Child views provide:** Page-specific CSS `<style>` blocks (including any with Razor expressions like `@Html.Raw(...)` for imagery overrides), page-specific C# logic at top, and HTML content.
4. **Imagery CSS overrides** (e.g. `--prism-hero-image: url('@heroImageUrl')`) must stay as inline `<style>` in child views — they are not static and cannot be extracted to a static CSS file.
5. **Do not** add `<html>`, `<head>`, `<body>`, `<header>`, `<footer>`, or mobile nav partial invocations to child views — Master handles all of these.

---

## Decision: Extract Inline Styles from Master.cshtml to Branding CSS Files

**Date:** 2026-07-10  
**Author:** Isabelle (Frontend Dev)  
**Status:** Implemented

### Context

`Master.cshtml` contained a large static `<style>` block with page-level rules. Because these rules were inline, tenants could not override them via CSS variables — the inline style always wins specificity. Extracting them to `/branding/` files lets tenant CSS variable overrides propagate correctly.

### Decision

All static CSS rules have been moved from the `Master.cshtml` inline `<style>` block to the appropriate branding CSS files:

| Category | File |
|---|---|
| Colour variables (`--tenant-primary-contrast`, `--bg-offset`) | `prism-colors.css` |
| Layout rules (`body`, `.header`, `.container`, `.footer`, `.prism-mobile` overrides) | `prism-layout.css` |
| Component rules (`.card`, `html.prism-mobile prism-mobile-nav`) | `prism-components.css` |

Only the dynamic Razor expression `--tenant-primary: @brandColor;` remains as an inline `<style>` in `Master.cshtml`.

`prism-branding.css` is linked in the `<head>` via `<link rel="stylesheet" href="/branding/prism-branding.css" />`.

### Rationale

- Tenant branding overrides work through CSS custom properties declared in `/branding/` files; inline styles prevent those overrides from being effective.
- `prism-branding.css` already aggregated all 5 branding files via `@import`; adding the `<link>` tag to `Master.cshtml` loads the full chain in one request.
- The mobile nav visibility rule (`html.prism-mobile prism-mobile-nav { display: block !important }`) belongs in `prism-components.css` — it is a component styling concern, not a view-level concern.

### Consequences

- Any future page-level styles should be added to the appropriate `/branding/` CSS file, not as inline styles in Razor views.
- Inline `<style>` blocks in Razor views should be reserved exclusively for dynamic server-injected values (Razor expressions).
