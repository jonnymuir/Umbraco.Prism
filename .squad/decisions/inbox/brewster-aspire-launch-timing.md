## Brewster — Aspire dashboard launch timing

**Context:** After removing the explicit VS Code `launchUrl`, the Aspire dashboard still opened too early when launching the AppHost from VS Code. Direct AppHost logs showed `Now listening on: https://localhost:17214` before Aspire had started the dashboard process and before it logged `Distributed application started.`.

**Decision:** Move browser opening responsibility from the AppHost launch profile to VS Code:

- Set `src/UmbracoPrism.AppHost/Properties/launchSettings.json` `launchBrowser` to `false`
- Launch the AppHost from `.vscode/launch.json` with a `coreclr` configuration
- Open the dashboard with `serverReadyAction` when Aspire logs `Distributed application started.`
- Build the AppHost explicitly from `.vscode/tasks.json` before launching the compiled DLL

**Why:** The AppHost launch-profile browser behavior is tied to the outer host becoming reachable, which happens before the Aspire dashboard is fully usable. VS Code `serverReadyAction` lets the repo key browser opening to a later, more accurate readiness signal without changing AppHost runtime behavior.

**Standing Effect:** If this repo's Aspire dashboard starts opening too early again, treat AppHost `launchBrowser` as too coarse-grained and prefer a VS Code-controlled browser open keyed to a later Aspire readiness log.
