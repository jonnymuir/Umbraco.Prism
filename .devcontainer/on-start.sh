#!/bin/bash

# Runs every time the Codespace starts (postStartCommand in devcontainer.json).
# Launches the Aspire stack in the background and waits until services are ready.

# If AppHost is already running (resumed Codespace), just print the URLs and exit.
if pgrep -f "UmbracoPrism.AppHost" > /dev/null 2>&1; then
    echo "✅ Umbraco Prism stack is already running."
    if [ -n "$CODESPACE_NAME" ]; then
        DOMAIN="${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}"
        echo ""
        echo "   Aspire Dashboard: https://${CODESPACE_NAME}-17214.${DOMAIN}"
        echo "   TestSite:         https://${CODESPACE_NAME}-44345.${DOMAIN}"
    fi
    exit 0
fi

echo "🚀 Starting Umbraco Prism stack..."
echo ""

# In Codespaces, the Aspire Dashboard uses token-based auth by default.
# The Codespaces port proxy intercepts unauthenticated requests and returns
# HTML instead of JS files (causing MIME-type errors in the browser).
# Setting this env var disables that auth — the port is already public-visibility-gated.
if [ -n "$CODESPACE_NAME" ]; then
    export DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true
    DOMAIN="${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}"
fi

# Docker-in-Docker takes a few seconds to start after Codespace boot.
echo "⏳ Waiting for Docker..."
for i in $(seq 1 30); do
    if docker info > /dev/null 2>&1; then
        echo "✅ Docker ready."
        break
    fi
    sleep 2
    if [ "$i" -eq 30 ]; then
        echo "❌ Docker did not start. Check the docker-in-docker feature."
        exit 1
    fi
done

# Start AppHost in the background; logs go to /tmp/prism-apphost.log.
echo ""
echo "🔄 Launching AppHost (full logs: /tmp/prism-apphost.log)..."
nohup dotnet run --project src/UmbracoPrism.AppHost > /tmp/prism-apphost.log 2>&1 &

# Poll for the Aspire Dashboard (faster to come up).
echo -n "   Aspire Dashboard  "
READY=false
for i in $(seq 1 60); do
    if curl -sk --max-time 2 https://localhost:17214 > /dev/null 2>&1; then
        echo " ✅"
        READY=true
        break
    fi
    echo -n "."
    sleep 3
done
if [ "$READY" = false ]; then
    echo " ⏱️"
fi

# Poll for the TestSite — takes longer (Umbraco bootstrap + SQLite migrations).
echo -n "   TestSite          "
READY=false
for i in $(seq 1 100); do
    if curl -sk --max-time 2 https://localhost:44345 > /dev/null 2>&1; then
        echo " ✅"
        READY=true
        break
    fi
    echo -n "."
    sleep 3
done
if [ "$READY" = false ]; then
    echo " ⏱️  (still starting — check /tmp/prism-apphost.log)"
fi

echo ""
echo "🎉 Umbraco Prism is ready!"
echo ""

if [ -n "$CODESPACE_NAME" ]; then
    echo "   Aspire Dashboard  https://${CODESPACE_NAME}-17214.${DOMAIN}"
    echo "   TestSite          https://${CODESPACE_NAME}-44345.${DOMAIN}"
    echo "   Keycloak admin    https://${CODESPACE_NAME}-8443.${DOMAIN}/admin"
else
    echo "   Aspire Dashboard  https://localhost:17214"
    echo "   TestSite          https://localhost:44345"
    echo "   Keycloak admin    https://localhost:8443/admin"
fi

echo ""
echo "   TestSite SSO      demo@prism.local  /  password"
echo "   Backoffice        admin@prism.local  /  PrismLocal!12345"
echo "   Keycloak admin    admin  /  admin"
echo ""
