#!/bin/bash
# Validate debugger shutdown cleanup for Aspire process tree
# Usage: Run BEFORE starting debugger, then again AFTER stopping it

set -e

echo "=== Aspire Process Tree Validation ==="
echo ""

# Aspire dashboard ports
DASHBOARD_HTTPS=17214
DASHBOARD_HTTP=15135
OTLP_PORT=21233
RESOURCE_SERVICE_PORT=22194

echo "Checking for listeners on Aspire dashboard ports..."
echo ""

has_listeners=false

for port in $DASHBOARD_HTTPS $DASHBOARD_HTTP $OTLP_PORT $RESOURCE_SERVICE_PORT; do
  result=$(lsof -i :$port -t 2>/dev/null || true)
  if [ -n "$result" ]; then
    echo "❌ Port $port has listeners (PIDs: $result)"
    # Show process details
    ps -p $result -o pid,ppid,comm,args 2>/dev/null | head -20
    echo ""
    has_listeners=true
  else
    echo "✅ Port $port is free"
  fi
done

echo ""
echo "Checking for stale Aspire DCP processes..."
dcp_processes=$(ps aux | grep -E "dotnet.*Aspire\.(AppHost|Hosting\.Dcp)" | grep -v grep || true)
if [ -n "$dcp_processes" ]; then
  echo "❌ Found Aspire/DCP processes:"
  echo "$dcp_processes"
  has_listeners=true
else
  echo "✅ No Aspire/DCP processes found"
fi

echo ""
echo "Checking for stale Keycloak containers..."
keycloak_containers=$(docker ps --filter "ancestor=quay.io/keycloak/keycloak" --format "{{.ID}} {{.Names}}" 2>/dev/null || true)
if [ -n "$keycloak_containers" ]; then
  echo "❌ Found Keycloak containers:"
  echo "$keycloak_containers"
  has_listeners=true
else
  echo "✅ No Keycloak containers running"
fi

echo ""
if [ "$has_listeners" = true ]; then
  echo "VERDICT: ❌ Stale processes/containers remain after debugger stop"
  exit 1
else
  echo "VERDICT: ✅ Clean shutdown — no stale processes"
  exit 0
fi
