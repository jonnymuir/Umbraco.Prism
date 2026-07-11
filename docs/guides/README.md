# Prism Developer Guides

Step-by-step guides for common workflows and customizations in Umbraco.Prism.

## Getting Started

- **[Embedding the Workflow Editor](./embedding-the-workflow-editor.md)** — **Primary integrator recipe.** Build a business app on top of Prism. Implement `WorkflowSource`, wire the editor, extend the action catalog.
- **[AI-Ready Workflow Authoring](./ai-workflow-authoring.md)** — Let an AI agent (Claude Code or any MCP client) list, read, validate, simulate, and save your workflows. Implement `IWorkflowSourceStore`, add `MapPrismWorkflowAuthoringApi()`/`MapPrismWorkflowAuthoringMcp()`.
- **[Workflow Editor Composition](./workflow-editor-composition.md)** — Advanced patterns for custom hosts. Custom canonical JSON helpers, custom action catalogs, building your own host wrapper.
- **[Umbraco Integration](./umbraco-integration.md)** — Embed Prism workflows in your Umbraco site. Member surface, business app, authentication, roles.
- **[Setting Up a Prism Workflow](./workflow-setup.md)** — Create and configure a workflow in Umbraco.Prism, from definition to runtime.
- **[Extending Prism for Your Business Domain](./extending-prism.md)** — Add domain-specific notification handlers, controllers, and models on top of Prism Core. Learn from the vinyl record store example in TestSite.

## Configuration & Customization

- **[Customizing Workflow UI & Theme](./workflow-customisation.md)** — Override CSS variables, Razor partials, and styles to customize workflow appearance.
- **[Form Validation Patterns](./workflow-forms-validation.md)** — Configure validation rules, error messages, and display patterns for workflow forms.
- **[Using GDS Components](./workflow-gds-components.md)** — Reference guide for GOV.UK Design System components used in Prism workflows.

## Reference

- **[Reference Workflow Contract](./reference-workflow-contract.md)** — Technical specification for the four reference workflows in Prism.
