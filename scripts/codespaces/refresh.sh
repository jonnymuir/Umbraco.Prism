#!/bin/bash
# refresh.sh — Pull latest code and restart the Umbraco Prism stack in a Codespace.
#
# Usage:
#   bash scripts/codespaces/refresh.sh [--rebuild] [--no-start]
#
# Options:
#   --rebuild    Also restore NuGet packages and rebuild the solution before restarting.
#                Use after pulling changes that add/remove packages or change project structure.
#   --no-start   Stop and update but do NOT restart the stack. Useful for inspection.
#
# What this does (by default):
#   1. Stop the running stack (AppHost + status server)
#   2. git pull origin main
#   3. If package-lock.json changed since last pull, re-run npm install
#   4. Restart via the repo's real startup contract (.devcontainer/on-start.sh)
#
# With --rebuild:
#   3a. dotnet restore UmbracoPrism.sln
#   3b. dotnet build UmbracoPrism.sln --no-restore
#   (then npm install if needed, then restart)
#
# Readiness: after restart, check port 3000 status page or run:
#   bash scripts/codespaces/health-check.sh

set -euo pipefail

REBUILD=false
NO_START=false

for arg in "$@"; do
    case "$arg" in
        --rebuild) REBUILD=true ;;
        --no-start) NO_START=true ;;
        *)
            echo "Unknown option: $arg"
            echo "Usage: bash scripts/codespaces/refresh.sh [--rebuild] [--no-start]"
            exit 1
            ;;
    esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

echo "🔄 Umbraco Prism — Codespace refresh"
echo "   Options: rebuild=$REBUILD  no-start=$NO_START"
echo ""

# ── Step 1: Stop ──────────────────────────────────────────────────────────────
bash scripts/codespaces/stop.sh
echo ""

# ── Step 2: Pull latest code ─────────────────────────────────────────────────
echo "⬇️  Pulling latest from origin/main..."
BEFORE_HASH=$(git rev-parse HEAD 2>/dev/null || echo "unknown")
git pull origin main
AFTER_HASH=$(git rev-parse HEAD 2>/dev/null || echo "unknown")

if [ "$BEFORE_HASH" = "$AFTER_HASH" ]; then
    echo "   Already up to date ($(git rev-parse --short HEAD))."
else
    echo "   Updated: $(git rev-parse --short "$BEFORE_HASH") → $(git rev-parse --short "$AFTER_HASH")"
fi
echo ""

# ── Step 3: Rebuild if requested ─────────────────────────────────────────────
if [ "$REBUILD" = true ]; then
    echo "🔨 Rebuilding solution (--rebuild flag set)..."
    dotnet restore UmbracoPrism.sln
    dotnet build UmbracoPrism.sln --no-restore
    echo "✅ Build complete."
    echo ""
fi

# ── Step 4: npm install if package-lock.json changed ─────────────────────────
# Check if package-lock.json changed between the two commits (only if we updated)
NEEDS_NPM=false
if [ "$BEFORE_HASH" != "$AFTER_HASH" ]; then
    if git diff --name-only "$BEFORE_HASH" "$AFTER_HASH" 2>/dev/null | grep -q "package-lock.json"; then
        NEEDS_NPM=true
    fi
fi

if [ "$NEEDS_NPM" = true ]; then
    echo "📦 package-lock.json changed — re-installing Node.js dependencies..."
    cd src/UmbracoPrism.Client && npm install && cd "$REPO_ROOT"
    echo "✅ npm install complete."
    echo ""
elif [ "$REBUILD" = true ]; then
    # Also run npm install on full rebuild to be safe
    echo "📦 Running npm install (full rebuild path)..."
    cd src/UmbracoPrism.Client && npm install && cd "$REPO_ROOT"
    echo "✅ npm install complete."
    echo ""
fi

# ── Step 5: Restart ──────────────────────────────────────────────────────────
if [ "$NO_START" = true ]; then
    echo "⏹️  --no-start specified — stack not restarted."
    echo "   When ready, start manually with: bash .devcontainer/on-start.sh"
    exit 0
fi

echo "🚀 Restarting stack via on-start.sh..."
echo ""
bash .devcontainer/on-start.sh
