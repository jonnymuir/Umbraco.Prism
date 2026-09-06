# Service Blueprint backend authoring and contracts

This document is the package-facing backend reference: what Prism expects back from your business app, and which extension points matter when you replace the demo business app with a real service.

For the authored JSON schema itself (`queues`/`stages`/`gateways`/`routes`/components), see the
[Reference Service Blueprint Contract](../guides/reference-service-blueprint-contract.md), this
doc doesn't repeat it. JSON seed files (`src/UmbracoPrism.MockBusinessApp/service-blueprints/*.json`)
or the [MCP/REST authoring toolkit](../guides/ai-service-blueprint-authoring.md) are both valid
ways to produce it.

## Actions and gateway routing

A route (on a stage or a gateway) is `{ id, target, trigger, requiresRole? }`. In the demo engine:

- regular user actions are routes with no `requiresRole`,
- reviewer actions are routes with `requiresRole == "reviewer"`,
- `change:{stageKey}` is handled specially for check-answers links,
- optimistic concurrency is enforced by comparing submitted `StateVersion`.

A stage's routes must always target a gateway, never another stage directly, see
[The gateway routing rule](../guides/reference-service-blueprint-contract.md#the-gateway-routing-rule).

Source: `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppProcessManager.cs`.

## Request policies

| Policy | Current behaviour |
| --- | --- |
| `single` | Resume the existing active instance for the user/blueprint key (including a terminal one), or create one |
| `multiple` | Always create a new instance |
| `prompt` | If an active (non-terminal) instance exists, return `instance_picker`; otherwise create a new one |

## Response envelope

`ServiceRequestResponseEnvelope` (`Wayfinder.Models.ServiceDesign`, in
[`jonnymuir/Wayfinder`](https://github.com/jonnymuir/Wayfinder)):

| Property | Meaning |
| --- | --- |
| `InstanceId` | Running instance identifier |
| `ResponseState` | `render`, `defer`, `complete`, `error`, plus `instance_picker` for `requestPolicy: "prompt"` |
| `StateVersion` | Concurrency token echoed back on POST |
| `CorrelationId` | Tracking identifier |
| `ServerTimeUtc` | Server timestamp |
| `PollAfterMs` | Used for `defer` responses (waiting Join gateways) |
| `Render` | The `StepContent` payload to render, only present when `ResponseState` is `render` |
| `RequestPolicy` | Echoes the blueprint's `requestPolicy` |
| `Problems` | Validation or fatal problems (`ServiceRequestProblem[]`) |

`StepContent` contains `StepType`, `StateDisplayName`, `Components` (`ComponentRenderPayload[]`), `AvailableActions`, and an optional `Data` (host-supplied structured display data resolved into "interactive" components, display data only, never instructions).

### Render payload details that matter

`FieldRenderPayload` is rich enough that Umbraco never needs to consult browser-submitted metadata to validate or re-render a field:

- `Value` and `DefaultValue`
- `ReadOnly`
- `Prefix`
- `Options`
- `ConditionalFields`
- `ConditionalOn` / `VisibleWhen`
- min/max length and numeric bounds
- regex `Pattern`
- content for content-only field types

## API responsibilities when you replace the demo engine

Your production business app should preserve the same responsibilities as `BusinessAppProcessManager`:

1. Resolve tenant and user from the forwarded bearer token, not from request body values.
2. Enforce instance ownership and state-version checks.
3. Return sanitized content payloads.
4. Keep instance lookup rules aligned with `requestPolicy`.
5. Return user-safe `ServiceRequestProblem` values for domain failures.

## Related docs

- [Reference Service Blueprint Contract](../guides/reference-service-blueprint-contract.md)
- [Building a service blueprint](./service-request-forms-engine-demo.md)
- [Client rendering](./service-request-forms-engine-client.md)
- [Service Request Hub and conditional fields](../design/service-request-hub-and-conditional-fields.md)
