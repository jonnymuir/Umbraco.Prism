# Embedding the Service Blueprint Editor

A guide for integrators. Build a business app on top of Prism.

The service blueprint editor is Lit web component. It renders the visual editor. But it does not decide where your service blueprints live.

You tell the editor where service blueprints come from.

---

## What You Get

Prism ships two Lit elements:

- **`<prism-service-blueprint-editor>`** — the visual editor (canvas, inspector, validation, history, simulation).
- **`<prism-service-blueprint-editor-shell>`** — a wrapper that adds service blueprint selection and displays.

You drop them in your page. They render the editor.

The editor does not ship HTTP endpoints. It does not read your database. It does not know about your auth.

Your business app owns all that.

---

## What You Write

You write a class that implements `WorkflowSource`:

```typescript
export interface WorkflowSource {
  /** Returns every service-blueprint the editor should let the author pick. */
  list(): Promise<WorkflowSummary[]>;

  /** Loads one authored service-blueprint by its host-facing key. */
  load(key: string): Promise<AuthoredServiceBlueprint>;

  /** Persists the authored service-blueprint back to the host. The host enforces save permissions. */
  save(key: string, service-blueprint: AuthoredServiceBlueprint): Promise<void>;

  /** Optional: current persisted version, for proactive staleness polling. See below. */
  checkVersion?(key: string): Promise<number | null>;
}
```

The interface has three required methods, plus one optional one:

- **`list()`** — return a list of service blueprints. Each entry has a `workflowKey`, `definitionKey`, and `displayName`.
- **`load(key)`** — load one service blueprint by key. Return an `AuthoredServiceBlueprint` object.
- **`save(key, service-blueprint)`** — save the service blueprint. Your implementation enforces permissions. Reject the promise if the user cannot save. If your host also exposes this service blueprint to AI agents (see the
  [AI-Ready Service Blueprint Authoring guide](./ai-service-blueprint-authoring.md)), a human and an agent can edit the same service blueprint at once — `save` should reject with a `WorkflowSaveError` whose `isConflict: true` when `service-blueprint.version` no longer matches what's persisted, so the editor can show its built-in "changed elsewhere, reload" affordance instead of silently overwriting the other side's change.
- **`checkVersion(key)`** *(optional)* — return the currently-persisted version. If you implement it, the editor polls every 15s while a service blueprint is open and toasts a heads-up (with its own Reload action) before the author even tries to save. Skip it and you just don't get proactive detection — `save`'s conflict handling still works either way.

Your implementation can talk to memory, a file system, a database, a blob store, or any HTTP API you want. The editor does not care.

### Example — Map-Backed Source (20 Lines)

Here is a source that keeps service blueprints in a JavaScript `Map`:

```typescript
import type { WorkflowSource, WorkflowSummary } from '@umbraco-prism/client/service-blueprint-editor';
import type { AuthoredServiceBlueprint } from '@umbraco-prism/client/service-blueprint-editor';

export class MapBackedWorkflowSource implements WorkflowSource {
  private readonly service-blueprints = new Map<string, AuthoredServiceBlueprint>();

  constructor(seed: AuthoredServiceBlueprint[] = []) {
    for (const service-blueprint of seed) {
      this.service-blueprints.set(service-blueprint.definitionKey, service-blueprint);
    }
  }

  async list(): Promise<WorkflowSummary[]> {
    return Array.from(this.service-blueprints.entries()).map(([workflowKey, service-blueprint]) => ({
      workflowKey,
      definitionKey: service-blueprint.definitionKey,
      displayName: service-blueprint.displayName,
    }));
  }

  async load(key: string): Promise<AuthoredServiceBlueprint> {
    const service-blueprint = this.service-blueprints.get(key);
    if (!service-blueprint) {
      throw new Error(`Service-Blueprint "${key}" not found.`);
    }
    return structuredClone(service-blueprint);
  }

  async save(key: string, service-blueprint: AuthoredServiceBlueprint): Promise<void> {
    this.service-blueprints.set(key, structuredClone(service-blueprint));
  }
}
```

That is a complete implementation. It stores service blueprints in memory for the lifetime of the page. When the page reloads, they are gone.

For real persistence, replace the `Map` with a fetch call to your own backend.

---

## Wiring It Up

Create an instance of your source. Assign it to the editor element:

```javascript
import '@umbraco-prism/client/service-blueprint-editor/prism-service-blueprint-editor.js';
import { MapBackedWorkflowSource } from './map-backed-service-blueprint-source.js';

const source = new MapBackedWorkflowSource([
  // seed with your service-blueprints here
]);

const editor = document.querySelector('prism-service-blueprint-editor');
editor.workflowSource = source;
```

That is all you need.

### Full HTML Example

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Service-Blueprint Editor</title>
</head>
<body>
  <prism-service-blueprint-editor></prism-service-blueprint-editor>

  <script type="module">
    import '@umbraco-prism/client/service-blueprint-editor/prism-service-blueprint-editor.js';
    import { MapBackedWorkflowSource } from './map-backed-service-blueprint-source.js';

    const source = new MapBackedWorkflowSource();
    const editor = document.querySelector('prism-service-blueprint-editor');
    editor.workflowSource = source;
  </script>
</body>
</html>
```

The editor loads. It calls `source.list()` to populate the service blueprint picker. When the user selects a service blueprint, it calls `source.load(key)`. When the user clicks Save, it calls `source.save(key, service-blueprint)`.

---

## The Reference Implementation

Prism ships a reference business app called **MockBusinessApp**. It demonstrates the full pattern.

The source code lives here:

- **Frontend:** `src/UmbracoPrism.Client/src/service-blueprint-editor/integrations/mockapp-service-blueprint-source.ts`
- **Backend:** `src/UmbracoPrism.MockBusinessApp/Program.cs` (endpoints at `/mockapp/service-blueprints/*`)

The `MockBusinessAppWorkflowSource` class is an HTTP-backed implementation of `WorkflowSource`. It calls three endpoints:

- `GET /mockapp/service-blueprints` — list
- `GET /mockapp/service-blueprints/{key}` — load
- `PUT /mockapp/service-blueprints/{key}` — save

The MockBusinessApp server stores service blueprints in memory. It seeds four reference service blueprints at startup:

1. **planning** — Planning application service blueprint
2. **leave-request** — Leave request with 5 gateways (demonstrates fan-in pattern)
3. **community-enquiry** — Community enquiry form
4. **information-request** — Information request form

Those service blueprints persist in memory until the server restarts. This is a reference implementation. Your app owns the analogous code. You decide whether to use a database, blob storage, or something else.

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
import { BuiltInWorkflowActionCatalog, type ActionCatalogEntry } from '@umbraco-prism/client/service-blueprint-editor';

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

Prism is **service-design tooling**. It helps you describe service blueprints. It does not run them. It does not store them.

Different business apps have different needs:

- **Storage:** One app uses PostgreSQL. Another uses Azure Blob. Another uses an in-memory cache.
- **Identity:** One app uses Entra ID. Another uses Keycloak. Another uses no auth at all (local dev).
- **Audit:** One app logs every save to a compliance system. Another does not care.
- **Multi-tenancy:** One app partitions service blueprints by tenant. Another does not have tenants.

Prism does not pick for you. It gives you `WorkflowSource`. You implement it. Your implementation knows your storage, your identity, your audit, your multi-tenancy.

That keeps Prism simple. That keeps your business logic where it belongs.

---

## The Two Domains

This is a **domain-driven design** boundary. Two domains:

### Service-Design Domain (Prism)

This is what Prism **is**:

- The visual editor (canvas, inspector, validation)
- The authored model (`AuthoredServiceBlueprint`, `AuthoredTouchpoint`, `AuthoredGateway`, `AuthoredRoute`)
- The JSON schema (`authored-service-blueprint.schema.json`)
- The validator (schema validation + structural linting)
- The projector (convert authored model to runtime model)
- The simulator (dry-run a service blueprint path)

All of this lives in `UmbracoPrism.Client` and `UmbracoPrism.Core`. It is domain-agnostic. It does not know about your business rules.

### Business Domain (Your App)

This is what **your app** owns:

- Storage (where do service blueprints live?)
- Identity (who can edit service blueprints?)
- Runtime instances (this customer is at stage 3)
- Roles (who can advance what?)
- Notifications (send email when a form is submitted)
- The actual UI presented to end users (the forms, the buttons, the confirmation pages)

Your app ships its own backend code. Your app ships its own frontend code. Your app uses Prism's editor to author service blueprints. Your app uses Prism's runtime to execute service blueprints. But your app owns the business logic.

### The Boundary

The interfaces are the boundary:

- **`WorkflowSource`** — the editor reads and writes service blueprints through this.
- **`WorkflowActionCatalog`** — the editor shows available actions through this.
- **`WorkflowAuthorContext`** — the editor reads save permissions through this.

Those three interfaces keep the domains separate. Prism never crosses into your business logic. Your business logic never crosses into Prism's service-design concerns.

---

## Next Steps

1. **Implement `WorkflowSource`** for your business app. Start with the `MapBackedWorkflowSource` example above, then replace the `Map` with your real storage.
2. **Mount the editor** in your host page. Use the HTML example above.
3. **Read the reference implementation** at `src/UmbracoPrism.Client/src/service-blueprint-editor/integrations/mockapp-service-blueprint-source.ts` and `src/UmbracoPrism.MockBusinessApp/Program.cs`.
4. **Extend the action catalog** if you have custom actions (SMS, API calls, etc.).
5. **Explore the authored model** at `src/UmbracoPrism.Client/src/service-blueprint-editor/types.ts` to understand what `AuthoredServiceBlueprint` contains.

---

## Related Documentation

- [Gateway-First Authoring](../walkthroughs/gateway-first-authoring.md) — how the gateway-and-route model works
- [Service Blueprint Editor Composition](./service-blueprint-editor-composition.md) — advanced patterns for custom hosts
- [Authoring a Service Blueprint](../walkthroughs/authoring-a-service-blueprint.md) — how to author service blueprints in the editor

---

[← Back to Guides](README.md)
