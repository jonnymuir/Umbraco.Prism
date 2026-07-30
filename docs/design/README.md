# Service Blueprint package design docs

These documents explain how to build, integrate, and operate Prism service blueprints from a package consumer's point of view. They are grounded in the current implementation:

- `Models/ServiceDesign/*`, `Builders/ServiceBlueprintBuilder.cs` — in [`jonnymuir/Wayfinder`](https://github.com/jonnymuir/Wayfinder)
- `Controllers/ServiceRequestPageController.cs`, `Views/*` — in [`jonnymuir/Wayfinder.Umbraco`](https://github.com/jonnymuir/Wayfinder.Umbraco)
- `src/UmbracoPrism.MockBusinessApp/*` — in this repo

Wayfinder and Wayfinder.Umbraco moved out of this repo into their own repos, consumed here as published packages (see `NuGet.config` and `CLAUDE.md`) — the paths above are no longer under `src/` in this repo.

## Start here

1. [Service Blueprint forms engine overview](./service-request-forms-engine.md) — package capabilities, vocabulary, and reading order.
2. [Multi-lane service blueprint engine design](./service-blueprint-multi-lane-engine.md) — the canonical behaviour for the move from linear flow to lane-owned split/join service blueprint execution.
3. [Building a service blueprint](./service-request-forms-engine-demo.md) — the end-to-end implementation story.
4. [Backend authoring and contracts](./service-request-forms-engine-backend.md) — definitions, transitions, components, instance policies, and API payloads.
5. [Umbraco integration](./service-request-forms-engine-umbraco.md) — service registration, page setup, controllers, and the service request hub.
6. [Client rendering](./service-request-forms-engine-client.md) — shells, render payloads, and how the Razor layer turns envelopes into GOV.UK UI.
7. [Validation](./service-blueprint-validation.md) and [security](./service-request-forms-engine-security.md) — the guard rails that make the package safe to use.
8. [Service Request Hub and conditional fields](./service-request-hub-and-conditional-fields.md) — advanced authoring patterns.

## Document map

| Document | Best for | Covers |
| --- | --- | --- |
| [service-request-forms-engine.md](./service-request-forms-engine.md) | Anyone new to Prism service blueprints | Architecture, vocabulary, current runtime model |
| [service-blueprint-multi-lane-engine.md](./service-blueprint-multi-lane-engine.md) | Product, runtime, and editor work on the next service blueprint model | Canonical multi-lane behaviour, split/join semantics, clean runtime contract, issue mapping |
| [service-request-forms-engine-demo.md](./service-request-forms-engine-demo.md) | Developers implementing a first service blueprint | End-to-end flow from seed definition to running page |
| [service-request-forms-engine-backend.md](./service-request-forms-engine-backend.md) | Backend and integration developers | Definitions, builders, engine envelopes, instance policies |
| [service-request-forms-engine-umbraco.md](./service-request-forms-engine-umbraco.md) | Umbraco package consumers | DI, content types, route hijacking, service blueprint pages, hub pages |
| [service-request-forms-engine-client.md](./service-request-forms-engine-client.md) | UI and rendering work | Shell selection, component payloads, field rendering rules |
| [service-blueprint-validation.md](./service-blueprint-validation.md) | Form authors and reviewers | Browser hints, nonce checks, server validation, domain validation |
| [service-request-forms-engine-security.md](./service-request-forms-engine-security.md) | Security reviews and production hardening | Tenant isolation, token forwarding, sanitization, concurrency |
| [service-request-hub-and-conditional-fields.md](./service-request-hub-and-conditional-fields.md) | Advanced service blueprint authors | Prompt policy, hub UX, conditional reveals, waiting/task patterns |

## Current implementation notes

- The authored service blueprint schema is the polymorphic `PrismComponent` tree, not the older field-group model.
- Step type is inferred from authored components via `PrismComponentExtensions.InferStepType()`.
- The main response states are `render`, `defer`, `complete`, and `error`; the demo business app also uses `instance_picker` for prompt-mode resume/start decisions and `validation_error` for domain-specific failures.
- Seed files live in `src/UmbracoPrism.MockBusinessApp/service-blueprints/` and are the best short examples of the current JSON shape.

## Multi-lane design note

The package docs above still describe the current shipped runtime. For the concurrent lane redesign, use [service-blueprint-multi-lane-engine.md](./service-blueprint-multi-lane-engine.md) as the behavioural source of truth.
