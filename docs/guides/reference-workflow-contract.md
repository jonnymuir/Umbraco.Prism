# Reference Workflow Contract

The **reference business app** (`src/UmbracoPrism.MockBusinessApp`) demonstrates how Umbraco Prism integrates with a downstream application. A key contract that this reference app establishes is the **four-workflow contract**: exactly four demo workflows are seeded at runtime and are available consistently across the editor, front-end, and runtime engine.

---

## The Four Reference Workflows

The reference app seeds exactly these four workflows:

| Workflow Key | Display Name | Use Case |
|---|---|---|
| `planning` | Planning Application | Multi-stage planning application with identity verification and submission approval |
| `community-enquiry` | Get in Touch | Simple contact form for community enquiries |
| `information-request` | Information Request | Data request form with urgency options |
| `payment-demo` | Payment Demo | Two-stage workflow demonstrating form entry and payment handling |

All four workflows are:
- **Authored** — defined in `src/UmbracoPrism.MockBusinessApp/workflow-authored/` as structured JSON (e.g., `planning.workflow.json`)
- **Seeded at startup** — loaded by `ReferenceWorkflowRepository.GetReferenceWorkflows()` and projected into the runtime engine
- **Available to the editor** — accessible via `/api/workflow-authoring/workflows` for authoring and editing
- **Available to the front-end** — rendered as workflow entry points in the member dashboard
- **Available to the runtime** — executed by the workflow engine with the same definition across all surfaces

---

## Where Workflows Are Defined

### Authored Workflows (Single Source of Truth)

**Location:** `src/UmbracoPrism.MockBusinessApp/workflow-authored/`

Each workflow is a JSON file (e.g., `planning.workflow.json`) that defines:
- Workflow metadata (name, description, version)
- Stages (form pages, confirmation screens, etc.)
- Transitions (how the workflow moves between stages)
- Fields (input forms, validation, conditional logic)
- Actions (side effects like form submission or payment processing)

These authored files are the **authoritative source**. They are never edited directly through the runtime; instead, they flow through:

1. **Authoring** (editor surface) → Makes changes to the authored JSON
2. **Publishing** (authoring API) → Validates and confirms the authored version
3. **Projection** (startup or on-publish) → Converts authored format → runtime format
4. **Runtime** (workflow engine) → Executes the projected definition

### Reference Implementation (Code)

**Location:** `src/UmbracoPrism.MockBusinessApp/Services/ReferenceWorkflowRepository.cs`

The `ReferenceWorkflowRepository` class provides a C# reference implementation that seeds the four workflows if no authored files are available (e.g., during testing). This ensures the contract is always satisfied, even if authored files are temporarily missing.

```csharp
public static IReadOnlyList<KeyValuePair<string, AuthoredWorkflow>> GetReferenceWorkflows()
{
    return
    [
        new KeyValuePair<string, AuthoredWorkflow>("planning", PlanningWorkflow()),
        new KeyValuePair<string, AuthoredWorkflow>("community-enquiry", CommunityEnquiryWorkflow()),
        new KeyValuePair<string, AuthoredWorkflow>("information-request", InformationRequestWorkflow()),
        new KeyValuePair<string, AuthoredWorkflow>("payment-demo", PaymentDemoWorkflow())
    ];
}
```

---

## How Workflows Are Seeded at Runtime

### Startup Sequence

When the MockBusinessApp starts:

1. **AuthoredWorkflowStore initialized** — reads from `workflow-authored/` directory
2. **Reference fallback seeded** — `ReferenceWorkflowRepository.GetReferenceWorkflows()` ensures the four workflows are available
3. **Projection runs** — Each authored workflow is projected into the runtime format
4. **Runtime store populated** — All four projected definitions are loaded into the `IWorkflowDefinitionStore`
5. **Authoring API ready** — Editors can load/edit workflows via `/api/workflow-authoring/workflows`

### Code Integration Points

**Program.cs:**

```csharp
// Seed exactly 4 demo workflows in memory (planning, community-enquiry,
// information-request, payment-demo). All 4 are available to the editor,
// front-end, and runtime. Downstream apps replace ReferenceWorkflowRepository
// with their own authored workflow store (filesystem, database, etc.).

var publishedWorkflowPath = Path.Combine(builder.Environment.ContentRootPath, "workflow-seeds");
builder.Services.AddSingleton<IAuthoredWorkflowStore>(
    _ => new InMemoryAuthoredWorkflowStore(ReferenceWorkflowRepository.GetReferenceWorkflows()));

builder.Services.AddSingleton<IPublishedWorkflowStore, InMemoryRuntimePublishedWorkflowStore>();
builder.Services.AddSingleton<IWorkflowDefinitionStore, ReferenceWorkflowDefinitionStore>();
```

---

## The Repository Seam: Downstream Customization

This reference implementation demonstrates the **repository seam** — the boundary where your own application takes over workflow management.

### For the Reference App

**What it does:**
- Uses `ReferenceWorkflowRepository` and `ReferenceWorkflowDefinitionStore` to seed four static workflows
- All workflows are loaded at startup from the authored directory or the reference code
- No changes are persisted to disk

### For Downstream Applications

**What you should do:**
- Replace `ReferenceWorkflowRepository` with your own `IAuthoredWorkflowStore` implementation
- Implement your own `IWorkflowDefinitionStore` for runtime execution
- Connect to your workflow storage (database, filesystem, API, etc.)
- Maintain the same interface contract — workflows are still loaded at startup and available to editor/runtime

**Example:**

```csharp
// Your downstream app
builder.Services.AddSingleton<IAuthoredWorkflowStore>(
    sp => new YourDatabaseWorkflowStore(sp.GetRequiredService<IConfiguration>()));

builder.Services.AddSingleton<IWorkflowDefinitionStore, YourDatabaseWorkflowDefinitionStore>();
```

---

## Verifying the Contract: End-to-End Tests

The four-workflow contract is verified by automated end-to-end tests that run on every PR:

### Backend Contract Tests

**File:** `src/UmbracoPrism.Core.Tests/FourWorkflowReferenceContractTests.cs`

Tests verify:
- Authoring API lists exactly 4 workflows
- All 4 workflows are loadable via the authoring API
- Runtime store publishes exactly 4 workflows at startup
- Admin screen shows exactly 4 workflow definitions
- All 4 workflows have editor links (proving authored lineage)
- Workflow keys match across authoring and admin surfaces

### Frontend Contract Tests

**File:** `src/UmbracoPrism.Client/tests/four-workflow-contract.spec.ts`

Tests verify:
- Admin screen DOM shows exactly 4 workflows
- All 4 have visible editor links
- Authoring API returns exactly 4 in the workflow list
- All 4 are loadable via the authoring API

### Behavioral Tests

**Runtime behavior:** Existing end-to-end tests (e.g., `workflow-gds-journey.spec.ts`) verify that all workflows execute correctly in the runtime with real form submission, state transitions, and user interactions.

**Editor behavior:** Editor-facing tests (e.g., `01-planning-workflow-editor.walkthrough.spec.ts`) verify that the editor can load each workflow, display its graph, validate it, and save changes.

---

## Why This Matters

1. **Clarity** — Developers know exactly what's included in the reference app and what they need to replace
2. **Consistency** — Workflows are defined once (in the authored directory) and flow consistently through all surfaces
3. **Testability** — Contract tests fail immediately if someone adds/removes workflows or if workflows drift across surfaces
4. **Regression Prevention** — Changes to workflow structure are caught early

---

## Quick Reference

| Question | Answer |
|---|---|
| How many workflows are seeded? | Exactly 4: planning, community-enquiry, information-request, payment-demo |
| Where are they defined? | `src/UmbracoPrism.MockBusinessApp/workflow-authored/` (JSON) + `ReferenceWorkflowRepository.cs` (C# fallback) |
| When are they seeded? | At application startup (MockBusinessApp `Program.cs`) |
| Are they the same across editor/runtime/front-end? | Yes — all consume the same authored source through projection |
| Can I use different workflows in my app? | Yes — replace `ReferenceWorkflowRepository` and `ReferenceWorkflowDefinitionStore` with your own implementations |
| How is the contract verified? | Backend + frontend end-to-end tests check exactly 4 workflows are present and consistent |
| What if I add a 5th workflow? | Contract tests will fail, alerting you to update the reference and tests |

---

## Related Resources

- [Reference Business App README](../../src/UmbracoPrism.MockBusinessApp/README.md) — Configuration and setup
- [Workflow Authoring Guide](authoring-a-workflow.md) — How to define workflows in JSON
- [Workflow Walkthroughs](../walkthroughs/) — Step-by-step guides for each of the four workflows
- [Workflow Forms Engine](../design/workflow-forms-engine.md) — Deep dive into forms integration

---

**Last updated:** 2026-05-19T23:15:48.767+01:00
