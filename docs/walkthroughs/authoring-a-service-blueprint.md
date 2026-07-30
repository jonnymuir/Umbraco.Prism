# Walkthrough — Wiring the Prism Service Blueprint Editor into Your Umbraco App

This walkthrough is for integrators starting from a working Umbraco v17 site. By the end, you will have:

- the Prism packages installed
- the authoring API and editor services registered in DI
- doctypes and templates that drive a service blueprint at runtime
- a clear picture of **where** the editor itself runs — and where it does not

The walkthrough does not cover the editor's UX. For that, see [Planning Service Blueprint Editor](planning-service-blueprint-editor.md).

---

## How the pieces fit

Prism splits cleanly into three projects:

| Project | What it does | Where it runs |
|---|---|---|
| `UmbracoPrism` (`UmbracoPrism.Core`) | Umbraco integration: route-hijacking controller, page model, member middleware, sanitiser, view helpers | Inside your Umbraco app |
| `Wayfinder.Editor` | Authoring API (`/api/service-blueprint-authoring/*`) and the web-component bundle that authors load | Authoring API on the server; web components in a separate business app |
| `Wayfinder.Engine` | The engine that advances cases through stages at runtime | Inside your business app (or your Umbraco app, if you co-host them) |

**The editor is not mounted in the Umbraco backoffice.** Squad ships it as web components that a separate business app embeds. In this repo, `MockBusinessApp` is the reference authoring host, and `TestSite` is the reference Umbraco runtime. This boundary is deliberate: the Umbraco backoffice stays a content tool; the service blueprint editor stays a developer/operator tool in your app.

---

## Step 1 — Install the packages

Add the Prism packages to your Umbraco project:

- `UmbracoPrism` — published to NuGet today; covers the Core integration.
- `Wayfinder.Editor` — the authoring API and web-component bundle. Reference the project in-repo, or the package when published.
- `Wayfinder.Engine` — the engine. Reference the project in-repo, or the package when published.

If you only need the **read-only viewer** for a published service blueprint on a public page, you can stop at `UmbracoPrism`. The viewer is a single web component (`<prism-service-blueprint-graph read-only>`) — see [Composing the Service Blueprint Editor](../guides/service-blueprint-editor-composition.md#read-only-public-viewer).

---

## Step 2 — Register Prism services and the ServiceBlueprintAuthor policy

In `Program.cs`, register Prism alongside Umbraco. The editor's authoring API is locked behind an authorization policy you own.

```csharp
using UmbracoPrism.Core.Extensions;
using Wayfinder.Editor.Extensions;
using Wayfinder.Engine.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPrismAuthentication(builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    // The editor's /api/service-blueprint-authoring/* routes require this policy.
    // Default: any authenticated principal. Tighten with your own claim or role gate.
    options.AddPolicy(
        ServiceBlueprintAuthoringPolicies.ServiceBlueprintAuthor,
        policy => policy.RequireAuthenticatedUser());
});

// Register the authoring API and supporting services.
builder.Services.AddPrismWorkflowEditor(
    authoredWorkflowBasePath: "/path/to/authored",
    publishedWorkflowBasePath: "/path/to/published");

var app = builder.Build();

// Map the authoring routes (group: /api/service-blueprint-authoring).
app.MapPrismWorkflowEditor();
```

Two things to know:

1. The `ServiceBlueprintAuthor` policy is required. If you skip it, every authoring request returns a 500 at startup. That is by design — the editor never trusts an unauthenticated caller.
2. The approver on every change is taken from the authenticated principal. The request body cannot set or spoof the approver. (Blathers' Slice 3c.)

---

## Step 3 — Define your doctypes

Service Blueprint runtime pages need three things from Umbraco: a doctype, a Razor template, and a member-aware identity. The reference doctypes in `MockBusinessApp` show one working shape — copy what fits, replace what does not.

Two starting points:

- **A service request hub** doctype that lists available service blueprints for a signed-in member.
- **A service blueprint page** doctype that hosts a single stage's form. Route-hijack this one.

You do not need to mirror the reference doctype names. The contract is the controller, not the schema.

---

## Step 4 — Route-hijack the service blueprint page

Subclass `ServiceRequestPageController<T>` for your service blueprint page doctype. The base class handles GET, POST, antiforgery, nonce binding, field collection, validation, and the post-redirect-get flow.

```csharp
using UmbracoPrism.Core.Controllers;
using UmbracoPrism.Core.Models.Service-Blueprint;

public class StagePageController(
    ILogger<RenderController> logger,
    ICompositeViewEngine compositeViewEngine,
    IUmbracoContextAccessor umbracoContextAccessor,
    IBusinessAppProcessManagerClient workflowClient,
    IPublishedValueFallback publishedValueFallback,
    IAntiforgery antiforgery,
    IStageNonceService nonceService,
    IServiceRequestFieldValidator fieldValidator)
    : ServiceRequestPageController<PrismServiceRequestViewModel>(
        logger, compositeViewEngine, umbracoContextAccessor,
        workflowClient, publishedValueFallback, antiforgery,
        nonceService, fieldValidator)
{
    // Override to pre-populate fields from member claims, or to add custom dispatch.
}
```

TestSite's `StagePageController` is the reference. It pre-populates a few fields from claims; everything else is base-class behaviour.

---

## Step 5 — Add the Razor templates

Each service blueprint page doctype needs a template that renders the current stage. TestSite has working examples — `serviceBlueprintDemoPage.cshtml` and `serviceRequestHub.cshtml` — that you can crib from. They use Prism's view helpers to render the stage shell, the field group, and the action buttons.

Keep templates thin. The base controller has already done the work; the template just renders the view model.

---

## Step 6 — Decide where to host the editor

The Prism service blueprint editor is shipped as web components. **Mount them in your business app, not in the Umbraco backoffice.**

In this repo:

- **MockBusinessApp** is the reference authoring host. It mounts `<prism-service-blueprint-editor>` (or `<prism-service-blueprint-editor-shell>`) on a normal page and points it at the authoring API.
- **TestSite** is the reference Umbraco runtime. It does not host the editor.

This split is the load-bearing boundary. The Umbraco backoffice stays for content; the editor stays in the place where your developers and operators already work. If you need to embed a *published* service blueprint as a read-only diagram on a public Umbraco page, use `<prism-service-blueprint-graph read-only>` — see [Composing the Service Blueprint Editor](../guides/service-blueprint-editor-composition.md#read-only-public-viewer).

A minimal mount in your business app:

```html
<prism-service-blueprint-editor
  service-blueprint-key="planning"
  authoring-api-base="https://your-umbraco-app/api/service-blueprint-authoring">
</prism-service-blueprint-editor>
```

The element is keyboard-reachable and announces edits to a polite live region. Accessibility is on by default — you do not need to add screen-reader scaffolding.

---

## Step 7 — Open the editor and use it

Once an author signs into your business app and loads the page that mounts `<prism-service-blueprint-editor>`, they can:

- pick a service blueprint
- edit stages and gateways in the **Canvas** tab (vertical lanes, top-to-bottom flow)
- read or edit the JSON in the **Definition** tab
- check warnings in the **Validation** tab
- save and publish through the authoring API

For a tour of the editor itself — what each tab does, how the lanes read, how the keyboard reach works — see [Planning Service Blueprint Editor](planning-service-blueprint-editor.md).

---

## Related guides

- **Editor composition and the read-only viewer:** [Composing the Service Blueprint Editor](../guides/service-blueprint-editor-composition.md)
- **Component API (public elements, attributes, events):** [`src/UmbracoPrism.Client/src/service-blueprint-editor/README.md`](../../src/UmbracoPrism.Client/src/service blueprint-editor/README.md)
- **Editor visual test contract:** [`docs/testing/service-blueprint-editor-visual-tests.md`](../testing/service-blueprint-editor-visual-tests.md)
- **Service Blueprint setup deep-dive:** [Setting Up a Prism Service Blueprint](../guides/service-blueprint-setup.md)
- **Reference service blueprint contract:** [Reference Service Blueprint Contract](../guides/reference-service-blueprint-contract.md)
