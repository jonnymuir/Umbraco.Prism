---
name: "aspire-prereq-validation"
description: "Guard VS Code Aspire launch paths with explicit prerequisite validation and actionable errors"
domain: "developer-experience"
confidence: "high"
source: "earned"
tools:
  - name: "bash"
    description: "Reproduce AppHost startup failures and verify prerequisite commands"
    when: "Use when confirming whether an Aspire startup issue is caused by local machine setup."
---

## Context

Use this pattern when a repo includes an Aspire AppHost and developers launch it through VS Code tasks or launch configurations. It is especially useful when external prerequisites such as the Aspire workload or Docker cannot be bundled with the repository and missing setup currently surfaces as a confusing runtime exception.

## Patterns

### Reproduce before changing UX

- First reproduce with the same AppHost entry point used by the launch configuration, for example `dotnet run --project src/Some.AppHost`.
- If startup fails with missing `CliPath` / `DashboardPath`, treat that as a likely local tooling prerequisite problem before changing application code.

### Avoid eager dashboard launches

- If a VS Code Aspire `dotnet` launch configuration points at the AppHost and the AppHost launch profile already has `launchBrowser: true`, do not also set `launchUrl` in `.vscode/launch.json`.
- Let the AppHost/browser launch flow open the dashboard when Aspire is actually ready, instead of having the editor open the URL immediately at debug start.

### If AppHost still opens too early, move browser launch to VS Code

- Aspire AppHost can log `Now listening on:` for the outer host before the dashboard process and resources are fully ready.
- In that case, disable `launchBrowser` in the AppHost `launchSettings.json` and switch the VS Code launch to a `coreclr` configuration that opens the dashboard via `serverReadyAction`.
- Match a later Aspire readiness log such as `Distributed application started.` and point `uriFormat` at the dashboard URL, so VS Code opens the page after orchestration has actually started.

### Put the guard in the launch path

- Add a repo-owned preflight step in `.vscode/tasks.json`.
- Point the launch configuration at a dedicated preparation task rather than a raw build task.
- Keep the preparation task small and focused: validate prerequisites, then continue into the existing build path.

### Make failures actionable

- Print the exact install or startup command the developer needs next.
- Mention the symptom the guard prevents, so users can connect the new message to the old exception they saw.
- Check all required external prerequisites for the launch path in one pass; for full-stack Aspire this commonly includes the Aspire workload and Docker/container runtime.

## Examples

- `scripts/validate-aspire-prereqs.mjs` checks `dotnet workload list` for `aspire` and verifies `docker info` succeeds.
- `.vscode/tasks.json` defines `Aspire: validate prerequisites` plus a composite `Full Stack: prepare` task.
- `.vscode/launch.json` uses `Full Stack: prepare` as the `preLaunchTask` for the AppHost configuration.
- `.vscode/launch.json` can use `type: "coreclr"` plus `serverReadyAction` on `Distributed application started.` when the AppHost launch profile opens the dashboard too early.

## Anti-Patterns

- **Changing AppHost code first** — if the failure is missing orchestration tooling, code changes in `Program.cs` will not fix it.
- **Relying on tribal knowledge** — requiring developers to remember `dotnet workload install aspire` without launch-path validation keeps the experience brittle.
- **Checking only one prerequisite** — if the full stack also needs Docker, validate that too so the next failure is not just deferred.
- **Setting both `launchBrowser` and VS Code `launchUrl`** — the editor-side URL launch can race ahead of AppHost readiness and show the dashboard too early.
- **Relying on AppHost `launchBrowser` after observing early opens** — once logs show the dashboard URL is announced before Aspire reports `Distributed application started.`, the launch profile alone is too early to be the browser trigger.
