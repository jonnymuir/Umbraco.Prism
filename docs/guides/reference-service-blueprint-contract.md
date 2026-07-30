# Reference Service Blueprint Contract

The technical specification for `ServiceBlueprint` — the JSON contract every
Prism service blueprint is authored in, whether by a human in the visual editor or an AI
agent through the [MCP/REST authoring toolkit](./ai-service-blueprint-authoring.md). This is
the shape you read from `read_service_blueprint`, write for `save_service_blueprint`, and check
against `validate_service_blueprint`.

This document is also exposed as an MCP resource (`service-blueprint-docs://authoring-guide`)
so an agent can fetch it directly without needing filesystem access to this repo.

For the embedded expression language used in `calculations` and `showWhen`, see
[The Prism Calculation Language](./calculation-language.md).

---

## Top-level shape

```jsonc
{
  "definitionKey": "money-modeller",   // stable identifier; used to read/save/route to this service-blueprint
  "displayName": "Money Modeller",
  "version": 1,                        // optimistic-concurrency version — see "Saving and conflicts" below
  "description": "...",                // optional
  "initialStage": "choose-start", // must match a stages[].stageKey
  "requestPolicy": "single",           // "single" (resume the one active instance), "multiple" (always new), or "prompt" (instance_picker when one is active)
  "queues": [ /* QueueDefinition[] — see Queues */ ],
  "stages": [ /* StageDefinition[] — see Stages and routes */ ],
  "gateways": [ /* ServiceBlueprintGatewayDefinition[] — see Gateways and routing */ ],
  "calculations": { /* ServiceBlueprintCalculationSet — see calculation-language.md */ },
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

Every stage and gateway declares which queue it belongs to via `queueKey`.
`money-modeller.json`, for example, has a `web-user` queue (the member modelling
their own benefits) and a `business-user` queue (scheme administrators reviewing a
formal quote request) — two independent perspectives on the same service request.

## Stages and routes

A stage (`StageDefinition`) is one stage of the service blueprint:

```json
{
  "stageKey": "model",
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

**A stage's routes must always target a gateway, never another stage directly.**
Gateway routes, in turn, may target either a stage or another gateway. This is
enforced by `ServiceBlueprint.ValidateGatewayRouting()` — called by
`validate_service_blueprint`/`save_service_blueprint` — and is not optional: a route from a stage
straight to another stage is always a validation error. Even the simplest
one-route stage needs a trivial pass-through gateway between it and its
destination (see `to-model-from-record` in `money-modeller.json`, a `Split`
gateway with a single `continue` route). This uniform shape is what lets a single
gateway later grow branching or join logic without restructuring every stage that
points at it.

## Gateways and routing

A gateway (`ServiceBlueprintGatewayDefinition`) is a routing node — not a rendered stage:

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
  usually waiting on user choice the way a stage's routes are, they're evaluating
  where an already-triggered action goes next.

**Convention for a business-side/reviewer action after a Split:** route it
*only* into the Join, never fan it out to its own separate terminal stage as
well. `simulate_service_blueprint`'s trace follows a single cursor — if the business
side's action routes to both a Join and its own terminal, the trace can only
follow one of those branches, silently leaving the other's actions
unexercised and unverified. `payment-demo` and `information-request` both
follow this convention already (the reviewer's action routes straight into
the Join, with no separate reviewer-side terminal) — match it rather than
adding a parallel terminal for the business queue.

**A "request more info" (or any) loop must have a real way out.** A gateway
can have every route resolve to a real target and still be a dead end in
practice — e.g. a business-side stage that requests more information by
routing to a gateway that only ever loops back within the *same* queue, with
no stage anywhere that the other queue's actor could actually answer from.
Nothing about that is structurally invalid (every gateway has outgoing
routes, every target exists), so it isn't caught by the routing checks above
— but no real instance that takes that branch can ever complete.
`validate_service_blueprint`/`save_service_blueprint` also run `ValidateReachability()`, which
checks that every stage and gateway has *some* path to a terminal stage (one
with no outgoing routes) — not that every path does, so a deliberate
self-loop like `money-modeller`'s `recalculate` route is fine as long as
another route out of the same stage still leads somewhere. A node with no
path at all is flagged as `STAGE_UNREACHABLE_TERMINAL` /
`GATEWAY_UNREACHABLE_TERMINAL`. It can't tell you *why* the loop is a dead
end (usually: the loop needed to hand off to a stage in the other queue and
never did) — only that structurally, nothing escapes it.

## Response states

Every runtime response (`ServiceRequestResponseEnvelope`, what `simulate_service_blueprint`
returns per step) carries a `responseState` — what the client should do next:

| Value | Meaning |
|---|---|
| `render` | Show the current stage — `Render` carries `StepContent` (components, available actions). |
| `defer` | Wait and poll again — `PollAfterMs` says how long. Used at Join gateways waiting on other cursors. |
| `complete` | The service request has finished. |
| `error` | Something went wrong — check `Problems`. |

## Components

`stages[].components` is a list of `PrismComponent` — a polymorphic type
discriminated by `"type"`. The full catalog:

**Input components** (declare a `fieldKey`, participate in the calculation scope —
see [calculation-language.md](./calculation-language.md#where-it-lives-in-a-service blueprint)):
`text`, `number`, `decimal`, `select`, `radio`, `checkboxlist`, `date`, `email`,
`textarea`, `boolean`, `slider`, `file-upload`, `guidance-checklist`.

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
it's labelled — only `stat-group` and `chart` render one. `validate_service_blueprint`/
`save_service_blueprint` check every `stat-group` item's `fieldKey` and every `chart`'s
`series` against what actually exists (a `calculations.fields`/`series` entry, or —
for `stat-group` only — a captured input `fieldKey`) and flag a dangling binding as
`DATA_DISPLAY_UNKNOWN_FIELD`. This can't catch every mistake — an input component
reused as a makeshift "display" is structurally valid and won't be flagged, it just
won't show a live value — so pick a data-display component when the goal is to
render a calculated result.

`summary-list` specifically is for **reviewing already-captured input values**, not
for presenting a calculated result — each child is an inline input-type component
(its own `fieldKey`, `label`, type) with an optional "Change" link back to the stage
that captured it, GOV.UK's standard check-your-answers pattern. Set `changeStateKey`
on the summary-list itself when every row was captured on the *same* earlier stage;
when rows summarise fields captured on *different* stages (e.g. a bin count captured
on `how-many-bins`, an address captured on a separate `property-address` stage), give
the individual child its own `changeStateKey` instead — it overrides the summary-list's
own default for that one row. `validate_service_blueprint`/`save_service_blueprint` check both the
component-level and any per-row `changeStateKey` against the service blueprint's actual stage
keys and flag a dangling target as `DATA_DISPLAY_UNKNOWN_CHANGE_STATE`. A summary-list
row *can* bind its `fieldKey` to a `calculations.fields` entry instead of a captured
input, but there's nothing sensible for a "Change" link to navigate to for a derived
value — `stat-group`/`chart` are the right choice for presenting a calculated result.

Only give a `summary-list` a `changeStateKey` (or per-row one) when the page is a
*pre-decision* check-your-answers step — the same actor whose input it's reviewing is
about to submit, and going back to change something is still meaningful. Once a
decision has actually been recorded (a discretionary call, an approval, anything past
the point where a route already fired based on those values), a summary-list showing
that same data is a historical record, not a form to revisit — leave `changeStateKey`
unset so it renders read-only. Nothing validates this distinction (it's about *when*
in the flow the page sits, not the JSON shape), so get it right at authoring time:
before drafting a "review" or "outcome" stage, check whether it comes before or after
the service blueprint's actual decision point.

`file-upload` is a real document upload — one component per named document a service blueprint
needs (e.g. "Current licence", "Proof of identity"; there's no multi-document
container), server-side saved via a host-registered file storage service and referenced
from `FieldValues` by a `ServiceRequestFileReference` (never the raw bytes). `required: true`
means a file must actually be posted, checked the same way any other required field is.
Optional `acceptedFileTypes` (e.g. `[".pdf", ".jpg"]`) and `maxSizeBytes` narrow what's
accepted; `maxSizeBytes` is enforced server-side on submit.

`guidance-checklist` lists linked guidance articles (each with its own `key`, `label`,
`href`) alongside an acknowledgement checkbox per item — unlike `checkboxlist`, where
`required: true` only means *some* option was chosen, here it means **every** listed
item must be acknowledged before the stage can advance. Use it for "you must read this
guidance before continuing" patterns; the linked articles are ordinary content (e.g.
separate CMS pages), not part of the component's own definition.

Every component, regardless of type, may declare `showWhen` — see
[Visibility (`showWhen`)](./calculation-language.md#visibility-showwhen) in the
calculation language guide.

### Queue render capabilities (host-declared)

Different queues in the same service blueprint can be served by entirely different host
applications with different rendering capability — a web front end with a full
component catalog, versus an admin surface that only supports a generic
"advance" action with no rendering pipeline at all. A host can optionally
register an `IQueueCapabilitiesProvider` (`Wayfinder.Engine.Abstractions`)
declaring, per queue key, which component `"type"` discriminators it actually
renders. When registered, `validate_service_blueprint`/`save_service_blueprint` check every
component in every stage against its queue's declared capability list and
reject (`QUEUE_CAPABILITY_UNSUPPORTED_COMPONENT`) a component type the queue's
host can't render — instead of letting you author something that silently
renders as nothing. A queue key with **no** declared entry is unrestricted —
not this host's concern (e.g. a queue actually served by a different app); an
entry with an **empty** list means the host genuinely supports zero component
types for that queue today. Use `list_queue_capabilities` to discover a
queue's supported types before drafting a stage for it.

Capabilities are a contract each host declares about itself, never a runtime
call to another host's process. `PrismComponentTypeCatalog`
(`Wayfinder`) reflects `PrismComponent`'s closed, compile-time-fixed
set of `[JsonDerivedType]` discriminators — since that assembly is shared by
every Prism-Core host, `PrismComponentTypeCatalog.AllDiscriminators` is a
ready-made, honest declaration of "I'm a stock Prism-Core web host", provable
locally with no dependency on any other app actually running. A host with
bespoke rendering (like `UmbracoPrism.MockBusinessApp`'s admin surface)
declares its own smaller, hand-maintained list instead, matching exactly what
it implements.

**Known limitation:** there is no mechanism today for a host to extend the
component catalog itself with genuinely new types beyond what
`Wayfinder` ships — the `[JsonDerivedType]` list is fixed at compile
time. If that ever becomes possible, a host with real extensions would need
its own way to publish an extended declaration; nothing exercises this today.

## Saving and conflicts

`ServiceBlueprint.version` is the optimistic-concurrency token. `save_service_blueprint`
(and the REST `PUT`) compare the submitted `version` against what's currently
stored: if they match, the save succeeds and the version increments; if not, the
save is rejected as a conflict (`ServiceBlueprintSaveStatus.Conflict`) rather than silently
overwriting a concurrent human or agent edit — reload and reapply on conflict.

**For a brand-new `definitionKey` that's never been saved before, set `version`
to `0`**, not `1` — a non-existent service blueprint's current version is `0`, so that's
what `save_service_blueprint` expects to match on the first save. It's an easy mistake
to copy `"version": 1` from an existing seed you read as a style reference
(`read_service_blueprint` shows a service blueprint's *current* saved version, e.g. `1` after
its first save — not what a new one should start at) and get
`SAVE_VERSION_CONFLICT` on your very first attempt. See
[AI-Ready Service Blueprint Authoring](./ai-service-blueprint-authoring.md) for the full save
protocol, including how a host implements the atomic compare-and-swap this depends
on.

**Note:** in the current demo/dev phase, service blueprint saves against the seed-file-backed
stores in this repo (`UmbracoPrism.MockBusinessApp`) are memory-only — a save reaches
the live engine immediately, but a process restart reloads from the seed files on
disk. This is intentional for now, not a bug; a production host's `IServiceBlueprintSourceStore`
would back this with real persistence.

## Worked examples

`src/UmbracoPrism.MockBusinessApp/service-blueprints/` has six real service blueprints to read as
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
- [AI-Ready Service Blueprint Authoring](./ai-service-blueprint-authoring.md) — the MCP/REST toolkit, the author loop, saving/conflicts
- [Reference Business App README](../../src/UmbracoPrism.MockBusinessApp/README.md) — configuration and setup

---

[← Back to Guides](README.md)
