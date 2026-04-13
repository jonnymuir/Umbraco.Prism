---
name: "aspire-project-endpoint-profiles"
description: "Keep Aspire-advertised localhost URLs aligned with executable project launch profiles and downstream config"
domain: "orchestration"
confidence: "high"
source: "earned"
tools:
  - name: "bash"
    description: "Run AppHost or standalone projects and probe the advertised localhost endpoints."
    when: "Use when validating that Aspire resource URLs match what the process actually listens on."
---

## Context

Use this when an Aspire executable project starts successfully but the dashboard does not show the expected browser URL, or when a downstream app hardcodes a different localhost origin than the one Aspire launches.

## Patterns

### Use launch profiles as the fixed-port source of truth

- For executable project resources, give Aspire a concrete `launchProfileName` so it can discover the intended `applicationUrl`.
- If a service should come up on HTTPS by default for fresh clones, make the `https` profile the default launch profile in `launchSettings.json`.

### Keep AppHost and app config on the same origin

- When one app calls another locally, derive the caller's config from the same URL Aspire advertises.
- In this repo, `PrismBusinessApp__WorkflowApiBaseUrl` and `KEYCLOAK_URL` should match the fixed HTTPS origins configured for `businessapp` and `keycloak-proxy`.

### Avoid duplicated port binding logic

- Do not hardcode a Kestrel localhost port in code if `launchSettings.json` is already the canonical local binding.
- Removing the duplicate binding keeps standalone `dotnet run` behavior and Aspire resource advertisement from drifting apart.

## Examples

- `src/UmbracoPrism.AppHost/Program.cs` explicitly selects `launchProfileName: "https"` for `keycloak-proxy` and `businessapp`.
- `src/UmbracoPrism.KeycloakProxy/Properties/launchSettings.json` defines `https://localhost:8443`, which Aspire can advertise directly.
- `src/UmbracoPrism.MockBusinessApp/Properties/launchSettings.json` makes the HTTPS profile default so `dotnet run --project src/UmbracoPrism.MockBusinessApp` listens on `https://localhost:7245`.
- `src/UmbracoPrism.TestSite/Controllers/DownstreamDemoController.cs` derives its default downstream target from `PrismBusinessApp:WorkflowApiBaseUrl` instead of hardcoding a second URL.

## Anti-Patterns

- **Relying on profile order accidentally** — Aspire may launch a project, but the dashboard can miss the intended URL if the wrong profile is inferred.
- **Hardcoding Kestrel ports and launch settings separately** — creates drift between what the process listens on and what Aspire advertises.
- **Hardcoding demo/downstream URLs in controllers** — guarantees local endpoint drift the next time ports or schemes change.
