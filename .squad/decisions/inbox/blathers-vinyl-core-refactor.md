---
author: blathers
date: 2026-05-23T13:51:28.022+01:00
status: implemented
area: notifications
---

# Decision: Vinyl/Core notification boundary — backend implementation

## Context

The vinyl demo features (`PrismVinylNotificationController`, `PrismVinylBackInStockRequest`,
`LimitedEditionDropNotifier`) were embedded in `UmbracoPrism.Core`, making Core domain-specific.
The TestSite had a duplicate `PrismContentPublishedHandler` that overlapped with Core's
config-driven `PrismContentPublishedHandler`, risking double-fire on `ContentPublishedNotification`.

Tom Nook, Brewster, and Tangy aligned on the split before implementation.

## Decision

### Moved out of Core → TestSite

- `PrismVinylNotificationController` — vinyl-specific API endpoint, lives in `UmbracoPrism.TestSite.Controllers`
- `PrismVinylBackInStockRequest` — vinyl-specific request model, lives in `UmbracoPrism.TestSite.Controllers.Models`
- `LimitedEditionDropNotifier` — vinyl-specific background service, lives in `UmbracoPrism.TestSite.BackgroundServices`

`LimitedEditionDropNotifier` is registered via `TestSiteComposer.builder.Services.AddHostedService<>()`,
not PrismComposer, so it is absent from any downstream host that does not use the TestSite composer.

### Deleted duplicate TestSite handler

The old TestSite `PrismContentPublishedHandler` was deleted. Core's config-driven handler
(`UmbracoPrism.Core.Notifications.PrismContentPublishedHandler`) is the single keeper.
`Prism:Notifications:NotifiableContentTypes` in the TestSite `appsettings.json` is set to
`vinylRecord` so the Core handler fires exactly once per vinyl publish.

### TestSite `appsettings.json`

Added:
```json
"Prism": {
  "Notifications": {
    "NotifiableContentTypes": "vinylRecord"
  }
}
```

### Security tests preserved

The Phase1SecurityRegressionTests and PrismVinylNotificationSecurityTests that verified
security properties of the vinyl controller and request model were updated to reference
`UmbracoPrism.TestSite.Controllers` and `UmbracoPrism.TestSite.Controllers.Models`.
These contracts remain tested and enforced.

### Fixture ordering fix

`WorkflowPatchServiceFailureTests` was using a direct assembly-path fixture locator
instead of the shared `WorkflowAuthoringFixtureLocator`. This caused a test ordering
race with `WorkflowAuthoringEndpointsTests` (which resets the fixture directory on
factory init). Switched to `WorkflowAuthoringFixtureLocator.GetFixturesPath()` —
the same source-tree-fallback-aware locator used by patch service and preview service tests.

## Consequences

- Core is now free of vinyl domain knowledge; downstream hosts that consume Core can use
  the push notification infrastructure without pulling in vinyl-specific controllers.
- Double-fire is impossible: the duplicate TestSite handler is gone; the Core handler fires
  iff `vinylRecord` is in `NotifiableContentTypes`.
- 815 backend tests pass, build is warning-clean.
