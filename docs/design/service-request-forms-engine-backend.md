# Service Blueprint backend authoring and contracts

This document is the package-facing backend reference: what you author, what Prism expects back, and which extension points matter when you replace the demo business app with a real service.

## Authoring options

Prism supports two practical authoring styles.

| Style | Best when | Canonical source |
| --- | --- | --- |
| JSON seed files | You want simple content-like service blueprints or a demo harness | `src/UmbracoPrism.MockBusinessApp/service-blueprints/*.json` |
| Fluent builder | You want compile-time help, shared code, or richer composition | `src/Wayfinder/Builders/ServiceBlueprintBuilder.cs` |

Both produce the same runtime shape: `ServiceBlueprint` with `StageDefinition` states and `RouteFile` transitions.

## Definition contract

Source: `src/Wayfinder/Models/ServiceDesign/ServiceBlueprint.cs`

| Property | Required | Notes |
| --- | --- | --- |
| `definitionKey` | Yes | Stable identifier used by the page and API route |
| `displayName` | Yes | User-facing service blueprint name |
| `version` | Yes | Definition revision, useful for your own migration/versioning story |
| `initialState` | Yes | First state for new instances |
| `instancePolicy` | No (defaults to `single`) | `single`, `multiple`, or `prompt` |
| `states` | Yes | Array of `StageDefinition` objects |
| `transitions` | Yes | Array of permitted action edges |

A state is intentionally small:

- `stateKey`
- `displayName`
- `components`

Step shell is inferred from `components`; it is not a separately-authored field anymore.

## Component model

The authored schema is the `PrismComponent` hierarchy declared in `src/Wayfinder/Models/ServiceDesign/Components/PrismComponent.cs`.

### Container components

| Type | Purpose |
| --- | --- |
| `fieldset` | Group related inputs under a legend |
| `accordion` | Group sections that expand/collapse |
| `summary-list` | Display previously captured answers with optional change links |
| `task-list` | Show work split into named tasks |

### Input components

| Type | Notes |
| --- | --- |
| `text`, `email`, `textarea` | Support length and pattern rules where appropriate |
| `number`, `decimal` | Support numeric min/max and optional prefixes |
| `select`, `radio`, `checkboxlist` | Support option lists; radios and checkbox lists can reveal conditional children |
| `date` | Rendered as GOV.UK day/month/year input and recombined server-side |
| `boolean` | Single checkbox style yes/no capture |

The fluent builder also exposes `Tel(...)`, which is useful for code-first service blueprints.

### Content and status components

| Type | Purpose |
| --- | --- |
| `body`, `heading` | Basic narrative copy |
| `inset-text`, `warning-text`, `details`, `notification-banner` | Guidance or emphasis |
| `panel` | Confirmation shell trigger |
| `waiting` | Waiting/status shell trigger with polling metadata |

## Transitions and actions

A transition is `fromState + action + toState`, with optional `requiresRole`.

In the demo engine:

- regular user actions are transitions where `RequiresRole == null`,
- reviewer actions are transitions where `RequiresRole == "reviewer"`,
- `change:{stateKey}` is handled specially for check-answers links,
- optimistic concurrency is enforced by comparing submitted `StateVersion`.

Source: `src/UmbracoPrism.MockBusinessApp/Services/BusinessAppProcessManager.cs`.

## Instance policies

| Policy | Current behaviour |
| --- | --- |
| `single` | Resume the existing active instance for the user/blueprint key, or create one |
| `multiple` | Always create a new instance |
| `prompt` | If an active instance exists, return `instance_picker`; otherwise create a new one |

The instance list contract used by the service request hub lives in `src/Wayfinder/Models/ServiceDesign/ServiceRequestListEnvelope.cs`.

## Response envelope

Source: `src/Wayfinder/Models/ServiceDesign/ServiceRequestResponseEnvelope.cs`

| Property | Meaning |
| --- | --- |
| `InstanceId` | Running instance identifier |
| `ResponseState` | `render`, `defer`, `complete`, `error` in the core contract |
| `StateVersion` | Concurrency token echoed back on POST |
| `CorrelationId` | Tracking identifier |
| `PollAfterMs` | Used for waiting states |
| `Render` | The `StepContent` payload to render |
| `Problems` | Validation or fatal problems |
| `InstancePolicy` | Echoes the definition policy |

`StepContent` currently contains:

- `StepType`
- `StateDisplayName`
- `Components`
- `AvailableActions`

### Render payload details that matter

`FieldRenderPayload` is richer than the old docs implied. Current fields include:

- value and `DefaultValue`
- `ReadOnly`
- `Prefix`
- `Options`
- `ConditionalFields`
- `ConditionalOn` / `VisibleWhen`
- min/max length and numeric bounds
- regex `Pattern`
- content for content-only field types

That makes rendered payloads self-describing enough for Umbraco to validate and re-render without consulting browser-submitted metadata.

## Minimal builder example

This is a good fit when you want code reuse instead of large JSON blobs:

```csharp
var definition = new ServiceBlueprintBuilder()
    .Key("pension-application")
    .DisplayName("Pension Application")
    .StartsAt("details")
    .AddState("details", state => state
        .DisplayName("Your details")
        .Fieldset(f => f
            .Legend("Personal information", "l")
            .TextInput("name", "Full name", required: true)
            .Email("email", "Email address", required: true)))
    .AddTransition("details", "submitted", "submit")
    .Build();
```

Keep examples small. Real reference behaviour is already covered by the builder implementation and the seed files above.

## API responsibilities when you replace the demo engine

Your production business app should preserve the same responsibilities as `BusinessAppProcessManager`:

1. Resolve tenant and user from the forwarded bearer token, not from request body values.
2. Enforce instance ownership and state-version checks.
3. Return sanitized content payloads.
4. Keep instance lookup rules aligned with `instancePolicy`.
5. Return user-safe `ServiceBlueprintProblem` values for domain failures.

## Related docs

- [Building a service blueprint](./service-request-forms-engine-demo.md)
- [Client rendering](./service-request-forms-engine-client.md)
- [Service Request Hub and conditional fields](./service-request-hub-and-conditional-fields.md)
