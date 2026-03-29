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
