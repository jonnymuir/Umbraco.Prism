# Workflow Editor Composition — Advanced Patterns

> **Note:** For the integration recipe (how to embed the editor in your business app), see [Embedding the Workflow Editor](./embedding-the-workflow-editor.md). This document covers advanced composition patterns for custom hosts.

This guide shows advanced patterns for composing the workflow editor into your own application. It assumes you have already implemented `WorkflowSource` and understand the basic integration flow.

**For context:**
- **Component API reference (public elements, attributes, events)?** See [`src/UmbracoPrism.Client/src/workflow-editor/README.md`](../../src/UmbracoPrism.Client/src/workflow-editor/README.md)
- **Understanding the editor design?** See [Workflow Editor V1 Design](../design/workflow-editor-v1/README.md)
- **Setting up workflows in Umbraco?** See [Setting Up a Prism Workflow](./workflow-setup.md)

> The editor lives in your **business app** — never inside the Umbraco backoffice, and not in TestSite (TestSite is the reference runtime). MockBusinessApp is the reference authoring host; your own host follows the same pattern. The Storybook harness is fine for development and tests. If you only need to display a published workflow on a public Umbraco page, use the read-only viewer (`<prism-workflow-graph read-only>`) — see [Read-only public viewer](#read-only-public-viewer).

> Only three elements are public API: `<prism-workflow-editor>`, `<prism-workflow-editor-shell>`, and `<prism-workflow-graph>` (the last also supports a `read-only` viewer mode for embedding a published workflow on read-only pages). Everything else under `src/workflow-editor/` is composition detail marked `@internal`.

---

## The Elements

The workflow editor ships two elements:

- **`<prism-workflow-editor>`** — the visual editor (canvas, inspector, validation, history, simulation).
- **`<prism-workflow-editor-shell>`** — a wrapper that adds workflow selection and displays.

Both require a `WorkflowSource` to be wired via JavaScript. See [Embedding the Workflow Editor](./embedding-the-workflow-editor.md) for the basic integration pattern.

---

## Host Responsibility Model

The host keeps one clear responsibility: **mount the editor and wire the `WorkflowSource`**. Everything else belongs to your application.

### What the Host Owns

✅ **Host responsibilities:**
- Workflow selection UI (if needed)
- `WorkflowSource` implementation wiring
- Page layout and branding
- Editor mounting and initialization

### What Your Application Owns

✅ **Application responsibilities:**
- Workflow storage and versioning (your `WorkflowSource` implementation)
- Authentication and authorization (who can edit what?)
- Action handlers and runtime execution
- Business logic and domain validation
- Forms engine integration
- Runtime case management and state persistence
- Error handling and recovery flows
- Analytics and observability

**Why this split?** The editor is an authoring tool, not a runtime system. Keep the editor host a thin shell. Let your application own the business logic.

---

## Custom Action Catalog

The editor ships a default catalog of generic actions. Your business app can extend it. See [Extending the Action Catalog](./embedding-the-workflow-editor.md#extending-the-action-catalog) for details.

Key points:

- Extend `BuiltInWorkflowActionCatalog` to add your custom actions.
- Each action has a `type`, `label`, `summary`, `appliesTo`, `paramsSchema`, and `defaultParams`.
- The editor validates parameters against the schema at design time.
- Your runtime executes the action at runtime.

---

## Custom Canonical JSON Helpers

The editor uses `normaliseWorkflow` and `serialiseWorkflow` from `workflow-wire-format.ts` to convert between wire JSON and `AuthoredWorkflow` objects. If you need custom field normalization or serialization (e.g., for backward compatibility with a legacy format), you can wrap these helpers in your own functions.

Example:

```typescript
import { normaliseWorkflow, serialiseWorkflow } from '@umbraco-prism/client/workflow-editor';
import type { AuthoredWorkflow } from '@umbraco-prism/client/workflow-editor';

export function loadLegacyWorkflow(json: Record<string, unknown>): AuthoredWorkflow {
  // Apply legacy field migrations before normalising
  const migrated = { ...json };
  if ('oldFieldName' in migrated) {
    migrated['newFieldName'] = migrated['oldFieldName'];
    delete migrated['oldFieldName'];
  }
  return normaliseWorkflow(migrated);
}

export function saveLegacyWorkflow(workflow: AuthoredWorkflow): Record<string, unknown> {
  const json = serialiseWorkflow(workflow);
  // Apply legacy field migrations after serialising
  if ('newFieldName' in json) {
    json['oldFieldName'] = json['newFieldName'];
    delete json['newFieldName'];
  }
  return json;
}
```

Use these in your `WorkflowSource` implementation instead of calling `normaliseWorkflow` / `serialiseWorkflow` directly.

---

## Building Your Own Host

If you need a custom host (for branding, custom workflows, or integration), follow this pattern:

### 1. Start with a thin wrapper

```typescript
import { LitElement, html } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import '@umbraco-prism/client/workflow-editor/prism-workflow-editor.js';
import type { WorkflowSource } from '@umbraco-prism/client/workflow-editor';

@customElement('my-workflow-host')
export class MyWorkflowHost extends LitElement {
  @property({ attribute: false }) workflowSource?: WorkflowSource;

  render() {
    return html`
      <prism-workflow-editor
        .workflowSource=${this.workflowSource}>
      </prism-workflow-editor>
    `;
  }
}
```

### 2. Add only what you need

If you need workflow selection:

```typescript
@property() selectedKey = 'planning';

private _handleWorkflowChange(key: string) {
  this.selectedKey = key;
}

render() {
  return html`
    <section class="workflow-selector">
      <select @change="${(e: Event) => this._handleWorkflowChange((e.target as HTMLSelectElement).value)}">
        <option value="planning">Planning Workflow</option>
        <option value="payment">Payment Workflow</option>
      </select>
    </section>
    <prism-workflow-editor
      .workflowSource=${this.workflowSource}
      .workflowKey=${this.selectedKey}>
    </prism-workflow-editor>
  `;
}
```

### 3. Stop there

Don't add:
- Form controls for source configuration (hard-code or use environment variables)
- Help text or explanatory copy (write it in your onboarding docs instead)
- Integration snippets or code examples (put those in your developer guides)

Your host should be **boring and minimal**. The editor is the interesting part.

---

## Read-only public viewer

The `<prism-workflow-graph>` element doubles as a read-only viewer. Use it to drop a published workflow onto a public page — for example, a "how this process works" explainer — without loading the full authoring shell.

Two attributes do all the work:

- `read-only` — hides the Add stage / Add gateway buttons, suppresses all dialogs and the context menu, and stops `workflow-updated` from firing. Selection and zoom still work, and the canvas advertises itself as a viewer to assistive tech (`aria-roledescription="viewer"`).
- `workflow-json` — a string containing the published `AuthoredWorkflow` JSON. The element parses it on attach and renders the lanes, stages, gateways, and transitions exactly as the editor would.

A one-line Razor embed for a published workflow:

```razor
<prism-workflow-graph read-only workflow-json='@Html.Raw(workflowJson)'></prism-workflow-graph>
```

`workflowJson` is the canonical JSON of the workflow you want to show. Render the workflow-editor bundle on the same page so the element is defined.

**Boundary reminder.** This Razor pattern is **only** for the read-only viewer. Do **not** mount `<prism-workflow-editor>` or `<prism-workflow-editor-shell>` from Razor or the Umbraco backoffice — the authoring editor belongs in your business app (MockBusinessApp is the reference), not in the Umbraco runtime.

---

## Definition tab (JSON view)

Inside `<prism-workflow-editor>` the **Definition** tab shows an editable JSON view of the current workflow. It stays in sync with the visual canvas in both directions — visual edits re-serialise the JSON, and valid JSON edits flow back into the canvas through the normal commit path (so undo and redo still work). Invalid JSON keeps the canvas on the last good state and explains the problem in a banner.

This is a power-user feature for copy-paste, quick edits, and diffing — there is nothing for the host to configure. See [`src/UmbracoPrism.Client/src/workflow-editor/README.md`](../../src/UmbracoPrism.Client/src/workflow-editor/README.md) for the test hooks and exact sync rules.

---

## Visual testing

The editor has a dedicated visual regression suite that pins five reading-level concerns on the canvas — lane fit, no overlap, label fit, scroll behaviour, and arrow legibility — plus an ergonomics suite covering the named author flows (add stage, selection survives a tab switch, keyboard reach). If you build a custom host, the same suite is the contract you embed against: if your host renders `<prism-workflow-editor>` in a normal page, the canvas behaviour is already covered.

See [`docs/testing/workflow-editor-visual-tests.md`](../testing/workflow-editor-visual-tests.md) for the full strategy and run instructions.

---

## Next Steps

1. **Implement `WorkflowSource`:** See [Embedding the Workflow Editor](./embedding-the-workflow-editor.md)
2. **Review the editor design:** Understand what the editor can do and what it can't in [Workflow Editor V1 Design](../design/workflow-editor-v1/README.md)
3. **Configure actions and forms:** Document your action catalog and forms engine integration for authors
4. **Test the workflow:** Use the editor's built-in validation and simulation features to verify your definitions
