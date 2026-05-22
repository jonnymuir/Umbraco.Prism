#!/bin/bash
# Cleanup orphaned Aspire AppHost and DCP processes after debugging
# This script finds and terminates process trees related to UmbracoPrism.AppHost
# and cleans up any Docker containers spawned by Aspire

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "🧹 Cleaning up Aspire processes..."

# Find all PIDs related to UmbracoPrism.AppHost (parent and children)
# This includes: AppHost itself, DCP, dashboard, and spawned services
PIDS=$(ps aux | grep -E "UmbracoPrism\.AppHost|aspire.*dashboard|dcp.*UmbracoPrism" | grep -v grep | awk '{print $2}' || true)

if [ -z "$PIDS" ]; then
    echo -e "${GREEN}✓${NC} No orphaned Aspire processes found"
else
    # Count processes
    COUNT=$(echo "$PIDS" | wc -w | tr -d ' ')
    echo -e "${YELLOW}Found $COUNT process(es) to clean up${NC}"

    # Kill each PID individually (safer than killall/pkill)
    for PID in $PIDS; do
        if kill -0 "$PID" 2>/dev/null; then
            echo "  Terminating PID $PID..."
            kill "$PID" 2>/dev/null || true
        fi
    done

    # Wait briefly for graceful shutdown
    sleep 1

    # Force kill any remaining processes
    for PID in $PIDS; do
        if kill -0 "$PID" 2>/dev/null; then
            echo "  Force killing PID $PID..."
            kill -9 "$PID" 2>/dev/null || true
        fi
    done
    
    echo -e "${GREEN}✓${NC} Process cleanup complete"
fi

# Clean up any Aspire-spawned Docker containers
# Look for containers with labels that Aspire adds
if command -v docker &> /dev/null; then
    echo ""
    echo "🐳 Checking for Aspire Docker containers..."
    
    # Find containers created by Aspire (they have specific labels)
    ASPIRE_CONTAINERS=$(docker ps -a --filter "label=aspire.resource.name" --format "{{.ID}} {{.Names}}" 2>/dev/null || true)
    
    if [ -n "$ASPIRE_CONTAINERS" ]; then
        echo "$ASPIRE_CONTAINERS" | while read -r CONTAINER_INFO; do
            CONTAINER_ID=$(echo "$CONTAINER_INFO" | awk '{print $1}')
            CONTAINER_NAME=$(echo "$CONTAINER_INFO" | awk '{print $2}')
            echo "  Stopping container: $CONTAINER_NAME ($CONTAINER_ID)..."
            docker stop "$CONTAINER_ID" 2>/dev/null || true
            docker rm "$CONTAINER_ID" 2>/dev/null || true
        done
        echo -e "${GREEN}✓${NC} Container cleanup complete"
    else
        echo -e "${GREEN}✓${NC} No Aspire containers found"
    fi
fi

echo ""
echo -e "${GREEN}✨ All cleanup complete${NC}"

