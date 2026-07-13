---
name: "aspire-launch-profile-endpoints"
description: "Keep Aspire localhost URLs visible by pinning the real launch profile for each project resource"
domain: "developer-experience"
confidence: "high"
source: "earned"
---

## Context

Use this when an Aspire AppHost runs repo-owned projects that do not all share Aspire's conventional `https` launch profile name, or when a project uses a fixed localhost port that developers need to see directly in the dashboard.

## Patterns

### Pin the real launch profile in AppHost

- Do not rely on Aspire's default profile matching when a project uses a custom profile name such as Umbraco's `Umbraco.Web.UI`.
- If the resource should expose a specific localhost HTTPS URL in Aspire, pass `launchProfileName` explicitly from `src/UmbracoPrism.AppHost/Program.cs`.

### Advertise fixed localhost ports through launch settings

- If a project listens on a fixed port outside Aspire's conventions, add repo-owned `Properties/launchSettings.json` so the dashboard can display a usable URL.
- This is especially important for helper processes like local HTTPS proxies.

### Keep calling code aligned with the visible endpoint

- If a local dashboard or controller calls another local service, resolve that target from configuration rather than duplicating a hardcoded URL in markup or controller defaults.
- Add a small automated guard that verifies both the visible endpoint wiring and the graceful failure contract.

## Examples

- `src/UmbracoPrism.AppHost/Program.cs` explicitly selects `launchProfileName: "Umbraco.Web.UI"` for TestSite and `launchProfileName: "https"` for MockBusinessApp and KeycloakProxy.
- `src/UmbracoPrism.KeycloakProxy/Properties/launchSettings.json` advertises `https://localhost:8443`.
- `src/UmbracoPrism.Core.Tests/DashboardLocalEndpointsValidationTests.cs` validates the endpoint wiring plus the dashboard's friendly network-failure payload.

## Anti-Patterns

- Assuming Aspire will pick the right launch profile automatically for every project.
- Exposing a fixed localhost port in code but not in launch settings, leaving the dashboard without a usable URL.
- Hardcoding a local downstream URL in a controller or view while AppHost advertises a different endpoint.
