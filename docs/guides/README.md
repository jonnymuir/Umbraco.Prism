# Prism Developer Guides

Step-by-step guides for common service blueprints and customizations in Umbraco.Prism.

## Getting Started

- **[Embedding the Service Blueprint Editor](./embedding-the-service-blueprint-editor.md)** — **Primary integrator recipe.** Build a business app on top of Prism. Implement `ServiceBlueprintSource`, wire the editor, extend the action catalog.
- **[AI-Ready Service Blueprint Authoring](./ai-service-blueprint-authoring.md)** — Let an AI agent (Claude Code or any MCP client) list, read, validate, simulate, and save your service blueprints. Implement `IServiceBlueprintSourceStore`, add `MapServiceBlueprintAuthoringApi()`/`MapServiceBlueprintAuthoringMcp()`.
- **[Service Blueprint Editor Composition](./service-blueprint-editor-composition.md)** — Advanced patterns for custom hosts. Custom canonical JSON helpers, custom action catalogs, building your own host wrapper.
- **[Umbraco Integration](./umbraco-integration.md)** — Embed Prism service blueprints in your Umbraco site. Member surface, business app, authentication, roles.
- **[Setting Up a Prism Service Blueprint](./service-blueprint-setup.md)** — Create and configure a service blueprint in Umbraco.Prism, from definition to runtime.
- **[Extending Prism for Your Business Domain](./extending-prism.md)** — Add domain-specific notification handlers, controllers, and models on top of Prism Core. Learn from the vinyl record store example in TestSite.

## Configuration & Customization

- **[Customizing Service Blueprint UI & Theme](./service-request-customisation.md)** — Override CSS variables, Razor partials, and styles to customize service blueprint appearance.
- **[Form Validation Patterns](./service-request-forms-validation.md)** — Configure validation rules, error messages, and display patterns for service blueprint forms.
- **[Using GDS Components](./service-blueprint-gds-components.md)** — Reference guide for GOV.UK Design System components used in Prism service blueprints.

## Reference

- **[Reference Service Blueprint Contract](./reference-service-blueprint-contract.md)** — Technical specification for `ServiceBlueprint`: states, routes, gateways, queues, components, response states.
- **[The Wayfinder Calculation Language](./calculation-language.md)** — Grammar, functions, tables/series, `showWhen`, and a worked walkthrough for the declarative calculation/actuarial expression language.
- **[Service Design Principles](./service-design-principles.md)** — Industry-agnostic grounding for service blueprint authors: the Design Council Double Diamond, the GOV.UK Service Standard, and Lou Downe's 15 principles of good services, each mapped to concrete authoring decisions.
