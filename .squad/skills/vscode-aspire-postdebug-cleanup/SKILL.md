---
name: "vscode-aspire-postdebug-cleanup"
description: "Automatically clean up orphaned Aspire DCP processes and Docker containers when stopping the VS Code debugger"
domain: "developer-experience"
confidence: "high"
source: "earned"
---

## Context

Use this when debugging .NET Aspire applications in VS Code and orphaned processes (DCP, dashboard, child services) or Docker containers remain running after stopping the debugger.

## Problem

The VS Code .NET debugger terminates the AppHost process but does not automatically clean up:
- Child processes spawned by Aspire's DCP (Distributed Application Runtime)
- Aspire Dashboard processes
- Docker containers launched by the AppHost
- Background services managed by DCP

This causes port conflicts on subsequent debug sessions and requires manual cleanup.

## Solution Pattern

### 1. Create a Cleanup Script

Create a shell script that:
- Finds process PIDs using `ps aux | grep <pattern>`
- Terminates processes gracefully with `kill $PID`, then force kills with `kill -9 $PID` if needed
- Stops and removes Docker containers with `docker ps --filter "label=aspire.resource.name"`

**Key requirements:**
- Use specific PIDs, not name-based killing (`pkill`/`killall`) for security
- Handle both processes and containers
- Fail gracefully if nothing needs cleanup
- Provide user-friendly output

### 2. Wire as VS Code Task

In `.vscode/tasks.json`:
```json
{
  "label": "Aspire: cleanup processes",
  "type": "shell",
  "command": "${workspaceFolder}/scripts/cleanup-aspire-processes.sh",
  "problemMatcher": [],
  "presentation": {
    "reveal": "silent",
    "close": true
  }
}
```

The `presentation` config keeps the UI clean (no persistent output panel).

### 3. Add postDebugTask to Launch Configuration

In `.vscode/launch.json`:
```json
{
  "name": "C#: Aspire (Full Stack)",
  "type": "coreclr",
  "request": "launch",
  "preLaunchTask": "Full Stack: prepare AppHost",
  "postDebugTask": "Aspire: cleanup processes",
  // ... rest of config
}
```

## Process Identification Patterns

Common patterns to grep for Aspire processes:
- `UmbracoPrism\.AppHost` (or your AppHost name)
- `aspire.*dashboard`
- `dcp.*<your-app>`
- `Aspire\.Hosting`

Container identification:
- Label: `aspire.resource.name` (standard Aspire container label)

## Example Cleanup Script

```bash
#!/bin/bash
set -euo pipefail

echo "🧹 Cleaning up Aspire processes..."

# Find Aspire-related PIDs
PIDS=$(ps aux | grep -E "UmbracoPrism\.AppHost|aspire.*dashboard" | grep -v grep | awk '{print $2}' || true)

if [ -n "$PIDS" ]; then
    for PID in $PIDS; do
        if kill -0 "$PID" 2>/dev/null; then
            kill "$PID" 2>/dev/null || true
        fi
    done
    
    sleep 1
    
    for PID in $PIDS; do
        if kill -0 "$PID" 2>/dev/null; then
            kill -9 "$PID" 2>/dev/null || true
        fi
    done
fi

# Clean up Docker containers
if command -v docker &> /dev/null; then
    docker ps -a --filter "label=aspire.resource.name" --format "{{.ID}}" | while read CONTAINER_ID; do
        docker stop "$CONTAINER_ID" 2>/dev/null || true
        docker rm "$CONTAINER_ID" 2>/dev/null || true
    done
fi

echo "✨ Cleanup complete"
```

## Benefits

- **Zero manual intervention:** Cleanup happens automatically on debugger stop
- **Port availability:** Subsequent debug sessions start cleanly
- **Container hygiene:** Prevents accumulation of stopped containers
- **Developer experience:** Removes friction from the debug cycle

## Limitations

- Cleanup runs *after* debugger stops, so brief window where processes remain
- If VS Code crashes, `postDebugTask` won't run (edge case)
- Developers need executable permissions on the cleanup script (`chmod +x`)

## Files in This Repo

- Script: `scripts/cleanup-aspire-processes.sh`
- Task: `.vscode/tasks.json` → `"Aspire: cleanup processes"`
- Launch config: `.vscode/launch.json` → `"postDebugTask": "Aspire: cleanup processes"`

## Related Patterns

- `preLaunchTask` for pre-debug setup (e.g., building, validating prerequisites)
- Aspire health checks to ensure containers are ready before dependent services start
- Launch profile pinning to ensure correct project endpoints are exposed

## References

- VS Code debugging documentation: [postDebugTask](https://code.visualstudio.com/docs/editor/debugging#_launchjson-attributes)
- Aspire DCP architecture
- Docker container labeling conventions
