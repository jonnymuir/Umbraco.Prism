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
- **Playwright test navigation patterns:** Browser tests should exercise authored Umbraco navigation (CTAs, links) rather than direct route access. This ensures tests follow the same user-visible journey and prevents false negatives where the test state doesn't match its assertions.
- **Dashboard route contract is stable when accessed via authored CTA.** Assert the signed-in home page's "Go to Dashboard" CTA href resolves to `/dashboard`, then click it. This avoids test false negatives and aligns the test with the published route structure editors maintain.
- **`@inject IPrismContext PrismContext`** is wired in `_ViewImports.cshtml` — available in all TestSite views without explicit declaration in each view.
- **IDataTypeService in Umbraco v17:** Use the built-in Multi URL Picker data type via its well-known GUID (`fd1e0da5-5606-4862-b679-5d0cf3a52a59`) rather than creating custom data types programmatically. Creating data types via code requires complex property editor instantiation - not recommended for seeders.
- **Multi URL Picker value converter:** Returns `IEnumerable<Umbraco.Cms.Core.Models.Link>` where `Link` has `Name`, `Url`, `Target`, `Type` properties. Access via `Model.Value<IEnumerable<Link>>("propertyAlias")`.
- **Partial views for nav components:** Extract repeatable navigation patterns into `Views/Partials/` for reusability. Accept strongly-typed models (`@model IEnumerable<Link>`) and handle null/empty gracefully by rendering nothing.
- **Settings node pattern (Paul Seal):** For site-wide configuration (navigation, footer links, social media, etc.), create a root-level `settings` document type with `AllowedAsRoot = true` and no template. Master layout reads it via `Umbraco.ContentAtRoot().FirstOrDefault(x => x.ContentType.Alias == "settings")`. Editors configure once, all pages inherit. Standard Umbraco community pattern — avoids per-page property duplication.
- **MockBackOffice extension pattern:** MockBackOffice is designed to be extensible for demo scenarios. New API surfaces follow the pattern: controller under `Controllers/`, service interfaces + implementations, DI registration in `Program.cs`, and configuration shape in `appsettings.json` under `PrismMockBackOffice:{Feature}`. RuntimeMode toggles allow switching between in-memory emulation and Core runtime proxying.
- **Workflow emulator governance:** Emulator-only extensions (operator personas, auto-assignment, fast-forward) MUST be namespaced under `UmbracoPrism.MockBackOffice.Workflow.*` and never leak into Core runtime contracts. Security guards always execute in Core runtime, even when initiated from emulator UI. Shared contracts live in `UmbracoPrism.Core.Workflow.Contracts`.
- **TestSite demo pages:** For complex interactive demos (e.g., workflow forms engine), create a dedicated document type with route-hijacking controller + Razor view. Properties drive configuration (e.g., workflow key, completion redirect). Member authentication via `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` on controller. Seed demo content via startup notification handler pattern (same as VinylVaultSeeder, DemoMobileNavSeeder).
- **Aspire launch profile matching matters for UmbracoPrism.TestSite.** The AppHost launches projects by matching the AppHost profile name first (`https` here). Because TestSite only exposed `IIS Express` and `Umbraco.Web.UI`, Aspire missed its `applicationUrl` endpoints until `AddProject(..., launchProfileName: "Umbraco.Web.UI")` was specified explicitly.
- **Aspire dashboard browser timing in VS Code:** For this repo's AppHost, the outer host logs `Now listening on:` before the dashboard process is fully ready. To avoid blank/half-ready dashboard loads, keep AppHost `launchBrowser` off and let VS Code open `https://localhost:17214` from a `coreclr` `serverReadyAction` keyed to `Distributed application started.`
- **Clean TestSite auth-flow contract:** For deterministic localhost auth/workflow runs, seed and preserve five Umbraco nodes — `Home` (`/`), `Dashboard` (`/dashboard`), `Get in Touch` (`workflowKey = community-enquiry`, `/get-in-touch/`), `My Workflows` (`/my-workflows/`), and `Settings` with mobile nav for Home/Dashboard/My Workflows. Razor views should resolve those links from the published tree, not from root-node order or hardcoded assumptions.
- **Live Aspire readiness should be machine-readable.** For this repo's localhost auth suite, brittle hero-copy probes caused false negatives on a healthy clean boot. The reliable pattern is `data-prism-home-ready="true"` on the rendered home page plus `/api/prism/downstream-demo/seed-contract-ready`, which verifies the published Home/Dashboard/Get in Touch/My Workflows/Settings contract and the expected `/auth/login?ReturnUrl=%2Fmy-workflows` challenge path.
- **Normalize Umbraco content URLs before treating them as a contract.** Published Umbraco URLs can include trailing slashes even when the intended route contract is `/dashboard`, `/get-in-touch`, or `/my-workflows`. `TestSiteSeedContract.NormalizeUrl()` should be the shared normalizer for readiness payloads and Razor link resolution so the browser journey stays stable.
- **Full AppHost restarts still invalidate pre-restart localhost Keycloak access tokens.** The TestSite can keep its Prism cookie session alive and now retries downstream calls with a forced refresh-token exchange, but the live restart API contract still needs deeper Keycloak/AppHost session persistence work outside the Umbraco route/readiness fix.
- **Dashboard route contract is direct, but browser tests should enter via the authored CTA.** The seeded TestSite contract still requires `memberDashboard` to publish at `/dashboard`, and unauthenticated requests correctly challenge through `/auth/login?ReturnUrl=%2Fdashboard`. For live Playwright flows, the most stable path is to assert the signed-in home page's `Go to Dashboard` link resolves to `/dashboard`, then click it so the test follows the same Umbraco-authored navigation editors see.
- **`/dashboard` itself does not bounce to `/`.** In the current localhost stack, an anonymous `GET /dashboard` challenges to `/auth/login?ReturnUrl=%2Fdashboard`, and a direct dashboard login returns from `/signin-oidc` back to `/dashboard`. The 302 to `/` appears when login starts from the home-page `Sign In` CTA, because `AccountController.Login()` defaults `returnUrl` to `/` when no `ReturnUrl` query string was supplied.
- **Cold-start can change authored link targets before route convergence finishes.** In this TestSite, Razor builds CTAs/nav from `Umbraco.ContentAtRoot()` plus `content.Url()` during the request, while `WorkflowPageSeeder` only seeds/publishes on `UmbracoApplicationStartedNotification` against a fresh runtime DB. On a clean boot, a seeded page can already be discoverable in the published tree but still report `"/"` until Umbraco's hierarchical route cache finishes converging, so a home-page CTA can momentarily emit `/auth/login?returnUrl=%2F` and bounce the member back to Home even though the eventual page contract is `/dashboard`, `/get-in-touch`, or `/my-workflows`.

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

---

**Design Task:** Umbraco-Specific Notifications Integration & Demo Content

**Status:** ✅ Completed

**Deliverable:** `docs/design/notifications-umbraco-demo.md`

**What I Designed:**

**Part 1: Umbraco Platform Integration**

1. **Content Notification Hooks:**
   - Recommended `ContentPublishedNotification` as primary hook
   - Designed opt-in pattern using Document Type composition (`notifiableContent`)
   - Properties: `notifyOnPublish` (toggle), `notificationTitle`, `notificationBody`, `notificationGroups` (Member Group Picker)
   - Allows editors to control notifications per content item without code changes
   - Consumer hook interface (`IPrismContentNotificationHandler`) for advanced customization

2. **Member Group Integration:**
   - Recommended Member Groups as notification audiences (v1 approach)
   - Groups = Topics pattern: "Event Subscribers", "News Subscribers", etc.
   - Zero schema changes — leverages existing `IMemberGroupService` and `IMemberService.AssociateRole()`
   - Noted that custom subscription table (Option B) could be added later for fine-grained topic control

3. **Backoffice Integration:**
   - Recommended deferring to v2 (correct scoping decision)
   - Provided v2 design sketch: Lit Web Component dashboard in Members section
   - Permission model: reuse existing `PrismConfiguration.AdminGroups` or add `NotificationSenderGroups`
   - Rationale: v1 should focus on developer-triggered notifications (content hooks, API endpoints)

4. **Scheduled Task Pattern:**
   - Recommended `IHostedService` with Umbraco Runtime Level checks
   - Pattern: Create scoped service provider to access `IMemberService`, etc.
   - Example: Daily membership expiry notification task
   - Avoids pre-v13 `IRecurringBackgroundTask` (removed pattern)
   - Simpler than Hangfire/Quartz for daily/hourly tasks

**Part 2: Demo Site Design**

**Demo #1: Content Subscription Notifications**

- Document Types: `notificationsHub` (subscription management page)
- Route-hijacked controller: `NotificationsHubController`
- UI: Checkboxes for "Event Updates", "News Alerts", "Offers"
- Backend: `IMemberService.AssignRole()` / `DissociateRole()` to manage group membership
- Trigger: Editor publishes content with `notifiableContent` composition + `notifyOnPublish` enabled

**Demo #2: Backend-Triggered Notification**

**Top Two Recommendations:**

1. **Option A: "Form Review Notification" (Recommended)**
   - Member submits document access request
   - Admin reviews via API endpoint → sends notification "Request Approved ✓"
   - BONUS: Scheduled task auto-approves after 48 hours
   - Document Types: `requestsHub`, `requestForm`
   - Database: `PrismFormSubmissions` table
   - **Why best:** Shows API-triggered AND scheduled notifications, realistic enterprise scenario, fits member portal

2. **Option B: "Membership Expiry Notification" (Runner-up)**
   - Scheduled task finds members expiring in 7 days
   - Sends notification "Your membership expires soon!"
   - Member Type property: `membershipExpiry` (DateTime)
   - Document Type: `membershipHub`
   - **Why strong:** Simplest demo, pure scheduled notification, zero dependencies

**Recommended to Jonny:** Pick Option A for comprehensive demo, or Option B for simplicity.

**Document Type Schema:**

- `notificationsHub` — subscription management page
- `requestsHub` — view member's form submissions
- `requestForm` — submit document access request
- `membershipHub` — membership status + renewal
- `eventPage` — example notifiable content with composition
- `notifiableContent` (composition) — adds notification properties to any content type

**Member Groups Required:**
- "Event Subscribers"
- "News Subscribers"
- "Offer Subscribers"

**Demo Content Tree:**
```
Home
├── Member Dashboard (existing)
├── Notifications (new)
├── My Requests (new)
├── Membership (new)
└── Settings (existing)
```

**Technical Learnings:**

- Umbraco v14+ uses `INotificationAsyncHandler<T>` for content lifecycle events
- Member Groups are backoffice-editable and work via `IMemberGroupService.GetAllMembersOfGroup()`
- Composition pattern allows adding notification capability to any document type without schema rebuild
- `IHostedService` + `IRuntimeState.Level` check is correct pattern for Umbraco-aware background tasks
- Route hijacking + `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` is correct pattern for member-only pages

**Service Interfaces Designed:**

```csharp
IPrismNotificationService
  - SendToMemberGroupsAsync()
  - SendToMemberAsync()
  - SendToAllMembersAsync()
  - GetNotificationLogAsync()

IPushNotificationProvider
  - SendBatchAsync() → PushNotificationBatchResult

IPrismContentNotificationHandler
  - OnContentPublishedAsync() → PrismNotificationPayload?
```

**Implementation Phasing:**

1. **Phase 1:** Blathers implements Core notification services + APNs/FCM providers
2. **Phase 2:** Brewster creates `notifiableContent` composition + content notification handler
3. **Phase 3:** Brewster builds demo site (document types, controllers, views, seeders)
4. **Phase 4:** Testing + documentation

**Coordination Notes:**

- Blathers will implement `IPrismNotificationService` in Core library
- I will consume this service in TestSite controllers + notification handler
- Isabelle may need to update `prism-mobile-nav` to handle deep link navigation (e.g., `/dashboard/requests/{id}`)
- Celeste will document consumer API once implemented

**Session Log:** This entry (appended to history)

**Decision Document:** `.squad/decisions/inbox/brewster-notifications-umbraco.md`

---

## 2026-04-03: Vinyl Vault Demo Redesign

**Task:** Redesign the notifications demo around a vinyl record shop theme.

**Request from:** Jonny Muir

**Previous Design:** Document access requests + membership expiry notifications

**New Design: "Vinyl Vault"** — A vintage vinyl record shop showcasing three notification use cases:

### Concept

A vinyl record shop built into the test site demonstrating:
1. **Content subscription notifications** — Members subscribe to genres (Jazz, Rock, Electronic, etc.); when editors publish new vinyl, subscribers get notified
2. **API-triggered notifications** — "Back in Stock" waitlist alerts when out-of-stock vinyl becomes available
3. **Scheduled notifications** — "Limited Edition Drop" alerts sent 30 minutes before a limited vinyl release

### Document Types Created (Design)

1. **`vinylRecord`** — Individual vinyl record content node
   - Properties: artist, albumTitle, genre (MNTP), coverArt, releaseYear, description, inStock, limitedEdition, limitedDropTime, price, catalogNumber
   - Compositions: `notifiableContent`, `seoBase`
   - Route: `/vinyl-vault/{genre}/{vinyl-name}`

2. **`genre`** — Genre category node (Jazz, Rock, Electronic, Hip-Hop, Classical)
   - Properties: genreName, description, genreIcon
   - Lists child vinyl records + subscription toggle

3. **`vinylVaultHub`** — Shop landing page
   - Properties: heroTitle, heroSubtitle, featuredVinyls
   - Route: `/vinyl-vault`

4. **`notificationSubscriptions`** — Member subscription management page
   - Route: `/vinyl-vault/notifications`
   - Protected route (requires `PrismMemberCookie` auth)

5. **`notifiableContent`** (Composition) — Adds notification capability
   - Properties: notifyOnPublish, notificationTitle, notificationBody, notificationGroups, notificationImageUrl

### Seeder Design

Pre-seeded demo content:
- **5 genres:** Jazz, Rock, Electronic, Hip-Hop, Classical
- **12 vinyl records:** Real artists/albums (Miles Davis, Pink Floyd, Daft Punk, Kendrick Lamar, etc.)
- **3 demo members:**
  - `demo@vinylvault.local` (subscribed to Jazz + All New Stock)
  - `vip@vinylvault.local` (subscribed to VIP + Electronic + Hip-Hop)
  - `rock@vinylvault.local` (subscribed to Rock)
- **7 member groups:** Genre-specific subscribers + "All New Stock Subscribers" + "VIP Members"

### Demo Script

**5-minute walkthrough:**
1. Subscribe to Jazz genre
2. Publish new vinyl in backoffice → notification appears on device
3. Join waitlist for out-of-stock vinyl → mark back in stock → waitlist notification
4. Limited edition drop scheduled → notification sent 30 minutes in advance

**2-minute quick demo:**
1. Subscribe to genre (20 sec)
2. Publish vinyl (60 sec)
3. Show notification (10 sec)
4. Navigate to content (10 sec)
5. Explain other scenarios verbally (20 sec)

### Why Vinyl Records?

- **Instantly relatable:** Everyone understands new stock arrivals and limited drops
- **Content-driven:** Each vinyl is a rich content node (artist, cover art, genre, year)
- **Natural subscription model:** Genre subscriptions mirror real preferences
- **Visual appeal:** Album cover art in notifications
- **Multiple triggers:** Content publish, API calls, scheduled tasks

### Coordination with Blathers

**Backend requirements:**
- `IPrismNotificationService` with:
  - `SendToMemberAsync(memberId, notification, ct)`
  - `SendToMemberGroupsAsync(groupNames, notification, ct)`
  - `SendToMemberGroupAsync(groupName, notification, ct)`
- `PrismContentPublishedHandler` for `ContentPublishedNotification`
- `VinylVaultApiController` endpoints:
  - `/umbraco/api/vinylvault/toggle-subscription`
  - `/umbraco/api/vinylvault/join-waitlist`
  - `/umbraco/api/vinylvault/notify-back-in-stock/{contentId}`
- `LimitedEditionDropNotifier` background task (`IRecurringBackgroundTask`)
- Database: Either member properties or custom `VinylWaitlist` table

**Handoff plan:**
1. Blathers provides `IPrismNotificationService` interface (stub OK initially)
2. I build document types, templates, and controllers against interface
3. Blathers implements FCM sending logic
4. Integration testing together

### Deliverable

**Updated:** `docs/design/notifications-umbraco-demo.md`
- Part 1 unchanged (Platform Integration Design)
- **Part 2 rewritten:** Complete Vinyl Vault specification
- **Part 3 updated:** Implementation guidance with effort estimates

**Estimated effort:**
- Brewster: 5-7 days (document types, templates, controllers, seeder, background task)
- Blathers: 2-3 days (notification service, API endpoints)
- Total: 7-10 days

### Session Outcome

✅ Complete Vinyl Vault demo design documented  
✅ Document Types schemas defined  
✅ Content tree structure specified  
✅ Member subscription flow designed  
✅ Demo script created (5-min + 2-min versions)  
✅ Seeder plan with realistic content  
✅ Coordination notes for Blathers

**Decision Document:** `.squad/decisions/inbox/brewster-vinyl-demo.md`

---

## Session: 2026-04-03 — Vinyl Vault Demo (Phase 2: Content Types + Seeder)

**Status:** Completed  
**Build outcome:** Success, 0 errors.

**Completed work:**

### Content Types (Document Types)
Created three Umbraco document types in code-first style using `INotificationAsyncHandler<UmbracoApplicationStartedNotification>`:

1. **VinylVaultHome** (`vinylVaultHome`)
   - Root node for Vinyl Vault shop
   - Properties: `heroTitle` (textstring), `heroSubtitle` (textarea)
   - Icon: `icon-store`
   - Allows children: `vinylGenreLanding`

2. **VinylGenreLanding** (`vinylGenreLanding`)
   - Genre category landing page (Jazz, Rock, Electronic, etc.)
   - Properties: `genre` (textstring, mandatory), `description` (textarea)
   - Icon: `icon-folder-music`
   - Allows children: `vinylRecord`

3. **VinylRecord** (`vinylRecord`)
   - Individual vinyl listing
   - **Content tab:** `title`, `artist`, `genre`, `releaseYear`, `description` (rich text), `coverImage` (media picker)
   - **Inventory tab:** `inStock` (bool), `stockCount` (int), `isLimitedEdition` (bool)
   - **Notifications tab:** `notificationGenre` (textstring) — **critical property** used by `PrismContentPublishedHandler` to route notifications to subscribed users
   - Icon: `icon-vinyl`

### Content Seeder
Created `VinylVaultSeeder.cs` that runs on startup in Development mode:
- Creates Vinyl Vault Home node if not already present (idempotent)
- Creates 7 genre landing pages: Jazz, Rock, Electronic, Hip-Hop, Classical, Techno, Nose Flute Jazz
- Creates 28 sample vinyl records (3-4 per genre) with realistic album data
- Sets `notificationGenre` property on each record to match the genre value (required for notification routing)

### Razor Views
Created strongly-typed views for all three document types:

1. **VinylVaultHome.cshtml**
   - Displays hero section with title/subtitle
   - Genre tiles grid linking to each genre landing page
   - Recent arrivals section showing 8 most recent vinyl records across all genres
   - Responsive grid layouts with CSS embedded

2. **VinylGenreLanding.cshtml**
   - Genre header with description
   - "Subscribe to [genre] notifications" button (placeholder for future API integration)
   - Vinyl grid showing all records in the genre with stock badges (In Stock / Out of Stock / Limited Edition)

3. **VinylRecord.cshtml**
   - Large cover art placeholder
   - Full vinyl metadata (artist, title, genre, release year, description)
   - Stock availability and count
   - "Subscribe to [genre] notifications" button
   - "Join Waitlist" button for out-of-stock items (placeholder for future API)
   - Breadcrumb navigation

### Notification Handler
Created `PrismContentPublishedHandler.cs`:
- Implements `INotificationAsyncHandler<ContentPublishedNotification>`
- Triggers when new vinyl records are published
- Reads `notificationGenre` property from published content
- Calls `IPrismNotificationService.SendNotificationToGenreSubscribersAsync()` to send push notifications
- Notification format: Title = "🎵 New arrival in {genre}", Body = "{artist} '{title}' just landed at Vinyl Vault!"
- Integrated with Core's `IPrismNotificationService` (implemented by Blathers)

### Registration
Updated `TestSiteComposer.cs` to register all handlers in correct order:
1. `VinylVaultContentTypes` (creates document types)
2. `VinylVaultSeeder` (seeds demo content)
3. `PrismContentPublishedHandler` (listens for published content)

### Learnings from This Session

- **Umbraco v17 IContentService publish pattern:** Must call `Save()` then `Publish()` separately. No `SaveAndPublish()` extension method exists. Pattern: `_contentService.Save(content, null, null!); _contentService.Publish(content, Array.Empty<string>(), Constants.Security.SuperUserId);`
- **AddPropertyGroup signature in v17:** Requires both `name` and `alias` parameters: `contentType.AddPropertyGroup("Content", "content");` — not just the name.
- **Built-in data type GUIDs are stable:** Umbraco v14+ provides well-known GUIDs for built-in data types (TextBox, TextArea, TrueFalse, Numeric, RichTextEditor, MediaPicker3). Use `IDataTypeService.GetAsync(guid)` to retrieve them, avoiding the complexity of PropertyEditorCollection instantiation.
- **Views without strongly-typed models:** When using `@inherits UmbracoViewPage` (non-generic), access properties via `Model.Value<T>("alias")` instead of `Model.PropertyName`. This avoids dependency on auto-generated models which don't exist until content types are scaffolded in backoffice.
- **Master.cshtml is the layout file:** Not `_Layout.cshtml`. The test site uses `~/Views/Shared/Master.cshtml` as the standard layout file that includes tenant branding and mobile nav.
- **Notification integration point:** The `notificationGenre` property on `vinylRecord` MUST match exactly what subscribers filter by (e.g., "Jazz", "Rock"). This is the contract between content and the notification system — no enum enforcement, just string matching.

### Files Created/Modified

**Created:**
- `src/UmbracoPrism.TestSite/VinylVaultContentTypes.cs` — Content type schema setup
- `src/UmbracoPrism.TestSite/VinylVaultSeeder.cs` — Demo content seeder
- `src/UmbracoPrism.TestSite/PrismContentPublishedHandler.cs` — Notification handler
- `src/UmbracoPrism.TestSite/Views/VinylVaultHome.cshtml` — Home page view
- `src/UmbracoPrism.TestSite/Views/VinylGenreLanding.cshtml` — Genre landing view
- `src/UmbracoPrism.TestSite/Views/VinylRecord.cshtml` — Record detail view

**Modified:**
- `src/UmbracoPrism.TestSite/TestSiteComposer.cs` — Registered Vinyl Vault handlers

**Dependencies:**
- Relies on `UmbracoPrism.Core.Services.IPrismNotificationService` (implemented by Blathers)
- Notification API endpoints (`/umbraco/api/prismnotification/subscribe`) referenced in views but not yet implemented (Blathers' domain)

### Design Decisions

1. **No hardcoded routes:** All navigation uses Umbraco's content tree (`Model.Url()`, `Model.Children`, `Model.Parent`) following Umbraco v17 best practices.
2. **Placeholder UI for subscription buttons:** JavaScript `alert()` placeholders for subscription features that will be wired to Blathers' API endpoints in Phase 3.
3. **Development-only seeder:** Both content type creation and seeding only run in Development environment via `IWebHostEnvironment.IsDevelopment()` check.
4. **Idempotent seeding:** Seeder checks if `vinylVaultHome` exists before creating any content, making it safe to run on every startup.
5. **Content-first approach:** Document types and content created in code to avoid manual backoffice setup for demo purposes. In production, editors would use backoffice to manage content.

### Next Steps (Out of Scope)

These are Blathers' or Isabelle's responsibilities:
- Implement subscription API endpoints in `PrismNotificationController`
- Wire up JavaScript in views to call subscription endpoints
- Implement "Join Waitlist" feature for out-of-stock items
- Implement limited edition drop scheduled notifications
- Add FCM device token registration flow for mobile apps

---

## 2026-04-03: Phase 2 Vinyl Vault Demo Content Completed

**Status:** ✅ Completed & Merged

**Deliverables:**
- Document types: VinylVaultHome, VinylGenreLanding, VinylRecord
- Idempotent seeder: `VinylVaultSeeder.cs` (7 genres, 28 records)
- Razor views: VinylVaultHome.cshtml, VinylGenreLanding.cshtml, VinylRecord.cshtml
- Event handler: ContentPublishedNotificationHandler.cs

**Key Decisions:**
1. Idempotent seeding — checks for existing content, safe to run repeatedly
2. Deterministic data — hardcoded genres/records for reproducibility
3. Event-driven demo — notification handler demonstrates Umbraco lifecycle

**Build Status:** ✅ C# 0 errors, seeder runs on startup, content publishes cleanly

**Documentation:**
- Inline code comments
- Seeder is self-documenting (genre names, record data hardcoded)
- Orchestration log: `.squad/orchestration-log/2026-04-03T12:23:47Z-brewster.md`
- Session log: `.squad/log/2026-04-03T12:23:47Z-phase2-phase3-notifications.md`

**Future Considerations:**
- Mobile bundle may reference Vinyl Vault as example content (no action needed)
- Production UI would require CSS framework (Bootstrap/Tailwind)
- Could expand genre/record data as needed

**Team Dependencies:** None (self-contained)

## Workflow Forms Engine Umbraco Integration Design (2026-04-08)

**Decision Set:** `📌 2026-04-08: Workflow Forms Engine Umbraco Integration (Brewster)` in `.squad/decisions.md`

**Role:** Umbraco platform specialist for Workflow Forms Engine. Designed MockBackOffice emulator, seed packs, TestSite integration, and security patterns aligned with Tom Nook's architecture and Copper's security decisions.

**Decisions Produced:** 5 Umbraco-specific decisions
1. MockBackOffice RuntimeMode Toggle — Config-based Emulator vs. Core runtime switching (dual-purpose: standalone demo + integration test harness)
2. Emulator-Only Extensions Must Be Namespaced — `MockBackOffice.Workflow.*` never leaks into Core contracts; Core uses "actor" terminology, emulator uses "persona"
3. Workflow Seed Packs in JSON Format — Reproducible demo scenarios, source-controlled, shareable
4. TestSite Workflow Demo Page Document Type — Code-first Umbraco v17 pattern; editors configure workflow key + page content
5. Security Guards Always Execute in Core Runtime — Emulator never bypasses auth/tenant checks; all decisions flow through Core services

**Integration Patterns:** Follows existing Prism conventions (tenant resolution, JWT Bearer tokens, MockBackOffice composer pattern, IWorkflowSeedLoader DI registration).

**Design Phase Status:** ✅ Complete (Umbraco design doc: `docs/design/workflow-forms-engine-umbraco.md` completed)



## Session: Workflow Forms Engine Redesign — 2026-04-09

**Timestamp:** 2026-04-09T17:48:03Z  
**Role:** Umbraco Platform Specialist  
**Sprint Type:** Cross-agent architecture sprint (parallel with Tom Nook, Blathers, Isabelle)

### Deliverables

1. **Platform Analysis:** `.squad/decisions/decisions.md` — "Element Types as Workflow Step Definitions — Umbraco v17 Platform Analysis"
   - Element Types API surface and DI patterns
   - Property editor discovery and rendering architecture
   - Code-first seeding strategy
   - Migration path (Phase 1–4)
2. **Orchestration Log:** `.squad/orchestration-log/2026-04-09T17:48:03Z-brewster.md`

### Key Findings

- ✅ Element Types approach is **sound and Umbraco-native**
- ✅ `IContentTypeService` and `IDataTypeService` already available in DI
- ✅ Deterministic code-first creation with fixed GUIDs ensures reproducibility
- ✅ Standard property editors (TextString, DateTime, Dropdown, TrueFalse) fully supported
- ✅ Built-in validation (mandatory, regex) works out of the box
- ✅ TestSite seeding strategy leverages existing `PrismContentTypeSeeder` pattern

### Phase Outcomes

- Platform integration path confirmed
- No blocker services or missing APIs
- Ready for Blathers backend implementation
- Ready for Isabelle frontend testing



## Session: Workflow Element Types + Seeding Implementation — 2026-04-09

**Timestamp:** 2026-04-09T18:15:00Z  
**Role:** Umbraco Platform Specialist  
**Task:** Implement WorkflowElementTypeSeeder and update WorkflowSeedServiceImpl

### Deliverables

1. **WorkflowElementTypeSeeder** (`src/UmbracoPrism.Core/Services/Workflow/WorkflowElementTypeSeeder.cs`)
   - Creates two Element Types programmatically: `workflowPersonalDetails` and `workflowFinancialDetails`
   - Uses fixed GUIDs for deterministic seeding (5 data types created)
   - Follows `PrismContentTypeSeeder.cs` pattern exactly
   - Idempotent: checks if element types exist before creating

2. **WorkflowSeedServiceImpl Updates**
   - Injects `IWorkflowDefinitionRepository` and `WorkflowElementTypeSeeder`
   - Seeds Element Types first, then workflow definitions
   - Parses JSON and calls `_repository.UpsertAsync()`
   - Removed `FieldGroupKeys` property reference (deprecated in redesign)

3. **WorkflowSeedService Updates**
   - Now uses `IServiceScopeFactory` to create scope for scoped services
   - Fixed DI lifetime issue (hosted service is singleton, dependencies are scoped)

4. **Demo Workflow JSON** (`src/UmbracoPrism.Core/workflow-seeds/retirement-quote-v1.json`)
   - Retirement quote workflow with 4 states (personal-details, financial-info, review, complete)
   - Uses `elementTypeAlias` references to `workflowPersonalDetails` and `workflowFinancialDetails`
   - Includes transitions with continue/back/submit actions

5. **DI Registration Updates** (`WorkflowBuilderExtensions.cs`)
   - Added `WorkflowElementTypeSeeder` as scoped service
   - Changed `IWorkflowSeedService` from singleton to scoped (dependency on scoped services)

### Integration Points

- ✅ Coordinated with Blathers' backend changes (`ElementTypeAlias` property on `WorkflowState`)
- ✅ Removed deprecated `FieldGroupKeys` from `WorkflowDefinition` mapping
- ✅ Follows Umbraco v17 API patterns (IContentTypeService, IDataTypeService)
- ✅ Solution builds successfully (Core + MockBackOffice + TestSite)

### Technical Decisions

- Fixed GUIDs for workflow data types ensure reproducibility across environments
- Element Types use standard property editors (TextBox, EmailAddress, DateTime, Integer, Toggle)
- Seed service continues even if Element Type creation fails (resilient startup)
- JSON structure matches `WorkflowDefinition` model with case-insensitive deserialization

### Status

✅ Complete — Ready for integration testing with Isabelle's frontend components

---

## Session: 2026-04-09 — Route-Hijacking Workflow Controller

**Status:** Completed
**Build outcome:** Success, 0 errors, 0 warnings (Core + TestSite).

### Problem

The previous workflow UI used a `workflowDemoPage` document type with a static `<prism-workflow-shell>` Web Component (Lit/JSON API). The team decided to replace this with a Razor-over-route-hijacking approach so the server renders each workflow step as HTML, no client-side JavaScript framework required.

### Work completed

1. **`WorkflowViewModel.cs`** — new file at `src/UmbracoPrism.TestSite/Models/`. Composes `WorkflowRenderPayload` properties (Archetype, StateDisplayName, FieldGroups, AvailableActions) with form-tracking fields (InstanceId, StateVersion, WorkflowKey, ReturnUrl). Includes `FieldErrors` computed property for inline validation display.

2. **`WorkflowAdvanceRequest.cs`** — new file at `src/UmbracoPrism.TestSite/Models/`. Simple flat model for the POST body: InstanceId, StateVersion, WorkflowKey, Action, ReturnUrl, FieldValues dictionary.

3. **`WorkflowPageController.cs`** — new file at `src/UmbracoPrism.TestSite/Controllers/`. Route-hijacking controller for the `workflowPage` document type. Handles both GET and POST in `Index()` (Umbraco's content router always targets Index; HTTP method is checked manually to avoid a Surface Controller). GET creates/resumes a workflow instance via cookie, builds WorkflowViewModel, returns `CurrentTemplate(vm)`. POST validates antiforgery manually via `IAntiforgery`, reads the form, calls `IWorkflowInstanceService.AdvanceAsync`, stores problems in TempData if validation failed, redirects (PRG) back to the page URL.

4. **`PrismContentTypeSeeder.cs`** — added `EnsureWorkflowPageAsync()` and `EnsureWorkflowKeyPropertyAsync()` methods. Creates the `workflowPage` document type (AllowedAsRoot = true, icon: activity) with a single `workflowKey` textstring property. Called from `HandleAsync` alongside the existing doc types.

5. **`WorkflowPageSeeder.cs`** — new file at `src/UmbracoPrism.TestSite/`. Development-only notification handler. Creates and publishes a root-level "Retirement Quote" content node of type `workflowPage` with `workflowKey = "retirement-quote"`. Idempotent — skips if the node already exists.

6. **`TestSiteComposer.cs`** — registered `WorkflowPageSeeder` as an `UmbracoApplicationStartedNotification` handler.

7. **`Views/WorkflowPage.cshtml`** — main template. Uses `@model WorkflowViewModel`, `Layout = "Master"`. Dispatches to partial views based on `Model.Archetype`. Shows an error panel when `Model.HasError` is true.

8. **`Views/Partials/_WorkflowStep-Collect.cshtml`** — renders field groups as a labelled form. Hidden fields echo InstanceId, StateVersion, WorkflowKey, ReturnUrl. Fields rendered by type (text, email, number, tel, textarea, select). Inline field-level error display. Action buttons use `name="Action" value="{actionKey}"`.

9. **`Views/Partials/_WorkflowStep-Review.cshtml`** — definition-list summary of all answered fields. Action buttons in a minimal form (no fields, just hidden tracking fields + action buttons).

10. **`Views/Partials/_WorkflowStep-Completion.cshtml`** — thank-you panel with a "Return to home" link. Clears the instance cookie after POST in the controller.

### Key decisions

- **Both verbs in Index():** Umbraco's content router hardcodes `action = "Index"` in the route values. Adding a `[HttpPost]` action named `Advance` would need explicit MVC routing registration to be reachable at the content node URL. Inspecting `HttpContext.Request.Method` inside `Index()` is simpler and equally correct for a demo.
- **Manual antiforgery:** `IAntiforgery.ValidateRequestAsync()` provides the same protection as `[ValidateAntiForgeryToken]` without requiring a dedicated `[HttpPost]` action.
- **Anonymous userId tracking:** Cookie `PrismAnonUserId` stores a GUID that serves as the userId for the instance service. Real implementations would use `User.FindFirst("oid")`.
- **form field prefix `fields[key]`:** Allows multiple fields without collision with the tracking hidden fields (InstanceId, StateVersion, etc.).
- **No workflowRenderService injection:** The existing `IWorkflowInstanceService` returns a `WorkflowResponseEnvelope` that already includes a rendered `WorkflowRenderPayload` via `WorkflowRenderService` internally. No double-render needed.

### Test URL (dev)

`https://localhost:{port}/retirement-quote` — shows the Retirement Quote workflow using the `retirement-quote` definition already seeded by `WorkflowSeedService`.

## Session: 2026-04-09 — Workflow Razor Redesign (Scribed)

**Orchestration Log:** `.squad/orchestration-log/2026-04-09T18:13:54Z-brewster-implement.md` + `.squad/orchestration-log/2026-04-09T18:13:54Z-brewster-controller.md`  
**Session Log:** `.squad/log/2026-04-09T18:13:54Z-workflow-razor-redesign.md`

**Parallel Agents:** Blathers (Element Type Pipeline), Isabelle (Razor Partials)

### Work Completed — Phase 1: Seeds & Element Types

1. **WorkflowElementTypeSeeder Service**
   - Created `workflowPersonalDetails` Element Type (name, email, DOB)
   - Created `workflowFinancialDetails` Element Type (income, employer, tax resident)
   - Idempotent pattern with deterministic GUIDs

2. **WorkflowSeedServiceImpl Updates**
   - Calls `EnsureElementTypesAsync()` before loading workflow definitions
   - Parses `retirement-quote-v1.json` workflow
   - Removed `FieldGroupKeys` (deprecated)

3. **Demo Workflow**
   - `retirement-quote-v1.json` — 4-state workflow (collect → financial → review → complete)

### Work Completed — Phase 2: Controller & HTTP

1. **WorkflowPageController**
   - Route-hijacking controller for `workflowPage` document type
   - GET/POST in single `Index()` (Umbraco pattern)
   - Manual antiforgery validation
   - Cookie-based anonymous user tracking

2. **View Models**
   - `WorkflowViewModel` — workflow state + step metadata
   - `WorkflowAdvanceRequest` — form submission binding

3. **workflowPage Document Type**
   - Added to `PrismContentTypeSeeder`
   - `workflowKey` textstring property

4. **Demo Content**
   - `WorkflowPageSeeder` publishes `/retirement-quote` node
   - Test URL: `https://localhost:{port}/retirement-quote`

5. **Razor Templates**
   - `Views/WorkflowPage.cshtml` — main layout
   - `Views/Partials/_WorkflowStep-Collect.cshtml`
   - `Views/Partials/_WorkflowStep-Review.cshtml`
   - `Views/Partials/_WorkflowStep-Completion.cshtml`

### Result

✅ **Build Status:** 0 errors, 0 warnings (Client + .NET)

**Integration:** Complete workflow orchestration from seeded data → HTTP handler → Razor rendering. Frontend (Isabelle) decorates with `_WorkflowField.cshtml` reusable renderer.

---

## Session: 2026-03-30 — Workflow Form Tag Helper Design Research

**Status:** Completed (Research + Design Doc)  
**Requested by:** Jonny Muir  

**Task:** Research current workflow form implementation and advise on tag helper approach for validation. Make it **idiomatic Umbraco 17** and follow **principle of least surprise**.

### Current State Findings

1. **Existing Workflow Views:**
   - `WorkflowPage.cshtml` — Route-hijacking pattern with archetype-driven partial selection (✅ idiomatic)
   - `_WorkflowStep-Collect.cshtml` — Manual form with 4 hidden fields, antiforgery token, field iteration via `Html.PartialAsync()`
   - `_WorkflowStep-Review.cshtml` — Read-only summary with separate form for actions
   - `_WorkflowField.cshtml` — 200-line partial handling 10+ field types (text, email, number, boolean, radio, checkboxlist, select, textarea, date, datetime)

2. **Current Model:**
   - `WorkflowViewModel` extends `PublishedContentWrapped` (enables route hijacking)
   - Contains `FieldGroups`, `AvailableActions`, `Problems`, `FieldErrors` dictionary
   - Controller uses manual antiforgery validation via `IAntiforgery.ValidateRequestAsync()`

3. **Existing Tag Helpers:**
   - **`PrismDebugTagHelper`** (`<prism-debug>`) — comprehensive debug panel
   - **`PrismMobileUserAgentDemoTagHelper`** (`<prism-mobile-user-agent-demo>`) — UA mocking toggle
   - Both in `UmbracoPrism.Core/TagHelpers/` namespace
   - Already registered in `_ViewImports.cshtml` via `@addTagHelper *, UmbracoPrism.Core`

### Tag Helper Design Recommendations

**Minimum viable set:**
1. **`<prism-workflow-form model="@Model">`** — Replaces form boilerplate, injects antiforgery + 4 hidden fields automatically
2. **`<prism-field field="@field" errors="@Model.FieldErrors" />`** — Replaces 200-line partial, type-safe, faster than runtime view engine
3. **`<prism-error-summary problems="@Model.Problems" />`** — GDS-style accessibility pattern for form-level errors

**Optional (lower priority):**
4. **`<prism-workflow-actions actions="@Model.AvailableActions" />`** — Action button renderer (less boilerplate savings)

### Assembly and Namespace

- **Namespace:** `UmbracoPrism.Core.TagHelpers` (same as existing tag helpers)
- **Assembly:** `UmbracoPrism.Core.csproj` (shipped package, not TestSite)
- **Registration:** Already auto-discovered via `@addTagHelper *, UmbracoPrism.Core` in `_ViewImports.cshtml`

### Umbraco v17 Considerations

- **Tag helpers are standard ASP.NET Core** — no special Umbraco handling required
- Umbraco v17 embraces tag helpers (e.g., `<umb-block-grid>`)
- If tag helpers need `ViewContext`, use `[ViewContext]` attribute (example in `PrismDebugTagHelper`)
- Constructor DI supported (example in `PrismDebugTagHelper` with 5 injected services)

### Before vs After

**Current implementation:** 55 lines of boilerplate (manual token, hidden fields, ViewData anti-pattern for error passing)  
**With tag helpers:** 18 lines (67% reduction), declarative, self-documenting

### Security Notes

- Antiforgery token injected via `IHtmlHelper.AntiForgeryToken()` (same as current)
- All field values HTML-encoded via `TagHelperOutput.SetAttribute()` (automatic encoding)
- Field keys are server-controlled — no injection risk

### Outcome

✅ **Design doc written:** `.squad/decisions/inbox/brewster-taghelper-design.md`  
✅ **Recommendation:** Implement MVP set (`<prism-workflow-form>`, `<prism-field>`, `<prism-error-summary>`)  
✅ **Principle of least surprise:** Tag helpers are expected ASP.NET Core patterns — Umbraco developers will recognize them immediately  
✅ **Idiomatic Umbraco v17:** Tag helpers are first-class citizens in Umbraco v17 (same as Block Grid, backoffice Web Components)

### Learnings

- **Tag helpers in Prism codebase already established** — two existing tag helpers in `UmbracoPrism.Core/TagHelpers/` prove the pattern is approved
- **ViewData anti-pattern in current workflow forms** — passing `errorVd` dictionary to partial is unnecessary with tag helpers (type-safe attributes replace it)
- **200-line partial is ripe for tag helper migration** — field rendering logic belongs in compiled code, not runtime view engine
- **Manual antiforgery token injection** — current implementation uses `@Html.AntiForgeryToken()` in every form partial; tag helper centralizes this
- **GDS-style error summary** — current implementation has manual `Model.Problems.Where(p => string.IsNullOrEmpty(p.FieldKey))` filtering; tag helper encapsulates this pattern
- **Tag helpers are faster than partials** — compiled at build time, no runtime view engine resolution overhead
- **Umbraco v17 has no tag helper restrictions** — standard ASP.NET Core patterns apply; no surprises

---

## Session: 2026-04-11 — Replace Retirement Quote Demo with Community Enquiry

**Status:** Completed  
**Requested by:** Jonny Muir  

**Task:** Replace the old "Retirement Quote" workflow demo with "Community Enquiry" (Get in Touch) — a better showcase of Prism workflow features.

### Files Updated

1. **`WorkflowPageSeeder.cs`**
   - Changed from seeding "Retirement Quote" at `/retirement-quote` to "Get in Touch" at `/get-in-touch`
   - Content node name: `"Get in Touch"` (user-facing page title)
   - `workflowKey` property: `"community-enquiry"` (workflow definition identifier)
   - URL slug: Auto-generated as `/get-in-touch` by Umbraco
   - Updated XML doc comment to reflect new demo
   - Added `CleanupOldRetirementQuotePage()` method to DELETE existing "Retirement Quote" nodes on startup (keeps demo clean)
   - Updated `EnsureCommunityEnquiryPage()` to check for existing node by BOTH name AND workflowKey (handles edge cases)

2. **`PrismContentTypeSeeder.cs`**
   - Updated `workflowKey` property description example from `'retirement-quote'` to `'community-enquiry'`

3. **`IBusinessAppWorkflowClient.cs`**
   - Updated `GetCurrentAsync` XML doc example from `"retirement-quote"` to `"community-enquiry"`

4. **`WorkflowPageController.cs`**
   - Updated field example in XML doc comment from `"fields[retirement-age]"` to `"fields[full-name]"` (more generic)

### Build Status

✅ **0 errors, 0 warnings** — Clean build on TestSite project

### Design Notes

- **Cleanup strategy:** The seeder now deletes old "Retirement Quote" nodes on startup rather than leaving orphaned demo data. This ensures developers see only the current demo.
- **Idempotent behavior:** The seeder checks for existing "Get in Touch" nodes by BOTH name and workflowKey to handle all edge cases (renamed nodes, manual edits, etc.).
- **Workflow key convention:** Kebab-case workflow keys (`community-enquiry`) match URL slug pattern (`/get-in-touch`) — consistent with REST API naming.
- **Auto-generated files:** `WorkflowPage.generated.cs` will be regenerated on next build with the updated description.

### Learnings

- **Content node lifecycle:** Umbraco content nodes persist in the database across code changes. Demo seeders must handle cleanup explicitly to avoid confusion.
- **Dual-key lookups:** Checking both `Name` and `workflowKey` property handles manual edits in the backoffice (e.g., a user renaming "Get in Touch" but leaving the workflowKey unchanged).
- **contentService.Delete():** Returns an `OperationResult` with `.Success` and `.Result` — same pattern as Save/Publish.

---

## Session: 2026-03-29 — Workflow Tag Helpers Implementation

---

## Session: 2026-04-11 — TestSite Demo Review and Polish

**Status:** Completed  
**Requested by:** Jonny Muir  
**Build outcome:** Success, 0 errors, 0 warnings.

**Task:** Thorough review of the testsite workflow demo to ensure everything is wired correctly and the demo is polished for showcase.

### Review Findings and Fixes

1. **WorkflowPage.cshtml** — ✅ Reviewed
   - Correctly delegates to `_WorkflowStep-Collect.cshtml` for `Collect` archetype
   - Error state handled gracefully with `Model.ErrorMessage`
   - Page title shows `Model.StateDisplayName` (semantically correct for multi-step workflows)
   - Browser title uses ViewBag.Title = StateDisplayName (shows current step)

2. **_WorkflowStep-Collect.cshtml** — ✅ Reviewed
   - Tag helper usage is correct: `<prism-workflow-form>`, `<prism-error-summary>`, `<prism-field>`
   - All attributes properly bound to view model properties
   - Clean 38-line implementation vs. previous 200+ line partial approach

3. **_WorkflowStep-StatusTimeline.cshtml** — ✅ Reviewed
   - Nice polish: ⏳ icon, clear messaging about submission review status
   - Copy is user-friendly and professional

4. **_WorkflowStep-Completion.cshtml** — ✅ Reviewed
   - ✅ icon, confirmation panel with green styling
   - "Return to home" action button
   - Professional completion messaging

5. **PrismWorkflowFormTagHelper.cs** — ✅ Reviewed
   - Nonce correctly emitted as `<input type="hidden" name="Nonce" value="..." />`
   - Antiforgery token correctly injected via `IAntiforgery.GetAndStoreTokens()`
   - Form `action` attribute correctly set from `return-url`
   - All hidden fields properly emitted (InstanceId, StateVersion, WorkflowKey, ReturnUrl, Nonce)

6. **PrismFieldTagHelper.cs** — ✅ Reviewed and Fixed
   - **FIXED:** Checkbox list checked state — now parses `field.Value` as comma-separated string and checks against each option
   - ✅ Boolean fields correctly submit `value="true"` when checked
   - ✅ Radio fields layout correct
   - ✅ Select fields have blank placeholder `-- Select --` as first option
   - All fields correctly emit `name="fields[{fieldKey}]"` matching controller extraction pattern

7. **Navigation** — ✅ Fixed
   - Added "Get in Touch" link to Master.cshtml header nav
   - Navigation now shows: Home | Get in Touch | hostname badge

8. **CSS Styles** — ✅ Added (Critical Fix)
   - **Added 300+ lines of workflow form CSS** to `components.css`
   - Comprehensive styling for all workflow components:
     - `.prism-workflow`, `.workflow-page__*` — page structure
     - `.workflow-alert--error`, `.workflow-alert--warn` — error states
     - `.prism-error-summary` — GDS-style error summary with accessible red styling
     - `.prism-form-group`, `.prism-label`, `.prism-required` — form structure
     - `.prism-input`, `.prism-textarea`, `.prism-select` — form controls with focus states
     - `.prism-radio-item`, `.prism-checkbox-item` — choice controls
     - `.prism-button--primary/secondary/destructive` — action buttons
     - `.prism-status__*`, `.prism-panel__*` — status and completion states
   - All styles follow Prism design token pattern (`var(--prism-primary, #4f46e5)`)
   - Accessibility: focus states, aria support, color contrast compliant

### Code Quality

- **Controller → Validator → Tag Helper flow:** All components correctly wired
- **Form submission:** Controller extracts `fields[*]` keys, validator handles checkboxlist suffix `[]` variation
- **Checkbox list values:** ASP.NET Core auto-concatenates multiple checkbox values with commas (e.g., `"option1,option2"`)
- **Boolean handling:** Unchecked checkboxes submit nothing; validator treats missing as `false` (correct behavior)
- **Nonce validation:** Tamper-proof nonce service prevents field definition manipulation
- **HTML encoding:** All user input HTML-encoded via `System.Net.WebUtility.HtmlEncode()`

### Build Status

✅ **0 errors, 0 warnings** — Clean build after all fixes

### Outcome

The "Get in Touch" workflow demo is now fully functional and polished:
- All tag helpers working correctly
- Full CSS styling in place (previously missing!)
- Navigation includes link to demo page
- Checkbox list state preservation fixed
- Professional UX with error states, status timeline, and completion confirmation

**Demo is ready for showcase.**

### Learnings

- **CSS is not optional:** Tag helpers were built but the demo was incomplete without matching CSS. Always check for missing styles when implementing new UI components.
- **Checkbox list checked state:** When `field.Value` contains multiple values, it's comma-separated (e.g., `"Red,Blue"`). Must split and compare against options.
- **ASP.NET Core form behavior:** Multiple checkboxes with the same `name` attribute auto-concatenate values with commas on POST.
- **Workflow title semantics:** For multi-step workflows, showing the current *state* display name as the page title is correct UX (not the workflow definition name).
- **GDS accessibility patterns:** Error summary with `role="alert"`, `tabindex="-1"`, and anchor links to fields is the gold standard for form error handling.

---


## Session: 2026-03-29 — Workflow Field Pre-population from Member Claims
**Status:** Completed  
**Build outcome:** Success, 0 errors, 0 warnings.

**Problem:** The "Get in Touch" workflow demo needed two enhancements:
1. A prominent link from the Member Dashboard to the workflow page
2. Pre-population of email and name fields from authenticated user claims

**Solution implemented:**

1. **MemberDashboard.cshtml:**
   - Added fourth card to the dashboard grid with "Get in Touch" workflow CTA
   - Styled with emoji icon (📝), clear description, and primary button linking to /get-in-touch
   - Visually consistent with existing dashboard cards

2. **WorkflowResponseEnvelope.cs (FieldRenderPayload):**
   - Added DefaultValue (string?) property for server-side field pre-population
   - Added ReadOnly (bool) property to indicate fields that cannot be edited by user
   - DefaultValue takes precedence over user-submitted values and BA-supplied Value

3. **WorkflowPageController.cs:**
   - Added PrePopulateFieldsFromClaims() method that inspects HttpContext.User claims
   - Extracts email from ClaimTypes.Email or "email" claim
   - Extracts name from ClaimTypes.Name or "name" claim
   - Sets DefaultValue and ReadOnly = true on email-address and full-name fields if claims exist
   - Only modifies fields where we have claim values (does not clear BA-supplied defaults)
   - Pre-population happens BEFORE nonce creation (so readonly state is authoritative)

4. **PrismFieldTagHelper.cs:**
   - Extended all render methods to accept readonlyAttr and readonlyCssClass parameters
   - For text/email/textarea/number inputs: adds readonly aria-readonly="true" attributes + CSS class
   - For select fields: renders as plain text display with hidden input to preserve value for submission
   - For checkboxes: adds disabled attribute (readonly checkboxes are not meaningful in HTML5)
   - DefaultValue takes precedence over Values dictionary (user-submitted) when rendering initial value

5. **WorkflowFieldValidator.cs:**
   - Skip validation for ReadOnly fields entirely (server provided value, not user input)
   - Readonly fields still pass through to BA in submitted field values

6. **prism-forms.css:**
   - Added .prism-field__input--readonly CSS class with greyed background, muted text, no-hover
   - Added .prism-field__readonly-display for plain-text rendering of readonly select/radio fields
   - Uses color-mix() for subtle background tint, maintains accessibility contrast
   - cursor: not-allowed and opacity: 0.85 provide visual feedback

**Technical approach:**
- Controller pre-population happens server-side before nonce generation (tamper-proof)
- Uses C# records with syntax for immutable envelope transformation
- TagHelper checks DefaultValue first, then Values, then Value for render priority
- Validator skips readonly fields (no client-side manipulation risk)
- CSS maintains Prism design system consistency (custom properties, color-mix)

**Outcome:** Authenticated members see their email and name pre-filled and locked in workflow forms. Dashboard provides clear entry point to workflow demo. Build clean, pattern proven, accessible.

## 2025-01-XX — Wired TestSite for conditional fields + workflow hub

**Context:** Added support for conditional "Other" field in community-enquiry form and seeded the new "My Workflows" hub page.

**Changes:**
1. **Updated `your-enquiry-v1.json`**
   - Added "Other" option to enquiry-type radio field
   - Added new conditional field `enquiry-type-other` (text, max 100 chars)
   - Conditional field uses `conditionalOn: "enquiry-type"` and `visibleWhen: "Other"`

2. **Updated `community-enquiry-v1.json`**
   - Added `"instancePolicy": "single"` to enforce single active instance per user

3. **Added workflow hub page seeding**
   - Created `EnsureWorkflowHubPage()` method in WorkflowPageSeeder.cs
   - Seeds a "My Workflows" page using `workflowHub` document type
   - Published at `/my-workflows`

4. **Updated Master.cshtml navigation**
   - Added "My Workflows" link to main nav

**No BA engine changes needed:** The FieldFile record and mapping in BusinessAppWorkflowEngine already included ConditionalOn/VisibleWhen properties (lines 105-107 in WorkflowDefinitionFile.cs, lines 381-382 in BusinessAppWorkflowEngine.cs).

**Build status:** Existing unrelated errors for WorkflowInstanceListEnvelope — not introduced by this work.


---

## Session: 2026-04-12 — Aspire TestSite Launch Profile Selection
**Status:** Completed  
**Build outcome:** Success.

**Problem:** UmbracoPrism.TestSite was launched by AppHost through Aspire but the Aspire dashboard showed the resource running without an advertised URL, preventing navigation to the site.

**Root Cause:** Aspire matches service launch profiles by name with the host profile by default. AppHost runs under the `https` profile, so Aspire looks for a `https` profile in TestSite's launchSettings.json. TestSite (sourced from Umbraco template) uses `Umbraco.Web.UI`, not `https`. When no name match is found, Aspire falls back to the first profile (`IIS Express`), which lacks the `applicationUrl` that advertises the site in the dashboard.

**Solution:** Pinned TestSite launch profile explicitly in src/UmbracoPrism.AppHost/Program.cs:

```csharp
builder.AddProject("testsite", "../UmbracoPrism.TestSite/UmbracoPrism.TestSite.csproj", launchProfileName: "Umbraco.Web.UI")
```

**Outcome:** Aspire now parses the correct `applicationUrl` from the `Umbraco.Web.UI` profile. TestSite advertises its URL in the dashboard on restart.

**Standing Effect:** When Umbraco-based projects in this repo use nonstandard launch profile names, AppHost should select them explicitly rather than relying on Aspire's default launch-profile matching.

---

## Session: 2026-04-12 — Keycloak HTTPS Exposure Check
**Status:** Completed  
**Build outcome:** Success.

**Problem:** Local docs and AppHost wiring advertised `https://localhost:8443` for Keycloak, but Safari could not open it.

**Root Cause:** `src/UmbracoPrism.AppHost/Program.cs` used `WithHttpsEndpoint(port: 8443, targetPort: 8080)` against Keycloak's HTTP-only `start-dev` listener. Aspire/DCP exposed a host port on 8443, but it served plain HTTP, not TLS. That made `KEYCLOAK_URL=https://localhost:8443` misleading and unusable.

**Solution:** Removed the fake HTTPS endpoint, pinned `KEYCLOAK_URL` to the real host HTTP route (`http://localhost:8080`), and updated local-dev docs to explain that real browser HTTPS requires a cert-backed reverse proxy or Keycloak native HTTPS.

**Outcome:** AppHost no longer injects a broken HTTPS authority into TestSite. The repo now documents the exact limitation instead of implying that Safari trust was the blocker.

## Learnings

- **Aspire container endpoint naming is not TLS termination.** On this repo's Keycloak `start-dev` container, `WithHttpsEndpoint(port: 8443, targetPort: 8080)` created a host listener that still served plain HTTP. Verify local "HTTPS" IdP routes with `curl`/`openssl` before seeding browser-facing authorities from them.
- **Umbraco v17 navigation in Razor should use typed extension methods, not deprecated tree properties.** In TestSite templates, prefer `Children<T>()` and `Parent<T>()` over `IPublishedContent.Children` / `Parent`; it removes obsolete warnings and keeps document-type intent explicit in `Views/VinylGenreLanding.cshtml`, `Views/VinylVaultHome.cshtml`, and `Views/VinylRecord.cshtml`.
- **MVC partial rendering in Razor layouts should stay async.** Use `@await Html.PartialAsync(...)` in shared Umbraco layouts like `Views/Shared/Master.cshtml` to avoid MVC1000 warnings and match the rest of the TestSite partial-rendering pattern.
- **Workflow hub contract:** `workflowHub` is protected member content. Its `RenderController` should use `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]`, and resume links must resolve the actual `workflowPage` content node from Umbraco content (`workflowKey`) instead of guessing a `/{workflowKey}` URL.
- **Workflow layouts:** Route-hijacked workflow views in `src/UmbracoPrism.TestSite/Views/WorkflowHub.cshtml` and `src/UmbracoPrism.TestSite/Views/WorkflowPage.cshtml` are stable when they set `Layout = "~/Views/Shared/Master.cshtml"`; filename-only `Master`/`Master.cshtml` is brittle in this area.
- **Workflow regression assertions:** The stable Playwright contract for the member workflow area is user-visible: `/my-workflows` requires the Prism member session, renders shared chrome plus the `My Workflows` heading, and every CTA in that area should lead to the seeded workflow content page (`/get-in-touch` today) via content-tree resolution rather than workflow-key URL guesses.

## Phase 1 Completion Summary (2026-04-13)

**Status:** ✅ Clean-boot readiness fix complete. Live localhost Playwright suite passes startup/auth/navigation tests.

### Deliverables

1. **Stable Umbraco Seed Contract** — Single canonical seed path for TestSite authenticated demo (Home → Dashboard → Workflow pages)
2. **Machine-Readable Readiness Endpoint** — `GET /api/prism/downstream-demo/seed-contract-ready` replaces rendered-text probe; includes normalized URLs and auth challenge contract
3. **Keycloak HTTPS on 8443** — Browser-facing issuer `https://localhost:8443/realms/prism-dev` with restart-stable cookie flow
4. **Build Warnings Elimination** — Typed Umbraco navigation in views; app-rooted layout paths (`~/Views/Shared/Master.cshtml`)

### Test Results

- ✅ Live Playwright suite: startup pass
- ✅ Live Playwright suite: auth pass
- ✅ Live Playwright suite: navigation pass
- ⚠️ Scoped blocker: restart-only downstream API case (Blathers follow-up)

### Follow-up

Blathers spawned to fix restart-only downstream API failure; Tangy to validate after fix.

## Tasks — 2026-04-13 — Dashboard Route Contract Validation (parallel spawn batch)

**Orchestration Log:** `.squad/orchestration-log/2026-04-13T23:42:20Z-brewster.md`

**Spawned:** Brewster, Blathers, Tangy for parallel investigation of dashboard redirect behavior

**Task Summary:**
- Brewster: Confirm `/dashboard` route validity and auth challenge behavior ✅

## Learnings (2026-04-14 — Dashboard home-bounce diagnosis)

- **A signed-in bounce back to `/` is more likely a home-owned auth entry point than broken Umbraco dashboard routing.** In this repo the dashboard CTA already resolves from published content to `/dashboard`, and the known `/ -> login -> /` loop comes from the unauthenticated home-page `Sign In` link omitting a `returnUrl`, which makes `AccountController.Login` and the OIDC callback fall back to `/`.
- **For member-area CTAs on public TestSite pages, carry the authored target into the login link.** `Views/HomePage.cshtml` should build `/auth/login?returnUrl={dashboardUrl}` from the same content-resolved dashboard URL it shows after sign-in, so the first successful login lands on the intended member page instead of the ambiguous signed-in home page.
- Blathers: Inspect auth/session redirect flow ⏳
- Tangy: Complete dashboard navigation trace and identify test readiness signals ✅

**Brewster Findings:**
- `/dashboard` is a valid published route with correct auth challenge behavior
- Unauthenticated requests correctly redirect to `/auth/login?ReturnUrl=%2Fdashboard`
- App-side route wiring is sound
- Route contract is valid; redirect behavior is login flow specific

**Decision Merged:** Consolidated Brewster and Tangy findings into `.squad/decisions.md` section "📌 2026-04-13: Brewster — Dashboard Route Contract" with sub-section "Tangy — Dashboard navigation trace"

## Learnings (2026-04-14 — Classifying transient seeded child routes)

- **A seeded child briefly resolving to `/` on first boot is not normal steady-state Umbraco behaviour; it is mainly a cold-start convergence artefact of this app's runtime pattern.** In this repo we intentionally boot against a reset isolated runtime DB, run unattended install, then publish the demo tree in `WorkflowPageSeeder` on `UmbracoApplicationStartedNotification` while Razor immediately resolves links from `ContentAtRoot()` + published `Url()`. That combination can expose a short window where the node exists in published discovery before Umbraco has finished computing the final hierarchical child path.
- **So the right classification is "Umbraco can transiently do this during startup, but our seeding/runtime design is what makes it visible and user-facing."** A warm, already-settled Umbraco site should not keep returning `/` for a valid child page; our development-only reset/seeding flow and eager route consumption are the primary reasons the wrong-route symptom shows up here.

## Learnings (2026-04-14 — Route-readiness strategy for cold boots)

- **The test harness should wait for the seeded route contract, not for page copy.** In this repo the authoritative startup signal is `GET /api/prism/downstream-demo/seed-contract-ready` returning `ready: true` / `routeContractReady: true`, with the home-page `data-prism-home-ready="true"` marker acting only as a smoke check that the real Razor site is serving.
- **Behaviour tests should never absorb cold-start convergence quirks into their assertions.** Once readiness says the contract is settled, tests should require the authored URLs and expected auth challenge targets (`/dashboard`, `/get-in-touch`, `/my-workflows`), rather than tolerating a transient `/` fallback that only exists during fresh-runtime bootstrapping.

## Learnings (2026-04-14 — Auth cookie redirect leakage and seeded routes)

- **Do not persist the one-off OIDC post-login `RedirectUri` inside `PrismMemberCookie`.** In this repo, storing `/dashboard` on the auth ticket let later protected requests such as `/my-workflows` collapse back to the previous login target even after `seed-contract-ready` reported the authored route contract as settled. Capture the return target for the immediate `/signin-oidc` redirect, then clear `AuthenticationProperties.RedirectUri` before issuing the long-lived member cookie.
- **A seeded-route readiness probe can be truly correct while a persisted auth redirect still falsifies later browser navigation.** `GET /api/prism/downstream-demo/seed-contract-ready` remained authoritative for Umbraco route convergence, but the browser could still be bounced from `/my-workflows` to `/dashboard` until the auth cookie stopped carrying stale redirect state. Treat that as a separate auth-session leak layered on top of the startup contract, not as proof the seed probe is wrong.

## Learnings (2026-04-14 — Restart auth recovery and offline_access scope strategy)

- **The restart auth recovery was already implemented correctly in working-tree changes.** The fix required three coordinated pieces: (1) PrismContext.ShouldRefreshForRuntimeRestart() detects when IssuedUtc < ProcessStartedUtc and forces a token refresh, (2) PrismOidcConfiguration.GetRefreshScope() returns null for the localhost demo tenant (signaling "omit scope parameter, use original scopes from initial login"), and (3) PrismOidcConfiguration.OnAuthorizationCodeReceived sets IssuedUtc = DateTimeOffset.UtcNow on the auth properties before persisting the cookie, ensuring future runtimes can detect the pre-restart session.
- **Generic OIDC tenants should NOT request offline_access by default.** The repo-owned localhost demo (localhost:8443/realms/prism-dev) is special-cased to request "openid profile offline_access" for restart-tolerant demos, but other generic OIDC tenants default to "openid profile" only (standard browser session scopes). This prevents production tenants from accidentally requesting long-lived refresh tokens without explicit product requirements and provider-side authorization.
- **Keycloak refresh token calls should omit the scope parameter entirely when using tokens issued with offline_access.** When the initial login included offline_access, the refresh_token grant should not restate scopes — Keycloak uses the original scopes bound to that refresh token. Sending scope=openid profile on refresh (without offline_access) can cause Keycloak to reject the call. The correct fix is GetRefreshScope() returning null for localhost demo, which PrismContext converts to an empty string, which then skips adding scope to the form parameters.
- **Pre-existing Phase1SecurityRegressionTests failures are unrelated to this work.** The AccountController_Login_RejectsExternalRedirect tests expect an InvalidOperationException to be thrown when calling LocalRedirect() with an external URL, but the test setup creates an unauthenticated principal, so the controller returns Challenge() instead of entering the LocalRedirect() branch. These tests were failing before the working-tree changes and remain failing after — they need separate investigation/correction.
- **The full localhost auth suite (8 tests) now passes, including the restart test.** All Playwright contracts pass: sign-in flow, API call, My Workflows navigation, seeded workflow page, dashboard navigation, restart + API call, sign-out, and restart + sign-out. The Core unit tests (PrismContextTests, PrismOidcConfigurationTests) also pass (26 tests total).
