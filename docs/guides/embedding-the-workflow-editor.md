# Embedding the Workflow Editor

A guide for integrators. Build a business app on top of Prism.

The workflow editor is Lit web component. It renders the visual editor. But it does not decide where your workflows live.

You tell the editor where workflows come from.

---

## What You Get

Prism ships two Lit elements:

- **`<prism-workflow-editor>`** — the visual editor (canvas, inspector, validation, history, simulation).
- **`<prism-workflow-editor-shell>`** — a wrapper that adds workflow selection and displays.

You drop them in your page. They render the editor.

The editor does not ship HTTP endpoints. It does not read your database. It does not know about your auth.

Your business app owns all that.

---

## What You Write

You write a class that implements `WorkflowSource`:

```typescript
export interface WorkflowSource {
  /** Returns every workflow the editor should let the author pick. */
  list(): Promise<WorkflowSummary[]>;

  /** Loads one authored workflow by its host-facing key. */
  load(key: string): Promise<AuthoredWorkflow>;

  /** Persists the authored workflow back to the host. The host enforces save permissions. */
  save(key: string, workflow: AuthoredWorkflow): Promise<void>;
}
```

The interface has three methods:

- **`list()`** — return a list of workflows. Each entry has a `workflowKey`, `definitionKey`, and `displayName`.
- **`load(key)`** — load one workflow by key. Return an `AuthoredWorkflow` object.
- **`save(key, workflow)`** — save the workflow. Your implementation enforces permissions. Reject the promise if the user cannot save.

Your implementation can talk to memory, a file system, a database, a blob store, or any HTTP API you want. The editor does not care.

### Example — Map-Backed Source (20 Lines)

Here is a source that keeps workflows in a JavaScript `Map`:

```typescript
import type { WorkflowSource, WorkflowSummary } from '@umbraco-prism/client/workflow-editor';
import type { AuthoredWorkflow } from '@umbraco-prism/client/workflow-editor';

export class MapBackedWorkflowSource implements WorkflowSource {
  private readonly workflows = new Map<string, AuthoredWorkflow>();

  constructor(seed: AuthoredWorkflow[] = []) {
    for (const workflow of seed) {
      this.workflows.set(workflow.definitionKey, workflow);
    }
  }

  async list(): Promise<WorkflowSummary[]> {
    return Array.from(this.workflows.entries()).map(([workflowKey, workflow]) => ({
      workflowKey,
      definitionKey: workflow.definitionKey,
      displayName: workflow.displayName,
    }));
  }

  async load(key: string): Promise<AuthoredWorkflow> {
    const workflow = this.workflows.get(key);
    if (!workflow) {
      throw new Error(`Workflow "${key}" not found.`);
    }
    return structuredClone(workflow);
  }

  async save(key: string, workflow: AuthoredWorkflow): Promise<void> {
    this.workflows.set(key, structuredClone(workflow));
  }
}
```

That is a complete implementation. It stores workflows in memory for the lifetime of the page. When the page reloads, they are gone.

For real persistence, replace the `Map` with a fetch call to your own backend.

---

## Wiring It Up

Create an instance of your source. Assign it to the editor element:

```javascript
import '@umbraco-prism/client/workflow-editor/prism-workflow-editor.js';
import { MapBackedWorkflowSource } from './map-backed-workflow-source.js';

const source = new MapBackedWorkflowSource([
  // seed with your workflows here
]);

const editor = document.querySelector('prism-workflow-editor');
editor.workflowSource = source;
```

That is all you need.

### Full HTML Example

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Workflow Editor</title>
</head>
<body>
  <prism-workflow-editor></prism-workflow-editor>

  <script type="module">
    import '@umbraco-prism/client/workflow-editor/prism-workflow-editor.js';
    import { MapBackedWorkflowSource } from './map-backed-workflow-source.js';

    const source = new MapBackedWorkflowSource();
    const editor = document.querySelector('prism-workflow-editor');
    editor.workflowSource = source;
  </script>
</body>
</html>
```

The editor loads. It calls `source.list()` to populate the workflow picker. When the user selects a workflow, it calls `source.load(key)`. When the user clicks Save, it calls `source.save(key, workflow)`.

---

## The Reference Implementation

Prism ships a reference business app called **MockBusinessApp**. It demonstrates the full pattern.

The source code lives here:

- **Frontend:** `src/UmbracoPrism.Client/src/workflow-editor/integrations/mockapp-workflow-source.ts`
- **Backend:** `src/UmbracoPrism.MockBusinessApp/Program.cs` (endpoints at `/mockapp/workflows/*`)

The `MockBusinessAppWorkflowSource` class is an HTTP-backed implementation of `WorkflowSource`. It calls three endpoints:

- `GET /mockapp/workflows` — list
- `GET /mockapp/workflows/{key}` — load
- `PUT /mockapp/workflows/{key}` — save

The MockBusinessApp server stores workflows in memory. It seeds four reference workflows at startup:

1. **planning** — Planning application workflow
2. **leave-request** — Leave request with 5 gateways (demonstrates fan-in pattern)
3. **community-enquiry** — Community enquiry form
4. **information-request** — Information request form

Those workflows persist in memory until the server restarts. This is a reference implementation. Your app owns the analogous code. You decide whether to use a database, blob storage, or something else.

**Key point:** MockBusinessApp is a **business-domain reference**. It is not the editor. The editor lives in `UmbracoPrism.Client`. MockBusinessApp is one example of a business app that uses the editor.

---

## Extending the Action Catalog

The editor ships a default catalog of generic actions (Send Email, Assign Case, etc.). Your business app can extend it.

Implement `WorkflowActionCatalog`:

```typescript
export interface WorkflowActionCatalog {
  entries(): Promise<ActionCatalogEntry[]>;
}
```

Each `ActionCatalogEntry` has:

- `type` — stable key (e.g., `"my-app.send-sms"`)
- `label` — display name for the editor
- `summary` — what the action does
- `appliesTo` — where the action is valid (`stage.onEntry`, `stage.onExit`, `transition`)
- `paramsSchema` — JSON Schema for parameters
- `defaultParams` — starter values

Example:

```typescript
import { BuiltInWorkflowActionCatalog, type ActionCatalogEntry } from '@umbraco-prism/client/workflow-editor';

export class MyAppActionCatalog extends BuiltInWorkflowActionCatalog {
  async entries(): Promise<ActionCatalogEntry[]> {
    const builtIn = await super.entries();
    return [
      ...builtIn,
      {
        type: 'my-app.send-sms',
        label: 'Send SMS',
        summary: 'Send an SMS notification via Twilio.',
        appliesTo: ['transition'],
        paramsSchema: {
          type: 'object',
          required: ['phoneNumber', 'message'],
          properties: {
            phoneNumber: { type: 'string' },
            message: { type: 'string' },
          },
        },
        defaultParams: { phoneNumber: '', message: '' },
        status: 'available',
      },
    ];
  }
}
```

Wire it up:

```javascript
const catalog = new MyAppActionCatalog();
editor.actionCatalog = catalog;
```

The editor will show your custom action in the dropdown. The editor validates parameters against the schema. At runtime, your business app executes the action.

---

## Author Context (Optional UX Hint)

The `WorkflowAuthorContext` interface lets you hint at save permissions:

```typescript
export interface WorkflowAuthorContext {
  /** When `false`, the editor disables the Save button. Defaults to enabled. */
  canSave?: boolean;

  /** Optional display name for the author currently viewing the editor. */
  displayName?: string;
}
```

This is a **UX hint only**. It is not enforcement. Your `WorkflowSource.save()` method is the only enforcement point. The editor just uses this to grey out the Save button early, so authors get a clear signal before they try to save.

Example:

```javascript
editor.authorContext = {
  canSave: false,
  displayName: 'Viewer (read-only)',
};
```

If you do not set `authorContext`, the editor assumes the user can save.

---

## Why There Is No HTTP API in Prism

Prism is **service-design tooling**. It helps you describe workflows. It does not run them. It does not store them.

Different business apps have different needs:

- **Storage:** One app uses PostgreSQL. Another uses Azure Blob. Another uses an in-memory cache.
- **Identity:** One app uses Entra ID. Another uses Keycloak. Another uses no auth at all (local dev).
- **Audit:** One app logs every save to a compliance system. Another does not care.
- **Multi-tenancy:** One app partitions workflows by tenant. Another does not have tenants.

Prism does not pick for you. It gives you `WorkflowSource`. You implement it. Your implementation knows your storage, your identity, your audit, your multi-tenancy.

That keeps Prism simple. That keeps your business logic where it belongs.

---

## The Two Domains

This is a **domain-driven design** boundary. Two domains:

### Service-Design Domain (Prism)

This is what Prism **is**:

- The visual editor (canvas, inspector, validation)
- The authored model (`AuthoredWorkflow`, `AuthoredStage`, `AuthoredGateway`, `AuthoredRoute`)
- The JSON schema (`authored-workflow.schema.json`)
- The validator (schema validation + structural linting)
- The projector (convert authored model to runtime model)
- The simulator (dry-run a workflow path)

All of this lives in `UmbracoPrism.Client` and `UmbracoPrism.Core`. It is domain-agnostic. It does not know about your business rules.

### Business Domain (Your App)

This is what **your app** owns:

- Storage (where do workflows live?)
- Identity (who can edit workflows?)
- Runtime instances (this customer is at stage 3)
- Roles (who can advance what?)
- Notifications (send email when a form is submitted)
- The actual UI presented to end users (the forms, the buttons, the confirmation pages)

Your app ships its own backend code. Your app ships its own frontend code. Your app uses Prism's editor to author workflows. Your app uses Prism's runtime to execute workflows. But your app owns the business logic.

### The Boundary

The interfaces are the boundary:

- **`WorkflowSource`** — the editor reads and writes workflows through this.
- **`WorkflowActionCatalog`** — the editor shows available actions through this.
- **`WorkflowAuthorContext`** — the editor reads save permissions through this.

Those three interfaces keep the domains separate. Prism never crosses into your business logic. Your business logic never crosses into Prism's service-design concerns.

---

## Next Steps

1. **Implement `WorkflowSource`** for your business app. Start with the `MapBackedWorkflowSource` example above, then replace the `Map` with your real storage.
2. **Mount the editor** in your host page. Use the HTML example above.
3. **Read the reference implementation** at `src/UmbracoPrism.Client/src/workflow-editor/integrations/mockapp-workflow-source.ts` and `src/UmbracoPrism.MockBusinessApp/Program.cs`.
4. **Extend the action catalog** if you have custom actions (SMS, API calls, etc.).
5. **Explore the authored model** at `src/UmbracoPrism.Client/src/workflow-editor/types.ts` to understand what `AuthoredWorkflow` contains.

---

## Related Documentation

- [Gateway-First Authoring](../walkthroughs/gateway-first-authoring.md) — how the gateway-and-route model works
- [Workflow Editor Composition](./workflow-editor-composition.md) — advanced patterns for custom hosts
- [Authoring a Workflow](../walkthroughs/authoring-a-workflow.md) — how to author workflows in the editor

---

[← Back to Guides](README.md)
