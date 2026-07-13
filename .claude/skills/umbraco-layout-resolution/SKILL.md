---
name: "umbraco-layout-resolution"
description: "Resolve shared Razor layouts correctly for Umbraco/TestSite views"
domain: "umbraco"
confidence: "high"
source: "observed"
---

## Context

Use this skill when a TestSite or Umbraco route-hijacked Razor view fails to find `Master.cshtml`, especially on pages rendered through a `RenderController`.

## Patterns

- Prefer `Layout = "~/Views/Shared/Master.cshtml"` when the view should always use the shared TestSite layout.
- Avoid `Layout = "Master.cshtml"` in these views; the explicit filename form can make MVC probe `/Views/Master.cshtml` instead of the shared layout.
- For route-hijacked member workflow pages, protect the hub/controller with `PrismMemberCookie` and resolve workflow links from Umbraco content (`Umbraco.ContentAtRoot()` in Razor, `IPublishedContentQuery.ContentAtRoot()` in controllers) instead of guessing from a workflow key.
- For bug triage, search nearby views for `Layout = "Master.cshtml"` before broadening the change.
- Keep the fix surgical: change only the affected surface unless other views use the same failing pattern.

## Examples

- `src/UmbracoPrism.TestSite/Views/WorkflowHub.cshtml`
- `src/UmbracoPrism.Core/Controllers/WorkflowHubController.cs`
- `src/UmbracoPrism.TestSite/Views/MemberDashboard.cshtml`
- `src/UmbracoPrism.TestSite/Views/Shared/Master.cshtml`

## Anti-Patterns

- Assuming MVC will resolve `Master.cshtml` through `Views/Shared` just because the shared layout exists.
- Normalizing unrelated `Layout = "Master"` usages without evidence they share the same resolution problem.
