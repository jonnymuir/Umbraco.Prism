# Decision: TestSite Views Use Master.cshtml as Shared Layout

**Date:** 2026  
**Author:** Brewster (Umbraco Platform Specialist)  
**Status:** Implemented

## Decision

All TestSite Razor views must use `Layout = "Master"` (not `Layout = null`). Views should contain only page-specific content, styles, and C# logic — never full HTML boilerplate.

## Context

`HomePage.cshtml` and `MemberDashboard.cshtml` were previously `Layout = null` standalone pages. This caused:
- `prism-mobile-nav` web component never being injected on those pages
- `prism-branding.css` (and other shared CSS) never loading
- Duplicated `<header>`, `<footer>`, mobile nav partial in every view

## Rules Going Forward

1. **New views:** Always start with `Layout = "Master";` — never `Layout = null`.
2. **Master.cshtml provides:** DOCTYPE, html/head/body shell, `<link>` tags for shared CSS (`prism-branding.css`), tenant-scoped `:root` CSS variables, shared header, shared footer, `_MobileShellNav` partial, `@RenderBody()`.
3. **Child views provide:** Page-specific CSS `<style>` blocks (including any with Razor expressions like `@Html.Raw(...)` for imagery overrides), page-specific C# logic at top, and HTML content.
4. **Imagery CSS overrides** (e.g. `--prism-hero-image: url('@heroImageUrl')`) must stay as inline `<style>` in child views — they are not static and cannot be extracted to a static CSS file.
5. **Do not** add `<html>`, `<head>`, `<body>`, `<header>`, `<footer>`, or mobile nav partial invocations to child views — Master handles all of these.
