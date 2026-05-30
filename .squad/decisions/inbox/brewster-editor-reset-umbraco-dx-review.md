---
author: brewster
date: 2026-05-30T13:00:00+01:00
status: proposed
area: workflow-editor
confidence: high
scope: review-only
---

# Workflow Editor Umbraco DX Review

## DX verdict

A competent Umbraco v17 integrator can stand the editor up — but only by following the *TestSite shape* almost exactly, because nothing in the codebase calls out the integrator-facing API as distinct from the demo wiring. The reset has materially improved things on the backend (single `AddPrismWorkflowEditor` + `MapPrismWorkflowEditor`, gateway-only model, clean route prefix), but the front-end story is still "embed an iframe pointing at the Business App" rather than "drop a web component into your backoffice", and there is no public/internal boundary on the Lit components. Net direction since the reset is positive on the backend, neutral on the front end — embedding the editor as an Umbraco-native web component, rather than an iframed app, is the next big DX cliff to climb.

## DX findings

### Backoffice integration

- **SHOULD-FIX** — Editor mounted as an **iframe**, not a web component — `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/web-components/prism-workflow-editor-host.js:121-125` — The Umbraco v17 dashboard renders `<iframe src="https://localhost:7245/workflow-editor">`. — **An integrator now has to deploy MockBusinessApp (or a clone of it) as a *second* origin to host the editor, plus configure CORS, plus deal with iframe sandbox/cookies.** The v17 manifest is correct (Lit + `UmbLitElement`), so we are paying the v17 cost without taking the v17 win. — Render `<prism-workflow-editor workflow-key="…" authoring-api-base="…">` directly inside the dashboard element, importing the compiled bundle from `App_Plugins`. The iframe pattern stays as a fallback only.

- **SHOULD-FIX** — Hard-coded dev host URL in the dashboard host — `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/web-components/prism-workflow-editor-host.js:13-16` — `getAuthoringBaseUrl()` defaults to `https://localhost:7245`. — Any integrator who is not Jonny has to edit JavaScript inside `App_Plugins` to point at their own API. — Read from a manifest `meta` value or an Umbraco-backed config endpoint instead of a literal in JS.

- **SHOULD-FIX** — Backoffice manifest lives in the TestSite, not in a distributable — `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/umbraco-package.json` — Anyone consuming Prism gets the manifest only by copying TestSite. — Move the App_Plugins payload into `UmbracoPrism.WorkflowEditor` and ship it as a content file (e.g. `staticwebassets` or `App_Plugins/PrismWorkflowEditor` packed into the NuGet) so it lights up on `dotnet add package`.

- **WORTH-NOTING** — The menu item set is **hardcoded** to `Planning Application` — `umbraco-package.json:39-46` — The `/api/workflow-authoring/workflows` endpoint already lists every authored workflow; the sidebar menu should be data-driven so adding a workflow in the editor adds a sidebar item, not require a manifest edit.

- **WORTH-NOTING** — No `umbraco-package-schema.json` reference for the App_Plugins manifest — `umbraco-package.json:2` points to `../../umbraco-package-schema.json` which only exists in TestSite, not in the shipped product. Breaks IntelliSense for integrators outside this repo.

### Test site / public-facing rendering

- **SHOULD-FIX** — No example of rendering a **published, read-only authored workflow** in a public Razor view — `src/UmbracoPrism.TestSite/Views/` only contains runtime forms (`workflowPage.cshtml`, `workflowHub.cshtml`). — Integrators who want a "what does this workflow look like" public diagram (citizen-facing process map, a service-design page, etc.) have no recipe — they would have to discover that `<prism-workflow-graph>` exists, then realise its `workflow` prop is `attribute: false` and *cannot* be set from Razor markup. — Add a small route-hijacked Razor page (e.g. `workflowDiagramPage.cshtml`) that fetches the published JSON server-side and bootstraps `<prism-workflow-graph>` via inline JSON + a tiny init script.

- **WORTH-NOTING** — `WorkflowHubController.ResolveWorkflowPageUrl` walks `_publishedContentQuery.ContentAtRoot().DescendantsOrSelf()` on every hub render — `src/UmbracoPrism.Core/Controllers/WorkflowHubController.cs:97-104` — Content-driven (good — no hardcoded routes), but a full-tree descendant scan per request scales poorly on larger Umbraco sites. Cache by `workflowKey` or replace with an `IPublishedContentCache` lookup keyed on a known root.

- **WORTH-NOTING** — `ReferenceWorkflowRepository` is a **static** class hardcoding four workflows — `src/UmbracoPrism.MockBusinessApp/Services/ReferenceWorkflowRepository.cs:11-26` — Useful as a demo but it is the only thing showing an integrator the shape of "your own workflow store". The pattern an integrator should follow is `IAuthoredWorkflowStore`, not this static helper; that hand-off is undocumented.

### Component public API

- **SHOULD-FIX** — No public/internal distinction on the 11 `<prism-…>` custom elements — `src/UmbracoPrism.Client/src/workflow-editor/*.ts` defines `prism-workflow-editor-shell`, `prism-workflow-editor`, `prism-workflow-graph`, `prism-step-inspector`, `prism-confidence-tabs`, `prism-help-panel`, `prism-stage-preview`, `prism-workflow-simulation`, `prism-workflow-outline`, `prism-workflow-action-editor`, `prism-inline-help`. — Integrators don't know which are safe to consume directly. A future refactor will silently break consumers of internal elements. — Add a `README.md` under `src/UmbracoPrism.Client/src/workflow-editor/` declaring `prism-workflow-editor` (full editor), `prism-workflow-editor-shell` (host harness), and `prism-workflow-graph` (read-only viewer) as the public surface; mark every other class JSDoc with `@internal`.

- **BLOCKER-FOR-READ-ONLY-USE** — `<prism-workflow-graph>` cannot be initialised from HTML alone — `src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts:181` declares `workflow` with `attribute: false`. — Razor integrators cannot do `<prism-workflow-graph workflow='@Html.Raw(json)'>`. They need JS glue to assign the property. — Accept a JSON attribute (`workflow-json`) that internally parses to the typed model, in addition to the prop. Mirrors how Umbraco's own Lit elements expose data.

- **WORTH-NOTING** — `<prism-workflow-editor>` wiring contract is reasonable (`workflow-key` + optional `authoring-api-base` + optional `approver-name`, no required event listeners) but the **self-fetch behaviour is the only mode** — `prism-workflow-editor.ts:140-156`. There is no "controlled" mode where a host supplies the workflow and intercepts saves. Limits embedding inside Umbraco where the host might want to gate saves through a property editor.

- **WORTH-NOTING** — Element JSDoc references the wrong layout ("Left — graph; Right — inspector") and stage list inside `prism-workflow-editor.ts:125-138` — pre-reset language; the layout is now lane-columned vertical. Drift between code-comments and the post-reset visual contract.

### Backend SDK / DI / endpoints

- **SHOULD-FIX** — `IWorkflowPublishService.PreviewAsync` and `PublishPreviewResult` survive the reset — `src/UmbracoPrism.WorkflowEditor/Authoring/IWorkflowPublishService.cs:8`, `WorkflowPublishService.cs:12`, `PublishPreviewResult.cs:8` — The reset (`.squad/decisions.md` "Workflow editor scope reset") explicitly removes the preview endpoint, but the *interface* still publishes it. — Integrators registering a custom `IWorkflowPublishService` will be forced to implement a method that no caller invokes. Either delete `PreviewAsync` from the interface, or replace `PublishResult : PublishPreviewResult` inheritance with a plain record and drop the preview type.

- **SHOULD-FIX** — `MapPrismWorkflowEditor` silently depends on a named CORS policy — `src/UmbracoPrism.WorkflowEditor/Extensions/WorkflowEditorEndpointExtensions.cs:43-46` requires a policy literally called `"WorkflowAuthoringDevCors"` in Development. — An integrator who calls `MapPrismWorkflowEditor()` without first calling `services.AddCors(opt => opt.AddPolicy("WorkflowAuthoringDevCors", …))` will get a runtime exception. The name is invisible from the public method signature. — Either own the policy from inside `AddPrismWorkflowEditor` (register a default policy), or accept the policy name as a parameter on `MapPrismWorkflowEditor(corsPolicyName: …)`.

- **SHOULD-FIX** — `AddPrismWorkflowEditor(authoredWorkflowBasePath: string.Empty, …)` is a sentinel-driven API — `src/UmbracoPrism.MockBusinessApp/Program.cs:47` passes `string.Empty` because MBA pre-registers its own `IAuthoredWorkflowStore`. The empty path is then still passed into `FilesystemAuthoredWorkflowStore` via `TryAddSingleton`, which only no-ops because the registration is already there. — Confusing. Split into two overloads: `AddPrismWorkflowEditor()` (caller supplies `IAuthoredWorkflowStore` / `IPublishedWorkflowStore`) and `AddPrismWorkflowEditorFilesystemStores(authoredPath, publishedPath?)`.

- **WORTH-NOTING** — `/apply` endpoint and the `ProposalEnvelope` apply protocol survive but are undocumented as the canonical save path — `WorkflowEditorEndpointExtensions.cs:202-249`. The decision log says "keep `ProposalEnvelope` as the apply protocol but drop the preview endpoint" — the code matches, but an integrator reading endpoint names will see both `/save` (POST whole workflow) and `/apply` (POST envelope) and have no idea which is the supported entry point.

- **WORTH-NOTING** — Authoring endpoints are discoverable (`/api/workflow-authoring/...` group), but `MapPrismWorkflowEditor` is named "Editor" while the endpoints are named "WorkflowAuthoring" — `WorkflowEditorEndpointExtensions.cs:38`. Minor, but a `grep` for "Editor" misses the routes.

### Documentation

- **SHOULD-FIX** — `docs/walkthroughs/authoring-a-workflow.md` and `docs/walkthroughs/planning-workflow-editor.md` are **editor-UX walkthroughs**, not Umbraco integration recipes. — Neither mentions `AddPrismWorkflowEditor()`, `MapPrismWorkflowEditor()`, `App_Plugins/PrismWorkflowEditor/umbraco-package.json`, or `IAuthoredWorkflowStore`. An Umbraco v17 dev landing on these docs cannot extract "how do I host this in *my* site".

- **SHOULD-FIX** — Step order in `authoring-a-workflow.md` is **editor-first**, not Umbraco-idiomatic. — A v17 integrator expects: (1) install package / NuGet, (2) compose `IUmbracoBuilder` and register services, (3) declare doctypes (`workflowPage`, `workflowHub`), (4) route-hijack with `PrismWorkflowPageController<T>`, (5) wire Razor views, (6) drop App_Plugins manifest, (7) finally open the editor. The current doc starts at step 7.

- **WORTH-NOTING** — `planning-workflow-editor.md:11-13` still references the editor as something the *operator* uses inside MockBusinessApp's `/workflow-editor` URL, not inside the Umbraco backoffice section. The backoffice integration story is invisible to docs.

- **WORTH-NOTING** — `planning-workflow-editor.md` mentions the "external MCP client" handling agent chat — post-reset the agentic surfaces are paused; this line will read as a current product feature to a fresh reader.

### Cross-cutting Umbraco patterns

- **SHOULD-FIX** — Workflow controllers don't pin to the `PrismMemberCookie` scheme — `src/UmbracoPrism.Core/Controllers/PrismWorkflowPageController.cs:87` and `WorkflowHubController.cs:42` both check `User.Identity?.IsAuthenticated` and redirect manually instead of using `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` (the pattern enforced by `BiometricController.cs:32`). — Works on TestSite because PrismMemberCookie is the de-facto default, but any integrator with a second auth scheme (multiple member apps, IdentityServer, etc.) will pick up the wrong principal silently and treat a backoffice/Identity user as the "member who submitted this workflow". — Add the explicit attribute or accept `authenticationScheme` as a constructor injection point.

- **WORTH-NOTING** — `WorkflowHubController` correctly uses `IPublishedContent` discovery (`ContentAtRoot().DescendantsOrSelf().FirstOrDefault(...)`) to resolve workflow page URLs — no hardcoded routes ✅. Confirms the pattern works under arbitrary content trees.

- **WORTH-NOTING** — CORS only "works" because the iframe origin and the API origin are the same MockBusinessApp host. If an integrator embeds the web component directly (the recommended fix above), MBA-style `AllowAnyOrigin` CORS becomes essential and there is no documented production CORS policy. Today the editor and the API silently share an origin.

- **WORTH-NOTING** — `umbraco-package.json` sets `"allowPublicAccess": false` and the dashboard condition is scoped to `Umb.Section.PrismWorkflowEditor`. Good v17 hygiene — section-scoped, no public exposure.

## Recipe smell test

- **Embed the editor in a backoffice section** — **😐** — The manifest works and Umbraco v17 recognises it, but the dashboard hosts an iframe to a second .NET process. An integrator gets a *section*, not an *editor*, without standing up MockBusinessApp.
- **Render a read-only published workflow in a public Razor view** — **💀** — `<prism-workflow-graph>`'s `workflow` is `attribute: false`, no `workflow-json` accessor; the only route-hijacked Razor surface (`workflowPage.cshtml`) renders runtime forms, not the authored graph. No existing recipe.
- **Authorize a member to submit a workflow** — **❤️** — Works today via the `PrismMemberCookie`-backed default scheme, route-hijacked `WorkflowPageController` extending `PrismWorkflowPageController<T>`, with `_workflowClient` carrying the member's identity through. Add explicit `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]` and it becomes bulletproof.

## Top-3 DX wins worth a slice

1. **Mount the editor as a native v17 web component, not an iframe.** Ship the compiled `<prism-workflow-editor>` bundle inside `UmbracoPrism.WorkflowEditor` as static web assets; have the dashboard host import and render the element directly, with `authoring-api-base` resolved from configuration. Removes the need to deploy MockBusinessApp at all and turns the section from "iframed app" into "Umbraco section".
2. **Expose a read-only `<prism-workflow-graph workflow-json="…">` and ship a Razor recipe.** One Razor partial that takes a published-workflow JSON blob and renders the graph read-only would unblock service-design, citizen-facing process pages, and "preview before publish" use cases. Coupled with declaring the three public elements (`-editor`, `-editor-shell`, `-graph`) in a `src/UmbracoPrism.Client/src/workflow-editor/README.md`.
3. **Make the backend SDK self-contained.** Split `AddPrismWorkflowEditor` into store-providing vs filesystem-default overloads, fold the `WorkflowAuthoringDevCors` policy into `AddPrismWorkflowEditor` (with a `corsPolicyName` override), and prune `IWorkflowPublishService.PreviewAsync` + `PublishPreviewResult` to remove the post-reset dead surface. An integrator's `Program.cs` collapses to two lines: `services.AddPrismWorkflowEditor(store)` and `app.MapPrismWorkflowEditor()`.
