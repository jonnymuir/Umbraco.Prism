# Blathers — Aspire dashboard launch timing

## Context

VS Code's `C#: Aspire (Full Stack)` launch configuration in `.vscode/launch.json` was setting `launchUrl` to the Aspire dashboard URL. That makes VS Code open the browser immediately when debugging starts, which races ahead of the AppHost and can land on the dashboard before Aspire is actually ready.

The AppHost already owns the correct readiness-aware browser behavior through `src/UmbracoPrism.AppHost/Properties/launchSettings.json`, where the `https` profile has `launchBrowser: true`.

## Decision

Remove the eager `launchUrl` from `.vscode/launch.json` and rely on the AppHost launch profile/browser launch flow instead.

## Why

- Avoids opening the dashboard before Aspire has finished initializing.
- Keeps the browser-launch responsibility with the AppHost profile that actually knows when startup is ready.
- Minimizes repo churn by making a one-line config change instead of adding custom launch logic.

## Standing Effect

- For repo-owned Aspire launch configurations, do not set a VS Code `launchUrl` when the AppHost launch profile already handles browser launch.
- If browser timing needs adjustment later, prefer changing the AppHost launch profile rather than reintroducing an eager editor-side URL launch.
