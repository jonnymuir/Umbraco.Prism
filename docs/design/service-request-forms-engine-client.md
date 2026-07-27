# Service Blueprint client rendering guide

Prism's client story is intentionally server-rendered: the business app returns a service blueprint envelope, and Umbraco turns that into the right shell and GOV.UK component markup.

## Rendering flow

1. `PrismServiceRequestPageController` populates `PrismPrismServiceRequestViewModel`.
2. `Views/stagePage.cshtml` decides which shell to use.
3. The chosen partial renders top-level components and action buttons.
4. Field values and validation errors are taken from the view model, not from browser-authored metadata.

## Shell selection

`src/UmbracoPrism.Core/Views/stagePage.cshtml` and `src/UmbracoPrism.Core/Models/ServiceDesign/ServiceRequestRenderShellResolver.cs` are the main routing points.

Current shell mapping:

| Shell | Trigger |
| --- | --- |
| `question` | Interactive inputs or general content |
| `check-answers` | Summary-list-only states |
| `confirmation` | A panel without interactive inputs |
| `status-timeline` | Read-only progress/status content |
| `task-list` | Task-list components |
| `waiting` | Waiting metadata or waiting component |
| `instance picker` | `ShowInstancePicker == true` |

The resolver still normalizes a few legacy names (`collect`, `review`, `completion`), which is useful for backwards compatibility but should not drive new documentation or new authored examples.

## Top-level component payloads

`PrismComponentRenderPayload` is a transport-friendly rendering model. The top-level `Type` values used by the current views are:

- `fieldset`
- `summary-list`
- `accordion`
- `task-list`
- `waiting`
- `panel`
- `body`
- `heading`
- `inset-text`
- `warning-text`
- `details`
- `notification-banner`

The views for these live under `src/UmbracoPrism.Core/Views/Partials/PrismComponents/`.

## Field payload rules worth knowing

`FieldRenderPayload` carries enough information to render, repopulate, and validate fields safely.

| Property | Why it matters in the UI |
| --- | --- |
| `Value` | Previously-saved service blueprint data |
| `DefaultValue` | Server-prepopulated value that can override blank user input |
| `ReadOnly` | Renders as non-editable content or disabled input |
| `Options` | Source of truth for select/radio/checkbox lists |
| `Prefix` | Currency/unit prefix |
| `ConditionalOn` / `VisibleWhen` | Simple dependent visibility |
| `ConditionalFields` | Option-driven sub-fields for radios/checkbox lists |
| `MinLength`, `MaxLength`, `Pattern`, `Min`, `Max` | Validation hints and server rules |

## Actions

`ServiceBlueprintAction` keeps action rendering deliberately simple:

- `ActionKey`
- `Label`
- `Style` (`primary`, `secondary`, `destructive`)

For check-answers pages, summary-list rows can emit `change:{stateKey}` actions that the mock engine resolves as direct navigation back to a named state.

## Waiting pages and polling

Waiting UX is a normal service blueprint shell, not a separate product feature.

What the client receives:

- `ResponseState = defer`
- `PollAfterMs`
- a `waiting` component with `ExpectedWaitSeconds`, `AllowDefer`, and optional `DeferMessage`

What the view does:

- renders a waiting panel/message,
- shows the leave-and-return-later affordance when `AllowDefer` is true,
- can route the user back to the hub if they choose not to wait.

## PRG and error display

Prism uses POST-Redirect-Get rather than rendering validation errors directly from the POST action. That gives you:

- no duplicate form submissions on refresh,
- preserved values via `TempData`,
- `ServiceBlueprintProblem` values available both inline and in summaries.

`PrismPrismServiceRequestViewModel` exposes:

- `Problems`
- `FormValues`
- `FieldErrors`
- `AllFields`

## Pre-population

Pre-population happens before nonce creation, so the rendered field definition and the later POST validation still agree.

The example override in `src/UmbracoPrism.TestSite/Controllers/StagePageController.cs` sets claim-derived defaults for `full-name` and `email-address`, then marks them read-only.

## Practical guidance

- Treat the render payload as authoritative; do not rebuild field rules in JavaScript.
- Keep component examples short and targeted. Seed files are the best place for longer real definitions.
- If you add a new component type, document both the authored `PrismComponent` shape and the rendered `PrismComponentRenderPayload` that the views consume.

## Related docs

- [Backend authoring and contracts](./service-request-forms-engine-backend.md)
- [Umbraco integration](./service-request-forms-engine-umbraco.md)
- [Validation](./service-blueprint-validation.md)
