# Umbraco Integration

A guide for integrators. Embed Prism workflows in your Umbraco site.

This guide shows how the reference implementation (TestSite + MockBusinessApp) integrates Prism. Your Umbraco site follows the same pattern.

---

## The Two Surfaces

Prism workflows run on two surfaces:

1. **Member Surface (Umbraco)** — authenticated users fill in forms and see their workflow progress. This is where the end-user journey happens.
2. **Business App (separate host)** — where you author workflows and where reviewers process submissions. MockBusinessApp is the reference.

The member surface lives in your Umbraco site. The business app is a separate ASP.NET host.

---

## Member Surface — What You Get

Prism ships two document types for the member surface:

- **`workflowPage`** — a single workflow entry point. Each workflow gets one `workflowPage` node in your content tree.
- **`workflowHub`** — a dashboard showing all workflows the user has started. One hub node per site.

These document types are seeded automatically by `PrismContentTypeSeeder`. You do not create them manually.

### How It Works

1. You create a `workflowPage` node in the Umbraco tree.
2. You set the `workflowKey` property to match a workflow key your business app knows about (e.g., `"planning"`, `"leave-request"`).
3. Users navigate to that page. Prism loads the workflow definition from your business app and renders the form.
4. Users fill in the form. Prism saves their progress to an instance store.
5. Users submit. Prism moves the instance to the next stage (or a waiting state if a reviewer is required).

### Route Hijacking

Prism uses **route hijacking** to intercept requests to `workflowPage` and `workflowHub` nodes. You do not write Razor templates for these pages. Prism ships embedded templates.

The controllers:

- **`WorkflowPageController`** (in your site) — extends `PrismWorkflowPageController<TViewModel>` from Core.
- **`WorkflowHubController`** (in Core) — already implemented.

Both extend `RenderController`. Both require authentication via `[Authorize(AuthenticationSchemes = "PrismMemberCookie")]`.

Your site can override `PrePopulateFields()` in `WorkflowPageController` to inject claims-based data (e.g., email, name) into read-only fields.

---

## Business App — Where Workflows Are Authored

The workflow editor lives in a **separate business app**, not in the Umbraco backoffice.

MockBusinessApp (`src/UmbracoPrism.MockBusinessApp/`) is the reference implementation. It:

- Hosts the workflow editor at `/admin/workflow-editor` (dev-only).
- Exposes `/mockapp/workflows/*` endpoints for the editor to read/write workflows.
- Seeds four reference workflows at startup.
- Runs the workflow runtime engine (for reviewers to process submissions).

Your business app follows the same pattern. You implement `WorkflowSource` to expose your workflows to the editor. See [Embedding the Workflow Editor](./embedding-the-workflow-editor.md) for details.

---

## The Contract Between the Two

The member surface and the business app communicate via:

1. **Workflow definitions** — your business app projects an `AuthoredWorkflow` into a `WorkflowDefinitionFile` (via `IWorkflowProjector`). The member surface loads this runtime definition and renders it.
2. **Workflow instances** — the member surface persists instance state (which stage the user is on, which fields they have filled in). The business app reads this state when reviewers process submissions.

The boundary is clean. The member surface never talks to the editor. The editor never talks to the member surface.

---

## Authentication and Roles

The member surface uses **Prism Member Cookie** authentication. Users log in via OIDC (Keycloak, Entra ID, etc.). Prism validates the cookie and resolves the user's identity.

The business app uses its own authentication. MockBusinessApp has no authentication (dev-only). A production business app would use bearer tokens, OIDC, or whatever your organization requires.

**Role-gated transitions:** Workflows can have transitions that require a role (e.g., `requiresRole: "reviewer"`). The member surface enforces this at the HTTP layer. The business app enforces it at the handler layer.

---

## Public Entry Points (Optional)

Some workflows start from public pages (no login required). You create a public content node (any document type) with a link to the protected `workflowPage`. When an anonymous user clicks the link, they get a login challenge.

Example:

```html
<a href="@linkedWorkflowPage.Url()">Start your application</a>
```

The `linkedWorkflowPage` is a content picker property pointing at a `workflowPage` node. Umbraco resolves the URL. The challenge redirect happens automatically.

---

## Where Workflows Are Stored

Prism does **not** store workflows in the Umbraco database. Workflows live in your business app.

The reference implementation (MockBusinessApp) stores workflows in memory. A production business app would use a database, blob storage, or whatever your organization requires.

The member surface never reads authored workflows directly. It only reads projected runtime definitions (via the workflow engine).

---

## Deploying the Business App

The business app is a separate ASP.NET host. Deploy it alongside your Umbraco site. It needs:

- A `/workflows/*` HTTP endpoint (or equivalent) for the editor to call.
- A workflow runtime engine (Prism ships `UmbracoPrism.WorkflowRuntime` as a reference).
- Storage for authored workflows and workflow instances.

MockBusinessApp demonstrates the pattern. Your business app is analogous.

---

## Next Steps

1. **Read the embedding guide:** [Embedding the Workflow Editor](./embedding-the-workflow-editor.md)
2. **Explore the reference implementation:** `src/UmbracoPrism.MockBusinessApp/`
3. **Set up your first workflow:** [Setting Up a Prism Workflow](./workflow-setup.md)
4. **Understand the runtime model:** [Runtime Projection](../design/workflow-editor-v1/02-runtime-projection.md)

---

[← Back to Guides](README.md)
