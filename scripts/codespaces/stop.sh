#!/bin/bash
# stop.sh — Gracefully stop all running Umbraco Prism services in this Codespace.
#
# Usage:
#   bash scripts/codespaces/stop.sh
#
# What this does:
#   1. Kills the Aspire AppHost (UmbracoPrism.AppHost dotnet process)
#   2. Kills the startup status server (scripts/startup-status/server.js)
#
# Safe to run even if services are already stopped.

set -euo pipefail

echo "🛑 Stopping Umbraco Prism stack..."

STOPPED_ANYTHING=false

# Kill AppHost (dotnet run on UmbracoPrism.AppHost)
APPHOST_PIDS=$(pgrep -f "UmbracoPrism.AppHost" 2>/dev/null || true)
if [ -n "$APPHOST_PIDS" ]; then
    echo "   Stopping AppHost (PIDs: $APPHOST_PIDS)..."
    echo "$APPHOST_PIDS" | xargs kill 2>/dev/null || true
    sleep 2
    # Force-kill anything that didn't exit cleanly
    APPHOST_REMAINING=$(pgrep -f "UmbracoPrism.AppHost" 2>/dev/null || true)
    if [ -n "$APPHOST_REMAINING" ]; then
        echo "   Force-stopping stubborn AppHost process(es)..."
        echo "$APPHOST_REMAINING" | xargs kill -9 2>/dev/null || true
    fi
    STOPPED_ANYTHING=true
else
    echo "   AppHost is not running."
fi

# Kill the startup status server (node scripts/startup-status/server.js)
STATUS_PIDS=$(pgrep -f "startup-status/server.js" 2>/dev/null || true)
if [ -n "$STATUS_PIDS" ]; then
    echo "   Stopping status server (PIDs: $STATUS_PIDS)..."
    echo "$STATUS_PIDS" | xargs kill 2>/dev/null || true
    STOPPED_ANYTHING=true
else
    echo "   Status server is not running."
fi

if [ "$STOPPED_ANYTHING" = true ]; then
    echo ""
    echo "✅ Stack stopped. Ports 3000, 17214, 44345, 8443, 7245 should now be free."
else
    echo ""
    echo "✅ Nothing was running — already stopped."
fi
