# Service Blueprint Editor Composition — Advanced Patterns

> **Note:** For the integration recipe (how to embed the editor in your business app), see [Embedding the Service Blueprint Editor](./embedding-the-service-blueprint-editor.md). This document covers advanced composition patterns for custom hosts.

This guide shows advanced patterns for composing the service blueprint editor into your own application. It assumes you have already implemented `WorkflowSource` and understand the basic integration flow.

**For context:**
- **Component API reference (public elements, attributes, events)?** See [`src/UmbracoPrism.Client/src/service-blueprint-editor/README.md`](../../src/UmbracoPrism.Client/src/service blueprint-editor/README.md)
- **Understanding the editor design?** See [Service Blueprint Editor V1 Design](../design/service-blueprint-editor-v1/README.md)
- **Setting up service blueprints in Umbraco?** See [Setting Up a Prism Service Blueprint](./service-blueprint-setup.md)

> The editor lives in your **business app** — never inside the Umbraco backoffice, and not in TestSite (TestSite is the reference runtime). MockBusinessApp is the reference authoring host; your own host follows the same pattern. The Storybook harness is fine for development and tests. If you only need to display a published service blueprint on a public Umbraco page, use the read-only viewer (`<prism-service-blueprint-graph read-only>`) — see [Read-only public viewer](#read-only-public-viewer).

> Only three elements are public API: `<prism-service-blueprint-editor>`, `<prism-service-blueprint-editor-shell>`, and `<prism-service-blueprint-graph>` (the last also supports a `read-only` viewer mode for embedding a published service blueprint on read-only pages). Everything else under `src/service-blueprint-editor/` is composition detail marked `@internal`.

---

## The Elements

The service blueprint editor ships two elements:

- **`<prism-service-blueprint-editor>`** — the visual editor (canvas, inspector, validation, history, simulation).
- **`<prism-service-blueprint-editor-shell>`** — a wrapper that adds service blueprint selection and displays.

Both require a `WorkflowSource` to be wired via JavaScript. See [Embedding the Service Blueprint Editor](./embedding-the-service-blueprint-editor.md) for the basic integration pattern.

---

## Host Responsibility Model

The host keeps one clear responsibility: **mount the editor and wire the `WorkflowSource`**. Everything else belongs to your application.

### What the Host Owns

✅ **Host responsibilities:**
- Service Blueprint selection UI (if needed)
- `WorkflowSource` implementation wiring
- Page layout and branding
- Editor mounting and initialization

### What Your Application Owns

✅ **Application responsibilities:**
- Service Blueprint storage and versioning (your `WorkflowSource` implementation)
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

The editor ships a default catalog of generic actions. Your business app can extend it. See [Extending the Action Catalog](./embedding-the-service-blueprint-editor.md#extending-the-action-catalog) for details.

Key points:

- Extend `BuiltInWorkflowActionCatalog` to add your custom actions.
- Each action has a `type`, `label`, `summary`, `appliesTo`, `paramsSchema`, and `defaultParams`.
- The editor validates parameters against the schema at design time.
- Your runtime executes the action at runtime.

---

## Configuration Boundaries

Keep authoring configuration in your API and docs, not in the editor shell itself.

- **Action catalog** — what actions exist and what parameters they need.
- **Validation rules** — what makes a service blueprint valid in your system.
- **Stage types** — which stage kinds your host supports.
- **Assignment model** — how authored `actor`, `roleGates`, and queue metadata map to your lane labels.

Keep runtime policies, secrets, analytics, and feature flags out of the editor UI. Preview and publish calls should receive the authored service blueprint contract only.

---

## Custom Canonical JSON Helpers

The editor uses `normaliseWorkflow` and `serialiseWorkflow` from `service-blueprint-wire-format.ts` to convert between wire JSON and `AuthoredServiceBlueprint` objects. If you need custom field normalization or serialization (e.g., for backward compatibility with a legacy format), you can wrap these helpers in your own functions.

Example:

```typescript
import { normaliseWorkflow, serialiseWorkflow } from '@umbraco-prism/client/service-blueprint-editor';
import type { AuthoredServiceBlueprint } from '@umbraco-prism/client/service-blueprint-editor';

export function loadLegacyWorkflow(json: Record<string, unknown>): AuthoredServiceBlueprint {
  // Apply legacy field migrations before normalising
  const migrated = { ...json };
  if ('oldFieldName' in migrated) {
    migrated['newFieldName'] = migrated['oldFieldName'];
    delete migrated['oldFieldName'];
  }
  return normaliseWorkflow(migrated);
}

export function saveLegacyWorkflow(service-blueprint: AuthoredServiceBlueprint): Record<string, unknown> {
  const json = serialiseWorkflow(service-blueprint);
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

If you need a custom host (for branding, custom service blueprints, or integration), follow this pattern:

### 1. Start with a thin wrapper

```typescript
import { LitElement, html } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import '@umbraco-prism/client/service-blueprint-editor/prism-service-blueprint-editor.js';
import type { WorkflowSource } from '@umbraco-prism/client/service-blueprint-editor';

@customElement('my-service-blueprint-host')
export class MyWorkflowHost extends LitElement {
  @property({ attribute: false }) workflowSource?: WorkflowSource;

  render() {
    return html`
      <prism-service-blueprint-editor
        .workflowSource=${this.workflowSource}>
      </prism-service-blueprint-editor>
    `;
  }
}
```

### 2. Add only what you need

If you need service blueprint selection:

```typescript
@property() selectedKey = 'planning';

private _handleWorkflowChange(key: string) {
  this.selectedKey = key;
}

render() {
  return html`
    <section class="service-blueprint-selector">
      <select @change="${(e: Event) => this._handleWorkflowChange((e.target as HTMLSelectElement).value)}">
        <option value="planning">Planning Service-Blueprint</option>
        <option value="payment">Payment Service-Blueprint</option>
      </select>
    </section>
    <prism-service-blueprint-editor
      .workflowSource=${this.workflowSource}
      .workflowKey=${this.selectedKey}>
    </prism-service-blueprint-editor>
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

The `<prism-service-blueprint-graph>` element doubles as a read-only viewer. Use it to drop a published service blueprint onto a public page — for example, a "how this process works" explainer — without loading the full authoring shell.

Two attributes do all the work:

- `read-only` — hides the Add stage / Add gateway buttons, suppresses all dialogs and the context menu, and stops `service-blueprint-updated` from firing. Selection and zoom still work, and the canvas advertises itself as a viewer to assistive tech (`aria-roledescription="viewer"`).
- `service-blueprint-json` — a string containing the published `AuthoredServiceBlueprint` JSON. The element parses it on attach and renders the lanes, stages, gateways, and transitions exactly as the editor would.

A one-line Razor embed for a published service blueprint:

```razor
<prism-service-blueprint-graph read-only service-blueprint-json='@Html.Raw(workflowJson)'></prism-service-blueprint-graph>
```

`workflowJson` is the canonical JSON of the service blueprint you want to show. Render the service blueprint-editor bundle on the same page so the element is defined.

**Boundary reminder.** This Razor pattern is **only** for the read-only viewer. Do **not** mount `<prism-service-blueprint-editor>` or `<prism-service-blueprint-editor-shell>` from Razor or the Umbraco backoffice — the authoring editor belongs in your business app (MockBusinessApp is the reference), not in the Umbraco runtime.

---

## Definition tab (JSON view)

Inside `<prism-service-blueprint-editor>` the **Definition** tab shows an editable JSON view of the current service blueprint. It stays in sync with the visual canvas in both directions — visual edits re-serialise the JSON, and valid JSON edits flow back into the canvas through the normal commit path (so undo and redo still work). Invalid JSON keeps the canvas on the last good state and explains the problem in a banner.

This is a power-user feature for copy-paste, quick edits, and diffing — there is nothing for the host to configure. See [`src/UmbracoPrism.Client/src/service-blueprint-editor/README.md`](../../src/UmbracoPrism.Client/src/service blueprint-editor/README.md) for the test hooks and exact sync rules.

---

## Visual testing

The editor has a dedicated visual regression suite that pins five reading-level concerns on the canvas — lane fit, no overlap, label fit, scroll behaviour, and arrow legibility — plus an ergonomics suite covering the named author flows (add stage, selection survives a tab switch, keyboard reach). If you build a custom host, the same suite is the contract you embed against: if your host renders `<prism-service-blueprint-editor>` in a normal page, the canvas behaviour is already covered.

See [`docs/testing/service-blueprint-editor-visual-tests.md`](../testing/service-blueprint-editor-visual-tests.md) for the full strategy and run instructions.

---

## Next Steps

1. **Implement `WorkflowSource`:** See [Embedding the Service Blueprint Editor](./embedding-the-service-blueprint-editor.md)
2. **Review the editor design:** Understand what the editor can do and what it can't in [Service Blueprint Editor V1 Design](../design/service-blueprint-editor-v1/README.md)
3. **Configure actions and forms:** Document your action catalog and forms engine integration for authors
4. **Test the service blueprint:** Use the editor's built-in validation and simulation features to verify your definitions
