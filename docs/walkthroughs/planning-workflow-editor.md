# Walkthrough — Planning Workflow Editor

A developer-facing guide to using the natural-language workflow editor to inspect and modify a planning permission workflow definition in Umbraco.Prism.

> **Prerequisites:** Stack running via [Codespaces](../../README.md#try-it-now--no-install-required) or [local setup](../../README.md#try-the-demo--local-setup). Familiarity with the [Planning Notification](planning-notification.md) walkthrough is recommended so you understand the citizen-facing journey you are modifying.
>
> **Wave 1 status:** The workflow editor page (`workflow-editor.html`) and its backing API (`/api/workflow-authoring/...`) are Wave 1 foundation deliverables. Screenshots will appear here once Isabelle's editor page and Blathers' API endpoints have shipped. The executable spec below (`planning-workflow-editor.walkthrough.spec.ts`) is ready and will populate this directory automatically when run with `CAPTURE_SCREENSHOTS=1`.

---

## Overview

The Prism workflow editor gives developers and operators a browser-based surface for inspecting and iterating on a live workflow definition using natural language. Instead of editing JSON by hand, the author types a change request in plain English; an AI agent drafts a structured proposal; the diff is reviewed inline; and a single click applies the change and persists it via the authoring API.

| Who uses this | Use case |
|---|---|
| Developer | Iterate on a workflow definition without leaving the browser |
| Operator / caseworker architect | Adjust stage order, add validation steps, or tune role assignments |
| QA engineer | Inspect the current live definition before writing a journey test |

The editor is a single-page application composed of three Lit web components:

| Component | Role |
|---|---|
| `<prism-workflow-graph>` | Visualises the workflow as a graph (default) or a navigable stage list |
| `<prism-conversation-pane>` | Natural-language input thread with proposal diff inline |
| `<prism-step-inspector>` | Sidebar showing the selected stage's fields and component tree |

---

## Step 1 — Load the workflow editor

Navigate to `/workflow-editor.html?workflow=planning`. MockBusinessApp resolves the `planning` key to the planning-permission workflow seed and calls `GET /api/workflow-authoring/planning-permission` to hydrate the editor.

<!-- Screenshot: 01-workflow-editor-loaded.png -->
> _Screenshot placeholder — will be populated when `CAPTURE_SCREENSHOTS=1` is run against the Wave 1 stack._

The editor loads with the workflow graph visible in visual (graph) mode. The `<prism-workflow-graph>` canvas has `role="application"` and is keyboard-accessible.

---

## Step 2 — Graph view shows the planning permission stages

`<prism-workflow-graph>` renders the workflow definition as a directed graph. Each stage is a node in the canvas; each permitted transition is an edge.

<!-- Screenshot: 02-graph-view-stages.png -->
> _Screenshot placeholder — will be populated when `CAPTURE_SCREENSHOTS=1` is run against the Wave 1 stack._

For the planning-permission workflow the stages are (from the planning seed):

| Stage key | Kind | Description |
|---|---|---|
| `applicant-details` | Capture | Personal and contact details |
| `check-answers` | Review | GDS check-answers summary |
| `waiting-for-review` | Waiting | Holding state while the caseworker assesses |
| `reviewer-assessment` | Decision | Caseworker approval or rejection |
| `confirmation` | Terminal | Outcome displayed to the applicant |

Each node carries `data-prism-stage="{stageKey}"` in shadow DOM — the same selector used by the `workflow-graph-keyboard.spec.ts` accessibility contract tests.

---

## Step 3 — Click a stage to open the step inspector

Clicking any stage node dispatches a `stage-selected` CustomEvent. `<prism-step-inspector>` renders in the right-hand sidebar.

<!-- Screenshot: 03-step-inspector-open.png -->
> _Screenshot placeholder — will be populated when `CAPTURE_SCREENSHOTS=1` is run against the Wave 1 stack._

The inspector shows the stage's display name, kind, and any role constraints. The sidebar root carries `data-prism-component="step-inspector"` and `data-prism-stage-detail="{stageKey}"`.

**Source:** [`prism-step-inspector.ts`](../../src/UmbracoPrism.Client/src/workflow-editor/prism-step-inspector.ts)

---

## Step 4 — Step inspector shows stage properties

The inspector lists the polymorphic component tree for the selected stage — sections, fieldsets, form fields, and conditional children. This mirrors the JSON structure in the workflow seed file.

<!-- Screenshot: 04-step-inspector-properties.png -->
> _Screenshot placeholder — will be populated when `CAPTURE_SCREENSHOTS=1` is run against the Wave 1 stack._

Each operation (field or component in the tree) carries `data-prism-op-index="{n}"`. The tree is read-only in Wave 1; editing individual components is a Wave 2 milestone.

**Source:** [`prism-step-inspector.ts`](../../src/UmbracoPrism.Client/src/workflow-editor/prism-step-inspector.ts), [`types.ts`](../../src/UmbracoPrism.Client/src/workflow-editor/types.ts)

---

## Step 5 — Toggle to stage list view

The mode-toggle button ("List view") is always visible in the toolbar. Clicking it switches `<prism-workflow-graph>` from graph mode (`role="application"` canvas) to linear mode (`role="listbox"` list of stage cards).

<!-- Screenshot: 05-stage-list-view.png -->
> _Screenshot placeholder — will be populated when `CAPTURE_SCREENSHOTS=1` is run against the Wave 1 stack._

In linear mode:
- Each stage card has `role="option"` and is keyboard-focusable.
- ArrowDown / ArrowUp navigate between stages (WCAG 2.1.1).
- The toggle button gains `aria-pressed="true"` and its label switches to "Graph view" (WCAG 4.1.2).

The full keyboard contract is exercised by [`workflow-graph-keyboard.spec.ts`](../../src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-keyboard.spec.ts).

**Source:** [`prism-workflow-graph.ts`](../../src/UmbracoPrism.Client/src/workflow-editor/prism-workflow-graph.ts)

---

## Step 6 — Type a natural language change request

`<prism-conversation-pane>` is the authoring input surface. The author types a change request in plain English and submits it via the Send button.

<!-- Screenshot: 06-nl-request-typed.png -->
> _Screenshot placeholder — will be populated when `CAPTURE_SCREENSHOTS=1` is run against the Wave 1 stack._

> **Example request used in this walkthrough:**
> _"Add an identity verification step before the reviewer assessment stage."_

The conversation pane carries `data-prism-component="conversation-pane"`. The textarea has `data-prism-conversation-input` in shadow DOM. Playwright's `getByRole('textbox')` finds it by piercing the shadow root.

**Source:** [`prism-conversation-pane.ts`](../../src/UmbracoPrism.Client/src/workflow-editor/prism-conversation-pane.ts)

---

## Step 7 — Submit and receive a proposal diff

Clicking Send issues a `POST /api/workflow-authoring/planning-permission/proposals` with the NL request body. Blathers' endpoint invokes the AI agent, which produces an `AuthoringProposal` envelope describing the proposed change as a set of atomic operations.

<!-- Screenshot: 07-proposal-diff.png -->
> _Screenshot placeholder — will be populated when `CAPTURE_SCREENSHOTS=1` is run against the Wave 1 stack._

The proposal diff (`<prism-proposal-diff>`, `data-prism-component="proposal-diff"`) renders inline in the conversation thread. It shows:
- The proposed change in a structured diff format (inserted/modified/deleted operations).
- A validation summary: `pass` enables "Accept all"; `fail` disables it and explains why.
- A "Reject" button that dismisses the proposal without applying it.

**Source:** [`prism-proposal-diff.ts`](../../src/UmbracoPrism.Client/src/workflow-editor/prism-proposal-diff.ts), [`04-agentic-surfaces.md`](../design/workflow-editor-v1/04-agentic-surfaces.md)

---

## Step 8 — Accept the proposal

When validation status is `pass`, the "Accept all" button is enabled. Clicking it issues a `PATCH /api/workflow-authoring/planning-permission` with the updated workflow definition.

The Accept all button is accessible via `getByRole('button', { name: /accept all/i })` — Playwright's role query pierces shadow DOM.

**Validation fail path:** If the agent proposes a structurally invalid change (e.g. a duplicate stage key or an unreachable transition), validation status is `fail`. Accept all is rendered but disabled. The author must reject the proposal and refine their request. This path is exercised by the `planning-workflow-agent-loop.spec.ts` stub (currently skipped pending a `with-failing-proposal` Storybook story).

---

## Step 9 — Workflow graph reflects the applied change

After the PATCH request completes, `<prism-workflow-graph>` re-renders with the updated definition returned by the API. The new identity-verification stage appears as a node in the graph, positioned between `check-answers` and `reviewer-assessment`.

<!-- Screenshot: 09-proposal-applied.png -->
> _Screenshot placeholder — will be populated when `CAPTURE_SCREENSHOTS=1` is run against the Wave 1 stack._

The graph reflects the canonical definition as persisted by Blathers' endpoint — not a local optimistic update. If the PATCH fails (e.g. a concurrent edit conflict), the graph stays unchanged and an error message appears in the conversation thread.

---

## Running the screenshots

Once both Wave 1 PRs have merged (Isabelle's editor page + Blathers' API), regenerate the screenshots with:

```bash
cd src/UmbracoPrism.Client
CAPTURE_SCREENSHOTS=1 npx playwright test \
  --config=playwright.localhost-auth.config.ts \
  --grep "Planning Workflow Editor walkthrough" \
  --reporter=line
```

The spec runs against the full Aspire stack (LiveAppHost) and writes PNGs to `docs/images/walkthroughs/planning-workflow-editor/`. Commit the images alongside the spec.

---

## Related

- **Executable spec:** [`planning-workflow-editor.walkthrough.spec.ts`](../../src/UmbracoPrism.Client/tests/walkthroughs/planning-workflow-editor.walkthrough.spec.ts)
- **Keyboard contract tests:** [`workflow-graph-keyboard.spec.ts`](../../src/UmbracoPrism.Client/tests/workflow-editor/workflow-graph-keyboard.spec.ts)
- **Agent-loop seam tests:** [`planning-workflow-agent-loop.spec.ts`](../../src/UmbracoPrism.Client/tests/agent-loop/planning-workflow-agent-loop.spec.ts)
- **Fixture contract tests:** [`PlanningWorkflowFixtureTests.cs`](../../src/UmbracoPrism.Core.Tests/Workflow/Authoring/PlanningWorkflowFixtureTests.cs)
- **Design doc:** [`01-authoring-ux.md`](../design/workflow-editor-v1/01-authoring-ux.md), [`04-agentic-surfaces.md`](../design/workflow-editor-v1/04-agentic-surfaces.md)
- **Citizen-facing context:** [Planning Notification](planning-notification.md)
