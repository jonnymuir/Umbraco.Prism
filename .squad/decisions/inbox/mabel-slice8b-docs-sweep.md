---
date: 2026-05-30T17:30:00+01:00
agent: mabel
area: workflow-editor
branch: squad/82-named-lanes-editor-slice
parent: copilot-directive-20260530T095311Z.md
status: shipped — scope-reset arc closed
---

# Slice 8b — Documentation sweep + obsolete manifest tests deleted

Closes out the workflow editor scope-reset sequence (Slices 1–8). Aligns all
public docs with the post-reset reality and removes the last pre-reset test
regressions left over from Slice 4.

## What changed

### Package 1 — Banners on historical design docs

- **`docs/design/workflow-editor-v1/04-agentic-surfaces.md`** — added "Status:
  Historical" banner. Whole doc subject (proposal-diff modal, conversation pane,
  chat drafter) was retired. Content preserved for archaeology.
- **`docs/design/workflow-editor-v1/03-umbraco-integration.md`** — added
  "Status: Historical" banner. The Umbraco backoffice mount (section, sidebar
  app, dashboard, `App_Plugins/PrismWorkflowEditor`) was retired in Slice 4.
  Points readers at the new `authoring-a-workflow.md` and the composition
  guide for current integration guidance.
- **`docs/design/workflow-editor-v1/01-authoring-ux.md`** — added a narrower
  "Status note" at the top calling out that the AI help / proposal diff
  sections (§15, §16) are historical, while the rest of the doc still
  describes today's editor.
- **`docs/design/workflow-editor-v1/README.md`** — added a partly-historical
  banner identifying Brewster's integration doc and Tangy's agentic doc as
  retired, and reframed the authors list accordingly.

### Package 2 — Walkthrough rewrite

- **`docs/walkthroughs/authoring-a-workflow.md`** — fully rewritten as an
  Umbraco integrator recipe. New order:
  1. Packages (`UmbracoPrism`, `UmbracoPrism.WorkflowEditor`,
     `UmbracoPrism.WorkflowRuntime`) — honest about which ship on NuGet today
     vs which are in-repo references.
  2. DI: `AddPrismWorkflowEditor(...)` + the **WorkflowAuthor** policy
     (Blathers' Slice 3c — failure to register returns 500 at startup; approver
     is bound to the authenticated principal and cannot be set in the body).
  3. Doctypes (MockBusinessApp reference only — no schema prescription).
  4. Route-hijack `PrismWorkflowPageController<T>` with TestSite's
     `WorkflowPageController` as the worked example.
  5. Razor templates (TestSite examples).
  6. **Where to host the editor** — plain statement of the load-bearing
     boundary: editor lives in the business app (MockBusinessApp is the
     reference), never in the Umbraco backoffice or TestSite. Read-only
     viewer is the only Razor-side mount.
  7. Open and use the editor — pointer to `planning-workflow-editor.md`.
- **`docs/walkthroughs/planning-workflow-editor.md`** — rewrote the overview
  to lead with vertical-lanes + slot-matrix language and to state plainly
  there is no chat / proposal-diff surface in the editor (external MCP
  client). Renamed Step 2 from "Graph view" to "Canvas shows the planning
  application stages in vertical lanes". Added **Step 10 — Open the
  Definition tab for the JSON view**, describing bidirectional sync, invalid
  JSON behaviour, and pointing at Isabelle's component README for sync rules.
  Editor mount location explicitly re-asserted as MockBusinessApp / not in
  the Umbraco backoffice.
- **`docs/walkthroughs/README.md`** — rewrote the two walkthrough blurbs:
  `authoring-a-workflow.md` is now described as the integrator recipe;
  `planning-workflow-editor.md` is the editor tour. Removed "proposal diffs"
  reference.

### Package 3 — composition.md alignment + cross-cutting sweep

- **`docs/guides/workflow-editor-composition.md`**:
  - Rewrote the top callout. Old text said "editor is runtime-only — never
    in the backoffice"; new text states that the editor lives in your
    business app (MockBusinessApp is the reference), not in the Umbraco
    backoffice and not in TestSite. Read-only viewer is the only public-page
    embed.
  - Added **Read-only public viewer** subsection with the `read-only` +
    `workflow-json` attributes explained, plus a one-line Razor example
    using `@Html.Raw(workflowJson)`. Explicit boundary reminder: the
    authoring editor must not be mounted from Razor or the backoffice.
  - Added **Definition tab (JSON view)** pointer (Slice 6) — bidirectional
    sync description, points at Isabelle's component README for the
    canonical rules.
  - Added **Visual testing** pointer (Slice 7) linking to
    `docs/testing/workflow-editor-visual-tests.md`.
- **Grep sweep across `docs/` and `README.md`** for the directive's
  retired-symbol list: `conversation pane`, `proposal diff`, `MockDrafter`,
  `preview endpoint`, `prism-proposal-diff`, `IWorkflowPreviewService`,
  `StageKind.Waiting`, `StageKind.StatusTimeline`,
  `App_Plugins/PrismWorkflowEditor`, `/api/workflow-authoring/.../save`,
  body-side `approver`. Only one stale survivor outside the design docs:
  the editor walkthrough blurb in `docs/walkthroughs/README.md` — fixed
  above. The lone `"waiting"` survivor in `docs/guides/workflow-setup.md`
  is the **runtime forms-engine** step type (still alive), not the retired
  editor stage kind — left in place.

### Package 4 — Delete obsolete manifest tests

- Deleted **`src/UmbracoPrism.Core.Tests/WorkflowEditorManifestTests.cs`**.
  Six tests asserted the existence of
  `src/UmbracoPrism.TestSite/App_Plugins/PrismWorkflowEditor/` files that
  Slice 4 deleted; they were the entire pre-reset backoffice-mount
  regression surface. No companion fixtures to delete.
- No other test file referenced `WorkflowEditorManifest`.

## Verification

- `dotnet build UmbracoPrism.sln -c Release` → 0 warnings, 0 errors.
- `dotnet test UmbracoPrism.sln -c Release --filter
  "FullyQualifiedName~UmbracoPrism.Core.Tests"` →
  **Passed: 860, Failed: 0, Skipped: 0, Total: 860** (was 860/866 with 6
  pre-existing failures at Slice 8a baseline).
- `cd src/UmbracoPrism.Client && npm run build` → clean. Bundle sizes
  unchanged (workflow-editor.js 334.87 kB; CodeMirror chunk 351.02 kB
  on-demand).
- Playwright editor specs not re-run (no code touches expected; Tangy's
  Slice 7/7.5 baseline of 88 passed / 11 skipped stands).

## Scope-reset arc — closed

Slices 1 through 8 are complete. The workflow editor now:

- ships as web components (3 public elements) consumed by a separate
  business app (MockBusinessApp is the reference);
- is **not** mounted in the Umbraco backoffice — that boundary is documented
  in `authoring-a-workflow.md`, `planning-workflow-editor.md`,
  `workflow-editor-composition.md`, and the component README;
- exposes a read-only viewer (`<prism-workflow-graph read-only>`) for
  public Razor pages, and only the viewer is acceptable as a Razor embed;
- has a JSON Definition tab synced with the canvas (Slice 6);
- has a visual regression suite covering canvas reading-level concerns
  (Slice 7) plus follow-up fixes (Slice 7.5);
- has a consolidated write surface (`/publish` canonical; `/save` retired;
  approver derived from principal, Slices 3c + 8a);
- has docs that lead with integration wiring, not editor UX (this slice).

No further scope-reset work outstanding.

## Files changed

```
M  docs/design/workflow-editor-v1/01-authoring-ux.md
M  docs/design/workflow-editor-v1/03-umbraco-integration.md
M  docs/design/workflow-editor-v1/04-agentic-surfaces.md
M  docs/design/workflow-editor-v1/README.md
M  docs/guides/workflow-editor-composition.md
M  docs/walkthroughs/README.md
M  docs/walkthroughs/authoring-a-workflow.md   (full rewrite)
M  docs/walkthroughs/planning-workflow-editor.md
D  src/UmbracoPrism.Core.Tests/WorkflowEditorManifestTests.cs
```
