# Workflow Editor V1 — Umbraco Integration

**Date:** 2026-05-16  
**Author:** Brewster (Umbraco Platform Specialist)  
**Status:** Proposed  
**Relates to:** `docs/design/workflow-editor-v1/README.md` (three-plane architecture, Tom Nook)

---

## 1. Purpose & Scope

This section owns how the V1 workflow editor and its projected workflow definitions live _inside_ Umbraco v17 and across the three runtime surfaces — public, member, and business-app/back-stage. It does not restate the three-plane (Authoring → Projection → Agent) architecture; that belongs to the README. It defines the Umbraco-specific responsibilities: Document Types, route-hijacking controllers, backoffice hosting, auth/roles enforcement, and the constraints that keep the TestSite's existing contract intact as editor work lands.

---

## 2. Surface Model

The same projected workflow definition drives three distinct runtime surfaces. Each surface maps onto the same authored workflow through a different entry point and authentication context.

```
┌─────────────────────────────────────────────────────────────────┐
│            Authored workflow (editor → projection)              │
│         WorkflowDefinitionFile  (runtime target contract)       │
└────────────────┬────────────────┬───────────────────────────────┘
                 │                │                        │
        PUBLIC SURFACE   MEMBER SURFACE         BACK-STAGE SURFACE
        (Umbraco CMS)    (Umbraco CMS)          (MockBusinessApp)
```

### 2.1 Public Surface

The public surface hosts unauthenticated or pre-login entry points: service explainer pages, calls to action, and initiation links. These are plain Umbraco content nodes authored in the tree.

- **Document type:** a new `workflowLanding` doctype (V1) or an existing generic page type used as an explainer shell.
- **Route:** content-owned, resolved by Umbraco's content router. No route hijacking is needed if the page carries no workflow interaction itself.
- **Template:** `@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage<ContentModels.WorkflowLanding>` (or the generic page model). No raw POST processing on this surface.
- **Navigation:** links to the protected `workflowPage` node for the specific workflow, resolved from `Model.Children` or a picker-driven URL. **No hardcoded route strings.**
- **Authentication:** anonymous. The link to the protected `workflowPage` triggers a challenge at the member surface boundary.

### 2.2 Member Surface

The member surface hosts authenticated workflow initiation, step progression, and the dashboard hub. It is the canonical Prism workflow integration point.

- **Document types:** `workflowPage` (single workflow entry) and `workflowHub` (instance list). Both are seeded by `PrismContentTypeSeeder` and **must not be redefined in TestSite**.
- **Route hijacking:** `WorkflowPageController` (TestSite-derived) and `WorkflowHubController` (Core) intercept requests. Both inherit from `RenderController`. Surface Controllers are prohibited.
- **Auth:** `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` on both controllers. Unauthenticated requests receive a challenge redirect.
- **Templates:** owned by `UmbracoPrism.Core` as embedded resources. The TestSite **must not** provide local override views for `workflowPage.cshtml` or `workflowHub.cshtml` using the raw published model. `TestSiteViewModelBindingTests` enforces this contract (see §8).
- **ViewModel binding:** the controller returns a typed `WorkflowViewModel` (or `WorkflowHubViewModel`). The embedded Core template uses `@inherits UmbracoViewPage<WorkflowViewModel>`, not the raw `ContentModels.WorkflowPage`.

### 2.3 Business-App / Back-Stage Surface

The back-stage surface is the reviewer and role-gated processing layer. It runs as a separate host (`src/UmbracoPrism.MockBusinessApp/`) and does **not** participate in the Umbraco content tree.

- **Same projected file:** the `WorkflowDefinitionFile` emitted by the projection plane is consumed by MockBusinessApp's workflow engine unchanged.
- **Role-gated handlers:** transitions marked `requiresRole: "reviewer"` are only advanceable by authenticated business-app users with the reviewer role. The Umbraco `PrismMemberCookie` scheme plays no role on this surface.
- **Reviewer UI:** the existing `/admin/workflow` admin panel (JSON-first, instance-first) remains the V1 reviewer surface. The workflow editor is a separate concern (§5).
- **TUI service:** retained as-is for V1. No changes to the MockBusinessApp runtime services are required by the editor work alone.

---

## 3. Document Type Design

### 3.1 Existing Shell Document Types (confirm, do not modify)

| Alias | Purpose | `workflowKey` property |
|---|---|---|
| `workflowPage` | Single workflow journey entry | ✅ Yes — bridges content node to workflow definition |
| `workflowHub` | Member instance list and resume | ❌ No — hub is key-agnostic |

Both are seeded by `PrismContentTypeSeeder`. They are **stable** V1 shell contracts. The only permitted TestSite change is to add new child content nodes under an existing workflow root; the aliases themselves must not be modified.

### 3.2 New Document Type: `workflowLanding` (V1 addition)

For the planning-application reference demo, V1 needs a public explainer shell:

| Property | Alias | Type |
|---|---|---|
| Page title | `pageTitle` | Textstring |
| Intro text | `introText` | Richtext |
| CTA label | `ctaLabel` | Textstring |
| Linked workflow page | `linkedWorkflowPage` | Content picker (workflowPage only) |

The linked workflow page picker resolves the protected `workflowPage` URL without hardcoding strings. Navigation to the protected surface uses `Model.Value<IPublishedContent>("linkedWorkflowPage")?.Url()` in the template.

No route-hijacking controller is required unless business logic demands pre-population. If a controller is added, it must extend `RenderController` and must not inherit from `SurfaceController`.

### 3.3 New Document Type: `workflowRegistry` (V1 addition — optional)

If the projected workflow definitions are referenced by content nodes (e.g., content editors need to pick a workflow key from a list rather than typing a string), a `workflowRegistry` singleton node provides a managed list:

| Property | Alias | Type |
|---|---|---|
| Registered workflows | `registeredWorkflows` | Repeatable text / Block List |
| — Workflow key | `key` | Textstring |
| — Display name | `displayName` | Textstring |
| — Description | `description` | Textarea |

This node lives as a singleton child of the Settings node. A data type backed by `workflowRegistry` entries can drive a dropdown picker on `workflowPage`, replacing the free-text `workflowKey` field. **V1 can defer this** — the free-text `workflowKey` is sufficient when editors know the projection output.

### 3.4 Editor-Specific Document Types

The workflow editor itself does **not** live as a Document Type or content node. It is hosted in the backoffice (§5). No Umbraco content tree node represents the editor application.

---

## 4. Route Hijacking & Controllers

V1 requires the following route-hijacking controllers. All extend `RenderController`. No `SurfaceController` subclasses are permitted anywhere in the workflow path.

### 4.1 `WorkflowPageController` — TestSite

```csharp
// src/UmbracoPrism.TestSite/Controllers/WorkflowPageController.cs
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class WorkflowPageController(...)
    : PrismWorkflowPageController<WorkflowViewModel>(...)
```

- **Hijacks:** `workflowPage` document type (matched by class name convention)
- **Base:** `PrismWorkflowPageController<TViewModel>` (Core) → `RenderController`
- **Template:** Core-embedded `workflowPage` view; TestSite must not provide a local override with the raw published model
- **Pre-population:** overrides `PrePopulateFields()` to inject claims-sourced email and name into read-only fields
- **Status:** exists. No changes required for V1 editor work.

### 4.2 `WorkflowHubController` — Core

```csharp
// src/UmbracoPrism.Core/Controllers/WorkflowHubController.cs
[Authorize(AuthenticationSchemes = "PrismMemberCookie")]
public class WorkflowHubController : RenderController
```

- **Hijacks:** `workflowHub` document type
- **Base:** `RenderController`
- **Resolves:** workflow page URLs via `_publishedContentQuery.ContentAtRoot()` tree traversal — **not** hardcoded strings
- **Status:** exists. No changes required for V1 editor work.

### 4.3 `WorkflowLandingController` — TestSite (V1 addition, if needed)

```csharp
// src/UmbracoPrism.TestSite/Controllers/WorkflowLandingController.cs
// [No Authorize — public surface]
public class WorkflowLandingController : RenderController
```

- **Hijacks:** `workflowLanding` document type
- **Base:** `RenderController`
- **Responsibility:** resolve the CTA URL from the content-picker property and pass it to the template. Simple enough that this controller may be omitted entirely if the Razor template handles the picker inline.
- **Status:** new; add only if the template alone is insufficient.

### 4.4 No New Controllers for the Editor

The workflow editor (§5) is hosted in the Umbraco backoffice, not as a Razor view. It does not require a route-hijacking controller.

---

## 5. Editor Hosting Decision

**Recommendation: Option (c) Hybrid — backoffice section embeds the editor app.**

### Decision

Add a v17 backoffice section (`prism-workflow-editor`) declared via the Umbraco package manifest. The section shell is a Lit/Web Component (`<prism-workflow-editor-app>`). The component embeds or frames the editor projection tooling, which can also be run standalone (CLI, CI) without Umbraco.

### Rationale

| Concern | (a) Pure backoffice Lit/WC | (b) Separate admin app | **(c) Hybrid** |
|---|---|---|---|
| Discoverable for editors | ✅ | ❌ manual URL | ✅ |
| Umbraco auth reuse | ✅ | ❌ separate login | ✅ |
| Works standalone / CI | ❌ tied to CMS | ✅ | ✅ |
| Avoids duplicating Umbraco internals | ✅ | ✅ | ✅ |
| v17 non-negotiable rules (no AngularJS) | ✅ | n/a | ✅ |
| Projection tooling reuse | limited | full | full |

The hybrid model keeps the projection tooling host-agnostic (usable from the CLI for agent operations) while making the editor discoverable through the Umbraco backoffice for human editors. The Lit component in the section acts as a thin shell — it authenticates via the Umbraco backoffice session and loads the editor app, which communicates with the projection layer via a local API.

### Manifest skeleton (v17)

```typescript
// src/UmbracoPrism.Core/wwwroot/backoffice/prism-workflow-editor/manifest.ts
import type { ManifestSection } from '@umbraco-cms/backoffice/extension-registry';

export const manifests: ManifestSection[] = [
  {
    type: 'section',
    alias: 'Prism.WorkflowEditor',
    name: 'Workflow Editor',
    meta: { label: 'Workflows', pathname: 'prism-workflow-editor' },
    js: () => import('./index.js'),
  },
];
```

The entry web component (`<prism-workflow-editor-app>`) is a `LitElement` subclass. No AngularJS. No Surface Controllers.

### Trade-offs to watch

- The editor app must be independently deployable for CI/agent validation workflows — keep the projection API behind a clean HTTP contract, not a Umbraco-only DI dependency.
- Backoffice auth tokens must not be forwarded to the MockBusinessApp runtime. The editor modifies _authored_ definitions; the runtime surface uses its own auth (§6).

---

## 6. Auth & Roles

### Runtime surfaces (unchanged)

| Surface | Scheme | Who |
|---|---|---|
| Public | Anonymous | Anyone |
| Member | `PrismMemberCookie` | Authenticated members |
| Back-stage | Business-app role | Reviewer / caseworker |

### Authoring / editor surface (new)

| Action | Principal | Enforcement |
|---|---|---|
| Open editor, browse authored workflows | Umbraco backoffice user | Backoffice login (standard Umbraco auth) |
| Preview projected workflow (read-only) | Any authenticated backoffice user | Section access in manifest |
| Edit / create authored workflow | Backoffice user | Umbraco user group permission on the `Prism.WorkflowEditor` section |
| Promote / publish projected file to live | Backoffice admin with `workflow-publisher` capability | Explicit permission check in the projection API before writing the projected file |

**Violation risk:** If the projection API endpoint is callable by any authenticated Umbraco user without checking capability, a non-admin editor could promote a definition to live. The `workflow-publisher` capability check must sit in the projection API layer (Core or a dedicated projection service), not solely in the Lit component.

---

## 7. Content Tree Navigation Rules

**V1 rule — non-negotiable for all new code in this delivery:**

- Navigate the content tree using `Model.Children`, `Model.Parent`, `Umbraco.ContentAtRoot()`, `_publishedContentQuery.ContentAtRoot()`, or property-picker resolved models.
- Do **not** hardcode URL strings (e.g., `"/apply/planning-notification"`) anywhere in controllers, views, or services.
- Do **not** use `Umbraco.TypedContent(id)` with hardcoded integer IDs.
- Resolve `workflowPage` URLs by traversing the tree filtered on `ContentType.Alias == "workflowPage"` and matching `WorkflowKey` (see `WorkflowHubController.ResolveWorkflowPageUrl()`).
- If a URL cannot be resolved via the tree, log a warning and fall back to the hub page URL — do not silently return `/` with no logging.

This rule applies equally to the `WorkflowLandingController` (if added) and to any future agent-plane redirect helpers.

---

## 8. Coexistence with Existing Views

`TestSiteViewModelBindingTests` enforces two contracts for `workflowPage` and `workflowHub`:

1. `TestSite_MustNotOverride_CoreOwnedViews` — the TestSite **must not** have local `Views/workflowPage.cshtml` or `Views/workflowHub.cshtml` files. The canonical view is an embedded resource in `UmbracoPrism.Core`.
2. `TestSite_IfViewExists_MustNotInheritRawPublishedModel` — if a file somehow exists, its `@inherits` directive must not name `ContentModels.WorkflowPage` or `ContentModels.WorkflowHub`.

**Current state:** stub view files at `src/UmbracoPrism.TestSite/Views/workflowPage.cshtml` and `src/UmbracoPrism.TestSite/Views/workflowHub.cshtml` currently exist and inherit the raw published model. These are test contract violations that predate this document. Removing them is a prerequisite for V1 landing cleanly — coordinate with `TestSiteViewModelBindingTests` before any editor-related TestSite changes ship.

**Rule for V1:** no new local TestSite view overrides for Core-owned document type aliases. New document types (`workflowLanding`) may have TestSite-local views freely, because Core does not own those aliases.

---

## 9. MockBusinessApp & Back-Stage Integration

The MockBusinessApp (`src/UmbracoPrism.MockBusinessApp/`) is the V1 reviewer surface. It is a separate host with its own URL (port 7245 in the Aspire stack) and its own authentication scheme.

### Consuming the projected file

The projected `WorkflowDefinitionFile` is consumed by the MockBusinessApp's workflow engine at seed time from `workflow-seeds/*.json`. For V1, the projection plane writes to this directory (or a configured output path). The MockBusinessApp reads seeds on startup; no live reload mechanism is required in V1.

Workflow-seed file location: `src/UmbracoPrism.MockBusinessApp/workflow-seeds/{workflow-key}.json`

### Reviewer / role-gated handlers

Transitions with `requiresRole: "reviewer"` are only advanceable by POST requests from an authenticated business-app session carrying the reviewer role. The Umbraco `PrismMemberCookie` is not forwarded; the two auth systems remain isolated.

The reviewer UI (existing `/admin/workflow` panel) renders workflow instance state and exposes approve/reject actions. No changes to the existing TUI service or admin panel are required by editor work in V1.

### Back-stage as a surface for the planning-application demo

The planning-application reference workflow maps to:

| Stage | Surface | Entry point |
|---|---|---|
| Public discovery | Umbraco `workflowLanding` node | `/planning/apply` (content-authored URL) |
| Citizen application | Umbraco `workflowPage` node (`workflowKey: "planning-notification"`) | `/planning/apply/start` |
| Citizen status | Umbraco `workflowHub` | `/my-workflows` |
| Caseworker review | MockBusinessApp `/admin/workflow` | Port 7245 (Aspire stack) |

---

## 10. Open Questions

1. **Multi-site / multi-tenant editor** — if multiple Umbraco sites (tenants) run against the same Prism Core deployment, should each tenant have its own backoffice section instance and projected file namespace, or is the editor global? V1 assumes a single-tenant deployment. Multi-tenancy requires a tenant discriminator in the `WorkflowDefinitionFile` path and projection API, and a per-tenant section or filtered view in the backoffice.

2. **Editor as Umbraco package vs Prism-Core admin app** — the hybrid model (§5) positions the editor inside Core's backoffice extension. An alternative is shipping the editor as a standalone Umbraco package (`UmbracoPrism.WorkflowEditor.csproj`) that is installed separately. This is cleaner for adopters who do not want the editor in production but is more complex to develop and release. Defer to V2 package extraction.

3. **Projection output transport** — V1 writes projected files to the MockBusinessApp seed directory on disk. For production use, the projection API should POST directly to the business app's workflow API to register/update definitions without requiring a restart. The seed-on-disk model is sufficient for the reference demo.

4. **Live preview** — can an editor preview a projected workflow on the member surface before promoting? This requires either a sandboxed workflow engine or a read-only simulation mode. Deferred from V1.

---

## 11. Acceptance Hooks

The following TestSite and MockBusinessApp changes constitute V1 deliverables, in priority order:

| Priority | Change | File(s) | Notes |
|---|---|---|---|
| 1 | Remove stub view files that violate `TestSiteViewModelBindingTests` | `src/UmbracoPrism.TestSite/Views/workflowPage.cshtml`<br>`src/UmbracoPrism.TestSite/Views/workflowHub.cshtml` | Delete files; Core embedded views take over. Prerequisite for all other TestSite work. |
| 2 | Add `workflowLanding` Document Type to `PrismContentTypeSeeder` or a TestSite-local seeder | `src/UmbracoPrism.Core/PrismContentTypeSeeder.cs` or `src/UmbracoPrism.TestSite/WorkflowPageSeeder.cs` | Needed for the planning-application public shell. |
| 3 | Add planning-application content nodes in the TestSite seeder | `src/UmbracoPrism.TestSite/WorkflowPageSeeder.cs` | `workflowLanding` node + `workflowPage` node with `workflowKey: "planning-notification"` |
| 4 | Add `WorkflowLandingController` (if template logic requires it) | `src/UmbracoPrism.TestSite/Controllers/WorkflowLandingController.cs` | Extends `RenderController`; no `[Authorize]`; public surface. |
| 5 | Add `planning-notification.json` projected seed to MockBusinessApp | `src/UmbracoPrism.MockBusinessApp/workflow-seeds/planning-notification.json` | Defines states, transitions, and roles for the planning demo. |
| 6 | Scaffold the backoffice extension manifest | `src/UmbracoPrism.Core/wwwroot/backoffice/prism-workflow-editor/manifest.ts` | v17 section manifest; empty `<prism-workflow-editor-app>` Lit element as placeholder. |
| 7 | Register the backoffice extension in the Umbraco composition | `src/UmbracoPrism.Core/Extensions/` or `UmbracoBuilder` composition | `AddBackOfficeExternalLoginProvider` / `WithManifest` per v17 package API. |
| 8 | Add `workflowRegistry` DocType and singleton node (if workflow-key picker is needed) | `src/UmbracoPrism.Core/PrismContentTypeSeeder.cs` | Can be deferred if free-text `workflowKey` is sufficient for V1. |

---

*This document is scoped to the Umbraco integration layer. The projection plane contract, editor domain model, and agent API are covered in sibling sections of this design.*

---

## Appendix A — V1 Implementation Status (2026-05-16)

### Shipped files

| File | Purpose |
|---|---|
| `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/umbraco-package.json` | Umbraco v17 package manifest — declares all five extensions (section, sidebarApp, menu, menuItem, dashboard) |
| `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/web-components/prism-workflow-editor-host.js` | Lit element (`<prism-workflow-editor-host>`) — thin iframe wrapper for `workflow-editor.html?workflow=planning` |
| `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/README.md` | Developer guide: enabling the section, configuring `authoringBaseUrl`, no-build-step explanation |
| `src/UmbracoPrism.Core.Tests/WorkflowEditorManifestTests.cs` | File-shape assertions: manifest exists, parses as JSON, contains section alias and dashboard element name |

### Extensions declared

| Extension type | Alias | Purpose |
|---|---|---|
| `section` | `Umb.Section.PrismWorkflowEditor` | Adds "Workflow Editor" tab to the backoffice nav bar |
| `sectionSidebarApp` (kind: menu) | `Umb.SidebarApp.PrismWorkflowEditor` | Sidebar scoped to the section |
| `menu` | `Umb.Menu.PrismWorkflowEditor` | Menu container |
| `menuItem` | `Umb.MenuItem.PrismWorkflowEditor.PlanningApplication` | "Planning Application" — V1's single workflow entry point |
| `dashboard` | `Umb.Dashboard.PrismWorkflowEditor` | Renders `<prism-workflow-editor-host>` in the main content pane |

### Enabling the section in a fresh install

1. Log in to the Umbraco backoffice.
2. Go to **Settings → Users → User Groups → Administrators**.
3. Under **Allowed Sections**, enable **"Workflow Editor"**.
4. Save. The section appears in the nav bar on the next page load.

### Authoring base URL (dev)

The Lit element defaults to `https://localhost:7245` (Blathers' MockBusinessApp dev server). To override at runtime without a rebuild:

```js
// Browser console or injected script block:
window.PrismWorkflowEditorConfig = { authoringBaseUrl: 'https://localhost:7245' };
```

If the authoring server is unreachable, the dashboard shows a friendly "Editor not yet built" message rather than a browser error.

### Build approach

No separate build step is required. Umbraco v17 resolves `@umbraco-cms/backoffice/*` bare specifiers via its built-in import map. The Lit element is plain ESM JS loaded directly from `App_Plugins/`.

### Acceptance hook status (§11 reference)

Priority 6 (backoffice extension manifest) from §11 is **complete** via `App_Plugins/PrismWorkflowEditor/`. The location was changed from the spec'd `src/UmbracoPrism.Core/wwwroot/backoffice/…` to `src/UmbracoPrism.TestSite/App_Plugins/…` — the TestSite location is loaded automatically by Umbraco without explicit composition registration, which is simpler and conventional for App_Plugins. Priority 7 (manual composition registration) is therefore **not required**.
