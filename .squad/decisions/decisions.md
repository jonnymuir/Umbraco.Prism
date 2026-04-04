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
# Notifications Design Pre-Implementation Alignment Pass

**Date:** 2026-07-14  
**Reviewer:** Tom Nook (Lead)  
**Task:** Cross-cutting consistency check across four notification design documents before implementation begins

## Summary

**Status:** ✅ **READY FOR IMPLEMENTATION**

All critical cross-cutting issues resolved. One document update required (backend).

---

## Issues Found & Resolved

### 1. 🔴 **Device Token Storage Conflict**

**What:** Architecture doc says "extend `prismDeviceCredentials`" but backend doc still defined a separate `prismDeviceTokens` table.

**Where:** 
- Architecture: `docs/design/notifications-architecture.md` (line 171: "extend `prismDeviceCredentials` with `PushToken` column")
- Backend: `docs/design/notifications-backend.md` (lines 314–397: full `prismDeviceTokens` schema + migration)

**Resolution:** ✅ **Extended `prismDeviceCredentials` (user-confirmed decision)**
- One unified row per device, whether it has biometric, push, or both
- Reuses tenant isolation, user binding, and credential lifecycle from existing table
- Migration: add `PushToken` nullable string column (512 chars)
- Stale token cleanup: NULL out `PushToken` rather than delete row (preserves device audit trail)

**Action Taken:** ✅ Updated `notifications-backend.md`:
- Removed `PrismDeviceTokenSchema` class and separate table definition
- Added `PushToken` property to existing `prismDeviceCredentials` schema
- Replaced migration from `CreatePrismDeviceTokensTable` → `AddPushTokenColumn`
- Fixed stale token cleanup logic: `UPDATE ... SET PushToken = NULL` instead of DELETE
- Updated Phase 1 checklist: "Add `PushToken` column to `prismDeviceCredentials`" (not separate table)

---

### 2. ✅ **API Surface Consistency — Device Token Registration**

**What:** Mobile doc specifies `POST /umbraco/prism/mobile/push/register`. Checking backend alignment.

**Where:**
- Mobile: `docs/design/notifications-mobile.md` (lines 284–383: register endpoint + payload)
- Backend: `docs/design/notifications-backend.md` (lines 762–800: endpoints defined with same path)

**Finding:** ✅ **Consistent**
- Both docs agree on `POST /umbraco/prism/mobile/push/register`
- Both expect `{token: string}` payload
- Both upsert by `(TenantId, DeviceId, UserId)`
- Backend doc correctly references this endpoint in controller definition

**Action:** None needed.

---

### 3. ✅ **Mobile ↔ Backend Contract — Push Token Registration Flow**

**What:** Does the Capacitor plugin (Kicks) register tokens the way Blathers' backend expects?

**Where:**
- Mobile: Capacitor plugin fires `registration` event with `PushToken`, calls `POST /register` with token
- Backend: Controller accepts token, upserts device credential with `PushToken` column

**Finding:** ✅ **Consistent**
- Kicks' plugin registration event → triggers `registerPushToken()` → sends token to `/register`
- Blathers' backend receives token, upserts `prismDeviceCredentials.PushToken`
- No contract mismatches

**Action:** None needed.

---

### 4. ✅ **Demo ↔ Backend Trigger Alignment**

**What:** Does Vinyl Vault demo's content types and publish scenarios align with backend's `ContentPublishedNotification` handler?

**Where:**
- Demo: `docs/design/notifications-umbraco-demo.md` (content types: `VinylRecord`, `genre`; trigger: publish)
- Backend: `docs/design/notifications-backend.md` (handler: `PrismContentPublishedNotificationHandler` on `ContentPublishedNotification`)

**Finding:** ✅ **Fully Consistent**
- Demo publishes `VinylRecord` → fires `ContentPublishedNotification`
- Backend handler listens to same notification
- Demo expects notifications to fire on `ContentPublishedNotification` → backend delivers exactly that
- Content type aliases referenced in demo (`VinylRecord`, `genre`) are arbitrary developer choices — no hardcoding in backend

**Action:** None needed.

---

### 5. ✅ **Opt-In Flag (`PushNotificationsEnabled`) Consistency**

**What:** Is the opt-in flag consistently named and applied across docs?

**Where:**
- Mobile: `docs/design/notifications-mobile.md` (lines 20, 115, 211, 736: `PushNotificationsEnabled` on `PrismMobileBundleRequest`)
- Architecture: `docs/design/notifications-architecture.md` (line 199: brief mention of notifications as independent of biometric)
- Backend: No direct mention of `PushNotificationsEnabled` (backend receives already-generated bundle)

**Finding:** ✅ **Consistent**
- Mobile layer controls `PushNotificationsEnabled` boolean in bundle request
- When true: generates `notifications-bridge.ts` + permission manifest updates
- When false: bundle ships without push scaffolding
- Backend doesn't need to know about this flag — it's a generation-time decision in MobileBundleService
- Architecture doc confirms notifications are independent feature (doesn't block biometric-only deployments)

**Action:** None needed.

---

### 6. ✅ **FCM Credential Storage Location**

**What:** Is the Firebase service account credential stored consistently?

**Where:**
- Architecture: `docs/design/notifications-architecture.md` (line 140: "Store FCM key in Azure Key Vault or config with `keyvault:` prefix")
- Backend: `docs/design/notifications-backend.md` (line 274: "Credential Storage: Azure Key Vault via `PrismNotificationKeyVaultConfigureOptions`")

**Finding:** ✅ **Consistent**
- Both recommend Azure Key Vault storage
- Architecture mentions optional `keyvault:` prefix pattern (same as existing Prism secrets pattern)
- Backend implements via dedicated `PrismNotificationKeyVaultConfigureOptions`
- Graceful degradation: if not configured, service logs warning and returns no-op

**Action:** None needed.

---

### 7. ✅ **Subscription Model Consistency**

**What:** Is the `prismNotificationSubscriptions` table design consistent across docs?

**Where:**
- Architecture: `docs/design/notifications-architecture.md` (lines 250–273: schema + index definition)
- Backend: `docs/design/notifications-backend.md` (lines 423–483: same schema)
- Demo: `docs/design/notifications-umbraco-demo.md` (implies subscription model; user subscribes to genre)

**Finding:** ✅ **Fully Consistent**
- Both define same table: `(Id, TenantId, UserId, Topic, SubscribedAt)`
- Both include index on `(TenantId, Topic)` for efficient lookups
- Demo user flow (subscribe to "Jazz" genre) maps to topic subscription (topic = `contentType:VinylRecord` or `genre:jazz`)
- Table name is consistent: `prismNotificationSubscriptions`

**Action:** None needed.

---

### 8. ✅ **Permission Request Timing**

**What:** Kicks said "after first biometric login". Do architecture and backend docs conflict with this?

**Where:**
- Mobile: `docs/design/notifications-mobile.md` (line 18: "Request permission AFTER first biometric login")
- Architecture: `docs/design/notifications-architecture.md` (no mention of timing)
- Backend: `docs/design/notifications-backend.md` (no mention of timing)

**Finding:** ✅ **No Conflicts**
- Mobile layer specifies permission request timing (after biometric enrollment)
- Backend doesn't care about timing — it's a mobile UX decision
- Architecture doesn't prescribe timing — aligns with Kicks' design
- No blocking issues

**Action:** None needed.

---

## Final Verification Checklist

| Item | Status | Details |
|------|--------|---------|
| Device token storage | ✅ Updated | Backend doc now matches architecture decision |
| API surface (register endpoint) | ✅ Consistent | Both docs agree on `/umbraco/prism/mobile/push/register` |
| Mobile ↔ Backend contract | ✅ Consistent | Token registration flow is clear and aligned |
| Demo ↔ Backend triggers | ✅ Consistent | `ContentPublishedNotification` triggers match demo scenarios |
| `PushNotificationsEnabled` flag | ✅ Consistent | Opt-in is well-defined in mobile layer; backend doesn't need it |
| FCM credential location | ✅ Consistent | Both recommend Azure Key Vault |
| Subscription table schema | ✅ Consistent | Same design in both architecture and backend docs |
| Permission timing | ✅ Aligned | No conflicts; mobile layer defines, backend is agnostic |

---

## Documents Updated

1. **`docs/design/notifications-backend.md`**
   - Removed separate `prismDeviceTokens` table definition (lines 314–397)
   - Added `PushToken` property to existing `prismDeviceCredentials` schema
   - Updated migration from create-table → add-column pattern
   - Fixed stale token cleanup: NULL instead of DELETE
   - Updated Phase 1 implementation checklist

---

## Go/No-Go Decision

**✅ GO FOR IMPLEMENTATION**

All cross-cutting concerns are resolved. The four design documents are now aligned and internally consistent. Implementation can proceed with confidence that:

- Database schema is unified and efficient
- API contracts are agreed across mobile, backend, and architecture layers
- Demo scenarios will work as designed
- No rework needed due to documentation conflicts

**Recommendation:** Begin with Phase 1 (database schema, service interfaces) and Phase 2 (API endpoints) as outlined in the backend doc.

---

## Notes for Implementation Team

1. **PushToken nullability:** Devices may have push without biometric (or vice versa). The `PushToken` column should be nullable and independent from biometric fields.

2. **Stale token handling:** When FCM returns `Unregistered`, null out the `PushToken` column rather than deleting the row. This preserves the device credential record for audit and future reuse.

3. **Tenant isolation:** All push operations are scoped to the current tenant context via `IPrismContext` — enforce this consistently in the service layer.

4. **Optional permissions on demo app:** The Vinyl Vault demo should have `PushNotificationsEnabled: true` in its bundle request so the demo showcases the full notification flow.
---
# Tom Nook — Notifications Architecture Design Decisions

**Date:** 2026-07-14
**Design Doc:** `docs/design/notifications-architecture.md`
**Status:** Proposal — needs team review before implementation

---

## Decision 1: Extend `prismDeviceCredentials` with `PushToken` column

**Decision:** Add a nullable `PushToken NVARCHAR(512)` column to the existing `prismDeviceCredentials` table rather than creating a separate push token table.

**Rationale:** The device credential row already contains `DeviceId`, `TenantId`, `UserId`, and `Platform` — exactly the fields needed for push routing. Avoids join overhead and keeps the "device" concept unified. Devices without biometric auth create a minimal credential row with only push-relevant fields populated.

**Trade-off:** Couples notifications to the biometric credential schema. If the team prefers separation, a `prismPushTokens` table with FK to `prismDeviceCredentials.Id` is the alternative.

**Status:** Needs team agreement.

---

## Decision 2: FCM as default provider behind `IPrismPushGateway` interface

**Decision:** Ship Firebase Cloud Messaging (HTTP v1 API) as the sole provider. Expose `IPrismPushGateway` interface so consumers can swap in their own provider (APNs direct, Azure Notification Hubs, OneSignal).

**Rationale:** FCM is free, cross-platform (iOS via APNs-through-FCM + Android native), has excellent Capacitor plugin support, and adds no cost to Marketplace consumers. Building multiple providers without demand violates YAGNI.

**Status:** Recommended — low risk.

---

## Decision 3: Prism-managed subscriptions (database) over FCM topic subscriptions

**Decision:** Store notification subscriptions in a new `prismNotificationSubscriptions` table, scoped per-user (not per-device) and per-tenant. Do not use FCM's built-in topic subscription feature.

**Rationale:** FCM topics are device-scoped, not user-scoped, and not tenant-aware. Prism-managed subscriptions give full control: queryable, tenant-isolated, per-user (all devices receive), and visible in admin API. Trade-off is more DB queries on send, mitigated by indexing and batching.

**Status:** Strongly recommended.

---

## Decision 4: Synchronous delivery for v1, optional queue for v2

**Decision:** v1 sends notifications in-process with async batching (500 tokens per FCM request, max 3 concurrent batches). v2 adds optional `IBackgroundTaskQueue` decoupling. The `IPrismNotificationService` interface is the same either way.

**Rationale:** Avoids infrastructure dependency (no Redis, no message bus) for v1. Most Marketplace consumers are small-to-medium sites where in-process delivery is fine. The interface abstraction means v2 queueing is a non-breaking internal change.

**Status:** Recommended for v1 simplicity.

---

## Decision 5: MobileBundleService generates push notification scaffolding

**Decision:** `MobileBundleService` conditionally generates `notifications-bridge.ts` in the Capacitor bundle when `NotificationsEnabled` is true in tenant's `MobileAppConfig`. Same pattern as biometric auth's `biometric-bridge.ts`.

**Rationale:** Consumer doesn't need to write any Capacitor push code. Permission request, token registration, notification received handling, and deep-link navigation are all generated. Follows the established biometric bridge pattern.

**Status:** Recommended — follows established pattern.

---

## Decision 6: "Content Expiry Watchdog" as Use Case 2 demo

**Decision:** The backend-triggered notification demo is a `ContentExpiryWatchdog` that runs hourly via `IRecurringBackgroundTask`, checks for content expiring within 24 hours, and pushes notifications to subscribers.

**Rationale:** Content expiry is a real Umbraco feature that nobody monitors proactively. It demonstrates the scheduled-task + `IPrismNotificationService` pattern that developers would actually use. Ships as example code, not auto-registered.

**Status:** Recommended.
---
# Push Notifications Design — Key Decisions (Kicks / Tom Nook)

**Date:** 2026-07-14
**Author:** Tom Nook (Lead) + Kicks (Mobile Native Specialist)
**Design document:** `docs/notifications-design.md`

---

## Decisions Made

### 1. Push provider: FCM via FirebaseAdmin + @capacitor/push-notifications

**Decision:** Use Firebase Cloud Messaging as the sole push transport. .NET dispatch via `FirebaseAdmin` NuGet package (HTTP v1 API, service account auth). Mobile receipt via `@capacitor/push-notifications`.

**Rationale:** Covers both iOS (via APNs relay) and Android in one integration. Avoids maintaining two dispatch paths. Capacitor push plugin has first-class FCM support. Rejected: OneSignal (third-party SaaS), Azure Notification Hubs (Azure lock-in), direct APNs (two paths).

### 2. FCM credentials via Azure Key Vault (existing pattern)

**Decision:** Consumer stores Firebase service account JSON as a Key Vault secret. Config: `Prism:Push:FcmServiceAccountSecretName`. Resolution uses existing `ISecretVaultService`. `FirebaseApp` initialised lazily on first dispatch call.

**Rationale:** Consistent with existing Prism credential management pattern. No new secret management surface.

### 3. Token storage: `prismPushTokens` custom table

**Decision:** FCM device tokens stored in `prismPushTokens`, keyed by client-generated `DeviceId` (same as `prismDeviceCredentials` pattern). Linked to `MemberKey` (Umbraco member GUID). `IsActive` flag for soft-deactivation.

**Rationale:** Consistent schema pattern. Allows upsert on token refresh. Supports multiple devices per member.

### 4. Subscription storage: `prismPushSubscriptions` custom table

**Decision:** Subscriptions stored with nullable `ContentNodeKey`, `ContentTypeAlias`, `Category` columns. Null = "any". Unique constraint on the full (MemberKey, ContentNodeKey, ContentTypeAlias, Category) tuple.

**Rationale:** Flexible multi-dimension matching without needing a complex EAV schema.

### 5. Scheduled notifications: `prismPushQueue` + `IRecurringBackgroundTask`

**Decision:** No Hangfire dependency. Delayed/scheduled notifications use a `prismPushQueue` table polled every 60 seconds by `PrismPushQueueRunner` (`IRecurringBackgroundTask`).

**Rationale:** Keeps NuGet package lean. 60-second precision is sufficient for all planned use cases. Consumers who need cron precision can add Hangfire and implement `IPrismPushNotificationService` themselves.

### 6. Permission request timing (iOS)

**Decision:** Never request push permission on app cold start. Request after member is authenticated and has seen app value (post-login screen). Surface a native-style in-app prompt before the OS prompt to improve grant rate.

**Rationale:** iOS cold-start permission requests have ~40-60% rejection rates. Contextual prompts significantly improve grant rates.

### 7. Public API surface: `IPrismPushNotificationService`

**Decision:** Prism exposes `IPrismPushNotificationService` with `SendToMemberAsync`, `SendToMembersAsync`, `BroadcastAsync`, `ScheduleForMemberAsync`. Consuming apps call this from their own notification handlers.

**Rationale:** Enables Use Case 2 (API-triggered) without Prism needing to know about consumer business logic.

### 8. Demo scenarios

**Demo 1:** "Prism Announcements" — Announcement content type, member subscribes on `/announcements` page, content publish triggers push, editor can broadcast from backoffice Notifications dashboard.

**Demo 2A:** "Content Expiry Warning" — daily `IRecurringBackgroundTask` sends push to editors 7 days before content expires.

**Demo 2B:** "Member Welcome Notification" — `MemberCreatedNotification` → `ScheduleForMemberAsync` T+1 minute.

### 9. Implementation phases

- **Phase 1:** Device registration + FCM dispatch plumbing (no UI)
- **Phase 2:** Content subscriptions + subscribe Lit component + Announcements demo
- **Phase 3:** Broadcast dashboard + backoffice UI
- **Phase 4:** Scheduled queue + welcome notification + content expiry demo
- **Phase 5:** Web push (deferred), foreground notification banner, token cleanup

---

## Open Questions (Blocking for Phase 1)

| # | Question | Decision needed |
|---|---|---|
| Q1 | Are push tokens keyed to Umbraco `MemberKey` or Entra OID? | Architecture of token↔member link |
| Q2 | Do editors/admins use the mobile app shell? | Whether content expiry warning is mobile push or email |
| Q3 | Is web push (in-browser PWA notifications) in scope? | Phase 5 scope |
| Q4 | Should push tokens be multi-tenant scoped (add `TenantId` column)? | Schema decision before migration runs |
| Q5 | Is one Firebase project per Prism installation correct? | Confirmed expected — document clearly |
---
# Kicks — Mobile Push Notifications Design Decisions

**Session:** 2026-07-14  
**Task:** Mobile-side push notification implementation design for Prism Mobile Capacitor apps  
**Requested by:** Jonny Muir

---

## Decision 1: Capacitor Plugin Selection

**Choice:** `@capacitor/push-notifications` (official Capacitor plugin)

**Alternatives Considered:**
- `@capacitor-firebase/messaging` (Capacitor Community Firebase plugin)
- Direct native implementation (no Capacitor plugin)

**Rationale:**
- **Smaller bundle:** `@capacitor/push-notifications` adds 5-10MB vs 20-50MB for full Firebase SDK
- **APNs-native on iOS:** Direct APNs integration is simpler than Firebase proxy layer
- **Sufficient for standard use cases:** Most Prism tenants need basic notification delivery, not advanced Firebase Analytics or topic-based targeting
- **Official Ionic plugin:** Stronger long-term maintenance guarantees

**When to use alternative:**
- Use `@capacitor-firebase/messaging` if backend is Firebase-first, or consumer needs data-only messages, Firebase Analytics, or topic subscriptions

**Impact:**
- MobileBundleService generates `@capacitor/push-notifications` dependency by default when `PushNotificationsEnabled: true`
- README documents Firebase alternative for advanced consumers

---

## Decision 2: Permission Request Timing

**Choice:** Request push permission AFTER first biometric login (post-authentication)

**Alternatives Considered:**
- On first app launch (cold start)
- On first page navigation
- Explicit user-initiated only (settings screen)

**Rationale:**
- **Contextual permission:** Apple HIG strongly discourages permission prompts on cold app launch; post-login provides clear context ("Get notified about your account activity")
- **User is authenticated:** Push token can immediately be associated with a `PrismMemberCookie` session; no orphaned tokens
- **Reduces friction:** New users see one permission at a time (biometric first, then push), not a wall of prompts
- **Consistent with biometric flow:** Biometric enrollment already happens post-OIDC; push follows same timing

**Impact:**
- `www/index.html` includes push permission request logic AFTER biometric enrollment flow
- Pre-permission explainer UI shown before calling `PushNotifications.requestPermissions()`
- Permission state stored in `Preferences` to avoid re-prompting

---

## Decision 3: Architecture — Consumer Configuration vs Prism Plugin

**Choice:** Consumer configuration (scaffolding in generated bundle), NOT a new Prism plugin

**Alternatives Considered:**
- `@umbracoprism/capacitor-push` plugin (encapsulates Prism-specific logic)
- Hybrid: Prism Web Component + official plugin

**Rationale:**
- **Minimal abstraction:** Push notifications are simple; `@capacitor/push-notifications` handles 90% of the work. Wrapping it in a Prism plugin adds little value.
- **Consumer ownership:** Many consumers will customize notification handling (banners, deep linking, analytics). Giving them scaffolding code directly makes customization trivial.
- **No version lock-in:** If Capacitor updates the push-notifications API, consumers can update `package.json` independently without waiting for Prism plugin release.
- **Prism backend integration is backend-side:** Prism-specific logic (token registration, revocation) lives in backend (`PushNotificationController`). Client just calls standard REST endpoints.

**Impact:**
- MobileBundleService generates push notification scaffolding when `PushNotificationsEnabled: true`
- No new NPM package to maintain
- Consumer owns and can customize all push notification code

---

## Decision 4: iOS APNs Setup — p8 Key vs p12 Certificate

**Choice:** Recommend APNs p8 Authentication Key (prefer over p12 certificate)

**Rationale:**
- **Never expires:** p8 keys are permanent; p12 certs expire annually
- **One key for all apps:** Single p8 key can be used across multiple apps
- **Simpler renewal:** No annual certificate regeneration workflow

**Impact:**
- README setup guide documents p8 key generation as primary method
- p12 certificate documented as alternative for teams with existing certs
- Backend configuration uses p8 key by default

---

## Decision 5: Foreground Notification UX

**Choice:** Custom in-app banner (injected into WebView), NOT system notification

**Alternatives Considered:**
- Let iOS/Android show system notification banner in foreground (requires native code)
- No foreground notification (silent)

**Rationale:**
- **Consistent UX:** In-app banner matches Prism Mobile branding and theme
- **No native code required:** Can be implemented entirely in generated `www/index.html` + CSS
- **User control:** Banner auto-dismisses after 5 seconds; system notification requires manual dismiss
- **Simplicity:** Avoids consumer needing to add native code to `AppDelegate.swift` or `MainActivity.kt`

**Impact:**
- `www/mobile-overrides.css` includes `.prism-notification-banner` styles
- `www/index.html` includes `showInAppNotificationBanner()` function
- System notification only shown when app is in background/killed

---

## Decision 6: Token Storage & Security

**Choice:** Store SHA256 hash of device token in database, not plaintext

**Rationale:**
- **Security:** If database is compromised, attacker cannot use raw tokens to send push notifications
- **Privacy:** Device tokens are sensitive and should be treated like passwords
- **Compliance:** Aligns with data minimization principles

**Impact:**
- Backend `POST /umbraco/prism/mobile/push/register` hashes token before storage
- `prismPushTokens` table has `DeviceTokenHash` column, not `DeviceToken`
- Token comparison uses hash equality check

---

## Decision 7: Token Lifecycle — Refresh Strategy

**Choice:** Listen for `registration` event, compare to stored token, update backend if changed

**Rationale:**
- **Android FCM tokens rotate:** Typically every 60 days or on app reinstall; must detect and update
- **iOS APNs tokens stable:** Rarely change, but should re-register on app launch to ensure currency
- **Idempotent registration:** Backend `POST /umbraco/prism/mobile/push/register` updates existing record if `DeviceId + UserOid + TenantId` match

**Impact:**
- `www/index.html` includes token refresh listener
- `Preferences` stores last-known token for comparison
- Backend supports idempotent registration (update vs insert)

---

## Decision 8: Permission Denied Handling

**Choice:** Store denial state, DO NOT block app functionality, show "Open Settings" deep link

**Rationale:**
- **Graceful degradation:** Prism Mobile must remain fully functional without push notifications
- **Respect user choice:** Avoid nagging; only re-prompt if user explicitly opts in
- **App Store compliance:** Repeatedly requesting denied permissions risks rejection

**Impact:**
- Permission denial stored in `Preferences` with timestamp
- No re-prompt until user taps "Enable Notifications" in app settings, OR 14+ days elapsed
- "Open Settings" button uses `App.openUrl({ url: 'app-settings:' })` deep link

---

## Decision 9: Opt-In Model for Push Notifications

**Choice:** `PushNotificationsEnabled` boolean in `PrismMobileBundleRequest`, default `false`

**Rationale:**
- **Keeps base bundle lean:** Consumers who don't need push don't get the scaffolding
- **Consumer choice:** Tenants can ship mobile app without push if not needed
- **Easier to add later:** Consumer can regenerate bundle with push enabled at any time

**Impact:**
- MobileBundleService only generates push scaffolding when `PushNotificationsEnabled: true`
- Default Prism Mobile bundle has no push notification code
- README documents how to enable push when regenerating bundle

---

## Decision 10: Consumer Setup Friction Target

**Choice:** 40-50 minutes for first-time setup, 15 minutes for repeat apps

**Breakdown:**
- iOS APNs key generation: 5 minutes
- iOS Xcode setup: 10-15 minutes
- Android Firebase setup: 10-15 minutes
- Testing: 10-15 minutes
- Repeat app setup: 15 minutes (reuse APNs key, new Firebase project)

**Rationale:**
- **Comparable to industry standard:** Other push notification SDKs (OneSignal, Pusher Beams) have similar setup times
- **Most time is platform setup:** Apple Developer Console + Firebase Console are external; we can't reduce that
- **Auto-injection reduces friction:** `bootstrap-ios.sh` and `bootstrap-android.sh` eliminate manual file editing where possible

**Impact:**
- README includes 10-step setup guide with estimated time per step
- `AGENT_PROMPT.md` provides AI-friendly instructions for setup assistance
- Auto-injection scripts reduce manual config steps by ~50%

---

## Conventions Established

1. **Mobile push scaffolding location:** Generated in mobile bundle root when `PushNotificationsEnabled: true`
2. **Permission state storage key:** `prism-push-permission-state` (values: `'granted'`, `'denied'`, `'prompt'`)
3. **Token storage key:** `prism-push-token` (stores device token for comparison on refresh)
4. **Backend API prefix:** `/umbraco/prism/mobile/push/*` (consistent with `/umbraco/prism/mobile/biometric/*`)
5. **Database table naming:** `prismPushTokens` (consistent with `prismBiometricTokens`, `prismDeviceCredentials`)
6. **Device token hashing:** SHA256 (same as biometric token hashing)
7. **Android notification channel ID:** `prism-default` (auto-created at app startup)
8. **Pre-permission explainer timing:** After biometric enrollment, before system permission prompt
9. **In-app notification banner class:** `.prism-notification-banner` (styled in `mobile-overrides.css`)
10. **Deep linking payload pattern:** `{ "data": { "page": "string", "id": "string", "params": "json-string" } }`

---

## Open Questions for Team

1. **Opt-in vs opt-out:** Should push be opt-in (default `false`) or opt-out (default `true`)?  
   **Recommendation:** Opt-in to keep base bundle lean.

2. **Test push UI in backoffice:** Should Prism provide a "Test Push" button in tenant management screen?  
   **Recommendation:** Defer to backend design; not critical for mobile-side implementation.

3. **Admin push broadcasts:** Support sending to all users of a tenant in v1?  
   **Recommendation:** Defer to backend design; mobile-side is agnostic to dispatch strategy.

4. **Silent notifications in v1:** Include data-only message scaffolding?  
   **Recommendation:** No, this is advanced functionality; document as "Future Enhancement."

5. **Multi-tenant token scoping:** Should tokens be scoped to `TenantId`?  
   **Recommendation:** Yes, align with biometric token pattern; prevents cross-tenant token reuse.

---

## Follow-Up Actions

1. **Backend implementation (Blathers):**
   - Implement `POST /umbraco/prism/mobile/push/register` endpoint
   - Implement `DELETE /umbraco/prism/mobile/push/revoke` endpoint
   - Create `prismPushTokens` database table migration
   - Integrate with existing FCM backend design from `docs/notifications-design.md`

2. **Security review (Copper):**
   - Audit token hashing strategy
   - Review CORS headers for push registration endpoints
   - Validate token storage encryption at rest

3. **MobileBundleService implementation (Kicks or Blathers):**
   - Add `PushNotificationsEnabled` property to `PrismMobileBundleRequest`
   - Generate push scaffolding files when enabled
   - Update `bootstrap-ios.sh` and `bootstrap-android.sh` for auto-injection

4. **Testing:**
   - Create test tenant with push enabled
   - Verify iOS + Android permission flows
   - Test token registration, refresh, and revocation
   - Validate foreground + background notification handling

5. **Documentation:**
   - Add push notifications section to main README
   - Update `AGENT_PROMPT.md` with setup instructions
   - Create Firebase Console walkthrough with screenshots

---

**Design Document:** `docs/design/notifications-mobile.md`  
**History Entry:** `.squad/agents/kicks/history.md` (2026-07-14 — Mobile Push Notifications Design)
---
# Brewster — Umbraco Notifications Integration Design Decisions

**Date:** 2026-04-03  
**Author:** Brewster (Umbraco Platform Specialist)  
**Design Document:** `docs/design/notifications-umbraco-demo.md`

---

## Decision 1: Content Notification Hook — Opt-In via Document Type Composition

**Decision:** Use Document Type composition (`notifiableContent`) with editor-controlled toggles for content-triggered notifications.

**Pattern:**
- Create `notifiableContent` composition with properties:
  - `notifyOnPublish` (boolean toggle, default: false)
  - `notificationTitle` (text override)
  - `notificationBody` (textarea, max 200 chars)
  - `notificationGroups` (Member Group Picker, multi-select)
- Apply composition to any document types that should support notifications (e.g., Event, News, Offer)
- Register `PrismContentPublishedHandler : INotificationAsyncHandler<ContentPublishedNotification>`
- Handler checks for composition + toggle, then calls `IPrismNotificationService.SendToMemberGroupsAsync()`

**Why:**
- **Opt-in by default:** Not all content changes should trigger push notifications — editors must explicitly enable
- **Backoffice control:** Editors can customize notification text and target groups without code deployment
- **Flexible composition:** Can add notification capability to any document type without schema rebuild
- **Umbraco-native pattern:** Composition is the recommended v13+ approach for reusable property sets

**Alternative considered:** Code-first attributes (e.g., `[NotifyOnPublish]`) — rejected because it removes editor control and requires code deployment for changes.

**Consumer hook:** Provide `IPrismContentNotificationHandler` interface for advanced scenarios where consumers want custom notification logic (e.g., "only notify if price increased by >10%").

---

## Decision 2: Member Group Integration — Groups as Notification Topics (v1)

**Decision:** Use Umbraco Member Groups as notification audiences. Member Group = Notification Topic.

**Pattern:**
- Create member groups: "Event Subscribers", "News Subscribers", "Offer Subscribers"
- Members join/leave groups via custom controller (`NotificationsHubController`) or backoffice
- `IPrismNotificationService.SendToMemberGroupsAsync()` resolves groups to member IDs, then device tokens
- No additional database tables required — leverages existing `IMemberService.AssignRole()` / `DissociateRole()`

**Why:**
- **Zero schema changes:** Works immediately with existing Umbraco infrastructure
- **Backoffice-editable:** Admins can manage groups via standard Umbraco Members section
- **Familiar pattern:** Umbraco developers already understand member groups
- **Natural fit:** "Subscribe to Event Updates" maps cleanly to "Join Event Subscribers group"

**Alternative considered (v2 candidate):** Custom subscription table with topic keys for fine-grained control (e.g., subscribe to "Sports Events" only, not all events). Deferred to v2 for simplicity.

**Implementation:** `NotificationsHubController` provides member-facing UI to toggle group membership. Route-hijacked controller with checkboxes for each notification category.

---

## Decision 3: Backoffice Integration — Defer to v2

**Decision:** Do NOT implement backoffice notification sending UI in v1. Defer to v2.

**Rationale:**
- **v1 scope:** Focus on developer-triggered notifications (content publish hooks, API endpoints, scheduled tasks)
- **Complexity:** Backoffice extension requires Umbraco v14+ Lit Web Components + permission checks + rate limiting
- **Immediate value:** Automatic content-triggered notifications and backend scheduled tasks cover 80% of use cases
- **v2 design ready:** Provided design sketch for Lit Web Component dashboard in Members section with "Send Notification" form

**v1 Alternatives:**
- Content publish hook with `notifyOnPublish` toggle (automatic)
- Test API endpoint (`/umbraco/prism/notification/test`) for development testing
- Scheduled tasks (e.g., membership expiry notifications)

**v2 Design:**
- Dashboard in Members section
- Lit Web Component form: title, body, member group picker, send button
- Permission model: require `PrismConfiguration.AdminGroups` membership
- Rate limiting to prevent accidental mass notifications

---

## Decision 4: Scheduled Task Pattern — IHostedService with Runtime Level Check

**Decision:** Use ASP.NET Core `IHostedService` with Umbraco `IRuntimeState.Level` check for scheduled notifications.

**Pattern:**
```csharp
public class NotificationTask : IHostedService, IDisposable
{
    private readonly IRuntimeState _runtimeState;
    
    public Task StartAsync(CancellationToken ct)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
            return Task.CompletedTask; // Don't run during install/upgrade
        
        _timer = new Timer(DoWork, null, initialDelay, period);
        return Task.CompletedTask;
    }
    
    private void DoWork(object? state)
    {
        using var scope = _serviceProvider.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IMemberService>();
        // Use scoped services here
    }
}
```

**Why:**
- **Umbraco-aware:** `RuntimeLevel.Run` check prevents tasks from running during install/upgrade
- **ASP.NET Core standard:** No custom Umbraco abstractions (pre-v13 `IRecurringBackgroundTask` removed)
- **Scoped service access:** Correctly creates scoped service provider for `IMemberService`, `IContentService`, etc.
- **Simpler than Hangfire:** For daily/hourly tasks, `IHostedService` is sufficient (no external dependencies)

**Registration:** `builder.Services.AddHostedService<NotificationTask>()` in Composer

**Use cases:**
- Daily membership expiry notifications (9 AM daily)
- Auto-approve form submissions after 48 hours
- Weekly digest notifications

---

## Decision 5: Demo Scenario — Form Review Notification (Recommended)

**Decision:** Recommend "Form Review Notification" as primary backend-triggered notification demo.

**Scenario:**
1. Member submits document access request via form
2. Form submission saved to `PrismFormSubmissions` table with status "Pending"
3. (Simulated) Admin reviews request via API endpoint → updates status → sends notification "Request Approved ✓"
4. (Bonus) Scheduled task auto-approves requests older than 48 hours → sends notification

**Why this wins:**
- **Realistic enterprise scenario:** Form workflow with member notification is extremely common in Umbraco
- **Demonstrates both triggers:** API-triggered (admin approval) AND scheduled (auto-approval after 48 hours)
- **Fits member portal architecture:** TestSite already has Member Dashboard; "My Requests" page fits naturally
- **Self-contained demo:** No external dependencies, easy to demonstrate

**Document Types:**
- `requestsHub` — displays member's form submissions with status badges
- `requestForm` — form UI for submitting document access request
- `membershipHub` (runner-up scenario) — membership status page

**API Endpoint:**
- `POST /umbraco/prism/requests/{id}/review` — admin-only endpoint to approve/reject
- `[Authorize(AuthenticationSchemes = "PrismBackoffice")]` — requires backoffice auth

**Scheduled Task:**
- `AutoApproveRequestsTask : IHostedService` — runs every 6 hours, auto-approves old requests

**Runner-up:** "Membership Expiry Notification" (simpler, pure scheduled task demo). Recommended if Jonny wants minimal complexity.

---

## Decision 6: Demo Site Document Type Schema

**Decision:** Create document types using Umbraco v13+ patterns (compositions, strongly-typed models).

**New Document Types:**
- `notificationsHub` — member subscription management page
- `requestsHub` — view member's form submissions
- `requestForm` — submit document access request
- `membershipHub` — membership status + renewal (runner-up scenario)
- `eventPage` — example notifiable content

**Compositions:**
- `notifiableContent` — adds notification properties to any document type (opt-in pattern)
- `contentBase` (if exists) — SEO fields, shared properties

**Member Type Properties:**
- `membershipExpiry` (DateTime) — for expiry notification demo
- `notificationPreferences` (CheckBoxList) — visual display of group membership (read-only)

**Member Groups:**
- "Event Subscribers"
- "News Subscribers"
- "Offer Subscribers"

**Content Tree:**
```
Home
├── Member Dashboard (existing)
├── Notifications (new)
├── My Requests (new)
├── Membership (new)
└── Settings (existing)
```

**Controllers:**
- `NotificationsHubController` — route-hijacked, handles subscription toggle POST
- `RequestsHubController` — displays member's submissions
- `RequestFormController` — handles form submission POST
- `RequestReviewController` — API endpoint for admin approval
- `MembershipHubController` — displays membership status + expiry

**Seeders:**
- `NotificationSchemaSetup` — creates document types, compositions, member groups (dev-only, idempotent)
- `DemoNotificationContentSeeder` — creates sample events/news with `notifiableContent` composition

---

## Technical Conventions Established

1. **Route hijacking for member pages:** `{DocumentTypeAlias}Controller : RenderController` with `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]`

2. **Notification handler registration:** `builder.AddNotificationAsyncHandler<ContentPublishedNotification, PrismContentPublishedHandler>()`

3. **Member group resolution:** Use `IMemberService.GetAllMembersOfGroup(groupId)` to find member IDs, then `IPrismDeviceCredentialRepository.GetByMemberIdsAsync()` to get device tokens

4. **Scheduled task lifecycle:** `StartAsync()` checks `IRuntimeState.Level`, `DoWork()` creates scoped service provider, `Dispose()` cleans up timer

5. **Document Type composition naming:** Use `-able` suffix (e.g., `notifiableContent`) to indicate composable behavior

6. **Notification service interface:** Three send methods (member groups, individual member, broadcast) + audit log query method

---

## Follow-Up Tasks for Other Squad Members

**Blathers (Core Library):**
- Implement `IPrismNotificationService` interface
- Implement `IPushNotificationProvider` (APNs/FCM adapters)
- Create `PrismNotificationLog` database table (audit trail)
- Create `PrismFormSubmissions` database table (for demo)
- Register services in `PrismComposer`

**Isabelle (Frontend):**
- Update `prism-mobile-nav` to handle notification deep links (if needed)
- Consider adding notification badge/count to nav icon (future enhancement)

**Celeste (Documentation):**
- Document `IPrismNotificationService` API once implemented
- Write consumer guide: "Adding notifications to your content types"
- Document scheduled task pattern with code examples

**Copper (Security Review):**
- Review notification service rate limiting (prevent spam)
- Review admin API endpoint permissions (`RequestReviewController`)
- Review notification payload sanitization (prevent XSS in notification text)

---

**End of Decision Document**
---
# Brewster — Vinyl Vault Demo Design Decisions

**Date:** 2026-04-03  
**Session:** Vinyl Vault Demo Redesign  
**Author:** Brewster (Umbraco Platform Specialist)

---

## Decision: Vinyl Record Shop Theme for Notifications Demo

**Context:**

The previous notifications demo design (document access requests + membership expiry) was deemed less engaging and harder to relate to for developers evaluating the package. A new demo theme was requested that would be fun, relatable, and immediately understandable.

**Decision:**

Adopt "Vinyl Vault" — a vintage vinyl record shop — as the demo theme for showcasing push notifications in UmbracoPrism.TestSite.

**Rationale:**

1. **Instant relatability:** Everyone understands "new stock arriving" and "limited edition drops" without explanation
2. **Content-driven:** Vinyl records are rich content nodes (artist, album, genre, cover art, year) that showcase Umbraco's content modeling
3. **Natural subscription model:** Genre-based subscriptions (Jazz, Rock, Electronic) mirror real-world preferences and are easy to explain
4. **Visual appeal:** Album cover art makes notifications more engaging than plain text
5. **Multiple notification triggers:** Demonstrates all three use cases naturally:
   - Content publish → new arrival notification
   - API trigger → back-in-stock waitlist alert
   - Scheduled task → limited edition drop advance warning

**Implications:**

- Replace existing demo document types with `vinylRecord`, `genre`, `vinylVaultHub`, `notificationSubscriptions`
- Seed demo content with real artists/albums for authentic feel (Miles Davis, Pink Floyd, Daft Punk, etc.)
- Design member subscription UX around genre preferences
- Create 5-minute walkthrough script for comprehensive demo
- Create 2-minute quick demo for evaluators with limited time

---

## Decision: Genre-Based Member Groups for Subscriptions

**Context:**

Members need to subscribe to notification categories. Two options:
- Option A: Use Umbraco member groups (e.g., "Jazz Subscribers")
- Option B: Custom subscription table with fine-grained topic control

**Decision:**

Use Umbraco member groups for v1 demo (Option A).

**Rationale:**

1. **Zero schema changes:** Leverages existing Umbraco member group infrastructure
2. **Backoffice-editable:** Editors can manage groups via Umbraco UI
3. **Familiar pattern:** Umbraco developers already understand member groups
4. **Simpler implementation:** No additional database tables or migrations
5. **Sufficient for demo:** Genre-level subscriptions adequately showcase the notification system

**Member groups created:**
- Jazz Subscribers
- Rock Subscribers
- Electronic Subscribers
- Hip-Hop Subscribers
- Classical Subscribers
- All New Stock Subscribers
- VIP Members

**Alternative (future):**

For production use cases requiring fine-grained control (e.g., subscribe to specific artists, price ranges, or content tags), consider adding a custom `PrismMemberSubscriptions` table. But for demo purposes, member groups are ideal.

---

## Decision: Three Notification Use Cases in One Demo

**Context:**

The demo needs to showcase different notification trigger patterns to demonstrate the full capability of the system.

**Decision:**

Implement all three use cases in the Vinyl Vault demo:

1. **Content subscription notifications** (automatic, content-driven)
   - Editor publishes vinyl → subscribers notified
   - Trigger: `ContentPublishedNotification`
   
2. **Back-in-stock alerts** (manual/API-triggered, business logic-driven)
   - Member joins waitlist → stock restored → waitlist notified
   - Trigger: API endpoint `/umbraco/api/vinylvault/notify-back-in-stock/{id}`
   
3. **Limited edition drop alerts** (scheduled, time-based)
   - Background task detects upcoming drop → advance notice sent
   - Trigger: `IRecurringBackgroundTask` (runs every 5 minutes)

**Rationale:**

1. **Comprehensive showcase:** Demonstrates notifications aren't limited to content publish events
2. **Real-world patterns:** All three patterns are common in e-commerce and membership scenarios
3. **Developer education:** Shows when to use each trigger mechanism
4. **Single demo walkthrough:** All three can be demonstrated in under 15 minutes

**Implementation notes:**

- Use Case 1 is the primary focus (easiest to demonstrate)
- Use Cases 2 and 3 can be explained verbally in quick demo version
- All three are fully functional and testable in the 5-minute comprehensive demo

---

## Decision: Pre-Seeded Demo Content with Real Artists

**Context:**

Demo content can be either generic/placeholder (e.g., "Vinyl 1", "Vinyl 2") or use real artist/album names.

**Decision:**

Seed demo with real, recognizable artists and albums (Miles Davis "Kind of Blue", Pink Floyd "Dark Side of the Moon", Daft Punk "Random Access Memories", etc.).

**Rationale:**

1. **Authentic feel:** Makes the demo feel like a real application, not a toy example
2. **Instant recognition:** Evaluators immediately understand the content without explanation
3. **Visual appeal:** Real album covers are more engaging than placeholder images
4. **Conversation starter:** Music preferences create natural engagement during demos
5. **Professionalism:** Shows attention to detail in demo design

**Copyright consideration:**

Album cover art used for demo purposes under fair use (non-commercial, educational, demonstration). If test site is publicly deployed, consider:
- Using placeholder covers
- Or licensing album art from music databases
- Or using public domain/Creative Commons album art

For local development and evaluation purposes, fair use applies.

---

## Decision: Flat Content Tree Structure

**Context:**

Vinyl records could be organized as:
- Option A: Flat — `/vinyl-vault/{genre}/` directly under hub
- Option B: Nested — `/vinyl-vault/catalog/{genre}/` with intermediate catalog node

**Decision:**

Use flat structure (Option A).

**Rationale:**

1. **Simpler URLs:** `/vinyl-vault/jazz` instead of `/vinyl-vault/catalog/jazz`
2. **Fewer clicks in backoffice:** Editors navigate directly to genre nodes
3. **No added value from catalog node:** Intermediate node serves no functional purpose
4. **Faster demo setup:** One less document type to create

**Content tree:**
```
Home
└── Vinyl Vault [vinylVaultHub]
    ├── Notifications [notificationSubscriptions]
    ├── Jazz [genre]
    │   └── ... vinyl records ...
    ├── Rock [genre]
    │   └── ... vinyl records ...
    └── ... other genres ...
```

---

## Decision: Waitlist Storage via Member Property (Not Custom Table)

**Context:**

"Back in Stock" waitlist needs to track which members are waiting for which vinyl. Two options:
- Option A: Member property `vinylWaitlist` (comma-separated vinyl IDs)
- Option B: Custom database table `VinylWaitlist`

**Decision:**

Use member property approach for demo (Option A). Leave custom table option documented for production implementations.

**Rationale:**

1. **No migrations needed:** Uses existing Umbraco member infrastructure
2. **Simpler seeder:** Can set member properties directly in seeder code
3. **Adequate for demo:** Limited number of waitlist entries in demo scenario
4. **Quick implementation:** No custom repository or EF models needed

**Implementation:**

- Add `vinylWaitlist` property to Member type (Textarea or Textstring)
- Store as comma-separated vinyl content IDs: "1234,5678,9012"
- Parse and filter when checking waitlist
- Clear specific vinyl ID when back-in-stock notification sent

**Production alternative:**

For production use, recommend custom table with:
- Proper foreign keys (MemberId, VinylContentId)
- Timestamp tracking (CreatedAt)
- Unique constraint (prevent duplicate waitlist entries)
- Better query performance for large datasets

---

## Summary

**Vinyl Vault** demo design decisions prioritize:
- **Relatability:** Vinyl record shop is universally understood
- **Simplicity:** Leverage Umbraco built-in features (member groups, member properties)
- **Completeness:** Showcase all three notification trigger patterns
- **Authenticity:** Real artists/albums for professional feel
- **Developer experience:** 2-minute quick demo + 5-minute comprehensive walkthrough

These decisions balance demo simplicity with production-readiness guidance, allowing developers to quickly evaluate the package while understanding how to scale for real-world use.
---
# Blathers — Notification Service Backend Design Decisions

**Date:** 2026-03-22  
**Author:** Blathers (Backend Developer)  
**Design Document:** `docs/design/notifications-backend.md`  

---

## Decision: Push Notification Service Architecture

**Context:**  
Umbraco.Prism is adding push notification support for mobile apps. Firebase Cloud Messaging (FCM) is the chosen provider. Backend needs to support device token registration, content-node subscriptions, event-triggered notifications, and scheduled notifications.

---

## Key Architectural Decisions

### 1. Service Interface Design

**Decision:** Create `IPrismNotificationService` with four user-centric methods:
- `SendToUserAsync(userOid, payload)` — single user by Entra Object ID
- `SendToUsersAsync(userOids, payload)` — batch users
- `SendToSubscribersAsync(contentKey, payload)` — all subscribers to a content node
- `BroadcastAsync(payload)` — all registered users in current tenant

**Why:**
- Developers think in terms of users (Entra OIDs), not device tokens.
- Service abstracts FCM complexity (token resolution, batching, error handling).
- Tenant-scoped by default (uses `IPrismContext.CurrentTenant` implicitly).
- `NotificationResult` returns delivered/failed counts + stale tokens for cleanup.

---

### 2. FCM Integration & Credential Management

**Decision:**
- SDK: `FirebaseAdmin` NuGet package (Google official, v3.x+)
- Credentials: Azure Key Vault via new `PrismNotificationKeyVaultConfigureOptions`
- Secret name: `Prism--Notifications--FcmServiceAccountJson` (Firebase service account JSON)
- Config: New `PrismNotificationOptions` class under `Prism:Notifications` section (separate from biometric options)

**Why:**
- `FirebaseAdmin` is the official, best-supported SDK for server-side FCM.
- Key Vault pattern mirrors existing `PrismKeyVaultConfigureOptions` for biometric keys (consistency).
- Separate config section (`Prism:Notifications`) allows independent management of notification settings.
- Zero-config path: If FCM secret is missing, service logs warning + returns no-op results (graceful degradation).

---

### 3. Device Token Storage

**Decision:** Custom database table `prismDeviceTokens` (not Umbraco Member properties).

**Schema:**
- Columns: `TenantId`, `UserId` (Entra OID), `DeviceToken`, `Platform`, `DeviceName`, `RegisteredAt`, `LastNotifiedAt`
- Indexes: `(TenantId, UserId)`, `(TenantId, DeviceToken)` composite
- Multi-device: One row per device; users can have multiple registered devices

**Why NOT Umbraco Member properties:**
- ❌ Umbraco Members are optional in Prism (stateless OIDC = Entra-only users)
- ❌ Multi-device support awkward (one property = one value; arrays in JSON = brittle)
- ❌ No relational querying (subscription joins would require JSON deserialization)

**Why custom table:**
- ✅ Mirrors existing `prismDeviceCredentials` pattern (familiar to developers)
- ✅ First-class multi-device support (one row per device token)
- ✅ Efficient relational queries (join with subscriptions table)
- ✅ No dependency on Umbraco Member model

---

### 4. Subscription Model

**Decision:** Custom table `prismNotificationSubscriptions` for user opt-in to content nodes.

**Schema:**
- Columns: `TenantId`, `UserId`, `ContentKey` (Umbraco content GUID), `SubscribedAt`
- Unique constraint: `(TenantId, UserId, ContentKey)` — prevents duplicate subscriptions
- Indexes: `(TenantId, ContentKey)` for fast subscriber lookups

**Query Pattern:**
1. Fetch all `UserId` where `ContentKey = X` AND `TenantId = Y`
2. Join to `prismDeviceTokens` to resolve device tokens
3. Send to all tokens

**Global Notifications:**
- No subscription table needed
- `BroadcastAsync` queries all device tokens for tenant

**Why:**
- Subscription table enables **opt-in granularity** (user controls which content nodes they follow)
- Unique constraint prevents duplicate subscriptions
- Efficient lookups via `(TenantId, ContentKey)` composite index

---

### 5. Content Event Integration

**Decision:** Use Umbraco's `INotificationAsyncHandler<ContentPublishedNotification>` pattern.

**Handler:** `PrismContentPublishedNotificationHandler`
- Checks content property: `sendPushNotification` (boolean)
- Extracts metadata: `notificationTitle`, `notificationBody`, `notificationImage`
- Sends to subscribers via `IPrismNotificationService.SendToSubscribersAsync()`
- Non-blocking: Try/catch wrapper ensures notification failures don't block publishing

**Why:**
- Standard Umbraco pattern for content lifecycle events
- Non-blocking (publish succeeds even if notification fails)
- Opt-in per content item (editor controls via checkbox)
- Tenant-scoped via `IPrismContext.CurrentTenant`

---

### 6. Scheduled Notifications

**Decision:** Use `IRecurringBackgroundTask` for scheduled/digest notifications.

**Example:** `PrismDailyDigestTask` (runs every 24 hours)
- Explicitly iterates tenants (no `IPrismContext` in background tasks)
- Uses `IServiceProvider.CreateScope()` for scoped service resolution
- Calls `BroadcastAsync` or subscription-based send

**Why:**
- Standard Umbraco pattern for scheduled tasks
- Decoupled from HTTP request lifecycle
- Scoped service resolution ensures proper DI lifetime management

---

### 7. API Endpoints

**Decision:** `NotificationController` with 4 endpoints:

| Endpoint                                  | Method | Purpose                                |
|-------------------------------------------|--------|----------------------------------------|
| `/umbraco/prism/notifications/register`   | POST   | Register FCM device token (upsert)     |
| `/umbraco/prism/notifications/subscribe`  | POST   | Subscribe to content node              |
| `/umbraco/prism/notifications/unsubscribe`| POST   | Unsubscribe from content node          |
| `/umbraco/prism/notifications/subscriptions` | GET | List user's subscriptions              |

**Authentication:** `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` (biometric JWT required)

**Tenant Isolation:** All queries filter by `IPrismContext.CurrentTenant.Id`

**Why:**
- RESTful design; simple CRUD operations
- Idempotent (register upserts; subscribe checks duplicates)
- Consistent with existing `BiometricController` auth pattern

---

### 8. Error Handling & Resilience

**Decision:** Polly resilience pipeline with retry + circuit breaker.

**Configuration:**
- Retry: 3 attempts, exponential backoff (1s initial delay)
- Circuit Breaker: 0.5 failure ratio, 2 min sampling, 1 min break duration
- Pipeline order: Circuit Breaker (outer) → Retry (inner) → FCM call

**Stale Token Handling:**
- FCM returns `MessagingErrorCode.Unregistered` → auto-delete from `prismDeviceTokens`
- `NotificationResult.StaleTokens` list returned to caller for tracking

**Delivery Model:** Fire-and-forget with resilience (no queue infrastructure for MVP)

**Why:**
- Consistent with existing `PrismTokenRefreshService` pattern
- Circuit breaker placement (outer) samples ONE failure per exhausted retry sequence (not per HTTP attempt)
- Stale token cleanup prevents wasted sends to dead tokens
- Fire-and-forget simpler than queueing; sufficient for most use cases

---

### 9. Composer Registration

**Decision:** Register all services in `PrismComposer.Compose`:

```csharp
// Notification Services
builder.Services.Configure<PrismNotificationOptions>(
    builder.Config.GetSection(PrismNotificationOptions.SectionName));
builder.Services.ConfigureOptions<PrismNotificationKeyVaultConfigureOptions>();
builder.Services.AddSingleton<IPrismNotificationService, PrismNotificationService>();

// Notification Event Handlers
builder.AddNotificationAsyncHandler<ContentPublishedNotification, PrismContentPublishedNotificationHandler>();

// Optional: Scheduled Tasks (commented out by default)
// builder.Services.AddHostedService<RecurringBackgroundTaskHostedService<PrismDailyDigestTask>>();
```

**Zero-Config Path:**
- If `Prism--Notifications--FcmServiceAccountJson` secret is missing in Key Vault:
  - Service logs warning: `"FCM credentials not configured. Notification methods will return no-op results."`
  - All send methods return `NotificationResult { IsSuccess = false, ErrorMessage = "...not configured..." }`
  - Package installation doesn't crash

**Why:**
- Centralized DI registration (follows existing pattern)
- Zero-config graceful degradation (sites without notifications don't break)
- Clear warning messages guide developers to configuration steps

---

## Database Schema Changes

### New Tables (Migrations Required)

1. **`prismDeviceTokens`**
   - Migration: `CreatePrismDeviceTokensTable`
   - Columns: `id`, `TenantId`, `UserId`, `DeviceToken`, `Platform`, `DeviceName`, `RegisteredAt`, `LastNotifiedAt`
   - Indexes: `IX_PrismDeviceTokens_TenantId`, `IX_PrismDeviceTokens_UserId`, `IX_PrismDeviceTokens_DeviceToken` (composite)

2. **`prismNotificationSubscriptions`**
   - Migration: `CreatePrismNotificationSubscriptionsTable`
   - Columns: `id`, `TenantId`, `UserId`, `ContentKey`, `SubscribedAt`
   - Unique Constraint: `UX_PrismSubscriptions_UserContent` (`TenantId`, `UserId`, `ContentKey`)
   - Indexes: `IX_PrismSubscriptions_TenantId`, `IX_PrismSubscriptions_UserId`, `IX_PrismSubscriptions_ContentKey` (composite)

**Migration Plan Update:**

```csharp
protected override void DefinePlan()
{
    To<CreatePrismTables>("initial-state")
    // ... existing migrations ...
    .To<AddAllowBiometricLoginColumn>("add-allow-biometric-login")
    .To<CreatePrismDeviceTokensTable>("add-device-tokens")
    .To<CreatePrismNotificationSubscriptionsTable>("add-notification-subscriptions");
}
```

---

## Configuration Example

### appsettings.json (Local Dev)

```json
{
  "Prism": {
    "VaultUri": null,
    "Notifications": {
      "FcmProjectId": "umbraco-prism-dev",
      "FcmServiceAccountJson": "{...Firebase service account JSON...}",
      "DryRun": true,
      "BatchSize": 500,
      "MaxRetryAttempts": 3,
      "RetryDelaySeconds": 1.0,
      "CircuitBreakerFailureRatio": 0.5
    }
  }
}
```

### appsettings.Production.json

```json
{
  "Prism": {
    "VaultUri": "https://myprismvault.vault.azure.net/",
    "Notifications": {
      "FcmProjectId": "umbraco-prism-prod"
    }
  }
}
```

**Note:** Production credentials fetched from Key Vault; local dev uses inline JSON (never commit to source control).

---

## Implementation Phases (Recommendation)

1. **Phase 1: Foundation** — Options, tables, core service, composer registration
2. **Phase 2: API Endpoints** — Controller + request/response models
3. **Phase 3: Content Integration** — Published notification handler
4. **Phase 4: Scheduled Tasks** — Optional digest/cron tasks (commented out by default)
5. **Phase 5: Testing & Docs** — Unit tests, README updates, Firebase setup guide

---

## Open Questions for Product Owner

1. **Content Type Seeding:** Auto-add notification properties to existing content types, or document manual setup?
2. **Subscription UI:** Backoffice UI for viewing/managing user subscriptions, or API-only sufficient?
3. **Rate Limiting:** Per-tenant send limits (e.g., max 1000/hour)?
4. **Analytics:** Delivery metrics (dashboard, logs, telemetry)?
5. **Multi-language:** Notification content localization (Umbraco variants, custom logic)?

---

## Impact on Other Squad Members

- **Isabelle (Web Components):** May need mobile app components for subscription UI (if product owner decides to build backoffice UI).
- **Tangy (Testing):** Will need to write unit tests for service logic (mock FCM client), integration tests for API endpoints.
- **Mabel (Documentation):** Will need to document Firebase Console setup, Key Vault secret creation, appsettings configuration.
- **Copper (Security):** Should review FCM credential storage pattern, API authorization model, stale token cleanup logic.
- **Celeste (Documentation):** Should add XML docs to new service interfaces and public methods.

---

**Handoff:** Design document complete at `docs/design/notifications-backend.md`. Ready for product owner review and team feedback before implementation begins.
---
### 2026-04-03: Notifications feature decisions
**By:** Jonny Muir (via Copilot)

**Decision 1 — Device token storage:** Extend existing `prismDeviceCredentials` table with a `PushToken` column. Do not create a separate `prismDeviceTokens` table. One device = one row, whether it has biometrics, push, or both. Aligns with Tom Nook's recommendation (simpler, leaner).

**Decision 2 — Push notifications are opt-in:** `PushNotificationsEnabled` defaults to `false` in `PrismMobileBundleRequest`. Consumers who don't need push pay zero bundle cost. Aligns with Kicks' recommendation.

**Decision 3 — Demo theme is a record shop:** The demo for the notifications feature will be a vinyl record shop ("Vinyl Vault" or similar). Members subscribe to genres (or all genres). When new vinyl is stocked (content published), subscribed members receive a push notification. This covers Use Case 1 (content-driven, genre subscription) and can also demonstrate Use Case 2 (backend-triggered, e.g. a "back in stock" or "limited edition drop" alert).

---

## Blathers — Phase 1 Notifications Implementation Decisions

**Author:** Blathers  
**Date:** 2026-07-07  
**Status:** Implemented  
**PR:** feat(notifications): Phase 1

### Decision 1: Subscription model uses `Genre` (not `Topic`)

The design doc used `Topic` as the subscription field name; the task spec uses `Genre`. Implemented as `Genre` throughout (`prismNotificationSubscriptions.Genre`, `SubscribeToGenreAsync`, etc.) to match the task's method signatures and intent.

### Decision 2: `IPrismNotificationService` follows task method signatures, not design doc interface

The design doc specified a tenant-scoped, result-returning interface (`SendToUserAsync`, `BroadcastAsync`, etc.). The task spec defines a different, simpler set of methods. The task spec takes precedence as the authoritative deliverable.

### Decision 3: FirebaseAdmin named instance (`prism-notifications`) guards against duplicate init

`FirebaseApp.Create` throws on duplicate registration. Used `FirebaseApp.GetInstance(name)` with a named instance `"prism-notifications"` and a try/catch guard. This is safe for scoped service lifetime because `FirebaseApp` is a static singleton in the Firebase SDK.

### Decision 4: `PrismNotificationService` registered as `Scoped` (not Singleton)

Follows the task spec and is appropriate because `IUmbracoDatabaseFactory` is consumed per-request. `FirebaseMessaging` is obtained from a static Firebase SDK singleton so it is safely shared across scoped instances.

### Decision 5: Device-only registration creates a minimal credential stub

When `RegisterDeviceTokenAsync` is called for a user with no existing `prismDeviceCredentials` row (e.g. non-biometric user), a minimal stub row is inserted. This keeps push notifications independent of biometric auth — a user doesn't need biometric registration to receive push notifications. The stub has a 10-year expiry and an empty `TokenHash`.

### Decision 6: `PrismContentPublishedHandler` reads `prismTenantId` property for tenant resolution

The content publish pipeline has no ambient tenant context (`IPrismContext` is request-scoped and not available in background notification handlers). To resolve the target tenant, the handler reads a `prismTenantId` content property. Content without this property is silently skipped (logged at Debug level).

### Decision 7: Notification handler uses `INotificationAsyncHandler<T>` pattern

Consistent with all other Umbraco notification handlers in the codebase (`PrismMigrationHandler`, `PrismContentTypeSeeder`, etc.). Registered via `AddNotificationAsyncHandler<ContentPublishedNotification, PrismContentPublishedHandler>()` in `PrismComposer`.

### Decision 8: Stale FCM token cleanup is in-band (synchronous after each batch)

Stale tokens flagged by FCM (`UNREGISTERED`) are nullified in `prismDeviceCredentials` immediately after each batch completes. No separate cleanup job needed for v1. Cleanup failures are swallowed with a warning log to avoid breaking the notification send.

---

## Decision: Use IServiceScopeFactory in BackgroundService Implementations

**Author:** Blathers  
**Date:** 2026-06-19  
**Status:** Accepted

### Context

`LimitedEditionDropNotifier` (a `BackgroundService`) was directly injecting `IPrismNotificationService` (scoped) into its constructor. Because `BackgroundService` is registered as a singleton, this creates a captive dependency that causes `System.InvalidOperationException` at startup.

### Decision

**All `BackgroundService` implementations in UmbracoPrism.Core MUST use `IServiceScopeFactory` to consume scoped services.** Scoped services must never be constructor-injected into singletons.

The pattern to follow:

```csharp
public MyBackgroundService(IServiceScopeFactory scopeFactory, ...)
{
    _scopeFactory = scopeFactory;
}

private async Task DoWorkAsync(CancellationToken ct)
{
    await using var scope = _scopeFactory.CreateAsyncScope();
    var svc = scope.ServiceProvider.GetRequiredService<IScopedService>();
    await svc.DoSomethingAsync(ct);
}
```

### Consequences

- Scoped services (including EF DbContext, per-request caches, etc.) are properly lifetime-managed per background operation
- No risk of stale state leaking across background task invocations
- Consistent with Microsoft's recommended pattern for hosted services

### Applies To

Any current or future `BackgroundService` or `IHostedService` implementation in this codebase.

---

## Decision: Android Bootstrap Script Fixes

**Date:** 2026-06-21
**Author:** Kicks (Mobile Native Specialist)

### 1. Use `perl` for AndroidManifest.xml INSERT operations

**Rule:** Never use `sed -i 'addr\i\text'` (INSERT command) in generated shell scripts. Use `perl -i -pe 's|pattern|replacement\noriginal|'` instead.

**Why:** BSD sed (macOS) requires a literal newline after `\i`, so the GNU inline form `i\text` causes a fatal parse error. `perl` behaves identically on macOS and Linux for this pattern.

**Applies to:** Any future manifest/plist injection in generated bootstrap scripts.

### 2. Upgrade Gradle wrapper to 8.14 in Android bootstrap

**Rule:** After `npx cap add android`, always upgrade `android/gradle/wrapper/gradle-wrapper.properties` to Gradle 8.14 before running `npx cap sync android`.

**Why:** `@capacitor/android@7.0.0` ships Gradle 8.11.1, which only supports Java up to version 23. Gradle 8.14 supports Java 25. Developers on modern JDKs (Java 25+) will hit a fatal Groovy compilation error otherwise.

**Note:** `sed -i.bak 's/pattern/replacement/'` (substitution) is safe on both macOS and Linux — only the INSERT command `i\` was problematic.

---

## Decision: Cloudflare-only Maintenance Handling

**Date:** 2025-01-23  
**By:** Jonny (via Copilot)

**What:** Maintenance/error handling (502/504/network down) for the mobile app is handled solely at the Cloudflare level. No changes required in the Capacitor app or ASP.NET backend.  

**Why:** Cloudflare is already in the request path for all mobile traffic. Adding app-level or backend-level maintenance detection would add complexity without meaningful benefit. Cloudflare Custom Pages handle the user-facing experience.

---

---

## Decision: Media Library Picker — Image Variable CSS Format

**Author:** Isabelle (Frontend Dev)  
**Date:** 2025-07  
**Status:** Implemented

### Decision

When a user picks an image from the Umbraco media library for a `type: image` CSS variable in the tenant branding editor, the stored value is wrapped in CSS `url('...')` format (e.g. `url('/media/abc.jpg')`).

When the user types a URL directly in the free-text input, the value is stored as-is (no wrapping applied). Users who need CSS `url()` format can type it themselves.

### Rationale

- CSS custom properties used for `background-image` require `url('...')` syntax to work correctly
- Picker-selected media is always intended for CSS background usage, so auto-wrapping is correct
- Free-text input is for advanced users who may want plain URLs (e.g. for `<img src>`) or already-formatted `url(...)` values — leave it unmodified

### Implications

- Any code consuming these branding override values for background-image CSS should expect `url('...')` wrapped values from the picker path
- Preview thumbnail strips the wrapper before setting `<img src>` to avoid broken images
- Future image picker improvements should preserve this distinction (picker = CSS url(), free-text = as-is)

---

## Decision: Static wwwroot Seed Assets for Umbraco Media Library

**Author:** Blathers (Backend Dev)  
**Date:** 2026-06-26

### Context

The branding editor needs a hero image seeded into the Umbraco media library for Isabelle's tenant branding image picker to have content on first run.

### Decision

Commit a small static seed image under `src/UmbracoPrism.TestSite/wwwroot/media/branding/`. The `PrismStarterContentSeeder` references this static path when creating the Umbraco `IMedia` record. The `umbracoFile` property is set to a JSON blob `{"src": "/media/branding/prism-hero.jpg"}` pointing to the static asset.

### Conventions Adopted

- Seed media assets live under `wwwroot/media/branding/` (gitignore exception carved out with `!/wwwroot/media/branding/**`)
- All media seeding is idempotent: check `GetRootMedia()` for the folder before creating
- Use `GetPagedChildren(parentId, 0, 100, out _)` — `GetChildren()` is not available on `IMediaService` in Umbraco 17
- Deterministic GUID keys for seeded data types (`PrismMediaPickerDataTypeKey = a2b3c4d5-...`) so re-runs find the same record

### Why

Avoids requiring a live Unsplash/external download at startup, keeps the repo self-contained for CI and offline dev, and follows the existing pattern established by `DemoMobileNavSeeder`.

### Impact

- `PrismStarterContentSeeder` now depends on `IMediaService` (added to primary constructor)
- `PrismContentTypeSeeder` now seeds a `heroImage` (Media Picker 3) property on the `homePage` doc type
- The Management API endpoint `GET /umbraco/management/api/v1/media/{id}` is the standard Umbraco 17 built-in; no auth policy changes needed (it is not blocked by `PrismAdmins` or `PrismStrictIsolation`)

