#!/bin/bash

# Runs every time the Codespace starts (postStartCommand in devcontainer.json).
# Launches the Aspire stack in the background and serves a visual startup status
# page on port 3000 so users can see progress rather than blank screens.

DOMAIN="${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}"

# If AppHost is already running (resumed Codespace), just print the URLs and exit.
if pgrep -f "UmbracoPrism.AppHost" > /dev/null 2>&1; then
    echo "✅ Umbraco Prism stack is already running."
    if [ -n "$CODESPACE_NAME" ]; then
        echo ""
        echo "   Startup status    https://${CODESPACE_NAME}-3000.${DOMAIN}"
        echo "   Aspire Dashboard  https://${CODESPACE_NAME}-15135.${DOMAIN}"
        echo "   TestSite          https://${CODESPACE_NAME}-44345.${DOMAIN}"
    fi
    exit 0
fi

echo "🚀 Starting Umbraco Prism stack..."
echo ""

# ── Startup status page (port 3000) ──────────────────────────────────────────
# Serve immediately so the browser has something to show while Aspire starts.
# The status page polls /api/status (served by the same process) which probes
# downstream services server-side, avoiding any CORS issues.
echo "🌐 Starting startup status page on port 3000..."
node scripts/startup-status/server.js > /tmp/prism-status-server.log 2>&1 &
STATUS_SERVER_PID=$!

# Brief pause to confirm the status server is up before Codespaces tries to open it.
sleep 2
if kill -0 "$STATUS_SERVER_PID" 2>/dev/null; then
    echo "✅ Status page ready — open port 3000 in your browser to follow progress."
else
    echo "⚠️  Status server did not start (check /tmp/prism-status-server.log)"
fi

echo ""

# ── Aspire Dashboard anonymous access ────────────────────────────────────────
# In Codespaces, DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS disables the browser
# login token so the Codespaces port proxy doesn't redirect to the token login page.
# ASPIRE_ALLOW_UNSECURED_TRANSPORT is also set — this relaxes OTLP exporter transport
# security for service-to-service communication inside the stack.
# In Codespaces, the dashboard is accessed on HTTP port 15135 (Codespaces proxy forwards HTTP,
# not HTTPS). Locally, port 17214 HTTPS is used directly.
if [ -n "$CODESPACE_NAME" ]; then
    export DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true
    export ASPIRE_ALLOW_UNSECURED_TRANSPORT=true
    DASHBOARD_URL=http://localhost:15135
else
    DASHBOARD_URL=https://localhost:17214
fi

# ── Wait for Docker-in-Docker ─────────────────────────────────────────────────
echo "⏳ Waiting for Docker..."
for i in $(seq 1 30); do
    if docker info > /dev/null 2>&1; then
        echo "✅ Docker ready."
        break
    fi
    sleep 2
    if [ "$i" -eq 30 ]; then
        echo "❌ Docker did not start. Check the docker-in-docker feature."
        kill "$STATUS_SERVER_PID" 2>/dev/null
        exit 1
    fi
done

# ── Launch AppHost ────────────────────────────────────────────────────────────
echo ""
echo "🔄 Launching AppHost (full logs: /tmp/prism-apphost.log)..."
echo "   The status page will update automatically as each service becomes ready."
echo ""
nohup dotnet run --project src/UmbracoPrism.AppHost > /tmp/prism-apphost.log 2>&1 &

# Keep the terminal informed while the status page does the visual heavy-lifting.
echo -n "   Waiting for all services"
for i in $(seq 1 150); do
    # Check Aspire + TestSite + Keycloak + MockBiz
    A=$(curl -sk --max-time 2 -o /dev/null -w "%{http_code}" "$DASHBOARD_URL" 2>/dev/null)
    T=$(curl -s --max-time 3 -o /dev/null -w "%{http_code}" http://localhost:9250/api/health 2>/dev/null)
    K=$(curl -sk --max-time 2 -o /dev/null -w "%{http_code}" https://localhost:8443/realms/prism-dev 2>/dev/null)
    M=$(curl -sk --max-time 2 -o /dev/null -w "%{http_code}" https://localhost:7245 2>/dev/null)

    if [[ "$A" -gt 0 && "$A" -lt 500 ]] && \
       [[ "$T" -gt 0 && "$T" -lt 500 ]] && \
       [[ "$K" -gt 0 && "$K" -lt 500 ]] && \
       [[ "$M" -gt 0 && "$M" -lt 500 ]]; then
        echo " ✅"
        break
    fi
    echo -n "."
    sleep 4
    if [ "$i" -eq 150 ]; then
        echo " ⏱️  (timed out — check the status page or /tmp/prism-apphost.log)"
    fi
done

echo ""
echo "🎉 Umbraco Prism is ready!"
echo ""

# ── Aspire Dashboard diagnostics ──────────────────────────────────────────────
# Always emit — tells us exactly what the dashboard is serving so we can
# debug proxy/Blazor issues without guessing.
echo "🔍 Aspire Dashboard diagnostics:"
echo "   DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=${DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS:-not set}"
echo "   ASPIRE_ALLOW_UNSECURED_TRANSPORT=${ASPIRE_ALLOW_UNSECURED_TRANSPORT:-not set}"
DASH_ROOT_STATUS=$(curl -sk --max-time 5 -o /dev/null -w "%{http_code}" "${DASHBOARD_URL}/" 2>/dev/null || echo "curl-failed")
echo "   / (root) HTTP status:             ${DASH_ROOT_STATUS}"
BLAZOR_LINE=$(curl -sk --max-time 5 -o /dev/null -w "HTTP %{http_code}  content-type: %{content_type}" "${DASHBOARD_URL}/_framework/blazor.web.js" 2>/dev/null || echo "curl-failed")
echo "   /_framework/blazor.web.js:        ${BLAZOR_LINE}"
DASH_BASEHREF=$(curl -sk --max-time 5 "${DASHBOARD_URL}/" 2>/dev/null | grep -o '<base href="[^"]*"' | head -1 || echo "(grep found nothing)")
echo "   <base href> in dashboard HTML:    ${DASH_BASEHREF:-not found}"
echo ""

if [ -n "$CODESPACE_NAME" ]; then
    echo "   Status page       https://${CODESPACE_NAME}-3000.${DOMAIN}"
    echo "   Aspire Dashboard  https://${CODESPACE_NAME}-15135.${DOMAIN}"
    echo "   TestSite          https://${CODESPACE_NAME}-44345.${DOMAIN}"
    echo "   Keycloak admin    https://${CODESPACE_NAME}-8443.${DOMAIN}/admin"
else
    echo "   Status page       http://localhost:3000"
    echo "   Aspire Dashboard  https://localhost:17214"
    echo "   TestSite          https://localhost:44345"
    echo "   Keycloak admin    https://localhost:8443/admin"
fi

echo ""
echo "   TestSite SSO      demo@prism.local  /  password"
echo "   Backoffice        admin@prism.local  /  PrismLocal!12345"
echo "   Keycloak admin    admin  /  admin"
echo ""
