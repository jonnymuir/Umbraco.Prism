# Aspire Debugger Cleanup Validation

## Problem

When stopping the VS Code debugger for the "C#: Aspire (Full Stack)" configuration, the CoreCLR debugger terminates the AppHost process but does not recursively clean up the full Aspire DCP process tree. This leaves behind:

- Child project services (TestSite, MockBusinessApp, KeycloakProxy)
- Aspire dashboard processes
- Docker containers (Keycloak)
- Port listeners on dashboard endpoints (17214, 15135, 21233, 22194)

This is a known VS Code + Aspire limitation documented in [dotnet/aspire#625](https://github.com/dotnet/aspire/issues/625).

## Solution

The repo uses VS Code's `postDebugTask` to automatically clean up stale processes after debugger stop.

### Automatic cleanup (already configured)

When you stop the debugger:
1. VS Code terminates the AppHost process
2. `postDebugTask` automatically runs `scripts/cleanup-aspire-processes.sh`
3. Script terminates orphaned AppHost/DCP processes and stops Docker containers

**No manual action required.**

### Manual validation

To verify cleanup is working:

```bash
# Before starting debugger
./scripts/validate-debugger-cleanup.sh
# Expected: ✅ Clean shutdown — no stale processes

# Start debugger, wait for Aspire to fully start, then stop it

# After stopping debugger
./scripts/validate-debugger-cleanup.sh
# Expected: ✅ Clean shutdown — no stale processes
```

### Manual cleanup (if automatic fails)

If validation shows stale processes after debugger stop:

```bash
./scripts/cleanup-aspire-processes.sh
```

## Files

- **`.vscode/launch.json`** — Aspire launch config with `postDebugTask: "Aspire: cleanup after debug"`
- **`.vscode/tasks.json`** — Task definition for cleanup script
- **`scripts/cleanup-aspire-processes.sh`** — Cleanup script (AppHost/DCP PIDs + Docker containers)
- **`scripts/validate-debugger-cleanup.sh`** — Validation script (check for stale processes/containers)

## Related

- **Decision:** `.squad/decisions/inbox/tangy-debugger-shutdown-validation.md`
- **Test patterns:** `src/UmbracoPrism.Client/tests/support/live-app-host.ts` (programmatic cleanup)
- **Skill:** `.squad/skills/playwright-aspire-restart-harness/SKILL.md` (test-owned restart patterns)
