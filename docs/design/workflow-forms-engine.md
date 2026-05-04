# Workflow forms engine overview

Umbraco.Prism gives you a workflow rendering package for Umbraco sites that delegate state, transitions, and business rules to a separate business application. Prism owns the page plumbing, rendering, validation, and security boundaries; your business app owns the workflow definition and decides what happens next.

## What the package gives you

- A **workflow page** pattern built around `PrismWorkflowPageController<TViewModel>`.
- A **workflow hub** page that lists in-progress and completed instances.
- A **component-based workflow schema** (`PrismComponent`) shared between authored definitions and rendered payloads.
- Built-in **nonce-backed structural validation**, **optimistic concurrency**, and **HTML sanitization**.
- A thin **HTTP client** (`IBusinessAppWorkflowClient`) that forwards the authenticated member token to your business app.

## Architecture

```mermaid
flowchart LR
    A[Workflow definition\nJSON seed or fluent builder] --> B[Business app workflow engine]
    B --> C[WorkflowResponseEnvelope]
    C --> D[PrismWorkflowPageController]
    D --> E[Razor workflow shells and Prism components]
    E --> F[Browser form submission]
    F --> D
    D --> G[Nonce + field validation]
    G --> B
    H[Workflow hub page] --> B
```

## The implementation story

1. **Author a definition** using `WorkflowDefinitionFile` JSON or `WorkflowDefinitionBuilder`.
2. **Expose workflow endpoints** in the business app (`/api/workflow/{workflowKey}/current`, `/advance`, `/instances`).
3. **Register Prism workflow services** in Umbraco with `AddPrismWorkflowEngine()`.
4. **Create a `workflowPage` node** and set its `workflowKey` property.
5. **Let Prism do the web work**: GET current state, render components, validate POSTs, then round-trip back to the business app.

The rest of this doc set expands each step in order.

## Canonical vocabulary

| Term | Meaning in the current package |
| --- | --- |
| Workflow definition | A `WorkflowDefinitionFile` with a key, initial state, states, transitions, and instance policy |
| State | A `StepDefinition` keyed by `stateKey`, containing authored `PrismComponent` values |
| Component | A polymorphic `PrismComponent` such as `fieldset`, `summary-list`, `waiting`, `body`, or `radio` |
| Step type | The shell Prism renders: `question`, `check-answers`, `confirmation`, `status-timeline`, or `task-list` |
| Response state | What the client should do next: usually `render`, `defer`, `complete`, or `error` |
| Instance policy | Whether a workflow is single-instance, multi-instance, or prompt-on-reentry |

## Step types

Prism infers step type from the authored components in a state.

| Step type | Inferred when the state contains | Typical use |
| --- | --- | --- |
| `question` | Regular interactive inputs or informational content without a specialised shell | Data collection |
| `check-answers` | `SummaryListComponent` | Final review before submit |
| `confirmation` | `PanelComponent` | End state / success page |
| `status-timeline` | `WaitingComponent`, or a read-only shell with no actions | Processing / review status |
| `task-list` | `TaskListComponent` | Multi-task journeys |

Source: `src/UmbracoPrism.Shared/Extensions/PrismComponentExtensions.cs`.

For waiting journeys, the envelope step type is still inferred from the waiting component, but the Razor layer promotes that state to the dedicated `waiting` shell when waiting metadata is present.

## Response states you should design for

| Response state | Meaning | Common source |
| --- | --- | --- |
| `render` | Render the returned step immediately | Question and check-answers states |
| `defer` | Stay on a waiting/status step and poll again after `PollAfterMs` | `WaitingComponent` |
| `complete` | Show the completion shell | Confirmation states |
| `error` | Fatal problem — definition missing, access denied, etc. | Client or engine error paths |

The demo business app currently adds two useful extensions:

- `instance_picker` when `instancePolicy = "prompt"` and the user already has an active instance.
- `validation_error` when domain rules fail after structural validation has already passed.

## Component families

- **Containers:** `fieldset`, `accordion`, `summary-list`, `task-list`
- **Inputs:** `text`, `number`, `decimal`, `select`, `radio`, `checkboxlist`, `date`, `email`, `textarea`, `boolean` (plus `tel` in the fluent builder)
- **Content:** `body`, `heading`, `inset-text`, `warning-text`, `details`, `notification-banner`, `panel`, `waiting`

The important shift is that authored definitions and rendered payloads now tell the same story: workflows are component trees, not ad-hoc field-group dumps.

## Where to go next

- Want the first implementation path? Read [Building a workflow](./workflow-forms-engine-demo.md).
- Need the exact schema and payload rules? Read [Backend authoring and contracts](./workflow-forms-engine-backend.md).
- Wiring Prism into Umbraco? Read [Umbraco integration](./workflow-forms-engine-umbraco.md).
