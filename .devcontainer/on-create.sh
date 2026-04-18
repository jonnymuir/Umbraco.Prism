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
