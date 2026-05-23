# Composing the Workflow Editor into Your Application

This guide shows how to embed the workflow editor into your own application with minimal complexity.

**For context:**
- **Understanding the editor design?** See [Workflow Editor V1 Design](../design/workflow-editor-v1/README.md)
- **Setting up workflows in Umbraco?** See [Setting Up a Prism Workflow](./workflow-setup.md)

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
- **Stage types** — What stage types (form, review, decision, waiting, etc.) are available?
- **Role definitions** — What actor roles exist in your system?

Example: If your system supports 5 stage types, document them in your API reference or setup guide. Don't expose a "stage type selector" in the editor UI for undefined types.

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

These belong in your application configuration, not the editor host.

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

## Next Steps

1. **Review the editor design:** Understand what the editor can do and what it can't in [Workflow Editor V1 Design](../design/workflow-editor-v1/README.md)
2. **Set up your authoring API:** See [Setting Up a Prism Workflow](./workflow-setup.md) for API contracts and examples
3. **Configure actions and forms:** Document your action catalog and forms engine integration for authors
4. **Test the workflow:** Use the editor's built-in validation and simulation features to verify your definitions
