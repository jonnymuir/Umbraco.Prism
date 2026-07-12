# Reference Workflow Contract

The technical specification for `WorkflowDefinitionFile` — the JSON contract every
Prism workflow is authored in, whether by a human in the visual editor or an AI
agent through the [MCP/REST authoring toolkit](./ai-workflow-authoring.md). This is
the shape you read from `read_workflow`, write for `save_workflow`, and check
against `validate_workflow`.

This document is also exposed as an MCP resource (`workflow-docs://authoring-guide`)
so an agent can fetch it directly without needing filesystem access to this repo.

For the embedded expression language used in `calculations` and `showWhen`, see
[The Prism Calculation Language](./calculation-language.md).

---

## Top-level shape

```jsonc
{
  "definitionKey": "money-modeller",   // stable identifier; used to read/save/route to this workflow
  "displayName": "Money Modeller",
  "version": 1,                        // optimistic-concurrency version — see "Saving and conflicts" below
  "description": "...",                // optional
  "initialState": "choose-start",      // must match a states[].stateKey
  "instancePolicy": "single",          // "single" (one active instance per user) or "multiple"
  "queues": [ /* WorkflowQueueDefinition[] — see Queues */ ],
  "states": [ /* StepDefinition[] — see States and routes */ ],
  "gateways": [ /* WorkflowGatewayDefinition[] — see Gateways and routing */ ],
  "calculations": { /* WorkflowCalculationSet — see calculation-language.md */ },
  "handoffs": [ /* optional, actor-change annotations */ ],
  "tags": { "key": "value" },          // optional, free-form
  "layout": { /* editor-owned canvas positions — the runtime never reads this */ }
}
```

## Queues

Host apps decide what queues exist and who can access them — the shared runtime
does **not** enforce queue-level access control, that's always the host's
responsibility. A queue is:

```json
{ "key": "web-user", "displayName": "Member", "description": "...", "actor": "member", "roleGates": ["..."] }
```

Every state and gateway declares which queue it belongs to via `queueKey`.
`money-modeller.json`, for example, has a `web-user` queue (the member modelling
their own benefits) and a `business-user` queue (scheme administrators reviewing a
formal quote request) — two independent perspectives on the same workflow instance.

## States and routes

A state (`StepDefinition`) is one stage of the workflow:

```json
{
  "stateKey": "model",
  "displayName": "Your money, modelled",
  "stageType": "Question",
  "actor": "member",
  "queueKey": "web-user",
  "roleGates": ["..."],
  "components": [ /* PrismComponent[] — see Components */ ],
  "routes": [
    { "id": "model--recalculate--recalculate-loop", "target": "recalculate-loop", "trigger": "recalculate", "label": "Recalculate", "style": "secondary" }
  ]
}
```

- **`components`** are what renders on this stage — see [Components](#components).
- **`routes`** are the actions available from this stage. Each route's `trigger` is
  the action key the client submits to advance; `target` is where it goes next.

### The gateway routing rule

**A state's routes must always target a gateway, never another state directly.**
Gateway routes, in turn, may target either a state or another gateway. This is
enforced by `WorkflowDefinitionFile.ValidateGatewayRouting()` — called by
`validate_workflow`/`save_workflow` — and is not optional: a route from a state
straight to another state is always a validation error. Even the simplest
one-route stage needs a trivial pass-through gateway between it and its
destination (see `to-model-from-record` in `money-modeller.json`, a `Split`
gateway with a single `continue` route). This uniform shape is what lets a single
gateway later grow branching or join logic without restructuring every state that
points at it.

## Gateways and routing

A gateway (`WorkflowGatewayDefinition`) is a routing node — not a rendered stage:

```json
{
  "key": "fan-out-quote-request",
  "displayName": "Send quote request",
  "gatewayType": "Split",
  "queueKey": "web-user",
  "routes": [
    { "id": "...", "target": "quote-requested", "trigger": "continue" },
    { "id": "...", "target": "review-quote-request", "trigger": "continue" }
  ]
}
```

- **`gatewayType`** is `"Split"` (fan out — one incoming path, one or more outgoing;
  multiple routes from a Split gateway all fire, e.g. sending the member to a
  confirmation screen *and* routing a copy to the reviewer queue) or `"Join"`
  (converge multiple incoming cursors before proceeding — carries additional
  `waiting*` fields: `waitingContent`, `waitingExpectedSeconds`,
  `waitingPollIntervalMs`, `waitingAllowDefer`, `waitingDeferMessage`,
  `requiredIncomingQueues`).
- A route's `trigger` on a gateway is typically `"continue"` — gateways aren't
  usually waiting on user choice the way a state's routes are, they're evaluating
  where an already-triggered action goes next.

## Response states

Every runtime response (`WorkflowResponseEnvelope`, what `simulate_workflow`
returns per step) carries a `responseState` — what the client should do next:

| Value | Meaning |
|---|---|
| `render` | Show the current stage — `Render` carries `StepContent` (components, available actions). |
| `defer` | Wait and poll again — `PollAfterMs` says how long. Used at Join gateways waiting on other cursors. |
| `complete` | The workflow instance has finished. |
| `error` | Something went wrong — check `Problems`. |

## Components

`states[].components` is a list of `PrismComponent` — a polymorphic type
discriminated by `"type"`. The full catalog:

**Input components** (declare a `fieldKey`, participate in the calculation scope —
see [calculation-language.md](./calculation-language.md#where-it-lives-in-a-workflow)):
`text`, `number`, `decimal`, `select`, `radio`, `checkboxlist`, `date`, `email`,
`textarea`, `boolean`, `slider`.

**Content components** (no `fieldKey`, purely presentational):
`body`, `heading`, `inset-text`, `warning-text`, `details`, `notification-banner`,
`panel`.

**Structural components** (contain other components):
`fieldset`, `accordion`.

**Data-display components** (bind to calculated values):
`summary-list`, `task-list`, `stat-group` (binds items by `fieldKey` to calculated
fields), `chart` (binds to a `calculations.series` entry).

**Flow-control component**: `waiting` (used at Join gateways).

An input component (`text`, `number`, etc.) never displays a calculated value, however
it's labelled — only `stat-group` and `chart` render one. `validate_workflow`/
`save_workflow` check every `stat-group` item's `fieldKey` and every `chart`'s
`series` against what actually exists (a `calculations.fields`/`series` entry, or —
for `stat-group` only — a captured input `fieldKey`) and flag a dangling binding as
`DATA_DISPLAY_UNKNOWN_FIELD`. This can't catch every mistake — an input component
reused as a makeshift "display" is structurally valid and won't be flagged, it just
won't show a live value — so pick a data-display component when the goal is to
render a calculated result.

Every component, regardless of type, may declare `showWhen` — see
[Visibility (`showWhen`)](./calculation-language.md#visibility-showwhen) in the
calculation language guide.

## Saving and conflicts

`WorkflowDefinitionFile.version` is the optimistic-concurrency token. `save_workflow`
(and the REST `PUT`) compare the submitted `version` against what's currently
stored: if they match, the save succeeds and the version increments; if not, the
save is rejected as a conflict (`WorkflowSaveStatus.Conflict`) rather than silently
overwriting a concurrent human or agent edit — reload and reapply on conflict. See
[AI-Ready Workflow Authoring](./ai-workflow-authoring.md) for the full save
protocol, including how a host implements the atomic compare-and-swap this depends
on.

**Note:** in the current demo/dev phase, workflow saves against the seed-file-backed
stores in this repo (`UmbracoPrism.MockBusinessApp`) are memory-only — a save reaches
the live engine immediately, but a process restart reloads from the seed files on
disk. This is intentional for now, not a bug; a production host's `IWorkflowSourceStore`
would back this with real persistence.

## Worked examples

`src/UmbracoPrism.MockBusinessApp/workflow-seeds/` has six real workflows to read as
reference, in roughly increasing order of complexity:

- **`planning.json`** — single-queue, linear applicant flow.
- **`planning-notification.json`** — a planning variant.
- **`community-enquiry.json`** — two-queue applicant/reviewer flow with an approval loop.
- **`information-request.json`** — two-queue, SLA-driven review flow.
- **`payment-demo.json`** — two-queue, Split **and** Join gateways, a payment flow.
- **`money-modeller.json`** — the fullest example: two-queue fan-out, a complete
  declarative `calculations` block, live components (sliders, `stat-group`, `chart`,
  extensive `showWhen` use), and a `recalculate` self-loop. See the
  [worked walkthrough](./calculation-language.md#worked-example-money-modellerjson)
  in the calculation language guide.

## Related documentation

- [The Prism Calculation Language](./calculation-language.md) — grammar, functions, `showWhen`
- [AI-Ready Workflow Authoring](./ai-workflow-authoring.md) — the MCP/REST toolkit, the author loop, saving/conflicts
- [Reference Business App README](../../src/UmbracoPrism.MockBusinessApp/README.md) — configuration and setup

---

[← Back to Guides](README.md)
