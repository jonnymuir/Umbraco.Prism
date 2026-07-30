# ServiceBlueprint editor — component API

The serviceBlueprint editor ships as a Lit-based bundle (`serviceBlueprint-editor.js`, served from
`Wayfinder.Editor/wwwroot/dist/`). Only three custom elements are
considered **public API**. Everything else in this folder is composition detail
and is marked `@internal` in its source — Razor authors and host applications
should not depend on it, and breaking changes there will not bump a contract.

> **Host it wherever the implementation needs it.** The editor is a plain Lit
> bundle with no assumptions about its host — the toolkit's job is to make
> hosting trivial anywhere, not to prescribe one hosting model. MockBusinessApp
> is a pure business-app host with no backoffice, so it hosts the editor
> runtime-only (`vite.serviceBlueprint-editor.config.ts` → `Wayfinder.Editor`'s
> static assets, served as a standalone page — see TestSite Razor pages, the
> Storybook harness, and the reference shell). Prism CMS Service Blueprint's entire
> reason for existing is the backoffice editing experience, so it mounts the
> same components natively as a Collection + entity-actions + Workspace backoffice
> screen instead (`vite.config.ts`'s `prism-cms-service-blueprint-manifests` entry →
> `UmbracoPrism.Core`'s own bundle; `<prism-service-blueprint-editor>` itself is mounted by
> `cms-service-blueprint-workspace-editor.element.ts` in
> `UmbracoPrism.Client/src/backoffice/cms-service-blueprint/workspace/`, scoped to whichever
> definitionKey the workspace route is currently editing).

## Public elements

| Element | Role | Bundle entry |
|---------|------|--------------|
| `<prism-service-blueprint-editor>` | Full authoring surface: graph + inspector + outline + validation + dialogs. | yes |
| `<prism-service-blueprint-editor-shell>` | Host harness — serviceBlueprint picker, API base wiring, URL sync. Mounts `<prism-service-blueprint-editor>`. | yes |
| `<prism-service-blueprint-graph>` | Vertical-queues graph. Authoring (default) or **read-only viewer** when `read-only` is set. | yes |

All three are registered as `customElements` when `serviceBlueprint-editor.js` loads.

`<prism-service-blueprint-graph>` is a Lit wrapper around a lazily-loaded
[React Flow](https://reactflow.dev) canvas (`graph/` module): the wrapper owns
the element contract (properties, events, dialogs, context menu, announcer)
while React Flow renders lanes, nodes, and edges inside the same shadow root
and provides pan, wheel zoom, and fitView. Node positions are derived by the
pure layout module `graph/service-blueprint-graph-layout.ts` (queue swim lanes for X,
Kahn longest-path ranking for Y — `npm run test:graph-layout` covers it).

---

### `<prism-service-blueprint-editor>`

Full authoring experience.

**Attributes**

| Attribute | Type | Default | Notes |
|-----------|------|---------|-------|
| `blueprint-key` | string | `"planning"` | ServiceBlueprint to load. Also reads `?serviceBlueprint=` URL param. |

**JS-only properties**

| Property | Type | Notes |
|----------|------|-------|
| `serviceBlueprintSource` | `ServiceBlueprintSource \| undefined` | Host-supplied source the editor reads serviceBlueprints from and writes back to. Required for runtime use. Storybook stories can pass `initialServiceBlueprint` instead. See `integrations/mockapp-service-blueprint-source.ts` for a reference HTTP implementation. |
| `actionCatalog` | `ServiceBlueprintActionCatalog \| undefined` | Host-supplied catalog of action types the editor can render. Falls back to `BuiltInServiceBlueprintActionCatalog` when unset. |
| `authorContext` | `ServiceBlueprintAuthorContext \| undefined` | Optional UX hint about the current author (`{ canSave?: boolean }`). Never authoritative — server-side authorization stays in the host application. |
| `availableQueues` | `QueueDefinition[]` | Host-supplied queue catalog used for queue labels and queue pickers. Shared editor code stays generic; the host decides which queues exist. |
| `initialServiceBlueprint` | `AuthoredServiceBlueprint \| null` | If set, bypasses `serviceBlueprintSource.load` and uses this serviceBlueprint directly. Designed for Storybook / fixtures. |

The editor has **no built-in HTTP client and no opinion about authentication**.
Hosts are responsible for implementing the `ServiceBlueprintSource` contract
(`list / load / save`) against their own persistence — the editor only sees
typed `AuthoredServiceBlueprint` values. A reference implementation that talks to the
MockBusinessApp's `/mockapp/serviceBlueprints/*` endpoints lives at
[`integrations/mockapp-service-blueprint-source.ts`](./integrations/mockapp-service-blueprint-source.ts).

**Definition tab — JSON twin-pane**

Alongside Canvas / Validation / Preview / Simulation / Help, the editor
exposes a **Definition** tab containing an editable JSON view of the current
`AuthoredServiceBlueprint`. Author-facing copy uses "Definition" — JSON is the
implementation detail.

* **Editor library:** [CodeMirror 6](https://codemirror.net/) (modules
  `@codemirror/{state,view,commands,language,lang-json,lint}`). Chosen for
  bundle size and clean shadow-DOM mounting over Monaco. Loaded **dynamically**
  on first activation of the Definition tab, so authors who stay on Canvas
  never download it. Static bundle stays ~338 KB; CodeMirror adds ~371 KB and
  the React Flow canvas chunk (react + react-dom + @xyflow/react) ~390 KB,
  each only on-demand.
* **Visual → Definition sync:** every visual edit (stage add/rename/move,
  gateway add/edit, route change, undo, redo) re-serializes the serviceBlueprint in
  canonical form (top-level key order: `definitionKey`, `displayName`,
  `version`, `schemaVersion`, `requestPolicy`, `initialStage`, `roles`,
  `stages`, `gateways`, `transitions`; nested keys alphabetical; 2-space
  indent) and pushes the new text into the editor.
* **Definition → Visual sync:** typing is debounced by **250 ms**. On
  settling:
  - **Valid JSON + schema-clean** → coerced to `AuthoredServiceBlueprint`, applied
    through the host's normal commit path (so the change lands on the
    document-level undo stack), and announced to a polite live region
    ("Definition updated. N stages, M gateways.").
  - **Invalid JSON or schema-violating** (e.g. retired `Waiting` /
    `StatusTimeline` stage kind, unnamed gateway, duplicate keys) → a banner
    above the editor explains why the definition can't be applied, with a
    disabled **Apply when valid** button and an enabled **Revert to current**
    button that rewinds the JSON to the serviceBlueprint's current canonical
    serialization. The visual pane stays on the last good state.
* **Diagnostics:** parse errors and schema violations render as inline
  CodeMirror lint markers on the offending lines.
* **Document-level undo:** an applied Definition edit goes onto the same
  history stack as visual edits — one Ctrl/Cmd-Z from the Canvas tab
  reverses it, and the Definition tab re-renders the prior canonical text.
  While the user is typing invalid or pending text, undo stays local to
  CodeMirror's own history (intra-text).
* **Read-only mode:** the underlying `<prism-definition-editor>` supports a
  `read-only` flag (used by future host-level read-only mode). Currently not
  exposed at the editor host level — that's Slice 8 territory.
* **Test hooks:** `data-prism-confidence-tab="definition"`,
  `data-prism-definition-panel`, `data-prism-definition-editor`,
  `data-prism-definition-banner`, `data-prism-definition-apply`,
  `data-prism-definition-revert`, `data-prism-definition-announcement`.

**Data hooks (test selectors)** — see the JSDoc block at the top of
`prism-service-blueprint-editor.ts` for the full list. The most stable ones are
`data-prism-save`, `data-prism-validation-rail`, `data-prism-toast`,
`data-prism-help-button`, `data-prism-history-undo`,
`data-prism-history-redo`.

---

### `<prism-service-blueprint-editor-shell>`

Thin shell that lists available serviceBlueprints and mounts
`<prism-service-blueprint-editor>`. Suitable for TestSite Razor pages and the reference
shell.

**Attributes**

| Attribute | Type | Default | Notes |
|-----------|------|---------|-------|
| `blueprint-key` | string | `"planning"` | Initial serviceBlueprint selection. Synced to `?serviceBlueprint=` URL param. |

**JS-only properties**

The shell forwards `serviceBlueprintSource`, `actionCatalog`, `authorContext`, and
`availableQueues` to
the nested `<prism-service-blueprint-editor>`. It uses `serviceBlueprintSource.list()` to
populate its picker, and renders a developer-affordance empty state when no
source is wired.

---

### `<prism-service-blueprint-graph>`

The vertical-queues graph. Queues are columns (intake → review → approval →
publish, or whichever the host configures); stages and gateways sit inside the
column for the queue they own. Queue labels live in the column headers, not on
the cards.

**Attributes**

| Attribute | Type | Default | Notes |
|-----------|------|---------|-------|
| `read-only` | boolean | `false` | Viewer mode — hides Add stage / Add gateway HUD buttons, all dialogs, and the canvas context menu. Selection and zoom remain available. Reflected to the DOM, so CSS can target `[read-only]`. |
| `service-blueprint-json` | string | `null` | Declarative form of the `serviceBlueprint` property. Parsed in `updated()` and assigned to `serviceBlueprint`. Invalid JSON is logged via `console.error`. Lets Razor / static HTML embed a graph with no JS wiring: `<prism-service-blueprint-graph read-only service-blueprint-json='...'>`. |

**JS-only properties**

| Property | Type | Notes |
|----------|------|-------|
| `serviceBlueprint` | `AuthoredServiceBlueprint \| null` | Programmatic form of `service-blueprint-json`. |
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
| `serviceBlueprint-updated` | `ServiceBlueprintUpdatedDetail` | Mutation occurred — authoring-only; never fires in `read-only` mode. |

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
* `serviceBlueprint-updated` cannot fire because no mutation paths are reachable.

A typical read-only embed:

```html
<prism-service-blueprint-graph
  read-only
  service-blueprint-json='{"blueprintKey":"planning","stages":[...],"transitions":[...],"gateways":[...]}'>
</prism-service-blueprint-graph>
```

---

## Internal composition (do not import)

The remaining elements are composition details of `<prism-service-blueprint-editor>` and
are tagged with `@internal` JSDoc. They may move, merge, or disappear without
notice:

* `<prism-step-inspector>`
* `<prism-confidence-tabs>`
* `<prism-help-panel>`
* `<prism-stage-preview>`
* `<prism-service-blueprint-simulation>`
* `<prism-service-blueprint-outline>`
* `<prism-stage-action-editor>`
* `<prism-inline-help>`
* `<prism-definition-editor>` — JSON twin-pane for the Definition tab

If a host needs functionality that one of these provides, raise a Squad
decision — we'd rather promote a stable element than have callers reach past
the public surface.

---

## Bundle reference

Built artefacts land in `src/Wayfinder.Editor/wwwroot/dist/`:

* `serviceBlueprint-editor.js` — Lit bundle that registers the three public elements.
* `serviceBlueprint-editor.html` — host harness used by TestSite Razor pages.

Build with `npm run build` from `src/UmbracoPrism.Client/`.

---

## Visual testing

The canvas has a dedicated visual regression suite proving five reading-level
concerns: lane fit, no-overlap, label fit (text doesn't crash), pan/fitView
behaviour, and arrow legibility — plus an ergonomics suite covering the
named author flows (add stage, selection survives a tab switch, keyboard
reach). The full strategy lives in
[`docs/testing/serviceBlueprint-editor-visual-tests.md`](../../../../docs/testing/serviceBlueprint-editor-visual-tests.md).

**Run locally:**

```bash
npx playwright test tests/service-blueprint-editor/serviceBlueprint-canvas-*.spec.ts \
                    tests/service-blueprint-editor/serviceBlueprint-editor-ergonomics.spec.ts \
                    --reporter=line
```

### Data attributes the visual suite depends on

These hooks are the public surface the visual contract leans on. **Do not
remove or rename without updating the suite in the same commit.**

| Attribute | Purpose |
|---|---|
| `data-prism-component="serviceBlueprint-graph"` | Graph root marker. |
| `data-prism-mode="graph"` | Workspace mode. |
| `data-prism-read-only="true|false"` | Read-only viewer marker. |
| `data-prism-graph-ready="true"` | Set on the host element once the React Flow canvas has committed nodes/edges — test probes wait on this. |
| `data-prism-queue-container=<queueKey>` | Lane bounding box for fit/overlap/arrow specs. |
| `data-prism-role-queue=<queueKey>` | Synonym kept for backwards compat. |
| `data-prism-queue-header=<queueKey>` | Lane header (pans with the canvas; not sticky). |
| `data-prism-stage-card=<stageKey>` | Stage bounding box. |
| `data-prism-stage=<stageKey>` | Stage click target + label container. |
| `data-prism-gateway-node=<gatewayKey>` | Gateway bounding box. |
| `data-prism-gateway=<gatewayKey>` | Gateway click target + label container. |
| `data-prism-route-path=<key>` | SVG route path (endpoint assertion). |
| `data-prism-route-from=<key>` / `data-prism-route-to=<key>` | Route endpoint mapping. |
| `data-prism-auto-arrange` | Tidy layout HUD button — rewrites every node's position back to the automatic arrangement in one undoable commit. |
