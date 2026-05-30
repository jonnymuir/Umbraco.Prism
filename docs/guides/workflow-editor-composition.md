# Composing the Workflow Editor into Your Application

This guide shows how to embed the workflow editor into your own application with minimal complexity.

**For context:**
- **Component API reference (public elements, attributes, events)?** See [`src/UmbracoPrism.Client/src/workflow-editor/README.md`](../../src/UmbracoPrism.Client/src/workflow-editor/README.md)
- **Understanding the editor design?** See [Workflow Editor V1 Design](../design/workflow-editor-v1/README.md)
- **Setting up workflows in Umbraco?** See [Setting Up a Prism Workflow](./workflow-setup.md)

> The editor lives in your **business app** — never inside the Umbraco backoffice, and not in TestSite (TestSite is the reference runtime). MockBusinessApp is the reference authoring host; your own host follows the same pattern. The Storybook harness is fine for development and tests. If you only need to display a published workflow on a public Umbraco page, use the read-only viewer (`<prism-workflow-graph read-only>`) — see [Read-only public viewer](#read-only-public-viewer).

> Only three elements are public API: `<prism-workflow-editor>`, `<prism-workflow-editor-shell>`, and `<prism-workflow-graph>` (the last also supports a `read-only` viewer mode for embedding a published workflow on read-only pages). Everything else under `src/workflow-editor/` is composition detail marked `@internal`.

---

## The Simplest Way: One Element + One API Base

The workflow editor is a Web Component. Drop it into your page and point it at your authoring API:

```html
<prism-workflow-editor
  workflow-key="planning"
  authoring-api-base="https://your-authoring-api/api/authoring">
</prism-workflow-editor>
```

That's all you need. The editor:
- loads the workflow definition from your authoring API
- lets authors edit stages, transitions, and actions
- validates the workflow structure
- publishes the result back to your authoring API

---

## Why the Host Should Stay Thin

The reference shell keeps one clear responsibility: **mount the editor and wire the authoring API**. Everything else belongs to your application.

### What the Host Owns

✅ **Host responsibilities:**
- Workflow key selection (which workflow to edit?)
- Authoring API wiring (where is the API?)
- Authentication (if needed)
- Editor mounting and initialization

### What Your Application Owns

✅ **Application responsibilities:**
- Workflow definition storage and versioning
- Action handlers and runtime execution
- Business logic and domain validation
- Forms engine integration
- Runtime case management and state persistence
- User authentication and authorization (both authoring and runtime)
- Error handling and recovery flows
- Analytics and observability

**Why this split?** The editor is an authoring tool, not a runtime system. Mixing authoring concerns into the runtime host makes both harder to test, understand, and evolve independently. Keep the editor host a thin shell; let your application own the business logic.

---

## Configuration: What Goes in Docs vs. Runtime UI

Some configuration belongs in documentation and your authoring API. Other configuration should stay out of the UI.

### Configuration That Belongs in Docs + API

These settings should be fixed in your authoring API and documented for developers:

- **Action catalog** — What actions are available? What parameters do they need?
- **Validation rules** — What makes a workflow definition valid in your system?
- **Forms engine components** — What form fields and layouts does your system support?
- **Stage types** — What stage types (form, review, decision, confirmation) are available?
- **Role definitions** — What actor roles exist in your system?
- **Assignment model** — Which `actor` and `roleGates` combinations should appear as journey or operations lanes in your host?

Example: If your system supports 5 stage types, document them in your API reference or setup guide. Don't expose a "stage type selector" in the editor UI for undefined types. Likewise, if you want journey/operations labels in your host, derive them from the authored assignment fields in one place instead of persisting a second surface flag.

### Configuration That Can Stay in the UI (Minimal)

Only expose in the runtime UI what developers actually need to change:

- Workflow key selection (if hosting multiple workflows)
- Authoring API endpoint (if switching between dev/staging/prod)
- User credentials or authentication (if not handled at the application level)

The reference shell includes these because they're useful for testing. In production, many applications hard-code the authoring API endpoint and don't expose workflow selection at all.

### Configuration That Should NOT Appear in the UI

Keep these concerns out of the editor shell entirely:

- Runtime configuration (deadlines, escalation rules, operational policies)
- Database connection strings or secrets
- Feature flags or A/B testing toggles
- Analytics or monitoring settings

These belong in your application configuration, not the editor host. Preview and publish calls should receive the authored workflow contract only; keep editor-only lane hints out of those payloads.

---

## Building Your Own Host

If you need a custom host (for branding, custom workflows, or integration), follow this pattern:

### 1. Start with a thin wrapper

```typescript
import { LitElement, html } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import 'prism-workflow-editor';

@customElement('my-workflow-host')
export class MyWorkflowHost extends LitElement {
  @property() workflowKey = 'planning';
  @property() authoringApiBase = 'https://api.example.com/authoring';

  render() {
    return html`
      <prism-workflow-editor
        workflow-key="${this.workflowKey}"
        authoring-api-base="${this.authoringApiBase}">
      </prism-workflow-editor>
    `;
  }
}
```

### 2. Add only what you need

If you need workflow selection:

```typescript
private _handleWorkflowChange(key: string) {
  this.workflowKey = key;
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
      workflow-key="${this.workflowKey}"
      authoring-api-base="${this.authoringApiBase}">
    </prism-workflow-editor>
  `;
}
```

### 3. Stop there

Don't add:
- Form controls for authoring API endpoints (hard-code or use environment variables)
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

1. **Review the editor design:** Understand what the editor can do and what it can't in [Workflow Editor V1 Design](../design/workflow-editor-v1/README.md)
2. **Set up your authoring API:** See [Setting Up a Prism Workflow](./workflow-setup.md) for API contracts and examples
3. **Configure actions and forms:** Document your action catalog and forms engine integration for authors
4. **Test the workflow:** Use the editor's built-in validation and simulation features to verify your definitions
