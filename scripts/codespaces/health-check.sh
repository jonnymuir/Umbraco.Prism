#!/bin/bash
# health-check.sh — Quick readiness probe for all Umbraco Prism services.
#
# Usage:
#   bash scripts/codespaces/health-check.sh
#
# Checks each service's readiness endpoint and prints a summary.
# All checks use curl with self-signed cert tolerance (-k).
# A non-zero exit code means at least one service is not yet ready.

set -euo pipefail

CODESPACE_NAME="${CODESPACE_NAME:-}"
DOMAIN="${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}"

# Determine dashboard URL (HTTP in Codespaces, HTTPS locally)
if [ -n "$CODESPACE_NAME" ]; then
    DASHBOARD_URL="http://localhost:15135"
else
    DASHBOARD_URL="https://localhost:17214"
fi

TESTSITE_URL="https://localhost:44345/api/prism/downstream-demo/seed-contract-ready"
KEYCLOAK_URL="https://localhost:8443/realms/prism-dev/.well-known/openid-configuration"
MOCKBIZ_URL="https://localhost:7245/debug/auth"

echo "🔍 Umbraco Prism — service health check"
echo ""

check() {
    local name="$1"
    local url="$2"
    local expected_code="${3:-200}"
    local status
    status=$(curl -sk --max-time 5 -o /dev/null -w "%{http_code}" "$url" 2>/dev/null || echo "000")
    if [ "$status" = "$expected_code" ] || { [ "$expected_code" = "2xx" ] && [[ "$status" -ge 200 ]] && [[ "$status" -lt 300 ]]; }; then
        echo "   ✅ $name (HTTP $status)"
        return 0
    elif [[ "$status" -ge 200 ]] && [[ "$status" -lt 500 ]] && [ "$expected_code" = "aspire" ]; then
        echo "   ✅ $name (HTTP $status)"
        return 0
    else
        echo "   ❌ $name (HTTP $status — expected ~$expected_code)"
        return 1
    fi
}

FAILED=0

check "Status server      (port 3000)" "http://localhost:3000/api/status" "200" || FAILED=1
check "Aspire Dashboard   (port 15135/17214)" "$DASHBOARD_URL" "aspire" || FAILED=1
check "TestSite seed-ready (port 44345)" "$TESTSITE_URL" "200" || FAILED=1
check "Keycloak discovery (port 8443)" "$KEYCLOAK_URL" "200" || FAILED=1
check "MockBusinessApp    (port 7245)" "$MOCKBIZ_URL" "200" || FAILED=1

echo ""

if [ "$FAILED" -eq 0 ]; then
    echo "🎉 All services are ready!"
    if [ -n "$CODESPACE_NAME" ]; then
        echo ""
        echo "   Status page  http://localhost:3000"
        echo "   (Public URLs are in the Codespaces Ports panel or on the status page)"
    fi
    exit 0
else
    echo "⚠️  One or more services are not ready."
    echo "   Check the status page: http://localhost:3000"
    echo "   AppHost log:           tail -f artifacts/startup-status/prism-apphost.log"
    exit 1
fi
