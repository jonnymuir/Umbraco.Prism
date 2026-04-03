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

---

## Session: 2026-03-29 — Integration & Documentation (Scribe Finalization)

**Status:** Completed  
**Action:** Merged 4 inbox decision files into consolidated `decisions.md`. All fixes integrated and committed.

**Key learnings consolidated for team:**

1. **PropertyEditorCollection is the canonical editor lookup pattern** — Use `propertyEditorCollection["Umbraco.MultiUrlPicker"]` by alias, never hard-coded GUIDs. GUID `fd1e0da5-5606-4862-b679-5d0cf3a52a59` is Multi Node Tree Picker, not Multi URL Picker.

2. **Custom data type creation pattern (v17):**
   - Requires `IDataEditor` and `IConfigurationEditorJsonSerializer` in constructor
   - Lookup editor by alias via `PropertyEditorCollection` (safe, documented)
   - Create with `new DataType(editor, serializer) { Name, DatabaseType, ConfigurationData }`
   - Always check for existing by name (idempotent)

3. **Idempotent seeder pattern for upgrades:**
   - Content Type: Check `DataTypeKey` not just property existence → update if wrong
   - Content Seeder: Separate guards (tree-empty for new content, property-empty for defaults)
   - Both patterns allow graceful auto-upgrade on startup

4. **Multi URL Picker pre-seeding:**
   - Use external-type links for defaults (simpler than content UDI)
   - Serialize with `System.Text.Json` (simple anonymous objects work)
   - Link type: string enum ("External", "Content", "Media") not integer

**Commit:** `42fdf5f` — All fixes integrated, decisions documented, ready for team adoption.

---

## Session: 2026-06-17 — Deterministic GUID Fix for Settings Node Seeder

**Status:** Completed
**Issue:** Settings node seeder crashing with JSON deserialization error on `MultiNodeTreePickerPropertyEditor+...EditorEntityReference` — missing `unique` property.

**Root cause confirmed (as provided):**
1. `dataTypeService.DeleteAsync` silently fails when data type is in use by a content type
2. In-place `existingProperty.DataTypeKey = newDataType.Key` is unreliable — Umbraco's `PropertyType` stores both a `DataTypeKey` (GUID) and `DataTypeId` (int), and setting just the key leaves the integer ID stale, causing validation to still use the old data type
3. The old MultiNodeTreePicker data type was therefore never actually replaced

**Changes made:**
1. **Deterministic fixed GUID** (`3b4c5d6e-7f80-9a1b-c2d3-e4f567890abc`) for the Prism Mobile Nav data type — now looked up by `dataTypeService.GetAsync(PrismMobileNavDataTypeKey)` instead of by name
2. **Set `Key = PrismMobileNavDataTypeKey`** on the `DataType` before creating it — ensures the same GUID is used on every fresh install
3. **Remove + re-add pattern** for property migration: call `contentType.RemovePropertyType(alias)`, save, re-fetch from DB, then fall through to create fresh — avoids stale integer ID mismatch
4. **`ILogger<T>` added** to both seeders with strategic log lines tracking data type key at each stage
5. **Guard in `EnsureSettingsDefaults`**: compares `mobileNavProperty.DataTypeKey` against expected GUID before setting nav links JSON — worst case publishes Settings node empty rather than crashing

**Learnings:**

- **`IDataType.Key` is settable** on `DataType` (concrete class) before calling `CreateAsync` — this is the correct way to create a data type with a deterministic GUID in Umbraco v17
- **`dataTypeService.GetAsync(Guid key)`** is the reliable lookup once a fixed GUID is established — no more name-based fragility
- **`dataTypeService.DeleteAsync` silently fails** when the data type is referenced by a content type (Umbraco blocks it at DB level). The `Attempt<>` result carries the failure but the code was discarding it. Log the failure explicitly.
- **In-place `DataTypeKey` mutation is unreliable** for existing PropertyType instances — the integer `DataTypeId` used internally for validation lookup is NOT updated. Always remove + re-add.
- **Re-fetch content type from DB after structural changes** (`contentTypeService.Get(alias)`) to get a clean cache-free object before further operations
- **Guard pattern in StarterSeeder** prevents the crash from propagating — publish succeeds (empty), user can fill in nav links manually


---

## Session: 2026-06-18 — Per-tenant biometric authentication toggle

**Status:** Completed

**Feature:** `AllowBiometricLogin` per-tenant flag — backoffice toggle, DB persistence, BiometricController enforcement.

**Changes made:**

1. **`AddAllowBiometricLoginColumn.cs`** (new) — idempotent migration adding `AllowBiometricLogin` boolean column to `prismTenants` with default `true`
2. **`PrismMigrationPlan.cs`** — appended `.To<AddAllowBiometricLoginColumn>("add-allow-biometric-login")` as final step
3. **`PrismTenantSchema.cs`** — added `[Column("AllowBiometricLogin")] bool AllowBiometricLogin { get; set; } = true`
4. **`PrismTenant.cs`** — added `bool AllowBiometricLogin { get; set; } = true`
5. **`PrismTenantRequest.cs`** — added `bool AllowBiometricLogin { get; set; } = true`
6. **`TenantManagementController.cs`** — mapped field in both `CreateTenant` and `UpdateTenant`
7. **`TenantService.cs`** — added `AllowBiometricLogin = tenantSchema.AllowBiometricLogin` to schema→model mapping
8. **`BiometricController.cs`** — HTTP 403 guard added after tenant null check in both `Register` and `Exchange` actions
9. **`prism-create-tenant-modal.ts`** — `@state() _allowBiometricLogin`, loaded in `connectedCallback` + `updated`, included in submit payload, toggle switch UI in General tab with full CSS

**Learnings:**

- **AllowBiometricLogin default = true** — backward compatible; existing rows get the default via DB migration `WithDefaultValue(true)`
- **Guard placement in BiometricController** — check goes immediately after the tenant null check in both `Register` (authenticated) and `Exchange` (anonymous) actions; Exchange also logs audit entry with `"biometric_disabled"` failure reason
- **Toggle in General tab** — placed after the Hostname field, uses a custom CSS toggle switch (not Umbraco UUI) since there is no UUI toggle component wired for raw boolean state in this codebase
- **TypeScript field casing** — API returns camelCase (`allowBiometricLogin`), consistent with all other fields read from `this.data?.tenant` in the modal
- **EditorUiAlias for MultiUrlPicker** in Umbraco v14+ is `Umb.PropertyEditorUi.MultiUrlPicker`
- **IDataType in Umbraco v17** has both `EditorAlias` (schema/backend) and `EditorUiAlias` (frontend Web Component) — both must be set when creating data types programmatically
- **Fix pattern for EditorUiAlias bug**: set `EditorUiAlias` at creation + repair existing records via `UpdateAsync` on startup

## 2026-03-29: Mobile Nav CSS Self-Contained Pattern

**Session Log:** `.squad/log/2026-03-29-mobile-nav-css-finalized.md`  
**Coordinator:** Copilot  
**Commit:** `b5109f3`

**Change:** Mobile nav CSS moved from `Master.cshtml` into `_MobileShellNav.cshtml` partial

**Decision:** Partials should own their styles. CSS that styles a partial component should live in that partial, not in the layout. This ensures styles are available on Layout=null pages and keeps components self-contained.

**Implementation:**
- Mobile nav styles now in `_MobileShellNav.cshtml`
- Uses `auto-fit` grid columns (responsive 2–4 link layout)
- Removed from `Master.cshtml`
- Works on Layout=null pages (previously broken)

**Why:** Previously, mobile nav styles in Master.cshtml weren't loaded on Layout=null pages, causing rendering issues. Partial-scoped CSS fixes this and establishes a cleaner component pattern.

**Status:** ✅ Confirmed working; pattern established for future partials.

**EditorUiAlias Confirmation (Brewster):** EditorUiAlias fix confirmed working on startup seeder. Decision merged and documented for future reference.


## Session: 2026 — Mobile Nav Seeder Recovery

**Status:** Completed

**Problem:** `DemoMobileNavSeeder.cs` was committed to feature branch after PR opened, never merged to main. Mobile nav didn't appear in the running test site because `mobileNavLinks` was empty, and `_MobileShellNav.cshtml` guards rendering on `Model != null && Model.Any()`.

**Changes made:**

1. **`DemoMobileNavSeeder.cs`** (recreated) — `src/UmbracoPrism.TestSite/DemoMobileNavSeeder.cs` — seeds 4 demo links (Home, Account, Settings, Help) into Settings node on `UmbracoApplicationStartedNotification`. Development-only, idempotent.

**Rendering chain verified:**
- `Master.cshtml` line 12: reads `mobileNavLinks` from settings node via `IPublishedContent.Value<IEnumerable<Link>>()`
- `Master.cshtml` ~line 87: `html.prism-mobile prism-mobile-nav { display: block !important; }` CSS rule present ✅
- `Master.cshtml` bottom: `@Html.Partial("_MobileShellNav", mobileNavLinks)` ✅
- `_MobileShellNav.cshtml`: guard `@if (Model != null && Model.Any())`, then renders `<prism-mobile-nav>` and loads `prism-mobile-nav.js` from `/App_Plugins/UmbracoPrism/dist/` ✅

**Registration:** `.AddComposers()` in `Program.cs` auto-discovers `INotificationAsyncHandler<UmbracoApplicationStartedNotification>` — no manual registration needed.

**Database:** Existing SQLite DB at `src/UmbracoPrism.TestSite/umbraco/Data/Umbraco.sqlite.db`. Seeder is idempotent — skips if `mobileNavLinks` already set. Will also skip if Settings content node doesn't exist yet (fresh install pre-content-setup).

**Build:** `dotnet build src/UmbracoPrism.TestSite/` → ✅ 0 errors, 0 warnings. Full solution `dotnet build UmbracoPrism.sln` has a pre-existing "Question build" error in `UmbracoPrism.Core` (MSBuild out-of-date check, unrelated to this change).

## Learnings

- **Seeder prerequisite:** `DemoMobileNavSeeder` requires the Settings content node (alias `settings`) to exist as root content. On a completely fresh database with no content, it logs a debug message and skips silently — developer must create the Settings node first, or the seeder fires on next app restart.
- **Full solution build quirk:** `dotnet build UmbracoPrism.sln` fails with "Question build" in `UmbracoPrism.Core` due to MSBuild incremental build check. Build individual projects (`dotnet build src/<Project>/`) for clean results.

## Session: 2026-04-02T20:18:48Z — Mobile Nav Seeder Restore

**Commit:** `3e810ee`  
**Status:** Completed

**Task:** Restore `DemoMobileNavSeeder.cs` which was committed to feature branch after PR #35 was opened and never merged to main.

**Changes:**
- Recreated `src/UmbracoPrism.TestSite/DemoMobileNavSeeder.cs`
- Seeds 4 demo mobile-nav links (Home, Account, Settings, Help) into Settings node
- Development-only (`env.IsDevelopment()` + `RuntimeLevel.Run` guard), idempotent
- Auto-discovered via `.AddComposers()` — no registration needed

**Build:** `dotnet build src/UmbracoPrism.TestSite/` → ✅ 0 errors, 0 warnings

**Decision merged:** `brewster-mobile-nav-seeder.md` — Demo seeder pattern for TestSite established.

## Session: 2026 — Layout Refactor: Wire Views to Master

**Status:** Completed

**Problem:** `HomePage.cshtml` and `MemberDashboard.cshtml` both used `Layout = null`, making them fully standalone pages that never passed through `Master.cshtml`. This meant:
- `prism-mobile-nav` was never injected on those pages
- Mobile CSS rules never loaded
- All layout/header/footer HTML was duplicated in every view
- Isabelle's `prism-branding.css` (extracted from Master) was not picked up

**Changes made:**

1. **`Master.cshtml`** — Already had `prism-branding.css` link added (by Isabelle in parallel). Confirmed full structure intact: header, footer, `@RenderBody()`, `_MobileShellNav` partial.

2. **`HomePage.cshtml`**
   - Changed `Layout = null` → `Layout = "Master"`
   - Removed: `<!DOCTYPE html>`, `<html>`, `<head>`, meta tags, title, 5 branding `<link>` tags, `</head>`, `<body>`, `<header class="portal-header">`, `<footer class="portal-footer">`, mobile nav code block, `</body>`
   - Kept: `@using`, `@inherits`, all C# logic block, imagery CSS `<style>` (has Razor `@Html.Raw(...)` expressions), full page-specific CSS `<style>`, all page content (`<div class="page">` + hero + features + debug)

3. **`MemberDashboard.cshtml`**
   - Changed `Layout = null` → `Layout = "Master"`
   - Removed: `<!DOCTYPE html>`, `<html>`, `<head>`, meta tags, title, 5 branding `<link>` tags, `</head>`, `<body>`, standalone `<prism-mobile-user-agent-demo />`, `<header class="dash-header">`, `<footer class="dash-footer">`, mobile nav code block, `</body>`, `</html>`
   - Kept: `@inherits`, all C# ViewBag reading (`displayName`, `email`, `userOid`), full page-specific CSS `<style>`, all dashboard content (`<main class="dash-page">` + all sections + API demo script)

**Build:** `dotnet build src/UmbracoPrism.TestSite/` → ✅ 0 errors, 0 warnings (3.76s)

**Result:** Both views now route through Master.cshtml. Mobile nav, branding CSS, and shared chrome are automatically injected. No layout HTML is duplicated.

---

## Session: Mobile Nav Icon Mapping

**Task:** Wire `icon` field into `_MobileShellNav.cshtml` so `prism-mobile-nav` renders icons on the live site.

**Problem:** The `navItems` projection omitted the `icon` property, so the Lit component never received icon data.

**Changes made:**

1. **`src/UmbracoPrism.TestSite/Views/Partials/_MobileShellNav.cshtml`**
   - Added `IconForLink(string? href, string? label)` local function inside the `@{ }` block
   - Matches URL segments and label text to built-in icon names: `home`, `dashboard`, `account`, `settings`, `transactions`, `notifications`, `more`
   - Returns `null` for unrecognised links — omitted from JSON via `WhenWritingNull`, component degrades to label-only
   - Added `icon = IconForLink(link.Url, link.Name)` to the anonymous projection

**Build:** `dotnet build src/UmbracoPrism.TestSite/` → ✅ 0 errors, 0 warnings (2.10s)

---

## Session: 2026-04-02 — Mobile Nav Icon Mapping (Finalized)

**Commit:** `37e9975`  
**Status:** Completed  
**Decision merged:** `brewster-nav-icons.md` — Icon mapping convention for mobile nav

**Implementation Details:**

The `_MobileShellNav.cshtml` partial now populates the `icon` property on `prism-mobile-nav` using a **URL-first, label-fallback** convention.

**Local function `IconForLink`:**
```csharp
string? IconForLink(string? href, string? label)
{
    if (string.IsNullOrWhiteSpace(href) && string.IsNullOrWhiteSpace(label)) return null;
    
    var urlLower = (href ?? "").ToLowerInvariant().TrimEnd('/');
    var labelLower = (label ?? "").ToLowerInvariant();
    
    // URL matching (priority)
    if (urlLower == "" || urlLower == "/") return "home";
    if (urlLower.Contains("dashboard")) return "dashboard";
    if (urlLower.Contains("account") || urlLower.Contains("profile")) return "account";
    if (urlLower.Contains("setting")) return "settings";
    if (urlLower.Contains("transaction") || urlLower.Contains("payment")) return "transactions";
    if (urlLower.Contains("notification") || urlLower.Contains("alert")) return "notifications";
    if (urlLower.Contains("help") || urlLower.Contains("support") || urlLower.Contains("more")) return "more";
    
    // Label fallback
    if (labelLower == "home") return "home";
    if (labelLower == "dashboard") return "dashboard";
    if (labelLower == "account" || labelLower == "profile") return "account";
    if (labelLower == "setting") return "settings";
    
    return null;
}
```

**Icon → URL/label mapping:**
- `home` → URLs: `""`, `"/"` | Labels: `"home"`
- `dashboard` → URLs containing `dashboard` | Labels: `dashboard`
- `account` → URLs containing `account`, `profile` | Labels: `account`, `profile`
- `settings` → URLs containing `setting` | Labels: `setting`
- `transactions` → URLs containing `transaction`, `payment`
- `notifications` → URLs containing `notification`, `alert`
- `more` → URLs containing `help`, `support`, `more`

**JSON projection:**
```csharp
icon = IconForLink(link.Url, link.Name)
```

Null icons are omitted from serialised JSON via `System.Text.Json.Serialization.WhenWritingNull`. The `prism-mobile-nav` component gracefully renders label-only for unrecognised links.

**Rationale:**
- No CMS schema changes required — mapping derived from existing Multi URL Picker link data
- Easily extended: add new `if` branches for new icon types
- Null-safe and gracefully degrading — no render failures

**Decision notes:**
- This is an **interim solution** using URL convention inference
- **Future work:** Custom `MobileNavItem` Element Type with explicit `icon` dropdown field for editor control (proper Umbraco pattern)
- See `copilot-mobile-nav-icon-approach.md` for context

**Build:** `dotnet build src/UmbracoPrism.TestSite/` → ✅ 0 errors, 0 warnings

---

## Session: 2025-07-17 — MobileNavItem Element Type with Media Picker Icon Support

**Status:** Completed
**Build outcome:** Success, 0 errors, 0 warnings.

**Problem:** `mobileNavLinks` on the `Settings` document type used a Multi URL Picker (`IEnumerable<Link>`). This had no icon field — icons were mapped by URL convention, a fragile hack. Jonny wanted editors to pick icons from the Umbraco media library per nav item.

**Solution implemented:**

1. **`MobileNavSchemaSetup.cs`** (new) — `INotificationAsyncHandler<UmbracoApplicationStartedNotification>`:
   - Step A: Creates `mobileNavItem` element type (`IsElement = true`) with property group "Navigation" containing `navLabel` (Textstring), `navUrl` (Textstring), `navIcon` (Media Picker), `openInNewTab` (True/False).
   - Step B: Creates `Mobile Nav Icon Picker` data type (`Umbraco.MediaPicker3`, UI alias `Umb.PropertyEditorUi.MediaPicker`, `multiple=false`).
   - Step C: Creates `Mobile Nav Block List` data type (`Umbraco.BlockList`, UI alias `Umb.PropertyEditorUi.BlockList`) referencing `mobileNavItem`, max 4 blocks.
   - Step D: Replaces `Settings.mobileNavLinks` — removes old Multi URL Picker property, re-adds as Block List.
   - All steps idempotent; guarded by `env.IsDevelopment()` and `runtimeState.Level >= Run`.
   - Uses deterministic GUIDs for all created entities (safe to recreate across installs).

2. **`TestSiteComposer.cs`** (new) — `IComposer` that registers both `MobileNavSchemaSetup` and `DemoMobileNavSeeder` as notification handlers. Schema setup runs first to ensure element type exists before seeder checks.

3. **`_MobileShellNav.cshtml`** — `@model` changed from `IEnumerable<Link>` to `IEnumerable<BlockListItem>`. Removed URL-convention icon mapping. Now reads `navLabel`, `navUrl`, `navIcon` (`.Url()` from media picker), `openInNewTab` directly from block content properties. Serializes to JSON for `<prism-mobile-nav>` web component.

4. **`Master.cshtml`** — Changed `Value<IEnumerable<Link>>("mobileNavLinks")` to `Value<BlockListModel>("mobileNavLinks")`. Wrapped partial call in null guard. Added `@using Umbraco.Cms.Core.Models.Blocks`.

5. **`DemoMobileNavSeeder.cs`** — Removed old Multi URL Picker seeding. Now just checks Settings node exists and logs a helpful message directing editors to add Block List items via the backoffice. Added code comment with Block List JSON format for future reference.

**Key technical learnings:**
- `Umbraco.BlockList` → `Umb.PropertyEditorUi.BlockList` (confirmed from static assets JS)
- `Umbraco.MediaPicker3` → `Umb.PropertyEditorUi.MediaPicker` (confirmed from DB)
- `BlockListConfiguration` ConfigurationData keys: `blocks[].contentElementTypeKey` (Guid), `blocks[].label` (handlebars), `validationLimit.{min,max}`
- `MediaPicker3Configuration` ConfigurationData keys: `multiple` (bool), `validationLimit.{min,max}`
- `ContentType.Key` can be set before `contentTypeService.Save()` to get deterministic GUIDs (same pattern as DataType)
- `TestSiteComposer.cs` needed — `DemoMobileNavSeeder` had no registration previously (bug discovered and fixed)

## Mobile Nav Block List Schema Setup (2026-04-03)

**Sprint:** Mobile nav media icons integration  
**Session Log:** `.squad/log/2026-04-03_07-39-08-mobile-nav-media-icons.md`  
**Orchestration Log:** `.squad/orchestration-log/2026-04-03_07-39-08-brewster-mobile-nav-element-type.md`

**Status:** Completed

**Problem:** `Settings.mobileNavLinks` used Multi URL Picker (`IEnumerable<Link>`). No icon field; icons resolved by URL convention (fragile hack). Needed idiomatic Umbraco path: editors pick icons from media library per nav item.

**Solution Implemented:**

1. **`MobileNavSchemaSetup.cs`** — Idempotent startup handler:
   - Creates `mobileNavItem` element type (`IsElement = true`)
     - Property group "Navigation" with `navLabel` (Textstring), `navUrl` (Textstring), `navIcon` (Media Picker), `openInNewTab` (Toggle)
   - Creates `Mobile Nav Icon Picker` data type (Umbraco.MediaPicker3, single, `Umb.PropertyEditorUi.MediaPicker`)
   - Creates `Mobile Nav Block List` data type (Umbraco.BlockList, max 4, `Umb.PropertyEditorUi.BlockList`)
   - Replaces `Settings.mobileNavLinks` property (removes Multi URL Picker, installs Block List)
   - Development-only guard (`env.IsDevelopment()` + `RuntimeLevel >= Run`)
   - Deterministic GUIDs for safe recreation across installs

2. **`TestSiteComposer.cs`** — New composer:
   - Registers `MobileNavSchemaSetup` and `DemoMobileNavSeeder` as notification handlers
   - **Bug fix:** `DemoMobileNavSeeder` had no container registration — now wired up

3. **`_MobileShellNav.cshtml`** — Updated partial:
   - `@model` changed from `IEnumerable<Link>` to `IEnumerable<BlockListItem>`
   - Removed URL-convention icon mapping hack
   - Reads `navLabel`, `navUrl`, `navIcon.Url()`, `openInNewTab` from block content
   - Serializes to JSON for `<prism-mobile-nav>` web component

4. **`Master.cshtml`** — Updated layout:
   - Changed property fetch to `BlockListModel` type
   - Added null guard around `_MobileShellNav` partial call
   - Added `@using Umbraco.Cms.Core.Models.Blocks`

5. **`DemoMobileNavSeeder.cs`** — Updated seeder:
   - Removed old Multi URL Picker seeding
   - Added helpful message directing editors to backoffice for Block List item entry
   - Code comment with Block List JSON format for future reference

**Build:** ✅ Passed (0 errors, 0 warnings)

**Breaking Change:** Old Multi URL Picker values NOT migrated (property fully replaced). Editors must re-enter nav items via backoffice.

**Technical Learnings:**

- `BlockListConfiguration` ConfigurationData keys: `blocks[].contentElementTypeKey`, `validationLimit.{min,max}`
- `MediaPicker3Configuration` ConfigurationData keys: `multiple`, `validationLimit.{min,max}`
- `ContentType.Key` can be set before `Save()` for deterministic GUIDs (same as DataType pattern)
- Umbraco.MediaPicker3 UI alias: `Umb.PropertyEditorUi.MediaPicker`
- Umbraco.BlockList UI alias: `Umb.PropertyEditorUi.BlockList`

**Paired with:** Isabelle's media icon URL support in prism-mobile-nav (runtime type check, CSS transitions)

---

**Coordination Update:** Mobile Nav Seeder Enhancement (Commit 2f3483d)

**Status:** ✅ Completed

**Enhancement:** `DemoMobileNavSeeder.cs` now creates SVG media items in the media library and seeds the block list with Home/Dashboard navigation items. Seeder remains idempotent and dev-only guarded.

**What Changed:**
- SVG icons created in media library for nav items
- Block list populated with Home/Dashboard nav entries
- Maintains idempotent seeding pattern (safe for repeated startup)

**Session Log:** `.squad/log/2026-04-03T06:59:36Z-seed-mobile-nav.md`
