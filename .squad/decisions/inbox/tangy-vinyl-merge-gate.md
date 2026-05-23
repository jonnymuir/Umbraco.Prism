---
author: tangy
date: 2026-05-23T13:51:28.022+01:00
status: proposed
area: notifications-boundary
---

# Decision: vinylRecord Notification Boundary Regression Guards

## Context

A boundary refactor moved vinyl-record notification logic from a hardcoded TestSite handler (`UmbracoPrism.TestSite/PrismContentPublishedHandler`) into a general-purpose, config-driven Core handler (`UmbracoPrism.Core/Notifications/PrismContentPublishedHandler`). After the refactor, **both handlers remain registered** — the Core composer and the TestSite composer each add their own `ContentPublishedNotification` handler — creating a double-fire risk when `vinylRecord` content is published in the TestSite runtime.

## What Was Missing

The existing `PrismContentPublishedHandlerTests` only used `newsArticle` and `announcement` as configured content types. There were no tests:
- Explicitly configuring `vinylRecord` in `Prism:Notifications:NotifiableContentTypes`
- Proving the Core handler is silent when `vinylRecord` is absent from config (the primary double-fire guard)

## Decision

Added 4 targeted regression guards to `PrismContentPublishedHandlerTests.cs`:

| Test | Purpose |
|------|---------|
| `Handle_VinylRecord_ConfigDriven_WithGenre_SendsToGenreSubscribers` | Proves Core handler routes to genre subscribers when `vinylRecord` is configured and genre is set |
| `Handle_VinylRecord_ConfigDriven_WithoutGenre_SendsToAllMembers` | Proves Core handler falls back to all-members broadcast when genre is absent |
| `Handle_VinylRecord_NotInConfig_CoreHandlerIsSilent_DoubleFirGuard` | **Primary double-fire guard**: Core handler is completely silent when `vinylRecord` is absent from config, so the TestSite handler remains the sole sender |
| `Handle_EmptyNotifiableTypes_CoreHandlerIsSilent_ForAnyContentType` | Guard: empty `NotifiableContentTypes` config produces a fully inert Core handler |

## Noted Risk (not fixed here)

The double-fire risk is managed by keeping `vinylRecord` absent from `Prism:Notifications:NotifiableContentTypes` in the TestSite's appsettings. If a future operator adds `vinylRecord` to that config key while the TestSite handler is still registered, subscribers will receive two notifications per publish. The recommended long-term fix is to retire `TestSite/PrismContentPublishedHandler` and rely solely on the Core config-driven handler — but that is a separate task for Blathers (config docs) and whoever owns TestSite cleanup.

## Validation

```
dotnet test UmbracoPrism.sln -c Release --filter "FullyQualifiedName~UmbracoPrism.Core.Tests"
# Result: 815 passed, 0 failed, 0 skipped (was 811 before this session)
```

All 4 new guards: ✅ GREEN
Full suite: ✅ 815/815 GREEN — no regressions introduced.

## Green Lane Sign-off

The branch is green enough to proceed to final check-in/merge for the core tests lane. The `storybook-tests` and `workflow-graph-visual` lanes require CI (headless Storybook server); no unrelated baseline failures observed locally. The double-fire architectural risk is documented above and flagged for a future cleanup task.
