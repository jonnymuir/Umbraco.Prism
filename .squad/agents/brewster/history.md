## 2026-05-31 — Slice B: Test-infra refit + contract test rewrite for `/mockapp/workflows/*`

**Session:** named-lanes editor — Slice B (DDD boundary, test cut)  
**Branch:** `squad/82-named-lanes-editor-slice`

**Outcomes:**
- ✅ Created `AuthoredWorkflowFixtureLoader` (static test helper, `Workflow/Authoring/`) — `LoadAsync(basePath, key)` / `ListKeys(basePath)`. Replaces the deleted `FilesystemAuthoredWorkflowStore` for tests that only need to read fixture JSON.
- ✅ Migrated six test files to use the loader: `AuthoredWorkflowSerializationTests`, `StartupWorkflowPublishingTests`, `WorkflowPatchServiceTests`, `WorkflowPatchServiceFailureTests`, `WorkflowProjectorShellInferenceTests`, `WorkflowProjectorDeterminismTests`, plus the moved-and-renamespaced `Workflow/Publishing/WorkflowPublishServiceTests`.
- ✅ Dropped three implementation-mirror tests in `AuthoredWorkflowSerializationTests` (`FilesystemStore_ListKeys_ReturnsFixtureKey`, `FilesystemStore_ListAsync_PreservesWorkflowKeySeparatelyFromDefinitionKey`, `FilesystemStore_ReturnsNull_ForMissingKey`) — all asserted on the deleted production class. Kept `FilesystemStore_LoadsFixtureDocument` and converted it to the new loader.
- ✅ Deleted four whole test files (all tested deleted production code): `WorkflowAuthoringEndpointsTests`, `WorkflowAuthoringEndpointSecurityTests`, `WorkflowAuthoringApplyRelaxationTests`, `InMemoryAuthoredWorkflowStoreTests`.
- ✅ Rewrote `FourWorkflowReferenceContractTests.cs` against the new `/mockapp/workflows/*` endpoints, with a new anonymous `MockBusinessAppWebFactory` (in-file) replacing the deleted `WorkflowAuthoringWebFactory` + `TestUserHeaderAuthHandler` infra.
- ✅ Validated: `dotnet test UmbracoPrism.sln` → 814 passed / 0 failed / 11 skipped (was 860 — 46 tests legitimately retired with their production classes; no surviving tests dropped).

**Peers:** Blathers (production deletions + endpoint rewrite + publish move), Isabelle (TS boundary + editor rewrite).

## 2026-05-16: Workflow Editor V1 Design Cycle

**Scope:** Five-agent orchestration for workflow editor design iteration  
**Outcome:** Complete V1 design with cross-cutting architecture, UX, runtime, integration, and agentic surfaces  
**Peers:** tom-nook, isabelle, blathers, brewster, tangy  
**Files:** docs/design/workflow-editor-v1/* (5 docs, ~145KB)  
**Decisions:** Merged to .squad/decisions.md  

### Contributions

- **Architecture** (tom-nook): Three-plane spine, cross-cutting contracts, planning-app reference
- **Authoring UX** (isabelle): 4 editor surfaces, WCAG 2.2 AA dual-mode, 10-component inventory
- **Runtime Projection** (blathers): AuthoredWorkflow model, 5-stage pipeline, JSON-Pointer patches
- **Umbraco Integration** (brewster): Hybrid editor hosting, v17 backoffice embedding, TestSite removal P1
- **Agentic Surfaces** (tangy): Proposal envelope, MCP+CLI, 4-level test seam, planning workflow spec

---

# Brewster — History

## Core Context

Umbraco v17 architecture, routing patterns, and workflow integration specialist.

**Key domains:** Umbraco 17 patterns, Route hijacking, Workflow/dashboard pages, Document type design, Auth flow validation

## 📋 Recent Sessions

---

## 2026-05-23T13:51:28+01:00 — Vinyl/Core Boundary Split

**Task:** Move vinyl-specific notification behaviour out of Core and into TestSite; delete the broken duplicate TestSite `PrismContentPublishedHandler`.

**Files moved to TestSite (`UmbracoPrism.TestSite.*`):**
- `Controllers/PrismVinylNotificationController.cs`
- `Controllers/Models/PrismVinylBackInStockRequest.cs`
- `BackgroundServices/LimitedEditionDropNotifier.cs`

**Deleted:**
- Core: above three files (originals)
- TestSite: `PrismContentPublishedHandler.cs` (duplicate, hardcoded alias, broken tenant)

**Wiring changes:**
- `PrismComposer`: removed `AddHostedService<LimitedEditionDropNotifier>()` and its using
- `TestSiteComposer`: added `AddHostedService<LimitedEditionDropNotifier>()`, removed duplicate `ContentPublishedNotification` handler registration

**Config:** `appsettings.json` now opts `vinylRecord` into `Prism:Notifications:NotifiableContentTypes`

**Tests updated (Core.Tests):** `PrismVinylNotificationSecurityTests` and `Phase1SecurityRegressionTests` now reference `UmbracoPrism.TestSite.Controllers.*` namespaces; 50 affected tests pass.

**Decision filed:** `.squad/decisions/inbox/brewster-vinyl-implementation.md`

---

## 2026-05-16T13:20:33 | Workflow Editor V1 — Umbraco Integration

**Editor hosting:** Hybrid model — v17 backoffice section (`prism-workflow-editor`) wrapping Lit/Web Component
**Surface mapping:** (1) Public: unauthenticated content shells, (2) Member: `PrismMemberCookie`-protected pages, (3) Back-stage: MockBusinessApp reviewer surface
**DocType strategy:** `workflowPage` and `workflowHub` stable Core-owned; V1 adds `workflowLanding`
**Auth boundary:** Umbraco backoffice gates editor, PrismMemberCookie gates member, MockBusinessApp role gates reviewer
**Key file:** `docs/design/workflow-editor-v1/03-umbraco-integration.md`

## 2026-05-16T23:17:22 | V1 Workflow Editor Backoffice Section Scaffold

**Files shipped:**
- `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/umbraco-package.json` — v17 package manifest
- `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/web-components/prism-workflow-editor-host.js` — Lit element
- `src/UmbracoPrism.Core.Tests/WorkflowEditorManifestTests.cs` — 4 file-shape assertions

**Manifest:** 5 extensions (section, sectionSidebarApp, menu, menuItem, dashboard)
**Dev base-URL:** Reads `window.PrismWorkflowEditorConfig?.authoringBaseUrl` → 4-second fetch probe for reachability
**No build step:** Umbraco v17 resolves `@umbraco-cms/backoffice/*` at runtime; plain .js ESM files load directly

---

**📚 Older sessions (pre-2026-05-10) archived to `history-archive.md` to keep active history under 15KB.**

## Learnings

- The strongest Umbraco reference pattern here is a **thin typed Razor wrapper** in TestSite that selects Core workflow shells and partials, while leaving nonce handling, field validation, and workflow progression in Core/Business App boundaries.
- Seeded workflow/member journeys read more like a real Umbraco site when `workflowPage` and `workflowHub` live under `Home` instead of as root nodes; route ownership stays content-driven without changing the public URLs.
- Exact workflow-key lookup is safer than alias fallback once multiple workflow pages exist; missing content should fall back to the expected route contract, not to whichever workflow page happens to be first in the tree.
- The live MockBusinessApp authored seed (`src/UmbracoPrism.MockBusinessApp/workflow-authored/planning.workflow.json`) is a separate contract from the backend fixture under `src/UmbracoPrism.Core.Tests/Workflow/Authoring/Fixtures/`; keep the live file non-empty and keyed to the shell route (`planning`) so `/workflow-editor` stays loadable.
- Long-running LiveAppHost startup belongs behind an explicit Playwright worker-fixture timeout; `src/UmbracoPrism.Client/tests/support/shared-app-host-fixture.ts` now allows 10 minutes so cold Aspire warmups do not die at the default 30-second fixture limit.
- The workflow editor browser client still expects `stageKey`/`displayName`/`kind` and `fromStage`/`toStage`/`action`, so `src/UmbracoPrism.Client/src/workflow-editor/workflow-authoring-client.ts` must normalize the canonical C# API shape (`key`/`title`/`type`, `source`/`target`/`trigger`) before rendering graph and inspector components.
- 2026-05-19T21:20:21.447+01:00 — The authoring host key (`planning`) and the authored/runtime `definitionKey` (`planning-application`) are different contracts; list, load, and save must round-trip on the host key or the admin screen and reference shell drift apart.

---

## Session 2026-05-18T12:17:12Z — Issue #57 Completion

**Outcome:** brewster completed assigned work on issue #57 publish pipeline.

**Status:** Green end-to-end

---

## Session 2026-05-18T21:48:37Z — Issue #71 Status Check

**Context:** Asked to continue work on issue #71 (Runtime: Enable workflow runtime in Umbraco surfaces).

**Finding:** All acceptance criteria already implemented and tested:
- Route hijacking controllers exist (`WorkflowPageController`, `WorkflowHubController`)
- `WorkflowRuntimeEngine` provides instance resolution, state management, transitions
- `PrismWorkflowPageController<T>` handles GET/POST/PRG pattern with nonce validation
- Member auth enforced via `PrismMemberCookie` scheme
- 782 unit tests pass (349 workflow-specific)
- 6 E2E scenarios in `workflow-gds-journey.spec.ts` verify full journey

**Action:** Closed issue #71 with completion documentation.

**Status:** Green — repo fully operational for workflow runtime in Umbraco public/member surfaces

## 2026-05-18: Issue #71 Approval

Issue #71 "Workflow Runtime in Umbraco Surfaces" has been marked **acceptance-complete** by Tangy (quality gate review). All seven acceptance criteria are satisfied:

✅ Workflow start page loads in Umbraco
✅ Forms render for first stage
✅ Submit creates instance and advances stage
✅ Back-stage visibility enforced
✅ Instance state persisted correctly
✅ Resume/dashboard works
✅ Tests for planning workflow through Umbraco

Backend test suite: 782/782 passing (no blockers). Issue ready for merge.

## Revision Handoff (2026-05-19)

Workflow editor shortcuts slice: Tangy final review complete. Blocker: admin definitions page missing 'Edit workflow' link. Isabelle assigned for revision cycle.

---

## 2026-05-19T18:16:08Z: Admin-Page Edit-Workflow Link — LOCKED OUT (Tangy Re-review Rejected)

**Status:** 🔴 Blocked

The admin-page edit-workflow slice has been re-reviewed and rejected by Tangy. The blocker is a deep-link parameter mismatch: admin card clicks do not consistently open the editor to the same workflow definition the user clicked on.

**Your next steps:** Await Blathers' resolution of the deep-link alignment. Brewster is locked out until Blathers submits a new revision with the parameter mismatch fixed.

**References:**
- `.squad/log/2026-05-19T18-16-08Z-workflow-editor-selection-mismatch.md`
- `.squad/decisions/inbox/tangy-edit-workflow-link-final.md`

## Scribe Consolidation (2026-05-19T21:41:48.843Z)

Decisions consolidated into team decisions log. Orchestration recorded.

## Session: Vinyl/Core Boundary Integration (2026-05-23T13:04:58.778000+00:00)

All squad members deployed together to complete the vinyl/core boundary work. Architecture split successful:
- Core remains reusable notification infrastructure
- TestSite vinyl behavior is now opt-in
- All 815 tests passing
- 0 warnings in build/test lane


## Learnings — 2026-05-30T13:00:00+01:00 — Workflow Editor Umbraco DX Review (Slices 1+1.5+2+3a+3b @ b03ee38)

- The v17 backoffice section under `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/` is manifest-correct (Lit + UmbLitElement, no AngularJS leakage), but the dashboard renders an `<iframe>` pointing at MockBusinessApp (`https://localhost:7245/workflow-editor`). Integrators get an Umbraco *section* without an Umbraco *editor* — they must stand up a second .NET process.
- The App_Plugins payload lives in TestSite, not in `UmbracoPrism.WorkflowEditor`. There is no NuGet-time way for an integrator to acquire the manifest.
- The 11 `<prism-…>` custom elements have no public/internal documentation. `<prism-workflow-editor>`, `<prism-workflow-editor-shell>`, `<prism-workflow-graph>` are the realistic public surface; nothing in JSDoc/README declares this.
- `<prism-workflow-graph>` declares its `workflow` property with `attribute: false`, so it cannot be initialised from Razor markup. A `workflow-json` attribute would unlock public read-only embedding.
- `MapPrismWorkflowEditor()` silently requires a CORS policy named exactly `"WorkflowAuthoringDevCors"` to be registered in Development — invisible from the method signature.
- `IWorkflowPublishService.PreviewAsync` and `PublishPreviewResult` survive the scope reset; the endpoint is gone but the interface members still impose work on custom implementations.
- `AddPrismWorkflowEditor(authoredWorkflowBasePath: string.Empty, …)` is a sentinel-driven shape that only works because callers pre-register the store; splitting into two overloads removes the sentinel.
- `PrismWorkflowPageController` and `WorkflowHubController` rely on `User.Identity?.IsAuthenticated` rather than `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]`. Works today because PrismMemberCookie is the default scheme, but it's the wrong contract for any integrator mixing schemes.
- `WorkflowHubController` *does* drive workflow page URLs from `IPublishedContent` (`ContentAtRoot().DescendantsOrSelf()` + `workflowKey` value) — content-driven discovery is preserved; only the full-tree descendant scan is worth optimising.
- Walkthroughs (`docs/walkthroughs/authoring-a-workflow.md`, `docs/walkthroughs/planning-workflow-editor.md`) are editor-UX guides and mention neither `AddPrismWorkflowEditor` / `MapPrismWorkflowEditor` nor the App_Plugins manifest. The Umbraco-idiomatic order (compose → doctypes → controllers → templates → App_Plugins mount → editor) is not documented anywhere.
- Decision written to `.squad/decisions/inbox/brewster-editor-reset-umbraco-dx-review.md`.

- 2026-05-31 — Slice C (server portion) — gateways own routes. Deleted `AuthoredTransition` entirely. `AuthoredGateway` gained `Source` (required on Split, forbidden on Join) + `Routes` (`IReadOnlyList<AuthoredRoute>`). New `AuthoredRoute` record (`Id`, `Target`, `Trigger`, `Condition`, `RequiresRole`, `Actions`). `AuthoredWorkflow.Transitions` removed. Rewrote `AuthoredWorkflowSchemaValidator` (new PROJ141–PROJ152; retired PROJ106–109 + old PROJ141/142), `WorkflowProjector` (emits transitions from `gateway.Source × routes`), `WorkflowSimulationService` (full rewrite — `gatewayBySourceStage` lookup, `ResolveNextStage` chains through gateways), `WorkflowPatchService` (`add-route` / `update-route` / `delete-route` ops on path `/gateways/{key}/routes/{id}`). Schema dropped top-level `transitions`; gateway shape now conditionally requires `source` only for Split. Multi-target fan-outs require `(trigger, target)` uniqueness — deliberate evolution from spec wording for routers like payment-demo. All four reference workflows reshaped (planning, community-enquiry, information-request, payment-demo) in MockBusinessApp + Core.Tests fixtures + client planning fixture. Test status: 811/811 Core.Tests green, full solution build 0/0. **Outstanding for follow-up:** TS types collapse, graph (3350 LOC), inspector (1688 LOC), wire-format, fixtures/index.ts, stories, Playwright specs, MockBusinessApp admin-page strip, walkthrough corrections. See `.squad/decisions/inbox/copilot-slice-c-gateways-own-routes.md`.
