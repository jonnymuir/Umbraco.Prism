# Workflow editor — component API

The workflow editor ships as a Lit-based bundle (`workflow-editor.js`, served from
`UmbracoPrism.WorkflowEditor/wwwroot/dist/`). Only three custom elements are
considered **public API**. Everything else in this folder is composition detail
and is marked `@internal` in its source — Razor authors and host applications
should not depend on it, and breaking changes there will not bump a contract.

> 🛑 **The editor is runtime-only.** It must not be mounted into the Umbraco v17
> backoffice. Hosts are TestSite Razor pages, the Storybook harness, and the
> reference shell — never a backoffice dashboard.

## Public elements

| Element | Role | Bundle entry |
|---------|------|--------------|
| `<prism-workflow-editor>` | Full authoring surface: graph + inspector + outline + validation + dialogs. | yes |
| `<prism-workflow-editor-shell>` | Host harness — workflow picker, API base wiring, URL sync. Mounts `<prism-workflow-editor>`. | yes |
| `<prism-workflow-graph>` | Vertical-lanes graph. Authoring (default) or **read-only viewer** when `read-only` is set. | yes |

All three are registered as `customElements` when `workflow-editor.js` loads.

---

### `<prism-workflow-editor>`

Full authoring experience.

**Attributes**

| Attribute | Type | Default | Notes |
|-----------|------|---------|-------|
| `workflow-key` | string | `"planning"` | Workflow to load. Also reads `?workflow=` URL param. |
| `authoring-api-base` | string | `""` | Optional override for the authoring API origin. |
| `approver-name` | string | `"reference-shell"` | Written into apply provenance. |

**JS-only properties**

| Property | Type | Notes |
|----------|------|-------|
| `initialWorkflow` | `AuthoredWorkflow \| null` | If set, bypasses the API and uses this workflow directly. Designed for Storybook / fixtures. |

**Data hooks (test selectors)** — see the JSDoc block at the top of
`prism-workflow-editor.ts` for the full list. The most stable ones are
`data-prism-save`, `data-prism-validation-rail`, `data-prism-toast`,
`data-prism-help-button`, `data-prism-history-undo`,
`data-prism-history-redo`.

---

### `<prism-workflow-editor-shell>`

Thin shell that lists available workflows and mounts
`<prism-workflow-editor>`. Suitable for TestSite Razor pages and the reference
shell.

**Attributes**

| Attribute | Type | Default | Notes |
|-----------|------|---------|-------|
| `workflow-key` | string | `"planning"` | Initial workflow selection. Synced to `?workflow=` URL param. |
| `authoring-api-base` | string | `""` | Optional override for the authoring API origin. |

---

### `<prism-workflow-graph>`

The vertical-lanes graph. Lanes are columns (intake → review → approval →
publish, or whichever the workflow defines); stages and gateways sit inside the
column for the lane they own. Lane labels live in the column headers, not on
the cards.

**Attributes**

| Attribute | Type | Default | Notes |
|-----------|------|---------|-------|
| `read-only` | boolean | `false` | Viewer mode — hides Add stage / Add gateway HUD buttons, all dialogs, and the canvas context menu. Selection and zoom remain available. Reflected to the DOM, so CSS can target `[read-only]`. |
| `workflow-json` | string | `null` | Declarative form of the `workflow` property. Parsed in `updated()` and assigned to `workflow`. Invalid JSON is logged via `console.error`. Lets Razor / static HTML embed a graph with no JS wiring: `<prism-workflow-graph read-only workflow-json='...'>`. |

**JS-only properties**

| Property | Type | Notes |
|----------|------|-------|
| `workflow` | `AuthoredWorkflow \| null` | Programmatic form of `workflow-json`. |
| `selectedStageKey` | `string \| null` | Inbound selection — host sets this to drive the graph's highlight. |
| `selectedGatewayKey` | `string \| null` | Inbound selection. |
| `selectedTransitionIndex` | `number \| null` | Inbound transition highlight. |
| `simulationCurrentStageKey` / `simulationPathStageKeys` / `simulationPathTransitionIndices` | various | Optional simulation overlay state. |

**Events**

| Event | Detail | When |
|-------|--------|------|
| `stage-selected` | `{ stageKey }` | A stage card receives selection. |
| `gateway-selected` | `{ gatewayKey }` | A gateway card receives selection. |
| `transition-selected` | `{ transitionIndex }` | A transition arrow is activated. |
| `selection-change` | `GraphSelectionDetail` | Any selection change (broader umbrella). |
| `inspector-requested` | `GraphSelectionDetail` | User explicitly asks for the inspector (e.g. Enter on focus). |
| `workflow-updated` | `WorkflowUpdatedDetail` | Mutation occurred — authoring-only; never fires in `read-only` mode. |

**Read-only behaviour**

When `read-only` is set:

* Add stage / Add gateway HUD buttons render as empty placeholders (no buttons).
* Empty-state suppresses the Add first stage CTA and shows alternate copy.
* Create / delete / gateway / route dialogs are skipped from the render tree
  entirely.
* Canvas, stage, and transition `contextmenu` handlers are not attached, so
  the editor context menu can never open.
* `aria-roledescription` becomes "viewer" so AT advertises it as
  navigation-only.
* `workflow-updated` cannot fire because no mutation paths are reachable.

A typical read-only embed:

```html
<prism-workflow-graph
  read-only
  workflow-json='{"workflowKey":"planning","stages":[...],"transitions":[...],"gateways":[...]}'>
</prism-workflow-graph>
```

---

## Internal composition (do not import)

The remaining elements are composition details of `<prism-workflow-editor>` and
are tagged with `@internal` JSDoc. They may move, merge, or disappear without
notice:

* `<prism-step-inspector>`
* `<prism-confidence-tabs>`
* `<prism-help-panel>`
* `<prism-stage-preview>`
* `<prism-workflow-simulation>`
* `<prism-workflow-outline>`
* `<prism-workflow-action-editor>`
* `<prism-inline-help>`

If a host needs functionality that one of these provides, raise a Squad
decision — we'd rather promote a stable element than have callers reach past
the public surface.

---

## Bundle reference

Built artefacts land in `src/UmbracoPrism.WorkflowEditor/wwwroot/dist/`:

* `workflow-editor.js` — Lit bundle that registers the three public elements.
* `workflow-editor.html` — host harness used by TestSite Razor pages.

Build with `npm run build` from `src/UmbracoPrism.Client/`.
