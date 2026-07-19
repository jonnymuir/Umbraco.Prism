---
name: "umbraco-razor-navigation-cleanup"
description: "Replace deprecated Umbraco content-tree properties and sync partial rendering in Razor views"
domain: "umbraco"
confidence: "high"
source: "observed"
---

## Context

Use this skill when Umbraco Razor templates emit warnings from deprecated `IPublishedContent` navigation properties or MVC analyzer warnings around synchronous partial rendering.

## Patterns

- In Umbraco v17 Razor, replace deprecated `IPublishedContent.Children` with `Children()` or, preferably for document-type-specific templates, `Children<T>()`.
- Replace deprecated `IPublishedContent.Parent` with `Parent()` or `Parent<T>()`.
- When the template already knows the document type, prefer generated Published Models such as `Children<VinylRecord>()` or `Parent<VinylGenreLanding>()` instead of alias-string filtering.
- Replace `@Html.Partial(...)` with `@await Html.PartialAsync(...)` in layouts and views.
- Rely on `Views/_ViewImports.cshtml` for shared `@using Umbraco.Extensions` imports instead of repeating them in each Razor file unless the view must stand alone.

## Examples

- `src/UmbracoPrism.TestSite/Views/VinylGenreLanding.cshtml`
- `src/UmbracoPrism.TestSite/Views/VinylVaultHome.cshtml`
- `src/UmbracoPrism.TestSite/Views/VinylRecord.cshtml`
- `src/UmbracoPrism.TestSite/Views/Shared/Master.cshtml`

## Anti-Patterns

- Using `Model.Children` or `Model.Parent` in new Umbraco v17 Razor templates.
- Filtering children by raw alias strings when a generated Published Model is available and the template is already document-type-specific.
- Calling synchronous `Html.Partial(...)` from shared layouts.
