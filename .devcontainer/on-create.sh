#!/bin/bash
set -e

echo "🚀 Setting up Umbraco Prism dev environment..."

# Trust the .NET development certificate
echo "🔐 Trusting .NET development certificate..."
dotnet dev-certs https --trust || true

# Restore .NET packages
echo "📦 Restoring .NET packages..."
dotnet restore UmbracoPrism.sln

# Pre-warm the build cache so the first `on-start.sh` run is faster
echo "🔨 Building solution (pre-warming compilation cache)..."
dotnet build UmbracoPrism.sln --no-restore

# Install Node.js frontend dependencies
echo "📦 Installing Node.js dependencies..."
cd src/UmbracoPrism.Client && npm install && cd ../..

# When running in GitHub Codespaces, export the public-facing Keycloak URL so
# the Aspire AppHost and DemoTenantSeeder can use the Codespace-forwarded address
# instead of localhost (browsers inside a Codespace can't reach localhost:8443).
if [ -n "$CODESPACE_NAME" ]; then
  DOMAIN="${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}"
  KEYCLOAK_URL="https://${CODESPACE_NAME}-8443.${DOMAIN}"
  echo "🌐 Codespaces detected — setting KEYCLOAK_URL=${KEYCLOAK_URL}"
  echo "export KEYCLOAK_URL=${KEYCLOAK_URL}" >> ~/.bashrc
  echo "export KEYCLOAK_URL=${KEYCLOAK_URL}" >> ~/.profile
fi

echo ""
echo "✅ Setup complete!"
echo ""
echo "   Start the full stack:"
echo "   dotnet run --project src/UmbracoPrism.AppHost"
echo ""
echo "   Then open the Aspire Dashboard at https://localhost:17214"
echo "   Log in with  demo@prism.local / password"

# Attempt to open the welcome file in VS Code.
# postCreateCommand runs before VS Code is fully attached, so the code CLI
# may not be available — that's fine, on-start.sh tries again each start.
if [ -n "$CODESPACE_NAME" ]; then
    echo ""
    echo "ℹ️  [on-create] Attempting to open CODESPACES.md in VS Code editor..."
    echo "   which code:      $(which code 2>/dev/null || echo 'NOT FOUND in PATH')"
    echo "   VSCODE_IPC_HOOK: ${VSCODE_IPC_HOOK:-not set}"
    echo "   TERM_PROGRAM:    ${TERM_PROGRAM:-not set}"
    if code CODESPACES.md 2>&1; then
        echo "   ✅ code command exited 0"
    else
        echo "   ⚠️  code command exited $? — expected if VS Code not yet attached"
    fi
fi
