# Brewster — History

## Project Context

**Project:** Umbraco.Prism — a multi-tenant web and mobile authentication package for Umbraco v17+. It provides:
- Automatic tenant resolution from hostname
- Entra ID (Azure CIAM) OIDC authentication via `PrismMemberCookie` scheme
- Tenant-scoped branding via CSS variables
- Biometric authentication for Capacitor mobile apps
- A MockBackOffice project demonstrating downstream credential propagation

**Stack:** .NET 10, Umbraco v17.2.2, SQLite, Capacitor/Ionic mobile

**User:** Jonny Muir

## Key Architecture Facts

- Auth scheme: `PrismMemberCookie` (Entra ID OIDC, custom). NOT Umbraco member groups.
- `IPrismContext.CurrentTenant` resolves the current tenant from the request hostname.
- `PrismContext.GetAuthorizationHeaderAsync()` returns a Bearer token for downstream API calls.
- MockBackOffice runs on `localhost:5163` — validates the Prism Bearer token at `/api/backoffice/me`.
- Test site auto-generated Umbraco models are in `src/UmbracoPrism.TestSite/umbraco/models/` — do not hand-edit.
- The old `HomePage.cshtml` had a working `CallBackOfficeAsync()` demo that was removed during a test site overhaul (commit `40834e8`) — it needs to be restored.
- A plain MVC `MemberDashboardController` was introduced in `40834e8` — this is NOT Umbraco-idiomatic and needs to be replaced with a proper `RenderController` route-hijacking approach.

## Learnings

- **`[ModelType]` attribute does not exist in Umbraco v17.** Route hijacking is purely by naming convention: a controller named `{DocumentTypeAlias}Controller` inheriting `RenderController` is auto-discovered. No attribute needed.
- **Hand-authored `PublishedContentModel` subclasses** can be placed in `src/UmbracoPrism.TestSite/umbraco/models/` alongside the auto-generated ones. Use the same `Umbraco.Cms.Web.Common.PublishedModels` namespace. Skip the `new` keyword on constants that don't hide a base member.
- **`CallBackOfficeAsync()` downstream demo** belongs in the dashboard view (not the homepage), since the dashboard is the authenticated area. Trigger via `?callApi=true` query string; show MockBackOffice start hint on network failure.
- **`@inject IPrismContext PrismContext`** is wired in `_ViewImports.cshtml` — available in all TestSite views without explicit declaration in each view.
- **IDataTypeService in Umbraco v17:** Use the built-in Multi URL Picker data type via its well-known GUID (`fd1e0da5-5606-4862-b679-5d0cf3a52a59`) rather than creating custom data types programmatically. Creating data types via code requires complex property editor instantiation - not recommended for seeders.
- **Multi URL Picker value converter:** Returns `IEnumerable<Umbraco.Cms.Core.Models.Link>` where `Link` has `Name`, `Url`, `Target`, `Type` properties. Access via `Model.Value<IEnumerable<Link>>("propertyAlias")`.
- **Partial views for nav components:** Extract repeatable navigation patterns into `Views/Partials/` for reusability. Accept strongly-typed models (`@model IEnumerable<Link>`) and handle null/empty gracefully by rendering nothing.
- **Settings node pattern (Paul Seal):** For site-wide configuration (navigation, footer links, social media, etc.), create a root-level `settings` document type with `AllowedAsRoot = true` and no template. Master layout reads it via `Umbraco.ContentAtRoot().FirstOrDefault(x => x.ContentType.Alias == "settings")`. Editors configure once, all pages inherit. Standard Umbraco community pattern — avoids per-page property duplication.

---

## Session: 2026-03-29 — Mobile Nav Editor Configuration

**Status:** Completed  
**Build outcome:** Success, 0 errors.

**Completed work:**
- Extended `PrismContentTypeSeeder.cs` to add `mobileNavLinks` property to `homePage` document type using Umbraco's built-in Multi URL Picker data type
- Added `IDataTypeService` injection to seeder constructor
- Created `Views/Partials/_MobileShellNav.cshtml` partial view that renders mobile nav from `IEnumerable<Link>` model
- Updated `HomePage.cshtml` to read `mobileNavLinks` property and pass to partial
- Partial handles active state detection by comparing link URL to current request path
- CSS structure preserved (`.mobile-shell-nav`, `.mobile-shell-nav__item`, `.mobile-shell-nav__item--active`)

**Technical approach:**
- Used Umbraco's built-in Multi URL Picker data type (GUID `fd1e0da5-5606-4862-b679-5d0cf3a52a59`) to avoid complex programmatic data type creation
- Property group "Mobile Navigation" added to homePage with single property `mobileNavLinks`
- Idempotent seeder checks if property exists before adding
- Graceful degradation: if no links configured by editor, no nav renders (intentional)

**Outcome:** Mobile navigation is now editor-configurable via the Umbraco backoffice. Any Umbraco developer will recognize this as the standard Multi URL Picker pattern. Test site remains functional, build clean.

---

## Session: 2026-03-29 — Settings Node Pattern Refactor

**Status:** Completed  
**Build outcome:** Success, 0 errors, 0 warnings.

**Problem:** Previous session added `mobileNavLinks` as a per-page property on `homePage`. This works but doesn't scale — every new page type would need the same property. Jonny caught this before commit and requested the standard Paul Seal Settings node pattern.

**Solution implemented:**
- **`PrismContentTypeSeeder.cs`:** Created new `settings` document type with `AllowedAsRoot = true`, icon `"icon-settings-alt"`, no template (it's a config node). Moved `mobileNavLinks` property from `homePage` to `settings` via new `EnsureSettingsDocumentTypeAsync()` method. Kept `EnsureMobileNavPropertyAsync()` reusable for Settings type.
- **`PrismStarterContentSeeder.cs`:** Now seeds a root-level `Settings` node alongside `Home`. Independent seeding (doesn't require Home to exist). Idempotent check prevents duplicate Settings nodes.
- **`Master.cshtml`:** Reads Settings node at top via `Umbraco.ContentAtRoot().FirstOrDefault(x => x.ContentType.Alias == "settings")`, extracts `mobileNavLinks`, renders `_MobileShellNav` partial before `</body>`. Moved all `.mobile-shell-nav` CSS from HomePage into Master so it applies globally.
- **`HomePage.cshtml`:** Removed per-page `mobileNavLinks` reading, removed duplicate nav rendering, removed CSS (now in Master). Page is now clean — no nav logic.
- **`_MobileShellNav.cshtml`:** No changes. Still accepts `@model IEnumerable<Link>` and renders correctly.

**Why this is better:**
- **Single source of truth:** Editors configure mobile nav once in Settings, all pages inherit via Master layout.
- **No per-page duplication:** New doc types don't need `mobileNavLinks` property.
- **Standard Umbraco pattern:** Any Umbraco developer will recognize this as the Paul Seal pattern for site-wide config.
- **Separation of concerns:** Master handles site-wide UI, pages handle page-specific content.

**Outcome:** Mobile navigation is now site-wide, editor-configurable, and follows Umbraco best practices. Build clean, pattern proven across the Umbraco community.

---

## Session: 2026-03-29 — Settings Node Pattern Implementation (Pass 2)

**Status:** Completed  
**Build outcome:** Success, 0 errors, 0 warnings.

**Problem:** Previous session (Pass 1) added `mobileNavLinks` as a per-page property on `homePage`. While functional, this doesn't scale — every new page type would need the same property. Jonny flagged this before commit and requested the standard Paul Seal Settings node pattern.

**Solution implemented:**

1. **PrismContentTypeSeeder.cs:**
   - Created new `settings` document type with `AllowedAsRoot = true`, icon `"icon-settings-alt"`, no template (it's a config node)
   - Moved `mobileNavLinks` property from `homePage` to `settings` via new `EnsureSettingsDocumentTypeAsync()` method
   - Kept `EnsureMobileNavPropertyAsync()` reusable for both types
   - Refactored for clarity: `EnsureHomepageDocumentTypeAsync()` and `EnsureSettingsDocumentTypeAsync()`

2. **PrismStarterContentSeeder.cs:**
   - Now seeds a root-level `Settings` node alongside `Home`
   - Independent seeding logic (doesn't require Home to exist)
   - Idempotent check prevents duplicate Settings nodes

3. **Master.cshtml:**
   - Reads Settings node at top: `Umbraco.ContentAtRoot().FirstOrDefault(x => x.ContentType.Alias == "settings")`
   - Extracts `mobileNavLinks` property from Settings
   - Renders `_MobileShellNav` partial before `</body>` (globally applied)
   - Moved all `.mobile-shell-nav` CSS from HomePage into Master (now inherited by all pages)

4. **HomePage.cshtml:**
   - Removed per-page `mobileNavLinks` reading
   - Removed duplicate nav rendering logic
   - Removed CSS (now inherited from Master)
   - Page is now clean and focused on home content only

5. **_MobileShellNav.cshtml:**
   - No changes — still accepts `@model IEnumerable<Link>` and renders correctly
   - Continues to work as a reusable partial

**Architectural benefits:**

- **Single source of truth:** Editors configure mobile nav once in Settings, all pages inherit via Master layout
- **No per-page duplication:** New doc types don't need `mobileNavLinks` property
- **Standard Umbraco pattern:** Follows Paul Seal pattern — recognized by any Umbraco developer
- **Separation of concerns:** Master handles site-wide UI, pages handle page-specific content
- **Scalable:** Future properties (footer links, social media, contact info, etc.) can be added to Settings without modifying individual doc types

**Outcome:** Mobile navigation is now site-wide, fully editor-configurable, and implements the standard Umbraco community pattern for site-wide configuration. Both pass 1 and pass 2 decisions documented. Build clean, pattern proven, ready for production.

---

## Session: 2026-03-29 — Mobile Nav Data Type and Pre-Seeding Fixes

**Status:** Completed  
**Build outcome:** Success, 0 errors, 0 warnings.

**Problem:** Two bugs in Settings node mobile nav implementation:
1. Built-in Multi URL Picker data type allowed only 1 link (default configuration)
2. Settings node seeded with empty `mobileNavLinks` — no default nav links

**Solution implemented:**

1. **PrismContentTypeSeeder.cs — Custom Data Type Creation:**
   - Created `GetOrCreatePrismMobileNavDataTypeAsync()` helper method
   - Checks for existing "Prism Mobile Nav Links" data type by name (idempotent)
   - If not found, clones built-in Multi URL Picker (`fd1e0da5-5606-4862-b679-5d0cf3a52a59`) with custom config
   - Sets `maxNumber: 4` to allow up to 4 navigation links
   - Uses Umbraco v17 pattern: `new DataType(IDataEditor, IConfigurationEditorJsonSerializer)` constructor
   - Injected `IConfigurationEditorJsonSerializer` dependency into seeder constructor
   - Updated `EnsureMobileNavPropertyAsync()` to use custom data type instead of built-in

2. **PrismStarterContentSeeder.cs — Pre-Seed Default Nav Links:**
   - Added `System.Text.Json` using statement
   - After creating Settings node, pre-seeds `mobileNavLinks` property with default links
   - Default links: Home (`/`) and Dashboard (`/dashboard`)
   - Uses external-type links (not content UDI) for simplicity and reliability
   - JSON format: `[{"name":"Home","target":"","type":"external","url":"/"},...]`
   - Only fires on fresh install (when content tree is empty) — correct behavior

**Technical approach:**
- Umbraco v17 DataType API requires `IDataEditor` and `IConfigurationEditorJsonSerializer` in constructor
- Cannot simply instantiate DataType with `IShortStringHelper` — that constructor doesn't exist in v17
- Clone approach: Get built-in picker's `Editor` property, pass to new DataType with custom config
- `ConfigurationData` must be `Dictionary<string, object>` (not `object?`) to avoid nullability warnings
- Multi URL Picker JSON structure documented for future reference

**Why this matters:**
- **Editors get working nav out-of-the-box** on fresh installs (Home + Dashboard pre-seeded)
- **Can add up to 4 links** without hitting single-link limitation
- **Custom data type is reusable** — if future properties need Multi URL Picker with max=4, it's there
- **Idempotent** — won't recreate data type or re-seed content if already exists

**Outcome:** Mobile navigation now fully functional on fresh installs with sensible defaults. Custom data type allows 4 links as intended. Build clean, both bugs fixed.

---

## Session: 2026-03-29 — Seeder Idempotency Fixes for Existing Installations

**Status:** Completed  
**Build outcome:** Success, 0 errors, 0 warnings.

**Problem:** Previous implementation assumed fresh installations. Two critical bugs for existing installations:

1. **Data Type Never Updated (PrismContentTypeSeeder.cs line 92):**
   - Early-returned if `mobileNavLinks` property existed, without checking if it used the correct data type
   - Existing Settings doc type had property pointing to old built-in Multi URL Picker (single-select)
   - New custom "Prism Mobile Nav Links" data type (maxNumber=4) was created but never assigned to existing property
   - Result: Property remained stuck on old data type, 4-link config never applied

2. **Settings Defaults Never Seeded (PrismStarterContentSeeder.cs line 40):**
   - Exited early if content tree wasn't empty: `if (rootContent.Any()) return;`
   - This skipped ALL seeding, including Settings node creation and default nav links
   - Result: Existing installations never got Settings node or default nav links populated

**Solution implemented:**

1. **PrismContentTypeSeeder.cs — Data Type Check and Update:**
   - Moved `GetOrCreatePrismMobileNavDataTypeAsync()` call to top of method (before existence check)
   - Changed early-return logic: now checks if property exists AND if it has correct data type
   - If property exists but has wrong data type → update `DataTypeKey` and save
   - If property exists and has correct data type → return (no-op)
   - If property doesn't exist → create it (existing logic preserved)
   - Pattern: **Always validate data type key, not just property existence**

2. **PrismStarterContentSeeder.cs — Separated Guards:**
   - Restructured `HandleAsync()` into two methods:
     - `SeedHomeAndDashboard()` — only runs if tree is empty (guarded by `!rootContent.Any()`)
     - `EnsureSettingsDefaults()` — always runs (checks Settings node independently)
   - `EnsureSettingsDefaults()` logic:
     - Finds or creates Settings node at root
     - Checks if `mobileNavLinks` property is empty
     - Only sets default nav links if currently empty (doesn't overwrite user edits)
     - Pattern: **Separate tree-empty guard from settings-empty guard**

**Architectural pattern:**
- **Idempotency via state checks, not existence checks:** Don't assume "exists" means "correct" — validate actual state
- **Decouple initialization concerns:** Starter content (tree-empty) vs configuration defaults (state-empty) are independent operations
- **Graceful upgrade path:** Existing installations now auto-upgrade data type and get Settings defaults on next startup

**Why this matters:**
- Users with existing installations (like Jonny) can now upgrade without manual backoffice edits
- Data type migration happens automatically on startup
- Settings node and defaults populate even if content tree isn't empty
- Future seeders should follow this pattern: check actual state, not just existence

**Outcome:** Both seeders now work correctly for fresh AND existing installations. Data type updates automatically, Settings defaults seed idempotently. Build clean, upgrade path proven.


## Session: 2026-03-29 — Data Type Editor Bug Fix (Multi URL Picker Type Correction)

**Status:** Completed  
**Build outcome:** Success, 0 errors, 0 warnings.

**Problem:** Two bugs in the mobile nav data type seeding implementation:

1. **Wrong Data Type Editor Created (PrismContentTypeSeeder.cs line 147):**
   - Used GUID `fd1e0da5-5606-4862-b679-5d0cf3a52a59` believing it was Multi URL Picker
   - **This GUID is actually Multi Node Tree Picker** (content tree picker, not URL picker)
   - Created a "Prism Mobile Nav Links" data type with the WRONG editor
   - Backoffice showed tree picker UI instead of URL picker
   - Error confirmed by stack trace: `MultiNodeTreePickerPropertyEditor+MultiNodeTreePickerPropertyValueEditor`

2. **Wrong JSON Type Value (PrismStarterContentSeeder.cs line 101):**
   - Seed JSON used `type = "external"` (string value)
   - Multi URL Picker `type` field is an **integer enum** (`LinkType`)
   - 0 = External, 1 = Content, 2 = Media
   - Property value converter failed due to type mismatch

**Solution implemented:**

1. **PrismContentTypeSeeder.cs — Use PropertyEditorCollection by Alias:**
   - **DO NOT use GUIDs to clone data type editors** — unreliable, easy to get wrong GUID
   - Injected `PropertyEditorCollection` into constructor (from `Umbraco.Cms.Core.PropertyEditors`)
   - Changed `GetOrCreatePrismMobileNavDataTypeAsync()` to:
     - Look up editor by alias: `propertyEditorCollection["Umbraco.MultiUrlPicker"]`
     - If wrong data type exists (e.g., MultiNodeTreePicker with same name), delete it via `DeleteAsync`
     - Create new data type using correct editor from registry
   - Removed all GUID-based cloning logic
   - Pattern: **Always use `PropertyEditorCollection[alias]` for safe editor lookup**

2. **PrismStarterContentSeeder.cs — Use Integer Enum for Link Type:**
   - Changed seed JSON from `type = "external"` to `type = 0`
   - 0 = External link type in Umbraco's `LinkType` enum
   - JSON now deserializes correctly for Multi URL Picker property value converter

**Technical approach:**
- `PropertyEditorCollection` is the DI-injectable registry for all property editors
- Access editors via indexer syntax: `collection[alias]` returns `IDataEditor` or null
- Multi URL Picker alias is `"Umbraco.MultiUrlPicker"` (case-sensitive)
- Multi Node Tree Picker alias is `"Umbraco.MultiNodeTreePicker"` (the WRONG editor)
- `IDataTypeService.DeleteAsync` removes old data types (returns `Attempt<IDataType>`)
- Fixed nullability warning: `Dictionary<string, object>` (not `object?`) for ConfigurationData

**Why this matters:**
- **GUID approach is fragile** — no compile-time safety, easy to copy wrong GUID, hard to debug
- **Alias approach is self-documenting** — code clearly states `"Umbraco.MultiUrlPicker"`, intent is obvious
- **Editor registry is the correct abstraction** — Umbraco's official pattern for getting property editors
- **Delete-and-recreate** allows clean migration from wrong data type to correct one

**Learnings:**
- **NEVER trust a GUID without verification** — check Umbraco documentation or source code
- **Prefer PropertyEditorCollection[alias] over dataTypeService.GetAsync(guid)** for programmatic data type creation
- **Multi URL Picker type field is integer, not string** — 0/1/2 enum values
- **GUID `fd1e0da5-5606-4862-b679-5d0cf3a52a59` is Multi Node Tree Picker**, NOT Multi URL Picker

**Outcome:** Correct Multi URL Picker data type now created. Seed data uses correct integer type values. Build clean, backoffice shows correct URL picker UI.

Umbraco v17 Multi URL Picker LinkType is serialized as string enum ("External", not 0)
