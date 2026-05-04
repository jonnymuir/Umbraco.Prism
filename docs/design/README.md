# Workflow package design docs

These documents explain how to build, integrate, and operate Prism workflows from a package consumer's point of view. They are grounded in the current implementation in:

- `src/UmbracoPrism.Shared/Models/Workflow/*`
- `src/UmbracoPrism.Shared/Builders/WorkflowDefinitionBuilder.cs`
- `src/UmbracoPrism.Core/Controllers/PrismWorkflowPageController.cs`
- `src/UmbracoPrism.Core/Views/*`
- `src/UmbracoPrism.MockBusinessApp/*`

## Start here

1. [Workflow forms engine overview](./workflow-forms-engine.md) — package capabilities, vocabulary, and reading order.
2. [Building a workflow](./workflow-forms-engine-demo.md) — the end-to-end implementation story.
3. [Backend authoring and contracts](./workflow-forms-engine-backend.md) — definitions, transitions, components, instance policies, and API payloads.
4. [Umbraco integration](./workflow-forms-engine-umbraco.md) — service registration, page setup, controllers, and the workflow hub.
5. [Client rendering](./workflow-forms-engine-client.md) — shells, render payloads, and how the Razor layer turns envelopes into GOV.UK UI.
6. [Validation](./workflow-validation.md) and [security](./workflow-forms-engine-security.md) — the guard rails that make the package safe to use.
7. [Workflow hub and conditional fields](./workflow-hub-and-conditional-fields.md) — advanced authoring patterns.

## Document map

| Document | Best for | Covers |
| --- | --- | --- |
| [workflow-forms-engine.md](./workflow-forms-engine.md) | Anyone new to Prism workflows | Architecture, vocabulary, current runtime model |
| [workflow-forms-engine-demo.md](./workflow-forms-engine-demo.md) | Developers implementing a first workflow | End-to-end flow from seed definition to running page |
| [workflow-forms-engine-backend.md](./workflow-forms-engine-backend.md) | Backend and integration developers | Definitions, builders, engine envelopes, instance policies |
| [workflow-forms-engine-umbraco.md](./workflow-forms-engine-umbraco.md) | Umbraco package consumers | DI, content types, route hijacking, workflow pages, hub pages |
| [workflow-forms-engine-client.md](./workflow-forms-engine-client.md) | UI and rendering work | Shell selection, component payloads, field rendering rules |
| [workflow-validation.md](./workflow-validation.md) | Form authors and reviewers | Browser hints, nonce checks, server validation, domain validation |
| [workflow-forms-engine-security.md](./workflow-forms-engine-security.md) | Security reviews and production hardening | Tenant isolation, token forwarding, sanitization, concurrency |
| [workflow-hub-and-conditional-fields.md](./workflow-hub-and-conditional-fields.md) | Advanced workflow authors | Prompt policy, hub UX, conditional reveals, waiting/task patterns |

## Current implementation notes

- The authored workflow schema is the polymorphic `PrismComponent` tree, not the older field-group model.
- Step type is inferred from authored components via `PrismComponentExtensions.InferStepType()`.
- The main response states are `render`, `defer`, `complete`, and `error`; the demo business app also uses `instance_picker` for prompt-mode resume/start decisions and `validation_error` for domain-specific failures.
- Seed files live in `src/UmbracoPrism.MockBusinessApp/workflow-seeds/` and are the best short examples of the current JSON shape.
