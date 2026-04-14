---
name: "protected-page-token-warmup"
description: "Avoid auth-cookie churn by deferring downstream token refresh work until an API/action actually needs it"
domain: "authentication"
confidence: "high"
source: "earned"
---

## Context

Use this when a protected server-rendered page is doing eager downstream token work during the initial HTML request.

## Pattern

- Let the protected page render first.
- Do not call refresh-capable token helpers just to “warm” the page.
- Keep refresh logic in the downstream action/API that truly needs the bearer token.

## Why

- First-navigation cookie renewal can turn a healthy sign-in callback into a redirect loop before browser tests reach their first product assertion.
- Readiness contracts become much more reliable when page renders and downstream token refreshes are separated.

## Example

- `src/UmbracoPrism.Core/Controllers/MemberDashboardController.cs` no longer warms Prism bearer tokens during the initial `/dashboard` render.
