# Service Blueprint validation

Prism validates service blueprint submissions in layers so the browser can be helpful without ever becoming authoritative.

## Validation layers

| Layer | Where it runs | Purpose |
| --- | --- | --- |
| Browser hints | Rendered HTML | Usability only: required markers, input types, min/max hints |
| Antiforgery | `ServiceRequestPageController` | Reject cross-site form posts |
| Nonce-backed structural validation | `StageNonceService` + `ServiceRequestFieldValidator` | Ensure the submitted fields still match the rendered definition |
| Domain validation | Your business app | Enforce business rules before a transition is accepted |

## Why the nonce matters

When Prism renders a page, it serializes the authoritative `FieldRenderPayload` list into the distributed cache and stores only a nonce in the HTML form.

On POST, Prism resolves that nonce and validates against the server copy, not against browser-submitted metadata.

This protects against:

- added fields,
- removed fields,
- renamed fields,
- changed options,
- relaxed client-side constraints.

Source: `src/Wayfinder.Umbraco/Services/StageNonceService.cs`.

## What the field validator checks

`src/Wayfinder.Umbraco/Services/ServiceRequestFieldValidator.cs` currently enforces:

- unknown-field rejection,
- required fields,
- date completeness and year range,
- numeric parsing,
- email format,
- option whitelist enforcement,
- regex and min/max constraints,
- conditional visibility rules,
- checkbox/date field naming quirks used by the GOV.UK components.

It also skips validation for:

- hidden conditional fields,
- read-only fields,
- content-only field types.

## Domain validation still belongs to your business app

Structural validation only proves that the submission matches the step Prism rendered. It does not decide whether the user's data is acceptable for your domain.

The mock business app demonstrates this by returning `validation_error` with a `ServiceBlueprintProblem` when a technical-support enquiry omits diagnostic detail.

Practical rule:

- **Prism validates shape and safety.**
- **Your business app validates meaning.**

## PRG behaviour

Failed validation does not render directly from POST. Prism stores problems and submitted values in `TempData`, redirects, then re-renders the GET.

That gives you:

- refresh-safe error pages,
- preserved user input,
- clean URLs.

## Multi-server deployments

The nonce cache uses `IDistributedCache`. The package default is fine for local development, but a real multi-server deployment needs a shared cache so GET and POST can land on different nodes safely.

## Checklist for your own service blueprints

- Use component-authored constraints (`required`, lengths, options, min/max) instead of inventing parallel client rules.
- Keep nonces short-lived but realistic for your journey length.
- Treat `ServiceBlueprintProblem` as the public contract for recoverable validation failures.
- Keep domain validation in the business app, even if the UI can also give a friendly hint.
