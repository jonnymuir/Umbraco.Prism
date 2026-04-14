---
name: "umbraco-workflow-page-ownership"
description: "Keep Prism workflow/member journeys content-owned and idiomatic in Umbraco v17"
domain: "umbraco"
confidence: "high"
source: "observed"
---

## Context

Use this skill when reviewing or building Prism workflow pages, member dashboards, or hubs inside the TestSite so Umbraco remains the authored site shell and Prism remains the workflow/auth integration layer.

## Patterns

- Treat workflow/member pages as normal Umbraco documents first: document types, generated models, templates, and route-hijacked `RenderController`s should describe the journey the way an editor would expect to see it in the tree.
- Prefer a single site root (`homePage`) with member pages beneath it unless there is a real multi-site/domain reason for extra roots; do not make workflow pages root nodes just because the seeder can.
- Add `[ModelType("alias")]` to route-hijacking controllers in v17 so the document-type intent stays explicit even when naming convention discovery already works.
- Keep controller-owned routes (`/auth/login`, `/auth/logout`) hardcoded only for auth endpoints; content-owned destinations such as dashboard, workflow hub, and workflow pages should resolve from published content.
- Use Prism pages as **workflow shells**, not workflow brains: let the Business App stay authoritative for state, field semantics, and progression, while Prism adds tenant/auth context, nonce validation, and safe rendering.
- For views that need extra runtime data, prefer a composite model that still preserves the generated Published Model rather than falling back to untyped `UmbracoViewPage` + raw alias strings.
- Avoid bypassing `CurrentTemplate()` or direct view-path rendering unless there is a documented platform bug/workaround; if you must do it, treat it as temporary debt because it weakens Umbraco template ownership.
- Keep client-side workflow scripts progressive only (visibility toggles, focus, polish). Do not let browser code or seeded route fallbacks become the authoritative source for workflow decisions or authored navigation.
- Keep skeletal placeholder demos separate from the canonical pattern. If a `workflowDemoPage` or front-end shell is unfinished, do not let it define the repo's “best practice” story.
- In the backoffice, use the Umbraco 17 manifest/Lit stack, but make the information architecture honest: if you define a custom Prism section, mount Prism dashboards there; if the dashboard belongs in Content, remove the unused custom section.

## Examples

- `src/UmbracoPrism.Core/PrismContentTypeSeeder.cs`
- `src/UmbracoPrism.Core/Controllers/MemberDashboardController.cs`
- `src/UmbracoPrism.Core/Controllers/WorkflowHubController.cs`
- `src/UmbracoPrism.TestSite/Controllers/WorkflowPageController.cs`
- `src/UmbracoPrism.TestSite/Views/HomePage.cshtml`
- `src/UmbracoPrism.TestSite/Views/MemberDashboard.cshtml`
- `src/UmbracoPrism.TestSite/Views/WorkflowPage.cshtml`
- `src/UmbracoPrism.TestSite/Views/WorkflowDemoPage.cshtml`
- `src/UmbracoPrism.Core/wwwroot/umbraco-package.json`

## Anti-Patterns

- Minimal document types that technically route but do not model the authored site structure.
- Hardcoding `/dashboard`, `/my-workflows`, or `/get-in-touch` inside member/page controllers when those pages are Umbraco content.
- Untyped Razor templates reading raw property aliases that have already drifted from generated models.
- Shipping placeholder workflow shells as if they were the preferred Prism-on-Umbraco pattern.
